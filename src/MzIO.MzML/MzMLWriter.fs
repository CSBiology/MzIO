namespace MzIO.IO.MzML


open System
open System.Globalization
open System.Xml
open System.Linq
open System.IO
open System.IO.Compression
open System.Collections.Generic
open System.Threading.Tasks
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.MetaData
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.PSIMSExtension
open NumpressHelper
open MzIO.Binary
open MzIO.IO
open MzIO.Model.CvParam


/// Contains segment names of the MzML file to check the position and state of the writer.
type private MzMLWriteState =
    ERROR
    | INITIAL
    | CLOSED
    | MzIOModel
    | RUN
    | SPECTRUM_LIST
    | SPECTRUM
    | CHROMATOGRAM_LIST
    | CHROMATOGRAM

/// Contains methods to encode and compress collections of doubles.
[<Sealed>]
type private MzMLCompression(?initialBufferSize:int) =

    let bufferSize = defaultArg initialBufferSize 1048576
    
    let mutable memoryStream' = new MemoryStream(bufferSize)

    member this.memoryStream
        with get() = memoryStream'
        and private set(value) = memoryStream' <- value

    /// Write value as the corresponding type.
    static member WriteValue(writer:BinaryWriter, binaryDataType:BinaryDataType, value:double) =
        match binaryDataType with
        | BinaryDataType.Int32      ->  writer.Write(int32 value)
        | BinaryDataType.Int64      ->  writer.Write(int64 value)
        | BinaryDataType.Float32    ->  writer.Write(single value)
        | BinaryDataType.Float64    ->  writer.Write(double value)
        | _     -> failwith (sprintf "%s%s" "BinaryDataType not supported: " (binaryDataType.ToString()))    

    /// Write bytes directly becasue they musn't be compressed.
    static member private NoCompression(memoryStream:Stream, dataType:BinaryDataType, floats:double[]) =        
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        for item in floats do
            MzMLCompression.WriteValue(writer, dataType, item)

    /// Compress bytes using zlib compression method.
    static member private DeflateStreamCompress (data: Byte []) =
        //use mStream = new MemoryStream(data)
        //(
        // use outStream = new MemoryStream()
        // use compress = new DeflateStream (outStream, CompressionMode.Compress, true)      
        // mStream.CopyTo(compress)
        // compress.Close() 
        // let byteArray = outStream.ToArray()
        // byteArray
        //)
        use input               = new MemoryStream(data)
        use outStream           = new MemoryStream()
        use compressorStream    = new DeflateStream(outStream, CompressionMode.Compress, true)
        (
            
            input.CopyTo(compressorStream)
            compressorStream.Close()
            outStream.ToArray()
        )

    /// Convert array of int32s to array of bytes.
    static member private int32sToBytes (peaks: float[]) =
        let data        =  peaks |> Array.map (fun item -> int32 item)
        let byteArray   = Array.zeroCreate<byte> (data.Length*4)
        Buffer.BlockCopy (data, 0, byteArray, 0, byteArray.Length)
        byteArray

    /// Convert array of int64s to array of bytes.
    static member private int64sToBytes (peaks: float[]) =
        let data        =  peaks |> Array.map (fun item -> int64 item)
        let byteArray   = Array.zeroCreate<byte> (data.Length*4)
        Buffer.BlockCopy (data, 0, byteArray, 0, byteArray.Length)
        byteArray

    /// Convert array of singles to array of bytes.
    static member private singlesToBytes (peaks: float[]) =
        let data        =  peaks |> Array.map (fun item -> float32 item)
        let byteArray   = Array.zeroCreate<byte> (data.Length*4)
        Buffer.BlockCopy (data, 0, byteArray, 0, byteArray.Length)
        byteArray

    /// Convert array of doubles to array of bytes.
    static member private floatsToBytes (peaks: float[]) =
        let data        =  peaks |> Array.map (fun item -> item)
        let byteArray   = Array.zeroCreate<byte> (data.Length*4)
        Buffer.BlockCopy (data, 0, byteArray, 0, byteArray.Length)
        byteArray

    /// Compress bytes using zlib compression method.
    static member private ZLib(memoryStream:Stream, dataType:BinaryDataType, peaks:float[]) =
        let bytes       = 
            match dataType with
            | BinaryDataType.Int32      -> MzMLCompression.int32sToBytes(peaks  |> Array.ofSeq)
            | BinaryDataType.Int64      -> MzMLCompression.int64sToBytes(peaks  |> Array.ofSeq)
            | BinaryDataType.Float32    -> MzMLCompression.singlesToBytes(peaks |> Array.ofSeq)
            | BinaryDataType.Float64    -> MzMLCompression.floatsToBytes(peaks  |> Array.ofSeq)
            |   _                       -> failwith "Not supported BinaryDataType"            
        let byteDeflate = MzMLCompression.DeflateStreamCompress bytes
        let writer      = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(Array.append [|120uy; 156uy|] byteDeflate)

    /// Compress double array based on numpress pic compression method.
    static member private NumpressPicCompression(memoryStream:Stream, values:double[]) =
        let encData = NumpressEncodingHelpers.encodePIC values
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(encData.NumberEncodedBytes)
        writer.Write(encData.OriginalDataLength)
        writer.Write(encData.Bytes)

    /// Compress double array based on numpress lin compression method.
    static member private NumpressLinCompression(memoryStream:Stream, values:double[]) =
        let encData = NumpressEncodingHelpers.encodeLin values
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(encData.NumberEncodedBytes)
        writer.Write(encData.OriginalDataLength)
        writer.Write(encData.Bytes)

    /// Compress double array based on numpress pic and zlib compression method.
    static member private NumpressPicAndDeflateCompression(memoryStream:Stream, values:double[]) =
        let encData = NumpressEncodingHelpers.encodePIC values
        //let deflateEncData = MzMLCompression.DeflateStreamCompress encData.Bytes
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        //writer.Write(encData.NumberEncodedBytes)
        //writer.Write(encData.OriginalDataLength)
        let numberEncodedBytes = BitConverter.GetBytes(encData.NumberEncodedBytes) 
        let originalDataLength = BitConverter.GetBytes(encData.OriginalDataLength)
        let lengths = Array.append numberEncodedBytes originalDataLength
        // ZLIBConversion 
        let byteDeflate = MzMLCompression.DeflateStreamCompress (Array.append lengths encData.Bytes)
        writer.Write(Array.append [|120uy; 156uy|] byteDeflate)
        //writer.Write(MzMLCompression.DeflateStreamCompress encData.Bytes)

    /// Compress double array based on numpress lin and zlib compression method.
    static member private NumpressLinAndDeflateCompression(memoryStream:Stream, values:double[]) =
        let encData = NumpressEncodingHelpers.encodeLin values
        //let deflateEncData = MzMLCompression.DeflateStreamCompress encData.Bytes
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        //writer.Write(encData.NumberEncodedBytes)
        //writer.Write(encData.OriginalDataLength)
        let numberEncodedBytes = BitConverter.GetBytes(encData.NumberEncodedBytes) 
        let originalDataLength = BitConverter.GetBytes(encData.OriginalDataLength)
        let lengths = Array.append numberEncodedBytes originalDataLength
        // ZLIBConversion 
        let byteDeflate = MzMLCompression.DeflateStreamCompress (Array.append lengths encData.Bytes)
        writer.Write(Array.append [|120uy; 156uy|] byteDeflate)
        //writer.Write(MzMLCompression.DeflateStreamCompress encData.Bytes)

    /// Compress intensity values with numpress pic and m/z values with numpress lin and afterwards both with zlib compression method.
    static member private NumpressDeflate(compressionType:BinaryDataCompressionType, memoryStream:Stream, peaks:float[]) =
        
        match compressionType with
        | BinaryDataCompressionType.NumPressPic -> MzMLCompression.NumpressPicAndDeflateCompression(memoryStream, peaks)
        | BinaryDataCompressionType.NumPressLin -> MzMLCompression.NumpressLinAndDeflateCompression(memoryStream, peaks)
        |   _                                   -> failwith (sprintf "NumPressZLibCompression type not supported: %s" (compressionType.ToString()))
        
    /// Compress intensity values with numpress pic and m/z values with numpress lin compression method.
    static member private Numpress(compressionType:BinaryDataCompressionType, memoryStream:Stream, peaks:float[]) =
        
        match compressionType with
        | BinaryDataCompressionType.NumPressPic -> MzMLCompression.NumpressPicCompression(memoryStream, peaks)
        | BinaryDataCompressionType.NumPressLin -> MzMLCompression.NumpressLinCompression(memoryStream, peaks)
        |   _                                   -> failwith (sprintf "NumPressCompression type not supported: %s" (compressionType.ToString()))

    /// Convert array of floats to array of bytes which are compressed, based on the chosen method.
    member this.Encode(compressionType:BinaryDataCompressionType, dataType:BinaryDataType, peaks:float[], numPressCompressionType:BinaryDataCompressionType) =
       
        this.memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore
        match compressionType with
        | BinaryDataCompressionType.NoCompression   -> MzMLCompression.NoCompression(this.memoryStream, dataType, peaks)
        | BinaryDataCompressionType.ZLib            -> MzMLCompression.ZLib(this.memoryStream, dataType, peaks)
        | BinaryDataCompressionType.NumPress        -> MzMLCompression.Numpress(numPressCompressionType, this.memoryStream, peaks)
        | BinaryDataCompressionType.NumPressZLib    -> MzMLCompression.NumpressDeflate(numPressCompressionType, this.memoryStream, peaks)
        |   _                                       -> failwith (sprintf "Compression type not supported: %s" (compressionType.ToString()))        
        this.memoryStream.ToArray()

[<Sealed>]
type private MzMLTransactionScope() =

    interface IDisposable with

        member this.Dispose() =
            ()

    member this.Dispose() =

        (this :> IDisposable).Dispose()

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

/// Contains methods to create a MzML file based on MzIO model.
[<Sealed>]
type MzMLWriter(path:string) =

    /// Binding of the XMLWriter.
    let writer =
        let mutable writerSettings = new XmlWriterSettings()
        writerSettings.Indent <- true
        XmlWriter.Create(path, writerSettings)

    let mutable isClosed = false

    let mutable currentWriteState = MzMLWriteState.INITIAL

    let mutable consumedWriteStates = new HashSet<MzMLWriteState>()

    let mutable model = new MzIOModel(Path.GetFileNameWithoutExtension(path))

    /// Closes connection to MzML file and prohibits further manipulation with this instance of the writer object.
    member private this.Close() =

        if isClosed = false then

            try
                this.EnterWriteState(MzMLWriteState.INITIAL, MzMLWriteState.CLOSED)
                writer.WriteEndDocument()
                writer.Flush()
                writer.Close()
                writer.Dispose()

                isClosed <- true

            with
                | :? Exception as ex ->
                    currentWriteState <- MzMLWriteState.ERROR
                    raise (new MzIOIOException("Error closing mzml output file.", ex))
        else ()

    /// Writes everything into MzML file, closes the connection and disposes everything left.
    member this.Commit() =
        writer.Close()
        writer.Dispose()

    /// Checks current write state and repalces it with another one. Musn't be the same!
    member private this.EnterWriteState(expectedWs:MzMLWriteState, newWs:MzMLWriteState) =
        if consumedWriteStates.Contains(newWs) then
            raise (MzIOIOException(String.Format("Can't reentering write state: '{0}'.", newWs)))
        this.EnsureWriteState(expectedWs)
        currentWriteState <- newWs
        consumedWriteStates.Add(newWs) |> ignore

    /// Checks wheter the write state is not the expected, error or closed and breaks then.
    member private this.EnsureWriteState(expectedWs:MzMLWriteState) =
        if currentWriteState = MzMLWriteState.ERROR then
            raise (new MzIOIOException("Current write state is ERROR."))
        if currentWriteState = MzMLWriteState.CLOSED then
            raise (new MzIOIOException("Current write state is CLOSED."))
        if currentWriteState <> expectedWs then
            raise (MzIOIOException(String.Format("Invalid write state: expected '{0}' but current is '{1}'.", expectedWs, currentWriteState)))

    /// Creates a cvParam element and inserts it into the MzML file.
    member private this.WriteCvParam(param:CvParam<#IConvertible>) =
        let potValue = tryGetValue (param :> IParamBase<#IConvertible>)
        let value =
            match potValue with
            | Some value -> if value = null then "" else value.ToString()
            | None       -> ""
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", param.CvAccession)        
        writer.WriteAttributeString("name", "not saved yet")
        writer.WriteAttributeString("value", value)
        if (tryGetCvUnitAccession (param :> IParamBase<#IConvertible>)).IsSome then
            writer.WriteAttributeString("unitCvRef", "UO")
            writer.WriteAttributeString("unitAccession", (tryGetCvUnitAccession (param :> IParamBase<#IConvertible>)).Value.ToString())
            writer.WriteAttributeString("unitName", "not saved yet")        
        writer.WriteEndElement()

    /// Creates a userParam element and inserts it into the MzML file.
    member private this.WriteUserParam(param:UserParam<#IConvertible>) =
        writer.WriteStartElement("userParam")
        writer.WriteAttributeString("name", param.Name)
        //writer.WriteAttributeString("type", "not saved yet")
        writer.WriteAttributeString("value", if (tryGetValue (param :> IParamBase<#IConvertible>)).IsSome then 
                                                (tryGetValue (param :> IParamBase<#IConvertible>)).Value.ToString() else "")
        if (tryGetCvUnitAccession (param :> IParamBase<#IConvertible>)).IsSome then
            writer.WriteAttributeString("unitCvRef", "UO")
            writer.WriteAttributeString("unitAccession", (tryGetCvUnitAccession (param :> IParamBase<#IConvertible>)).Value.ToString())
            writer.WriteAttributeString("unitName", "not saved yet")        
        writer.WriteEndElement()

    /// Creates a cvList element and inserts it into the MzML file.
    member private this.WriteCvList() =
        writer.WriteStartElement("cvList")
        writer.WriteAttributeString("count", "2")
        writer.WriteStartElement("cv")
        writer.WriteAttributeString("id", "MS")
        writer.WriteAttributeString("fullName", "Proteomics Standards Initiative Mass Spectrometry Ontology")
        writer.WriteAttributeString("version", "2.26.0" )
        writer.WriteAttributeString("URI", "http://psidev.cvs.sourceforge.net/*checkout*/psidev/psi/psi-ms/mzML/controlledVocabulary/psi-ms.obo")
        writer.WriteEndElement()
        writer.WriteStartElement("cv")
        writer.WriteAttributeString("id", "UO")
        writer.WriteAttributeString("fullName", "Unit Ontology")
        writer.WriteAttributeString("version", "14:07:2009")
        writer.WriteAttributeString("URI", "http://obo.cvs.sourceforge.net/*checkout*/obo/obo/ontology/phenotype/unit.obo")
        writer.WriteEndElement()
        writer.WriteEndElement()

    /// Checks whether item is a cv or user param and assigns then the right writing function.
    member private this.assignParam<'T when 'T :> IConvertible>(item:Object) =
        match item with
        | :? CvParam<'T>     -> this.WriteCvParam    (item :?> CvParam<'T>)
        | :? UserParam<'T>   -> this.WriteUserParam  (item :?> UserParam<'T>)
        |   _ -> failwith "Not castable to Cv nor UserParam"

    /// Creates a detector element and inserts it into the MzML file.
    member private this.WriteDetector(item:DetectorComponent) =
        writer.WriteStartElement("detector")
        writer.WriteAttributeString("order", "3")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    /// Creates a detector element and inserts it into the MzML file.
    member private this.WriteAnalyzer(item:AnalyzerComponent) =
        writer.WriteStartElement("analyzer")
        writer.WriteAttributeString("order", "2")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    /// Creates a source element and inserts it into the MzML file.
    member private this.WriteSource(item:SourceComponent) =
        writer.WriteStartElement("source")
        writer.WriteAttributeString("order", "1")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    /// Creates a processingMethod element and inserts it into the MzML file.
    member private this.WriteProcessingMethod(item:DataProcessingStep) =
        writer.WriteStartElement("processingMethod")
        writer.WriteAttributeString("order", item.Name)
        writer.WriteAttributeString("softwareRef", item.Software.ID)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    /// Creates a softwareRef element and inserts it into the MzML file.
    member private this.WriteSoftwareRef(item:Software) =
        writer.WriteStartElement("softwareRef")
        writer.WriteAttributeString("ref", item.ID)
        writer.WriteEndElement()

    /// Checks whether item is a source, analyzer or detector component and assigns then the right writing function.
    member private this.assignComponent(item:Object) =
        match item with
        | :? SourceComponent    -> this.WriteSource     (item :?> SourceComponent)
        | :? AnalyzerComponent  -> this.WriteAnalyzer   (item :?> AnalyzerComponent)
        | :? DetectorComponent  -> this.WriteDetector   (item :?> DetectorComponent)
        |   _ -> failwith "Not castable to SourceComponent nor AnalyzerComponent nor DetectorComponent"

    /// Creates a componentList element and inserts it into the MzML file.
    member private this.WriteComponentList(item:ComponentList) =
        writer.WriteStartElement("componentList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun component' -> this.assignComponent(component'.Value))
        writer.WriteEndElement()

    /// Creates a sourceFile element and inserts it into the MzML file.
    member private this.WriteSourceFile(item:SourceFile) =
        writer.WriteStartElement("sourceFile")
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("location", item.Location)
        writer.WriteAttributeString("name", item.Name)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    /// Creates a selectedIon element and inserts it into the MzML file.
    member private this.WriteSelectedIon(item:SelectedIon) =
        writer.WriteStartElement("selectedIon")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    /// Creates a scanWindow element and inserts it into the MzML file.
    member private this.WriteScanWindow(item:ScanWindow) =
        writer.WriteStartElement("scanWindow")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    /// Creates a binary element and inserts it into the MzML file.
    member private this.WriteBinary(item:string) =
        writer.WriteElementString("binary", item)

    /// Creates a activation element and inserts it into the MzML file.
    member private this.WriteActivation(item:Activation) =
        writer.WriteStartElement("activation")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()
    
    /// Creates a selectedIonList element and inserts it into the MzML file.
    member private this.WriteSelectedIonList(item:SelectedIonList) =
        if item.Count() > 0 then
            writer.WriteStartElement("selectedIonList")
            writer.WriteAttributeString("count", item.Count().ToString())
            item.GetProperties false
            |> Seq.iter (fun selectedIon -> this.WriteSelectedIon(selectedIon.Value :?> SelectedIon))
            writer.WriteEndElement()

    /// Creates a isolationWindow element and inserts it into the MzML file.
    member private this.WriteIsolationWindow(item:IsolationWindow) =
        if item.Count() > 0 then
            writer.WriteStartElement("isolationWindow")
            item.GetProperties false
            |> Seq.iter (fun param ->
                this.assignParam param.Value)
            writer.WriteEndElement()

    /// Creates a scanWindowList element and inserts it into the MzML file.
    member private this.WriteScanWindowList(item:ScanWindowList) =
        if item.Count() > 0 then
            writer.WriteStartElement("scanWindowList")
            writer.WriteAttributeString("count", item.Count().ToString())
            item.GetProperties false
            |> Seq.iter (fun scanWindow -> this.WriteScanWindow(scanWindow.Value :?> ScanWindow))
            writer.WriteEndElement()    

    /// Assign the cv accession based on the BinaryDataType.
    static member private assignBinaryDataType(item:BinaryDataType) =
        match item with
        | BinaryDataType.Int32      -> "MS:1000519"
        | BinaryDataType.Int64      -> "MS:1000522"
        | BinaryDataType.Float32    -> "MS:1000521"
        | BinaryDataType.Float64    -> "MS:1000523"
        | _ -> failwith "BinaryDataType is unknown"

    /// Creates a cvParam element for compression type and inserts it into the MzML file.
    member private this.WriteCvParamCompression(accession:string, name:string) =
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", accession)        
        writer.WriteAttributeString("name", name)
        writer.WriteAttributeString("value", "")
        writer.WriteEndElement()

    /// Creates a userParam element for compression and inserts it into the MzML file.
    member private this.WriteUserParamCompression(name:string) =
        writer.WriteStartElement("userParam")
        writer.WriteAttributeString("name", name)
        writer.WriteAttributeString("value", "")       
        writer.WriteEndElement()

    /// Assign the cv accession based on the BinaryDataCompressionType.
    member private this.writeCompressionParam(item:BinaryDataCompressionType) =
        match item with
        | BinaryDataCompressionType.NoCompression   -> this.WriteCvParamCompression("MS:1000576", "NoCompression")
        | BinaryDataCompressionType.ZLib            -> this.WriteCvParamCompression("MS:1000574", "ZLib")
        //| BinaryDataCompressionType.NumPressZLib    -> this.WriteUserParamCompression("NumPressZLib")
        | BinaryDataCompressionType.NumPressPic     -> this.WriteCvParamCompression("MS:1002313", "NumPressPic")
        | BinaryDataCompressionType.NumPressLin     -> this.WriteCvParamCompression("MS:1002314", "NumPressLin")
        | _ -> failwith "BinaryDataCompressionType is unknown"    

    /// Write all cv params that are needed for a bianry data array of m/z values.
    member private this.WriteQParams(item:Peak1DArray) =
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", (MzMLWriter.assignBinaryDataType item.MzDataType).ToString())
        writer.WriteAttributeString("name", item.MzDataType.ToString())
        writer.WriteAttributeString("value", "")
        writer.WriteEndElement()

        match item.CompressionType with
        | BinaryDataCompressionType.NumPress        -> this.writeCompressionParam BinaryDataCompressionType.NumPressLin
        | BinaryDataCompressionType.NumPressZLib    -> 
            this.writeCompressionParam BinaryDataCompressionType.NumPressLin
            this.writeCompressionParam BinaryDataCompressionType.ZLib
        | _                                         -> this.writeCompressionParam item.CompressionType

        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", "MS:1000514")      
        writer.WriteAttributeString("name", "m/z array")
        writer.WriteAttributeString("value", "")
        writer.WriteAttributeString("unitCvRef", "UO")
        writer.WriteAttributeString("unitAccession", "MS:1000040")
        writer.WriteAttributeString("unitName", "m/z")
        writer.WriteEndElement()

    /// Write all cv params that are needed for a bianry data array of intensity values.
    member private this.WriteIntensityParams(item:Peak1DArray) =
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", (MzMLWriter.assignBinaryDataType item.MzDataType).ToString())
        writer.WriteAttributeString("name", "not saved yet")
        writer.WriteAttributeString("value", "")
        writer.WriteEndElement()

        match item.CompressionType with
        | BinaryDataCompressionType.NumPress        -> this.writeCompressionParam BinaryDataCompressionType.NumPressPic
        | BinaryDataCompressionType.NumPressZLib    -> 
            this.writeCompressionParam BinaryDataCompressionType.NumPressPic
            this.writeCompressionParam BinaryDataCompressionType.ZLib
        | _                                         -> this.writeCompressionParam item.CompressionType

        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", "MS:1000515")      
        writer.WriteAttributeString("name", "intensity array")
        writer.WriteAttributeString("value", "")
        writer.WriteAttributeString("unitCvRef", "UO")
        writer.WriteAttributeString("unitAccession", "MS:1000131")
        writer.WriteAttributeString("unitName", "number of counts")
        writer.WriteEndElement()

    /// Creates a binaryDataArray element and inserts it into the MzML file.
    member private this.WriteBinaryDataArray(spectrum:MassSpectrum, peaks:Peak1DArray) =
        //printfn "WriteBinaryDataArray spectrum %s" spectrum.ID
        let encoder = new MzMLCompression()
        let mzs = 
            peaks.Peaks
            |> Seq.map (fun peak -> peak.Mz)
            |> Array.ofSeq
        let encodedMzs = Convert.ToBase64String(encoder.Encode(peaks.CompressionType, peaks.MzDataType, mzs, BinaryDataCompressionType.NumPressLin))
        //printfn "encodedMzs %i" encodedMzs.Length
        writer.WriteStartElement("binaryDataArray")
        writer.WriteAttributeString("arrayLength", mzs.Length.ToString())
        if not (String.IsNullOrWhiteSpace spectrum.DataProcessingReference) then 
            writer.WriteAttributeString("dataProcessingRef", spectrum.DataProcessingReference)
        writer.WriteAttributeString("encodedLength", encodedMzs.Length.ToString())        
        this.WriteQParams(peaks)
        this.WriteBinary(encodedMzs)
        writer.WriteFullEndElement()

        let intensities = 
            peaks.Peaks
            |> Seq.map (fun peak -> peak.Intensity)
            |> Array.ofSeq
        let encodedIntensities = Convert.ToBase64String(encoder.Encode(peaks.CompressionType, peaks.IntensityDataType, intensities, BinaryDataCompressionType.NumPressPic))
        writer.WriteStartElement("binaryDataArray")
        writer.WriteAttributeString("arrayLength", intensities.Length.ToString())
        if not (String.IsNullOrWhiteSpace spectrum.DataProcessingReference) then 
            writer.WriteAttributeString("dataProcessingRef", spectrum.DataProcessingReference)
        writer.WriteAttributeString("encodedLength", encodedIntensities.Length.ToString())
        this.WriteIntensityParams(peaks)
        this.WriteBinary(encodedIntensities)
        writer.WriteFullEndElement()  

    /// Creates a product element and inserts it into the MzML file.
    member private this.WriteProduct(item:Product) =
        writer.WriteStartElement("product")
        this.WriteIsolationWindow(item.IsolationWindow)
        writer.WriteEndElement()  

    /// Creates a precursor element and inserts it into the MzML file.
    member private this.WritePrecursor(item:Precursor) =
        writer.WriteStartElement("precursor")
        //writer.WriteAttributeString("externalSpectrumID", item.SpectrumReference.SpectrumID)
        if not (String.IsNullOrWhiteSpace item.SpectrumReference.SourceFileID) then 
            writer.WriteAttributeString("sourceFileRef", item.SpectrumReference.SourceFileID)
        if not (String.IsNullOrWhiteSpace item.SpectrumReference.SpectrumID) then 
            writer.WriteAttributeString("spectrumRef", item.SpectrumReference.SpectrumID)
        this.WriteIsolationWindow(item.IsolationWindow)
        this.WriteSelectedIonList(item.SelectedIons)
        this.WriteActivation(item.Activation)
        writer.WriteEndElement()
    
    /// Creates a scan element and inserts it into the MzML file.
    member private this.WriteScan(item:Scan) =
        writer.WriteStartElement("scan")
        //writer.WriteAttributeString("externalSpectrumID", item.SpectrumReference.SpectrumID)
        //writer.WriteAttributeString("instrumentConfigurationRef", "not saved yet")
        if not (String.IsNullOrWhiteSpace item.SpectrumReference.SourceFileID) then 
            writer.WriteAttributeString("sourceFileID", item.SpectrumReference.SourceFileID)
        if not (String.IsNullOrWhiteSpace item.SpectrumReference.SpectrumID) then 
            writer.WriteAttributeString("spectrumID", item.SpectrumReference.SpectrumID)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        if item.Count() > 0 then
            this.WriteScanWindowList(item.ScanWindows)
        writer.WriteEndElement()

    /// Creates a binaryDataArrayList element and inserts it into the MzML file.
    member private this.WriteBinaryDataArrayList(spectrum:MassSpectrum, peaks:Peak1DArray) =
        writer.WriteStartElement("binaryDataArrayList")
        writer.WriteAttributeString("count", "2")
        this.WriteBinaryDataArray(spectrum, peaks)
        writer.WriteEndElement()

    /// Creates a productList element and inserts it into the MzML file.
    member private this.WriteProductList(item:ProductList) =
        writer.WriteStartElement("productList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun product -> this.WriteProduct(product.Value :?> Product))
        writer.WriteEndElement()

    /// Creates a precursorList element and inserts it into the MzML file.
    member private this.WritePrecursorList(item:PrecursorList) =
        writer.WriteStartElement("precursorList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun precursor -> this.WritePrecursor(precursor.Value :?> Precursor))
        writer.WriteEndElement()

    /// Creates a scanList element and inserts it into the MzML file.
    member private this.WriteScanList<'T when 'T :> IConvertible>(item:ScanList) =
        let scans = (item.GetProperties false) |> Seq.filter (fun value -> value.Value :? Scan)
        writer.WriteStartElement("scanList")
        writer.WriteAttributeString("count", scans.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun param -> 
            match param.Value with
            | :? CvParam<'T>    -> this.assignParam param.Value
            | :? UserParam<'T>  -> this.assignParam param.Value
            | :? Scan           -> (*this.WriteScan(param.Value :?> Scan)*) ()
            |   _   -> failwith "wrong item got isnerted in scanList"
                    )
        scans
        |> Seq.iter (fun scan -> this.WriteScan(scan.Value :?> Scan))
        writer.WriteEndElement()

    //Talk once more about chromatogram with dave and timo
    member private this.WriteChromatogram(item:Chromatogram, peaks:Peak2DArray) =
        this.EnsureWriteState(MzMLWriteState.CHROMATOGRAM)
        failwith "not supported yet"
        //writer.WriteStartElement("chromatogram")
        //writer.WriteAttributeString("count", peaks.)
        //spectrum.GetProperties false
        //|> Seq.iter (fun param -> this.assignParam(param.Value))
        //writer.WriteEndElement()

    /// Creates a spectrum element and inserts it into the MzML file.
    member private this.WriteSpectrum(spectrum:MassSpectrum, index:int, peaks:Peak1DArray) =
        this.EnsureWriteState(MzMLWriteState.SPECTRUM)
        writer.WriteStartElement("spectrum")
        if not (String.IsNullOrWhiteSpace spectrum.DataProcessingReference) then 
            writer.WriteAttributeString("dataProcessingRef", spectrum.DataProcessingReference)
        writer.WriteAttributeString("defaultArrayLength", peaks.Peaks.Count().ToString())
        writer.WriteAttributeString("id", spectrum.ID)
        writer.WriteAttributeString("index", sprintf "%i" index)
        if not (String.IsNullOrWhiteSpace spectrum.SourceFileReference) then 
            writer.WriteAttributeString("sourceFileRef", spectrum.SourceFileReference)            
        //writer.WriteAttributeString("spotID", "not saved yet")
        spectrum.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        if spectrum.Scans.Count() <> 0 then
            this.WriteScanList(spectrum.Scans)
        if spectrum.Precursors.Count() <> 0 then
            this.WritePrecursorList(spectrum.Precursors)
        if spectrum.Products.Count() <> 0 then
            this.WriteProductList(spectrum.Products)
        this.WriteBinaryDataArrayList(spectrum, peaks)
        writer.WriteEndElement()
        index

    /// Creates a chromatogramList element and inserts it into the MzML file.
    member private this.WriteChromatogramList(item:Run, chromatogramListCount:int) =
        this.EnsureWriteState(MzMLWriteState.CHROMATOGRAM_LIST)
        writer.WriteStartElement("chromatogramList") 
        writer.WriteAttributeString("count", chromatogramListCount.ToString())
        writer.WriteAttributeString("defaultDataProcessingRef", item.DefaultChromatogramProcessing.ID)
        this.EnterWriteState(MzMLWriteState.CHROMATOGRAM_LIST, MzMLWriteState.CHROMATOGRAM)
        //this.WriteChromatogram()
        writer.WriteEndElement()

    /// Creates a spectrumList element and inserts it into the MzML file.
    member private this.WriteSpectrumList(item:Run, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>) =
        this.EnsureWriteState(MzMLWriteState.SPECTRUM_LIST)
        writer.WriteStartElement("spectrumList") 
        writer.WriteAttributeString("count", (Seq.length spectra).ToString())
        writer.WriteAttributeString("defaultDataProcessingRef", item.DefaultSpectrumProcessing.ID)
        this.EnterWriteState(MzMLWriteState.SPECTRUM_LIST, MzMLWriteState.SPECTRUM)
        Seq.fold2 (fun start spectrum peak -> this.WriteSpectrum(spectrum, start + 1, peak)) 0 spectra peaks |> ignore
        writer.WriteEndElement()

    /// Creates a dataProcessing element and inserts it into the MzML file.
    member private this.WriteDataProcessing(item:DataProcessing) =
        writer.WriteStartElement("dataProcessing") 
        writer.WriteAttributeString("id", item.ID)
        item.ProcessingSteps.GetProperties false
        |> Seq.iter (fun dataStep -> this.WriteProcessingMethod (dataStep.Value :?> DataProcessingStep))
        writer.WriteEndElement()

    /// Creates a instrumentConfiguration element and inserts it into the MzML file.
    member private this.WriteInstrumentConfiguration(item:Instrument) =
        writer.WriteStartElement("instrumentConfiguration")
        writer.WriteAttributeString("id", item.ID)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        this.WriteComponentList item.Components
        this.WriteSoftwareRef item.Software
        writer.WriteEndElement()

    /// Creates a software element and inserts it into the MzML file.
    member private this.WriteSoftware(item:Software) =
        writer.WriteStartElement("software")
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("version", "not saved yet")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    /// Creates a sample element and inserts it into the MzML file.
    member private this.WriteSample(item:Sample) =
        writer.WriteStartElement("sample")
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("name", item.Name)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    /// Creates a contact element and inserts it into the MzML file.
    member private this.WriteContact(item:Contact) =
        writer.WriteStartElement("contact")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    /// Creates a sourceFileList element and inserts it into the MzML file.
    member private this.WriteSourceFileList(item:SourceFileList) =
        writer.WriteStartElement("sourceFileList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun source -> this.WriteSourceFile (source.Value :?> SourceFile))
        writer.WriteEndElement()

    /// Creates a fileContent element and inserts it into the MzML file.
    member private this.WriteFileContent(item:FileContent) =
        writer.WriteStartElement("fileContent")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    /// Creates a run element and inserts it into the MzML file.
    member private this.WriteRun(item:Run, model:MzIOModel, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>, chromatogramListCount:int) =
        this.EnsureWriteState(MzMLWriteState.RUN)
        writer.WriteStartElement("run")
        writer.WriteAttributeString("defaultInstrumentConfigurationRef", item.DefaultInstrumentID)
        let sourceFileCount = model.FileDescription.SourceFiles.Count()
        if sourceFileCount > 0 then
            writer.WriteAttributeString("defaultSourceFileRef", ((Seq.head(model.FileDescription.SourceFiles.GetProperties false)).Value :?> SourceFile).ID)
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("sampleRef", item.SampleID)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        this.EnterWriteState(MzMLWriteState.RUN, MzMLWriteState.SPECTRUM_LIST)
        this.WriteSpectrumList(item, spectra, peaks)
        this.EnterWriteState(MzMLWriteState.SPECTRUM, MzMLWriteState.CHROMATOGRAM_LIST)
        if chromatogramListCount <> 0 then
            this.WriteChromatogramList(item, chromatogramListCount)
        else
            this.EnterWriteState(MzMLWriteState.CHROMATOGRAM_LIST, MzMLWriteState.CHROMATOGRAM)
        writer.WriteEndElement()

    /// Creates a dataProcessingList element and inserts it into the MzML file.
    member private this.WriteDataProcessingList(item:DataProcessingList) =
        writer.WriteStartElement("dataProcessingList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun dataProc -> this.WriteDataProcessing (dataProc.Value :?> DataProcessing))
        writer.WriteEndElement()

    /// Creates a instrumentConfigurationList element and inserts it into the MzML file.
    member private this.WriteInstrumentConfigurationList(item:InstrumentList) =
        writer.WriteStartElement("instrumentConfigurationList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun instConf -> this.WriteInstrumentConfiguration (instConf.Value :?> Instrument))
        writer.WriteEndElement()

    /// Creates a target element and inserts it into the MzML file.
    member private this.WriteTarget(item:MzIOModel) =
        writer.WriteStartElement("target")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    /// Creates a sourceFileRef element and inserts it into the MzML file.
    member private this.WriteSourceFileRef(item:string) =
        writer.WriteStartElement("sourceFileRef")
        writer.WriteAttributeString("ref", item)
        writer.WriteEndElement()

    /// Creates a targetList element and inserts it into the MzML file.
    member private this.WriteTargetList(item:SourceFileList) =
        writer.WriteStartElement("targetList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun sourceFile -> this.WriteSourceFileRef((sourceFile.Value :?> SourceFile).ID))
        writer.WriteEndElement()

    /// Creates a sourceFileRefList element and inserts it into the MzML file.
    member private this.WriteSourceFileRefList(item:SourceFileList) =
        writer.WriteStartElement("sourceFileRefList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun sourceFile -> this.WriteSourceFileRef((sourceFile.Value :?> SourceFile).ID))
        writer.WriteEndElement()

    /// Creates a scanSettings element and inserts it into the MzML file.
    member private this.WriteScanSettings(item:MzIOModel) =
        writer.WriteStartElement("scanSettings")
        writer.WriteAttributeString("id", "PlaceHolderID_settings")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param))
        if item.FileDescription.SourceFiles.Count() > 0 then
            this.WriteSourceFileRefList(item.FileDescription.SourceFiles)
        //this.WriteTargetList(item)
        writer.WriteEndElement()
        
    /// Creates a scanSettingsList element and inserts it into the MzML file.
    member private this.WriteScanSettingsList(item:MzIOModel) =
        writer.WriteStartElement("scanSettingsList")
        writer.WriteAttributeString("count", item.FileDescription.SourceFiles.Count().ToString())
        this.WriteScanSettings(item)
        writer.WriteEndElement()

    /// Creates a softwareList element and inserts it into the MzML file.
    member private this.WriteSoftwareList(item:SoftwareList) =
        this.EnsureWriteState(MzMLWriteState.MzIOModel)
        writer.WriteStartElement("softwareList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun software -> this.WriteSoftware(software.Value :?> Software))
        writer.WriteEndElement()

    /// Creates a sampleList element and inserts it into the MzML file.
    member private this.WriteSampleList(item:SampleList) =
        this.EnsureWriteState(MzMLWriteState.MzIOModel)
        writer.WriteStartElement("sampleList")
        writer.WriteAttributeString("count", item.Count().ToString())
        item.GetProperties false
        |> Seq.iter (fun sample -> this.WriteSample (sample.Value :?> Sample))
        writer.WriteEndElement()

    /// Creates a fileDescription element and inserts it into the MzML file.
    member private this.WriteFileDescription(item:FileDescription) =
        this.EnsureWriteState(MzMLWriteState.MzIOModel)
        writer.WriteStartElement("fileDescription")
        this.WriteFileContent(item.FileContent)
        if item.SourceFiles.Count() > 0 then
            this.WriteSourceFileList(item.SourceFiles)
        if item.Contact.Count() > 0 then
            this.WriteContact(item.Contact)
        writer.WriteEndElement()

    /// Creates a Run element and inserts it into the MzML file.
    /// Requires one writer.WriteEndElement() to finish the element.
    member private this.WriteRunList(item:RunList, model:MzIOModel, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>, chromatogramListCount:int) =
        this.EnsureWriteState(MzMLWriteState.RUN)
        item.GetProperties false
        |> Seq.iter (fun run -> this.WriteRun(run.Value :?> Run, model, spectra, peaks, chromatogramListCount))
        //writer.WriteEndElement()

    /// Write whole MzML file based on MzIOModel, spectra and peaks.
    member private this.WriteMzMl(item:MzIOModel, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>) = 
        this.EnsureWriteState(MzMLWriteState.INITIAL)
        this.EnterWriteState(MzMLWriteState.INITIAL, MzMLWriteState.MzIOModel)
        writer.WriteStartElement("mzML", "http://psi.hupo.org/ms/mzml")
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance")
        writer.WriteAttributeString("xsi", "schemaLocation", null, "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.0.xsd")
        writer.WriteAttributeString("id", item.Name)
        writer.WriteAttributeString("version", "1.1.0")
        this.WriteCvList()
        this.WriteFileDescription(item.FileDescription)
        if item.Samples.Count() <> 0 then
            this.WriteSampleList(item.Samples)
        this.WriteSoftwareList(item.Softwares)
        if item.FileDescription.SourceFiles.Count() > 0 then
            this.WriteScanSettingsList(item)
        this.WriteInstrumentConfigurationList(item.Instruments)
        this.WriteDataProcessingList(item.DataProcessings)
        this.EnterWriteState(MzMLWriteState.MzIOModel, MzMLWriteState.RUN)
        this.WriteRunList(item.Runs, item, spectra, peaks, 0)
        writer.WriteEndElement()
        currentWriteState <- MzMLWriteState.INITIAL
        this.Close()

    //member this.WriteWholedMzML(item:MzIOModel, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>) =
    //    this.EnsureWriteState(MzMLWriteState.INITIAL)
    //    this.EnterWriteState(MzMLWriteState.INITIAL, MzMLWriteState.MzIOModel)
    //    writer.WriteStartElement("indexedmzML", "http://psi.hupo.org/ms/mzml")
    //    writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance")
    //    writer.WriteAttributeString("xsi", "schemaLocation", null, "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.0.xsd")
    //    this.WriteMzMl(item, spectra, peaks)
    //    writer.WriteEndElement()
    //    currentWriteState <- MzMLWriteState.INITIAL
    //    this.Close()

    /// Write spectrum list in into MzML file based on MzIOModel.
    member private this.WriteSingleSpectrumList(count:string, spectrumProcessingRef:string) =
        this.EnsureWriteState(MzMLWriteState.SPECTRUM_LIST)
        writer.WriteStartElement("spectrumList") 
        writer.WriteAttributeString("count", count)
        writer.WriteAttributeString("defaultDataProcessingRef", spectrumProcessingRef)
        //this.WriteSpectrum(spectrum, peaks)
        //writer.WriteEndElement()
        this.EnterWriteState(MzMLWriteState.SPECTRUM_LIST, MzMLWriteState.SPECTRUM)

    /// Write run in into MzML file based on MzIOModel.
    /// Requires one writer.WriteEndElement() to finish the element.
    member private this.WriteSingleRun(runID:string, instrumentRef:string, potDefaultSourceFileRef:string option, sampleRef:string, count:string, spectrumProcessingRef:string) =
        this.EnsureWriteState(MzMLWriteState.RUN)
        writer.WriteStartElement("run")
        writer.WriteAttributeString("defaultInstrumentConfigurationRef", instrumentRef)
        match potDefaultSourceFileRef with
        | Some defaultSourceFileRef -> writer.WriteAttributeString("defaultSourceFileRef", defaultSourceFileRef)
        | None                      -> ()
        writer.WriteAttributeString("id", runID)
        writer.WriteAttributeString("sampleRef", sampleRef)
        this.EnterWriteState(MzMLWriteState.RUN, MzMLWriteState.SPECTRUM_LIST)
        this.WriteSingleSpectrumList(count, spectrumProcessingRef)
        //writer.WriteEndElement()  

    /// Write every element up to run into MzML file based on MzIOModel.
    member private this.writeMzIOModel(item:MzIOModel) =
        this.EnsureWriteState(MzMLWriteState.INITIAL)
        this.EnterWriteState(MzMLWriteState.INITIAL, MzMLWriteState.MzIOModel)
        writer.WriteStartElement("mzML")
        writer.WriteAttributeString("mzML", "xmlns", null, "http://psi.hupo.org/ms/mzml")
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance")
        writer.WriteAttributeString("xsi", "schemaLocation", null, "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.0.xsd")
        writer.WriteAttributeString("id", item.Name)
        writer.WriteAttributeString("version", "1.1.0")
        this.WriteCvList()
        this.WriteFileDescription(item.FileDescription)
        if item.Samples.Count() <> 0 then
            this.WriteSampleList(item.Samples)
        this.WriteSoftwareList(item.Softwares)
        if item.FileDescription.SourceFiles.Count() > 0 then
            this.WriteScanSettingsList(item)
        this.WriteInstrumentConfigurationList(item.Instruments)
        this.WriteDataProcessingList(item.DataProcessings)
        //writer.WriteEndElement()
        this.EnterWriteState(MzMLWriteState.MzIOModel, MzMLWriteState.RUN)

    //member private this.writeIndexedMzIOModel(item:MzIOModel) =
    //    this.EnsureWriteState(MzMLWriteState.INITIAL)
    //    this.EnterWriteState(MzMLWriteState.INITIAL, MzMLWriteState.MzIOModel)
    //    writer.WriteStartElement("indexedmzML")
    //    writer.WriteAttributeString("indexedmzML", "xmlns", null, "http://psi.hupo.org/ms/mzml")
    //    writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance")
    //    writer.WriteAttributeString("xsi", "schemaLocation", null, "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.0.xsd")
    //    this.writeMzIOModel(item)
        //writer.WriteEndElement()

    interface IMzIODataWriter with

         /// Write runID, spectrum and peaks into MzML file into the MzML file.
        member this.InsertMass(runID, spectrum: MassSpectrum, peaks: Peak1DArray) =
            this.EnsureWriteState(MzMLWriteState.SPECTRUM)
            this.WriteSpectrum(spectrum, 0, peaks) |> ignore

        /// Not implemented yet
        member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            this.EnsureWriteState(MzMLWriteState.CHROMATOGRAM)
            this.WriteChromatogram(chromatogram, peaks)

        /// Write runID, spectrum and peaks into MzML file asynchronously into the MzML file.
        member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            async {return (this.InsertMass(runID, spectrum, peaks))}

        /// Not implemented yet.
        member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            async {return (this.InsertChrom(runID, chromatogram, peaks))}

    /// Write runID, spectrum and peaks into MzML file.
    member this.InsertMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertMass(runID, spectrum, peaks)

    /// Not implemented yet.
    member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertChrom(runID, chromatogram, peaks)

    /// Write runID, spectrum and peaks into MzML file asynchronously.
    member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertAsyncMass(runID, spectrum, peaks)

    /// Not implemented yet.
    member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertAsyncChrom(runID, chromatogram, peaks)
        
    interface IDisposable with

        /// Closes connection to MzML file and prohibits further manipulation with this instance of the writer object.
        member this.Dispose() =
            this.Close()

    /// Closes connection to MzML file and prohibits further manipulation with this instance of the writer object.
    member this.Dispose() =

        (this :> IDisposable).Dispose()

    interface IMzIOIO with

        /// Ensures that writer is at start position and if not failes.
        member this.BeginTransaction() =
            this.EnsureWriteState(MzMLWriteState.INITIAL)
            new MzMLTransactionScope() :> ITransactionScope

        /// Creates default MzIOModel with name of path as name for new instance of MzIOModel.
        member this.CreateDefaultModel() =
            this.EnsureWriteState(MzMLWriteState.INITIAL)
            new MzIOModel(Path.GetFileNameWithoutExtension(path))

        /// Writes MzIOModel into MzML file.
        member this.SaveModel() =
            this.EnsureWriteState(MzMLWriteState.INITIAL)
            this.writeMzIOModel(this.Model)
            writer.WriteEndElement()
            writer.WriteEndElement()
            writer.WriteEndElement()

        /// Access in memory MzIOModel of MzMLWriter.
        member this.Model = model

    /// Ensures that writer is at start position and if not failes.
    member this.BeginTransaction() =
        
        (this :> IMzIOIO).BeginTransaction()

    /// Creates default MzIOModel with name of path as name for new instance of MzIOModel.
    member this.CreateDefaultModel() =
        (this :> IMzIOIO).CreateDefaultModel()        

    /// Writes MzIOModel into MzML file.
    member this.SaveModel() =
        (this :> IMzIOIO).SaveModel()

    /// Access in memory MzIOModel of MzMLWriter.
    member this.Model = 
        (this :> IMzIOIO).Model

    /// Updates current in memory MzIOModel of the MzIOWriter by adding values of new MzIOModel.
    member this.UpdateModel(model':MzIOModel) = 
        model <- model'

    /// Inserts runID, MassSpectra with corresponding Peak1DArrasy into datbase Spectrum table with chosen compression type for the peak data.
    member this.insertMSSpectrum (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectrum: MassSpectrum)= 
        let peakArray = reader.ReadSpectrumPeaks(spectrum.ID)
        match compress with 
        | BinaryDataCompressionType.NoCompression  -> 
            let clonedP = new Peak1DArray(BinaryDataCompressionType.NoCompression, peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            this.InsertMass(runID, spectrum, clonedP)
        | BinaryDataCompressionType.ZLib -> 
            let clonedP = new Peak1DArray(BinaryDataCompressionType.ZLib, peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            this.InsertMass(runID, spectrum, clonedP)
        | BinaryDataCompressionType.NumPress ->
            let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPress, peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            this.InsertMass(runID, spectrum, clonedP)
        | BinaryDataCompressionType.NumPressZLib ->
            let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPressZLib, peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            this.InsertMass(runID, spectrum, clonedP)
        | _ ->
            failwith "Not a valid compression Method"

    /// Modifies spectrum according to the used spectrumPeaksModifierF and inserts the result into the MzML file.
    member this.insertModifiedSpectrumBy (spectrumPeaksModifierF: IMzIODataReader -> MassSpectrum -> BinaryDataCompressionType -> Peak1DArray) (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectrum: MassSpectrum) = 
        let modifiedP = spectrumPeaksModifierF reader spectrum compress
        this.InsertMass(runID, spectrum, modifiedP)

    /// Starts bulkinsert of mass spectra into a MzLiteSQL database
    member this.insertMSSpectraBy insertSpectrumF (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectra: seq<MassSpectrum>) = 
        this.writeMzIOModel(reader.Model)
        let potDefaultSourceFileRef = if reader.Model.FileDescription.SourceFiles.Count() > 0 then Some ((Seq.head(reader.Model.FileDescription.SourceFiles.GetProperties false)).Value :?> SourceFile).ID else None
        let instrumentRef           = ((Seq.head(reader.Model.Runs.GetProperties false)).Value :?> Run).DefaultInstrumentID
        let sampleRef               = ((Seq.head(reader.Model.Runs.GetProperties false)).Value :?> Run).SampleID
        let spectrumProcessingRef   = ((Seq.head(reader.Model.Runs.GetProperties false)).Value :?> Run).DefaultSpectrumProcessing.ID
        let count                   = spectra.Count().ToString()
        this.WriteSingleRun(runID, instrumentRef, potDefaultSourceFileRef, sampleRef, count, spectrumProcessingRef)
        
        let realRun = 
            reader.Model.Runs.GetProperties false
            |> Seq.head
            |> fun item -> item.Value :?> Run

        let bulkInsert spectra = 
            spectra
            |> Seq.iter (insertSpectrumF realRun.ID reader compress)
        bulkInsert spectra
        writer.WriteEndElement()
        writer.WriteEndElement()
        currentWriteState <- MzMLWriteState.INITIAL
        this.Commit()
        
