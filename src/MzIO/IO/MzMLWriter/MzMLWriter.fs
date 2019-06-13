namespace MzIO.IO.MzML

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

    member this.BeginMzML(model:MzLiteModel) =
        
        try
            this.EnterWriteState(MzMLWriteState.INITIAL, MzMLWriteState.MZML) |> ignore
            writer.WriteStartElement("mzML", "http://psi.hupo.org/ms/mzml")
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance")
            writer.WriteAttributeString("xsi", "schemaLocation", null, "http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.0.xsd")
            writer.WriteAttributeString("version", "1.1.0")

            this.WriteCvList()              

            //TODO scanSettingsList, also add to model

            //this.WriteFileDescription(model.FileDescription);
            //this.WriteList<DataProcessing>("dataProcessingList", model.DataProcessings, this.WriteDataProcessing, false);
            //this.WriteList("softwareList", model.Software, this.WriteSoftware, false);
            //this.WriteList("instrumentConfigurationList", model.Instruments, this.WriteInstrument, false);
            //this.WriteList("sampleList", model.Samples, this.WriteSample);

        with
            | :? Exception as ex ->
                currentWriteState <- MzMLWriteState.ERROR
                failwith ((new MzLiteIOException("Error writing mzml output file.", ex)).ToString())

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

    member private this.WriteList<'TItem>(elementName:string, list:DynamicObj, writeItem:Action<'TItem>, skipEmpty:bool) =

        let count = list.GetProperties false |> Seq.length
        if skipEmpty= true && count = 0 then ()
        else
            writer.WriteStartElement(elementName)
            this.WriteXmlAttribute("count", count.ToString(formatProvider))
            list.GetProperties false
            |> Seq.iter (fun item -> writeItem.Invoke(item.Value :?> 'TItem))

            writer.WriteEndElement()

    member private this.WriteList<'TItem>(elementName:string, list:DynamicObj, writeItem:Action<'TItem, int>, skipEmpty:bool) =

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

    member this.WriteFileDescription(fdesc:FileDescription) =

        writer.WriteStartElement("fileDescription");

        writer.WriteStartElement("fileContent");
        this.WriteParamGroup(fdesc.FileContent);
        writer.WriteEndElement();
        
        //this.WriteList("sourceFileList", fdesc.SourceFiles, this.WriteSourceFile)
            
        //if fdesc.Contact <> null then
        //    writer.WriteStartElement("contact");
        //    this.WriteParamGroup(fdesc.Contact)
        //    writer.WriteEndElement()
            
            writer.WriteEndElement()

    member this.WriteDataProcessing(dp:DataProcessing) =
        
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


//        public void EndMzML()
//        {
//            try
//            {
//                LeaveWriteState(MzMLWriteState.MZML, MzMLWriteState.INITIAL);
//                writer.WriteEndElement(); // </mzML>
//            }
//            catch (Exception ex)
//            {
//                currentWriteState = MzMLWriteState.ERROR;
//                throw new MzLiteIOException("Error writing mzml output file.", ex);
//            }
//        }

//        public void BeginRun(Run run)
//        {
//            try
//            {
//                if (run == null)
//                    throw new ArgumentNullException("run");

//                EnterWriteState(MzMLWriteState.MZML, MzMLWriteState.RUN);

//                writer.WriteStartElement("run");
//                WriteXmlAttribute("id", run.ID, true);

//                if (run.Sample != null)
//                {
//                    WriteXmlAttribute("sampleRef", run.Sample.ID, true);
//                }

//                if (run.DefaultInstrument != null)
//                {
//                    WriteXmlAttribute("defaultInstrumentConfigurationRef", run.DefaultInstrument.ID, true);
//                }

//                WriteParamGroup(run);
//            }
//            catch (Exception ex)
//            {
//                currentWriteState = MzMLWriteState.ERROR;
//                throw new MzLiteIOException("Error writing mzml output file.", ex);
//            }
//        }

//        public void EndRun()
//        {
//            try
//            {
//                LeaveWriteState(MzMLWriteState.RUN, MzMLWriteState.MZML);
//                writer.WriteEndElement(); // </run>
//            }
//            catch (Exception ex)
//            {
//                currentWriteState = MzMLWriteState.ERROR;
//                throw new MzLiteIOException("Error writing mzml output file.", ex);
//            }
//        }

//        public void BeginSpectrumList(int count)
//        {
//            try
//            {
//                if (count < 0)
//                    throw new ArgumentOutOfRangeException("count");

//                EnterWriteState(MzMLWriteState.RUN, MzMLWriteState.SPECTRUM_LIST);
//                writer.WriteStartElement("spectrumList");
//                WriteXmlAttribute("count", count.ToString(formatProvider));                
//            }
//            catch (Exception ex)
//            {
//                currentWriteState = MzMLWriteState.ERROR;
//                throw new MzLiteIOException("Error writing mzml output file.", ex);
//            }
//        }

//        public void EndSpectrumList()
//        {
//            try
//            {
//                LeaveWriteState(MzMLWriteState.SPECTRUM_LIST, MzMLWriteState.RUN);
//                writer.WriteEndElement(); // </spectrumList>
//            }
//            catch (Exception ex)
//            {
//                currentWriteState = MzMLWriteState.ERROR;
//                throw new MzLiteIOException("Error writing mzml output file.", ex);
//            }
//        }

//        public void WriteSpectrum(MassSpectrum ms, Peak1DArray peaks, int index)
//        {
//            try
//            {
//                if (ms == null)
//                    throw new ArgumentNullException("ms");
//                if (peaks == null)
//                    throw new ArgumentNullException("peaks");
//                if (index < 0)
//                    throw new ArgumentOutOfRangeException("idx");

//                EnsureWriteState(MzMLWriteState.SPECTRUM_LIST);

//                writer.WriteStartElement("spectrum");

//                WriteXmlAttribute("id", ms.ID, true);
//                WriteXmlAttribute("index", index.ToString(formatProvider), true);
//                WriteXmlAttribute("dataProcessingRef", ms.DataProcessingReference, false);
//                WriteXmlAttribute("sourceFileRef", ms.SourceFileReference, false);
//                WriteXmlAttribute("defaultArrayLength", peaks.Peaks.Length.ToString(formatProvider), true);

//                WriteParamGroup(ms);

//                WriteList("scanList", ms.Scans, WriteScan);
//                WriteList("precursorList", ms.Precursors, WritePrecursor);
//                WriteList("productList", ms.Products, WriteProduct);

//                WriteBinaryDataArrayList(peaks);

//                writer.WriteEndElement();
                              
//            }
//            catch (Exception ex)
//            {
//                currentWriteState = MzMLWriteState.ERROR;
//                throw new MzLiteIOException("Error writing mzml output file.", ex);
//            }
//        }                     

//        #region xml writing helper

//        private void WriteXmlAttribute(
//            string name, 
//            string value, 
//            bool required = false)
//        {
//            if (string.IsNullOrWhiteSpace(value))
//            {
//                if (required)
//                    throw new MzLiteIOException("Value required for xml attribute: " + name);
//                else
//                    return;
//            }
//            else
//            {
//                writer.WriteAttributeString(name, value);
//            }
//        }

//        private void WriteList<TItem>(
//            string elementName, 
//            ICollection<TItem> list,
//            Action<TItem> writeItem, 
//            bool skipEmpty = true)
//        {

//            int count = list.Count;

//            if (skipEmpty && count == 0)
//                return;

//            writer.WriteStartElement(elementName);
            
//            WriteXmlAttribute("count", count.ToString(formatProvider));
            
//            foreach (var item in list)
//            {
//                writeItem.Invoke(item);
//            }

//            writer.WriteEndElement();
//        }

//        #endregion

//        #region write states

//        private MzMLWriteState currentWriteState = MzMLWriteState.INITIAL;
//        private readonly HashSet<MzMLWriteState> consumedWriteStates = new HashSet<MzMLWriteState>();

//        private void EnsureWriteState(MzMLWriteState expectedWs)
//        {
//            if (currentWriteState == MzMLWriteState.ERROR)
//                throw new MzLiteIOException("Current write state is ERROR.");
//            if (currentWriteState == MzMLWriteState.CLOSED)
//                throw new MzLiteIOException("Current write state is CLOSED.");
//            if (currentWriteState != expectedWs)
//                throw new MzLiteIOException("Invalid write state: expected '{0}' but current is '{1}'.", expectedWs, currentWriteState);
//        }

//        private void EnterWriteState(MzMLWriteState expectedWs, MzMLWriteState newWs)
//        {
//            if (consumedWriteStates.Contains(newWs))
//                throw new MzLiteIOException("Can't reentering write state: '{0}'.", newWs);
//            EnsureWriteState(expectedWs);
//            currentWriteState = newWs;
//            consumedWriteStates.Add(newWs);
//        }

//        private void LeaveWriteState(MzMLWriteState expectedWs, MzMLWriteState newWs)
//        {
//            EnsureWriteState(expectedWs);
//            currentWriteState = newWs;
//        }

//        private enum MzMLWriteState
//        {
//            ERROR,
//            INITIAL,
//            CLOSED,
//            MZML,
//            RUN,
//            SPECTRUM_LIST,
//            CHROMATOGRAM_LIST
//        }

//        #endregion

//        #region binary data writing

//        private void WriteBinaryDataArrayList(Peak1DArray peaks)
//        {
//            writer.WriteStartElement("binaryDataArrayList");
//            WriteXmlAttribute("count", "2");

//            UserDescription mzParams = new UserDescription("mzParams");
//            mzParams
//                .SetMzArray()
//                .SetCompression(BinaryDataCompressionType.NoCompression)
//                .SetBinaryDataType(peaks.MzDataType);

//            double[] mzValues = peaks.Peaks.Select(x => x.Mz).ToArray();
//            WriteBinaryDataArray(mzValues, peaks.MzDataType, mzParams);

//            UserDescription intParams = new UserDescription("intParams");
//            intParams
//                .SetIntensityArray().NoUnit()
//                .SetCompression(BinaryDataCompressionType.NoCompression)
//                .SetBinaryDataType(peaks.IntensityDataType);

//            double[] intValues = peaks.Peaks.Select(x => x.Intensity).ToArray();
//            WriteBinaryDataArray(intValues, peaks.IntensityDataType, intParams);

//            writer.WriteEndElement();
//        }

//        private void WriteBinaryDataArray(double[] values, BinaryDataType binaryDataType, UserDescription pars)
//        {

//            BinaryDataEncoder encoder = new BinaryDataEncoder(values.Length * 8);
//            string base64 = encoder.EncodeBase64(values, BinaryDataCompressionType.NoCompression, binaryDataType);
//            int len = base64.Length;

//            writer.WriteStartElement("binaryDataArray");
//            WriteXmlAttribute("encodedLength", len.ToString(formatProvider));

//            WriteParamGroup(pars);

//            writer.WriteStartElement("binary");
//            writer.WriteString(base64);
//            writer.WriteEndElement();

//            writer.WriteEndElement();
//        }  

//        #endregion

//        #region model writing

        
//        private void WriteSourceFile(SourceFile sf)
//        {
//            writer.WriteStartElement("sourceFile");

//            WriteXmlAttribute("id", sf.ID, true);
//            WriteXmlAttribute("name", sf.Name, true);
//            WriteXmlAttribute("location", sf.Location, true);

//            WriteParamGroup(sf);

//            writer.WriteEndElement();
//        }

//        private void WriteSoftware(Software sw)
//        {
//            writer.WriteStartElement("software");
//            WriteXmlAttribute("id", sw.ID, true);
//            WriteXmlAttribute("version", "not supported");
//            WriteParamGroup(sw);
//            writer.WriteEndElement();
//        }

//        private void WriteInstrument(Instrument instr)
//        {
//            writer.WriteStartElement("instrumentConfiguration");
//            WriteXmlAttribute("id", instr.ID, true);
//            WriteParamGroup(instr);

//            if (instr.Software != null)
//            {
//                writer.WriteStartElement("softwareRef");
//                WriteXmlAttribute("ref", instr.Software.ID, true);
//                writer.WriteEndElement();
//            }
                
//            // TODO scanSettingsRef            

//            WriteList("componentList", instr.Components, WriteComponent, true);

//            writer.WriteEndElement();
//        }

//        private void WriteComponent(Component comp, int index)
//        {
//            string elemName;

//            if (comp is SourceComponent)
//                elemName = "sourceComponent";
//            if (comp is DetectorComponent)
//                elemName = "detectorComponent";
//            if (comp is AnalyzerComponent)
//                elemName = "analyzerComponent";
//            else
//                return;

//            writer.WriteStartElement(elemName);
//            WriteXmlAttribute("order", index.ToString(formatProvider));
//            WriteParamGroup(comp);
//            writer.WriteEndElement();
//        }
        
//        private void WriteSample(Sample sample)
//        {
//            writer.WriteStartElement("sample");

//            WriteXmlAttribute("id", sample.ID, true);
//            WriteXmlAttribute("name", sample.Name, false);

//            WriteParamGroup(sample);

//            writer.WriteEndElement();
//        }

//        private void WriteProduct(Product p)
//        {
//            writer.WriteStartElement("product");
//            if (p.IsolationWindow != null)
//                WriteIsolationWindow(p.IsolationWindow);
//            writer.WriteEndElement();
//        }
        
//        private void WritePrecursor(Precursor pc)
//        {
//            writer.WriteStartElement("precursor");

//            if (pc.SpectrumReference != null)
//                WriteSpectrumRef(pc.SpectrumReference);

//            WriteList("selectedIonList", pc.SelectedIons, WriteSelectedIon);

//            if (pc.IsolationWindow != null)
//            {
//                WriteIsolationWindow(pc.IsolationWindow);
//            }

//            if (pc.Activation != null)
//            {
//                writer.WriteStartElement("activation");
//                WriteParamGroup(pc.Activation);
//                writer.WriteEndElement();
//            }

//            writer.WriteEndElement();
//        }

//        private void WriteIsolationWindow(IsolationWindow isolationWindow)
//        {            
//            writer.WriteStartElement("isolationWindow");
//            WriteParamGroup(isolationWindow);
//            writer.WriteEndElement();
//        }

//        private void WriteSelectedIon(SelectedIon ion)
//        {
//            writer.WriteStartElement("selectedIon");
//            WriteParamGroup(ion);
//            writer.WriteEndElement();            
//        }

//        private void WriteScanWindow(ScanWindow sw)
//        {
//            writer.WriteStartElement("scanWindow");
//            WriteParamGroup(sw);
//            writer.WriteEndElement();
//        }

//        private void WriteScan(Scan scan)
//        {
//            writer.WriteStartElement("scan");
//            if (scan.SpectrumReference != null)
//                WriteSpectrumRef(scan.SpectrumReference);
//            WriteParamGroup(scan);
//            WriteList("scanWindowList", scan.ScanWindows, WriteScanWindow);
//            writer.WriteEndElement();
//        }

//        private void WriteSpectrumRef(SpectrumReference spectrumReference)
//        {
//            if (spectrumReference.IsExternal)
//            {
//                WriteXmlAttribute("sourceFileRef", spectrumReference.SourceFileID, true);
//                WriteXmlAttribute("externalSpectrumID", spectrumReference.SpectrumID, true);
//            }
//            else
//            {
//                WriteXmlAttribute("spectrumRef", spectrumReference.SpectrumID, true);
//            }
//        } 

//        #endregion

//        #region IDisposable Members

//        public void Dispose()
//        {
//            Close();
//        }

//        #endregion
//    }

//}
