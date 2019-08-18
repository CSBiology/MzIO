namespace MzIO.Thermo


open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Threading.Tasks
open ThermoFisher
open ThermoFisher.CommonCore
open ThermoFisher.CommonCore.RawFileReader
open ThermoFisher.CommonCore.RawFileReader.Writers
open ThermoFisher.CommonCore.Data
open ThermoFisher.CommonCore.Data.Business
open ThermoFisher.CommonCore.Data.FilterEnums
open ThermoFisher.CommonCore.Data.Interfaces
open ThermoFisher.CommonCore.BackgroundSubtraction
open ThermoFisher.CommonCore.MassPrecisionEstimator
open MzIO.Binary
open MzIO.Commons.Arrays
open MzIO.Commons.Arrays.MzIOArray
open MzIO.IO
open MzIO.Json
open MzIO.Model
open MzIO.MetaData.PSIMSExtension
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.UO.UO


[<Sealed>]
type ThermoPeaksArray(peakData:float [,], peakArraySize:int) =

    interface IMzIOArray<Peak1D> with

        member this.Length =

            peakArraySize
            
        member this.Item with get idx = new Peak1D(peakData.[1, idx], peakData.[0, idx])

    static member private Yield(peakData:float [,], peakArraySize:int) =

        [0..peakArraySize-1] 
        |> Seq.ofList
        |> Seq.map (fun idx -> new Peak1D(peakData.[1, idx], peakData.[0, idx]))

    interface IEnumerable<Peak1D> with
    
        member this.GetEnumerator() =

            (ThermoPeaksArray.Yield(peakData, peakArraySize)).GetEnumerator()

    interface System.Collections.IEnumerable with

        member this.GetEnumerator() =

            ThermoPeaksArray.Yield(peakData, peakArraySize).GetEnumerator() :> Collections.IEnumerator

type ThermoRawTransactionScope() =

    interface ITransactionScope with
    
        member this.Commit() =

            ()

        member this.Rollback() =

            ()

    interface IDisposable with

        member this.Dispose() =

            ()


[<Sealed>]
type ThermoRawFileReader(rawFilePath:string) =

    let rawFilePath =
        if String.IsNullOrWhiteSpace(rawFilePath) then
            failwith ((new ArgumentNullException("rawFilePath")).ToString())
        else
            if File.Exists(rawFilePath) = false then
                failwith ((new FileNotFoundException("Raw file not exists.")).ToString())
            else 
                rawFilePath
                //try
    //let rawFile = new MSFileReader_XRawfile() :> IXRawfile5
    //rawFile.Open(rawFilePath)
    let rawFile =
        RawFileReaderAdapter.FileFactory(rawFilePath)
        //ThermoFisher.CommonCore.Data.RawDataCreator(null, null, null)

    let x =        
        //rawfile.Open(rawFilePath)
        //rawfile.SetCurrentController(0, 1)
        rawFile.SelectInstrument(rawFile.GetInstrumentType(0), 1)

    let mutable disposed = false

    let startScanNo = rawFile.RunHeaderEx.FirstSpectrum
    let endScanNo = rawFile.RunHeaderEx.LastSpectrum

    //member private this.IsSet = rawFile.SelectInstrument(rawFile.GetInstrumentType(0), 1)

    member private this.model = MzIOJson.HandleExternalModelFile(this, ThermoRawFileReader.GetModelFilePath(rawFilePath))

                //with
                //    | :? Exception as ex ->
                //        failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

    member private this.rawFilePath = rawFilePath
        

//        private bool disposed = false;
//        private readonly MzLiteModel model;
//        private readonly string rawFilePath;
//        private readonly IXRawfile5 rawFile;
//        private readonly int startScanNo;
//        private readonly int endScanNo;

//        #region ThermoRawFileReader Members

    static member private GetModelFilePath(rawFilePath) =

        sprintf "%s%s" rawFilePath ".mzlitemodel"

    static member private GetFirstSpectrumNumber(rawFile:IRawDataPlus) =
        //let t,firstScanNumber = rawFile.GetFirstSpectrumNumber(& firstScanNumber)

        rawFile.RunHeaderEx.FirstSpectrum

    static member private GetLastSpectrumNumber(rawFile:IRawDataPlus) =

        rawFile.RunHeaderEx.LastSpectrum

    static member private IsCentroidSpectrum(rawFile:IRawDataPlus, scanNo:int) =

        rawFile.GetScanStatsForScanNumber(scanNo).IsCentroidScan
        //IsCentroidScan(scanNo, & isCentroidScan)
        
    static member private GetRetentionTime(rawFile:IRawDataPlus, scanNo:int) =

        rawFile.RetentionTimeFromScanNumber(scanNo)

    static member GetFilterString(rawFile:IRawDataPlus, scanNo:int) =
        
        rawFile.GetFilterForScanNumber(scanNo)
        
    static member private GetMSLevel(rawFile:IRawDataPlus, scanNo:int) =

        int (rawFile.GetFilterForScanNumber(scanNo).MSOrder)

    static member private GetIsolationWindowWidth(rawFile:IRawDataPlus, scanNo:int, msLevel:int) =

        rawFile.GetFilterForScanNumber(scanNo).GetIsolationWidth(0)

    static member private GetIsolationWindowTargetMz(rawFile:IRawDataPlus, scanNo:int, msLevel:int) =

        rawFile.GetScanEventForScanNumber(scanNo).GetReaction(0).PrecursorMass

    static member private GetPrecursorMz(rawFile:IRawDataPlus, scanNo:int, msLevel:int) =

        rawFile.GetScanEventForScanNumber(scanNo).GetReaction(0).PrecursorMass

    static member private GetCollisionEnergy(rawFile:IRawDataPlus, scanNo:int, msLevel:int) =
        
        rawFile.GetScanEventForScanNumber(scanNo).GetReaction(0).CollisionEnergy

    static member private GetChargeState(rawFile:IRawDataPlus, scanNo:int) =

        //Propably wrong index for charge state, needs further testing
        Convert.ToInt32(rawFile.GetTrailerExtraValue(scanNo, 9(*"Charge State:"*)))


    /// Parse the scan number from spectrum native id.
    /// Native id has the format: 'scan=[scanNumber]', i.e. for scan number 1000 the id is 'scan=1000'.
    member private this.ParseSpectrumId(id:string) =
        
        if id = null then 
            raise (new ArgumentNullException("id"))
        else
            let splitted = id.Split('=')
            if splitted.Length = 2 && splitted.[0].Equals("scan") then
                
                let scanNo = Int32.Parse(splitted.[1])

                if scanNo < startScanNo || scanNo > endScanNo then

                    raise (new IndexOutOfRangeException("Scan number out of range."))

                else
                    scanNo
            else
                raise (new FormatException("Wrong native id format in: " + id))


    /// Builds a spectrum id from scan number in format : 'scan=scanNumber'.
    static member private GetSpectrumID(scanNumber:int) =

        sprintf "scan=%s" (scanNumber.ToString())

    member this.RaiseDisposed() =

        if disposed then raise (new ObjectDisposedException(this.GetType().Name))

    interface IDisposable with

        member this.Dispose() =

            if disposed then ()
        
            if rawFile <> null then rawFile.Dispose()

            disposed <- true

    member private this.ReadMassSpectrum(scanNo:int) =

        rawFile.SelectInstrument(rawFile.GetInstrumentType(0), 1)
        
        this.RaiseDisposed()

        //try

        let spectrumID   = ThermoRawFileReader.GetSpectrumID(scanNo)
        let spectrum     = new MassSpectrum(spectrumID)

        // spectrum
        let msLevel = ThermoRawFileReader.GetMSLevel(rawFile, scanNo)
        spectrum.SetMsLevel(msLevel) |> ignore

        if (ThermoRawFileReader.IsCentroidSpectrum(rawFile, scanNo)) then 
            spectrum.SetCentroidSpectrum() |> ignore
        else
            spectrum.SetProfileSpectrum() |> ignore

        // scan
        let scan = new Scan()
        //Maybe orther type than Name needed
        scan.SetFilterString(ThermoRawFileReader.GetFilterString(rawFile, scanNo).ToString())
        scan.SetScanStartTime(ThermoRawFileReader.GetRetentionTime(rawFile, scanNo)).UO_Minute() |> ignore
        spectrum.Scans.Add(Guid.NewGuid().ToString(), scan)

        // precursor
        if (msLevel > 1) then 

            let precursor   = new Precursor()
            let isoWidth    = ThermoRawFileReader.GetIsolationWindowWidth(rawFile, scanNo, msLevel) * 0.5
            let targetMz    = ThermoRawFileReader.GetIsolationWindowTargetMz(rawFile, scanNo, msLevel)
            let precursorMz = ThermoRawFileReader.GetPrecursorMz(rawFile, scanNo, msLevel)
            let chargeState = ThermoRawFileReader.GetChargeState(rawFile, scanNo)

            precursor.IsolationWindow.SetIsolationWindowTargetMz(targetMz)      |> ignore
            precursor.IsolationWindow.SetIsolationWindowUpperOffset(isoWidth)   |> ignore
            precursor.IsolationWindow.SetIsolationWindowLowerOffset(isoWidth)   |> ignore

            let selectedIon = new SelectedIon()
            selectedIon.SetSelectedIonMz(precursorMz)   |> ignore
            selectedIon.SetChargeState(chargeState)     |> ignore

            precursor.SelectedIons.Add(Guid.NewGuid().ToString(), selectedIon)
            spectrum.Precursors.Add(Guid.NewGuid().ToString(), precursor)

            spectrum

        else
                
            spectrum

        //with
        //    | :? Exception as ex ->
        //        raise (new MzIOIOException(ex.Message, ex))

    member private this.ReadSpectrumPeaks(scanNo:int) =

        this.RaiseDisposed()

        try
            let scanStats       = rawFile.GetScanStatsForScanNumber(scanNo)
            let segmentedScan   = rawFile.GetSegmentedScanFromScanNumber(scanNo, scanStats)

            //maybe length of intensities needed
            let peaks = 

                let tmp = array2D [|segmentedScan.Positions; segmentedScan.Intensities|]

                new ThermoPeaksArray(tmp, segmentedScan.Positions.Length)

            let mutable pa = 
                new Peak1DArray(
                    BinaryDataCompressionType.NoCompression, BinaryDataType.Float32, 
                    BinaryDataType.Float32, peaks)
            pa

            //int peakArraySize = 0;
            //double controidPeakWith = 0;
            //object massList = null;
            //object peakFlags = null;

            //rawFile.GetMassListFromScanNum(
                //ref scanNo,
                //null, 1, 0, 0, 0,
                //ref controidPeakWith,
                //ref massList,
                //ref peakFlags,
                //ref peakArraySize);

            //Peak1DArray pa = new Peak1DArray(
                    //BinaryDataCompressionType.NoCompression,
                    //BinaryDataType.Float32,
                    //BinaryDataType.Float32);

            ////pa.Peaks = MzLiteArray.ToMzLiteArray(peaks);

            //pa.Peaks = new ThermoPeaksArray(peakData, peakArraySize);

            //return pa;

        with
            | :? Exception as ex ->
                raise (new MzIOIOException(ex.Message, ex))

//        #region IMzLiteDataReader Members

    interface IMzIODataReader with

        member this.ReadMassSpectrum(spectrumID:string) =

            let scanNumber = this.ParseSpectrumId(spectrumID)
            this.ReadMassSpectrum(scanNumber)

        member this.ReadMassSpectra(runID:string) =

            let scanNumbers = [startScanNo..endScanNo] |> Seq.ofList
            scanNumbers
            |> Seq.map (fun scanNumber -> this.ReadMassSpectrum(scanNumber))
             
        member this.ReadSpectrumPeaks(spectrumID:string) =

            let scanNo = this.ParseSpectrumId(spectrumID)
            this.ReadSpectrumPeaks(scanNo)

        member this.ReadMassSpectrumAsync(spectrumID:string) =

            Task<MassSpectrum>.Run(fun () -> (this :> IMzIODataReader).ReadMassSpectrum(spectrumID))

        member this.ReadSpectrumPeaksAsync(spectrumID:string) =

            Task<Peak1DArray>.Run(fun () -> (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID))

        member this.ReadChromatogram(chromatogramID:string) =

            raise (new NotSupportedException())

        member this.ReadChromatograms(runID:string) =

            Enumerable.Empty<Chromatogram>()

        member this.ReadChromatogramPeaks(chromatogramID:string) =

            raise (new NotSupportedException())        

        member this.ReadChromatogramAsync(chromatogramID:string) =

            raise (new NotSupportedException())

        member this.ReadChromatogramPeaksAsync(chromatogramID:string) =

            raise (new NotSupportedException())

    member this.ReadMassSpectrum(spectrumID:string) =

        (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

    member this.ReadMassSpectra(runID:string) =

        (this :> IMzIODataReader).ReadMassSpectra(runID)

    member this.ReadSpectrumPeaks(spectrumID:string) =

        (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

    member this.ReadMassSpectrumAsync(spectrumID:string) =

        (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

    member this.ReadSpectrumPeaksAsync(spectrumID:string) =

        (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

    member this.ReadChromatogram(chromatogramID:string) =

        (this :> IMzIODataReader).ReadChromatogram(chromatogramID)

    member this.ReadChromatograms(runID:string) =

        (this :> IMzIODataReader).ReadChromatograms(runID)

    member this.ReadChromatogramPeaks(chromatogramID:string) =

        (this :> IMzIODataReader).ReadChromatogramPeaks(chromatogramID)

    member this.ReadChromatogramAsync(chromatogramID:string) =

        (this :> IMzIODataReader).ReadChromatogramAsync(chromatogramID)

    member this.ReadChromatogramPeaksAsync(chromatogramID:string) =

        (this :> IMzIODataReader).ReadChromatogramPeaksAsync(chromatogramID)

    interface IMzIOIO with

        member this.CreateDefaultModel() =

            this.RaiseDisposed()

            let modelName   = Path.GetFileNameWithoutExtension(rawFilePath)
            let model       = new MzIOModel(modelName)

            let sampleName  = Path.GetFileNameWithoutExtension(rawFilePath)
            let sample      = new Sample("sample_1", sampleName)
            model.Samples.Add(sample.ID, sample)

            let run         = new Run("run_1", sampleName, rawFile.GetInstrumentData().Name)
            //run.Sample      = sample |> ignore
            model.Runs.Add(run.ID, run)

            model

        member this.Model =

            this.RaiseDisposed()

            this.model        

        member this.SaveModel() =

            this.RaiseDisposed()

            try
                MzIOJson.SaveJsonFile(this.model, ThermoRawFileReader.GetModelFilePath(rawFilePath))
            with
                | :? Exception as ex ->

                    raise (new MzIOIOException(ex.Message, ex))

        member this.BeginTransaction() =

            this.RaiseDisposed()

            new ThermoRawTransactionScope() :> ITransactionScope

    member this.Model =

        (this :> IMzIOIO).Model

    member this.getIsolationWindow = 
        
        rawFile.ScanEvents

    member this.getIsolationWindow2 = 
        
        rawFile.GetFilters()
