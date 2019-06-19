namespace MzLite.Thermo


//open System
//open System.Collections.Generic
//open System.IO
//open System.Linq
//open System.Threading.Tasks
//open MSFileReaderLib
//open MzIO.Binary
//open MzIO.Commons.Arrays
//open MzIO.IO
//open MzIO.Json
//open MzIO.MetaData.PSIMSExtension
//open MzIO.Model


//[<Sealed>]
//type ThermoRawFileReader(rawFilePath:string) =(* : IMzLiteDataReader*)

//    let rawFilePath =
//        if String.IsNullOrWhiteSpace(rawFilePath) then
//            failwith ((new ArgumentNullException("rawFilePath")).ToString())
//        else
//            if File.Exists(rawFilePath) = false then
//                failwith ((new FileNotFoundException("Raw file not exists.")).ToString())
//            else 
//                rawFilePath
//                //try
//    let rawFile = new MSFileReader_XRawfile() :> IXRawfile5
//    rawFile.Open(rawFilePath)
//    rawFile.SetCurrentController(0, 1)

//    let startScanNo = GetFirstSpectrumNumber(rawFile)
//    let endScanNo = GetLastSpectrumNumber(rawFile)

//    let model = MzLiteJson.HandleExternalModelFile(this, GetModelFilePath())

//                //with
//                //    | :? Exception as ex ->
//                //        failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

//    member private this.rawFilePath = rawFilePath
        

////        private bool disposed = false;
////        private readonly MzLiteModel model;
////        private readonly string rawFilePath;
////        private readonly IXRawfile5 rawFile;
////        private readonly int startScanNo;
////        private readonly int endScanNo;

////        #region ThermoRawFileReader Members

//    member private this.GetModelFilePath() =

//        sprintf "%s%s" this.rawFilePath ".mzlitemodel"

//    static member private GetFirstSpectrumNumber(rawFile:IXRawfile5) =
//        //let t,firstScanNumber = rawFile.GetFirstSpectrumNumber(& firstScanNumber)

//        let mutable firstScanNumber = 0
//        rawFile.GetFirstSpectrumNumber(& firstScanNumber)
//        firstScanNumber

//    static member private GetLastSpectrumNumber(rawFile:IXRawfile5) =

//        let mutable lastScanNumber = 0
//        rawFile.GetLastSpectrumNumber(& lastScanNumber)
//        lastScanNumber

//    static member private IsCentroidSpectrum(rawFile:IXRawfile5, scanNo:int) =

//        let mutable isCentroidScan = 0
//        rawFile.IsCentroidScanForScanNum(scanNo, & isCentroidScan)
//        isCentroidScan <> 0
        
//    static member private GetRetentionTime(rawFile:IXRawfile5, scanNo:int) =

//        let mutable rt = 0
//        rawFile.RTFromScanNum(scanNo, & rt)
//        rt

//    static member private GetFilterString(rawFile:IXRawfile5, scanNo:int) =
        
//        let mutable filter = null
//        rawFile.GetFilterForScanNum(scanNo, & filter)
//        filter
        
//    static member private GetMSLevel(rawFile:IXRawfile5, scanNo:int) =

//        let mutable msLevel = 1;
//        rawFile.GetMSOrderForScanNum(scanNo, & msLevel);
//        msLevel

//    static member private GetIsolationWindowWidth(rawFile:IXRawfile5, scanNo:int, msLevel:int) =

//        let mutable width = 0
//        rawFile.GetIsolationWidthForScanNum(scanNo, msLevel - 1, ref width)
//        width

//    static member private GetIsolationWindowTargetMz(rawFile:IXRawfile5, scanNo:int, msLevel:int) =

//        let mutable mz = 0
//        rawFile.GetPrecursorMassForScanNum(scanNo, msLevel, ref mz)
//        mz

//    static member private GetPrecursorMz(rawFile:IXRawfile5, scanNo:int, msLevel:int) =

//        let mutable mz = 0
//        rawFile.GetPrecursorMassForScanNum(scanNo, msLevel, ref mz)
//        mz

//    static member private GetCollisionEnergy(rawFile:IXRawfile5, scanNo:int, msLevel:int) =
        
//        let mutable e = 0
//        rawFile.GetCollisionEnergyForScanNum(scanNo, msLevel - 1, ref e)
//        e

//    static member private GetCollisionEnergy(rawFile:IXRawfile5, scanNo:int, msLevel:int) =

//        let mutable e  = 0
//        rawFile.GetCollisionEnergyForScanNum(scanNo, msLevel - 1, ref e)
//        e

//    static member private GetChargeState(rawFile:IXRawfile5, scanNo:int) =

//        let mutable pChargeValue = null
//        rawFile.GetTrailerExtraValueForScanNum(scanNo, "Charge State:", ref pChargeValue);
//        Convert.ToInt32(pChargeValue)

//    static member private GetChargeState(rawFile:IXRawfile5, scanNo:int) =

//        let mutable pChargeValue = null;
//        rawFile.GetTrailerExtraValueForScanNum(scanNo, "Charge State:", ref pChargeValue);
//        Convert.ToInt32(pChargeValue)


//    /// <summary>
//    /// Parse the scan number from spectrum native id.
//    /// Native id has the format: 'scan=[scanNumber]', i.e. for scan number 1000 the id is 'scan=1000'.
//    /// </summary>
//    /// <param name="id"></param>
//    /// <returns></returns>
//    member private this.ParseSpectrumId(id:string) =
        
//        if id = null then 
//            raise (new ArgumentNullException("id"))
//        else
//            let splitted = id.Split('=')
//            if splitted.Length = 2 && splitted.[0].Equals("scan") then
                
//                let scanNo = Int32.Parse(splitted.[1])

//                if scanNO < startScanNo || scanNo > endScanNo then

//                    raise (new IndexOutOfRangeException("Scan number out of range."))

//                else
//                    scanNo
//            else
//                raise (new FormatException("Wrong native id format in: " + id))


//    /// <summary>
//    /// Builds a spectrum id from scan number in format : 'scan=scanNumber'.
//    /// </summary>
//    /// <param name="scanNumber"></param>
//    /// <returns></returns>
//    static member private GetSpectrumID(scanNumber:int) =

//        sprintf "scan=%s" (scanNumber.ToString())

//    member private this.ReadMassSpectrum(scanNo:int) =

////        private MzLite.Model.MassSpectrum ReadMassSpectrum(int scanNo)
////        {

////            RaiseDisposed();

////            try
////            {

////                string spectrumID = GetSpectrumID(scanNo);
////                MassSpectrum spectrum = new MassSpectrum(spectrumID);

////                // spectrum

////                int msLevel = GetMSLevel(rawFile, scanNo);
////                spectrum.SetMsLevel(msLevel);

////                if (IsCentroidSpectrum(rawFile, scanNo))
////                    spectrum.SetCentroidSpectrum();
////                else
////                    spectrum.SetProfileSpectrum();

////                // scan

////                Scan scan = new Scan();
////                scan.SetFilterString(GetFilterString(rawFile, scanNo))
////                    .SetScanStartTime(GetRetentionTime(rawFile, scanNo));
////                //.UO_Minute();

////                spectrum.Scans.Add(scan);

////                // precursor

////                if (msLevel > 1)
////                {

////                    Precursor precursor = new Precursor();

////                    double isoWidth = GetIsolationWindowWidth(rawFile, scanNo, msLevel) * 0.5d;
////                    double targetMz = GetIsolationWindowTargetMz(rawFile, scanNo, msLevel);
////                    double precursorMz = GetPrecursorMz(rawFile, scanNo, msLevel);
////                    int chargeState = GetChargeState(rawFile, scanNo);

////                    precursor.IsolationWindow
////                            .SetIsolationWindowTargetMz(targetMz)
////                            .SetIsolationWindowUpperOffset(isoWidth)
////                            .SetIsolationWindowLowerOffset(isoWidth);

////                    SelectedIon selectedIon = new SelectedIon();

////                    selectedIon
////                        .SetSelectedIonMz(precursorMz)
////                        .SetChargeState(chargeState);

////                    precursor.SelectedIons.Add(selectedIon);

////                    spectrum.Precursors.Add(precursor);
////                }

////                return spectrum;

////            }
////            catch (Exception ex)
////            {
////                throw new MzLiteIOException(ex.Message, ex);
////            }
////        }

////        private Binary.Peak1DArray ReadSpectrumPeaks(int scanNo)
////        {

////            RaiseDisposed();

////            try
////            {
////                int peakArraySize = 0;
////                double controidPeakWith = 0;
////                object massList = null;
////                object peakFlags = null;

////                rawFile.GetMassListFromScanNum(
////                    ref scanNo,
////                    null, 1, 0, 0, 0,
////                    ref controidPeakWith,
////                    ref massList,
////                    ref peakFlags,
////                    ref peakArraySize);

////                double[,] peakData = massList as double[,];

////                Peak1DArray pa = new Peak1DArray(
////                        BinaryDataCompressionType.NoCompression,
////                        BinaryDataType.Float32,
////                        BinaryDataType.Float32);

////                //Peak1D[] peaks = new Peak1D[peakArraySize];

////                //for (int i = 0; i < peakArraySize; i++)
////                //    peaks[i] = new Peak1D(peakData[1, i], peakData[0, i]);

////                //pa.Peaks = MzLiteArray.ToMzLiteArray(peaks);

////                pa.Peaks = new ThermoPeaksArray(peakData, peakArraySize);

////                return pa;
////            }
////            catch (Exception ex)
////            {
////                throw new MzLiteIOException(ex.Message, ex);
////            }
////        }

////        #endregion

////        #region IMzLiteDataReader Members

////        public IEnumerable<Model.MassSpectrum> ReadMassSpectra(string runID)
////        {
////            for (int i = startScanNo; i <= endScanNo; i++)
////            {
////                yield return ReadMassSpectrum(i);
////            }
////        }

////        public Model.MassSpectrum ReadMassSpectrum(string spectrumID)
////        {
////            int scanNo = ParseSpectrumId(spectrumID);
////            return ReadMassSpectrum(scanNo);
////        }

////        public Binary.Peak1DArray ReadSpectrumPeaks(string spectrumID)
////        {
////            int scanNo = ParseSpectrumId(spectrumID);
////            return ReadSpectrumPeaks(scanNo);
////        }

////        public Task<MassSpectrum> ReadMassSpectrumAsync(string spectrumID)
////        {
////            return Task<MassSpectrum>.Run(() => { return ReadMassSpectrum(spectrumID); });
////        }

////        public Task<Peak1DArray> ReadSpectrumPeaksAsync(string spectrumID)
////        {
////            return Task<Peak1DArray>.Run(() => { return ReadSpectrumPeaks(spectrumID); });
////        }

////        public IEnumerable<Model.Chromatogram> ReadChromatograms(string runID)
////        {
////            return Enumerable.Empty<Chromatogram>();
////        }

////        public Model.Chromatogram ReadChromatogram(string chromatogramID)
////        {
////            try
////            {
////                throw new NotSupportedException();
////            }
////            catch (Exception ex)
////            {
////                throw new MzLiteIOException(ex.Message, ex);
////            }
////        }

////        public Binary.Peak2DArray ReadChromatogramPeaks(string chromatogramID)
////        {
////            try
////            {
////                throw new NotSupportedException();
////            }
////            catch (Exception ex)
////            {
////                throw new MzLiteIOException(ex.Message, ex);
////            }
////        }

////        public Task<Chromatogram> ReadChromatogramAsync(string spectrumID)
////        {
////            try
////            {
////                throw new NotSupportedException();
////            }
////            catch (Exception ex)
////            {
////                throw new MzLiteIOException(ex.Message, ex);
////            }
////        }

////        public Task<Peak2DArray> ReadChromatogramPeaksAsync(string spectrumID)
////        {
////            try
////            {
////                throw new NotSupportedException();
////            }
////            catch (Exception ex)
////            {
////                throw new MzLiteIOException(ex.Message, ex);
////            }
////        }

////        #endregion

////        #region IMzLiteIO Members

////        public MzLiteModel CreateDefaultModel()
////        {
////            RaiseDisposed();

////            string modelName = Path.GetFileNameWithoutExtension(rawFilePath);
////            MzLiteModel model = new MzLiteModel(modelName);

////            string sampleName = Path.GetFileNameWithoutExtension(rawFilePath);
////            Sample sample = new Sample("sample_1", sampleName);
////            model.Samples.Add(sample);

////            Run run = new Run("run_1");
////            run.Sample = sample;
////            model.Runs.Add(run);

////            return model;
////        }

////        public MzLiteModel Model
////        {

////            get
////            {
////                RaiseDisposed();
////                return model;
////            }
////        }

////        public void SaveModel()
////        {
////            RaiseDisposed();

////            try
////            {
////                MzLiteJson.SaveJsonFile(model, GetModelFilePath());
////            }
////            catch (Exception ex)
////            {
////                throw new MzLiteIOException(ex.Message, ex);
////            }
////        }

////        public ITransactionScope BeginTransaction()
////        {
////            RaiseDisposed();
////            return new ThermoRawTransactionScope();
////        }

////        #endregion

////        #region IDisposable Members

////        private void RaiseDisposed()
////        {
////            if (disposed)
////                throw new ObjectDisposedException(this.GetType().Name);
////        }

////        public void Dispose()
////        {
////            if (disposed)
////                return;

////            if (rawFile != null)
////            {
////                rawFile.Close();
////            }

////            disposed = true;
////        }

////        #endregion
////    }

////    internal sealed class ThermoPeaksArray : IMzLiteArray<Peak1D>
////    {

////        private readonly double[,] peakData;
////        private readonly int peakArraySize;

////        internal ThermoPeaksArray(double[,] peakData, int peakArraySize)
////        {
////            this.peakData = peakData;
////            this.peakArraySize = peakArraySize;
////        }

////        #region IMzLiteArray<Peak1D> Members

////        public int Length
////        {
////            get { return peakArraySize; }
////        }

////        public Peak1D this[int idx]
////        {
////            get { return new Peak1D(peakData[1, idx], peakData[0, idx]); }
////        }

////        #endregion

////        #region IEnumerable<Peak1D> Members

////        private static IEnumerable<Peak1D> Yield(double[,] peakData, int peakArraySize)
////        {
////            for (int i = 0; i < peakArraySize; i++)
////            {
////                yield return new Peak1D(peakData[1, i], peakData[0, i]);
////            }
////        }

////        public IEnumerator<Peak1D> GetEnumerator()
////        {
////            return Yield(peakData, peakArraySize).GetEnumerator();
////        }

////        #endregion

////        #region IEnumerable Members

////        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
////        {
////            return Yield(peakData, peakArraySize).GetEnumerator();
////        }

////        #endregion
////    }

////    internal class ThermoRawTransactionScope : ITransactionScope
////    {
////        #region ITransactionScope Members

////        public void Commit()
////        {
////        }

////        public void Rollback()
////        {
////        }

////        #endregion

////        #region IDisposable Members

////        public void Dispose()
////        {
////        }

////        #endregion
////    }
////}


