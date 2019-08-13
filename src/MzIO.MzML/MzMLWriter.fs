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
open MzIO.Binary
open MzIO.IO
open MzIO.Model.CvParam


/// Enum that enables to control and check the position of the writer.
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

/// Class which contains methods to encode and compress collections of doubles.
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
    static member private DeflateStreamCompress (data: byte[]) =
        use mStream = new MemoryStream(data)
        (
         use outStream = new MemoryStream()
         use compress = new DeflateStream (outStream, CompressionMode.Compress, true)      
         mStream.CopyTo(compress)
         compress.Close() 
         let byteArray = outStream.ToArray()
         byteArray
        )

    /// Convert array of doubles to array of bytes.
    static member private FloatToByteArray (floatArray: float[]) =
        let byteArray = Array.init (floatArray.Length*8) (fun x -> byte(0))
        Buffer.BlockCopy (floatArray, 0, byteArray, 0, byteArray.Length)
        byteArray

    /// Compress bytes using zlib compression method.
    static member private ZLib(memoryStream:Stream, floats:float[]) =
        let bytes       = MzMLCompression.FloatToByteArray(floats |> Array.ofSeq)
        let byteDeflate = MzMLCompression.DeflateStreamCompress bytes

        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(byteDeflate.Length)
        writer.Write(byteDeflate)

    /// Convert array of floats to array of bytes which are compressed, based on the chosen method.
    member this.Encode(compressionType:BinaryDataCompressionType, dataType:BinaryDataType, floats:float[]) =
       
        this.memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore

        match compressionType with
        | BinaryDataCompressionType.NoCompression   -> MzMLCompression.NoCompression(this.memoryStream, dataType, floats)
        | BinaryDataCompressionType.ZLib            -> MzMLCompression.ZLib(this.memoryStream, floats)
        //| BinaryDataCompressionType.NumPress        -> MzMLCompression.Numpress(this.memoryStream, peakArray)
        //| BinaryDataCompressionType.NumPressZLib    -> MzMLCompression.NumpressDeflate(this.memoryStream, peakArray)
        | _ -> failwith (sprintf "Compression type not supported: %s" (compressionType.ToString()))
        
        this.memoryStream.ToArray()

[<Sealed>]
type private MzMLTransactionScope() =

    interface IDisposable with

        member this.Dispose() =
            ()

    member this.Dispose() =

        (this :> IDisposable).Dispose()

    interface ITransactionScope with

        member this.Commit() =
            ()

        member this.Rollback() =
            ()

    member this.Commit() =

        (this :> ITransactionScope).Commit()

    member this.Rollback() =

        (this :> ITransactionScope).Rollback()

/// Contains methods to create a mzml file based on mzio model.
[<Sealed>]
type MzMLWriter(path:string) =

    let writer =
        let mutable writerSettings = new XmlWriterSettings()
        writerSettings.Indent <- true
        XmlWriter.Create(path, writerSettings)

    let mutable isClosed = false

    let mutable currentWriteState = MzMLWriteState.INITIAL

    let mutable consumedWriteStates = new HashSet<MzMLWriteState>()

    let mutable model = new MzIOModel(Path.GetFileNameWithoutExtension(path))

    member this.Close() =

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

    member private this.WriteCvParam(param:CvParam<#IConvertible>) =
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", param.CvAccession)        
        writer.WriteAttributeString("name", "not saved yet")
        writer.WriteAttributeString("value", if (tryGetValue (param :> IParamBase<#IConvertible>)).IsSome then (tryGetValue (param :> IParamBase<#IConvertible>)).Value.ToString() else "")
        if (tryGetCvUnitAccession (param :> IParamBase<#IConvertible>)).IsSome then
            writer.WriteAttributeString("unitCvRef", "UO")
            writer.WriteAttributeString("unitAccession", (tryGetCvUnitAccession (param :> IParamBase<#IConvertible>)).Value.ToString())
            writer.WriteAttributeString("unitName", "not saved yet")        
        writer.WriteEndElement()

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

    member private this.WriteDetector(item:DetectorComponent) =
        writer.WriteStartElement("detector")
        writer.WriteAttributeString("order", "3")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    member private this.WriteAnalyzer(item:AnalyzerComponent) =
        writer.WriteStartElement("analyzer")
        writer.WriteAttributeString("order", "2")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    member private this.WriteSource(item:SourceComponent) =
        writer.WriteStartElement("source")
        writer.WriteAttributeString("order", "1")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    member private this.WriteProcessingMethod(item:DataProcessingStep) =
        writer.WriteStartElement("processingMethod")
        writer.WriteAttributeString("order", item.Name)
        writer.WriteAttributeString("softwareRef", item.Software.ID)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

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

    member private this.WriteComponentList(item:ComponentList) =
        writer.WriteStartElement("componentList")
        writer.WriteAttributeString("count", (item.GetProperties false |> Seq.length).ToString())
        item.GetProperties false
        |> Seq.iter (fun component' -> this.assignComponent(component'.Value))
        writer.WriteEndElement()

    member private this.WriteSourceFile(item:SourceFile) =
        writer.WriteStartElement("sourceFile")
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("location", item.Location)
        writer.WriteAttributeString("name", item.Name)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    member private this.WriteSelectedIon(item:SelectedIon) =
        writer.WriteStartElement("selectedIon")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member private this.WriteScanWindow(item:ScanWindow) =
        writer.WriteStartElement("scanWindow")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member private this.WriteBinary(item:string) =
        writer.WriteElementString("binary", item)

    member private this.WriteActivation(item:Activation) =
        writer.WriteStartElement("activation")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member private this.WriteSelectedIonList(item:SelectedIonList) =
        writer.WriteStartElement("selectedIonList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun selectedIon -> this.WriteSelectedIon(selectedIon.Value :?> SelectedIon))
        writer.WriteEndElement()

    member private this.WriteIsolationWindow(item:IsolationWindow) =
        writer.WriteStartElement("isolationWindow")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member private this.WriteScanWindowList(item:ScanWindowList) =
        writer.WriteStartElement("scanWindowList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
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

    member private this.WriteCvParamCompression(accession:string, name:string) =
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("accession", accession)
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("name", name)
        writer.WriteAttributeString("value", "")
        writer.WriteEndElement()

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
        | BinaryDataCompressionType.NumPress        -> this.WriteUserParamCompression("NumPress")
        | BinaryDataCompressionType.NumPressZLib    -> this.WriteUserParamCompression("NumPressZLib")
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
        this.writeCompressionParam item.CompressionType
        //writer.WriteStartElement("cvParam")
        //writer.WriteAttributeString("accession", (MzMLWriter.assignCompressionType item.CompressionType).ToString())
        //writer.WriteAttributeString("cvRef", "MS")
        //writer.WriteAttributeString("name", item.CompressionType.ToString())
        //writer.WriteAttributeString("value", "")
        //writer.WriteEndElement()
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
        this.writeCompressionParam item.CompressionType
        //writer.WriteStartElement("cvParam")
        //writer.WriteAttributeString("accession", (MzMLWriter.assignCompressionType item.CompressionType).ToString())
        //writer.WriteAttributeString("cvRef", "MS")
        //writer.WriteAttributeString("name", "not saved yet")
        //writer.WriteAttributeString("value", "")
        //writer.WriteEndElement()
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", "MS:1000515")      
        writer.WriteAttributeString("name", "intensity array")
        writer.WriteAttributeString("value", "")
        writer.WriteAttributeString("unitCvRef", "UO")
        writer.WriteAttributeString("unitAccession", "MS:1000131")
        writer.WriteAttributeString("unitName", "number of counts")
        writer.WriteEndElement()

    member private this.WriteBinaryDataArray(spectrum:MassSpectrum, peaks:Peak1DArray) =
        let encoder = new MzMLCompression()
        let qs = 
            peaks.Peaks
            |> Seq.map (fun peak -> peak.Mz)
            |> Array.ofSeq
        let encodedQs = Convert.ToBase64String(encoder.Encode(peaks.CompressionType, peaks.MzDataType, qs))
        writer.WriteStartElement("binaryDataArray")
        writer.WriteAttributeString("arrayLength", qs.Length.ToString())
        if not (String.IsNullOrWhiteSpace spectrum.DataProcessingReference) then 
            writer.WriteAttributeString("dataProcessingRef", spectrum.DataProcessingReference)
        writer.WriteAttributeString("encodedLength", encodedQs.Length.ToString())        
        this.WriteQParams(peaks)
        this.WriteBinary(encodedQs)
        writer.WriteFullEndElement()

        let intensities = 
            peaks.Peaks
            |> Seq.map (fun peak -> peak.Intensity)
            |> Array.ofSeq
        let encodedIntensities = Convert.ToBase64String(encoder.Encode(peaks.CompressionType, peaks.IntensityDataType, intensities))
        writer.WriteStartElement("binaryDataArray")
        writer.WriteAttributeString("arrayLength", intensities.Length.ToString())
        if not (String.IsNullOrWhiteSpace spectrum.DataProcessingReference) then 
            writer.WriteAttributeString("dataProcessingRef", spectrum.DataProcessingReference)
        writer.WriteAttributeString("encodedLength", encodedIntensities.Length.ToString())
        this.WriteIntensityParams(peaks)
        this.WriteBinary(encodedIntensities)
        writer.WriteFullEndElement()  

    member private this.WriteProduct(item:Product) =
        writer.WriteStartElement("product")
        this.WriteIsolationWindow(item.IsolationWindow)
        writer.WriteEndElement()  

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
        if Seq.length (item.ScanWindows.GetProperties false) >0 then
            this.WriteScanWindowList(item.ScanWindows)
        writer.WriteEndElement()

    member private this.WriteBinaryDataArrayList(spectrum:MassSpectrum, peaks:Peak1DArray) =
        writer.WriteStartElement("binaryDataArrayList")
        writer.WriteAttributeString("count", "2")
        this.WriteBinaryDataArray(spectrum, peaks)
        writer.WriteEndElement()

    member private this.WriteProductList(item:ProductList) =
        writer.WriteStartElement("productList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun product -> this.WriteProduct(product.Value :?> Product))
        writer.WriteEndElement()

    member private this.WritePrecursorList(item:PrecursorList) =
        writer.WriteStartElement("precursorList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun precursor -> this.WritePrecursor(precursor.Value :?> Precursor))
        writer.WriteEndElement()

    member private this.WriteScanList<'T when 'T :> IConvertible>(item:ScanList) =
        let scans = (item.GetProperties false) |> Seq.filter (fun value -> value.Value :? Scan)
        writer.WriteStartElement("scanList")
        writer.WriteAttributeString("count", (Seq.length scans).ToString())
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

    member private this.WriteSpectrum(spectrum:MassSpectrum, index:int, peaks:Peak1DArray) =
        this.EnsureWriteState(MzMLWriteState.SPECTRUM)
        writer.WriteStartElement("spectrum")
        if not (String.IsNullOrWhiteSpace spectrum.DataProcessingReference) then 
            writer.WriteAttributeString("dataProcessingRef", spectrum.DataProcessingReference)
        writer.WriteAttributeString("defaultArrayLength", (Seq.length peaks.Peaks).ToString())
        writer.WriteAttributeString("id", spectrum.ID)
        writer.WriteAttributeString("index", sprintf "%i" index)
        if not (String.IsNullOrWhiteSpace spectrum.SourceFileReference) then 
            writer.WriteAttributeString("sourceFileRef", spectrum.SourceFileReference)            
        //writer.WriteAttributeString("spotID", "not saved yet")
        spectrum.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        if Seq.length (spectrum.Scans.GetProperties false) <> 0 then
            this.WriteScanList(spectrum.Scans)
        if Seq.length (spectrum.Precursors.GetProperties false) <> 0 then
            this.WritePrecursorList(spectrum.Precursors)
        if Seq.length (spectrum.Products.GetProperties false) <> 0 then
            this.WriteProductList(spectrum.Products)
        if Seq.length (peaks.GetProperties false) <> 0 then
            this.WriteBinaryDataArrayList(spectrum, peaks)
        writer.WriteEndElement()
        index

    member private this.WriteChromatogramList(item:Run, chromatogramListCount:int) =
        this.EnsureWriteState(MzMLWriteState.CHROMATOGRAM_LIST)
        writer.WriteStartElement("chromatogramList") 
        writer.WriteAttributeString("count", chromatogramListCount.ToString())
        writer.WriteAttributeString("defaultDataProcessingRef", item.DefaultChromatogramProcessing.ID)
        this.EnterWriteState(MzMLWriteState.CHROMATOGRAM_LIST, MzMLWriteState.CHROMATOGRAM)
        //this.WriteChromatogram()
        writer.WriteEndElement()

    member private this.WriteSpectrumList(item:Run, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>) =
        this.EnsureWriteState(MzMLWriteState.SPECTRUM_LIST)
        writer.WriteStartElement("spectrumList") 
        writer.WriteAttributeString("count", (Seq.length spectra).ToString())
        writer.WriteAttributeString("defaultDataProcessingRef", item.DefaultSpectrumProcessing.ID)
        this.EnterWriteState(MzMLWriteState.SPECTRUM_LIST, MzMLWriteState.SPECTRUM)
        Seq.fold2 (fun start spectrum peak -> this.WriteSpectrum(spectrum, start + 1, peak)) 0 spectra peaks |> ignore
        writer.WriteEndElement()

    member private this.WriteDataProcessing(item:DataProcessing) =
        writer.WriteStartElement("dataProcessing") 
        writer.WriteAttributeString("id", item.ID)
        item.ProcessingSteps.GetProperties false
        |> Seq.iter (fun dataStep -> this.WriteProcessingMethod (dataStep.Value :?> DataProcessingStep))
        writer.WriteEndElement()

    member private this.WriteInstrumentConfiguration(item:Instrument) =
        writer.WriteStartElement("instrumentConfiguration")
        writer.WriteAttributeString("id", item.ID)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        this.WriteComponentList item.Components
        this.WriteSoftwareRef item.Software
        writer.WriteEndElement()

    member private this.WriteSoftware(item:Software) =
        writer.WriteStartElement("software")
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("version", "not saved yet")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member private this.WriteSample(item:Sample) =
        writer.WriteStartElement("sample")
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("name", item.Name)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member private this.WriteContact(item:Contact) =
        writer.WriteStartElement("contact")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member private this.WriteSourceFileList(item:SourceFileList) =
        writer.WriteStartElement("sourceFileList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun source -> this.WriteSourceFile (source.Value :?> SourceFile))
        writer.WriteEndElement()

    member private this.WriteFileContent(item:FileContent) =
        writer.WriteStartElement("fileContent")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member private this.WriteRun(item:Run, model:MzIOModel, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>, chromatogramListCount:int) =
        this.EnsureWriteState(MzMLWriteState.RUN)
        writer.WriteStartElement("run")
        writer.WriteAttributeString("defaultInstrumentConfigurationRef", item.DefaultInstrument.ID)
        writer.WriteAttributeString("defaultSourceFileRef", ((Seq.head(model.FileDescription.SourceFiles.GetProperties false)).Value :?> SourceFile).ID)
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("sampleRef", item.Sample.ID)
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

    member private this.WriteDataProcessingList(item:DataProcessingList) =
        writer.WriteStartElement("dataProcessingList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun dataProc -> this.WriteDataProcessing (dataProc.Value :?> DataProcessing))
        writer.WriteEndElement()

    member private this.WriteInstrumentConfigurationList(item:InstrumentList) =
        writer.WriteStartElement("instrumentConfigurationList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun instConf -> this.WriteInstrumentConfiguration (instConf.Value :?> Instrument))
        writer.WriteEndElement()

    member private this.WriteTarget(item:MzIOModel) =
        writer.WriteStartElement("target")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member private this.WriteSourceFileRef(item:string) =
        writer.WriteStartElement("sourceFileRef")
        writer.WriteAttributeString("ref", item)
        writer.WriteEndElement()

    member private this.WriteTargetList(item:SourceFileList) =
        writer.WriteStartElement("targetList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun sourceFile -> this.WriteSourceFileRef((sourceFile.Value :?> SourceFile).ID))
        writer.WriteEndElement()

    member private this.WriteSourceFileRefList(item:SourceFileList) =
        writer.WriteStartElement("sourceFileRefList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun sourceFile -> this.WriteSourceFileRef((sourceFile.Value :?> SourceFile).ID))
        writer.WriteEndElement()

    member private this.WriteScanSettings(item:MzIOModel) =
        writer.WriteStartElement("scanSettings")
        writer.WriteAttributeString("id", "PlaceHolderID_settings")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param))
        if Seq.length (item.FileDescription.SourceFiles.GetProperties false) > 0 then
            this.WriteSourceFileRefList(item.FileDescription.SourceFiles)
        //this.WriteTargetList(item)
        writer.WriteEndElement()
        
    member private this.WriteScanSettingsList(item:MzIOModel) =
        writer.WriteStartElement("scanSettingsList")
        writer.WriteAttributeString("count", (Seq.length (item.FileDescription.SourceFiles.GetProperties false)).ToString())
        this.WriteScanSettings(item)
        writer.WriteEndElement()

    member private this.WriteSoftwareList(item:SoftwareList) =
        this.EnsureWriteState(MzMLWriteState.MzIOModel)
        writer.WriteStartElement("softwareList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun software -> this.WriteSoftware(software.Value :?> Software))
        writer.WriteEndElement()

    member private this.WriteSampleList(item:SampleList) =
        this.EnsureWriteState(MzMLWriteState.MzIOModel)
        writer.WriteStartElement("sampleList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun sample -> this.WriteSample (sample.Value :?> Sample))
        writer.WriteEndElement()

    member private this.WriteFileDescription(item:FileDescription) =
        this.EnsureWriteState(MzMLWriteState.MzIOModel)
        writer.WriteStartElement("fileDescription")
        this.WriteFileContent(item.FileContent)
        if Seq.length (item.SourceFiles.GetProperties false) > 0 then
            this.WriteSourceFileList(item.SourceFiles)
        if Seq.length (item.Contact.GetProperties false) >0 then
            this.WriteContact(item.Contact)
        writer.WriteEndElement()

    member private this.WriteRunList(item:RunList, model:MzIOModel, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>, chromatogramListCount:int) =
        this.EnsureWriteState(MzMLWriteState.RUN)
        item.GetProperties false
        |> Seq.iter (fun run -> this.WriteRun(run.Value :?> Run, model, spectra, peaks, chromatogramListCount))
        //writer.WriteEndElement()    

    /// Write whole mzml file based on MzIOModel, spectra and peaks.
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
        if Seq.length (item.Samples.GetProperties false) <> 0 then
            this.WriteSampleList(item.Samples)
        this.WriteSoftwareList(item.Softwares)
        if Seq.length (item.FileDescription.SourceFiles.GetProperties false) > 0 then
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

    /// Write spectrum list in into mzml file based on MzIOModel.
    member private this.WriteSingleSpectrumList(count:string, spectrumProcessingRef:string) =
        this.EnsureWriteState(MzMLWriteState.SPECTRUM_LIST)
        writer.WriteStartElement("spectrumList") 
        writer.WriteAttributeString("count", count)
        writer.WriteAttributeString("defaultDataProcessingRef", spectrumProcessingRef)
        //this.WriteSpectrum(spectrum, peaks)
        //writer.WriteEndElement()
        this.EnterWriteState(MzMLWriteState.SPECTRUM_LIST, MzMLWriteState.SPECTRUM)

    /// Write run in into mzml file based on MzIOModel.
    member private this.WriteSingleRun(runID:string, instrumentRef:string, defaultSourceFileRef:string, sampleRef:string, count:string, spectrumProcessingRef:string) =
        this.EnsureWriteState(MzMLWriteState.RUN)
        writer.WriteStartElement("run")
        writer.WriteAttributeString("defaultInstrumentConfigurationRef", instrumentRef)
        writer.WriteAttributeString("defaultSourceFileRef", defaultSourceFileRef)
        writer.WriteAttributeString("id", runID)
        writer.WriteAttributeString("sampleRef", sampleRef)
        this.EnterWriteState(MzMLWriteState.RUN, MzMLWriteState.SPECTRUM_LIST)
        this.WriteSingleSpectrumList(count, spectrumProcessingRef)
        //writer.WriteEndElement()  

    /// Write every element up to run into mzml file based on MzIOModel.
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
        if Seq.length (item.Samples.GetProperties false) <> 0 then
            this.WriteSampleList(item.Samples)
        this.WriteSoftwareList(item.Softwares)
        if Seq.length (item.FileDescription.SourceFiles.GetProperties false) > 0 then
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

        member this.InsertMass(runID, spectrum: MassSpectrum, peaks: Peak1DArray) =
            this.EnsureWriteState(MzMLWriteState.SPECTRUM)
            this.WriteSpectrum(spectrum, 0, peaks) |> ignore

        member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            this.EnsureWriteState(MzMLWriteState.CHROMATOGRAM)
            this.WriteChromatogram(chromatogram, peaks)

        member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            async {return (this.InsertMass(runID, spectrum, peaks))}

        member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            async {return (this.InsertChrom(runID, chromatogram, peaks))}

    /// Write spectrum into mzml file.
    member this.InsertMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertMass(runID, spectrum, peaks)

    /// Not implemented yet.
    member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertChrom(runID, chromatogram, peaks)

    /// Write spectrum into mzml file asynchronously.
    member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertAsyncMass(runID, spectrum, peaks)

    /// Not implemented yet.
    member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertAsyncChrom(runID, chromatogram, peaks)
        
    interface IDisposable with

        member this.Dispose() =
            this.Close()

    member this.Dispose() =

        (this :> IDisposable).Dispose()

    interface IMzIOIO with

        member this.BeginTransaction() =
            this.EnsureWriteState(MzMLWriteState.INITIAL)
            new MzMLTransactionScope() :> ITransactionScope

        member this.CreateDefaultModel() =
            this.EnsureWriteState(MzMLWriteState.INITIAL)
            new MzIOModel(Path.GetFileNameWithoutExtension(path))

        member this.SaveModel() =
            this.EnsureWriteState(MzMLWriteState.INITIAL)
            this.writeMzIOModel(this.Model)
            writer.WriteEndElement()
            writer.WriteEndElement()
            writer.WriteEndElement()

        member this.Model =
            //this.EnsureWriteState(MzMLWriteState.INITIAL)
            model

    member this.BeginTransaction() =
        
        (this :> IMzIOIO).BeginTransaction()

    member this.CreateDefaultModel() =
        (this :> IMzIOIO).CreateDefaultModel()        

    member this.SaveModel() =
        (this :> IMzIOIO).SaveModel()

    member this.Model = 
        (this :> IMzIOIO).Model

    member this.UpdateModel(model':MzIOModel) = 
        model <- model'

    /// copies MassSpectrum into DB schema
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

    /// modifies spectrum according to the used spectrumPeaksModifierF and inserts the result into the DB schema 
    member this.insertModifiedSpectrumBy (spectrumPeaksModifierF: IMzIODataReader -> MassSpectrum -> BinaryDataCompressionType -> Peak1DArray) (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectrum: MassSpectrum) = 
        let modifiedP = spectrumPeaksModifierF reader spectrum compress
        this.InsertMass(runID, spectrum, modifiedP)

    /// Starts bulkinsert of mass spectra into a MzLiteSQL database
    member this.insertMSSpectraBy insertSpectrumF (model:MzIOModel)(runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectra: seq<MassSpectrum>) = 
        this.writeMzIOModel(model)
        let sourceFileID = ((Seq.head(model.FileDescription.SourceFiles.GetProperties false)).Value :?> SourceFile).ID
        let instrumentRef = ((Seq.head(model.Runs.GetProperties false)).Value :?> Run).DefaultInstrument.ID
        let sampleRef = ((Seq.head(model.Runs.GetProperties false)).Value :?> Run).Sample.ID
        let spectrumProcessingRef = ((Seq.head(model.Runs.GetProperties false)).Value :?> Run).DefaultSpectrumProcessing.ID
        let count = spectra.Count().ToString()
        this.WriteSingleRun(runID, instrumentRef, sourceFileID, sampleRef, count, spectrumProcessingRef)
        let bulkInsert spectra = 
            spectra
            |> Seq.iter (insertSpectrumF runID reader compress)
        bulkInsert spectra
        writer.WriteEndElement()
        writer.WriteEndElement()
        currentWriteState <- MzMLWriteState.INITIAL
        this.Commit()
        
