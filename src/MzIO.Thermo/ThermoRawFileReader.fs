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


/// Special peakArray for PeakArrays of thermo Raw files.
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
    
        /// Does Nothing.
        member this.Commit() = ()

        /// Does Nothing.
        member this.Rollback() = ()

    interface IDisposable with

        /// Does Nothing.
        member this.Dispose() = ()

/// Reader for Thermo RAW files.
[<Sealed>]
type ThermoRawFileReader(rawFilePath:string) =

    // Checks whether path is empty and file exists or not.
    let rawFilePath =
        if String.IsNullOrWhiteSpace(rawFilePath) then
            failwith ((new ArgumentNullException("rawFilePath")).ToString())
        else
            if File.Exists(rawFilePath) = false then
                failwith ((new FileNotFoundException("Raw file not exists.")).ToString())
            else 
                rawFilePath

    // Init IRawDataPlus itnerface to access information of the RAW file.
    let rawFile = RawFileReaderAdapter.FileFactory(rawFilePath)

    //Select instrument in order to gain information that is connected to this instrument with the follow up methods.
    //let x =        
        //rawfile.Open(rawFilePath)
        //rawfile.SetCurrentController(0, 1)
    do rawFile.SelectInstrument(rawFile.GetInstrumentType(0), 1)

    let mutable disposed = false

    // Get id of first spectrum.
    let startScanNo = rawFile.RunHeaderEx.FirstSpectrum
    // Get id of last spectrum.
    let endScanNo = rawFile.RunHeaderEx.LastSpectrum

    member private this.model = MzIOJson.HandleExternalModelFile(this, ThermoRawFileReader.GetModelFilePath(rawFilePath))

    // #region ThermoRawFileReader Members
    static member private GetModelFilePath(rawFilePath) = sprintf "%s%s" rawFilePath ".mzlitemodel"

    static member private GetFirstSpectrumNumber(rawFile:IRawDataPlus) =
        rawFile.RunHeaderEx.FirstSpectrum

    static member private GetLastSpectrumNumber(rawFile:IRawDataPlus) =
        rawFile.RunHeaderEx.LastSpectrum

    /// Checks whether spectrum is centroided or not.
    static member private IsCentroidSpectrum(rawFile:IRawDataPlus, scanNo:int) =
        rawFile.GetScanStatsForScanNumber(scanNo).IsCentroidScan
        
    /// Gets retentionTime of the spectrum.
    static member private GetRetentionTime(rawFile:IRawDataPlus, scanNo:int) =
        rawFile.RetentionTimeFromScanNumber(scanNo)

    /// Gets the scanning method for a spectrum.
    static member GetFilterString(rawFile:IRawDataPlus, scanNo:int) =        
        rawFile.GetFilterForScanNumber(scanNo)
        
    /// Gets MSLEvel of spectrum.
    static member private GetMSLevel(rawFile:IRawDataPlus, scanNo:int) =
        int (rawFile.GetFilterForScanNumber(scanNo).MSOrder)

    /// Ges IsolationWindowWith of Spectrum.
    static member private GetIsolationWindowWidth(rawFile:IRawDataPlus, scanNo:int, msLevel:int) =
        rawFile.GetFilterForScanNumber(scanNo).GetIsolationWidth(0)

    /// Gets target M/Z of isolationWindowWith of spectrum.
    static member private GetIsolationWindowTargetMz(rawFile:IRawDataPlus, scanNo:int, msLevel:int) =
        rawFile.GetScanEventForScanNumber(scanNo).GetReaction(0).PrecursorMass

    /// Gets precursor M/Z of isolationWindowWith of spectrum.
    static member private GetPrecursorMz(rawFile:IRawDataPlus, scanNo:int, msLevel:int) =
        rawFile.GetScanEventForScanNumber(scanNo).GetReaction(0).PrecursorMass

    /// Get CollisionEnergy of spectrum.
    static member GetCollisionEnergy(rawFile:IRawDataPlus, scanNo:int, msLevel:int) =        
        rawFile.GetScanEventForScanNumber(scanNo).GetReaction(0).CollisionEnergy

    static member private GetChargeState(rawFile:IRawDataPlus, scanNo:int) =
        // Propably wrong index for charge state, needs further testing
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

    /// Checks whether connection is disposed or not and fails when it is.
    member this.RaiseDisposed() =

        if disposed then raise (new ObjectDisposedException(this.GetType().Name))

    interface IDisposable with

        /// Sets disposed to true and disables work with this instance of the ThermoRawFileReader.
        member this.Dispose() =

            if disposed then ()
        
            if rawFile <> null then rawFile.Dispose()

            disposed <- true

    /// Gets MassSpectrum of RAW file.
    member private this.ReadMassSpectrum(scanNo:int) =

        //Select instrument in order to gain information that is connected to this instrument with the follow up methods.
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
        // Maybe orther type than Name needed
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

    /// Gets peaks of spectrum of RAW file.
    member private this.ReadSpectrumPeaks(scanNo:int) =

        this.RaiseDisposed()

        try
            let scanStats       = rawFile.GetScanStatsForScanNumber(scanNo)
            let segmentedScan   = rawFile.GetSegmentedScanFromScanNumber(scanNo, scanStats)

            // maybe length of intensities needed
            let peaks = 

                let tmp = array2D [|segmentedScan.Positions; segmentedScan.Intensities|]

                new ThermoPeaksArray(tmp, segmentedScan.Positions.Length)

            new Peak1DArray( BinaryDataCompressionType.NoCompression, BinaryDataType.Float32, BinaryDataType.Float32, peaks)

        with
            | :? Exception as ex ->
                raise (new MzIOIOException(ex.Message, ex))

    // #region IMzLiteDataReader Members
    interface IMzIODataReader with

        /// Read mass spectrum of RAW file.
        member this.ReadMassSpectrum(spectrumID:string) =
            
            // Gets scanNumber associacted with spectrumID.
            let scanNumber = this.ParseSpectrumId(spectrumID)
            this.ReadMassSpectrum(scanNumber)

        /// Read mass spectra of RAW file.
        member this.ReadMassSpectra(runID:string) =

            // First and last scanNumber are called upon when file is created, so everything between can be generated.
            let scanNumbers = [startScanNo..endScanNo] |> Seq.ofList
            scanNumbers
            |> Seq.map (fun scanNumber -> this.ReadMassSpectrum(scanNumber))
             
        /// Read peaks of mass spectrum of RAW file.
        member this.ReadSpectrumPeaks(spectrumID:string) =

            let scanNo = this.ParseSpectrumId(spectrumID)
            this.ReadSpectrumPeaks(scanNo)

        /// Read mass spectrum of RAW file asynchronously.
        member this.ReadMassSpectrumAsync(spectrumID:string) =
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadMassSpectrum(spectrumID)
                }
            //Task<MassSpectrum>.Run(fun () -> (this :> IMzIODataReader).ReadMassSpectrum(spectrumID))

        /// Read peaks of mass spectrum of RAW file asynchronously.
        member this.ReadSpectrumPeaksAsync(spectrumID:string) =
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadSpectrumPeaks(spectrumID)
                }
            //Task<Peak1DArray>.Run(fun () -> (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID))

        /// Read chromatogram of RAW file.
        member this.ReadChromatogram(chromatogramID:string) =

            raise (new NotSupportedException())

        /// Read chromatograms of RAW file.
        member this.ReadChromatograms(runID:string) =

            Enumerable.Empty<Chromatogram>()

        /// Read peaks of chromatogram of RAW file.
        member this.ReadChromatogramPeaks(chromatogramID:string) =

            raise (new NotSupportedException())        

        /// Read chromatogram of RAW file asynchronously.
        member this.ReadChromatogramAsync(chromatogramID:string) =

            raise (new NotSupportedException())

        /// Read peaks of chromatogram of RAW file asynchronously.
        member this.ReadChromatogramPeaksAsync(chromatogramID:string) =

            raise (new NotSupportedException())

    /// Read all mass spectra of one run of MzSQL.
    member this.ReadMassSpectra(runID: string) =
            (this :> IMzIODataReader).ReadMassSpectra(runID)

    /// Read mass spectrum of MzSQL.
    member this.ReadMassSpectrum(spectrumID: string) =
        (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

    /// Read peaks of mass spectrum of MzSQL.
    member this.ReadSpectrumPeaks(spectrumID: string) =
        (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

    /// Read mass spectrum of MzSQL asynchronously.
    member this.ReadMassSpectrumAsync(spectrumID:string) =        
        (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

    /// Read peaks of mass spectrum of MzSQL asynchronously.
    member this.ReadSpectrumPeaksAsync(spectrumID:string) =            
        (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

    /// Read all chromatograms of one run of MzSQL.
    member this.ReadChromatograms(runID: string) =
        (this :> IMzIODataReader).ReadChromatograms(runID)

    /// Read chromatogram of MzSQL.
    member this.ReadChromatogram(chromatogramID: string) =
        (this :> IMzIODataReader).ReadChromatogram(chromatogramID)

    /// Read peaks of chromatogram of MzSQL.
    member this.ReadChromatogramPeaks(chromatogramID: string) =
        (this :> IMzIODataReader).ReadChromatogramPeaks(chromatogramID)

    /// Read chromatogram of MzSQL asynchronously.
    member this.ReadChromatogramAsync(chromatogramID:string) =
        (this :> IMzIODataReader).ReadChromatogramAsync(chromatogramID)
        
    /// Read peaks of chromatogram of MzSQL asynchronously.
    member this.ReadChromatogramPeaksAsync(chromatogramID:string) =
        (this :> IMzIODataReader).ReadChromatogramPeaksAsync(chromatogramID)

    interface IMzIOIO with

        /// Creates MzIOModel based on global metadata in RAW file.
        member this.CreateDefaultModel() =

            this.RaiseDisposed()

            let modelName   = Path.GetFileNameWithoutExtension(rawFilePath)
            let model       = new MzIOModel(modelName)

            let sampleName  = Path.GetFileNameWithoutExtension(rawFilePath)
            let sample      = new Sample("sample_1", sampleName)
            model.Samples.Add(sample.ID, sample)

            let run         = new Run("run_1", sampleName, rawFile.GetInstrumentData().Name)

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

    /// In memory MzIOModel of ThermoRawFileReader.
    member this.Model =

        (this :> IMzIOIO).Model

    //Testing Function, how to get IsolationWindow, target M/Z and so on.
    member this.getIsolationWindow = 
        
        rawFile.ScanEvents

    //Testing Function, how to get IsolationWindow, target M/Z and so on.
    member this.getIsolationWindow2 = 
        
        rawFile.GetFilters()
