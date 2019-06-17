﻿namespace MzIO.IO.MzML

open System
open System.Globalization
open System.Xml
open MzIO.Model
open MzIO.Model.CvParam
open System.Linq
open MzIO.MetaData
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.PSIMSExtension
open System.Collections.Generic
open MzIO.Binary
open MzIO.IO
open MzIO.Model.CvParam

type private MzMLWriteState =
    ERROR
    | INITIAL
    | CLOSED
    | MZML
    | RUN
    | SPECTRUM_LIST
    | CHROMATOGRAM_LIST

//    // TODO cv lookup
//    // TODO param name lookup
//    // TODO model only one run
//    // TODO simplify write states, only speclist, chromlist
//    // TODO write chromatogram list
//    // TODO get disposable on all beginxxx methods
[<Sealed>]
type MzMLWriter(path:string) =

    let mutable formatProvider = new CultureInfo("en-US")
    let mutable isClosed = false

    let mutable currentWriteState   = MzMLWriteState.INITIAL
    let mutable consumedWriteStates = new HashSet<MzMLWriteState>()

    let writer =
        match path with
        | null  -> failwith ((new ArgumentNullException("path")).ToString())
        | ""    -> failwith ((new ArgumentNullException("path")).ToString())
        | " "   -> failwith ((new ArgumentNullException("path")).ToString())
        |   _   ->
            try
                let tmp = XmlWriter.Create(path, new XmlWriterSettings())
                tmp.Settings.Indent <- true
                tmp.WriteStartDocument()
                tmp
            with
                | :? Exception as ex ->
                    currentWriteState <- MzMLWriteState.ERROR
                    failwith ((new MzLiteIOException("Error init mzml output file.", ex)).ToString())

    interface IDisposable with

        member this.Dispose() =

            this.Close()

    member private this.EnsureWriteState(expectedWs:MzMLWriteState) =

        if currentWriteState = MzMLWriteState.ERROR then
            failwith ((new MzLiteIOException("Current write state is ERROR.")).ToString())
        else
            if currentWriteState = MzMLWriteState.CLOSED then
                failwith ((new MzLiteIOException("Current write state is CLOSED.")).ToString())
            else
                if currentWriteState <> expectedWs then
                    failwith ((new MzLiteIOException(String.Format("Invalid write state: expected '{0}' but current is '{1}'.", expectedWs, currentWriteState))).ToString())

    member private this.EnterWriteState(expectedWs:MzMLWriteState, newWs:MzMLWriteState) =

            if consumedWriteStates.Contains(newWs) then
                failwith (((new MzLiteIOException(String.Format("Can't reentering write state: '{0}'.", newWs))).ToString()))
            else
                this.EnsureWriteState(expectedWs)
                currentWriteState = newWs
                consumedWriteStates.Add(newWs)

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
                    failwith ((new MzLiteIOException("Error closing mzml output file.", ex)).ToString())
        else ()

    //#region xml writing helper
    member this.WriteXmlAttribute(name:string, value:string, required:bool) =

        if required = true then
            match path with
            | null  -> failwith ((new MzLiteIOException("Value required for xml attribute: " + name)).ToString())
            | ""    -> failwith ((new MzLiteIOException("Value required for xml attribute: " + name)).ToString())
            | " "   -> failwith ((new MzLiteIOException("Value required for xml attribute: " + name)).ToString())
            |   _   -> writer.WriteAttributeString(name, value)
        else
            writer.WriteAttributeString(name, value)

    //#region xml writing helper
    member this.WriteXmlAttribute(name:string, value:string) =

        let required = false

        if required = true then
            match path with
            | null  -> failwith ((new MzLiteIOException("Value required for xml attribute: " + name)).ToString())
            | ""    -> failwith ((new MzLiteIOException("Value required for xml attribute: " + name)).ToString())
            | " "   -> failwith ((new MzLiteIOException("Value required for xml attribute: " + name)).ToString())
            |   _   -> writer.WriteAttributeString(name, value)
        else
            writer.WriteAttributeString(name, value)

    member private this.WriteList<'TItem>(elementName:string, list:DynamicObj, writeItem:Action<'TItem>, ?skipEmpty:bool) =

        let skipEmpty = defaultArg skipEmpty true
        let count = list.GetProperties false |> Seq.length
        if skipEmpty= true && count = 0 then ()
        else
            writer.WriteStartElement(elementName)
            this.WriteXmlAttribute("count", count.ToString(formatProvider))
            list.GetProperties false
            |> Seq.iter (fun item -> writeItem.Invoke(item.Value :?> 'TItem))

            writer.WriteEndElement()

    member private this.WriteList<'TItem>(elementName:string, list:DynamicObj, writeItem:Action<'TItem, int>, ?skipEmpty:bool) =

        let skipEmpty = defaultArg skipEmpty true
        let count = list.GetProperties false |> Seq.length
        if skipEmpty= true && count = 0 then ()
        else
            writer.WriteStartElement(elementName)
            this.WriteXmlAttribute("count", count.ToString(formatProvider))
            list.GetProperties false
            |> Seq.fold (fun (idx:int) item ->
                writeItem.Invoke(item.Value :?> 'TItem, idx)
                idx + 1) 0
            |> ignore

            writer.WriteEndElement()

    member this.BeginMzML(model:MzLiteModel) =

        try
            this.EnterWriteState(MzMLWriteState.INITIAL, MzMLWriteState.MZML) |> ignore
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
                failwith ((new MzLiteIOException("Error writing mzml output file.", ex)).ToString())

    //#region param writing
    member this.WriteCvList() =
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

    member this.ParseCvRef(accession:string) =

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

    //#region model writing

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

        //null check missing
        writer.WriteStartElement("contact");
        this.WriteParamGroup(fdesc.Contact)
        writer.WriteEndElement()

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

    member private this.WriteInstrument(instr: Instrument) =

        writer.WriteStartElement("instrumentConfiguration")
        this.WriteXmlAttribute("id", instr.ID, true)
        this.WriteParamGroup(instr)

        //null check missing
        writer.WriteStartElement("softwareRef")
        this.WriteXmlAttribute("ref", instr.Software.ID, true)
        writer.WriteEndElement()

        // TODO scanSettingsRef

        this.WriteList("componentList", instr.Components, this.WriteComponent, true)

        writer.WriteEndElement()

    member private this.WriteSample(sample: Sample) =

        writer.WriteStartElement("sample")

        this.WriteXmlAttribute("id", sample.ID, true)
        this.WriteXmlAttribute("name", sample.Name, false)
        this.WriteParamGroup(sample)

        writer.WriteEndElement()

    member private this.WriteIsolationWindow(isolationWindow: IsolationWindow) =

        writer.WriteStartElement("isolationWindow")
        this.WriteParamGroup(isolationWindow)
        writer.WriteEndElement()

    member private this.WriteProduct(p: Product) =

        writer.WriteStartElement("product")
        //missing null check
        this.WriteIsolationWindow(p.IsolationWindow)
        writer.WriteEndElement()

    member private this.WritePrecursor(pc: Precursor) =

        //missing null check
        this.WriteIsolationWindow(pc.IsolationWindow)
        //missing null check
        writer.WriteStartElement("activation")
        this.WriteParamGroup(pc.Activation)
        writer.WriteEndElement()

        writer.WriteEndElement()

    member private this.WriteSelectedIon(ion: SelectedIon) =

        writer.WriteStartElement("selectedIon")
        this.WriteParamGroup(ion)
        writer.WriteEndElement()

    member private this.WriteScanWindow(sw: ScanWindow) =

        writer.WriteStartElement("scanWindow")
        this.WriteParamGroup(sw)
        writer.WriteEndElement()

    member private this.WriteSpectrumRef(spectrumReference: SpectrumReference) =

        if (spectrumReference.IsExternal) then
            this.WriteXmlAttribute("sourceFileRef", spectrumReference.SourceFileID, true)
            this.WriteXmlAttribute("externalSpectrumID", spectrumReference.SpectrumID, true)
        else
            this.WriteXmlAttribute("spectrumRef", spectrumReference.SpectrumID, true)

    member private this.WriteScan(scan: Scan) =

        writer.WriteStartElement("scan")
        //missing null check
        this.WriteSpectrumRef(scan.SpectrumReference)
        this.WriteParamGroup(scan)
        this.WriteList("scanWindowList", scan.ScanWindows, this.WriteScanWindow)
        writer.WriteEndElement()