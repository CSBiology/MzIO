namespace MzIO.MzSQL


open System
open System.Data
open System.IO
open System.Threading.Tasks
open System.Collections.Generic
open System.Data.SQLite
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.Json
open MzIO.Binary
open MzIO.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq


type private MzSQLTransactionScope() =

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
    
/// Contains methods and procedures to create, insert and access MzSQL files.
type MzSQL(path) =

    let mutable disposed = false

    let encoder = new BinaryDataEncoder()

    let sqlitePath = 
        if String.IsNullOrWhiteSpace(path) then
                raise (ArgumentNullException("sqlitePath"))
            else
                path

    let mutable cn = new SQLiteConnection(sprintf "Data Source=%s;Version=3" sqlitePath)

    do
        if File.Exists("sqlitePath") then 
            ()
        else
            MzSQL.SqlInitSchema(cn)

    let mutable tr = 
        MzSQL.RaiseConnectionState(cn)
        cn.BeginTransaction()

    let insertModel =
        MzSQL.prepareInsertModel(cn, &tr)

    let selectModel =
        MzSQL.prepareSelectModel(cn, &tr)

    let insertMassSpectrum =
        MzSQL.prepareInsertMassSpectrum(cn, &tr)

    let selectMassSpectrum =
        MzSQL.prepareSelectMassSpectrum(cn, &tr)

    let selectMassSpectra =
        MzSQL.prepareSelectMassSpectra(cn, &tr)

    let selectPeak1DArray =
        MzSQL.prepareSelectPeak1DArray(cn, &tr)

    let insertChromatogram =
        MzSQL.prepareInsertChromatogram(cn, &tr)

    let selectChromatogram =
        MzSQL.prepareSelectChromatogram(cn, &tr)

    let selectChromatograms =
        MzSQL.prepareSelectChromatograms(cn, &tr)

    let selectPeak2DArray =
        MzSQL.prepareSelectPeak2DArray(cn, &tr)

    let model =
        let potMdoel = MzSQL.trySelectModel(cn, &tr)
        match potMdoel with
        | Some model    -> model
        | None          -> 
            let tmp = new MzIOModel(Path.GetFileNameWithoutExtension(sqlitePath))
            insertModel tmp
            tr.Commit()
            tr <- cn.BeginTransaction()
            tmp

    member this.Commit() = 
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        tr.Commit()

    member this.CreateConnection() =
        disposed <- false
        new SQLiteConnection(sprintf "Data Source=%s;Version=3" sqlitePath)

    member this.Close() = cn.Close() 

    static member RaiseConnectionState(cn:SQLiteConnection) =
        if (cn.State=ConnectionState.Open) then 
            ()
        else
            cn.Open()

    static member RaiseTransactionState(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =

        if tr.Connection = null then
            tr <- cn.BeginTransaction()
        else
            ()

    member private this.RaiseDisposed() =

            if disposed then 
                raise (new ObjectDisposedException(this.GetType().Name))
            else 
                ()

    static member private SqlInitSchema(cn:SQLiteConnection) =
        MzSQL.RaiseConnectionState(cn)
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Model (Lock INTEGER  NOT NULL PRIMARY KEY DEFAULT(0) CHECK (Lock=0), Content TEXT NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Spectrum (RunID TEXT NOT NULL, SpectrumID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Chromatogram (RunID TEXT NOT NULL, ChromatogramID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore

    static member private trySelectModel(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.SqlInitSchema(cn)
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let querySelect = "SELECT Content FROM Model WHERE Lock = 0"
        let cmdSelect = new SQLiteCommand(querySelect, cn, tr)
        let selectReader = cmdSelect.ExecuteReader()
        let rec loopSelect (reader:SQLiteDataReader) model =
            match reader.Read() with
            | true  -> loopSelect reader (Some(MzIOJson.deSerializeMzIOModel(reader.GetString(0))))
            | false -> model           
        loopSelect selectReader None

    static member private prepareInsertModel(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let queryString = 
            "INSERT INTO Model (
                Lock,
                Content)
                VALUES(
                    @lock,
                    @content)"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@lock"      ,Data.DbType.Int64)     |> ignore
        cmd.Parameters.Add("@content"   ,Data.DbType.String)    |> ignore
        (fun (model:MzIOModel) ->
            cmd.Parameters.["@lock"].Value      <- 0
            cmd.Parameters.["@content"].Value   <- MzIOJson.ToJson(model)
            cmd.ExecuteNonQuery() |> ignore
        ) 

    static member prepareSelectModel(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        let querySelect = "SELECT Content FROM Model WHERE Lock = 0"
        let cmd = new SQLiteCommand(querySelect, cn, tr)
        fun () ->
        let rec loopSelect (reader:SQLiteDataReader) model =
            match reader.Read() with
            | true  -> loopSelect reader (MzIOJson.deSerializeMzIOModel(reader.GetString(0)))
            | false -> model  
        use reader = cmd.ExecuteReader()
        loopSelect reader (new MzIOModel())

    member this.SelectModel =
        selectModel

    static member private prepareUpdateRunIDOfMzIOModel(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        
        //let jsonModel = MzIOJson.ToJson(model)
        let queryString = "UPDATE Model SET Content = @model WHERE Lock = 0"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@model" ,Data.DbType.String)    |> ignore
        fun (runID:string) (model:MzIOModel) ->
            let run = 
                let tmp =
                    model.Runs.GetProperties false
                    |> Seq.head
                    |> (fun item -> item.Value :?> Run)
                new Run(runID, tmp.SampleID, tmp.DefaultInstrumentID,tmp.DefaultSpectrumProcessing, tmp.DefaultChromatogramProcessing)
            if model.Runs.TryAdd(run.ID, run) then
                cmd.Parameters.["@model"].Value <- MzIOJson.ToJson(model)
                cmd.ExecuteNonQuery() |> ignore
            else 
                ()

    ///Prepare function to insert MzQuantMLDocument-record.
    static member private prepareInsertMassSpectrum(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let queryString = 
            "INSERT INTO Spectrum (
                RunID,
                SpectrumID,
                Description,
                PeakArray,
                PeakData)
                VALUES(
                    @runID,
                    @spectrumID,
                    @description,
                    @peakArray,
                    @peakData)"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@runID"         ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@spectrumID"    ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@description"   ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakArray"     ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakData"      ,Data.DbType.Binary)    |> ignore
        (fun (encoder:BinaryDataEncoder) (runID:string) (spectrum:MassSpectrum) (peaks:Peak1DArray) ->
            cmd.Parameters.["@runID"].Value         <- runID
            cmd.Parameters.["@spectrumID"].Value    <- spectrum.ID
            cmd.Parameters.["@description"].Value   <- MzIOJson.ToJson(spectrum)
            cmd.Parameters.["@peakArray"].Value     <- MzIOJson.ToJson(peaks)
            cmd.Parameters.["@peakData"].Value      <- encoder.Encode(peaks)
            cmd.ExecuteNonQuery() |> ignore
        )        

    /// Prepare function to select element of Description table of MzSQL.
    static member private prepareSelectMassSpectrum(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let queryString = "SELECT Description FROM Spectrum WHERE SpectrumID = @spectrumID"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@spectrumID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) (acc:MassSpectrum option) =
            match reader.Read() with
                    | true  -> loop reader (Some (MzIOJson.deSerializeMassSpectrum(reader.GetString(0))))
                    | false -> acc 
        fun (id:string) ->
        cmd.Parameters.["@spectrumID"].Value <- id            
        use reader = cmd.ExecuteReader()
        match loop reader None with
        | Some spectrum -> spectrum
        | None          -> failwith ("No enum with this SpectrumID found")

    /// Prepare function to select elements of Description table of MzSQL.
    static member private prepareSelectMassSpectra(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let queryString = "SELECT Description FROM Spectrum WHERE RunID = @runID"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@runID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) acc =
            seq
                {
                    match reader.Read() with
                    | true  -> 
                        yield MzIOJson.deSerializeMassSpectrum(reader.GetString(0))
                        yield! loop reader acc
                    | false -> 
                        yield! acc 
                }
        fun (id:string) ->
        cmd.Parameters.["@runID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader Seq.empty
        |> List.ofSeq
        |> (fun spectra -> if spectra.IsEmpty then failwith ("No enum with this RunID found") else spectra)
        |> (fun spectra -> spectra :> IEnumerable<MassSpectrum>)

    /// Prepare function to select elements of PeakArray and PeakData tables of MzSQL.
    static member private prepareSelectPeak1DArray(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let decoder = new BinaryDataDecoder()
        let queryString = "SELECT PeakArray, PeakData FROM Spectrum WHERE SpectrumID = @spectrumID"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@spectrumID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) peaks =
            match reader.Read() with
            | true  -> loop reader (decoder.Decode(reader.GetStream(1), MzIOJson.FromJson<Peak1DArray>(reader.GetString(0))))
            | false -> peaks 
        fun id ->
        cmd.Parameters.["@spectrumID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader (new Peak1DArray())

    /// Prepare function to insert element into Chromatogram table of MzSQL.
    static member private prepareInsertChromatogram(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let encoder = new BinaryDataEncoder()
        let selectModel = MzSQL.prepareSelectModel(cn, &tr)
        let updateRunID = MzSQL.prepareUpdateRunIDOfMzIOModel(cn, &tr)
        let queryString = 
            "INSERT INTO Chromatogram (
                RunID,
                ChromatogramID,
                Description,
                PeakArray,
                PeakData)
                VALUES(
                    @runID,
                    @chromatogramID,
                    @description,
                    @peakArray,
                    @peakData)"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@runID"         ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@chromatogramID"  ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@description"   ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakArray"     ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakData"      ,Data.DbType.Binary)    |> ignore
        (fun (runID:string) (chromatogram:Chromatogram) (peaks:Peak2DArray) ->
            updateRunID runID (selectModel())
            cmd.Parameters.["@runID"].Value         <- runID
            cmd.Parameters.["@chromatogramID"].Value  <- chromatogram.ID
            cmd.Parameters.["@description"].Value   <- MzIOJson.ToJson(chromatogram)
            cmd.Parameters.["@peakArray"].Value     <- MzIOJson.ToJson(peaks)
            cmd.Parameters.["@peakData"].Value      <- encoder.Encode(peaks)
            cmd.ExecuteNonQuery() |> ignore
        )        

    /// Prepare function to select element of Description table of MzSQL.
    static member private prepareSelectChromatogram(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let queryString = "SELECT Description FROM Chromatogram WHERE RunID = @runID"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@runID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) (acc:Chromatogram) =
            match reader.Read() with
            | true  -> loop reader (MzIOJson.FromJson<Chromatogram>(reader.GetString(0)))
            | false -> acc 
        fun (id:string) ->
        cmd.Parameters.["@runID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader (new Chromatogram())

    /// Prepare function to select elements of Description table of MzSQL.
    static member private prepareSelectChromatograms(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let queryString = "SELECT Description FROM Chromatogram WHERE ChromatogramID = @chromatogramID"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@chromatogramID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) acc =
            seq
                {
                    match reader.Read() with
                    | true  -> 
                        yield MzIOJson.FromJson<Chromatogram>(reader.GetString(0))
                        yield! loop reader acc
                    | false -> 
                        yield! acc 
                }
        fun (id:string) ->
        cmd.Parameters.["@chromatogramID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader Seq.empty
        |> List.ofSeq
        |> (fun item -> item :> IEnumerable<Chromatogram>)

    /// Prepare function to select elements of PeakArray and PeakData tables of MzSQL.
    static member private prepareSelectPeak2DArray(cn:SQLiteConnection, tr:byref<SQLiteTransaction>) =
        MzSQL.RaiseConnectionState(cn)
        MzSQL.RaiseTransactionState(cn, &tr)
        let decoder = new BinaryDataDecoder()
        let queryString = "SELECT PeakArray, PeakData FROM Chromatogram WHERE ChromatogramID = @chromatogramID"
        let cmd = new SQLiteCommand(queryString, cn, tr)
        cmd.Parameters.Add("@chromatogramID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) peaks =
            match reader.Read() with
            | true  -> loop reader (decoder.Decode(reader.GetStream(1), MzIOJson.FromJson<Peak2DArray>(reader.GetString(0))))
            | false -> peaks 
        fun (id:string) ->
        cmd.Parameters.["@chromatogramID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader (new Peak2DArray())

    interface IMzIODataReader with

        member this.ReadMassSpectra(runID: string) =
            this.RaiseDisposed()
            selectMassSpectra(runID)

        member this.ReadMassSpectrum(spectrumID: string) =
            this.RaiseDisposed()
            selectMassSpectrum(spectrumID)

        member this.ReadSpectrumPeaks(spectrumID: string) =
            this.RaiseDisposed()
            selectPeak1DArray(spectrumID)

        member this.ReadMassSpectrumAsync(spectrumID:string) =        
            Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSpectrum(spectrumID))

        member this.ReadSpectrumPeaksAsync(spectrumID:string) =            
            Task<Peak1DArray>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))

        member this.ReadChromatograms(runID: string) =
            this.RaiseDisposed()
            selectChromatograms(runID)

        member this.ReadChromatogram(chromatogramID: string) =
            this.RaiseDisposed()
            selectChromatogram(chromatogramID)

        member this.ReadChromatogramPeaks(chromatogramID: string) =
            this.RaiseDisposed()
            selectPeak2DArray(chromatogramID)

        member this.ReadChromatogramAsync(chromatogramID:string) =
           async {return selectChromatogram(chromatogramID)}
        
        member this.ReadChromatogramPeaksAsync(chromatogramID:string) =
           async {return selectPeak2DArray(chromatogramID)}

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

    interface IMzIODataWriter with

        member this.InsertMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            this.RaiseDisposed()
            insertMassSpectrum encoder runID spectrum peaks

        member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            this.RaiseDisposed()
            insertChromatogram runID chromatogram peaks

        member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            async {return (this.InsertMass(runID, spectrum, peaks))}

        member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            async {return (this.InsertChrom(runID, chromatogram, peaks))}

    /// Write runID, spectrum and peaks into MzSQL file.
    member this.InsertMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertMass(runID, spectrum, peaks)

    /// Write runID, chromatogram and peaks into MzSQL file.
    member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertChrom(runID, chromatogram, peaks)

    /// Write runID, spectrum and peaks into MzSQL file asynchronously.
    member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertAsyncMass(runID, spectrum, peaks)

    /// Write runID, chromatogram and peaks into MzSQL file asynchronously.
    member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertAsyncChrom(runID, chromatogram, peaks)
        
    interface IDisposable with

        member this.Dispose() =
            disposed <- true
            tr.Dispose()
            cn.Close()

    member this.Dispose() =

        (this :> IDisposable).Dispose()

    interface IMzIOIO with

        member this.BeginTransaction() =
            this.RaiseDisposed()
            MzSQL.RaiseConnectionState(cn)
            MzSQL.RaiseTransactionState(cn, &tr)
            new MzSQLTransactionScope() :> ITransactionScope

        member this.CreateDefaultModel() =
            this.RaiseDisposed()
            new MzIOModel(Path.GetFileNameWithoutExtension(sqlitePath))

        member this.SaveModel() =
            this.RaiseDisposed()
            insertModel this.Model
            tr.Commit()

        member this.Model =
            this.RaiseDisposed()
            model

    member this.BeginTransaction() =
        
        (this :> IMzIOIO).BeginTransaction()

    member this.CreateDefaultModel() =
        (this :> IMzIOIO).CreateDefaultModel()        

    member this.SaveModel() =
        (this :> IMzIOIO).SaveModel()

    member this.Model = 
        (this :> IMzIOIO).Model

    /// copies MassSpectrum into DB schema
    member this.insertMSSpectrum (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectrum: MassSpectrum)= 
        let peakArray = reader.ReadSpectrumPeaks(spectrum.ID)
        match compress with 
        | BinaryDataCompressionType.NoCompression  -> 
            let clonedP = new Peak1DArray(BinaryDataCompressionType.NoCompression, peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            this.InsertMass(runID, spectrum, clonedP)
        | BinaryDataCompressionType.ZLib -> 
            let clonedP = new Peak1DArray(BinaryDataCompressionType.ZLib, peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            this.InsertMass(runID, spectrum, clonedP)
        | BinaryDataCompressionType.NumPress ->
            let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPress, peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            this.InsertMass(runID, spectrum, clonedP)
        | BinaryDataCompressionType.NumPressZLib ->
            let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPressZLib, peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            this.InsertMass(runID, spectrum, clonedP)
        | _ ->
            failwith "Not a valid compression Method"

    /// modifies spectrum according to the used spectrumPeaksModifierF and inserts the result into the DB schema 
    member this.insertModifiedSpectrumBy (spectrumPeaksModifierF: IMzIODataReader -> MassSpectrum -> BinaryDataCompressionType -> Peak1DArray) (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectrum: MassSpectrum) = 
        let modifiedP = spectrumPeaksModifierF reader spectrum compress
        this.InsertMass(runID, spectrum, modifiedP)

    static member private updateModel(newModel:MzIOModel, oldModel:MzIOModel) =
        oldModel.GetProperties false
        |> Seq.iter (fun item -> newModel.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.Instruments.GetProperties false
        |> Seq.iter (fun item -> newModel.Instruments.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.Runs.GetProperties false
        |> Seq.iter (fun item -> newModel.Runs.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.DataProcessings.GetProperties false
        |> Seq.iter (fun item -> newModel.DataProcessings.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.Softwares.GetProperties false
        |> Seq.iter (fun item -> newModel.Softwares.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.Samples.GetProperties false
        |> Seq.iter (fun item -> newModel.Samples.TryAdd(item.Key, item.Value) |> ignore)
        oldModel.FileDescription <- newModel.FileDescription
        newModel

    /// Starts bulkinsert of mass spectra into a MzLiteSQL database
    member this.insertMSSpectraBy insertSpectrumF (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectra: seq<MassSpectrum>) = 
        let selectModel = MzSQL.prepareSelectModel(cn, &tr)
        let updateRunID = MzSQL.prepareUpdateRunIDOfMzIOModel(cn, &tr)
        let model = MzSQL.updateModel(selectModel(), reader.Model)
        updateRunID runID model
        let bulkInsert spectra = 
            spectra
            |> Seq.iter (insertSpectrumF runID reader compress)
        bulkInsert spectra
        this.Commit()
        this.Dispose()
        this.Close()
