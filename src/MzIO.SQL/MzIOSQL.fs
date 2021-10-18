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
open System.Runtime.InteropServices


type private MzSQLTransactionScope(tr:SQLiteTransaction) =

    interface IDisposable with
        member this.Dispose() =
            tr.Dispose()

    member this.Dispose() =
        (this :> IDisposable).Dispose()

    interface ITransactionScope with

        member this.Commit() =
            tr.Commit()

        member this.Rollback() =
            tr.Rollback()

    member this.Commit() =
        (this :> ITransactionScope).Commit()


    member this.Rollback() =
        (this :> ITransactionScope).Rollback()
    

/// Contains methods and procedures to create, insert and access MzSQL files.
type MzSQL(path, ?cacheSize) =

    let mutable disposed = false

    let encoder = new BinaryDataEncoder()

    /// Creates the tables in the connected dataBase.
    let sqlInitSchema(cn:SQLiteConnection) =
        //MzSQL.RaiseConnectionState(cn)
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Model (Lock INTEGER  NOT NULL PRIMARY KEY DEFAULT(0) CHECK (Lock=0), Content TEXT NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Spectrum (RunID TEXT NOT NULL, SpectrumID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Chromatogram (RunID TEXT NOT NULL, ChromatogramID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore

    let sqlitePath = 
        if String.IsNullOrWhiteSpace(path) then
                raise (ArgumentNullException("sqlitePath"))
            else
                path

    let cn = 
        let cn' = 
            match cacheSize with
            | Some size -> new SQLiteConnection(sprintf "Data Source=%s;Version=3;Cache Size=%i" sqlitePath size)
            | None -> new SQLiteConnection(sprintf "Data Source=%s;Version=3" sqlitePath)
        //if File.Exists(sqlitePath) then 
        //    cn'
        //else
        cn'.Open()
        sqlInitSchema(cn')
        cn'.Close()
        cn'
    
    /// Selects model from DB. It has always the same ID and only one Model should be saved per DB.
    let trySelectModel(cn:SQLiteConnection) =
        let querySelect = "SELECT Content FROM Model WHERE Lock = 0"
        let cmdSelect = new SQLiteCommand(querySelect, cn)
        let selectReader = cmdSelect.ExecuteReader()
        let rec loopSelect (reader:SQLiteDataReader) model =
            match reader.Read() with
            | true  -> loopSelect reader (Some(MzIOJson.deSerializeMzIOModel(reader.GetString(0))))
            | false -> model           
        loopSelect selectReader None

    /// Prepare function to insert MzIOModel-JSONString.
    let prepareInsertModel(cn:SQLiteConnection) =
        let queryString = 
            "INSERT INTO Model (
                Lock,
                Content)
                VALUES(
                    @lock,
                    @content)"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@lock"      ,Data.DbType.Int64)     |> ignore
        cmd.Parameters.Add("@content"   ,Data.DbType.String)    |> ignore
        (fun (model:MzIOModel) ->
            cmd.Parameters.["@lock"].Value      <- 0
            cmd.Parameters.["@content"].Value   <- MzIOJson.ToJson(model)
            cmd.ExecuteNonQuery() |> ignore
        ) 

    /// Prepare function to select MzIOModel as a MzIOModel object.
    let prepareSelectModel(cn:SQLiteConnection) =
        let querySelect = "SELECT Content FROM Model WHERE Lock = 0"
        let cmd = new SQLiteCommand(querySelect, cn)
        fun () ->
        let rec loopSelect (reader:SQLiteDataReader) model =
            match reader.Read() with
            | true  -> loopSelect reader (MzIOJson.deSerializeMzIOModel(reader.GetString(0)))
            | false -> model  
        use reader = cmd.ExecuteReader()
        loopSelect reader (new MzIOModel())

    /// Prepare function to update runID in MzIOModel in DB.
    let prepareUpdateRunIDOfMzIOModel(cn:SQLiteConnection) =        
        let queryString = "UPDATE Model SET Content = @model WHERE Lock = 0"
        let cmd = new SQLiteCommand(queryString, cn)
        let potModel = trySelectModel(cn)
        let insertModel = prepareInsertModel(cn)
        cmd.Parameters.Add("@model" ,Data.DbType.String)    |> ignore
        fun (runID:string) (model:MzIOModel) ->
            if potModel.IsSome then
                let run = 
                    let tmp =
                        model.Runs.GetProperties false
                        |> Seq.head
                        |> (fun item -> item.Value :?> Run)
                    new Run(runID, tmp.SampleID, tmp.DefaultInstrumentID,tmp.DefaultSpectrumProcessing, tmp.DefaultChromatogramProcessing)
                if model.Runs.TryAdd(run.ID, run) then
                    printfn "MzIOJson.ToJson(model) %s" (MzIOJson.ToJson(model))
                    cmd.Parameters.["@model"].Value <- MzIOJson.ToJson(model)
                    cmd.ExecuteNonQuery() |> ignore
                else 
                    ()
            else
                insertModel model

    //let updateRunID runID (model:MzIOModel) = 
    //    let tmp =
    //        model.Runs.GetProperties false
    //        |> Seq.head
    //        |> (fun item -> item.Value :?> Run)
    //    new Run(runID, tmp.SampleID, tmp.DefaultInstrumentID,tmp.DefaultSpectrumProcessing, tmp.DefaultChromatogramProcessing)
    ///// Prepare function to insert element into Chromatogram table of MzSQL.

    //let prepareInsertChromatogram(cn:SQLiteConnection) =
    //    let encoder = new BinaryDataEncoder()
    //    let selectModel = prepareSelectModel(cn)
    //    let updateRunID = prepareUpdateRunIDOfMzIOModel(cn)
    //    let queryString = 
    //        "INSERT INTO Chromatogram (
    //            RunID,
    //            ChromatogramID,
    //            Description,
    //            PeakArray,
    //            PeakData)
    //            VALUES(
    //                @runID,
    //                @chromatogramID,
    //                @description,
    //                @peakArray,
    //                @peakData)"
    //    let cmd = new SQLiteCommand(queryString, cn)
    //    cmd.Parameters.Add("@runID"         ,Data.DbType.String)    |> ignore
    //    cmd.Parameters.Add("@chromatogramID"  ,Data.DbType.String)  |> ignore
    //    cmd.Parameters.Add("@description"   ,Data.DbType.String)    |> ignore
    //    cmd.Parameters.Add("@peakArray"     ,Data.DbType.String)    |> ignore
    //    cmd.Parameters.Add("@peakData"      ,Data.DbType.Binary)    |> ignore
    //    (fun (runID:string) (chromatogram:Chromatogram) (peaks:Peak2DArray) ->
    //        updateRunID runID (selectModel())
    //        cmd.Parameters.["@runID"].Value         <- runID
    //        cmd.Parameters.["@chromatogramID"].Value  <- chromatogram.ID
    //        cmd.Parameters.["@description"].Value   <- MzIOJson.ToJson(chromatogram)
    //        cmd.Parameters.["@peakArray"].Value     <- MzIOJson.ToJson(peaks)
    //        cmd.Parameters.["@peakData"].Value      <- encoder.Encode(peaks)
    //        cmd.ExecuteNonQuery() |> ignore
    //    )        

    ///// Prepare function to select element of Description table of MzSQL.
    //let prepareSelectChromatogram(cn:SQLiteConnection) =
    //    let queryString = "SELECT Description FROM Chromatogram WHERE RunID = @runID"
    //    let cmd = new SQLiteCommand(queryString, cn)
    //    cmd.Parameters.Add("@runID", Data.DbType.String) |> ignore
    //    let rec loop (reader:SQLiteDataReader) (acc:Chromatogram) =
    //        match reader.Read() with
    //        | true  -> loop reader (MzIOJson.FromJson<Chromatogram>(reader.GetString(0)))
    //        | false -> acc 
    //    fun (id:string) ->
    //    cmd.Parameters.["@runID"].Value <- id            
    //    use reader = cmd.ExecuteReader()            
    //    loop reader (new Chromatogram())

    ///// Prepare function to select elements of Description table of MzSQL.
    //let prepareSelectChromatograms(cn:SQLiteConnection) =
    //    let queryString = "SELECT Description FROM Chromatogram WHERE ChromatogramID = @chromatogramID"
    //    let cmd = new SQLiteCommand(queryString, cn)
    //    cmd.Parameters.Add("@chromatogramID", Data.DbType.String) |> ignore
    //    let rec loop (reader:SQLiteDataReader) acc =
    //        match reader.Read() with
    //        | true  -> loop reader (MzIOJson.FromJson<Chromatogram>(reader.GetString(0))::acc)
    //        | false -> acc 
    //    fun (id:string) ->
    //    cmd.Parameters.["@chromatogramID"].Value <- id            
    //    use reader = cmd.ExecuteReader()            
    //    loop reader []
    //    |> (fun spectra -> if spectra.IsEmpty then failwith ("No enum with this RunID found") else spectra)
    //    |> (fun item -> item :> IEnumerable<Chromatogram>)

    ///Prepare function to insert MzQuantMLDocument-record.
    let prepareInsertMassSpectrum(cn:SQLiteConnection) =
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
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@runID"         ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@spectrumID"    ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@description"   ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakArray"     ,Data.DbType.String)    |> ignore
        cmd.Parameters.Add("@peakData"      ,Data.DbType.Binary)    |> ignore
        (fun (encoder:BinaryDataEncoder) (runID:string) (spectrum:MassSpectrum) (peaks:Peak1DArray) ->
            let encodedValues = encoder.Encode(peaks)
            cmd.Parameters.["@runID"].Value         <- runID
            cmd.Parameters.["@spectrumID"].Value    <- spectrum.ID
            cmd.Parameters.["@description"].Value   <- MzIOJson.MassSpectrumToJson(spectrum)
            cmd.Parameters.["@peakArray"].Value     <- MzIOJson.ToJson(peaks)
            cmd.Parameters.["@peakData"].Value      <- encodedValues
            cmd.ExecuteNonQuery() |> ignore
        )        

    /// Prepare function to select element of Description table of MzSQL.
    let prepareSelectMassSpectrum(cn:SQLiteConnection) =
        let queryString = "SELECT Description FROM Spectrum WHERE SpectrumID = @spectrumID"
        let cmd = new SQLiteCommand(queryString, cn)
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
    let prepareSelectMassSpectra(cn:SQLiteConnection) =
        let queryString = "SELECT Description FROM Spectrum WHERE RunID = @runID"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@runID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) acc =
            match reader.Read() with
            | true  -> loop reader (MzIOJson.deSerializeMassSpectrum(reader.GetString(0))::acc)
            | false -> acc 
        fun (id:string) ->
        cmd.Parameters.["@runID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader []
        |> (fun spectra -> if spectra.IsEmpty then failwith ("No enum with this RunID found") else spectra)
        |> (fun spectra -> spectra :> IEnumerable<MassSpectrum>)

    /// Prepare function to select elements of PeakArray and PeakData tables of MzSQL.
    let prepareSelectPeak1DArray(cn:SQLiteConnection) =
        let decoder = new BinaryDataDecoder()
        let queryString = "SELECT PeakArray, PeakData FROM Spectrum WHERE SpectrumID = @spectrumID"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@spectrumID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) peaks =
            match reader.Read() with
            | true  -> loop reader (decoder.Decode(reader.GetStream(1), MzIOJson.FromJson<Peak1DArray>(reader.GetString(0))))
            | false -> peaks 
        fun (id:string) ->
        cmd.Parameters.["@spectrumID"].Value <- id            
        use reader = cmd.ExecuteReader()       
        loop reader (new Peak1DArray())

    /// Prepare function to select elements of PeakArray and PeakData tables of MzSQL.
    let prepareSelectPeak2DArray(cn:SQLiteConnection) =
        let decoder = new BinaryDataDecoder()
        let queryString = "SELECT PeakArray, PeakData FROM Chromatogram WHERE ChromatogramID = @chromatogramID"
        let cmd = new SQLiteCommand(queryString, cn)
        cmd.Parameters.Add("@chromatogramID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) peaks =
            match reader.Read() with
            | true  -> loop reader (decoder.Decode(reader.GetStream(1), MzIOJson.FromJson<Peak2DArray>(reader.GetString(0))))
            | false -> peaks 
        fun (id:string) ->
        cmd.Parameters.["@chromatogramID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader (new Peak2DArray())

    /// Initialization of all prePareFunctions for the current connection.
    let insertModel             = prepareInsertModel(cn)
    let selectModel             = prepareSelectModel(cn)
    //let updateRunIDOfMzIOModel  = prepareUpdateRunIDOfMzIOModel(cn)
    let insertMassSpectrum      = prepareInsertMassSpectrum(cn)
    let selectMassSpectrum      = prepareSelectMassSpectrum(cn)
    let selectMassSpectra       = prepareSelectMassSpectra(cn)
    let selectPeak1DArray       = prepareSelectPeak1DArray(cn)
    //let insertChromatogram      = prepareInsertChromatogram(cn)
    //let selectChromatogram      = prepareSelectChromatogram(cn)
    //let selectChromatograms     = prepareSelectChromatograms(cn)
    let selectPeak2DArray       = prepareSelectPeak2DArray(cn)

    member this.Connection = cn

    member this.Open() = this.Connection.Open()

    member this.Close() = this.Connection.Close() 

    /// Initialization of all prePareFunctions for the current connection.
    member _.InsertModel             = insertModel            
    member _.SelectModel             = selectModel            
    //member _.UpdateRunIDOfMzIOModel  = updateRunIDOfMzIOModel 
    member _.InsertMassSpectrum      = insertMassSpectrum     
    member _.SelectMassSpectrum      = selectMassSpectrum     
    member _.SelectMassSpectra       = selectMassSpectra      
    member _.SelectPeak1DArray       = selectPeak1DArray      
    //member _.InsertChromatogram      = insertChromatogram     
    //member _.SelectChromatogram      = selectChromatogram     
    //member _.SelectChromatograms     = selectChromatograms    
    member _.SelectPeak2DArray       = selectPeak2DArray      


    member this.model =
        //let tr = this.Connection.BeginTransaction()
        let potMdoel = trySelectModel(cn)
        match potMdoel with
        | Some model    -> model
        | None          -> 
            let model = new MzIOModel(Path.GetFileNameWithoutExtension(sqlitePath))
            this.InsertModel model
            //tr.Commit()
            //tr.Dispose()
            //cn.Close()
            model

    /// Checks whether connection is disposed or not and fails when it is.
    member private this.RaiseDisposed() =

            if disposed then 
                raise (new ObjectDisposedException(this.GetType().Name))
            else 
                ()

    interface IMzIODataReader with

        /// Read all mass spectra of one run of MzSQL.
        member this.ReadMassSpectra(runID: string) =
            this.RaiseDisposed()
            this.SelectMassSpectra(runID)

        /// Read mass spectrum of MzSQL.
        member this.ReadMassSpectrum(spectrumID: string) =
            this.RaiseDisposed()
            this.SelectMassSpectrum(spectrumID)

        /// Read peaks of mass spectrum of MzSQL.
        member this.ReadSpectrumPeaks(spectrumID: string) =
            this.RaiseDisposed()
            this.SelectPeak1DArray(spectrumID)

        /// Read mass spectrum of MzSQL asynchronously.
        member this.ReadMassSpectrumAsync(spectrumID:string) =    
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadMassSpectrum(spectrumID)
                }
            //Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSpectrum(spectrumID))

        /// Read peaks of mass spectrum of MzSQL asynchronously.
        member this.ReadSpectrumPeaksAsync(spectrumID:string) =  
            let tmp = this :> IMzIODataReader
            async
                {
                    return tmp.ReadSpectrumPeaks(spectrumID)
                }
            //Task<Peak1DArray>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))

        /// Read all chromatograms of one run of MzSQL.
        member this.ReadChromatograms(runID: string) =
            this.RaiseDisposed()
            failwith "MethodNotImplemented"
            //this.SelectChromatograms(runID)

        /// Read chromatogram of MzSQL.
        member this.ReadChromatogram(chromatogramID: string) =
            this.RaiseDisposed()
            failwith "MethodNotImplemented"
            //this.SelectChromatogram(chromatogramID)

        /// Read peaks of chromatogram of MzSQL.
        member this.ReadChromatogramPeaks(chromatogramID: string) =
            this.RaiseDisposed()
            this.SelectPeak2DArray(chromatogramID)

        /// Read chromatogram of MzSQL asynchronously.
        member this.ReadChromatogramAsync(chromatogramID:string) =
           failwith "MethodNotImplemented"
           //async {return this.SelectChromatogram(chromatogramID)}
        
        /// Read peaks of chromatogram of MzSQL asynchronously.
        member this.ReadChromatogramPeaksAsync(chromatogramID:string) =
           async {return this.SelectPeak2DArray(chromatogramID)}

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
            this.InsertMassSpectrum encoder runID spectrum peaks

        member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            this.RaiseDisposed()
            failwith "MethodNotImplemented"
            //this.InsertChromatogram runID chromatogram peaks

        member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            async {return (this.Insert(runID, spectrum, peaks))}

        member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            async {return (this.Insert(runID, chromatogram, peaks))}

    /// Write runID, spectrum and peaks into MzSQL file.
    member this.Insert(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertMass(runID, spectrum, peaks)

    /// Write runID, chromatogram and peaks into MzSQL file.
    member this.Insert(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertChrom(runID, chromatogram, peaks)

    /// Write runID, spectrum and peaks into MzSQL file asynchronously.
    member this.InsertAsync(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertAsyncMass(runID, spectrum, peaks)

    /// Write runID, chromatogram and peaks into MzSQL file asynchronously.
    member this.InsertAsync(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertAsyncChrom(runID, chromatogram, peaks)
        
    interface IDisposable with

        /// Disposes everything and closes connection.
        member this.Dispose() =
            disposed <- true            
            cn.Dispose()

    /// Disposes everything and closes connection.
    member this.Dispose() =
        (this :> IDisposable).Dispose()


    interface IMzIOIO with

        /// Open connection to MzSQL data base.
        member this.BeginTransaction() =
            let tr = cn.BeginTransaction()
            new MzSQLTransactionScope(tr) :> ITransactionScope

        /// Creates MzIOModel based on global metadata in MzSQL or default model when no model was in the db.
        member this.CreateDefaultModel() =
            new MzIOModel(Path.GetFileNameWithoutExtension(sqlitePath))

        /// Saves in memory MzIOModel into the MzSQL data base.
        member this.SaveModel() =
            this.InsertModel this.Model
            
        /// Access MzIOModel in memory.
        member this.Model =
            this.model

    /// Open connection to MzSQL data base.
    member this.BeginTransaction() =
        cn.BeginTransaction()
        
    /// Creates model based on model in MzSQL or default model when no model was in the db.
    member this.CreateDefaultModel() =
        (this :> IMzIOIO).CreateDefaultModel()        

    /// Saves in memory MzIOModel into the MzSQL data base.
    member this.SaveModel() =
        (this :> IMzIOIO).SaveModel()
        
    /// Access MzIOModel in memory.
    member this.Model = 
        (this :> IMzIOIO).Model

    /// Updates the an MzIOModel by adding all values of the other MzIOModel.
    static member internal updateModel(oldModel:MzIOModel, newModel:MzIOModel) =
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
        newModel.FileDescription <- oldModel.FileDescription
        newModel

    /// Inserts runID, MassSpectra with corresponding Peak1DArrasy into datbase Spectrum table with chosen compression type for the peak data.
    member this.insertMSSpectrum (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectrum: MassSpectrum) = 
        let peakArray = reader.ReadSpectrumPeaks(spectrum.ID)
        let clonedP = new Peak1DArray(BinaryDataCompressionType.NoCompression, peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        this.Insert(runID, spectrum, clonedP)

    /// Modifies spectrum according to the used spectrumPeaksModifier and inserts the result into the MzSQL data base. 
    member this.insertModifiedSpectrumBy (spectrumPeaksModifierF: IMzIODataReader -> MassSpectrum -> BinaryDataCompressionType -> Peak1DArray) (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectrum: MassSpectrum) = 
        let modifiedP = spectrumPeaksModifierF reader spectrum compress
        this.Insert(runID, spectrum, modifiedP)

    /// Starts bulkinsert of mass spectra into a MzLiteSQL database
    member this.insertMSSpectraBy insertSpectrumF (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectra: seq<MassSpectrum>) = 
        let selectModel = prepareSelectModel(cn)
        let updateRunID = prepareUpdateRunIDOfMzIOModel(cn)
        let model = MzSQL.updateModel(selectModel(), reader.Model)
        updateRunID runID model
        let bulkInsert spectra = 
            spectra
            |> Seq.iter (insertSpectrumF runID reader compress)
        bulkInsert spectra

    member _.test = 
        printfn "test"
