namespace MzIO.Bruker


open System
open System.Data
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open MzIO.Binary
open MzIO.IO
open MzIO.Json
open MzIO.Model
open System.Linq
open MzIO.MetaData
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.PSIMSExtension
open MzIO.MetaData.UO
open MzIO.MetaData.UO.UO
open MzIO.Commons.Arrays
open System.Collections.ObjectModel
open MzIO.Bruker
open MzIO.Bruker.Baf2SqlWrapper
open MzIO.Bruker.Linq2BafSql
open System.Data.SQLite


/// Defines a collection which connects the BafSqlSupportedVariable to a key in order to enable fast access to the different SQLite keys.
type private SupportedVariablesCollection() =

    inherit KeyedCollection<string, BafSqlSupportedVariable>()

    /// Returns BafSqlSupportedVariable based on the keys saved in Linq2BafSql.
    static member ReadSupportedVariables(linq2BafSql:Linq2BafSql) =

        let tmp = linq2BafSql
        let variables = tmp.SupportedVariables.ToArray().Where(fun x -> x.Variable.HasValue && String.IsNullOrWhiteSpace(x.PermanentName) = false)
        let col = new SupportedVariablesCollection()
        variables
        |> Seq.iter (fun item -> col.Add(item))
        col

    /// Returns the BafSqlSupportedVariable of the SupportedVariablesCollection connected with the key.
    member this.TryGetItem(variablePermanentName:string, variable:byref<BafSqlSupportedVariable>) =
        if this.Contains(variablePermanentName) then
            variable <- this.[variablePermanentName]
            true
        else
            variable <- Unchecked.defaultof<BafSqlSupportedVariable>
            false

    /// Returns the key of the BafSqlSupportedVariable in the SupportedVariablesCollection
    override this.GetKeyForItem(item:BafSqlSupportedVariable) =
        item.PermanentName

/// Controlles connection with Baf file.
type private BafFileTransactionScope() =

    interface ITransactionScope with

        /// Does Nothing.
        member this.Commit() =
            ()

        /// Does Nothing.
        member this.Rollback() =
            ()

    interface IDisposable with

        member this.Dispose() =
            ()

/// Defines a collection which connects the BafSqlPerSpectrumVariable to a key in order to enable fast access to the different SQLite keys.
type private SpectrumVariableCollection() =

    inherit KeyedCollection<uint64, BafSqlPerSpectrumVariable>()

    /// Creates a SpectrumVariableCollection with BafSqlPerSpectrumVariables from the BAF file.
    static member ReadSpectrumVariables(getPerSpectrumVariables, ?spectrumId:UInt64) =

        let spectrumId  = defaultArg spectrumId Unchecked.defaultof<UInt64>
        let variables   = getPerSpectrumVariables spectrumId

        let col = new SpectrumVariableCollection()
        variables
        |> Seq.iter (fun item -> col.Add(item))
        col

    /// Get key for BafSqlPerSpectrumVariable in SpectrumVariableCollection.
    override this.GetKeyForItem(item:BafSqlPerSpectrumVariable) =

        item.Variable.Value

    /// Get decimal value of BafSqlPerSpectrumVariable in SupportedVariablesCollection based on variablePermanentName.
    member this.TryGetValue(variablePermanentName:string, supportedVariables:SupportedVariablesCollection, value:byref<decimal>) =

        let mutable variable = new BafSqlSupportedVariable()

        if supportedVariables.TryGetItem(variablePermanentName, & variable) then

            if this.Contains(variable.Variable.Value) then

                value <- this.[variable.Variable.Value].Value.Value
                true
            else
                value <- Unchecked.defaultof<Decimal>
                false
        else
            value <- Unchecked.defaultof<Decimal>
            false

/// Baf specific peak array because BAF intensities are saved as uint32 and not as float.
type private BafPeaksArray(masses:double[], intensities:UInt32[]) =

    interface IMzIOArray<Peak1D> with

        /// Get length of BafPeaksArray.
        member this.Length =

            Math.Min(masses.Length, intensities.Length)

        /// Get item with index of BafPeaksArray.
        member this.Item

            with get idx =

                if idx < 0 || idx > this.Length then
                    raise (new IndexOutOfRangeException())
                else
                    new Peak1D(float intensities.[idx], masses.[idx])

    interface IEnumerable<Peak1D> with

        member this.GetEnumerator() =

            this.Yield().GetEnumerator()

    interface System.Collections.IEnumerable with
        member this.GetEnumerator() =
            this.Yield().GetEnumerator() :> Collections.IEnumerator
        
    /// Returns collection of Peak1D. 
    member this.Yield() =

        [0..this.Length-1]
        |> Seq.ofList
        |> Seq.map (fun i -> this.[i])
        |> (fun item -> item :> IEnumerable<'T>)

    member this.GetEnumerator() =

        this.Yield().GetEnumerator()

    /// Get length of BafPeaksArray.
    member this.Length =
        (this :> IMzIOArray<Peak1D>).Length

    /// Get item with index of BafPeaksArray.
    member this.Item

        with get idx =

            (this :> IMzIOArray<Peak1D>).[idx]

/// Contains methods to access spectrum and peak information of BAF files.
[<Sealed>]
type BafFileReader(bafFilePath:string) =

    let mutable bafFilePath =

        if String.IsNullOrWhiteSpace(bafFilePath) then
            raise (ArgumentNullException("bafFilePath"))
        else
            if File.Exists(bafFilePath) = false then
                raise (FileNotFoundException("Baf file not exists."))
            else
                bafFilePath

    let mutable disposed = false

    let sqlFilePath = Baf2SqlWrapper.GetSQLiteCacheFilename(bafFilePath)

    let cn = new SQLiteConnection(sprintf "%s%s%s" "Data Source=" sqlFilePath ";Version=3")

    // First argument = 1, ignore contents of Calibrator.ami (if it exists)
    let baf2SqlHandle =
        Baf2SqlWrapper.baf2sql_array_open_storage(1, bafFilePath)

    let baf2SqlHandle =
        if baf2SqlHandle = Convert.ToUInt64 0 then
            Baf2SqlWrapper.ThrowLastBaf2SqlError()
        else baf2SqlHandle
    let mutable linq2BafSql = new Linq2BafSql(sqlFilePath)

    let supportedVariables =
        SupportedVariablesCollection.ReadSupportedVariables(linq2BafSql)

    //member private this.model = MzIOJson.HandleExternalModelFile(this, BafFileReader.GetModelFilePath(bafFilePath))

    let getBafSqlSpectrum =
        linq2BafSql.PrepareGetBafSqlSpectrum cn

    let getBafSqlAcquisitionKey =
        linq2BafSql.PrepareGetBafSqlAcquisitionKey cn

    let getBafSqlSteps =
        linq2BafSql.PrepareGetBafSqlSteps cn

    let getPerSpectrumVariables =
        linq2BafSql.PrepareGetPerSpectrumVariables cn

    interface IMzIOIO with

        member this.CreateDefaultModel() =

            this.RaiseDisposed()

            let modelName = Path.GetFileNameWithoutExtension(bafFilePath)
            let model = new MzIOModel(modelName)

            let sampleName = Path.GetFileNameWithoutExtension(bafFilePath)
            let sample = new Sample("sample_1", sampleName)
            model.Samples.Add(sample.ID, sample)
            let run = new Run("run_1", sampleName, "DefaultInstrumentName")
            //run.Sample <- sample
            model.Runs.Add(run.ID, run)
            model

        /// MzIO model based on shadow file or BAF file. Creates shadow if it doesn't exist.
        member this.Model =
            this.RaiseDisposed()
            MzIOJson.HandleExternalModelFile(this, BafFileReader.GetModelFilePath(bafFilePath))

        /// Save current mzio model in shadow file.
        member this.SaveModel() =

            this.RaiseDisposed()

            try
                MzIOJson.SaveJsonFile(this.Model, BafFileReader.GetModelFilePath(bafFilePath))
            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        member this.BeginTransaction() =

            this.RaiseDisposed()

            new BafFileTransactionScope() :> ITransactionScope

    /// Opens connection to Baf file.
    member this.BeginTransaction() =

        (this :> IMzIOIO).BeginTransaction()

    /// Creates basic MzIOModel based on minimal information of BAF file.
    member this.CreateDefaultModel() =

        (this :> IMzIOIO).CreateDefaultModel()

    /// Saves current MzIOModel in the memory as a Shadowfile.
    member this.SaveModel() =

        (this :> IMzIOIO).SaveModel()

    /// Gives access to the MzIOModel in memory.
    member this.Model =

        (this :> IMzIOIO).Model

    interface IDisposable with

        member this.Dispose() =

            this.RaiseDisposed()

            if disposed = true then
                raise (new ObjectDisposedException(this.GetType().Name))
            else ()

            if disposed = true then ()
            else
                if
                    baf2SqlHandle <> Convert.ToUInt64 0 then Baf2SqlWrapper.baf2sql_array_close_storage(baf2SqlHandle)
                else
                    disposed <- true

    member this.BafFilePath = bafFilePath

    /// Generates path for shadow file.
    static member private GetModelFilePath(bafFilePath:string) =

        sprintf "%s%s" bafFilePath ".MzIOmodel"

    /// Checks wheter reader is disposed or not.
    member private this.RaiseDisposed() =

        if disposed = true then

            raise (ObjectDisposedException(this.GetType().Name))
        else
            ()

    /// Reads mass spectra of BAF file.
    member private this.YieldMassSpectra() =

        let mutable ids = linq2BafSql.Spectra.OrderBy(fun x -> x.Rt).Select(fun x -> x.Id)

        if not (cn.State = ConnectionState.Open) then cn.Open()

        let tmp =
            ids
            |> Seq.map (fun id -> this.ReadMassSpectrum(getBafSqlSpectrum, getBafSqlAcquisitionKey, getBafSqlSteps, getPerSpectrumVariables, id.Value))

        tmp

    /// Returns peaks of spectra of BAF file.
    member this.ReadSpectrumPeaks(spectrumID:string, getCentroids:bool) =

        this.RaiseDisposed()

        try
            
            let id = UInt64.Parse(spectrumID)
            this.ReadSpectrumPeaks(id, getCentroids)

        with
            | :? MzIOIOException as ex ->
                raise ex

            | :? Exception as ex ->
                raise (new MzIOIOException("Error reading spectrum peaks: " + spectrumID, ex))

    interface IMzIODataReader with

        /// Returns all mass spectra of BAF file.
        member this.ReadMassSpectra(runID:string) =

            this.RaiseDisposed()

            try
                this.YieldMassSpectra()
            with

                | :? MzIOIOException as ex ->
                    raise ex
                | :? Exception as ex ->
                    raise (new MzIOIOException("Error reading spectrum.", ex))

        /// Returns mass spectrum with key from BAF file.
        member this.ReadMassSpectrum(spectrumID:string) =

            this.RaiseDisposed()

            cn.Open()

            try
                let id = UInt64.Parse(spectrumID)
                this.ReadMassSpectrum(getBafSqlSpectrum, getBafSqlAcquisitionKey, getBafSqlSteps, getPerSpectrumVariables, id)

            with
                | :? MzIOIOException as ex ->

                    raise ex

                | :? Exception as ex ->

                    raise (new MzIOIOException("Error reading spectrum: " + spectrumID, ex))

        /// Returns peaks of spectrum with key from BAF file.
        member this.ReadSpectrumPeaks(spectrumID:string) =

            this.RaiseDisposed()

            try
                let id = UInt64.Parse(spectrumID)

                this.ReadSpectrumPeaks(id, false)

            with
                | :? MzIOIOException as ex ->
                    raise ex

                | :? Exception as ex ->
                    raise (new MzIOIOException("Error reading spectrum peaks: " + spectrumID, ex))

        /// Returns spectrum with key from BAF file async.
        member this.ReadMassSpectrumAsync(spectrumID:string) =

            Task<MzIO.Model.MassSpectrum>.Run(fun () -> (this :> IMzIODataReader).ReadMassSpectrum(spectrumID))

        /// Returns peaks of spectrum with key from BAF file async.
        member this.ReadSpectrumPeaksAsync(spectrumID:string) =

            Task<Peak1DArray>.Run(fun () -> (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID))

        /// Not implemented yet.
        member this.ReadChromatograms(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        /// Not implemented yet.
        member this.ReadChromatogram(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        /// Not implemented yet.
        member this.ReadChromatogramPeaks(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        /// Not implemented yet.
        member this.ReadChromatogramAsync(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        /// Not implemented yet.
        member this.ReadChromatogramPeaksAsync(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

    /// Read mass spectrum of BAF file.
    member private this.ReadMassSpectrum(getBafSqlSpectrum:Nullable<UInt64>->BafSqlSpectrum, getBafSqlAcquisitionKey:Nullable<UInt64>->BafSqlAcquisitionKey,
                                         getBafSqlSteps:Nullable<UInt64>->list<BafSqlStep>, getBafPerSpecVariables:UInt64->list<BafSqlPerSpectrumVariable>,
                                         spectrumId:UInt64) =

        let bafSpec =  getBafSqlSpectrum(Nullable(spectrumId))

        let ms = new MassSpectrum(spectrumId.ToString())

        // determine ms level
        let aqKey = getBafSqlAcquisitionKey bafSpec.AcquisitionKey

        let mutable msLevel = None

        if aqKey.MsLevel.HasValue then
            // bruker starts ms level by 0, must be added by 1
            msLevel <- Some (aqKey.MsLevel.Value + (int64 1))
            ms.SetMsLevel(int32 msLevel.Value) |> ignore

        // determine type of spectrum and read peak data
        // if profile data available we prefer to get profile data otherwise centroided data (line spectra)
        if bafSpec.ProfileMzId.HasValue&& bafSpec.ProfileIntensityId.HasValue then

            ms.SetProfileSpectrum() |> ignore

        else
            if bafSpec.LineMzId.HasValue && bafSpec.LineIntensityId.HasValue then

                    ms.SetCentroidSpectrum() |> ignore
            else
                ms |> ignore

        if msLevel.IsSome then
            if msLevel.Value = int64 1 then

                ms.SetMS1Spectrum() |> ignore
            else
                if msLevel.Value > int64 1 then

                    ms.SetMSnSpectrum() |> ignore
                else
                    ms.SetMSnSpectrum() |> ignore

        // scan
        if bafSpec.Rt.HasValue then
            let scan = new Scan()
            scan.SetScanStartTime(bafSpec.Rt.Value).UO_Second() |> ignore
            ms.Scans.Add(Guid.NewGuid().ToString(), scan)
        else
            ()

        // precursor
        if msLevel.IsSome then

            if msLevel.Value > int64 1 then

                let spectrumVariables =
                    if bafSpec.Id.HasValue then
                        SpectrumVariableCollection.ReadSpectrumVariables(getBafPerSpecVariables, bafSpec.Id.Value)
                    else SpectrumVariableCollection.ReadSpectrumVariables(getBafPerSpecVariables)

                let precursor = new Precursor()
                let mutable value = decimal(0)
                if spectrumVariables.TryGetValue("Collision_Energy_Act", supportedVariables, & value) then
                    precursor.Activation.SetCollisionEnergy(Decimal.ToDouble(value)) |> ignore
                else
                    ()

                if spectrumVariables.TryGetValue("MSMS_IsolationMass_Act", supportedVariables, & value) then
                    precursor.IsolationWindow.SetIsolationWindowTargetMz(Decimal.ToDouble(value)) |> ignore
                else
                    ()

                if spectrumVariables.TryGetValue("Quadrupole_IsolationResolution_Act", supportedVariables, & value) then
                    let width = Decimal.ToDouble(value) * 0.5
                    precursor.IsolationWindow.SetIsolationWindowUpperOffset(width) |> ignore
                    precursor.IsolationWindow.SetIsolationWindowLowerOffset(width) |> ignore

                else ()

                let mutable charge = None

                if spectrumVariables.TryGetValue("MSMS_PreCursorChargeState", supportedVariables, & value) then
                    charge <- Some(Decimal.ToInt32(value))
                else ()

                let ions =
                    //linq2BafSql.GetBafSqlSteps(this.linq2BafSql.Core,bafSpec.Id).Target :?> IEnumerable<BafSqlStep>
                    getBafSqlSteps bafSpec.Id
                    
                ions
                |> Seq.iter (fun ion ->
                                if ion.Mass.HasValue then
                                    let selectedIon = new SelectedIon()
                                    selectedIon.SetSelectedIonMz(ion.Mass.Value)                                |> ignore
                                    selectedIon.SetValue("Number", int32 ion.Number.Value)                      |> ignore
                                    selectedIon.SetUserParam("IsolationType", int32 ion.IsolationType.Value)    |> ignore
                                    selectedIon.SetUserParam("ReactionType", int32 ion.ReactionType.Value)      |> ignore
                                    selectedIon.SetUserParam("MsLevel", int32 ion.MsLevel.Value)                |> ignore
                                    precursor.SelectedIons.Add(Guid.NewGuid().ToString(), selectedIon)          |> ignore

                                    if charge.IsSome then
                                        selectedIon.SetChargeState(charge.Value)    |> ignore
                                    else
                                        ()
                            ) |> ignore

                // set parent spectrum as reference
                if bafSpec.Parent.HasValue then
                    precursor.SpectrumReference <- new SpectrumReference(bafSpec.Parent.ToString())
                else
                    ()
                ms.Precursors.Add(Guid.NewGuid().ToString(), precursor) |> ignore
            else ()
        else ()
        ms

    /// Read peaks of BAF file.
    member this.ReadSpectrumPeaks(spectrumId:UInt64, getCentroids:bool) =

        if not (cn.State = ConnectionState.Open) then cn.Open()

        let bafSpec =
            //fst (Seq.head ((linq2BafSql.GetBafSqlSpectrum(this.linq2BafSql.Core, Nullable(spectrumId)))))
            getBafSqlSpectrum(Nullable(spectrumId))

        let pa = new Peak1DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float64, BinaryDataType.Float64)

        let mutable masses      = Array.zeroCreate<float> 0
        let mutable intensities = Array.zeroCreate<UInt32> 0
        // if profile data available we prefer to get profile data otherwise centroided data (line spectra)
        if getCentroids && bafSpec.LineMzId.HasValue && bafSpec.LineIntensityId.HasValue then
            masses      <- Baf2SqlWrapper.GetBafDoubleArray(baf2SqlHandle, bafSpec.LineMzId.Value)
            intensities <- Baf2SqlWrapper.GetBafUInt32Array(baf2SqlHandle, bafSpec.LineIntensityId.Value)

        else
            if getCentroids = false && bafSpec.ProfileMzId.HasValue && bafSpec.ProfileIntensityId.HasValue then
                masses      <- Baf2SqlWrapper.GetBafDoubleArray(baf2SqlHandle, bafSpec.ProfileMzId.Value)
                intensities <- Baf2SqlWrapper.GetBafUInt32Array(baf2SqlHandle, bafSpec.ProfileIntensityId.Value)

            else
                masses      <- Array.zeroCreate<float> 0
                intensities <- Array.zeroCreate<UInt32> 0
        pa.Peaks <- new BafPeaksArray(masses, intensities)
        pa

    /// Read all mass spectra of one run of BAF file.
    member this.ReadMassSpectra(runID:string)               =

        (this :> IMzIODataReader).ReadMassSpectra(runID)

    /// Read mass spectrum of BAF file.
    member this.ReadMassSpectrum(spectrumID:string)         =

        (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

    /// Read peaks of mass spectrum of BAF file.
    member this.ReadSpectrumPeaks(spectrumID:string)        =

        (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

    /// Read mass spectrum of BAF file asynchronously.
    member this.ReadMassSpectrumAsync(spectrumID:string)    =

        (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

    /// Read peaks of mass spectrum of BAF file asynchronously.
    member this.ReadSpectrumPeaksAsync(spectrumID:string)   =

        (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

    /// Not implemented yet.
    member this.ReadChromatograms(runID:string)             =

        (this :> IMzIODataReader).ReadChromatograms(runID)

    /// Not implemented yet.
    member this.ReadChromatogramPeaks(runID:string)         =

        (this :> IMzIODataReader).ReadChromatogramPeaks(runID)

    /// Not implemented yet.
    member this.ReadChromatogramAsync(runID:string)         =

        (this :> IMzIODataReader).ReadChromatogramAsync(runID)

    /// Not implemented yet.
    member this.ReadChromatogramPeaksAsync(runID:string)    =

        (this :> IMzIODataReader).ReadChromatogramPeaksAsync(runID)

