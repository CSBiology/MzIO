namespace MzIO.IO.MzML


open System
open System.Collections.Generic
open System.Linq
open System.IO
open System.IO.Compression
open System.Xml
open System.Threading.Tasks
open FSharp.Core
open NumpressHelper
open MzIO.Model
open MzIO.Model.Helper
open MzIO.Model.CvParam
open MzIO.Commons.Arrays
open MzIO.Binary
open MzIO.IO
open MzIO.Json
open MzIO.Processing
open MzIO.Processing.MzIOLinq


type private MzMLReaderTransactionScope() =

    interface ITransactionScope with

        /// Does Nothing.
        member this.Commit() =
            ()

        /// Does Nothing.
        member this.Rollback() =
            ()

    /// Does Nothing.
    member this.Commit() =

        (this :> ITransactionScope).Commit()

    /// Does Nothing.
    member this.Rollback() =

        (this :> ITransactionScope).Rollback()

    interface IDisposable with
        
        member this.Dispose() =
            ()

    member this.Dispose() =

        (this :> IDisposable).Dispose()

// Use reader.ReadSubtree() in order to avoid moving into an element of the same or higher level.
/// Contains methods to access spectrum and peak information of mzml files.
type MzMLReader(filePath: string) =

    let mutable reader = 
        let tmp = XmlReader.Create(filePath)
        tmp.MoveToContent() |> ignore
        if tmp.Name = "indexedmzML" then
            tmp.ReadToDescendant("mzML")
        else
            false
        |> ignore
        tmp

    /// Tries to return attribute element from XML element as string.
    member private this.tryGetAttribute (name:string, ?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let tmp = xmlReader.GetAttribute(name)
        if tmp=null then None
        else Some tmp

    /// Gets attribute element from XML element as string.
    member private this.getAttribute (name:string, ?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        xmlReader.GetAttribute(name)

    /// Creates CVParam object based on cvParam element.
    member private this.getCVParam (?xmlReader:XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let potValue =
            match (this.tryGetAttribute ("value", xmlReader)) with
            | Some value ->
                match (this.tryGetAttribute ("unitAccession", xmlReader)) with
                | Some unitAcc  -> Some (ParamValue.WithCvUnitAccession(value, unitAcc))
                | None          -> Some (ParamValue.CvValue(value))
            | None      ->
                match (this.tryGetAttribute ("unitAccession", xmlReader)) with
                | Some unitAcc  -> Some (ParamValue.WithCvUnitAccession(null, unitAcc))
                | None          -> None
        match potValue with
        | Some value -> CvParam<string>((this.getAttribute ("accession", xmlReader)), value)
        | None -> CvParam<string>((this.getAttribute ("accession", xmlReader)))

    /// Creates UserParam object based on userParam element.
    member private this.getUserParam (?xmlReader:XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let potValue =
            match (this.tryGetAttribute ("value", xmlReader)) with
            | Some value ->
                match (this.tryGetAttribute ("unitAccession", xmlReader)) with
                | Some unitAcc  -> Some (ParamValue.WithCvUnitAccession(value, unitAcc))
                | None          -> Some (ParamValue.CvValue(value))
            | None      ->
                match (this.tryGetAttribute ("unitAccession", xmlReader)) with
                | Some unitAcc  -> Some (ParamValue.WithCvUnitAccession(null, unitAcc))
                | None          -> None
        match potValue with
        | Some value -> UserParam<string>((this.getAttribute ("name", xmlReader)), value)
        | None -> UserParam<string>((this.getAttribute ("name", xmlReader)))

    /// Moves to content of mzML and checks if it is an indexed mzML or not. When it is an indexedmzML it moves to the mzML Element.
    // Additional Information: https://csharp.hotexamples.com/examples/-/MzML_Version/-/php-mzml_version-class-examples.html
    static member private accessMzMLElement (reader: XmlReader) =
        reader.MoveToContent() |> ignore
        if reader.Name = "indexedmzML" then
            reader.ReadToDescendant("mzML")
        else
            false
    
    /// Check for mzML Schema
    static member private checkSchema (reader: XmlReader) =
        let schemaName = reader.GetAttribute("xsi:schemaLocation")
        if (schemaName.Contains("mzML1.0.0.xsd")) then
            failwith "mzML1.0.0 is not supported"

    /// Gets kind of binary array elements (intensity; m/z) based on cvParam elements.
    static member private getArrayTypeOfP1D (peakArray:Peak1DArray) (arrayType:BinaryDataType) (keys:seq<string>) =
        for key in keys do
            match key with
            // M/Z Array
            | "MS:1000514" ->   peakArray.RemoveItem(key)
                                peakArray.MzDataType <- arrayType
            // IntensityArray
            | "MS:1000515" ->   peakArray.RemoveItem(key)
                                peakArray.IntensityDataType <- arrayType
            | _            -> ()

    /// Gets data type of binary array elements based on cvParam elements.
    static member private getBinaryDataTypeOfP1D (peakArray:Peak1DArray) (keys:seq<string>) =
        for key in keys do
            match key with
            // Float32
            | "MS:1000521"  ->  peakArray.RemoveItem(key)
                                MzMLReader.getArrayTypeOfP1D peakArray BinaryDataType.Float32 keys
            // Float64
            | "MS:1000523"  ->  peakArray.RemoveItem(key)
                                MzMLReader.getArrayTypeOfP1D peakArray BinaryDataType.Float64 keys
            // Int32
            | "MS:1000519"  ->  peakArray.RemoveItem(key)
                                MzMLReader.getArrayTypeOfP1D peakArray BinaryDataType.Int32 keys
            // Int64
            | "MS:1000522"  ->  peakArray.RemoveItem(key)
                                MzMLReader.getArrayTypeOfP1D peakArray BinaryDataType.Int64 keys
            | _             ->  ()

    /// Gets compression type of binary array elements based on cvParam elements.
    static member private getCompressionTypeOfP1D (peakArray:Peak1DArray) (keys:seq<string>) =
        for key in keys do
            match key with
            // NoCompression
            | "MS:1000576"  ->  peakArray.RemoveItem(key)
                                peakArray.CompressionType <- BinaryDataCompressionType.NoCompression
                                MzMLReader.getBinaryDataTypeOfP1D peakArray keys
            // ZlibCompression
            | "MS:1000574"  ->  peakArray.RemoveItem(key)
                                peakArray.CompressionType <- BinaryDataCompressionType.ZLib
                                MzMLReader.getBinaryDataTypeOfP1D peakArray keys
            | _             -> ()

    /// Gets compression type of binary array elements based on cvParam elements.
    static member private getCompressionTypeOfP1D' (peakArray:Peak1DArray) (keys:seq<string>) =
        if Seq.contains "MS:1002314" keys && Seq.contains "MS:1002313" keys && Seq.contains "MS:1000574" keys then
            peakArray.RemoveItem("MS:1002314")
            peakArray.RemoveItem("MS:1002313")
            peakArray.RemoveItem("MS:1000574")
            peakArray.CompressionType <- BinaryDataCompressionType.NumPressZLib
            MzMLReader.getBinaryDataTypeOfP1D peakArray keys
        else 
            if Seq.contains "MS:1002314" keys && Seq.contains "MS:1002313" keys then
                peakArray.RemoveItem("MS:1002314")
                peakArray.RemoveItem("MS:1002313")
                peakArray.CompressionType <- BinaryDataCompressionType.NumPress
                MzMLReader.getBinaryDataTypeOfP1D peakArray keys
            else
                if Seq.contains "MS:1000576" keys then
                    peakArray.RemoveItem("MS:1000576")
                    peakArray.CompressionType <- BinaryDataCompressionType.NoCompression
                    MzMLReader.getBinaryDataTypeOfP1D peakArray keys
                else
                    if Seq.contains "MS:1000574" keys then
                        peakArray.RemoveItem("MS:1000574")
                        peakArray.CompressionType <- BinaryDataCompressionType.ZLib
                        MzMLReader.getBinaryDataTypeOfP1D peakArray keys
                    else
                        failwith "No supported Compression Type"

    /// Adds compression type to Peak1DArray.
    static member private addCompressionTypeToPeak1DArray (peakArray:Peak1DArray) =
        peakArray.GetProperties false
        |> Seq.map (fun pair -> pair.Key)
        |> MzMLReader.getCompressionTypeOfP1D' peakArray
        peakArray

    /// Gets kind of binary array elements (intensity; m/z; retention time) based on cvParam elements.
    static member private getArrayTypeOfP2D (peakArray:Peak2DArray) (arrayType:BinaryDataType) (keys:string []) =
        for key in keys do
            match key with
            //M/Z Array
            | "MS:1000514" ->   peakArray.RemoveItem(key)
                                peakArray.MzDataType <- arrayType
            //IntensityArray
            | "MS:1000515" ->   peakArray.RemoveItem(key)
                                peakArray.IntensityDataType <- arrayType
            //RetentionTimeArray
            | "MS:1000595" ->   peakArray.RemoveItem(key)
                                peakArray.RtDataType <- arrayType
            | _            -> ()

    /// Gets data type of binary array elements based on cvParam elements.
    static member private getBinaryDataTypeOfP2D (peakArray:Peak2DArray) (keys:string []) =
        for key in keys do
            match key with
            // Float32
            | "MS:1000521"  ->  peakArray.RemoveItem(key)
                                MzMLReader.getArrayTypeOfP2D peakArray BinaryDataType.Float32 keys
            // Float64
            | "MS:1000523"  ->  peakArray.RemoveItem(key)
                                MzMLReader.getArrayTypeOfP2D peakArray BinaryDataType.Float64 keys
            // Int32
            | "MS:1000519"  ->  peakArray.RemoveItem(key)
                                MzMLReader.getArrayTypeOfP2D peakArray BinaryDataType.Int32 keys
            // Int64
            | "MS:1000522"  ->  peakArray.RemoveItem(key)
                                MzMLReader.getArrayTypeOfP2D peakArray BinaryDataType.Int64 keys
            | _             ->  ()

    /// Gets compression type of binary array elements based on cvParam elements.
    static member private getCompressionTypeOfP2D (peakArray:Peak2DArray) (keys:string []) =
        for key in keys do
            match key with
            // NoCompression
            | "MS:1000576"  ->  peakArray.RemoveItem(key)
                                peakArray.CompressionType <- BinaryDataCompressionType.NoCompression
                                MzMLReader.getBinaryDataTypeOfP2D peakArray keys
            // ZlibCompression
            | "MS:1000574"  ->  peakArray.RemoveItem(key)
                                peakArray.CompressionType <- BinaryDataCompressionType.ZLib
                                MzMLReader.getBinaryDataTypeOfP2D peakArray keys
            | _             ->  ()

    // Adds compression type to Peak2DArray.
    static member private addCompressionTypeToPeak2DArray (peakArray:Peak2DArray) =
        peakArray.GetProperties false
        |> Seq.map (fun pair -> pair.Key)
        |> Array.ofSeq
        |> MzMLReader.getCompressionTypeOfP2D peakArray
        peakArray

    /// Converts bytes to singles.
    static member byteToSingles (littleEndian:Boolean) (byteArray: byte[]) =
        match littleEndian with
        | false ->  let floatArray = Array.init (byteArray.Length/4) (fun x -> 0. |> float32)                    
                    Buffer.BlockCopy  (byteArray, 0, floatArray, 0, byteArray.Length)
                    floatArray
        | true  ->  let floatArray = Array.init (byteArray.Length/4) (fun x -> 0. |> float32)
                    Buffer.BlockCopy  (Array.rev byteArray, 0, floatArray, 0, byteArray.Length)
                    Array.rev floatArray

    /// Converts bytes to doubles.
    static member private byteToDoubles (littleEndian:Boolean) (byteArray: byte[]) =
        match littleEndian with
        | false ->  let floatArray = Array.init (byteArray.Length/8) (fun x -> 0.)
                    Buffer.BlockCopy (byteArray, 0, floatArray, 0, byteArray.Length)
                    floatArray
        | true  ->  let floatArray = Array.init (byteArray.Length/8) (fun x -> 0.)
                    Buffer.BlockCopy (Array.rev byteArray, 0, floatArray, 0, byteArray.Length)
                    floatArray
    
    /// Convert byte array to float array.
    static member private ByteToFloatArray (byteArray: byte[]) =
        let floatArray = Array.init (byteArray.Length/8) (fun x -> 0.)
        Buffer.BlockCopy  (byteArray, 0, floatArray, 0, byteArray.Length)
        floatArray

    /// Decompress stream and convert to new byte array.
    static member private DeflateStreamDecompress (data: byte[]) =
        let outStream = new MemoryStream()
        let decompress = new System.IO.Compression.DeflateStream (new MemoryStream(data), CompressionMode.Decompress)
        decompress.CopyTo(outStream)
        let byteArray = outStream.ToArray()
        byteArray

    /// Remove 1st two bytes because they are the header of zLib compression
    static member private decompressZlib (data:byte []) =
        let buffer = Array.skip 2 data
        let memoryStream = new MemoryStream ()
        memoryStream.Write(buffer, 0, Array.length buffer)
        memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
        let deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress, true)
        let outerMemoryStream = new MemoryStream()
        deflateStream.CopyTo(outerMemoryStream)
        outerMemoryStream.ToArray()

    /// Decompress bytes based on numpress decompression method and convert to Peak1DArray.
    static member private decodeIntensitiesByPIC(data:byte []) =
        let memoryStream            = new MemoryStream(data)
        let reader                  = new BinaryReader(memoryStream, System.Text.Encoding.UTF8, true)
        //read information for decoding of intensities
        let numberEncodedBytesInt   = reader.ReadInt32()
        let originalDataLengthInt   = reader.ReadInt32()
        let byteArrayInt            = reader.ReadBytes(numberEncodedBytesInt + 5)
        NumpressDecodingHelpers.decodePIC (byteArrayInt, numberEncodedBytesInt, originalDataLengthInt)

    /// Decompress bytes based on numpress decompression method and convert to Peak1DArray.
    static member private decodeMZsByLin(data:byte []) =
        let memoryStream            = new MemoryStream(data)
        let reader                  = new BinaryReader(memoryStream, System.Text.Encoding.UTF8, true)
        //read information for decoding of m/z
        let numberEncodedBytesMz    = reader.ReadInt32()
        let originalDataLengthMz    = reader.ReadInt32()
        let byteArrayMz             = reader.ReadBytes(numberEncodedBytesMz + 5)
        NumpressDecodingHelpers.decodeLin (byteArrayMz, numberEncodedBytesMz, originalDataLengthMz)

    /// Get binaryArray element as string.
    member private this.getBinary (?xmlReader: XmlReader)=
        let xmlReader = defaultArg xmlReader reader
        xmlReader.ReadElementContentAsString()

    /// Checks and decompresses the peaks based on the compression type and adds it to the Peak1DArray.
    static member private get1DPeaks (convertedPeaks:string list) (peakArray:Peak1DArray) =
        if convertedPeaks.Head.Length <> 0 then
            match peakArray.CompressionType with
            | BinaryDataCompressionType.NoCompression ->
                match peakArray.MzDataType with
                | BinaryDataType.Float32 ->
                    let mzs =
                        Convert.FromBase64String(convertedPeaks.[1])
                        |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | _ -> failwith "No compelement Type"
                | BinaryDataType.Float64 ->
                    let mzs =
                        Convert.FromBase64String(convertedPeaks.[1])
                        |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | _ -> failwith "No compelement Type"
                | _ -> failwith "No compelement Type"
            | BinaryDataCompressionType.ZLib ->
                match peakArray.MzDataType with
                | BinaryDataType.Float32 ->
                    let mzs =
                        Convert.FromBase64String(convertedPeaks.[1])
                        |> MzMLReader.decompressZlib
                        |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decompressZlib
                            |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decompressZlib
                            |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                        | _ -> failwith "No compelement Type"
                | BinaryDataType.Float64 ->
                    let mzs =
                        Convert.FromBase64String(convertedPeaks.[1])
                        |> MzMLReader.decompressZlib
                        |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decompressZlib
                            |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decompressZlib
                            |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                        | _ -> failwith "No compelement Type"
                | _ -> failwith "No compelement Type"
            | BinaryDataCompressionType.NumPress ->
                match peakArray.MzDataType with
                | BinaryDataType.Float32 ->
                    let mzs =
                        Convert.FromBase64String(convertedPeaks.[1])
                        |> MzMLReader.decodeMZsByLin
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decodeIntensitiesByPIC
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decodeIntensitiesByPIC
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                        | _ -> failwith "No compelement Type"
                | BinaryDataType.Float64 ->
                    let mzs =
                        Convert.FromBase64String(convertedPeaks.[1])
                        |> MzMLReader.decodeMZsByLin
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decodeIntensitiesByPIC
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decodeIntensitiesByPIC
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                        | _ -> failwith "No compelement Type"
                | _ -> failwith "No compelement Type"
            | BinaryDataCompressionType.NumPressZLib ->
                match peakArray.MzDataType with
                | BinaryDataType.Float32 ->
                    let mzs =
                        Convert.FromBase64String(convertedPeaks.[1])
                        |> MzMLReader.decompressZlib
                        |> MzMLReader.decodeMZsByLin
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decompressZlib
                            |> MzMLReader.decodeIntensitiesByPIC
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decompressZlib
                            |> MzMLReader.decodeIntensitiesByPIC
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                        | _ -> failwith "No compelement Type"
                | BinaryDataType.Float64 ->
                    let mzs =
                        Convert.FromBase64String(convertedPeaks.[1])
                        |> MzMLReader.decompressZlib
                        |> MzMLReader.decodeMZsByLin
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decompressZlib
                            |> MzMLReader.decodeIntensitiesByPIC
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(convertedPeaks.[0])
                            |> MzMLReader.decompressZlib
                            |> MzMLReader.decodeIntensitiesByPIC
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                        | _ -> failwith "No compelement Type"
                | _ -> failwith "No compelement Type"
            | _ -> failwith "No compelement Type"
        else
            ()

    /// Checks and decompresses the peaks based on the compression type and adds it to the Peak2DArray.
    static member private get2DPeaks (peaks:string list) (peakArray:Peak2DArray) =
        match peakArray.CompressionType with
        | BinaryDataCompressionType.NoCompression ->
            match peakArray.RtDataType with
            | BinaryDataType.Float32 ->
                let rts =
                    Convert.FromBase64String(peaks.[1])
                    |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                match peakArray.IntensityDataType with
                | BinaryDataType.Float32 ->
                    let intensities =
                        Convert.FromBase64String(peaks.[0])
                        |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                    let peaks = Array.map2 (fun rt int -> new Peak2D(float int, -1., float rt)) rts intensities
                    peakArray.Peaks <- ArrayWrapper(peaks)
                | BinaryDataType.Float64 ->
                    let intensities =
                        Convert.FromBase64String(peaks.[0])
                        |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                    let peaks = Array.map2 (fun rt int -> new Peak2D(int, -1., float rt)) rts intensities
                    peakArray.Peaks <- ArrayWrapper(peaks)
                    | _ -> failwith "No compelement Type"
            | BinaryDataType.Float64 ->
                let rts =
                    Convert.FromBase64String(peaks.[1])
                    |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                match peakArray.IntensityDataType with
                | BinaryDataType.Float64 ->
                    let intensities =
                        Convert.FromBase64String(peaks.[0])
                        |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                    let peaks = Array.map2 (fun rt int -> new Peak2D(int, -1., rt)) rts intensities
                    peakArray.Peaks <- ArrayWrapper(peaks)
                | BinaryDataType.Float32 ->
                    let intensities =
                        Convert.FromBase64String(peaks.[0])
                        |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                    let peaks = Array.map2 (fun rt int -> new Peak2D(float int, -1., rt)) rts intensities
                    peakArray.Peaks <- ArrayWrapper(peaks)
                    | _ -> failwith "No compelement Type"
            | _ -> failwith "No compelement Type"
        | BinaryDataCompressionType.ZLib ->
            match peakArray.RtDataType with
            | BinaryDataType.Float32 ->
                let rts =
                    Convert.FromBase64String(peaks.[1])
                    |> MzMLReader.decompressZlib
                    |> (fun bytes -> MzMLReader.byteToSingles true bytes)
                match peakArray.IntensityDataType with
                | BinaryDataType.Float32 ->
                    let intensities =
                        Convert.FromBase64String(peaks.[0])
                        |> MzMLReader.decompressZlib
                        |> (fun bytes -> MzMLReader.byteToSingles true bytes)
                    let peaks = Array.map2 (fun rt int -> new Peak2D(float int, -1., float rt)) rts intensities
                    peakArray.Peaks <- ArrayWrapper(peaks)
                | BinaryDataType.Float64 ->
                    let intensities =
                        Convert.FromBase64String(peaks.[0])
                        |> MzMLReader.decompressZlib
                        |> (fun bytes -> MzMLReader.byteToDoubles true bytes)
                    let peaks = Array.map2 (fun rt int -> new Peak2D(int, -1., float rt)) rts intensities
                    peakArray.Peaks <- ArrayWrapper(peaks)
                    | _ -> failwith "No compelement Type"
            | BinaryDataType.Float64 ->
                let rts =
                    Convert.FromBase64String(peaks.[1])
                    |> MzMLReader.decompressZlib
                    |> (fun bytes -> MzMLReader.byteToDoubles true bytes)
                match peakArray.IntensityDataType with
                | BinaryDataType.Float64 ->
                    let intensities =
                        Convert.FromBase64String(peaks.[0])
                        |> MzMLReader.decompressZlib
                        |> (fun bytes -> MzMLReader.byteToDoubles true bytes)
                    let peaks = Array.map2 (fun rt int -> new Peak2D(int, -1., rt)) rts intensities
                    peakArray.Peaks <- ArrayWrapper(peaks)
                | BinaryDataType.Float32 ->
                    let intensities =
                        Convert.FromBase64String(peaks.[0])
                        |> MzMLReader.decompressZlib
                        |> (fun bytes -> MzMLReader.byteToSingles true bytes)
                    let peaks = Array.map2 (fun rt int -> new Peak2D(float int, -1., rt)) rts intensities
                    peakArray.Peaks <- ArrayWrapper(peaks)
                    | _ -> failwith "No compelement Type"
            | _ -> failwith "No compelement Type"
        | _ -> failwith "No compelement Type"

    /// Gets DataProcessing element from XML and adds it directly to the MassSpectrum.
    member private this.getDataProcessingReference (spectrum:MassSpectrum, ?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree = xmlReader.ReadSubtree()
        let readOp = readSubtree.Read
        let rec loop dbProcRef cvParams peaks read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "binaryDataArray" ->  loop (this.tryGetAttribute ("dataProcessingRef", readSubtree)) cvParams peaks (readOp() |> ignore)
                | _                 ->  loop dbProcRef cvParams peaks (readOp() |> ignore)
            else
                if readOp()=true then loop dbProcRef cvParams peaks read
                else
                    match dbProcRef with
                    | Some ref  ->  (spectrum :> PeakList).DataProcessingReference <- ref
                    | None      ->  ()
        loop None () [] ()

    /// Creates Peak1DArray based on spectrum, BinaryArrayList and cvParam elements.
    member private this.createPeak1DArray(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree = xmlReader.ReadSubtree()
        let readOp = readSubtree.Read
        let peakArray = new Peak1DArray()
        let rec loop dbProcRef cvParams peaks read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "referenceableParamGroupRef"  ->  loop dbProcRef cvParams peaks (readOp() |> ignore)
                | "cvParam"                     ->  loop dbProcRef (peakArray.AddCvParam(this.getCVParam readSubtree)) peaks (readOp() |> ignore)
                | "userParam"                   ->  loop dbProcRef (peakArray.AddUserParam(this.getUserParam readSubtree)) peaks (readOp() |> ignore)
                | "binary"                      ->  loop dbProcRef cvParams ((this.getBinary readSubtree)::peaks) read
                | _                             ->  loop dbProcRef cvParams peaks (readOp() |> ignore)
            elif readSubtree.NodeType= XmlNodeType.EndElement && readSubtree.Name="binaryDataArray" then
                match dbProcRef with
                | Some ref  ->  MzMLReader.addCompressionTypeToPeak1DArray peakArray |> ignore
                | None      ->  MzMLReader.addCompressionTypeToPeak1DArray peakArray |> ignore
                loop dbProcRef cvParams peaks (readOp() |> ignore)
            else
                if readOp()=true then 
                    loop dbProcRef cvParams peaks read
                else
                    match dbProcRef with
                    | Some ref  ->  
                        MzMLReader.get1DPeaks peaks peakArray
                        peakArray
                    | None      ->  
                        MzMLReader.get1DPeaks peaks peakArray
                        peakArray
        loop None () [] ()

    /// Creates Peak2DArray based on chromatogram, BinaryArrayList and cvParam elements.
    /// Does not work properly yet.
    member private this.createPeak2DArray(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree = xmlReader.ReadSubtree()
        let readOp = readSubtree.Read
        let peakArray = new Peak2DArray()
        let rec loop dbProcRef cvParams peaks read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "referenceableParamGroupRef"  ->  loop dbProcRef cvParams peaks (readOp() |> ignore)
                | "cvParam"                     ->  loop dbProcRef (peakArray.AddCvParam(this.getCVParam readSubtree)) peaks (readOp() |> ignore)
                | "userParam"                   ->  loop dbProcRef (peakArray.AddUserParam(this.getUserParam readSubtree)) peaks (readOp() |> ignore)
                | "binary"                      ->  loop dbProcRef cvParams ((this.getBinary readSubtree)::peaks) read
                | _                             ->  loop dbProcRef cvParams peaks (readOp() |> ignore)
            else
                if readOp()=true then loop dbProcRef cvParams peaks read
                else
                    match dbProcRef with
                    | Some ref  ->  MzMLReader.addCompressionTypeToPeak2DArray peakArray
                                    |> MzMLReader.get2DPeaks peaks
                                    peakArray
                    | None      ->  MzMLReader.addCompressionTypeToPeak2DArray peakArray
                                    |> MzMLReader.get2DPeaks peaks
                                    peakArray
        loop None () [] ()

    /// Creates ScanWindow object based on scanWindow element.
    member private this.getScanWindow (?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            //acc not used? Only here to call loop with reader.Read?
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="scanWindow" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let scanWindow = new ScanWindow()
        let rec loop addParam read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "scanWindow"  -> loop addParam (readOp() |> ignore)
                | "cvParam"     -> loop (scanWindow.AddCvParam(this.getCVParam readSubtree)) (readOp() |> ignore)
                | "userParam"   -> loop (scanWindow.AddUserParam(this.getUserParam readSubtree)) (readOp() |> ignore)
                |   _           -> loop addParam (readOp() |> ignore)
            else
                if readOp()=true then loop addParam read
                else scanWindow
        loop () ()

    /// Creates ScanWindowList object based on scanWindowList element and adds ScanWindow objects.
    member private this.getScanWindowList (?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="scanWindowList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let scanWindowList = new ScanWindowList()
        let readOp = readSubtree.Read
        let rec loop addParam read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "scanWindow"  ->  loop (scanWindowList.Add(Guid.NewGuid().ToString(), this.getScanWindow readSubtree)) (readOp() |> ignore)
                |   _           ->  loop addParam (readOp() |> ignore)
            else
                if readOp()=true then loop addParam read
                else scanWindowList
        loop () ()

    /// Creates Scan object based on scan element.
    member private this.getScan (?xmlReader:XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree = 
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="scan" then
                    xmlReader.ReadSubtree()
                else loop (reader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop spectrumID sourceFileID scanWindows cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "scan"            ->  loop (this.tryGetAttribute ("spectrumRef", readSubtree)) (this.tryGetAttribute("sourceFileRef", readSubtree)) scanWindows cvParams userParams (readOp() |> ignore)
                | "cvParam"         ->  loop spectrumID sourceFileID scanWindows ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"       ->  loop spectrumID sourceFileID scanWindows cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                | "scanWindowList"  ->  loop spectrumID sourceFileID (this.getScanWindowList reader) cvParams userParams (readOp() |> ignore)
                |   _               ->  loop spectrumID sourceFileID scanWindows cvParams userParams (readOp() |> ignore)
            else 
                if readOp()=true then loop spectrumID sourceFileID scanWindows cvParams userParams read
                else
                    let scan =
                        new Scan(
                                new SpectrumReference(
                                    (if sourceFileID.IsNone then null else sourceFileID.Value), 
                                    (if spectrumID.IsNone then null else spectrumID.Value)
                                                        ), scanWindows
                                )
                    cvParams
                    |> Seq.iter (fun cvParam -> scan.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> scan.AddUserParam userParam)
                    scan
        loop None None (new ScanWindowList()) [] [] ()

    /// Creates ScanList object based on scanList element and adds Scan objects.
    member private this.getScanList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="scanList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let scanList = new ScanList()
        let rec loop cvParams userParams scans read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "cvParam"         ->  loop (scanList.AddCvParam(this.getCVParam readSubtree)) userParams scans (readOp() |> ignore)
                | "userParam"       ->  loop cvParams (scanList.AddUserParam(this.getUserParam readSubtree)) scans (readOp() |> ignore)
                | "scan"    ->  loop cvParams userParams (scanList.Add(Guid.NewGuid().ToString(), this.getScan readSubtree)) (readOp() |> ignore)
                |   _       ->  loop cvParams userParams scans (readOp() |> ignore)
            else
                if readOp()=true then loop cvParams userParams scans read
                else 
                    scanList
        loop () () () ()

    /// Creates Activation object based on activation element.
    member private this.getActivation(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="activation" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let activation = new Activation()
        let readOp = readSubtree.Read
        let rec loop addParam read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "cvParam"     -> loop (activation.AddCvParam(this.getCVParam readSubtree)) (readOp() |> ignore)
                | "userParam"   -> loop (activation.AddUserParam(this.getUserParam readSubtree)) (readOp() |> ignore)
                |   _           ->  loop addParam (readOp() |> ignore)
            else
                if readOp()=true then loop addParam read
                else activation
        loop () ()

    /// Creates SelectedIon object based on selectedIon element.
    member private this.getSelectedIon (?xmlReader: XmlReader)=
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="selectedIon" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let selectedIon = new SelectedIon()
        let rec loop addParam read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "selectedIon" -> loop addParam (readOp() |> ignore)
                | "cvParam"     -> loop (selectedIon.AddCvParam(this.getCVParam readSubtree)) (readOp() |> ignore)
                | "userParam"   -> loop (selectedIon.AddUserParam(this.getUserParam readSubtree)) (readOp() |> ignore)
                |   _           -> loop addParam (readOp() |> ignore)
            else
                if readOp()=true then loop addParam read
                else selectedIon
        loop () ()

    /// Creates SelectedIonList object based on selectedIon element and adds selectedIon objects.
    member private this.getSelectedIonList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="selectedIonList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let selectedIonList = new SelectedIonList()
        let readOp = readSubtree.Read
        let rec loop addParam read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "selectedIon" ->  loop (selectedIonList.Add(Guid.NewGuid().ToString(), this.getSelectedIon readSubtree)) (readOp() |> ignore)
                |   _           ->  loop addParam (readOp() |> ignore)
            else
                if readOp()=true then loop addParam read
                else selectedIonList
        loop () ()

    /// Creates IsolationWindow object based on isolationWindow element.
    member private this.getIsolationWindow(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="isolationWindow" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let isolationWindow = new IsolationWindow()
        let readOp = readSubtree.Read
        let rec loop addParam read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "cvParam"     -> loop (isolationWindow.AddCvParam(this.getCVParam readSubtree)) (readOp() |> ignore)
                | "userParam"   -> loop (isolationWindow.AddUserParam(this.getUserParam readSubtree)) (readOp() |> ignore)
                |   _           ->  loop addParam (readOp() |> ignore)
            else
                if readOp()=true then loop addParam read
                else isolationWindow
        loop () ()

    /// Creates PRecursor object based on precursor element.
    member private this.getPrecursor(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="precursor" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop spectrumID sourceFileID isolationWindow selectedIonList activation read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "precursor"       ->  loop (this.tryGetAttribute ("spectrumRef", readSubtree)) (this.tryGetAttribute ("sourceFileRef", readSubtree)) isolationWindow selectedIonList activation (readOp() |> ignore)
                | "isolationWindow" ->  loop spectrumID sourceFileID (this.getIsolationWindow readSubtree) selectedIonList activation (readOp() |> ignore)
                | "selectedIonList" ->  loop spectrumID sourceFileID isolationWindow (this.getSelectedIonList readSubtree) activation (readOp() |> ignore)
                | "activation"      ->  loop spectrumID sourceFileID isolationWindow selectedIonList (this.getActivation readSubtree) (readOp() |> ignore)
                |   _               ->  loop spectrumID sourceFileID isolationWindow selectedIonList activation (readOp() |> ignore)
            else
                if readOp()=true then loop spectrumID sourceFileID isolationWindow selectedIonList activation read
                else
                    let precursor =
                        new Precursor(
                                new SpectrumReference(
                                    (if sourceFileID.IsNone then null else sourceFileID.Value),
                                    (if spectrumID.IsNone then null else spectrumID.Value)
                                                        ),
                                        isolationWindow, selectedIonList, activation
                                        )
                    precursor
        loop None None (new IsolationWindow()) (new SelectedIonList()) (new Activation()) ()

    /// Creates PrecursorList object based on precursorList element and adds Precursor objects.
    member private this.getPrecursorList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="precursorList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let precursorList = new PrecursorList()
        let rec loop precursors read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "precursor"    ->  loop (precursorList.Add(Guid.NewGuid().ToString(), this.getPrecursor readSubtree)) (readOp() |> ignore)
                |   _       ->  loop precursors (readOp() |> ignore)
            else
                if readOp()=true then loop precursors read
                else precursorList
        loop () ()

    /// Creates Product object based on product element.
    member private this.getProduct(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="product" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop isolationWindow read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "isolationWindow" ->  loop (this.getIsolationWindow readSubtree) (readOp() |> ignore)
                |   _               ->  loop isolationWindow (readOp() |> ignore)
            else
                if readOp()=true then loop isolationWindow read
                else
                    let product =
                        new Product(isolationWindow)
                    product
        loop (new IsolationWindow()) ()

    /// Creates ProductList object based on productList element and adds Product objects.
    member private this.getProductList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="productList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let productList = new ProductList()
        let rec loop products read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "product" ->  loop (productList.Add(Guid.NewGuid().ToString(), this.getProduct readSubtree)) (readOp() |> ignore)
                |   _       ->  loop products (readOp() |> ignore)
            else
                if readOp()=true then loop products read
                else productList
        loop () ()

    /// Creates MassSpectrum object based on spectrum element.
    member private this.getSpectrum(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="spectrum" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let mutable spectrum = new MassSpectrum()
        let rec loop id sourceRef dataProcRef cvParams userParams scans precs products read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "spectrum"                    -> loop
                                                    (this.getAttribute ("id", readSubtree))
                                                    (this.tryGetAttribute ("sourceFileRef", readSubtree))
                                                    (this.tryGetAttribute ("dataProcessingRef", readSubtree))
                                                    cvParams userParams scans precs products
                                                    (readOp() |> ignore)
                | "referenceableParamGroupRef"  -> loop id sourceRef dataProcRef cvParams userParams scans precs products (readOp() |> ignore)
                | "cvParam"                     -> loop id sourceRef dataProcRef ((this.getCVParam readSubtree)::cvParams) userParams scans precs products (readOp() |> ignore)
                | "userParam"                   -> loop id sourceRef dataProcRef cvParams ((this.getUserParam readSubtree)::userParams) scans precs products   (readOp() |> ignore)
                | "scanList"                    -> loop id sourceRef dataProcRef cvParams userParams (this.getScanList readSubtree) precs products read
                | "precursorList"               -> loop id sourceRef dataProcRef cvParams userParams scans (this.getPrecursorList readSubtree) products read
                | "productList"                 -> loop id sourceRef dataProcRef cvParams userParams scans precs (this.getProductList readSubtree) read
                | "binaryDataArrayList"         -> spectrum <- new MassSpectrum(id, (if dataProcRef.IsSome then dataProcRef.Value else null), precs, scans, products, if sourceRef.IsSome then sourceRef.Value else null)
                                                   (this.getDataProcessingReference (spectrum, readSubtree))
                                                   cvParams
                                                   |> List.iter(fun cvParam -> spectrum.AddCvParam cvParam)
                                                   userParams
                                                   |> List.iter(fun userParam -> spectrum.AddUserParam userParam)
                                                   spectrum
                |   _                           -> spectrum
            else
                if readOp()=true then 
                    loop id sourceRef dataProcRef cvParams userParams scans precs products read
                else 
                    spectrum
        loop null None None [] [] (new ScanList()) (new PrecursorList()) (new ProductList()) ()

    /// Creates Chromatogram object based on chromatogram element.
    member private this.getChromatogram(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="spectrum" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let mutable chromatogram = new Chromatogram()
        let rec loop id cvParams userParams precs products read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "chromatogram"        -> loop
                                            (this.getAttribute ("id", readSubtree))
                                            cvParams userParams precs products
                                            (readOp() |> ignore)
                | "cvParam"             -> loop id ((this.getCVParam readSubtree)::cvParams) userParams precs products (readOp() |> ignore)
                | "userParam"           -> loop id cvParams ((this.getUserParam readSubtree)::userParams) precs products   (readOp() |> ignore)
                | "scanList"            -> loop id cvParams userParams precs products read
                | "precursor"           -> loop id cvParams userParams (this.getPrecursor readSubtree) products read
                | "product"             -> loop id cvParams userParams precs (this.getProduct readSubtree) read
                | "binaryDataArrayList" -> chromatogram <- new Chromatogram(id, precs, products)
                                           cvParams
                                           |> List.iter(fun cvParam -> chromatogram.AddCvParam cvParam)
                                           userParams
                                           |> List.iter(fun userParam -> chromatogram.AddUserParam userParam)
                                           chromatogram
                |   _                   -> chromatogram
            else
                if readOp()=true then loop id cvParams userParams precs products read
                else chromatogram
        loop null [] [] (new Precursor()) (new Product()) ()

    /// Creates collection of Chromatogramms objects based on chromatogram elements of chormatogramList element.
    member private this.getChromatogramms() =
        let rec outerLoop acc =
            if reader.Name = "chromatogramList" then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop (acc:seq<Chromatogram>) =
                    seq
                        {
                            if readSubtree.NodeType=XmlNodeType.Element then
                                match readSubtree.Name with
                                | "chromatogram"    ->  yield this.getChromatogram readSubtree
                                                        (readOp()) |> ignore
                                                        yield! loop acc
                                |   _               ->  (readOp()) |> ignore
                                                        yield! loop acc
                            else
                                if readOp()=true then yield! loop acc
                                else yield! acc
                        }
                loop Seq.empty
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Creates Peak1DArray object based on spectrum, binaryArrayList and cvParam elements.
    member private this.getPeak1DArray(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="spectrum" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let mutable peakArray = new Peak1DArray()
        let rec loop id read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "spectrum"            -> loop (this.tryGetAttribute ("id", readSubtree)) (readOp() |> ignore)
                | "binaryDataArrayList" ->  peakArray <- (this.createPeak1DArray readSubtree)
                                            peakArray
                |   _                   -> loop id (readOp() |> ignore)
            else
                if readOp()=true then loop id read
                else peakArray
        loop None ()

    /// Creates Peak2DArray object based on spectrum, binaryArrayList and cvParam elements.
    /// Nt fully functional yet.
    member private this.getPeak2DArray (?xmlReader:XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="chromatogram" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let mutable peakArray = new Peak2DArray()
        let rec loop id read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "chromatogram"        -> loop (this.tryGetAttribute ("id", readSubtree)) (readOp() |> ignore)
                | "binaryDataArrayList" ->  peakArray <- (this.createPeak2DArray readSubtree)
                                            peakArray
                |   _                   -> loop id (readOp() |> ignore)
            else
                if readOp()=true then loop id read
                else peakArray
        loop None ()

    /// Gets DataProcessingRef as a string from spectrumList or chromatogramList element.
    member private this.getDefaultDataProcessingRef (?xmlReader:XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="spectrumList" || 
                    xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="chromatogramList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop id read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "spectrumList"        -> loop (this.getAttribute ("defaultDataProcessingRef", readSubtree)) (readOp() |> ignore)
                | "chromatogramList"    -> loop (this.getAttribute ("defaultDataProcessingRef", readSubtree)) (readOp() |> ignore)
                |   _                   -> loop id (readOp() |> ignore)
            else
                if readOp()=true then loop id read
                else id
        loop null ()

    /// Creates Run object based on run element.
    member private this.getRun(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="run" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop id instrumentRef sourceFileRef sampleRef cvParams userParams spectrumProcessing chromaProcessing read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "run"                 ->  loop (this.getAttribute ("id", readSubtree)) (this.getAttribute ("defaultInstrumentConfigurationRef", readSubtree)) (this.tryGetAttribute ("defaultSourceFileRef", readSubtree)) (this.tryGetAttribute ("sampleRef", readSubtree)) cvParams userParams spectrumProcessing chromaProcessing (readOp() |> ignore)
                | "cvParam"             ->  loop id instrumentRef sourceFileRef sampleRef ((this.getCVParam readSubtree)::cvParams) userParams spectrumProcessing chromaProcessing (readOp() |> ignore)
                | "userParam"           ->  loop id instrumentRef sourceFileRef sampleRef cvParams ((this.getUserParam readSubtree)::userParams) spectrumProcessing chromaProcessing (readOp() |> ignore)
                | "spectrumList"        ->  loop id instrumentRef sourceFileRef sampleRef cvParams userParams (this.getDefaultDataProcessingRef(readSubtree)) chromaProcessing (readOp() |> ignore)
                | "chromatogramList"    ->  loop id instrumentRef sourceFileRef sampleRef cvParams userParams spectrumProcessing (this.getDefaultDataProcessingRef(readSubtree)) (readOp() |> ignore)
                |   _                   ->  loop id instrumentRef sourceFileRef sampleRef cvParams userParams spectrumProcessing chromaProcessing (readOp() |> ignore)
            else
                if readOp()=true then loop id instrumentRef sourceFileRef sampleRef cvParams userParams spectrumProcessing chromaProcessing read
                else
                    let run =
                        if sampleRef.IsSome then
                            new Run(
                                    id, sampleRef.Value, instrumentRef, new DataProcessing(spectrumProcessing),
                                    if chromaProcessing <> null then new DataProcessing(chromaProcessing) else new DataProcessing()
                                    )
                        else
                            new Run(
                                    id, instrumentRef, new DataProcessing(spectrumProcessing),
                                    if chromaProcessing <> null then new DataProcessing(chromaProcessing) else new DataProcessing()
                                    )
                    cvParams
                    |> Seq.iter (fun cvParam -> run.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> run.AddUserParam userParam)
                    let runList = new RunList()
                    runList.AddModelItem(run)
                    runList
        loop null null None None [] [] null null ()

    /// Get InstrumentRef of run element.
    member private this.getInstrumentRef() =
        //let xmlReader = XmlReader.Create(filePath)
        //let readSubtree =
        //    let rec loop acc =
        //        if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="run" then
        //            xmlReader.ReadSubtree()
        //        else loop (xmlReader.Read())
        //    loop false
        //let rec loop read =
        //    if readSubtree.NodeType=XmlNodeType.Element then
        //        match readSubtree.Name with
        //        | "run"                 ->  (this.getAttribute ("defaultInstrumentConfigurationRef", readSubtree))
        //        |   _                   ->  null
        //    else
        //        null                    
        //loop ()
        let xmlReader = XmlReader.Create(filePath)
        let rec loop acc =
            if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="run" then
                this.getAttribute ("defaultInstrumentConfigurationRef", xmlReader)
            else loop (xmlReader.Read())
        loop false

    /// Creates DataProcessingStep object based on processingMethod element.
    member private this.getProcessingMethod(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="processingMethod" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop name softwareRef cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "processingMethod"    ->  loop (this.getAttribute ("order", readSubtree)) (this.getAttribute ("softwareRef", readSubtree)) cvParams userParams (readOp() |> ignore)
                | "cvParam"             ->  loop name softwareRef ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"           ->  loop name softwareRef cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                |   _                   ->  loop name softwareRef cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop name softwareRef cvParams userParams read
                else
                    let dataProcStep = new DataProcessingStep(name, new Software(softwareRef))
                    cvParams
                    |> Seq.iter (fun cvParam -> dataProcStep.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> dataProcStep.AddUserParam userParam)
                    dataProcStep
        loop null null [] [] ()

    /// Creates DataProcessingStepList object based on processingMethod elements.
    member private this.getProcessingMethods(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="processingMethod" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let dataProcessingStepList = new DataProcessingStepList()
        let rec loop dataProcessingSteps read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "processingMethod"    ->  loop (dataProcessingStepList.AddNamedItem(this.getProcessingMethod readSubtree)) (readOp() |> ignore)
                |   _                   ->  loop dataProcessingSteps (readOp() |> ignore)
            else
                if readOp()=true then loop dataProcessingSteps read
                else dataProcessingStepList
        loop () ()

    /// Creates DataProcessing object based on dataProcessing element and add DataProcessingStepList.
    member private this.getDataProcessing (?xmlReader: XmlReader)=
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="dataProcessing" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop id processingMethod read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "dataProcessing"      ->  loop (this.getAttribute ("id", readSubtree)) processingMethod (readOp() |> ignore)
                | "processingMethod"    ->  loop id (this.getProcessingMethods readSubtree) (readOp() |> ignore)
                |   _                   ->  loop id processingMethod (readOp() |> ignore)
            else
                if readOp()=true then loop id processingMethod read
                else
                    new DataProcessing(id, processingMethod)
        loop null (new DataProcessingStepList()) ()

    /// Creates DataProcessingStepList object based on dataProcessingList element and add DataProcessing objects.
    member private this.getDataProcessingList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="dataProcessingList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let dataProcessingList = new DataProcessingList()
        let rec loop dataProcessings read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "dataProcessing"  ->  loop (dataProcessingList.AddModelItem(this.getDataProcessing readSubtree)) (readOp() |> ignore)
                |   _               ->  loop dataProcessings (readOp() |> ignore)
            else
                if readOp()=true then loop dataProcessings read
                else dataProcessingList
        loop () ()

    /// Creates DetectorComponent object based on detector element.
    member private this.GetDetector(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="detector" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "cvParam"     ->  loop ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"   ->  loop cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                |   _           ->  loop cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop cvParams userParams read
                else
                    let componentList = new DetectorComponent()
                    cvParams
                    |> Seq.iter (fun cvParam -> componentList.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> componentList.AddUserParam userParam)
                    componentList
        loop [] [] ()

    /// Creates AnalyzerComponent object based on analyzer element.
    member private this.GetAnalyzer(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="analyzer" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "cvParam"     ->  loop ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"   ->  loop cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                |   _           ->  loop cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop cvParams userParams read
                else
                    let componentList = new AnalyzerComponent()
                    cvParams
                    |> Seq.iter (fun cvParam -> componentList.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> componentList.AddUserParam userParam)
                    componentList
        loop [] [] ()

    /// Creates SourceComponent object based on source element.
    member private this.GetSource(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="source" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "cvParam"     ->  loop ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"   ->  loop cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                |   _           ->  loop cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop cvParams userParams read
                else
                    let componentList = new SourceComponent()
                    cvParams
                    |> Seq.iter (fun cvParam -> componentList.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> componentList.AddUserParam userParam)
                    componentList
        loop [] [] ()

    /// Creates ComponentList object based on componentList element and add DetectorComponent, AnalyzerComponent and SourceComponent.
    member private this.getComponentList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="componentList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop sources analyzers detectors read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "source"      ->  loop ((this.GetSource readSubtree)::sources) analyzers detectors (readOp() |> ignore)
                | "analyzer"    ->  loop sources ((this.GetAnalyzer readSubtree)::analyzers) detectors (readOp() |> ignore)
                | "detector"    ->  loop sources analyzers ((this.GetDetector readSubtree)::detectors) (readOp() |> ignore)
                |   _           ->  loop sources analyzers detectors (readOp() |> ignore)
            else
                if readOp()=true then loop sources analyzers detectors read
                else
                    let componentList = new ComponentList()
                    sources
                    |> Seq.iter (fun source -> componentList.Add(Guid.NewGuid().ToString(), source))
                    analyzers
                    |> Seq.iter (fun analyzer -> componentList.Add(Guid.NewGuid().ToString(), analyzer))
                    detectors
                    |> Seq.iter (fun detector -> componentList.Add(Guid.NewGuid().ToString(), detector))
                    componentList
        loop [] [] [] ()

    /// Creates Instrument object based on instrumentConfiguration element.
    member private this.getInstrumentConfiguration(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="instrumentConfiguration" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop id components softwareRef cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "instrumentConfiguration" ->  loop (this.getAttribute ("id", readSubtree)) components softwareRef cvParams userParams (readOp() |> ignore)
                | "cvParam"                 ->  loop id components softwareRef ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"               ->  loop id components softwareRef cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                | "componentList"           ->  loop id (this.getComponentList readSubtree) softwareRef cvParams userParams (readOp() |> ignore)
                | "softwareRef"             ->  loop id components (new Software(this.getAttribute ("ref", readSubtree))) cvParams userParams (readOp() |> ignore)
                |   _                       ->  loop id components softwareRef cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop id components softwareRef cvParams userParams read
                else
                    let instrument = new Instrument(id, softwareRef, components)
                    cvParams
                    |> Seq.iter (fun cvParam -> instrument.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> instrument.AddUserParam userParam)
                    instrument
        loop null (new ComponentList()) (new Software()) [] [] ()

    /// Creates InstrumentList object based on instrumentConfigurationList element and add Instrument objects.
    member private this.getInstrumentConfigurationList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="instrumentConfigurationList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let instrumentList = new InstrumentList()
        let rec loop instruments read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "instrumentConfiguration" ->  loop (instrumentList.AddModelItem(this.getInstrumentConfiguration readSubtree)) (readOp() |> ignore)
                |   _                       ->  loop instruments (readOp() |> ignore)
            else
                if readOp()=true then loop instruments read
                else instrumentList
        loop () ()

    /// Creates Software object based on software element.
    member private this.getSoftware(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="software" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop id cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "software"    ->  loop (this.getAttribute ("id", readSubtree)) cvParams userParams (readOp() |> ignore)
                | "cvParam"     ->  loop id ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"   ->  loop id cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                |   _           ->  loop id cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop id cvParams userParams read
                else
                    let software = new Software(id)
                    cvParams
                    |> Seq.iter (fun cvParam -> software.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> software.AddUserParam userParam)
                    software
        loop null [] [] ()

    /// Creates SoftwareList object based on softwareList element and add Software objects.
    member private this.getSoftwareList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="softwareList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let softwareList = new SoftwareList()
        let rec loop softwares read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "software"    ->  loop (softwareList.AddModelItem(this.getSoftware readSubtree)) (readOp() |> ignore)
                |   _           ->  loop softwares (readOp() |> ignore)
            else
                if readOp()=true then loop softwares read
                else softwareList
        loop () ()

    /// Creates Sample object based on sample element.
    member private this.getSample(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="sample" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop id name cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "sample"      ->  loop (this.getAttribute ("id", readSubtree)) (this.getAttribute ("name", readSubtree)) cvParams userParams (readOp() |> ignore)
                | "cvParam"     ->  loop id name ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"   ->  loop id name cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                |   _           ->  loop id name cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop id name cvParams userParams read
                else
                    let sample = new Sample(id, name)
                    cvParams
                    |> Seq.iter (fun cvParam -> sample.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> sample.AddUserParam userParam)
                    sample
        loop null null [] [] ()

    /// Creates SampleList object based on sampleList element and add Sample objects.
    member private this.getSampleList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="sampleList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let sampleList = new SampleList()
        let rec loop samples read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "sample"  ->  loop (sampleList.AddModelItem(this.getSample readSubtree)) (readOp() |> ignore)
                |   _       ->  loop samples (readOp() |> ignore)
            else
                if readOp()=true then loop samples read
                else sampleList
        loop () ()

    /// Creates Contact object based on contact element.
    member private this.getContact(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="contact" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "cvParam"     ->  loop ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"   ->  loop cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                |   _           ->  loop cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop cvParams userParams read
                else
                    let contact = new Contact()
                    cvParams
                    |> Seq.iter (fun cvParam -> contact.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> contact.AddUserParam userParam)
                    contact
        loop [] [] ()

    /// Creates SourceFile object based on sourceFile element.
    member private this.getSourceFile(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="sourceFile" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop id location name cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "sourceFile"  ->  loop (this.getAttribute ("id", readSubtree)) (this.getAttribute ("location", readSubtree)) (this.getAttribute ("name", readSubtree)) cvParams userParams (readOp() |> ignore)
                | "cvParam"     ->  loop id location name ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"   ->  loop id location name cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                |   _           ->  loop id location name cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop id location name cvParams userParams read
                else
                    let sourceFile = new SourceFile(id, name, location)
                    cvParams
                    |> Seq.iter (fun cvParam -> sourceFile.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> sourceFile.AddUserParam userParam)
                    sourceFile
        loop null null null [] [] ()

    /// Creates SourceFileList object based on sourceFileList element and add SourceFile objects.
    member private this.getSourceFileList(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="sourceFileList" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let sourceFileList = new SourceFileList()
        let rec loop sourceFiles read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "sourceFile"  ->  loop (sourceFileList.AddModelItem(this.getSourceFile readSubtree)) (readOp() |> ignore)
                |   _           ->  loop sourceFiles (readOp() |> ignore)
            else
                if readOp()=true then loop sourceFiles read
                else sourceFileList
        loop () ()

    /// Creates FileContenct object based on filecontent element.
    member private this.getFileContent(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="fileContent" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop cvParams userParams read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "cvParam"     ->  loop ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                | "userParam"   ->  loop cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                |   _           ->  loop cvParams userParams (readOp() |> ignore)
            else
                if readOp()=true then loop cvParams userParams read
                else
                    let fileCon = new FileContent()
                    cvParams
                    |> Seq.iter (fun cvParam -> fileCon.AddCvParam cvParam)
                    userParams
                    |> Seq.iter (fun userParam -> fileCon.AddUserParam userParam)
                    fileCon
        loop [] [] ()

    /// Creates FileDescription object based on fileDescription element and add FileContent, SourceFileList and Contact objects.
    member private this.getFileDescription(?xmlReader: XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let readSubtree =
            let rec loop acc =
                if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="fileDescription" then
                    xmlReader.ReadSubtree()
                else loop (xmlReader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop fileContent sourceFileList contact read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "fileContent"     -> loop (this.getFileContent reader) sourceFileList contact (readOp() |> ignore)
                | "sourceFileList"  -> loop fileContent (this.getSourceFileList reader) contact (readOp() |> ignore)
                | "contact"         -> loop fileContent sourceFileList (this.getContact reader) (readOp() |> ignore)
                |   _               -> loop fileContent sourceFileList contact (readOp() |> ignore)
            else
                if readOp()=true then loop fileContent sourceFileList contact read
                else new FileDescription(contact, fileContent, sourceFileList)
        loop (new FileContent()) (new SourceFileList()) (new Contact()) ()

    /// Creates MzIOModel based on all elements until and including run element. Also resets reader automatically to the beginning.
    member private this.getMzIOModel() =
        reader <- XmlReader.Create(filePath)
        MzMLReader.accessMzMLElement reader |> ignore
        MzMLReader.checkSchema reader
        let rec outerLoop acc =
            if reader.Name = "mzML" then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop name fileDes samples softwares instruments dataProcessings run read =
                    if readSubtree.NodeType=XmlNodeType.Element then
                        match readSubtree.Name with
                        | "mzML"                        ->  let tmp = (this.tryGetAttribute ("id", readSubtree))
                                                            loop (if tmp.IsNone then Some (this.getAttribute ("version", readSubtree)) else tmp)
                                                                fileDes samples softwares instruments dataProcessings run (readOp() |> ignore)
                        | "fileDescription"             -> loop name (this.getFileDescription readSubtree) samples softwares instruments dataProcessings run (readOp() |> ignore)
                        | "sampleList"                  -> loop name fileDes (this.getSampleList readSubtree) softwares instruments dataProcessings run (readOp() |> ignore)
                        | "softwareList"                -> loop name fileDes samples (this.getSoftwareList readSubtree) instruments dataProcessings run (readOp() |> ignore)
                        | "instrumentConfigurationList" -> loop name fileDes samples softwares (this.getInstrumentConfigurationList readSubtree) dataProcessings run (readOp() |> ignore)
                        | "dataProcessingList"          -> loop name fileDes samples softwares instruments (this.getDataProcessingList readSubtree) run (readOp() |> ignore)
                        | "run"                         -> loop name fileDes samples softwares instruments dataProcessings (this.getRun readSubtree) (readOp() |> ignore)
                        |   _                           -> loop name fileDes samples softwares instruments dataProcessings run (readOp() |> ignore)
                    else
                        if readOp()=true then loop name fileDes samples softwares instruments dataProcessings run ()
                        else new MzIOModel(name.Value, fileDes, samples, softwares, dataProcessings, instruments, run)
                loop None (new FileDescription()) (new SampleList()) (new SoftwareList()) (new InstrumentList()) (new DataProcessingList()) (new RunList()) ()
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Tries to create MassSpectrum object based on spectrum element with same spectrumID as ID attribute.
    member private this.tryGetSpectrum(spectrumID: string) =
        
        let readSubtree =
            let rec loop acc =
                if reader.NodeType=XmlNodeType.Element && reader.Name="spectrum" then
                    reader.ReadSubtree()
                else loop (reader.Read())
            loop false
        let readOp = readSubtree.Read
        let mutable spectrum = new MassSpectrum()
        let rec loop id sourceRef dataProcRef cvParams userParams scans precs products read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "spectrum"                    -> if (this.getAttribute ("id", readSubtree)) = spectrumID then
                                                        loop
                                                            (this.getAttribute ("id", readSubtree))
                                                            (this.tryGetAttribute ("sourceFileRef", readSubtree))
                                                            (this.tryGetAttribute ("dataProcessingRef", readSubtree))
                                                            cvParams userParams scans precs products
                                                            (readOp() |> ignore)
                                                    else 
                                                        None
                | "referenceableParamGroupRef"  -> loop id sourceRef dataProcRef cvParams userParams scans precs products (readOp() |> ignore)
                | "cvParam"                     -> loop id sourceRef dataProcRef ((this.getCVParam readSubtree)::cvParams) userParams scans precs products (readOp() |> ignore)
                | "userParam"                   -> loop id sourceRef dataProcRef cvParams ((this.getUserParam readSubtree)::userParams) scans precs products   (readOp() |> ignore)
                | "scanList"                    -> loop id sourceRef dataProcRef cvParams userParams (this.getScanList readSubtree) precs products read
                | "precursorList"               -> loop id sourceRef dataProcRef cvParams userParams scans (this.getPrecursorList readSubtree) products read
                | "productList"                 -> loop id sourceRef dataProcRef cvParams userParams scans precs (this.getProductList readSubtree) read
                | "binaryDataArrayList"         -> spectrum <- new MassSpectrum(id, (if dataProcRef.IsSome then dataProcRef.Value else null), precs, scans, products, if sourceRef.IsSome then sourceRef.Value else null)
                                                   (this.getDataProcessingReference (spectrum, readSubtree))
                                                   cvParams
                                                   |> List.iter(fun cvParam -> spectrum.AddCvParam cvParam)
                                                   userParams
                                                   |> List.iter(fun userParam -> spectrum.AddUserParam userParam)
                                                   Some spectrum
                |   _                           -> Some spectrum
            else
                if readOp()=true then loop id sourceRef dataProcRef cvParams userParams scans precs products read
                else Some spectrum

        loop null None None [] [] (new ScanList()) (new PrecursorList()) (new ProductList()) ()

    /// Creates MassSpectrum object based on spectrum element with same spectrumID as ID attribute.
    member private this.getSpectrum(spectrumID: string) =
        let rec outerLoop acc =
            if reader.Name = "spectrumList" then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop (acc) =
                    if readSubtree.NodeType=XmlNodeType.Element then
                        match readSubtree.Name with
                        | "spectrum"    ->  match this.tryGetSpectrum spectrumID with
                                            | Some spectrum -> spectrum
                                            | None          -> 
                                                (readOp()) |> ignore
                                                loop acc
                        |   _           ->  (readOp()) |> ignore
                                            loop acc
                    else
                        if readOp()=true then loop acc
                        else failwith "No valid spectrumID"
                loop ()
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Creates collection of MassSpectrum objects based on all spectrum elements of run element with same ID attribute as runID.
    /// Resets reader to beginning before starting to iterate.
    member private this.getSpectra(runID) =
        reader <- XmlReader.Create(filePath)
        MzMLReader.accessMzMLElement reader |> ignore
        MzMLReader.checkSchema reader
        let rec outerLoop acc =
            if reader.Name = "run" then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop (acc:seq<MassSpectrum>) =
                    seq
                        {
                            if readSubtree.NodeType=XmlNodeType.Element then
                                match readSubtree.Name with
                                | "run"         ->
                                    if (this.getAttribute ("id", readSubtree)) = runID then
                                        (readOp()) |> ignore
                                        yield! loop acc
                                    else
                                        (readOp()) |> ignore
                                        yield! loop acc
                                | "spectrum"    ->  yield this.getSpectrum readSubtree
                                                    (readOp()) |> ignore
                                                    yield! loop acc
                                |   _           ->  (readOp()) |> ignore
                                                    yield! loop acc
                            else
                                if readOp()=true then yield! loop acc
                                else yield! acc
                        }
                loop Seq.empty
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Tries to create Peak1DArray object based on spectrum with same ID attribute as spectrumID, binaryArrayList and cvParam elements.
    member private this.tryGetPeak1DArray(spectrumID: string) =
        let readSubtree =
            let rec loop acc =
                if reader.NodeType=XmlNodeType.Element && reader.Name="spectrum" then
                    reader.ReadSubtree()
                else loop (reader.Read())
            loop false
        let readOp = readSubtree.Read
        let rec loop read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "spectrum"            ->  if (this.getAttribute ("id", readSubtree)) = spectrumID then
                                                loop (readOp() |> ignore)
                                            else
                                                None
                | "binaryDataArrayList" ->  Some (this.createPeak1DArray readSubtree)
                |   _                   ->  loop (readOp() |> ignore)
            else
                if readOp()=true then loop read
                else None
        loop ()

    /// Creates Peak1DArray object based on spectrum, binaryArrayList and cvParam elements.
    member private this.GetPeak1DArray(?xmlReader:XmlReader) =
        let xmlReader = defaultArg xmlReader reader
        let rec outerLoop acc =
            if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name = "spectrum" then
                let readSubtree = xmlReader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop (acc) =
                    if readSubtree.NodeType=XmlNodeType.Element then
                        match readSubtree.Name with
                        | "binaryDataArrayList" ->  this.createPeak1DArray readSubtree
                        |   _                   ->  loop (readOp() |> ignore)
                    else
                        if readOp()=true then loop acc
                        else failwith "No valid spectrumID"
                loop ()
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Creates collection of Peak1DArray objects based on spectrum, binaryArrayList and cvParam elements which are children of the run 
    /// with a corresponding ID attribute to runID.
    member private this.getAllPeak1DArrays(runID) =
        reader <- XmlReader.Create(filePath)
        MzMLReader.accessMzMLElement reader |> ignore
        MzMLReader.checkSchema reader
        let rec outerLoop acc =
            if reader.Name = "run" then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop (acc:seq<Peak1DArray>) =
                    seq
                        {
                            if readSubtree.NodeType=XmlNodeType.Element then
                                match readSubtree.Name with
                                | "run"         ->
                                    if (this.getAttribute ("id", readSubtree)) = runID then
                                        (readOp()) |> ignore
                                        yield! loop acc
                                    else
                                        (readOp()) |> ignore
                                        yield! loop acc
                                | "spectrum"    ->  yield this.GetPeak1DArray readSubtree
                                                    (readOp()) |> ignore
                                                    yield! loop acc
                                |   _           ->  (readOp()) |> ignore
                                                    yield! loop acc
                            else
                                if readOp()=true then yield! loop acc
                                else yield! acc
                        }
                loop Seq.empty
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Creates collection of Peak1DArray and their spectrum ID objects based on spectrum, binaryArrayList and cvParam elements which are children of the run 
    /// with a corresponding ID attribute to runID.
    member this.getAllPeak1DArraysWithID(runID, ?peakCompression) =
        let peakCompression = defaultArg peakCompression BinaryDataCompressionType.ZLib
        let encoder = new BinaryDataEncoder()
        reader <- XmlReader.Create(filePath)
        MzMLReader.accessMzMLElement reader |> ignore
        MzMLReader.checkSchema reader
        let rec outerLoop acc =
            if reader.Name = "run" then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop (acc:seq<string*Peak1DArray>) =
                    seq
                        {
                            if readSubtree.NodeType=XmlNodeType.Element then
                                match readSubtree.Name with
                                | "run"         ->
                                    if (this.getAttribute ("id", readSubtree)) = runID then
                                        (readOp()) |> ignore
                                        yield! loop acc
                                    else
                                        (readOp()) |> ignore
                                        yield! loop acc
                                | "spectrum"    ->  yield (this.getAttribute ("id", readSubtree)) ,this.GetPeak1DArray readSubtree
                                                    (readOp()) |> ignore
                                                    yield! loop acc
                                |   _           ->  (readOp()) |> ignore
                                                    yield! loop acc
                            else
                                if readOp()=true then yield! loop acc
                                else yield! acc
                        }
                loop Seq.empty
            else
                outerLoop (reader.Read())
        outerLoop false
        |> Seq.map (fun (id,p1d) ->
            p1d.CompressionType <- peakCompression
            id, encoder.Encode p1d
        )

    /// Creates Peak1DArray object based on spectrum, binaryArrayList and cvParam elements.
    member private this.getSpecificPeak1DArray(spectrumID:string) =
        let rec outerLoop acc =
            if reader.Name = "spectrumList" && reader.NodeType = XmlNodeType.Element then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop acc =
                    if readSubtree.NodeType=XmlNodeType.Element then
                        match readSubtree.Name with
                        | "spectrum"    ->  match this.tryGetPeak1DArray spectrumID with
                                            | Some peak1DArray  -> peak1DArray
                                            | None              ->
                                                (readOp()) |> ignore
                                                loop acc
                        |   _           ->  (readOp()) |> ignore
                                            loop acc
                    else
                        if readOp()=true then 
                            loop acc
                        else 
                            failwith "Invalid spectrumID"
                loop ()
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Creates collection of Chomatogram objects based on chromatogram, binaryArrayList and cvParam elements which are children of the run 
    /// with a corresponding ID attribute to runID.
    /// Does not work properly yet.
    member private this.ReadChromatogramms(runID) =
        let rec outerLoop acc =
            if reader.Name = "run" then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop (acc:seq<Chromatogram>) =
                    seq
                        {
                            if readSubtree.NodeType=XmlNodeType.Element then
                                match readSubtree.Name with
                                | "run"         ->  
                                    if (this.getAttribute ("id", readSubtree)) = runID then
                                        (readOp()) |> ignore
                                        yield! loop acc
                                    else
                                        failwith "Invalid runID"
                                | "spectrum"    ->  yield this.getChromatogram readSubtree
                                                    (readOp()) |> ignore
                                                    yield! loop acc
                                |   _           ->  (readOp()) |> ignore
                                                    yield! loop acc
                            else
                                if readOp()=true then yield! loop acc
                                else yield! acc
                        }
                loop Seq.empty
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Tries to create Chromatogram object based on chromatogram element with same chromatogramID as ID attribute.
    member private this.tryGetChromatogram(chromatogramID: string) =
        let readSubtree =
            let rec loop acc =
                if reader.NodeType=XmlNodeType.Element && reader.Name="spectrum" then
                    reader.ReadSubtree()
                else loop (reader.Read())
            loop false
        let readOp = reader.Read
        let mutable chromatogram = new Chromatogram()
        let rec loop id cvParams userParams precs products read =
            if reader.NodeType=XmlNodeType.Element then
                match reader.Name with
                | "chromatogram"                ->  if this.getAttribute ("id", readSubtree) = chromatogramID then                                                        
                                                        loop
                                                            (this.getAttribute ("id", readSubtree))
                                                            cvParams userParams precs products
                                                            (readOp() |> ignore)
                                                    else None
                | "cvParam"                     ->  loop id ((this.getCVParam readSubtree)::cvParams) userParams precs products (readOp() |> ignore)
                | "userParam"                   ->  loop id cvParams ((this.getUserParam readSubtree)::userParams) precs products   (readOp() |> ignore)
                | "scanList"                    ->  loop id cvParams userParams precs products read
                | "precursor"                   ->  loop id cvParams userParams (this.getPrecursor readSubtree) products read
                | "product"                     ->  loop id cvParams userParams precs (this.getProduct readSubtree) read
                | "binaryDataArrayList"         ->  chromatogram <- new Chromatogram(id, precs, products)
                                                    cvParams
                                                    |> List.iter(fun cvParam -> chromatogram.AddCvParam cvParam)
                                                    userParams
                                                    |> List.iter(fun userParam -> chromatogram.AddUserParam userParam)
                                                    Some chromatogram
                |   _                           ->  Some chromatogram
            else
                if readOp()=true then loop id cvParams userParams precs products read
                else Some chromatogram
        loop null [] [] (new Precursor()) (new Product()) ()
        
    /// Creates Chromatogram object based on chromatogram element with same chromatogramID as ID attribute.
    member private this.getSpecificChromatogram(chromatogramID: string) =
        let rec outerLoop acc =
            if reader.Name = "chromatogramList" then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop acc =
                            if readSubtree.NodeType=XmlNodeType.Element then
                                match readSubtree.Name with
                                | "chromatogram"    ->  match this.tryGetChromatogram chromatogramID with
                                                        | Some chromatogram -> chromatogram
                                                        | None              ->
                                                            (readOp()) |> ignore
                                                            loop acc
                                |   _               ->  (readOp()) |> ignore
                                                        loop acc
                            else
                                if readOp()=true then 
                                    loop acc
                                else 
                                    failwith "Invalid chromatogramID"
                loop Seq.empty
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Tries to create Peak2DArray object based on chromatogram with same ID attribute as chromatogram, binaryArrayList and cvParam elements.
    member private this.tryGetPeak2DArray(chromatogramID:string) =
        let readSubtree =
            let rec loop acc =
                if reader.NodeType=XmlNodeType.Element && reader.Name="chromatogram" then
                    reader.ReadSubtree()
                else loop (reader.Read())
            loop false
        let readOp = readSubtree.Read
        //let mutable peakArray = new Peak2DArray()
        let rec loop read =
            if readSubtree.NodeType=XmlNodeType.Element then
                match readSubtree.Name with
                | "chromatogram"        -> 
                    if (this.getAttribute ("id", readSubtree)) = chromatogramID then
                        loop (readOp() |> ignore)
                    else
                        None
                | "binaryDataArrayList" -> Some (this.createPeak2DArray readSubtree)
                |   _                   -> loop (readOp() |> ignore)
            else
                if readOp()=true then loop read
                else None
        loop ()

    /// Creates collection of Peak2DArray objects based on chromatogram, binaryArrayList and cvParam elements which are children of the run 
    /// with a corresponding ID attribute to runID.
    member private this.getPeak2DArrays(chromatogramID:string) =
        let rec outerLoop acc =
            if reader.Name = "chromatogramList" then
                let readSubtree = reader.ReadSubtree()
                let readOp = readSubtree.Read
                let rec loop acc =
                            if readSubtree.NodeType=XmlNodeType.Element then
                                match readSubtree.Name with
                                | "chromatogram" -> match this.tryGetPeak2DArray chromatogramID with
                                                    | Some chromatogram -> chromatogram
                                                    | None ->
                                                            (readOp()) |> ignore
                                                            loop acc
                                |   _            -> (readOp()) |> ignore
                                                    loop acc
                            else
                                if readOp()=true then loop acc
                                else 
                                    failwith "Invalid chromatogramID"
                loop ()
            else
                outerLoop (reader.Read())
        outerLoop false

    /// Current MzIOModel in the memory
    member private this.model = this.getMzIOModel()

    static member private GetModelFilePath(filePath) =

        sprintf "%s%s" filePath ".MzIOmodel"

    interface IDisposable with

        member this.Dispose() =
            //if disposed = true then
            //    ()
            //else
            //    if dataProvider<>null then
            //        dataProvider.Close()
            //    disposed <- true
            ()

    member this.Dispose() =

        (this :> IDisposable).Dispose()

    interface IMzIOIO with

        member this.CreateDefaultModel() =

            let modelName = Path.GetFileNameWithoutExtension(filePath)
            let model = new MzIOModel(modelName)

            let sampleName = Path.GetFileNameWithoutExtension(filePath)
            let sample = new Sample("sample_1", sampleName)
            model.Samples.TryAdd(sample.ID, sample) |> ignore
            let instruments = this.getInstrumentConfigurationList()
            instruments.GetProperties false
            |> Seq.iter (fun instrument -> model.Instruments.Add(instrument.Key, instrument.Value :?> Instrument))
            let instrumentRef = this.getInstrumentRef()
            let run = new Run("run_1", sampleName, instrumentRef)
            //run.Sample <- sample
            model.Runs.TryAdd(run.ID, run)  |> ignore
            model

        /// Current MzIOModel in the memory
        member this.Model = this.model
        
        /// Saves in memory MzIOModel in the shadow file.
        member this.SaveModel() =

            try
                MzIOJson.SaveJsonFile(this.model, MzMLReader.GetModelFilePath(filePath))
            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        member this.BeginTransaction() =

            new MzMLReaderTransactionScope() :> ITransactionScope

    member this.BeginTransaction() =

        (this :> IMzIOIO).BeginTransaction()

    /// Create MzIOModel based on minimal information of mzML file.
    member this.CreateDefaultModel() =

        (this :> IMzIOIO).CreateDefaultModel()

    /// Saves in memory MzIOModel in the shadow file.
    member this.SaveModel() =

        (this :> IMzIOIO).SaveModel()

    /// Current MzIOModel in the memory.
    member this.Model =
        (this :> IMzIOIO).Model

    interface IMzIODataReader with
    
        member this.ReadMassSpectra(runID: string) =
            reader <- XmlReader.Create(filePath)
            this.getSpectra(runID)

        member this. ReadMassSpectrum(spectrumID: string) =
            reader <- XmlReader.Create(filePath)
            this.getSpectrum(spectrumID)

        member this.ReadSpectrumPeaks(spectrumID: string) =
            reader <- XmlReader.Create(filePath)
            this.getSpecificPeak1DArray(spectrumID)

        member this.ReadMassSpectrumAsync(spectrumID: string) =
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadMassSpectrum(spectrumID)
                }
            //Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSpectrum(spectrumID))

        member this.ReadSpectrumPeaksAsync(spectrumID: string) =
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadSpectrumPeaks(spectrumID)
                }
            //Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))

        member this.ReadChromatograms(runID:string) =
            this.ReadChromatogramms(runID)

        member this.ReadChromatogram(chromatogramID:string) =
            this.getSpecificChromatogram(chromatogramID)

        member this.ReadChromatogramPeaks(chromatgramID:string) =
            this.getPeak2DArrays(chromatgramID)

        member this.ReadChromatogramAsync(runID:string) =
            try
                raise ((new NotSupportedException()))
            with
                | :? Exception as ex -> 
                    raise (MzIOIOException(ex.Message, ex))

        member this.ReadChromatogramPeaksAsync(runID:string) =
            try
                raise ((new NotSupportedException()))
            with
                | :? Exception as ex -> 
                    raise (MzIOIOException(ex.Message, ex))

    /// Read all mass spectra of one run of mzml file.
    member this.ReadMassSpectra(runID:string)               =

        (this :> IMzIODataReader).ReadMassSpectra(runID)

    /// Read mass spectrum of mzml file.
    member this.ReadMassSpectrum(spectrumID:string)         =

        (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

    /// Read peaks of mass spectrum of mzml file.
    member this.ReadSpectrumPeaks(spectrumID:string)        =

        (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

    /// Read mass spectrum of baf file asynchronously.
    member this.ReadMassSpectrumAsync(spectrumID:string)    =

        (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

    /// Read peaks of mass spectrum of baf file asynchronously.
    member this.ReadSpectrumPeaksAsync(spectrumID:string)   =

        (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

    /// Not implemented yet.
    member this.ReadChromatograms(runID:string)             =

        (this :> IMzIODataReader).ReadChromatograms(runID)

    /// Not implemented yet.
    member this.ReadChromatogramPeaks(runID:string)         =

        (this :> IMzIODataReader).ReadChromatogramPeaks(runID)

    /// Not implemented yet.
    member this.ReadChromatogramAsync(runID:string)         =

        (this :> IMzIODataReader).ReadChromatogramAsync(runID)

    /// Not implemented yet.
    member this.ReadChromatogramPeaksAsync(runID:string)    =

        (this :> IMzIODataReader).ReadChromatogramPeaksAsync(runID)

    /// Not implemented yet.
    member this.ReadAllSpectrumPeaks(runID:string) =

        this.getAllPeak1DArrays(runID)

    /// Create Peak2DArray based on range of retention time and m/z.
    member this.RtProfile(rtIndex: IMzIOArray<RtIndexEntry>, rtRange: RangeQuery, mzRange: RangeQuery) =

        reader <- XmlReader.Create(filePath)
        let entries = RtIndexEntry.Search(rtIndex, rtRange).ToArray()
        let profile = Array.zeroCreate<Peak2D> entries.Length
        let rtIdxs = [0..entries.Length-1]
        rtIdxs
        |> List.iter (
                        fun rtIdx ->
                        let entry = entries.[rtIdx]
                        let peaks = this.ReadSpectrumPeaks(entry).Peaks
                        let p = 
                            (RtIndexEntry.MzSearch (peaks, mzRange)).DefaultIfEmpty(new Peak1D(0., mzRange.LockValue))
                            |> fun x -> RtIndexEntry.ClosestMz (x, mzRange.LockValue)
                            |> fun x -> RtIndexEntry.AsPeak2D (x, entry.Rt)
                        profile.[rtIdx] <- p
                    )
        profile
