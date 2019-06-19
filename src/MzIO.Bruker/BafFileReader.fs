namespace MzIO.Bruker

open System
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


type private SupportedVariablesCollection() =

    inherit KeyedCollection<string, BafSqlSupportedVariable>()

    static member ReadSupportedVariables(linq2BafSql:Linq2BafSql) =
        
        let tmp = linq2BafSql
        let variables = tmp.SupportedVariables.ToArray().Where(fun x -> x.Variable<>System.Nullable() && String.IsNullOrWhiteSpace(x.PermanentName) = false)
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

    static member ReadSpectrumVariables(linq2BafSql:Linq2BafSql, ?spectrumId:UInt64) =
        let variables = linq2BafSql.GetPerSpectrumVariables(linq2BafSql.Core, spectrumId).Target :?> IEnumerable<BafSqlPerSpectrumVariable>
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

    interface IMzLiteArray<Peak1D> with

        member this.Length =
        
            Math.Min(masses.Length, intensities.Length)

        member this.Item 
        
            with get idx =

                if idx < 0 || idx > this.Length then 
                    failwith ((new IndexOutOfRangeException()).ToString())
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
        (this :> IMzLiteArray<Peak1D>).Length

    member this.Item 
    
        with get idx =

            (this :> IMzLiteArray<Peak1D>).[idx]

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
    let sqlFilePath = 
        Baf2SqlWrapper.GetSQLiteCacheFilename(bafFilePath)
    // First argument = 1, ignore contents of Calibrator.ami (if it exists)
    
    let baf2SqlHandle =
        Baf2SqlWrapper.baf2sql_array_open_storage(1, bafFilePath)

    let baf2SqlHandle =
        if baf2SqlHandle = Convert.ToUInt64 0 then Baf2SqlWrapper.ThrowLastBaf2SqlError()
        else baf2SqlHandle
    let mutable linq2BafSql = new Linq2BafSql(sqlFilePath)

    let supportedVariables =
        SupportedVariablesCollection.ReadSupportedVariables(linq2BafSql)

    interface IMzLiteIO with

        member this.CreateDefaultModel() =

            this.RaiseDisposed()

            let modelName = Path.GetFileNameWithoutExtension(bafFilePath)
            let model = new MzLiteModel(modelName)

            let sampleName = Path.GetFileNameWithoutExtension(bafFilePath)
            let sample = new Sample("sample_1", sampleName);
            model.Samples.Add(sample)

            let run = new Run("run_1")
            run.Sample <- sample
            model.Runs.Add(run)
            model

        member this.Model =
            this.RaiseDisposed()
            this.model

        member this.SaveModel() =

            this.RaiseDisposed()

            try
                MzIOJson.SaveJsonFile(this.model, this.GetModelFilePath())
            with
                | :? Exception as ex ->
                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

        member this.BeginTransaction() =
            
            this.RaiseDisposed()

            new BafFileTransactionScope() :> ITransactionScope

    interface IDisposable with

        member this.Dispose() =

            this.RaiseDisposed()

            if disposed = true then 
                failwith ((new ObjectDisposedException(this.GetType().Name)).ToString())
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


    member private this.model = MzIOJson.HandleExternalModelFile(this, this.GetModelFilePath())
        //let model = MzLiteJson.HandleExternalModelFile(this, this.GetModelFilePath())

    //let supportedVariables = SupportedVariablesCollection.ReadSupportedVariables(linq2BafSql)

        //with
        //    | :? Exception as ex ->
        //        failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

    member private this.linq2BafSql = linq2BafSql

    member this.BafFilePath = bafFilePath

    member private this.GetModelFilePath() =
        
        bafFilePath + ".mzlitemodel"

    member private this.RaiseDisposed() =

        if disposed = true then

            failwith (ObjectDisposedException(this.GetType().Name).ToString())
        else
            ()

    member private this.YieldMassSpectra() =

        let mutable ids = linq2BafSql.Spectra.Where(fun x -> x.Id<>System.Nullable()).OrderBy(fun x -> x.Rt).Select(fun x -> x.Id).ToArray()

        ids
        |> Array.map (fun id -> this.ReadMassSpectrum(id.Value))
        |> (fun item -> item :> IEnumerable<MassSpectrum>)
            

    interface IMzLiteDataReader with

        member this.ReadMassSpectra(runID:string) =

            this.RaiseDisposed()

            try
                this.YieldMassSpectra()

            with

                | :? MzLiteIOException as ex ->

                    failwith (ex.ToString())

                | :? Exception as ex ->
                
                    failwith ((new MzLiteIOException("Error reading spectrum.", ex)).ToString())
        
        member this.ReadMassSpectrum(spectrumID:string) =

            this.RaiseDisposed()

            try 
                let id = UInt64.Parse(spectrumID)
                this.ReadMassSpectrum(id)

            with
                | :? MzLiteIOException as ex ->

                    failwith (ex.ToString())

                | :? Exception as ex ->
                    
                    failwith ((new MzLiteIOException("Error reading spectrum: " + spectrumID, ex)).ToString())

        //member this.ReadSpectrumPeaks(spectrumID:string, getCentroids:bool) =

        //    this.RaiseDisposed()

        //    try
        //        let id = UInt64.Parse(spectrumID)
        //        this.ReadSpectrumPeaks(id, getCentroids)

        //    with
        //        | :? MzLiteIOException as ex ->
        //            failwith (ex.ToString())

        //        | :? Exception as ex ->
        //            failwith ((new MzLiteIOException("Error reading spectrum peaks: " + spectrumID, ex)).ToString())

        member this.ReadSpectrumPeaks(spectrumID:string) =

            this.RaiseDisposed()

            try
                let id = UInt64.Parse(spectrumID)

                this.ReadSpectrumPeaks(id, false)

            with
                | :? MzLiteIOException as ex ->
                    failwith (ex.ToString())

                | :? Exception as ex ->
                    failwith ((new MzLiteIOException("Error reading spectrum peaks: " + spectrumID, ex)).ToString())

        member this.ReadMassSpectrumAsync(spectrumID:string) =

            Task<MzIO.Model.MassSpectrum>.Run(fun () -> (this :> IMzLiteDataReader).ReadMassSpectrum(spectrumID))

        member this.ReadSpectrumPeaksAsync(spectrumID:string) =

            Task<Peak1DArray>.Run(fun () -> (this :> IMzLiteDataReader).ReadSpectrumPeaks(spectrumID))

        member this.ReadChromatograms(runID:string) =
            
            try
                failwith ((new NotSupportedException()).ToString())

            with
                | :? Exception as ex ->
                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

        member this.ReadChromatogram(runID:string) =
            
            try
                failwith ((new NotSupportedException()).ToString())

            with
                | :? Exception as ex ->
                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

        member this.ReadChromatogramPeaks(runID:string) =
            
            try
                failwith ((new NotSupportedException()).ToString())

            with
                | :? Exception as ex ->
                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())
    
        member this.ReadChromatogramAsync(runID:string) =
            
            try
                failwith ((new NotSupportedException()).ToString())

            with
                | :? Exception as ex ->
                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())

        member this.ReadChromatogramPeaksAsync(runID:string) =
            
            try
                failwith ((new NotSupportedException()).ToString())

            with
                | :? Exception as ex ->
                    failwith ((new MzLiteIOException(ex.Message, ex)).ToString())
        
    member private this.ReadMassSpectrum(spectrumId:UInt64) =

        let bafSpec = linq2BafSql.GetBafSqlSpectrum(this.linq2BafSql.Core, spectrumId).Target :?> BafSqlSpectrum

        //if bafSpec = null then
            
        //    failwith ((new MzLiteIOException("No spectrum found for id: " + spectrumId.ToString())).ToString())

        let ms = new MassSpectrum(spectrumId.ToString())

        // determine ms level
        let aqKey = linq2BafSql.GetBafSqlAcquisitionKey(this.linq2BafSql.Core, bafSpec.AcquisitionKey).Target :?> BafSqlAcquisitionKey
        let mutable msLevel = Unchecked.defaultof<int option>
        
        if (*aqKey <> null && *)aqKey.MsLevel<>System.Nullable() then
            // bruker starts ms level by 0, must be added by 1
            msLevel <- Some (aqKey.MsLevel.Value + 1)
            ms.SetMsLevel(msLevel.Value) |> ignore

        // determine type of spectrum and read peak data
        // if profile data available we prefer to get profile data otherwise centroided data (line spectra)
        if bafSpec.ProfileMzId<>System.Nullable() && bafSpec.ProfileIntensityId<>System.Nullable() = true then
            
            ms.SetProfileSpectrum() |> ignore

        else
            if bafSpec.LineMzId<>System.Nullable() && bafSpec.LineIntensityId<>System.Nullable() then
                    
                    ms.SetCentroidSpectrum() |> ignore
            else
                ms |> ignore

        if msLevel.IsSome then
            if msLevel.Value = 1 then

                ms.SetMS1Spectrum() |> ignore
            else
                if msLevel.Value > 1 then

                    ms.SetMSnSpectrum() |> ignore
                else
                    ms.SetMSnSpectrum() |> ignore

        // scan
        if bafSpec.Rt<>System.Nullable() then
            let scan = new Scan()
            scan.SetScanStartTime(bafSpec.Rt.Value).UO_Second() |> ignore
            ms.Scans.Add(scan)
        else 
            ()

        // precursor
        if msLevel.IsSome then

            if msLevel.Value > 1 then
            
                let spectrumVariables = 
                    if bafSpec.Id <> System.Nullable() then SpectrumVariableCollection.ReadSpectrumVariables(linq2BafSql, Convert.ToUInt64(id))
                    else SpectrumVariableCollection.ReadSpectrumVariables(linq2BafSql)

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

                let ions = linq2BafSql.GetBafSqlSteps(this.linq2BafSql.Core,bafSpec.Id).Target :?> IEnumerable<BafSqlStep>
                ions
                |> Seq.map (fun ion ->
                                if ion.Mass<>System.Nullable() then
                                    let selectedIon = new SelectedIon()
                                    precursor.SelectedIons.Add(selectedIon)
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
                if bafSpec.Parent<>System.Nullable() then
                    
                    precursor.SpectrumReference <- new SpectrumReference(bafSpec.Parent.ToString())

                else 
                    () 
                ms.Precursors.Add(precursor) |> ignore 
            else ()
        else ()
        ms

    member this.ReadSpectrumPeaks(spectrumId:UInt64, getCentroids:bool) =
        
        let bafSpec = linq2BafSql.GetBafSqlSpectrum(this.linq2BafSql.Core,spectrumId).Target :?> BafSqlSpectrum

        let pa = new Peak1DArray(BinaryDataCompressionType.NoCompression, BinaryDataType.Float32, BinaryDataType.Float32)


        let mutable masses      = Array.zeroCreate<float> 0
        let mutable intensities = Array.zeroCreate<UInt32> 0

        // if profile data available we prefer to get profile data otherwise centroided data (line spectra)
        if getCentroids && bafSpec.LineMzId<>System.Nullable() && bafSpec.LineIntensityId<>System.Nullable() then
            
            masses      <- Baf2SqlWrapper.GetBafDoubleArray(baf2SqlHandle, bafSpec.LineMzId.Value)
            intensities <- Baf2SqlWrapper.GetBafUInt32Array(baf2SqlHandle, bafSpec.LineIntensityId.Value)

        else
            if getCentroids = false && bafSpec.ProfileMzId<>System.Nullable() && bafSpec.ProfileIntensityId<>System.Nullable() then
                
                masses      <- Baf2SqlWrapper.GetBafDoubleArray(baf2SqlHandle, bafSpec.ProfileMzId.Value);
                intensities <- Baf2SqlWrapper.GetBafUInt32Array(baf2SqlHandle, bafSpec.ProfileIntensityId.Value);

            else
                masses      <- Array.zeroCreate<float> 0
                intensities <- Array.zeroCreate<UInt32> 0

        pa.Peaks <- new BafPeaksArray(masses, intensities)
        pa
