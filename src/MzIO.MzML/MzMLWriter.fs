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

// TODO cv lookup
// TODO param name lookup
// TODO model only one run
// TODO simplify write states, only speclist, chromlist
// TODO write chromatogram list
// TODO get disposable on all beginxxx methods
[<Sealed>]
type MzMLWriter(path:string) =

    let mutable formatProvider = new CultureInfo("en-US")
    let mutable isClosed = 
        false

    let mutable currentWriteState   = MzMLWriteState.INITIAL
    let mutable consumedWriteStates = new HashSet<MzMLWriteState>()

    let writer =
        match path.Trim() with
        | ""    -> raise (ArgumentNullException("path"))
        |   _   ->
            try
                let mutable writerSetting = new XmlWriterSettings()
                writerSetting.Indent <- true
                let xmlWriter = XmlWriter.Create(path, writerSetting)
                xmlWriter.WriteStartDocument()
                xmlWriter
            with
                | :? Exception as ex ->
                    currentWriteState <- MzMLWriteState.ERROR
                    raise (MzIOIOException("Error init mzml output file.", ex))

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

    interface IDisposable with

        member this.Dispose() =

            this.Close()

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////#region write states/////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    member private this.EnsureWriteState(expectedWs:MzMLWriteState) =

        if currentWriteState = MzMLWriteState.ERROR then
            raise (new MzIOIOException("Current write state is ERROR."))
        if currentWriteState = MzMLWriteState.CLOSED then
            raise (new MzIOIOException("Current write state is CLOSED."))
        if currentWriteState <> expectedWs then
            raise (MzIOIOException(String.Format("Invalid write state: expected '{0}' but current is '{1}'.", expectedWs, currentWriteState)))

    member private this.EnterWriteState(expectedWs:MzMLWriteState, newWs:MzMLWriteState) =

        if consumedWriteStates.Contains(newWs) then
            raise (MzIOIOException(String.Format("Can't reentering write state: '{0}'.", newWs)))
        this.EnsureWriteState(expectedWs)
        currentWriteState <- newWs
        consumedWriteStates.Add(newWs) |> ignore

    member private this.LeaveWriteState(expectedWs: MzMLWriteState, newWs: MzMLWriteState) =

        this.EnsureWriteState(expectedWs);
        currentWriteState <- newWs

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////#region xml writing helper/////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    member private this.WriteXmlAttribute(name:string, value:string, ?required: bool) =

        let required = defaultArg required false

        if String.IsNullOrWhiteSpace(value) then
            if required then
                raise (MzIOIOException("Value required for xml attribute: " + name))
            else
                ()
        writer.WriteAttributeString(name, value)

    //member private this.WriteList<'TItem>(elementName: string, list: DynamicObj, writeItem: Func<'TItem>, ?skipEmtpy: bool) =
        
    //    let skipEmpty = defaultArg skipEmtpy true
    //    let count = list.Count

    //    if skipEmpty && count = 0 then ()

    //    writer.WriteStartElement(elementName)

    //    this.WriteXmlAttribute("count", count.ToString(formatProvider))

    //    for item in (list.GetProperties false) do
    //        writeItem.Invoke(item.Value :?> 'TItem)
        
    //    writer.WriteEndElement()

    member private this.WriteList<'TItem>(elementName:string, list:DynamicObj, writeItem:'TItem -> unit, ?skipEmpty:bool) =

        let skipEmpty = defaultArg skipEmpty true
        let count = list.GetProperties false |> Seq.length
        if skipEmpty= true && count = 0 then ()
        else
            writer.WriteStartElement(elementName)
            this.WriteXmlAttribute("count", count.ToString(formatProvider))
            list.GetProperties false
            |> Seq.iter (fun item -> writeItem(item.Value :?> 'TItem))

            writer.WriteEndElement()

    member private this.WriteList2<'TItem>(elementName:string, list:DynamicObj, writeItem:('TItem * int) -> unit, ?skipEmpty:bool) =

        let skipEmpty = defaultArg skipEmpty true
        let count = list.GetProperties false |> Seq.length
        if skipEmpty= true && count = 0 then ()
        else
            writer.WriteStartElement(elementName)
            this.WriteXmlAttribute("count", count.ToString(formatProvider))
            list.GetProperties false
            |> Seq.fold (fun (idx:int) item ->
                writeItem(item.Value :?> 'TItem, idx)
                idx + 1) 0
            |> ignore

            writer.WriteEndElement()

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////#region param writing////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    member private this.WriteCvList() =
        writer.WriteStartElement("cvList")
        this.WriteXmlAttribute("count", "2")
        this.WriteCv("MS", "Proteomics Standards Initiative Mass Spectrometry Ontology", "3.79.0", "http://psidev.info/ms/mzML/psi-ms.obo")
        this.WriteCv("UO", "Unit Ontology", "1.15", "http://obo.cvs.sourceforge.net/obo/obo/ontology/phenotype/unit.obo")
        writer.WriteEndElement()

    member private this.WriteCv(id:string, fullName:string, version:string, uri:string) =
        writer.WriteStartElement("cv")
        this.WriteXmlAttribute("id", id)
        this.WriteXmlAttribute("fullName", fullName)
        this.WriteXmlAttribute("version", version)
        this.WriteXmlAttribute("URI", uri)
        writer.WriteEndElement()

    member private this.IsMSParam(p:IParamBase<'T>) =
        match p.ID with
            | null  -> false
            | ""    -> false
            | " "   -> false
            |   _   -> p.ID.StartsWith("MS:", StringComparison.InvariantCultureIgnoreCase)

    member private this.HasValidOrEmptyUnit(p:IParamBase<'T>) =
        if p.Value.IsNone then true
        else
            let tmp = tryGetCvUnitAccession p
            if tmp.IsSome then
                tmp.Value.StartsWith("MS:", StringComparison.InvariantCultureIgnoreCase) ||
                tmp.Value.StartsWith("UO:", StringComparison.InvariantCultureIgnoreCase)
            else true

    member private this.ParseCvRef(accession:string) =

        match accession with
        | null  -> null
        | ""    -> null
        | " "   -> null
        |   _   ->
            let split = accession.Split(':')
            if split.Length > 0 then split.First().ToUpperInvariant()
            else null

    member private this.WriteParamGroup(pc:DynamicObj) =

        pc.GetProperties false
        |> Seq.filter (fun item ->
            let tmp = item.Value :?> IParamBase<string>
            this.IsMSParam(tmp) && this.HasValidOrEmptyUnit(tmp)
            )
        |> Seq.iter (fun item ->
                let cvp = item.Value :?> IParamBase<string>

                writer.WriteStartElement("cvParam")

                this.WriteXmlAttribute("cvRef", "MS", true)
                this.WriteXmlAttribute("accession", cvp.ID, true)
                this.WriteXmlAttribute("name", cvp.ID, true)
                this.WriteXmlAttribute("value", cvp.GetStringOrDefault())

                if cvp.HasUnit() then
                    this.WriteXmlAttribute("unitCvRef",this.ParseCvRef((tryGetCvUnitAccession cvp).Value), true)
                    this.WriteXmlAttribute("unitAccession", (tryGetCvUnitAccession cvp).Value, true)
                    this.WriteXmlAttribute("unitName", (tryGetCvUnitAccession cvp).Value, true)
                )
        writer.WriteEndElement()

        pc.GetProperties false
        |> Seq.filter (fun item ->
            let tmp = item.Value :?> IParamBase<string>
            this.IsMSParam(tmp) = false && this.HasValidOrEmptyUnit(tmp)
            )
        |> Seq.iter (fun item ->
            let up = item.Value :?> IParamBase<string>

            writer.WriteStartElement("userParam")
            this.WriteXmlAttribute("name", up.ID, true);
            this.WriteXmlAttribute("value", up.GetStringOrDefault());

            if up.HasUnit() then
                this.WriteXmlAttribute("unitCvRef",this.ParseCvRef((tryGetCvUnitAccession up).Value), true)
                this.WriteXmlAttribute("unitAccession", (tryGetCvUnitAccession up).Value, true)
                this.WriteXmlAttribute("unitName", (tryGetCvUnitAccession up).Value, true)
            )
        writer.WriteEndElement()

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////#region model writing////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    member private this.WriteSourceFile(sf: SourceFile) =

        writer.WriteStartElement("sourceFile")
        this.WriteXmlAttribute("id", sf.ID, true)
        this.WriteXmlAttribute("name", sf.Name, true)
        this.WriteXmlAttribute("location", sf.Location, true)

        this.WriteParamGroup(sf)

        writer.WriteEndElement()

    member private this.WriteFileDescription(fdesc:FileDescription) =

        writer.WriteStartElement("fileDescription");

        writer.WriteStartElement("fileContent");
        this.WriteParamGroup(fdesc.FileContent);
        writer.WriteEndElement();

        this.WriteList("sourceFileList", fdesc.SourceFiles, this.WriteSourceFile)

        //missing null check
        writer.WriteStartElement("contact");
        this.WriteParamGroup(fdesc.Contact)
        writer.WriteEndElement()

        writer.WriteEndElement()

    member private this.WriteDataProcessing(dp:DataProcessing) =

        writer.WriteStartElement("dataProcessing")
        this.WriteXmlAttribute("id", dp.ID, true)
        this.WriteParamGroup(dp)

        dp.ProcessingSteps.GetProperties false
        |> Seq.map (fun item -> item.Value :?> DataProcessingStep)
        |> Seq.fold (fun (order:int) (dps:DataProcessingStep) ->
            writer.WriteStartElement("processingMethod")
            this.WriteXmlAttribute("order", order.ToString(formatProvider))
            this.WriteParamGroup(dp)
            writer.WriteEndElement()

            let tmp = dps.Software.GetProperties false |> Seq.length
            if tmp > 0  then
                writer.WriteStartElement("softwareRef");
                this.WriteXmlAttribute("ref", dps.Software.ID, true);
                writer.WriteEndElement();
            else ()
            order + 1
            ) 1 |> ignore

        writer.WriteEndElement()

    member private this.WriteSoftware(sw: Software) =

        writer.WriteStartElement("software")
        this.WriteXmlAttribute("id", sw.ID, true)
        this.WriteXmlAttribute("version", "not supported")
        this.WriteParamGroup(sw)
        writer.WriteEndElement()

    member private this.WriteComponent(comp: Component, index: int) =

        let mutable elemName = ""

        if (comp :? SourceComponent) then
           elemName <- "sourceComponent"
        if (comp :? DetectorComponent) then
           elemName <- "detectorComponent"
        if (comp :? AnalyzerComponent) then
           elemName <- "analyzerComponent"
        else
            ()
        writer.WriteStartElement(elemName)
        this.WriteXmlAttribute("order", index.ToString(formatProvider))
        this.WriteParamGroup(comp)
        writer.WriteEndElement()

    member private this.WriteInstrument(instr: Instrument) =

        writer.WriteStartElement("instrumentConfiguration")
        this.WriteXmlAttribute("id", instr.ID, true)
        this.WriteParamGroup(instr)

        //missing null check
        writer.WriteStartElement("softwareRef")
        this.WriteXmlAttribute("ref", instr.Software.ID, true)
        writer.WriteEndElement()

        // TODO scanSettingsRef

        this.WriteList2("componentList", instr.Components, this.WriteComponent, true)

        writer.WriteEndElement()

    member private this.WriteSample(sample: Sample) =

        writer.WriteStartElement("sample")

        this.WriteXmlAttribute("id", sample.ID, true)
        this.WriteXmlAttribute("name", sample.Name, false)
        this.WriteParamGroup(sample)

        writer.WriteEndElement()

    member private this.WriteProduct(p: Product) =

        writer.WriteStartElement("product")
        //missing null check
        this.WriteIsolationWindow(p.IsolationWindow)
        writer.WriteEndElement()

    member private this.WritePrecursor(pc: Precursor) =

        writer.WriteStartElement("precursor")
        //missing null check
        this.WriteSpectrumRef(pc.SpectrumReference)
        this.WriteList("selectedIonList", pc.SelectedIons, this.WriteSelectedIon)
        //missing null check
        this.WriteIsolationWindow(pc.IsolationWindow)
        //missing null check
        writer.WriteStartElement("activation")
        this.WriteParamGroup(pc.Activation)
        writer.WriteEndElement()

        writer.WriteEndElement()

    member private this.WriteIsolationWindow(isolationWindow: IsolationWindow) =

        writer.WriteStartElement("isolationWindow")
        this.WriteParamGroup(isolationWindow)
        writer.WriteEndElement()

    member private this.WriteSelectedIon(ion: SelectedIon) =

        writer.WriteStartElement("selectedIon")
        this.WriteParamGroup(ion)
        writer.WriteEndElement()

    member private this.WriteScanWindow(sw: ScanWindow) =

        writer.WriteStartElement("scanWindow")
        this.WriteParamGroup(sw)
        writer.WriteEndElement()

    member private this.WriteScan(scan: Scan) =

        writer.WriteStartElement("scan")
        //missing null check
        this.WriteSpectrumRef(scan.SpectrumReference)
        this.WriteParamGroup(scan)
        this.WriteList("scanWindowList", scan.ScanWindows, this.WriteScanWindow)
        writer.WriteEndElement()

    member private this.WriteSpectrumRef(spectrumReference: SpectrumReference) =

        if (spectrumReference.IsExternal) then
            this.WriteXmlAttribute("sourceFileRef", spectrumReference.SourceFileID, true)
            this.WriteXmlAttribute("externalSpectrumID", spectrumReference.SpectrumID, true)
        else
            this.WriteXmlAttribute("spectrumRef", spectrumReference.SpectrumID, true)

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////#region binary data writing/////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    member private this.WriteBinaryDataArray(values: double[], binaryDataType: BinaryDataType, pars: UserDescription) =

        let encoder = new BinaryDataEncoder(values.Length * 8)
        let base64 = encoder.EncodeBase64(values, BinaryDataCompressionType.NoCompression, binaryDataType)
        let len = base64.Length

        writer.WriteStartElement("binaryDataArray")
        this.WriteXmlAttribute("encodedLength", len.ToString(formatProvider))

        this.WriteParamGroup(pars)

        writer.WriteStartElement("binary")
        writer.WriteString(base64)
        writer.WriteEndElement()

        writer.WriteEndElement()

    member private this.WriteBinaryDataArrayList(peaks: Peak1DArray) =

        writer.WriteStartElement("binaryDataArrayList");
        this.WriteXmlAttribute("count", "2");

        let mzParams = new UserDescription("mzParams");
        mzParams
            .SetMzArray()
            .SetCompression(BinaryDataCompressionType.NoCompression)
            .SetBinaryDataType(peaks.MzDataType) |> ignore

        let mzValues = peaks.Peaks.Select(fun x -> x.Mz).ToArray()
        this.WriteBinaryDataArray(mzValues, peaks.MzDataType, mzParams)

        let intParams = new UserDescription("intParams")
        intParams
            .SetIntensityArray().NoUnit()
            .SetCompression(BinaryDataCompressionType.NoCompression)
            .SetBinaryDataType(peaks.IntensityDataType) |> ignore

        let intValues = peaks.Peaks.Select(fun x -> x.Intensity).ToArray()
        this.WriteBinaryDataArray(intValues, peaks.IntensityDataType, intParams)

        writer.WriteEndElement()

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    member this.BeginMzML(model:MzIOModel) =

        try
            this.EnterWriteState(MzMLWriteState.INITIAL, MzMLWriteState.MzIOModel)
            writer.WriteStartElement("mzML", "http://psi.hupo.org/ms/mzml")
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance")
            writer.WriteAttributeString("xsi", "schemaLocation", null, "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.0.xsd")
            writer.WriteAttributeString("version", "1.1.0")

            this.WriteCvList()

            //TODO scanSettingsList, also add to model

            this.WriteFileDescription(model.FileDescription)
            this.WriteList<DataProcessing>("dataProcessingList", model.DataProcessings, this.WriteDataProcessing, false)
            this.WriteList("softwareList", model.Softwares, this.WriteSoftware, false)
            this.WriteList("instrumentConfigurationList", model.Instruments, this.WriteInstrument, false)
            this.WriteList("sampleList", model.Samples, this.WriteSample)

        with
            | :? Exception as ex ->
                currentWriteState <- MzMLWriteState.ERROR
                raise (MzIOIOException("Error writing mzml output file.", ex))

    member this.EndMzML() =

        try
            this.LeaveWriteState(MzMLWriteState.MzIOModel, MzMLWriteState.INITIAL)
            writer.WriteEndElement()

        with
            | :? Exception as ex ->
                currentWriteState <- MzMLWriteState.ERROR
                raise (MzIOIOException("Error writing mzml output file.", ex))

    member this.BeginRun(run: Run) =

        try
            //missing null check
            this.EnterWriteState(MzMLWriteState.MzIOModel, MzMLWriteState.RUN)

            writer.WriteStartElement("run")
            this.WriteXmlAttribute("id", run.ID, true)
            //missing null check
            this.WriteXmlAttribute("sampleRef", run.Sample.ID, true)
            //missing null check
            this.WriteXmlAttribute("defaultInstrumentConfigurationRef", run.DefaultInstrument.ID, true)

            this.WriteParamGroup(run)

        with
            | :? Exception as ex ->
                currentWriteState <- MzMLWriteState.ERROR
                raise (MzIOIOException("Error writing mzml output file.", ex))

    member this.EndRun() =

        try
            this.LeaveWriteState(MzMLWriteState.RUN, MzMLWriteState.MzIOModel)
            writer.WriteEndElement()

        with
            | :? Exception as ex ->
                currentWriteState <- MzMLWriteState.ERROR
                raise (MzIOIOException("Error writing mzml output file.", ex))

    member this.BeginSpectrumList(count: int) =

        try
            if count < 0 then
                raise (ArgumentOutOfRangeException("count"))
            this.EnterWriteState(MzMLWriteState.RUN, MzMLWriteState.SPECTRUM_LIST)
            writer.WriteStartElement("spectrumList")
            this.WriteXmlAttribute("count", count.ToString(formatProvider))

        with
            | :? Exception as ex ->
                currentWriteState <- MzMLWriteState.ERROR
                raise (MzIOIOException("Error writing mzml output file.", ex))

    member this.EndSpectrumList() =

        try
            this.LeaveWriteState(MzMLWriteState.SPECTRUM_LIST, MzMLWriteState.RUN)
            writer.WriteEndElement()

        with
            | :? Exception as ex ->
                currentWriteState <- MzMLWriteState.ERROR
                raise (MzIOIOException("Error writing mzml output file.", ex))

    member this.WriteSpectrum(ms: MassSpectrum, peaks: Peak1DArray, index: int) =

        try
            //missing null checks for ms and peaks
            if index < 0 then
                raise (ArgumentOutOfRangeException("idx"))

            this.EnsureWriteState(MzMLWriteState.SPECTRUM_LIST)

            writer.WriteStartElement("spectrum")

            this.WriteXmlAttribute("id", ms.ID, true)
            this.WriteXmlAttribute("index", index.ToString(formatProvider), true)
            this.WriteXmlAttribute("dataProcessingRef", ms.DataProcessingReference, false)
            this.WriteXmlAttribute("sourceFileRef", ms.SourceFileReference, false)
            this.WriteXmlAttribute("defaultArrayLength", peaks.Peaks.Length.ToString(formatProvider), true)

            this.WriteParamGroup(ms)

            this.WriteList("scanList", ms.Scans, this.WriteScan)
            this.WriteList("precursorList", ms.Precursors, this.WritePrecursor)
            this.WriteList("productList", ms.Products, this.WriteProduct)

            this.WriteBinaryDataArrayList(peaks)

            writer.WriteEndElement()

        with
            | :? Exception as ex ->
                currentWriteState <- MzMLWriteState.ERROR
                raise (MzIOIOException("Error writing mzml output file.", ex))

    interface IMzIOIO with

        member this.CreateDefaultModel() =

            failwith "interface not supported yet"

        member this.Model =
            
            failwith "interface not supported yet"

        member this.SaveModel() =

            failwith "interface not supported yet"

        member this.BeginTransaction() =

            failwith "interface not supported yet"

    member this.BeginTransaction() =

        (this :> IMzIOIO).BeginTransaction()

    member this.CreateDefaultModel() =

        (this :> IMzIOIO).CreateDefaultModel()

    member this.SaveModel() =

        (this :> IMzIOIO).SaveModel()

    member this.Model =
        (this :> IMzIOIO).Model


    interface IMzIODataWriter with
        
        member this.InsertMass(spectrumID: string, ms: MzIO.Model.MassSpectrum, peak1D: Peak1DArray) =
            
            failwith "interface not supported yet"

        member this.InsertChrom(chromatogramID: string, chrom: Chromatogram, peak2D: Peak2DArray) =

            failwith "interface not supported yet"

        member this.InsertAsyncMass(spectrumID: string, ms: MzIO.Model.MassSpectrum, peak1D: Peak1DArray) =
            
            failwith "interface not supported yet"

        member this.InsertAsyncChrom(chromatogramID: string, chrom: Chromatogram, peak2D: Peak2DArray) =

            failwith "interface not supported yet"

    member this.InsertMass(spectrumID: string, ms: MzIO.Model.MassSpectrum, peak1D: Peak1DArray) =
        
        (this :> IMzIODataWriter).InsertMass

    member this.InsertChrom(chromatogramID: string, chrom: Chromatogram, peak2D: Peak2DArray) =

        (this :> IMzIODataWriter).InsertChrom

    member this.InsertAsyncMass(spectrumID: string, ms: MzIO.Model.MassSpectrum, peak1D: Peak1DArray) =

        (this :> IMzIODataWriter).InsertAsyncMass

    member this.InsertAsyncChrom(chromatogramID: string, chrom: Chromatogram, peak2D: Peak2DArray) =

        (this :> IMzIODataWriter).InsertAsyncChrom



type private MzMLCompression(?initialBufferSize:int) =

    let bufferSize = defaultArg initialBufferSize 1048576
    
    let mutable memoryStream' = new MemoryStream(bufferSize)

    member this.memoryStream
        with get() = memoryStream'
        and private set(value) = memoryStream' <- value

    //Write float32 instead of float64 for float32.
    static member WriteValue(writer:BinaryWriter, binaryDataType:BinaryDataType, value:double) =
        match binaryDataType with
        | BinaryDataType.Int32      ->  writer.Write(int32 value)
        | BinaryDataType.Int64      ->  writer.Write(int64 value)
        | BinaryDataType.Float32    ->  writer.Write(single value)
        | BinaryDataType.Float64    ->  writer.Write(double value)
        | _     -> failwith (sprintf "%s%s" "BinaryDataType not supported: " (binaryDataType.ToString()))    

    static member private NoCompression(memoryStream:Stream, dataType:BinaryDataType, floats:double[]) =
        
        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        //let (len:int32) = floats.Length
        //writer.Write(len)
        for item in floats do
            MzMLCompression.WriteValue(writer, dataType, item)

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

    static member private FloatToByteArray (floatArray: float[]) =
        let byteArray = Array.init (floatArray.Length*8) (fun x -> byte(0))
        Buffer.BlockCopy (floatArray, 0, byteArray, 0, byteArray.Length)
        byteArray

    static member private ZLib(memoryStream:Stream, floats:float[]) =
        let bytes       = MzMLCompression.FloatToByteArray(floats |> Array.ofSeq)
        let byteDeflate = MzMLCompression.DeflateStreamCompress bytes

        let writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, true)
        writer.Write(byteDeflate.Length)
        writer.Write(byteDeflate)

    member this.Encode(compressionType:BinaryDataCompressionType, dataType:BinaryDataType, floats:float[]) =
       
        this.memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore

        match compressionType with
        | BinaryDataCompressionType.NoCompression   -> MzMLCompression.NoCompression(this.memoryStream, dataType, floats)
        | BinaryDataCompressionType.ZLib            -> MzMLCompression.ZLib(this.memoryStream, floats)
        //| BinaryDataCompressionType.NumPress        -> MzMLCompression.Numpress(this.memoryStream, peakArray)
        //| BinaryDataCompressionType.NumPressZLib    -> MzMLCompression.NumpressDeflate(this.memoryStream, peakArray)
        | _ -> failwith (sprintf "Compression type not supported: %s" (compressionType.ToString()))
        
        this.memoryStream.ToArray()

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

type MzIOMLDataWriter(path:string) =

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

    member private this.EnterWriteState(expectedWs:MzMLWriteState, newWs:MzMLWriteState) =
        if consumedWriteStates.Contains(newWs) then
            raise (MzIOIOException(String.Format("Can't reentering write state: '{0}'.", newWs)))
        this.EnsureWriteState(expectedWs)
        currentWriteState <- newWs
        consumedWriteStates.Add(newWs) |> ignore

    member private this.EnsureWriteState(expectedWs:MzMLWriteState) =
        if currentWriteState = MzMLWriteState.ERROR then
            raise (new MzIOIOException("Current write state is ERROR."))
        if currentWriteState = MzMLWriteState.CLOSED then
            raise (new MzIOIOException("Current write state is CLOSED."))
        if currentWriteState <> expectedWs then
            raise (MzIOIOException(String.Format("Invalid write state: expected '{0}' but current is '{1}'.", expectedWs, currentWriteState)))

    member this.WriteCvParam(param:CvParam<#IConvertible>) =
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

    member this.WriteUserParam(param:UserParam<#IConvertible>) =
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

    member this.WriteCvList() =
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

    member private this.assignParam<'T when 'T :> IConvertible>(item:Object) =
        match item with
        | :? CvParam<'T>     -> this.WriteCvParam    (item :?> CvParam<'T>)
        | :? UserParam<'T>   -> this.WriteUserParam  (item :?> UserParam<'T>)
        |   _ -> failwith "Not castable to Cv nor UserParam"

    member this.WriteDetector(item:DetectorComponent) =
        writer.WriteStartElement("detector")
        writer.WriteAttributeString("order", "3")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    member this.WriteAnalyzer(item:AnalyzerComponent) =
        writer.WriteStartElement("analyzer")
        writer.WriteAttributeString("order", "2")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    member this.WriteSource(item:SourceComponent) =
        writer.WriteStartElement("source")
        writer.WriteAttributeString("order", "1")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    member this.WriteProcessingMethod(item:DataProcessingStep) =
        writer.WriteStartElement("processingMethod")
        writer.WriteAttributeString("order", item.Name)
        writer.WriteAttributeString("softwareRef", item.Software.ID)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    member this.WriteSoftwareRef(item:Software) =
        writer.WriteStartElement("softwareRef")
        writer.WriteAttributeString("ref", item.ID)
        writer.WriteEndElement()

    member private this.assignComponent(item:Object) =
        match item with
        | :? SourceComponent    -> this.WriteSource     (item :?> SourceComponent)
        | :? AnalyzerComponent  -> this.WriteAnalyzer   (item :?> AnalyzerComponent)
        | :? DetectorComponent  -> this.WriteDetector   (item :?> DetectorComponent)
        |   _ -> failwith "Not castable to SourceComponent nor AnalyzerComponent nor DetectorComponent"

    member this.WriteComponentList(item:ComponentList) =
        writer.WriteStartElement("componentList")
        writer.WriteAttributeString("count", (item.GetProperties false |> Seq.length).ToString())
        item.GetProperties false
        |> Seq.iter (fun component' -> this.assignComponent(component'.Value))
        writer.WriteEndElement()

    member this.WriteSourceFile(item:SourceFile) =
        writer.WriteStartElement("sourceFile")
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("location", item.Location)
        writer.WriteAttributeString("name", item.Name)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param.Value))
        writer.WriteEndElement()

    member this.WriteSelectedIon(item:SelectedIon) =
        writer.WriteStartElement("selectedIon")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member this.WriteScanWindow(item:ScanWindow) =
        writer.WriteStartElement("scanWindow")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member this.WriteBinary(item:string) =
        writer.WriteElementString("binary", item)

    member this.WriteActivation(item:Activation) =
        writer.WriteStartElement("activation")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member this.WriteSelectedIonList(item:SelectedIonList) =
        writer.WriteStartElement("selectedIonList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun selectedIon -> this.WriteSelectedIon(selectedIon.Value :?> SelectedIon))
        writer.WriteEndElement()

    member this.WriteIsolationWindow(item:IsolationWindow) =
        writer.WriteStartElement("isolationWindow")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member this.WriteScanWindowList(item:ScanWindowList) =
        writer.WriteStartElement("scanWindowList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun scanWindow -> this.WriteScanWindow(scanWindow.Value :?> ScanWindow))
        writer.WriteEndElement()    

    static member private assignBinaryDataType(item:BinaryDataType) =
        match item with
        | BinaryDataType.Int32      -> "MS:1000519"
        | BinaryDataType.Int64      -> "MS:1000522"
        | BinaryDataType.Float32    -> "MS:1000521"
        | BinaryDataType.Float64    -> "MS:1000523"
        | _ -> failwith "BinaryDataType is unknown"

    static member private assignCompressionType(item:BinaryDataCompressionType) =
        match item with
        | BinaryDataCompressionType.NoCompression   -> "MS:1000576"
        | BinaryDataCompressionType.ZLib            -> "MS:1000574"
        | BinaryDataCompressionType.NumPress        -> failwith "BinaryDataCompressionType is unknown"
        | BinaryDataCompressionType.NumPressZLib    -> failwith "BinaryDataCompressionType is unknown"
        | BinaryDataCompressionType.NumPressPic     -> failwith "BinaryDataCompressionType is unknown"
        | BinaryDataCompressionType.NumPressLin     -> failwith "BinaryDataCompressionType is unknown"
        | _ -> failwith "BinaryDataCompressionType is unknown"

    member this.WriteQParams(item:Peak1DArray) =
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", (MzIOMLDataWriter.assignBinaryDataType item.MzDataType).ToString())
        writer.WriteAttributeString("name", item.MzDataType.ToString())
        writer.WriteAttributeString("value", "")
        writer.WriteEndElement()
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("accession", (MzIOMLDataWriter.assignCompressionType item.CompressionType).ToString())
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("name", item.CompressionType.ToString())
        writer.WriteAttributeString("value", "")
        writer.WriteEndElement()
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", "MS:1000514")      
        writer.WriteAttributeString("name", "m/z array")
        writer.WriteAttributeString("value", "")
        writer.WriteAttributeString("unitCvRef", "UO")
        writer.WriteAttributeString("unitAccession", "MS:1000040")
        writer.WriteAttributeString("unitName", "m/z")
        writer.WriteEndElement()

    member this.WriteIntensityParams(item:Peak1DArray) =
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", (MzIOMLDataWriter.assignBinaryDataType item.MzDataType).ToString())
        writer.WriteAttributeString("name", "not saved yet")
        writer.WriteAttributeString("value", "")
        writer.WriteEndElement()
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("accession", (MzIOMLDataWriter.assignCompressionType item.CompressionType).ToString())
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("name", "not saved yet")
        writer.WriteAttributeString("value", "")
        writer.WriteEndElement()
        writer.WriteStartElement("cvParam")
        writer.WriteAttributeString("cvRef", "MS")
        writer.WriteAttributeString("accession", "MS:1000515")      
        writer.WriteAttributeString("name", "intensity array")
        writer.WriteAttributeString("value", "")
        writer.WriteAttributeString("unitCvRef", "UO")
        writer.WriteAttributeString("unitAccession", "MS:1000131")
        writer.WriteAttributeString("unitName", "number of counts")
        writer.WriteEndElement()

    member this.WriteBinaryDataArray(spectrum:MassSpectrum, peaks:Peak1DArray) =
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

    member this.WriteProduct(item:Product) =
        writer.WriteStartElement("product")
        this.WriteIsolationWindow(item.IsolationWindow)
        writer.WriteEndElement()  

    member this.WritePrecursor(item:Precursor) =
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
    
    member this.WriteScan(item:Scan) =
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

    member this.WriteBinaryDataArrayList(spectrum:MassSpectrum, peaks:Peak1DArray) =
        writer.WriteStartElement("binaryDataArrayList")
        writer.WriteAttributeString("count", "2")
        this.WriteBinaryDataArray(spectrum, peaks)
        writer.WriteEndElement()

    member this.WriteProductList(item:ProductList) =
        writer.WriteStartElement("productList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun product -> this.WriteProduct(product.Value :?> Product))
        writer.WriteEndElement()

    member this.WritePrecursorList(item:PrecursorList) =
        writer.WriteStartElement("precursorList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun precursor -> this.WritePrecursor(precursor.Value :?> Precursor))
        writer.WriteEndElement()

    member this.WriteScanList<'T when 'T :> IConvertible>(item:ScanList) =
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
    member this.WriteChromatogram(item:Chromatogram, peaks:Peak2DArray) =
        this.EnsureWriteState(MzMLWriteState.CHROMATOGRAM)
        failwith "not supported yet"
        //writer.WriteStartElement("chromatogram")
        //writer.WriteAttributeString("count", peaks.)
        //spectrum.GetProperties false
        //|> Seq.iter (fun param -> this.assignParam(param.Value))
        //writer.WriteEndElement()

    member this.WriteSpectrum(spectrum:MassSpectrum, index:int, peaks:Peak1DArray) =
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

    member this.WriteChromatogramList(item:Run, chromatogramListCount:int) =
        this.EnsureWriteState(MzMLWriteState.CHROMATOGRAM_LIST)
        writer.WriteStartElement("chromatogramList") 
        writer.WriteAttributeString("count", chromatogramListCount.ToString())
        writer.WriteAttributeString("defaultDataProcessingRef", item.DefaultChromatogramProcessing.ID)
        this.EnterWriteState(MzMLWriteState.CHROMATOGRAM_LIST, MzMLWriteState.CHROMATOGRAM)
        //this.WriteChromatogram()
        writer.WriteEndElement()

    member this.WriteSpectrumList(item:Run, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>) =
        this.EnsureWriteState(MzMLWriteState.SPECTRUM_LIST)
        writer.WriteStartElement("spectrumList") 
        writer.WriteAttributeString("count", (Seq.length spectra).ToString())
        writer.WriteAttributeString("defaultDataProcessingRef", item.DefaultSpectrumProcessing.ID)
        this.EnterWriteState(MzMLWriteState.SPECTRUM_LIST, MzMLWriteState.SPECTRUM)
        Seq.fold2 (fun start spectrum peak -> this.WriteSpectrum(spectrum, start + 1, peak)) 0 spectra peaks |> ignore
        writer.WriteEndElement()

    member this.WriteDataProcessing(item:DataProcessing) =
        writer.WriteStartElement("dataProcessing") 
        writer.WriteAttributeString("id", item.ID)
        item.ProcessingSteps.GetProperties false
        |> Seq.iter (fun dataStep -> this.WriteProcessingMethod (dataStep.Value :?> DataProcessingStep))
        writer.WriteEndElement()

    member this.WriteInstrumentConfiguration(item:Instrument) =
        writer.WriteStartElement("instrumentConfiguration")
        writer.WriteAttributeString("id", item.ID)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        this.WriteComponentList item.Components
        this.WriteSoftwareRef item.Software
        writer.WriteEndElement()

    member this.WriteSoftware(item:Software) =
        writer.WriteStartElement("software")
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("version", "not saved yet")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member this.WriteSample(item:Sample) =
        writer.WriteStartElement("sample")
        writer.WriteAttributeString("id", item.ID)
        writer.WriteAttributeString("name", item.Name)
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member this.WriteContact(item:Contact) =
        writer.WriteStartElement("contact")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member this.WriteSourceFileList(item:SourceFileList) =
        writer.WriteStartElement("sourceFileList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun source -> this.WriteSourceFile (source.Value :?> SourceFile))
        writer.WriteEndElement()

    member this.WriteFileContent(item:FileContent) =
        writer.WriteStartElement("fileContent")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member this.WriteRun(item:Run, model:MzIOModel, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>, chromatogramListCount:int) =
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

    member this.WriteDataProcessingList(item:DataProcessingList) =
        writer.WriteStartElement("dataProcessingList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun dataProc -> this.WriteDataProcessing (dataProc.Value :?> DataProcessing))
        writer.WriteEndElement()

    member this.WriteInstrumentConfigurationList(item:InstrumentList) =
        writer.WriteStartElement("instrumentConfigurationList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun instConf -> this.WriteInstrumentConfiguration (instConf.Value :?> Instrument))
        writer.WriteEndElement()

    member this.WriteTarget(item:MzIOModel) =
        writer.WriteStartElement("target")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam param.Value)
        writer.WriteEndElement()

    member this.WriteSourceFileRef(item:string) =
        writer.WriteStartElement("sourceFileRef")
        writer.WriteAttributeString("ref", item)
        writer.WriteEndElement()

    member this.WriteTargetList(item:SourceFileList) =
        writer.WriteStartElement("targetList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun sourceFile -> this.WriteSourceFileRef((sourceFile.Value :?> SourceFile).ID))
        writer.WriteEndElement()

    member this.WriteSourceFileRefList(item:SourceFileList) =
        writer.WriteStartElement("sourceFileRefList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun sourceFile -> this.WriteSourceFileRef((sourceFile.Value :?> SourceFile).ID))
        writer.WriteEndElement()

    member this.WriteScanSettings(item:MzIOModel) =
        writer.WriteStartElement("scanSettings")
        writer.WriteAttributeString("id", "PlaceHolderID_settings")
        item.GetProperties false
        |> Seq.iter (fun param -> this.assignParam(param))
        if Seq.length (item.FileDescription.SourceFiles.GetProperties false) > 0 then
            this.WriteSourceFileRefList(item.FileDescription.SourceFiles)
        //this.WriteTargetList(item)
        writer.WriteEndElement()
        
    member this.WriteScanSettingsList(item:MzIOModel) =
        writer.WriteStartElement("scanSettingsList")
        writer.WriteAttributeString("count", (Seq.length (item.FileDescription.SourceFiles.GetProperties false)).ToString())
        this.WriteScanSettings(item)
        writer.WriteEndElement()

    member this.WriteSoftwareList(item:SoftwareList) =
        this.EnsureWriteState(MzMLWriteState.MzIOModel)
        writer.WriteStartElement("softwareList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun software -> this.WriteSoftware(software.Value :?> Software))
        writer.WriteEndElement()

    member this.WriteSampleList(item:SampleList) =
        this.EnsureWriteState(MzMLWriteState.MzIOModel)
        writer.WriteStartElement("sampleList")
        writer.WriteAttributeString("count", (Seq.length (item.GetProperties false)).ToString())
        item.GetProperties false
        |> Seq.iter (fun sample -> this.WriteSample (sample.Value :?> Sample))
        writer.WriteEndElement()

    member this.WriteFileDescription(item:FileDescription) =
        this.EnsureWriteState(MzMLWriteState.MzIOModel)
        writer.WriteStartElement("fileDescription")
        this.WriteFileContent(item.FileContent)
        if Seq.length (item.SourceFiles.GetProperties false) > 0 then
            this.WriteSourceFileList(item.SourceFiles)
        if Seq.length (item.Contact.GetProperties false) >0 then
            this.WriteContact(item.Contact)
        writer.WriteEndElement()

    member this.WriteRunList(item:RunList, model:MzIOModel, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>, chromatogramListCount:int) =
        this.EnsureWriteState(MzMLWriteState.RUN)
        item.GetProperties false
        |> Seq.iter (fun run -> this.WriteRun(run.Value :?> Run, model, spectra, peaks, chromatogramListCount))
        //writer.WriteEndElement()    

    member this.WriteMzMl(item:MzIOModel, spectra:seq<MassSpectrum>, peaks:seq<Peak1DArray>) = 
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

    member this.WriteSingleSpectrumList(count:string, spectrumProcessingRef:string) =
        this.EnsureWriteState(MzMLWriteState.SPECTRUM_LIST)
        writer.WriteStartElement("spectrumList") 
        writer.WriteAttributeString("count", count)
        writer.WriteAttributeString("defaultDataProcessingRef", spectrumProcessingRef)
        //this.WriteSpectrum(spectrum, peaks)
        //writer.WriteEndElement()
        this.EnterWriteState(MzMLWriteState.SPECTRUM_LIST, MzMLWriteState.SPECTRUM)

    member this.WriteSingleRun(runID:string, instrumentRef:string, defaultSourceFileRef:string, sampleRef:string, count:string, spectrumProcessingRef:string) =
        this.EnsureWriteState(MzMLWriteState.RUN)
        writer.WriteStartElement("run")
        writer.WriteAttributeString("defaultInstrumentConfigurationRef", instrumentRef)
        writer.WriteAttributeString("defaultSourceFileRef", defaultSourceFileRef)
        writer.WriteAttributeString("id", runID)
        writer.WriteAttributeString("sampleRef", sampleRef)
        this.EnterWriteState(MzMLWriteState.RUN, MzMLWriteState.SPECTRUM_LIST)
        this.WriteSingleSpectrumList(count, spectrumProcessingRef)
        //writer.WriteEndElement()  

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

    member this.InsertMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertMass(runID, spectrum, peaks)

    member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertChrom(runID, chromatogram, peaks)

    member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertAsyncMass(runID, spectrum, peaks)

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
        
