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


type private SupportedVariablesCollection() =

    inherit KeyedCollection<string, BafSqlSupportedVariable>()

    static member ReadSupportedVariables(linq2BafSql:Linq2BafSql) =

        let tmp = linq2BafSql
        let variables = tmp.SupportedVariables.ToArray().Where(fun x -> x.Variable.HasValue && String.IsNullOrWhiteSpace(x.PermanentName) = false)
        let col = new SupportedVariablesCollection()
        variables
        |> Seq.iter (fun item -> col.Add(item))
        col

    member this.TryGetItem(variablePermanentName:string, variable:byref<BafSqlSupportedVariable>) =
        if this.Contains(variablePermanentName) then
            variable <- this.[variablePermanentName]
            true
        else
            variable <- Unchecked.defaultof<BafSqlSupportedVariable>
            false

    override this.GetKeyForItem(item:BafSqlSupportedVariable) =
        item.PermanentName

type private BafFileTransactionScope() =

    interface ITransactionScope with

        member this.Commit() =
            ()

        member this.Rollback() =
            ()

    interface IDisposable with

        member this.Dispose() =
            ()

type private SpectrumVariableCollection() =

    inherit KeyedCollection<uint64, BafSqlPerSpectrumVariable>()

    static member ReadSpectrumVariables(getPerSpectrumVariables, ?spectrumId:UInt64) =

        let spectrumId = defaultArg spectrumId Unchecked.defaultof<UInt64>
        let variables =
            //linq2BafSql.GetPerSpectrumVariables(linq2BafSql.Core, spectrumId).Target :?> IEnumerable<BafSqlPerSpectrumVariable>
            getPerSpectrumVariables spectrumId

        let col = new SpectrumVariableCollection()
        variables
        |> Seq.iter (fun item -> col.Add(item))
        col

    override this.GetKeyForItem(item:BafSqlPerSpectrumVariable) =

        item.Variable.Value

    member this.TryGetValue(variablePermanentName:string, supportedVariables:SupportedVariablesCollection,value:byref<decimal>) =

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

type private BafPeaksArray(masses:double[], intensities:UInt32[]) =

    interface IMzIOArray<Peak1D> with

        member this.Length =

            Math.Min(masses.Length, intensities.Length)

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

    member this.Yield() =

        [0..this.Length-1]
        |> Seq.ofList
        |> Seq.map (fun i -> this.[i])
        |> (fun item -> item :> IEnumerable<'T>)

    member this.GetEnumerator() =

        this.Yield().GetEnumerator()

    member this.Length =
        (this :> IMzIOArray<Peak1D>).Length

    member this.Item

        with get idx =

            (this :> IMzIOArray<Peak1D>).[idx]

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

    //member this.Test =
    //    try
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
            let sample = new Sample("sample_1", sampleName);
            model.Samples.Add(sample.ID, sample)

            let run = new Run("run_1")
            run.Sample <- sample
            model.Runs.Add(run.ID, run)
            model

        member this.Model =
            this.RaiseDisposed()
            MzIOJson.HandleExternalModelFile(this, BafFileReader.GetModelFilePath(bafFilePath))

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

    member this.BeginTransaction() =

        (this :> IMzIOIO).BeginTransaction()

    member this.CreateDefaultModel() =

        (this :> IMzIOIO).CreateDefaultModel()

    member this.SaveModel() =

        (this :> IMzIOIO).SaveModel()

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
                    //if
                    //    linq2BafSql <> null then (linq2BafSql :> IDisposable).Dispose()
                    //else
                        disposed <- true

        //let model = MzIOJson.HandleExternalModelFile(this, this.GetModelFilePath())

    //let supportedVariables = SupportedVariablesCollection.ReadSupportedVariables(linq2BafSql)

        //with
        //    | :? Exception as ex ->
        //        raise (new MzIOIOException(ex.Message, ex))

    member private this.linq2BafSql = linq2BafSql

    member this.BafFilePath = bafFilePath

    static member private GetModelFilePath(bafFilePath:string) =

        sprintf "%s%s" bafFilePath ".MzIOmodel"

    member private this.RaiseDisposed() =

        if disposed = true then

            raise (ObjectDisposedException(this.GetType().Name))
        else
            ()

    member private this.YieldMassSpectra() =

        let mutable ids = linq2BafSql.Spectra.OrderBy(fun x -> x.Rt).Select(fun x -> x.Id)

        if not (cn.State = ConnectionState.Open) then cn.Open()

        let tmp =
            ids
            |> Seq.map (fun id -> this.ReadMassSpectrum(getBafSqlSpectrum, getBafSqlAcquisitionKey, getBafSqlSteps, getPerSpectrumVariables, id.Value))
        //cn.Close()
        tmp

    member this.ReadSpectrumPeaks(spectrumID:string, getCentroids:bool) =

        this.RaiseDisposed()

        try
            
            let id = UInt64.Parse(spectrumID)
            this.ReadSpectrumPeaks(getBafSqlSpectrum, id, getCentroids)

        with
            | :? MzIOIOException as ex ->
                raise ex

            | :? Exception as ex ->
                raise (new MzIOIOException("Error reading spectrum peaks: " + spectrumID, ex))

    interface IMzIODataReader with

        member this.ReadMassSpectra(runID:string) =

            this.RaiseDisposed()

            try

                this.YieldMassSpectra()

            with

                | :? MzIOIOException as ex ->

                    raise ex

                | :? Exception as ex ->

                    raise (new MzIOIOException("Error reading spectrum.", ex))

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

        member this.ReadSpectrumPeaks(spectrumID:string) =

            this.RaiseDisposed()

            try
                let id = UInt64.Parse(spectrumID)

                this.ReadSpectrumPeaks(getBafSqlSpectrum, id, false)

            with
                | :? MzIOIOException as ex ->
                    raise ex

                | :? Exception as ex ->
                    raise (new MzIOIOException("Error reading spectrum peaks: " + spectrumID, ex))

        member this.ReadMassSpectrumAsync(spectrumID:string) =

            Task<MzIO.Model.MassSpectrum>.Run(fun () -> (this :> IMzIODataReader).ReadMassSpectrum(spectrumID))

        member this.ReadSpectrumPeaksAsync(spectrumID:string) =

            Task<Peak1DArray>.Run(fun () -> (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID))

        member this.ReadChromatograms(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        member this.ReadChromatogram(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        member this.ReadChromatogramPeaks(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        member this.ReadChromatogramAsync(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

        member this.ReadChromatogramPeaksAsync(runID:string) =

            try
                raise (new NotSupportedException())

            with
                | :? Exception as ex ->
                    raise (new MzIOIOException(ex.Message, ex))

    member private this.ReadMassSpectrum(getBafSqlSpectrum:Nullable<UInt64>->BafSqlSpectrum, getBafSqlAcquisitionKey:Nullable<UInt64>->BafSqlAcquisitionKey,
                                         getBafSqlSteps:Nullable<UInt64>->seq<BafSqlStep>, getBafPerSpecVariables:UInt64->seq<BafSqlPerSpectrumVariable>,
                                         spectrumId:UInt64) =

        let bafSpec =
            //(linq2BafSql.GetBafSqlSpectrum(this.linq2BafSql.Core, Nullable(spectrumId)))
            //|> (Seq.takeWhile(fun item -> snd item = true))
            //|> Seq.head
            //|> (fun item -> fst item)
            getBafSqlSpectrum(Nullable(spectrumId))

        //if bafSpec = null then

        //    raise (new MzIOIOException("No spectrum found for id: " + spectrumId.ToString()))

        let ms = new MassSpectrum(spectrumId.ToString())

        // determine ms level
        let aqKey =
            //linq2BafSql.GetBafSqlAcquisitionKey(this.linq2BafSql.Core, bafSpec.AcquisitionKey).Target :?> BafSqlAcquisitionKey
            getBafSqlAcquisitionKey bafSpec.AcquisitionKey

        let mutable msLevel = None

        if (*aqKey <> null && *)aqKey.MsLevel.HasValue then
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
                                    precursor.SelectedIons.Add(Guid.NewGuid().ToString(), selectedIon)
                                    selectedIon.SetSelectedIonMz(ion.Mass.Value) |> ignore
                                    selectedIon.SetValue("Number", ion.Number.Value)
                                    selectedIon.SetUserParam("IsolationType", ion.IsolationType.Value)  |> ignore
                                    selectedIon.SetUserParam("ReactionType", ion.ReactionType.Value)    |> ignore
                                    selectedIon.SetUserParam("MsLevel", ion.MsLevel.Value)              |> ignore

                                    if charge.IsSome then
                                        selectedIon.SetChargeState(charge.Value) |> ignore
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

    member this.ReadSpectrumPeaks(getBafSqlSpectrum:Nullable<UInt64>->BafSqlSpectrum, spectrumId:UInt64, getCentroids:bool) =

        if not (cn.State = ConnectionState.Open) then cn.Open()

        let getBafSqlSpectrum (spectrumId:Nullable<UInt64>) =

            getBafSqlSpectrum spectrumId

        let bafSpec =
            //fst (Seq.head ((linq2BafSql.GetBafSqlSpectrum(this.linq2BafSql.Core, Nullable(spectrumId)))))
            getBafSqlSpectrum(Nullable(spectrumId))

        let pa = new Peak1DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float32, BinaryDataType.Float32)

        let mutable masses      = Array.zeroCreate<float> 0
        let mutable intensities = Array.zeroCreate<UInt32> 0
        // if profile data available we prefer to get profile data otherwise centroided data (line spectra)
        if getCentroids && bafSpec.LineMzId.HasValue && bafSpec.LineIntensityId.HasValue then
            masses      <- Baf2SqlWrapper.GetBafDoubleArray(baf2SqlHandle, bafSpec.LineMzId.Value)
            intensities <- Baf2SqlWrapper.GetBafUInt32Array(baf2SqlHandle, bafSpec.LineIntensityId.Value)

        else
            if getCentroids = false && bafSpec.ProfileMzId.HasValue && bafSpec.ProfileIntensityId.HasValue then
                masses      <- Baf2SqlWrapper.GetBafDoubleArray(baf2SqlHandle, bafSpec.ProfileMzId.Value);
                intensities <- Baf2SqlWrapper.GetBafUInt32Array(baf2SqlHandle, bafSpec.ProfileIntensityId.Value);

            else
                masses      <- Array.zeroCreate<float> 0
                intensities <- Array.zeroCreate<UInt32> 0
        pa.Peaks <- new BafPeaksArray(masses, intensities)
        pa

    member this.ReadMassSpectra(runID:string)               =

        (this :> IMzIODataReader).ReadMassSpectra(runID)

    member this.ReadMassSpectrum(spectrumID:string)         =

        (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

    member this.ReadSpectrumPeaks(spectrumID:string)        =

        (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

    member this.ReadMassSpectrumAsync(spectrumID:string)    =

        (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

    member this.ReadSpectrumPeaksAsync(spectrumID:string)   =

        (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

    member this.ReadChromatograms(runID:string)             =

        (this :> IMzIODataReader).ReadChromatograms(runID)

    member this.ReadChromatogramPeaks(runID:string)         =

        (this :> IMzIODataReader).ReadChromatogramPeaks(runID)

    member this.ReadChromatogramAsync(runID:string)         =

        (this :> IMzIODataReader).ReadChromatogramAsync(runID)

    member this.ReadChromatogramPeaksAsync(runID:string)    =

        (this :> IMzIODataReader).ReadChromatogramPeaksAsync(runID)

