namespace MzIO.IO.MzML


open System
open System.IO
open System.IO.Compression
open System.Xml
open System.Threading.Tasks
open FSharp.Core
open MzIO.Model
open MzIO.Model.Helper
open MzIO.Model.CvParam
open MzIO.Commons.Arrays
open MzIO.Binary
open MzIO.IO
open MzIO.Json


module MzML =

    type private MzMLReaderTransactionScope() =

        interface ITransactionScope with

            member this.Commit() =
                ()

            member this.Rollback() =
                ()

        interface IDisposable with
        
            member this.Dispose() =
                ()

    type MzMLReader(filePath: string) =

        let reader = XmlReader.Create(filePath)

        member this.tryGetAttribute (name:string, ?xmlReader: XmlReader) =
            let xmlReader = defaultArg xmlReader reader
            let tmp = xmlReader.GetAttribute(name)
            if tmp=null then None
            else Some tmp

        member this.getAttribute (name:string, ?xmlReader: XmlReader) =
            let xmlReader = defaultArg xmlReader reader
            xmlReader.GetAttribute(name)

        member this.getCVParam (?xmlReader:XmlReader) =
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

        member this.getUserParam (?xmlReader:XmlReader) =
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

        member this.getIDs (elementName:string) (attributeName:string) =
            let readSubtree = reader.ReadSubtree()
            let readOp = readSubtree.Read
            let rec loop (acc:seq<string option>) =
                seq
                    {
                        if readSubtree.NodeType=XmlNodeType.Element then
                            match readSubtree.Name=elementName with
                            | true  ->  yield (this.tryGetAttribute (attributeName, readSubtree))
                                        (readOp()) |> ignore
                                        yield! loop acc
                            | false ->  (readOp()) |> ignore
                                        yield! loop acc
                        else
                            if readOp()=true then yield! loop acc
                            else yield! acc
                    }
            loop Seq.empty
            |> List.ofSeq
            |> (fun item -> if item.IsEmpty then None else Some (item |> Seq.ofList))

        static member getArrayTypeOfP1D (peakArray:Peak1DArray) (arrayType:BinaryDataType) (keys:string []) =
            for key in keys do
                match key with
                //M/Z Array
                | "MS:1000514" -> peakArray.RemoveItem(key)
                                  peakArray.MzDataType <- arrayType
                //IntensityArray
                | "MS:1000515" -> peakArray.RemoveItem(key)
                                  peakArray.IntensityDataType <- arrayType
                | _            -> ()

        static member getBinaryDataTypeOfP1D (peakArray:Peak1DArray) (keys:string []) =
            for key in keys do
                match key with
                //Float32
                | "MS:1000521"  ->  peakArray.RemoveItem(key)
                                    MzMLReader.getArrayTypeOfP1D peakArray BinaryDataType.Float32 keys
                //Float64
                | "MS:1000523"  ->  peakArray.RemoveItem(key)
                                    MzMLReader.getArrayTypeOfP1D peakArray BinaryDataType.Float64 keys
                //Int32
                | "MS:1000519"  ->  peakArray.RemoveItem(key)
                                    MzMLReader.getArrayTypeOfP1D peakArray BinaryDataType.Int32 keys
                //Int64
                | "MS:1000522"  ->  peakArray.RemoveItem(key)
                                    MzMLReader.getArrayTypeOfP1D peakArray BinaryDataType.Int64 keys
                | _             ->  ()

        static member getCompressionTypeOfP1D (peakArray:Peak1DArray) (keys:string []) =
            for key in keys do
                match key with
                //NoCompression
                | "MS:1000576"  ->  peakArray.RemoveItem(key)
                                    peakArray.CompressionType <- BinaryDataCompressionType.NoCompression
                                    MzMLReader.getBinaryDataTypeOfP1D peakArray keys
                //ZlibCompression
                | "MS:1000574"  ->  peakArray.RemoveItem(key)
                                    peakArray.CompressionType <- BinaryDataCompressionType.ZLib
                                    MzMLReader.getBinaryDataTypeOfP1D peakArray keys
                | _             -> ()

        static member createPeak1DArray (peakArray:Peak1DArray) =
            peakArray.GetProperties false
            |> Seq.map (fun pair -> pair.Key)
            |> Array.ofSeq
            |> MzMLReader.getCompressionTypeOfP1D peakArray
            peakArray

        static member getArrayTypeOfP2D (peakArray:Peak2DArray) (arrayType:BinaryDataType) (keys:string []) =
            for key in keys do
                match key with
                //M/Z Array
                | "MS:1000514" -> peakArray.RemoveItem(key)
                                  peakArray.MzDataType <- arrayType
                //IntensityArray
                | "MS:1000515" -> peakArray.RemoveItem(key)
                                  peakArray.IntensityDataType <- arrayType
                //RetentionTimeArray
                | "MS:1000595" -> peakArray.RemoveItem(key)
                                  peakArray.RtDataType <- arrayType
                | _            -> ()

        static member getBinaryDataTypeOfP2D (peakArray:Peak2DArray) (keys:string []) =
            for key in keys do
                match key with
                //Float32
                | "MS:1000521"  ->  peakArray.RemoveItem(key)
                                    MzMLReader.getArrayTypeOfP2D peakArray BinaryDataType.Float32 keys
                //Float64
                | "MS:1000523"  ->  peakArray.RemoveItem(key)
                                    MzMLReader.getArrayTypeOfP2D peakArray BinaryDataType.Float64 keys
                //Int32
                | "MS:1000519"  ->  peakArray.RemoveItem(key)
                                    MzMLReader.getArrayTypeOfP2D peakArray BinaryDataType.Int32 keys
                //Int64
                | "MS:1000522"  ->  peakArray.RemoveItem(key)
                                    MzMLReader.getArrayTypeOfP2D peakArray BinaryDataType.Int64 keys
                | _             ->  ()

        static member getCompressionTypeOfP2D (peakArray:Peak2DArray) (keys:string []) =
            for key in keys do
                match key with
                //NoCompression
                | "MS:1000576"  ->  peakArray.RemoveItem(key)
                                    peakArray.CompressionType <- BinaryDataCompressionType.NoCompression
                                    MzMLReader.getBinaryDataTypeOfP2D peakArray keys
                //ZlibCompression
                | "MS:1000574"  ->  peakArray.RemoveItem(key)
                                    peakArray.CompressionType <- BinaryDataCompressionType.ZLib
                                    MzMLReader.getBinaryDataTypeOfP2D peakArray keys
                | _             ->  ()

        static member createPeak2DArray (peakArray:Peak2DArray) =
            peakArray.GetProperties false
            |> Seq.map (fun pair -> pair.Key)
            |> Array.ofSeq
            |> MzMLReader.getCompressionTypeOfP2D peakArray
            peakArray

        static member singleToBytes (floatArray: float[]) =
            let byteArray = Array.init (floatArray.Length*4) (fun x -> byte(0 |> float32))
            Buffer.BlockCopy (floatArray, 0, byteArray, 0, byteArray.Length)
            byteArray

        static member byteToSingles (littleEndian:Boolean) (byteArray: byte[]) =
            match littleEndian with
            | false ->  let floatArray = Array.init (byteArray.Length/4) (fun x -> 0. |> float32)
                        Buffer.BlockCopy  (byteArray, 0, floatArray, 0, byteArray.Length)
                        floatArray
            | true  ->  let floatArray = Array.init (byteArray.Length/4) (fun x -> 0. |> float32)
                        Buffer.BlockCopy  (Array.rev byteArray, 0, floatArray, 0, byteArray.Length)
                        Array.rev floatArray

        static member doubleToBytes (floatArray: float[]) =
            let byteArray = Array.init (floatArray.Length*8) (fun x -> byte(0))
            Buffer.BlockCopy (floatArray, 0, byteArray, 0, byteArray.Length)
            byteArray

        static member byteToDoubles (littleEndian:Boolean) (byteArray: byte[]) =
            match littleEndian with
            | false ->  let floatArray = Array.init (byteArray.Length/8) (fun x -> 0.)
                        Buffer.BlockCopy (byteArray, 0, floatArray, 0, byteArray.Length)
                        floatArray
            | true  ->  let floatArray = Array.init (byteArray.Length/8) (fun x -> 0.)
                        Buffer.BlockCopy (Array.rev byteArray, 0, floatArray, 0, byteArray.Length)
                        floatArray

        ///Remove 1st two bytes because they are the header of zLib compression
        static member decompressZlib (bytes : byte []) =
            let buffer = Array.skip 2 bytes
            let memoryStream = new MemoryStream ()
            memoryStream.Write(buffer, 0, Array.length buffer)
            memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
            let deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress, true)
            let outerMemoryStream = new MemoryStream()
            deflateStream.CopyTo(outerMemoryStream)
            outerMemoryStream.ToArray()

        member this.getBinary (?xmlReader: XmlReader)=
            let xmlReader = defaultArg xmlReader reader
            xmlReader.ReadElementContentAsString()

        static member get1DPeaks (peaks:string list) (peakArray:Peak1DArray) =
            match peakArray.CompressionType with
            | BinaryDataCompressionType.NoCompression ->
                match peakArray.MzDataType with
                | BinaryDataType.Float32 ->
                    let mzs =
                        Convert.FromBase64String(peaks.[1])
                        |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(peaks.[0])
                            |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(peaks.[0])
                            |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | _ -> failwith "No compelement Type"
                | BinaryDataType.Float64 ->
                    let mzs =
                        Convert.FromBase64String(peaks.[1])
                        |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(peaks.[0])
                            |> (fun bytes -> MzMLReader.byteToSingles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(peaks.[0])
                            |> (fun bytes -> MzMLReader.byteToDoubles false bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | _ -> failwith "No compelement Type"
                | _ -> failwith "No compelement Type"
            | BinaryDataCompressionType.ZLib ->
                match peakArray.MzDataType with
                | BinaryDataType.Float32 ->
                    let mzs =
                        Convert.FromBase64String(peaks.[1])
                        |> MzMLReader.decompressZlib
                        |> (fun bytes -> MzMLReader.byteToSingles true bytes)
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(peaks.[0])
                            |> MzMLReader.decompressZlib
                            |> (fun bytes -> MzMLReader.byteToSingles true bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(peaks.[0])
                            |> MzMLReader.decompressZlib
                            |> (fun bytes -> MzMLReader.byteToDoubles true bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, float mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                     | _ -> failwith "No compelement Type"
                | BinaryDataType.Float64 ->
                    let mzs =
                        Convert.FromBase64String(peaks.[1])
                        |> MzMLReader.decompressZlib
                        |> (fun bytes -> MzMLReader.byteToDoubles true bytes)
                    match peakArray.IntensityDataType with
                    | BinaryDataType.Float64 ->
                        let intensities =
                            Convert.FromBase64String(peaks.[0])
                            |> MzMLReader.decompressZlib
                            |> (fun bytes -> MzMLReader.byteToDoubles true bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                    | BinaryDataType.Float32 ->
                        let intensities =
                            Convert.FromBase64String(peaks.[0])
                            |> MzMLReader.decompressZlib
                            |> (fun bytes -> MzMLReader.byteToSingles true bytes)
                        let peaks = Array.map2 (fun mz int -> new Peak1D(float int, mz)) mzs intensities
                        peakArray.Peaks <- ArrayWrapper(peaks)
                     | _ -> failwith "No compelement Type"
                | _ -> failwith "No compelement Type"
            | _ -> failwith "No compelement Type"

        static member get2DPeaks (peaks:string list) (peakArray:Peak2DArray) =
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

        member this.getDataProcessingReference (spectrum:MassSpectrum, ?xmlReader: XmlReader) =
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

        member this.translatePeak1DArray(?xmlReader: XmlReader) =
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
                else
                    if readOp()=true then loop dbProcRef cvParams peaks read
                    else
                        match dbProcRef with
                        | Some ref  ->  MzMLReader.createPeak1DArray peakArray
                                        |> MzMLReader.get1DPeaks peaks
                                        peakArray
                        | None      ->  MzMLReader.createPeak1DArray peakArray
                                        |> MzMLReader.get1DPeaks peaks
                                        peakArray
            loop None () [] ()

        member this.translatePeak2DArray(?xmlReader: XmlReader) =
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
                        | Some ref  ->  MzMLReader.createPeak2DArray peakArray
                                        |> MzMLReader.get2DPeaks peaks
                                        peakArray
                        | None      ->  MzMLReader.createPeak2DArray peakArray
                                        |> MzMLReader.get2DPeaks peaks
                                        peakArray
            loop None () [] ()

        //let getBinaryDataArrayList (spectrum:MassSpectrum) (reader:XmlReader) =
        //    let readSubtree = reader.ReadSubtree()
        //    let readOp = readSubtree.Read
        //    let rec loop acc =
        //        seq
        //            {
        //                if readSubtree.NodeType=XmlNodeType.Element then
        //                    match readSubtree.Name with
        //                    | "binaryDataArray" ->  yield getBinaryDataArray spectrum readSubtree
        //                                            readOp() |> ignore
        //                                            yield! loop acc
        //                    |   _               ->  readOp() |> ignore
        //                                            yield! loop acc
        //                else
        //                    if readOp()=true then yield! loop acc
        //                    else yield! acc
        //            }
        //    loop Seq.empty
        //    |> List.ofSeq
        //    |> Seq.ofList

        member this.getScanWindow (?xmlReader: XmlReader) =
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

        member this.getScanWindowList (?xmlReader: XmlReader) =
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
                    | "scanWindow"  ->  loop (scanWindowList.Add(this.getScanWindow readSubtree)) (readOp() |> ignore)
                    |   _           ->  loop addParam (readOp() |> ignore)
                else
                    if readOp()=true then loop addParam read
                    else scanWindowList
            loop () ()

        member this.getScan (?xmlReader:XmlReader) =
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

        member this.getScanList(?xmlReader: XmlReader) =
            let xmlReader = defaultArg xmlReader reader
            let readSubtree =
                let rec loop acc =
                    if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="scanList" then
                        xmlReader.ReadSubtree()
                    else loop (xmlReader.Read())
                loop false
            let readOp = readSubtree.Read
            let scanList = new ScanList()
            let rec loop scans read =
                if readSubtree.NodeType=XmlNodeType.Element then
                    match readSubtree.Name with
                    | "scan"    ->  loop (scanList.Add(this.getScan readSubtree)) (readOp() |> ignore)
                    |   _       ->  loop scans (readOp() |> ignore)
                else
                    if readOp()=true then loop scans read
                    else scanList
            loop () ()

        member this.getActivation(?xmlReader: XmlReader) =
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

        member this.getSelectedIon (?xmlReader: XmlReader)=
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

        member this.getSelectedIonList(?xmlReader: XmlReader) =
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
                    | "selectedIon" ->  loop (selectedIonList.Add(this.getSelectedIon readSubtree)) (readOp() |> ignore)
                    |   _           ->  loop addParam (readOp() |> ignore)
                else
                    if readOp()=true then loop addParam read
                    else selectedIonList
            loop () ()

        member this.getIsolationWindow(?xmlReader: XmlReader) =
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

        member this.getPrecursor(?xmlReader: XmlReader) =
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

        member this.getPrecursorList(?xmlReader: XmlReader) =
            let xmlReader = defaultArg xmlReader reader
            let readSubtree =
                let rec loop acc =
                    if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="precursorList" then
                        xmlReader.ReadSubtree()
                    else loop (xmlReader.Read())
                loop false
            let readOp = readSubtree.Read
            let precursorList = new PrecursorList()
            let rec loop scans read =
                if readSubtree.NodeType=XmlNodeType.Element then
                    match readSubtree.Name with
                    | "precursor"    ->  loop (precursorList.Add(this.getPrecursor readSubtree)) (readOp() |> ignore)
                    |   _       ->  loop scans (readOp() |> ignore)
                else
                    if readOp()=true then loop scans read
                    else precursorList
            loop () ()

        member this.getProduct(?xmlReader: XmlReader) =
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

        member this.getProductList(?xmlReader: XmlReader) =
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
                    | "product" ->  loop (productList.Add(this.getProduct readSubtree)) (readOp() |> ignore)
                    |   _       ->  loop products (readOp() |> ignore)
                else
                    if readOp()=true then loop products read
                    else productList
            loop () ()

        member this.getSpectrum(?xmlReader: XmlReader) =
            let xmlReader = defaultArg xmlReader reader
            let readSubtree =
                let rec loop acc =
                    if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="spectrum" then
                        xmlReader.ReadSubtree()
                    else loop (xmlReader.Read())
                loop false
            let readOp = readSubtree.Read
            let mutable spectrum = new MassSpectrum()
            let rec loop id sourceRef cvParams userParams scans precs products read =
                if readSubtree.NodeType=XmlNodeType.Element then
                    match readSubtree.Name with
                    | "spectrum"                    -> loop
                                                        (this.getAttribute ("id", readSubtree))
                                                        (this.tryGetAttribute ("sourceFileRef", readSubtree))
                                                        cvParams userParams scans precs products
                                                        (readOp() |> ignore)
                    | "referenceableParamGroupRef"  -> loop id sourceRef cvParams userParams scans precs products (readOp() |> ignore)
                    | "cvParam"                     -> loop id sourceRef ((this.getCVParam readSubtree)::cvParams) userParams scans precs products (readOp() |> ignore)
                    | "userParam"                   -> loop id sourceRef cvParams ((this.getUserParam readSubtree)::userParams) scans precs products   (readOp() |> ignore)
                    | "scanList"                    -> loop id sourceRef cvParams userParams (this.getScanList readSubtree) precs products read
                    | "precursorList"               -> loop id sourceRef cvParams userParams scans (this.getPrecursorList readSubtree) products read
                    | "productList"                 -> loop id sourceRef cvParams userParams scans precs (this.getProductList readSubtree) read
                    | "binaryDataArrayList"         -> spectrum <- new MassSpectrum(id, precs, scans, products, if sourceRef.IsSome then sourceRef.Value else null)
                                                       (this.getDataProcessingReference (spectrum, readSubtree))
                                                       cvParams
                                                       |> List.iter(fun cvParam -> spectrum.AddCvParam cvParam)
                                                       userParams
                                                       |> List.iter(fun userParam -> spectrum.AddUserParam userParam)
                                                       spectrum
                    |   _                           -> spectrum
                else
                    if readOp()=true then loop id sourceRef cvParams userParams scans precs products read
                    else spectrum
            loop null None [] [] (new ScanList()) (new PrecursorList()) (new ProductList()) ()

        member this.getSpectra() =
            let rec outerLoop acc =
                if reader.Name = "spectrumList" then
                    let readSubtree = reader.ReadSubtree()
                    let readOp = readSubtree.Read
                    let rec loop (acc:seq<MassSpectrum>) =
                        seq
                            {
                                if readSubtree.NodeType=XmlNodeType.Element then
                                    match readSubtree.Name with
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

        member this.getChromatogram(?xmlReader: XmlReader) =
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
                    | "chromatogram"                -> loop
                                                        (this.getAttribute ("id", readSubtree))
                                                        cvParams userParams precs products
                                                        (readOp() |> ignore)
                    | "cvParam"                     -> loop id ((this.getCVParam readSubtree)::cvParams) userParams precs products (readOp() |> ignore)
                    | "userParam"                   -> loop id cvParams ((this.getUserParam readSubtree)::userParams) precs products   (readOp() |> ignore)
                    | "scanList"                    -> loop id cvParams userParams precs products read
                    | "precursor"                   -> loop id cvParams userParams (this.getPrecursor readSubtree) products read
                    | "product"                     -> loop id cvParams userParams precs (this.getProduct readSubtree) read
                    | "binaryDataArrayList"         -> chromatogram <- new Chromatogram(id, precs, products)
                                                       cvParams
                                                       |> List.iter(fun cvParam -> chromatogram.AddCvParam cvParam)
                                                       userParams
                                                       |> List.iter(fun userParam -> chromatogram.AddUserParam userParam)
                                                       chromatogram
                    |   _                           -> chromatogram
                else
                    if readOp()=true then loop id cvParams userParams precs products read
                    else chromatogram
            loop null [] [] (new Precursor()) (new Product()) ()

        member this.getChromatogramms() =
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

        member this.getPeak1DArray(?xmlReader: XmlReader) =
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
                    | "binaryDataArrayList" -> peakArray <- (this.translatePeak1DArray readSubtree)
                                               peakArray
                    |   _                   -> loop id (readOp() |> ignore)
                else
                    if readOp()=true then loop id read
                    else peakArray
            loop None ()

        member this.getPeak1DArrays() =
            let rec outerLoop acc =
                if reader.Name = "spectrumList" then
                    let readSubtree = reader.ReadSubtree()
                    let readOp = readSubtree.Read
                    let rec loop (acc:seq<Peak1DArray>) =
                        seq
                            {
                                if readSubtree.NodeType=XmlNodeType.Element then
                                    match readSubtree.Name with
                                    | "spectrum"    ->  yield this.getPeak1DArray readSubtree
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

        //let readPeak1DArrays (reader:XmlReader) =
        //    let readOp = reader.Read
        //    let rec loop acc =
        //        if reader.NodeType=XmlNodeType.Element then
        //            if reader.Name = "spectrumList" then
        //                getPeak1DArrays reader
        //            else
        //                readOp() |> ignore
        //                loop acc
        //        else
        //            readOp() |> ignore
        //            loop acc
        //    loop Seq.empty

        member this.getPeak2DArray (?xmlReader:XmlReader) =
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
                    | "binaryDataArrayList" -> peakArray <- (this.translatePeak2DArray readSubtree)
                                               peakArray
                    |   _                   -> loop id (readOp() |> ignore)
                else
                    if readOp()=true then loop id read
                    else peakArray
            loop None ()

        member this.getPeak2DArrays() =
            let rec outerLoop acc =
                if reader.Name = "chromatogramList" then
                    let readSubtree = reader.ReadSubtree()
                    let readOp = readSubtree.Read
                    let rec loop (acc:seq<Peak2DArray>) =
                        seq
                            {
                                if readSubtree.NodeType=XmlNodeType.Element then
                                    match readSubtree.Name with
                                    | "chromatogram" ->  yield this.getPeak2DArray readSubtree
                                                         (readOp()) |> ignore
                                                         yield! loop acc
                                    |   _            ->  (readOp()) |> ignore
                                                         yield! loop acc
                                else
                                    if readOp()=true then yield! loop acc
                                    else yield! acc
                            }
                    loop Seq.empty
                else
                    outerLoop (reader.Read())
            outerLoop false

        member this.getRun(?xmlReader: XmlReader) =
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
                    | "spectrumList"        ->  loop id instrumentRef sourceFileRef sampleRef cvParams userParams (this.getAttribute ("defaultDataProcessingRef", readSubtree)) chromaProcessing (readOp() |> ignore)
                    | "chromatogramList"    ->  loop id instrumentRef sourceFileRef sampleRef cvParams userParams spectrumProcessing (this.tryGetAttribute ("defaultDataProcessingRef", readSubtree)) (readOp() |> ignore)
                    |   _                   ->  loop id instrumentRef sourceFileRef sampleRef cvParams userParams spectrumProcessing chromaProcessing (readOp() |> ignore)
                else
                    if readOp()=true then loop id instrumentRef sourceFileRef sampleRef cvParams userParams spectrumProcessing chromaProcessing read
                    else
                        let run =
                            new Run(
                                    id,
                                    (if sampleRef.IsSome then new Sample(sampleRef.Value, "default") else new Sample()),
                                    new Instrument(instrumentRef), new DataProcessing(spectrumProcessing),
                                    if chromaProcessing.IsSome then new DataProcessing(chromaProcessing.Value) else new DataProcessing()
                                   )
                        cvParams
                        |> Seq.iter (fun cvParam -> run.AddCvParam cvParam)
                        userParams
                        |> Seq.iter (fun userParam -> run.AddUserParam userParam)
                        let runList = new RunList()
                        runList.AddModelItem(run)
                        runList
            loop null null None None [] [] null None ()

        member this.getProcessingMethod(?xmlReader: XmlReader) =
            let xmlReader = defaultArg xmlReader reader
            let readSubtree =
                let rec loop acc =
                    if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="processingMethod" then
                        xmlReader.ReadSubtree()
                    else loop (xmlReader.Read())
                loop false
            let readOp = readSubtree.Read
            let rec loop id softwareRef cvParams userParams read =
                if readSubtree.NodeType=XmlNodeType.Element then
                    match readSubtree.Name with
                    | "processingMethod"    ->  loop (this.getAttribute ("order", readSubtree)) (this.getAttribute ("softwareRef", readSubtree)) cvParams userParams (readOp() |> ignore)
                    | "cvParam"             ->  loop id softwareRef ((this.getCVParam readSubtree)::cvParams) userParams (readOp() |> ignore)
                    | "userParam"           ->  loop id softwareRef cvParams ((this.getUserParam readSubtree)::userParams) (readOp() |> ignore)
                    |   _                   ->  loop id softwareRef cvParams userParams (readOp() |> ignore)
                else
                    if readOp()=true then loop id softwareRef cvParams userParams read
                    else
                        let dataProcStep = new DataProcessingStep(id, new Software(softwareRef))
                        cvParams
                        |> Seq.iter (fun cvParam -> dataProcStep.AddCvParam cvParam)
                        userParams
                        |> Seq.iter (fun userParam -> dataProcStep.AddUserParam userParam)
                        dataProcStep
            loop null null [] [] ()

        member this.getProcessingMethods(?xmlReader: XmlReader) =
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

        member this.getDataProcessing (?xmlReader: XmlReader)=
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

        member this.getDataProcessingList(?xmlReader: XmlReader) =
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

        member this.getComponentList(?xmlReader: XmlReader) =
            let xmlReader = defaultArg xmlReader reader
            let readSubtree =
                let rec loop acc =
                    if xmlReader.NodeType=XmlNodeType.Element && xmlReader.Name="componentList" then
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
                        let componentList = new ComponentList()
                        cvParams
                        |> Seq.iter (fun cvParam -> componentList.AddCvParam cvParam)
                        userParams
                        |> Seq.iter (fun userParam -> componentList.AddUserParam userParam)
                        componentList
            loop [] [] ()

        member this.getInstrumentConfiguration(?xmlReader: XmlReader) =
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

        member this.getInstrumentConfigurationList(?xmlReader: XmlReader) =
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

        member this.getSoftware(?xmlReader: XmlReader) =
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

        member this.getSoftwareList(?xmlReader: XmlReader) =
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

        member this.getSample(?xmlReader: XmlReader) =
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

        member this.getSampleList(?xmlReader: XmlReader) =
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

        member this.getContact(?xmlReader: XmlReader) =
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

        member this.getSourceFile(?xmlReader: XmlReader) =
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

        member this.getSourceFileList(?xmlReader: XmlReader) =
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

        member this.getFileContent(?xmlReader: XmlReader) =
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

        member this.getFileDescription(?xmlReader: XmlReader) =
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

        member this.getMzIOModel() =
            let rec outerLoop acc =
                if reader.Name = "mzML" then
                    let readSubtree = reader.ReadSubtree()
                    let readOp = readSubtree.Read
                    let rec loop name fileDes samples softwares instruments dataProcessings run read =
                        if readSubtree.NodeType=XmlNodeType.Element then
                            match readSubtree.Name with
                            | "mzML"                        -> let tmp = (this.tryGetAttribute ("id", readSubtree))
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

        member this.getSpectra(runID) =
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
                                            failwith "Invalid runID"
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

        member private this.tryGetSpectrum(spectrumID: string) =
            let readSubtree =
                let rec loop acc =
                    if reader.NodeType=XmlNodeType.Element && reader.Name="spectrum" then
                        reader.ReadSubtree()
                    else loop (reader.Read())
                loop false
            let readOp = readSubtree.Read
            let mutable spectrum = new MassSpectrum()
            let rec loop id sourceRef cvParams userParams scans precs products read =
                if readSubtree.NodeType=XmlNodeType.Element then
                    match readSubtree.Name with
                    | "spectrum"                    -> if (this.getAttribute ("id", readSubtree)) = spectrumID then
                                                            loop
                                                                (this.getAttribute ("id", readSubtree))
                                                                (this.tryGetAttribute ("sourceFileRef", readSubtree))
                                                                cvParams userParams scans precs products
                                                                (readOp() |> ignore)
                                                       else 
                                                            None
                    | "referenceableParamGroupRef"  -> loop id sourceRef cvParams userParams scans precs products (readOp() |> ignore)
                    | "cvParam"                     -> loop id sourceRef ((this.getCVParam readSubtree)::cvParams) userParams scans precs products (readOp() |> ignore)
                    | "userParam"                   -> loop id sourceRef cvParams ((this.getUserParam readSubtree)::userParams) scans precs products   (readOp() |> ignore)
                    | "scanList"                    -> loop id sourceRef cvParams userParams (this.getScanList readSubtree) precs products read
                    | "precursorList"               -> loop id sourceRef cvParams userParams scans (this.getPrecursorList readSubtree) products read
                    | "productList"                 -> loop id sourceRef cvParams userParams scans precs (this.getProductList readSubtree) read
                    | "binaryDataArrayList"         -> spectrum <- new MassSpectrum(id, precs, scans, products, if sourceRef.IsSome then sourceRef.Value else null)
                                                       (this.getDataProcessingReference (spectrum, readSubtree))
                                                       cvParams
                                                       |> List.iter(fun cvParam -> spectrum.AddCvParam cvParam)
                                                       userParams
                                                       |> List.iter(fun userParam -> spectrum.AddUserParam userParam)
                                                       Some spectrum
                    |   _                           -> Some spectrum
                else
                    if readOp()=true then loop id sourceRef cvParams userParams scans precs products read
                    else Some spectrum

            loop null None [] [] (new ScanList()) (new PrecursorList()) (new ProductList()) ()

        member this.getSpectrum(spectrumID: string) =
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
                    | "binaryDataArrayList" ->  Some (this.translatePeak1DArray readSubtree)
                    |   _                   ->  loop (readOp() |> ignore)
                else
                    if readOp()=true then loop read
                    else None
            loop ()

        member private this.getSpecificPeak1DArray(spectrumID:string) =
            let rec outerLoop acc =
                if reader.Name = "spectrumList" then
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
                                    if readOp()=true then loop acc
                                    else 
                                        failwith "Invalid spectrumID"
                    loop ()
                else
                    outerLoop (reader.Read())
            outerLoop false

        member this.ReadChromatogramms(runID) =
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

        member this.tryGetPeak2DArray(chromatogramID:string) =
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
                    | "binaryDataArrayList" -> Some (this.translatePeak2DArray readSubtree)
                    |   _                   -> loop (readOp() |> ignore)
                else
                    if readOp()=true then loop read
                    else None
            loop ()

        member this.getPeak2DArrays(chromatogramID:string) =
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
                                    |   _            ->  (readOp()) |> ignore
                                                         loop acc
                                else
                                    if readOp()=true then loop acc
                                    else 
                                        failwith "Invalid chromatogramID"
                    loop ()
                else
                    outerLoop (reader.Read())
            outerLoop false

        member private this.model = MzIOJson.HandleExternalModelFile(this, MzMLReader.GetModelFilePath(filePath))

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
                let sample = new Sample("sample_1", sampleName);
                model.Samples.Add(sample)

                let run = new Run("run_1")
                run.Sample <- sample
                model.Runs.Add(run)
                model

            member this.Model =
                //this.RaiseDisposed()
                this.model

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

        member this.CreateDefaultModel() =

            (this :> IMzIOIO).CreateDefaultModel()

        member this.SaveModel() =

            (this :> IMzIOIO).SaveModel()

        member this.Model =
            (this :> IMzIOIO).Model

        interface IMzIODataReader with
    
            member this.ReadMassSpectra(runID: string) = 
                this.getSpectra(runID)

            member this. ReadMassSpectrum(spectrumID: string) =
                this.getSpectrum(spectrumID)

            member this.ReadSpectrumPeaks(spectrumID: string) =
                this.getSpecificPeak1DArray(spectrumID)

            member this.ReadMassSpectrumAsync(spectrumID: string) =
                Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSpectrum(spectrumID))

            member this.ReadSpectrumPeaksAsync(spectrumID: string) =
                Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))

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

        member this.ReadMassSpectra(runID:string)               =

            (this :> IMzIODataReader).ReadMassSpectra(runID)

        member this.ReadMassSpectrum(spectrumID:string)         =

            (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

        member this.ReadSpectrumPeaks(spectrumID:string)        =

            (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

        member this.ReadMassSpectrumAsync(spectrumID:string)    =

            (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

        member this.ReadSpectrumPeaksAsync(spectrumID:string)   =

            (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

        member this.ReadChromatograms(runID:string)             =

            (this :> IMzIODataReader).ReadChromatograms(runID)

        member this.ReadChromatogramPeaks(runID:string)         =

            (this :> IMzIODataReader).ReadChromatogramPeaks(runID)

        member this.ReadChromatogramAsync(runID:string)         =

            (this :> IMzIODataReader).ReadChromatogramAsync(runID)

        member this.ReadChromatogramPeaksAsync(runID:string)    =

            (this :> IMzIODataReader).ReadChromatogramPeaksAsync(runID)