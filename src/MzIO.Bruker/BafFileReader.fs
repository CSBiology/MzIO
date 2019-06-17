namespace MzIO.Bruker

//open System
//open System.Collections.Generic
//open System.IO
//open System.Threading.Tasks
//open MzIO.Binary
//open MzIO.IO
//open MzIO.Json
//open MzIO.Model
//open System.Linq;
//open MzIO.MetaData.PSIMSExtension
//open MzIO.MetaData.UO
//open MzIO.MetaData
//open MzIO.Commons.Arrays
//open System.Collections.ObjectModel
//open MzIO.Bruker
//open MzIO.Bruker.Baf2SqlWrapper
//open MzIO.Bruker.Linq2BafSql


//type private SupportedVariablesCollection() =

//    inherit KeyedCollection<string, BafSqlSupportedVariable>()

//    static member ReadSupportedVariables(linq2BafSql:Linq2BafSql) =
        
//        let tmp = linq2BafSql
//        let variables = tmp.SupportedVariables.ToArray().Where(fun x -> x.Variable.HasValue && String.IsNullOrWhiteSpace(x.PermanentName) = false)
//        let col = new SupportedVariablesCollection()
//        variables
//        |> Seq.iter (fun item -> col.Add(item))
//        col

//    member this.TryGetItem(variablePermanentName:string, variable:byref<BafSqlSupportedVariable>) =
//        if this.Contains(variablePermanentName) then
//            variable <- this.[variablePermanentName]
//            true
//        else
//            variable <- Unchecked.defaultof<BafSqlSupportedVariable>
//            false

//    override this.GetKeyForItem(item:BafSqlSupportedVariable) =
//        item.PermanentName

//type private BafFileTransactionScope() =

//    interface ITransactionScope with

//        member this.Commit() =
//            ()

//        member this.Rollback() =
//            ()

//    interface IDisposable with
        
//        member this.Dispose() =
//            ()

//[<Sealed>]
//type BafFileReader(bafFilePath:string) =

//    let mutable bafFilePath =
//        match bafFilePath with
//        | null  -> failwith (ArgumentNullException("bafFilePath").ToString())
//        | ""    -> failwith (ArgumentNullException("bafFilePath").ToString())
//        | " "   -> failwith (ArgumentNullException("bafFilePath").ToString())
//        |   _   -> 
//            if File.Exists(bafFilePath) = false then 
//                failwith (FileNotFoundException("Baf file not exists.").ToString())
//            else 
//                bafFilePath

//    let mutable disposed = false

//    //member this.Test =  
//    //    try
//    let sqlFilePath = Baf2SqlWrapper.GetSQLiteCacheFilename(bafFilePath)
//    // First argument = 1, ignore contents of Calibrator.ami (if it exists)

//    let baf2SqlHandle = Baf2SqlWrapper.baf2sql_array_open_storage(1, bafFilePath)

//    let baf2SqlHandle =
//        if baf2SqlHandle = Convert.ToUInt64 0 then Baf2SqlWrapper.ThrowLastBaf2SqlError()
//        else baf2SqlHandle
//    let linq2BafSql = new Linq2BafSql(sqlFilePath)

//    let supportedVariables = SupportedVariablesCollection.ReadSupportedVariables(linq2BafSql)

//    interface IMzLiteIO with

//        member this.CreateDefaultModel() =

//            this.RaiseDisposed()

//            let modelName = Path.GetFileNameWithoutExtension(bafFilePath)
//            let model = new MzLiteModel(modelName)

//            let sampleName = Path.GetFileNameWithoutExtension(bafFilePath)
//            let sample = new Sample("sample_1", sampleName);
//            model.Samples.Add(sample)

//            let run = new Run("run_1")
//            run.Sample <- sample
//            model.Runs.Add(run)
//            model

//        member this.Model =
//            this.RaiseDisposed()
//            this.model

//        member this.SaveModel() =

//            this.RaiseDisposed()

//            try
//                MzLiteJson.SaveJsonFile(this.model, this.GetModelFilePath())
//            with
//                | :? Exception as ex ->
//                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

//        member this.BeginTransaction() =
            
//            this.RaiseDisposed()

//            new BafFileTransactionScope() :> ITransactionScope

//    interface IDisposable with

//        member this.Dispose() =

//            this.RaiseDisposed()

//            if disposed = true then 
//                failwith ((new ObjectDisposedException(this.GetType().Name)).ToString())
//            else ()
 
//            if disposed = true then ()
//            else
//                if 
//                    baf2SqlHandle <> Convert.ToUInt64 0 then Baf2SqlWrapper.baf2sql_array_close_storage(baf2SqlHandle)
//                else
//                    //if
//                    //    linq2BafSql <> null then (linq2BafSql :> IDisposable).Dispose()
//                    //else
//                        disposed <- true


//    member this.model = MzLiteJson.HandleExternalModelFile(this, this.GetModelFilePath())
//        //let model = MzLiteJson.HandleExternalModelFile(this, this.GetModelFilePath())

//    //let supportedVariables = SupportedVariablesCollection.ReadSupportedVariables(linq2BafSql)

//        //with
//        //    | :? Exception as ex ->
//        //        failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

//    member this.BafFilePath = bafFilePath

//    member private this.GetModelFilePath() =
        
//        bafFilePath + ".mzlitemodel"

//    member private this.RaiseDisposed() =

//        if disposed = true then

//            failwith (ObjectDisposedException(this.GetType().Name).ToString())
//        else
//            ()

//    member private this.YieldMassSpectra() =

//        let mutable ids = linq2BafSql.Spectra.Where(fun x -> x.Id <> System.Nullable()).OrderBy(fun x -> x.Rt).Select(fun x -> x.Id).ToArray()

//        ids
//        |> Array.map (fun id -> this.ReadMassSpectrum(id.Value))
//        |> (fun item -> item :> IEnumerable<MassSpectrum>)
            

//    interface IMzLiteDataReader with

//        member this.ReadMassSpectra(runID:string) =

//            this.RaiseDisposed()

//            try
//                this.YieldMassSpectra()

//            with

//                | :? MzLiteIOException as ex ->

//                    failwith (ex.ToString())

//                | :? Exception as ex ->
                
//                    failwith ((new MzLiteIOException("Error reading spectrum.", ex)).ToString())
        
//        member this.ReadMassSpectrum(spectrumID:string) =

//            this.RaiseDisposed()

//            try 
//                let id = UInt64.Parse(spectrumID)
//                this.ReadMassSpectrum(id)

//            with
//                | :? MzLiteIOException as ex ->

//                    failwith (ex.ToString())

//                | :? Exception as ex ->
                    
//                    failwith ((new MzLiteIOException("Error reading spectrum: " + spectrumID, ex)).ToString())

//        member this.ReadSpectrumPeaks(spectrumID:string, getCentroids:bool) =

//            this.RaiseDisposed()

//            try
//                let id = UInt64.Parse(spectrumID)
//                this.ReadSpectrumPeaks(id, getCentroids)

//            with
//                | :? MzLiteIOException as ex ->
//                    failwith (ex.ToString())

//                | :? Exception as ex ->
//                    failwith ((new MzLiteIOException("Error reading spectrum peaks: " + spectrumID, ex)).ToString())

//        member this.ReadSpectrumPeaks(spectrumID:string) =

//            this.RaiseDisposed()

//            try
//                let id = UInt64.Parse(spectrumID)
//                this.ReadSpectrumPeaks(id, false)

//            with
//                | :? MzLiteIOException as ex ->
//                    failwith (ex.ToString())

//                | :? Exception as ex ->
//                    failwith ((new MzLiteIOException("Error reading spectrum peaks: " + spectrumID, ex)).ToString())

//        member this.ReadMassSpectrumAsync(spectrumID:string) =

//            Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSoectrum(spectrumID))

//        member this.ReadSpectrumPeaksAsync(spectrumID:string) =

//            Task<Peak1DArray>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))

//        member this.ReadChromatograms(runID:string) =
            
//            try
//                failwith ((new NotSupportedException()).ToString())

//            with
//                | :? Exception as ex ->
//                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

//        member this.ReadChromatogram(runID:string) =
            
//            try
//                failwith ((new NotSupportedException()).ToString())

//            with
//                | :? Exception as ex ->
//                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

//        member this.ReadChromatogramPeaks(runID:string) =
            
//            try
//                failwith ((new NotSupportedException()).ToString())

//            with
//                | :? Exception as ex ->
//                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())
    
//        member this.ReadChromatogramAsync(runID:string) =
            
//            try
//                failwith ((new NotSupportedException()).ToString())

//            with
//                | :? Exception as ex ->
//                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

//        member this.ReadChromatogramPeaksAsync(runID:string) =
            
//            try
//                failwith ((new NotSupportedException()).ToString())

//            with
//                | :? Exception as ex ->
//                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())
        
////        #region BafFileReader Members     

////        private MassSpectrum ReadMassSpectrum(UInt64 spectrumId)
////        {

////            BafSqlSpectrum bafSpec = linq2BafSql.GetBafSqlSpectrum(this.linq2BafSql.Core, spectrumId);

////            if (bafSpec == null)
////                throw new MzLiteIOException("No spectrum found for id: " + spectrumId);

////            MassSpectrum ms = new MassSpectrum(spectrumId.ToString());

////            // determine ms level
////            BafSqlAcquisitionKey aqKey = linq2BafSql.GetBafSqlAcquisitionKey(this.linq2BafSql.Core, bafSpec.AcquisitionKey);
////            Nullable<int> msLevel = null;

////            if (aqKey != null && aqKey.MsLevel.HasValue)
////            {
////                // bruker starts ms level by 0, must be added by 1
////                msLevel = aqKey.MsLevel.Value + 1;
////                ms.SetMsLevel(msLevel.Value);
////            }

////            // determine type of spectrum and read peak data
////            // if profile data available we prefer to get profile data otherwise centroided data (line spectra)
////            if (bafSpec.ProfileMzId.HasValue && bafSpec.ProfileIntensityId.HasValue)
////            {
////                ms.SetProfileSpectrum();
////            }
////            else if (bafSpec.LineMzId.HasValue && bafSpec.LineIntensityId.HasValue)
////            {
////                ms.SetCentroidSpectrum();
////            }

////            if (msLevel == 1)
////            {
////                ms.SetMS1Spectrum();
////            }
////            else if (msLevel > 1)
////            {
////                ms.SetMSnSpectrum();
////            }

////            // scan
////            if (bafSpec.Rt.HasValue)
////            {
////                Scan scan = new Scan();
////                scan.SetScanStartTime(bafSpec.Rt.Value).UO_Second();
////                ms.Scans.Add(scan);
////            }

////            // precursor
////            if (msLevel > 1)
////            {

////                SpectrumVariableCollection spectrumVariables = SpectrumVariableCollection.ReadSpectrumVariables(linq2BafSql, bafSpec.Id);

////                Precursor precursor = new Precursor();

////                decimal value;

////                if (spectrumVariables.TryGetValue("Collision_Energy_Act", supportedVariables, out value))
////                {
////                    precursor.Activation.SetCollisionEnergy(Decimal.ToDouble(value));
////                }
////                if (spectrumVariables.TryGetValue("MSMS_IsolationMass_Act", supportedVariables, out value))
////                {
////                    precursor.IsolationWindow.SetIsolationWindowTargetMz(Decimal.ToDouble(value));
////                }
////                if (spectrumVariables.TryGetValue("Quadrupole_IsolationResolution_Act", supportedVariables, out value))
////                {
////                    double width = Decimal.ToDouble(value) * 0.5d;
////                    precursor.IsolationWindow.SetIsolationWindowUpperOffset(width);
////                    precursor.IsolationWindow.SetIsolationWindowLowerOffset(width);
////                }

////                Nullable<int> charge = null;

////                if (spectrumVariables.TryGetValue("MSMS_PreCursorChargeState", supportedVariables, out value))
////                {
////                    charge = Decimal.ToInt32(value);
////                }

////                IEnumerable<BafSqlStep> ions = linq2BafSql.GetBafSqlSteps(this.linq2BafSql.Core,bafSpec.Id);

////                foreach (BafSqlStep ion in ions)
////                {
////                    if (ion.Mass.HasValue)
////                    {
////                        SelectedIon selectedIon = new SelectedIon();
////                        precursor.SelectedIons.Add(selectedIon);
////                        selectedIon.SetSelectedIonMz(ion.Mass.Value);

////                        selectedIon.SetUserParam("Number", ion.Number.Value);
////                        selectedIon.SetUserParam("IsolationType", ion.IsolationType.Value);
////                        selectedIon.SetUserParam("ReactionType", ion.ReactionType.Value);
////                        selectedIon.SetUserParam("MsLevel", ion.MsLevel.Value);

////                        if (charge.HasValue)
////                        {
////                            selectedIon.SetChargeState(charge.Value);
////                        }
////                    }

////                }

////                // set parent spectrum as reference
////                if (bafSpec.Parent.HasValue)
////                {
////                    precursor.SpectrumReference = new SpectrumReference(bafSpec.Parent.ToString());
////                }

////                ms.Precursors.Add(precursor);
////            }

////            return ms;
////        }

////        public Peak1DArray ReadSpectrumPeaks(UInt64 spectrumId, bool getCentroids)
////        {

////            BafSqlSpectrum bafSpec = linq2BafSql.GetBafSqlSpectrum(this.linq2BafSql.Core,spectrumId);

////            if (bafSpec == null)
////                throw new MzLiteIOException("No spectrum found for id: " + spectrumId);

////            Peak1DArray pa = new Peak1DArray(
////                        BinaryDataCompressionType.NoCompression,
////                        BinaryDataType.Float32,
////                        BinaryDataType.Float32);

////            double[] masses;
////            UInt32[] intensities;

////            // if profile data available we prefer to get profile data otherwise centroided data (line spectra)
////            if (getCentroids && bafSpec.LineMzId.HasValue && bafSpec.LineIntensityId.HasValue)
////            {
////                masses = Baf2SqlWrapper.GetBafDoubleArray(baf2SqlHandle, bafSpec.LineMzId.Value);
////                intensities = Baf2SqlWrapper.GetBafUInt32Array(baf2SqlHandle, bafSpec.LineIntensityId.Value);
////            }
////            else if (getCentroids == false && bafSpec.ProfileMzId.HasValue && bafSpec.ProfileIntensityId.HasValue)
////            {
////                masses = Baf2SqlWrapper.GetBafDoubleArray(baf2SqlHandle, bafSpec.ProfileMzId.Value);
////                intensities = Baf2SqlWrapper.GetBafUInt32Array(baf2SqlHandle, bafSpec.ProfileIntensityId.Value);
////            }
////            else
////            {
////                masses = new double[0];
////                intensities = new UInt32[0];
////            }

////            pa.Peaks = new BafPeaksArray(masses, intensities);

////            return pa;
////        }        

////        #endregion

////        private class BafPeaksArray : IMzLiteArray<Peak1D>
////        {

////            private readonly double[] masses;
////            private readonly UInt32[] intensities;

////            public BafPeaksArray(double[] masses, UInt32[] intensities)
////            {
////                this.masses = masses;
////                this.intensities = intensities;
////            }

////            #region IMzLiteArray<Peak1D> Members

////            public int Length
////            {
////                get { return Math.Min(masses.Length, intensities.Length); }
////            }

////            public Peak1D this[int idx]
////            {
////                get
////                {
////                    if (idx < 0 || idx >= Length)
////                        throw new IndexOutOfRangeException();
////                    return new Peak1D(
////                        intensities[idx],
////                        masses[idx]);
////                }
////            }

////            #endregion

////            #region IEnumerable<Peak1D> Members

////            private IEnumerable<Peak1D> Yield()
////            {
////                for (int i = 0; i < Length; i++)
////                    yield return this[i];
////            }

////            public IEnumerator<Peak1D> GetEnumerator()
////            {
////                return Yield().GetEnumerator();
////            }

////            #endregion

////            #region IEnumerable Members

////            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
////            {
////                return Yield().GetEnumerator();
////            }

////            #endregion
////        }

////        private class SpectrumVariableCollection : KeyedCollection<ulong, BafSqlPerSpectrumVariable>
////        {

////            private SpectrumVariableCollection()
////            {
////            }

////            public static SpectrumVariableCollection ReadSpectrumVariables(Linq2BafSql linq2BafSql, UInt64? spectrumId)
////            {
////                IEnumerable<BafSqlPerSpectrumVariable> variables = linq2BafSql.GetPerSpectrumVariables(linq2BafSql.Core, spectrumId);
////                var col = new SpectrumVariableCollection();
////                foreach (var v in variables)
////                    col.Add(v);

////                return col;
////            }

////            protected override ulong GetKeyForItem(BafSqlPerSpectrumVariable item)
////            {
////                return item.Variable.Value;
////            }

////            public bool TryGetValue(string variablePermanentName, SupportedVariablesCollection supportedVariables, out decimal value)
////            {
////                BafSqlSupportedVariable variable;

////                if (supportedVariables.TryGetItem(variablePermanentName, out variable))
////                {
////                    if (Contains(variable.Variable.Value))
////                    {
////                        value = this[variable.Variable.Value].Value.Value;
////                        return true;
////                    }
////                    else
////                    {
////                        value = default(decimal);
////                        return false;
////                    }
////                }
////                else
////                {
////                    value = default(decimal);
////                    return false;
////                }
////            }
////        }
////    }


////}
