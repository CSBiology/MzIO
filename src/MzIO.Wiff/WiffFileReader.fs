namespace MzIO.Wiff


open System
open System.IO
open System.Linq
open System.Threading.Tasks
open System.Collections.Generic
open System.Text.RegularExpressions
open Clearcore2.Data.AnalystDataProvider
open Clearcore2.Data.DataAccess.SampleData
open MzIO.Json
open MzIO.Binary
open MzIO.IO
open MzIO.Model
open MzIO.MetaData.UO.UO
open MzIO.MetaData.PSIMSExtension
open MzIO.Commons.Arrays


//regular expression to check for repeated occurrences of words in a string
//retrieves sample, experiment and scan ID
//put in an extra module for improved performance
module Regex =

    let regexID =
        new Regex(@"sample=(\d+) experiment=(\d+) scan=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

    let regexSampleIndex =
        new Regex(@"sample=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

    //let mutable sampleIndex     = 0
    //let mutable experimentIndex = 0
    //let mutable scanIndex       = 0

open Regex

type WiffPeaksArray(wiffSpectrum:Clearcore2.Data.MassSpectrum) =

    //let wiffSpectrum = new Clearcore2.Data.MassSpectrum(

    interface IMzLiteArray<Peak1D> with

        member this.Length = wiffSpectrum.NumDataPoints

        //potential error source
        member this.Item
            with get (idx:int) =
                if (idx < 0 || idx >= this.Length) then
                    failwith ((new IndexOutOfRangeException()).ToString())
                else
                    new Peak1D(wiffSpectrum.GetYValue(idx), wiffSpectrum.GetXValue(idx))

    member this.Length =

        (this :> IMzLiteArray<Peak1D>).Length

    member this.Item(idx:int) =

        (this :> IMzLiteArray<Peak1D>).Item(idx)

    static member private Yield(wiffSpectrum:Clearcore2.Data.MassSpectrum) =

        let spectrum = Array.create wiffSpectrum.NumDataPoints (new Peak1D())

        for i=0 to wiffSpectrum.NumDataPoints-1 do
            spectrum.[i] <- new Peak1D(wiffSpectrum.GetYValue(i), wiffSpectrum.GetXValue(i))
        spectrum

    interface IEnumerable<Peak1D> with

        member this.GetEnumerator() =
            WiffPeaksArray.Yield(wiffSpectrum).AsEnumerable<Peak1D>().GetEnumerator()

    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            WiffPeaksArray.Yield(wiffSpectrum).GetEnumerator()

    member this.GetEnumerator() =

        (this :> IEnumerable<Peak1D>).GetEnumerator()

    //member this.Peak1D (idx:int) =
    //    if (idx < 0 || idx >= this.Length) then
    //        failwith ((new IndexOutOfRangeException()).ToString())
    //    else new Peak1D(wiffSpectrum.GetYValue(idx), wiffSpectrum.GetXValue(idx))

type WiffTransactionScope() =

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

type WiffFileReader(dataProvider:AnalystWiffDataProvider, disposed:Boolean, wiffFilePath:string, licenseFilePath:string) =

    //let licenseFilePath = defaultArg licenseFilePath (sprintf @"%s"(__SOURCE_DIRECTORY__ + "\License\Clearcore2.license.xml"))

    let mutable dataProvider    = dataProvider

    let mutable batch           = AnalystDataProviderFactory.CreateBatch(wiffFilePath, dataProvider)

    let mutable disposed        = disposed

    //let parseHelper = new ParseHelper()

    let wiffFileCheck =

        if not (File.Exists(wiffFilePath)) then
            raise (FileNotFoundException("Wiff file does not exist."))
        if (wiffFilePath.Trim() = "") then
            raise (ArgumentNullException("wiffFilePath"))

    let licenseFileCheck =
        
        if (licenseFilePath.Trim() = "") then
            raise (ArgumentNullException("licenseFilePath"))
        else 
            WiffFileReader.ReadWiffLicense(licenseFilePath)

    //let mutable wiffFilePath =
    //    if wiffFilePath<>"wiffFilePath" then
    //        if not (File.Exists(wiffFilePath)) then
    //            failwith  ((new FileNotFoundException("Wiff file not exists.")).ToString())
    //        else
    //            match wiffFilePath with
    //            | null  -> failwith (ArgumentNullException("WiffFilePath").ToString())
    //            | ""    -> failwith (ArgumentNullException("WiffFilePath").ToString())
    //            | " "   -> failwith (ArgumentNullException("WiffFilePath").ToString())
    //            |   _   -> wiffFilePath
    //    else wiffFilePath

    //let mutable licenseFilePath =
    //    match licenseFilePath with
    //    | null  -> failwith (ArgumentNullException("LicenseFilePath").ToString())
    //    | ""    -> failwith (ArgumentNullException("LicenseFilePath").ToString())
    //    | " "   -> failwith (ArgumentNullException("LicenseFilePath").ToString())
    //    |   _   -> WiffFileReader.ReadWiffLicense(licenseFilePath)

    new(wiffFilePath:string, ?licenseFilePath:string) =
        let licenseFilePath = defaultArg licenseFilePath (sprintf @"%s"(__SOURCE_DIRECTORY__ + "\License\Clearcore2.license.xml"))
        new WiffFileReader
            (
                new AnalystWiffDataProvider(true), false, wiffFilePath, licenseFilePath
            )

    //new() = new WiffFileReader(new AnalystWiffDataProvider(), null, false, @"wiffFilePath", @"licenseFilePath", new MzLiteModel())

    //static member private regexID =
    //    new Regex(@"sample=(\d+) experiment=(\d+) scan=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

    //static member private regexSampleIndex =
    //    new Regex(@"sample=(\d+)", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

    //changed sampleIndex to byref from mutable variable
    static member private ParseByRunID(runID:string, sampleIndex:byref<int>) =

        let match' = regexSampleIndex.Match(runID)
        if match'.Success=true then

            //try
            let groups = match'.Groups
            sampleIndex <- int (groups.[1].Value)

            //with
            //    | :? FormatException ->
            //        failwith ((new FormatException("Error parsing wiff sample index: " + runID)).ToString())
        else
            failwith ((new FormatException("Not a valid wiff sample index format: " + runID)).ToString())

    static member private ParseBySpectrumID(spectrumID:string, sampleIndex:byref<int>, experimentIndex:byref<int>, scanIndex:byref<int>) =

        let match' = regexID.Match(spectrumID)

        if match'.Success then

            //try
            //This part  causes the slowness of the whole funtion.
            let groups = match'.Groups
            sampleIndex     <- (int (groups.[1].Value))
            experimentIndex <- (int (groups.[2].Value))
            scanIndex       <- (int (groups.[3].Value))

            //with
            //    | :? FormatException -> failwith ((new FormatException("Error parsing wiff spectrum id format: " + spectrumID)).ToString())
        else
            failwith ((new FormatException("Not a valid wiff spectrum id format: " + spectrumID)).ToString())

    static member GetIsolationWindow(exp:MSExperiment, isoWidth:byref<double>, targetMz:byref<double>) =

        let mutable mri = null

        let mutable mr  = exp.Details.MassRangeInfo

        isoWidth <- double 0.

        targetMz <- double 0.

        if mr.Length>0 then

            mri <- mr.[0] :?> FragmentBasedScanMassRange

            isoWidth <- mri.IsolationWindow * (double 0.5)

            targetMz <- double mri.FixedMasses.[0]

            mri <> null
        else
            false

    static member private ToSpectrumID(sampleIndex:int, experimentIndex:int, scanIndex:int) =

        String.Format("sample={0} experiment={1} scan={2}", sampleIndex, experimentIndex, scanIndex)

    static member private GetSpectrum(batch:Batch, sample:MassSpectrometerSample, msExp:MSExperiment, sampleIndex:int, experimentIndex:int, scanIndex:int) =

        let mutable wiffSpectrum    = msExp.GetMassSpectrumInfo(scanIndex)
        let mutable mzLiteSpectrum  = new MassSpectrum(WiffFileReader.ToSpectrumID(sampleIndex, experimentIndex, scanIndex))

        // spectrum

        mzLiteSpectrum.SetMsLevel(wiffSpectrum.MSLevel) |> ignore

        if wiffSpectrum.CentroidMode=true then
            mzLiteSpectrum.SetCentroidSpectrum()        |> ignore
        else
            mzLiteSpectrum.SetProfileSpectrum()         |> ignore

        // scan
        let mutable scan = new Scan()
        scan.SetScanStartTime(wiffSpectrum.StartRT).UO_Minute |> ignore
        mzLiteSpectrum.Scans.Add(scan)

        // precursor
        let precursor = new Precursor()
        let mutable isoWidth = double 0
        let mutable targetMz = double 0

        if wiffSpectrum.IsProductSpectrum then
            if WiffFileReader.GetIsolationWindow(wiffSpectrum.Experiment, & isoWidth, & targetMz)=true
            then
                precursor.IsolationWindow.SetIsolationWindowTargetMz(targetMz)      |> ignore
                precursor.IsolationWindow.SetIsolationWindowUpperOffset(isoWidth)   |> ignore
                precursor.IsolationWindow.SetIsolationWindowLowerOffset(isoWidth)   |> ignore
            let selectedIon = new SelectedIon()
            selectedIon.SetSelectedIonMz(wiffSpectrum.ParentMZ)                     |> ignore
            selectedIon.SetChargeState(wiffSpectrum.ParentChargeState)              |> ignore
            precursor.SelectedIons.Add(selectedIon)
            precursor.Activation.SetCollisionEnergy(wiffSpectrum.CollisionEnergy)   |> ignore
            mzLiteSpectrum.Precursors.Add(precursor)
            mzLiteSpectrum

        else
            mzLiteSpectrum

    static member private ToRunID(sample: int) =
            String.Format("sample={0}", sample)

    static member Yield(batch:Batch, sampleIndex:int) =

        use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        (
            let tmp =
                seq{
                    for experimentIndex= 0 to sample.ExperimentCount-1 do
                        let mutable msExp = sample.GetMSExperiment(experimentIndex)
                        for scanIndex = 0 to msExp.Details.NumberOfScans-1 do
                            yield WiffFileReader.GetSpectrum(batch, sample, msExp, sampleIndex, experimentIndex, scanIndex)
                }
            tmp.AsEnumerable<MassSpectrum>()
        )

        //let mutable massSpectra = []
        //use sample = batch.GetSample(sampleIndex).MassSpectrometerSample
        //(
        //for experimentIndex=0 to sample.ExperimentCount-1 do
        //    use msExp = sample.GetMSExperiment(experimentIndex)
        //    (
        //        for scanIndex=0 to msExp.Details.NumberOfScans-1 do
        //            massSpectra <- WiffFileReader.GetSpectrum(batch, sample, msExp,sampleIndex, experimentIndex, scanIndex) :: massSpectra
        //    )
        //)
        //(List.rev massSpectra).AsEnumerable<PeakList.MassSpectrum>()

    member private this.RaiseDisposed() =

        if disposed = true then printfn "%s" ((new ObjectDisposedException(this.GetType().Name)).ToString())
        else ()

    interface IDisposable with

        member this.Dispose() =
            if disposed = true then
                ()
            else
                if dataProvider<>null then
                    dataProvider.Close()
                disposed <- true

    member this.Dispose() =

        (this :> IDisposable).Dispose()

    //potentiel failure due to exception
    interface IMzLiteIO with

        member this.BeginTransaction() =
            this.RaiseDisposed()
            new WiffTransactionScope() :> ITransactionScope

        member this.CreateDefaultModel() =

            this.RaiseDisposed()

            let model = new MzLiteModel(batch.Name)

            let sampleNames = batch.GetSampleNames()

            for sampleIdx=0 to sampleNames.Length-1 do
                use wiffSample = batch.GetSample(sampleIdx)
                (
                    let sampleName = sampleNames.[sampleIdx].Trim()
                    let sampleID = WiffFileReader.ToRunID(sampleIdx)
                    let msSample = wiffSample.MassSpectrometerSample
                    let mzLiteSample =
                        new Sample
                            (
                                sampleID,
                                sampleName
                            )
                    model.Samples.Add(mzLiteSample.ID, mzLiteSample)
                    let softwareID = wiffSample.Details.SoftwareVersion.Trim()
                    let software = new Software(softwareID)
                    (
                        if model.Softwares.TryGetItemByKey(softwareID, software)=false then
                            model.Softwares.Add(software.ID, software)
                    )
                    let instrumentID = msSample.InstrumentName.Trim()
                    let instrument = new Instrument(instrumentID)
                    (
                        if model.Instruments.TryGetItemByKey(instrumentID, instrument)=false then
                            model.Instruments.Add(instrument.ID, instrument)
                    )
                    let runID = String.Format("sample={0}", sampleIdx)
                    let run = new Run(runID, mzLiteSample, instrument)
                    model.Runs.Add(run.ID, run)

                )
            model

        //Function for Json to save seralzed information
        member this.SaveModel() =

            MzIOJson.SaveJsonFile(this.Model, WiffFileReader.GetModelFilePath(wiffFilePath))

        member this.Model =
            this.RaiseDisposed()
            this.Model

    member this.BeginTransaction() =

        (this :> IMzLiteIO).BeginTransaction()

    member this.CreateDefaultModel() =

        (this :> IMzLiteIO).CreateDefaultModel()

    member this.SaveModel() =

        (this :> IMzLiteIO).SaveModel()

    member this.Model =
        let model = MzIOJson.HandleExternalModelFile(this, WiffFileReader.GetModelFilePath(wiffFilePath))
        model

    //potentiel failure due to exception
    interface IMzLiteDataReader with

        member this.ReadMassSpectra(runID:string) =
            this.RaiseDisposed()
            //let mutable  ex = Exception()
            //try
            let mutable sampleIndex = 0
            WiffFileReader.ParseByRunID(runID, & sampleIndex)
            //let sampleIndex = WiffFileReader.GetSampleIndex runID
            WiffFileReader.Yield(batch, sampleIndex)
            //with
            //    | :? Exception -> failwith (MzLiteIOException.MzLiteIOException(ex.Message, ex).ToString())

        //potentiel failure due to exception due to the use of use
        member this.ReadMassSpectrum(spectrumID:string) =
            this.RaiseDisposed()
            //let mutable  ex = Exception()
            //try
            let mutable sampleIndex     = 0
            let mutable experimentIndex = 0
            let mutable scanIndex       = 0
            WiffFileReader.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
            use sample      = batch.GetSample(sampleIndex).MassSpectrometerSample
            use msExp       = sample.GetMSExperiment(experimentIndex)
            (WiffFileReader.GetSpectrum(batch, sample, msExp, sampleIndex, experimentIndex, scanIndex))
            //with
            //    | :? Exception -> failwith (MzLiteIOException.MzLiteIOException(ex.Message, ex).ToString())

        member this.ReadSpectrumPeaks(spectrumID:string) =
            this.RaiseDisposed()
            //let mutable  ex = Exception()
            //try
            let mutable sampleIndex     = 0
            let mutable experimentIndex = 0
            let mutable scanIndex       = 0

            WiffFileReader.ParseBySpectrumID(spectrumID, & sampleIndex, & experimentIndex, & scanIndex)
            use sample      = batch.GetSample(sampleIndex).MassSpectrometerSample
            use msExp       = sample.GetMSExperiment(experimentIndex)
            let ms          = msExp.GetMassSpectrum(scanIndex)
            let mutable pa = new Peak1DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64)

            pa.Peaks <- new WiffPeaksArray(ms)
            pa
            //with
            //    | :? Exception
            //        -> failwith (MzLiteIOException.MzLiteIOException(ex.Message, ex).ToString())

        member this.ReadMassSpectrumAsync(spectrumID:string) =        
            //let tmp = this :> IMzLiteDataReader
            //async
            //    {
            //        return tmp.ReadMassSpectrum(spectrumID)
            //    }

            Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSpectrum(spectrumID))

        member this.ReadSpectrumPeaksAsync(spectrumID:string) =            
            //let tmp = this :> IMzLiteDataReader
            //async
            //    {
            //        return tmp.ReadSpectrumPeaks(spectrumID)
            //    }

            Task<Peak1DArray>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))


        member this.ReadChromatograms(runID:string) =
            Enumerable.Empty<Chromatogram>()

        member this.ReadChromatogram(runID:string) =
            try
                raise (new NotSupportedException())
            with
                | :? Exception as ex-> 
                    raise (MzLiteIOException(ex.Message, ex))

        member this.ReadChromatogramPeaks(runID:string) =
            try
                raise ((new NotSupportedException()))
            with
                | :? Exception as ex -> 
                    raise (MzLiteIOException(ex.Message, ex))

        member this.ReadChromatogramAsync(runID:string) =
            try
                raise ((new NotSupportedException()))
            with
                | :? Exception as ex -> 
                    raise (MzLiteIOException(ex.Message, ex))

        member this.ReadChromatogramPeaksAsync(runID:string) =
            try
                raise ((new NotSupportedException()))
            with
                | :? Exception as ex -> 
                    raise (MzLiteIOException(ex.Message, ex))

    member this.ReadMassSpectra(runID:string)               =

        (this :> IMzLiteDataReader).ReadMassSpectra(runID)

    member this.ReadMassSpectrum(spectrumID:string)         =

        (this :> IMzLiteDataReader).ReadMassSpectrum(spectrumID)

    member this.ReadSpectrumPeaks(spectrumID:string)        =

        (this :> IMzLiteDataReader).ReadSpectrumPeaks(spectrumID)

    member this.ReadMassSpectrumAsync(spectrumID:string)    =

        (this :> IMzLiteDataReader).ReadMassSpectrumAsync(spectrumID)

    member this.ReadSpectrumPeaksAsync(spectrumID:string)   =

        (this :> IMzLiteDataReader).ReadSpectrumPeaksAsync(spectrumID)

    member this.ReadChromatograms(runID:string)             =

        (this :> IMzLiteDataReader).ReadChromatograms(runID)

    member this.ReadChromatogramPeaks(runID:string)         =

        (this :> IMzLiteDataReader).ReadChromatogramPeaks(runID)

    member this.ReadChromatogramAsync(runID:string)         =

        (this :> IMzLiteDataReader).ReadChromatogramAsync(runID)

    member this.ReadChromatogramPeaksAsync(runID:string)    =

        (this :> IMzLiteDataReader).ReadChromatogramPeaksAsync(runID)

    //potential error source because text isn't splitted into several keys
    static member ReadWiffLicense(licensePath:string) =
        if not (File.Exists(licensePath)) then
            raise  (new FileNotFoundException("Missing Clearcore2 license file: " + licensePath))
        let text = File.ReadAllText(licensePath)
        Clearcore2.Licensing.LicenseKeys.Keys <- [|text|]

    static member GetModelFilePath(wiffFilePath) =

        sprintf "%s%s" wiffFilePath ".mzlitemodel"