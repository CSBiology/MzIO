namespace MzIO.SQLReader


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

module CMD =

    let mutable cmd = new SQLiteCommand()

open CMD

type MzIOSQLTransactionScope(connection: SQLiteConnection, transaction: SQLiteTransaction, commands: IDictionary<string, SQLiteCommand>) =
    
    let mutable disposed = false

    let mutable transaction = transaction

    new(connection) = new MzIOSQLTransactionScope(connection, connection.BeginTransaction(), new Dictionary<string, SQLiteCommand>())

    member private this.RaiseDisposed() =

            if disposed = true then printfn "%s" ((new ObjectDisposedException(this.GetType().Name)).ToString())

            else ()

    //member private this.connection = connection

    //member private this.transaction 
    //    with get() = transaction'
    //    and set(value) = transaction' <- value

    interface IDisposable with
        
        member this.Dispose() =
            if disposed then ()
            for cmd in commands.Values do
                cmd.Dispose()
            commands.Clear()

            if transaction <> null then
                transaction.Dispose()
            let mutable tmp = Some this
            MzIOSQL.ReleaseTransactionScope(& tmp)
            disposed <- true

    member this.Dispose() =
        (this :> IDisposable).Dispose()

    interface ITransactionScope with
        
        member this.Commit() =
            this.RaiseDisposed()
            transaction.Commit()

        member this.Rollback() = 
            this.RaiseDisposed()
            transaction.Rollback()

    member this.Commit() =
        (this :> ITransactionScope).Commit()

    member this.Rollback() =
        (this :> ITransactionScope).Rollback()

    member internal this.CreateCommand(commandText: string) =
        this.RaiseDisposed()
        let cmd = connection.CreateCommand()
        cmd.CommandText <- commandText
        cmd.Transaction <- transaction
        cmd

    member internal this.PrepareCommand(name: string, commandText: string) =
        this.RaiseDisposed()
        let cmd = this.CreateCommand(commandText)
        cmd.Prepare()
        commands.Remove name |> ignore
        commands.Add (name,cmd)
        cmd

    member internal this.TryGetCommand(name: string, cmd: byref<SQLiteCommand>) =

        this.RaiseDisposed()
        commands.TryGetValue(name, & cmd)


/// <summary>
/// The MzIO data reader/writer implementation for SQLite databases.
/// </summary>
and MzIOSQL(encoder:BinaryDataEncoder,decoder:BinaryDataDecoder, model:MzIOModel, currentScope:MzIOSQLTransactionScope option, sqlFilePath:string) =
    
    let mutable disposed = false

    let mutable currentScope = currentScope

    let mutable model = model

    //Added to improve speed
    //let mutable cmd = new SQLiteCommand()

    let path = 
        if String.IsNullOrWhiteSpace(sqlFilePath) then
            raise (ArgumentNullException("sqlFilePath"))
        else
            sqlFilePath

    let connection = MzIOSQL.GetConnection(path)
        
    let tmp =
        if not (File.Exists(sqlFilePath)) then
            File.Create(sqlFilePath) |> ignore
        else
        //let connection = MzIOSQL.GetConnection(sqlFilePath)
        MzIOSQL.SqlRunPragmas(connection)
        //let ex = new Exception()
        use (scope: ITransactionScope) = MzIOSQL.BeginTransaction(connection,& currentScope,& disposed)
        (
            //try
                MzIOSQL.SqlInitSchema(currentScope.Value)
                if not (MzIOSQL.SqlTrySelect(currentScope.Value, & model)) then
                    model<- MzIOSQL.CreateDefaultModel(path)
                    MzIOSQL.SqlSave(& cmd, model, & currentScope)
                scope.Commit()
            //with
                //| :? Exception -> 
                //    scope.Rollback()
                //    this
                //    //failwith (MzIOIOException.MzIOIOException(ex.Message, ex).ToString())
        )

    new(sqlFilePath:string) = new MzIOSQL(new BinaryDataEncoder(), new BinaryDataDecoder(), new MzIOModel(), None, sqlFilePath)

    static member private SqlInitSchema(currentScope:MzIOSQLTransactionScope) =
        use cmd = currentScope.CreateCommand("CREATE TABLE IF NOT EXISTS Model (Lock INTEGER  NOT NULL PRIMARY KEY DEFAULT(0) CHECK (Lock=0), Content TEXT NOT NULL)")
        cmd.ExecuteNonQuery() |> ignore
        use cmd = currentScope.CreateCommand("CREATE TABLE IF NOT EXISTS Spectrum (RunID TEXT NOT NULL, SpectrumID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)")
        cmd.ExecuteNonQuery() |> ignore
        use cmd = currentScope.CreateCommand("CREATE TABLE IF NOT EXISTS Chromatogram (RunID TEXT NOT NULL, ChromatogramID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)")
        cmd.ExecuteNonQuery() |> ignore

    static member private SqlTrySelect(currentScope:MzIOSQLTransactionScope, model:byref<MzIOModel>) =
        use cmd = currentScope.CreateCommand("SELECT Content FROM Model")
        (
            let content = cmd.ExecuteScalar()
            if (content :? string) then
                model <- MzIOJson.FromJson<MzIOModel>(content :?> string)
                true
                    
            else 
                model<- new MzIOModel()
                false
        )

    //member this.MzIOSQL() =
            
    //    //let connection = MzIOSQL.GetConnection(sqlFilePath)
    //    MzIOSQL.SqlRunPragmas(connection)
    //    //let ex = new Exception()
    //    use (scope: ITransactionScope) = this.BeginTransaction()
    //    (
    //        //try
    //            this.SqlInitSchema()
    //            if not (this.SqlTrySelect()) then
    //                model<- this.CreateDefaultModel()
    //                this.SqlSave(model)
    //            scope.Commit()
    //            this
    //        //with
    //            //| :? Exception -> 
    //            //    scope.Rollback()
    //            //    this
    //            //    //failwith (MzIOIOException.MzIOIOException(ex.Message, ex).ToString())
    //    )

        
    static member private SqlSave(cmd:byref<SQLiteCommand>, model:MzIOModel, currentScope:byref<MzIOSQLTransactionScope option>) =

        cmd <- currentScope.Value.CreateCommand("DELETE FROM Model")
        (
            cmd.ExecuteNonQuery() |> ignore
        )
        use cmd = currentScope.Value.CreateCommand("INSERT INTO Model VALUES(@lock, @content)")
        (
            cmd.Parameters.AddWithValue("@lock", 0) |> ignore
            cmd.Parameters.AddWithValue("@content", MzIOJson.ToJson(model)) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        )
  
    static member private RaiseDisposed(disposed:byref<bool>) =

            if disposed = true then 
                raise (new ObjectDisposedException("MzIOSQL"))
            else ()

    member private this.RaiseDisposed() =

            if disposed = true then printfn "%s" ((new ObjectDisposedException(this.GetType().Name)).ToString())

            else ()

    member private this.IsOpenScope = currentScope.IsSome

    interface IDisposable with
        
        member this.Dispose() =
            if disposed then ()
            if currentScope.IsSome then
                currentScope.Value.Dispose()
            if connection <> null then
                connection.Dispose()
            disposed <- true

    member this.Dispose() =
            
        (this :> IDisposable).Dispose()

    interface IMzIOIO with
        
        member this.BeginTransaction() =

            this.RaiseDisposed()

            currentScope <- Some (new MzIOSQLTransactionScope(connection))
            currentScope.Value :> ITransactionScope

        member this.Model = 
            this.RaiseDisposed()
            model

        member this.SaveModel() =
            this.RaiseDisposed()
            //function omitted
            this.RaiseNotInScope() //does nothing
            MzIOSQL.SqlSave(& cmd, model, & currentScope)

        member this.CreateDefaultModel() =
            this.RaiseDisposed()
            new MzIOModel(Path.GetFileNameWithoutExtension(path))

    static member internal ReleaseTransactionScope(currentScope:byref<MzIOSQLTransactionScope option>) =

        currentScope <- None

    member private this.RaiseNotInScope() =
        if not (this.IsOpenScope) then
            failwith "No transaction scope was initialized."

    member this.CreateDefaultModel() =

        (this :> IMzIOIO).CreateDefaultModel()

    static member CreateDefaultModel(path) =

        new MzIOModel(Path.GetFileNameWithoutExtension(path))

    member this.Model =

        (this :> IMzIOIO).Model

    member this.BeginTransaction() =

        (this :> IMzIOIO).BeginTransaction()

    static member BeginTransaction(connection:SQLiteConnection, currentScope:byref<MzIOSQLTransactionScope option>, disposed:byref<bool>) =

        MzIOSQL.RaiseDisposed(& disposed)
        currentScope <- Some (new MzIOSQLTransactionScope(connection))
        currentScope.Value :> ITransactionScope

    static member GetConnection(path:string) =
        let conn = new SQLiteConnection(String.Format("DataSource={0}", path), true)
        if not (conn.State=ConnectionState.Open) then
            conn.Open()
        conn

    static member SqlRunPragmas(conn:SQLiteConnection) =
        use cmd = conn.CreateCommand()
        (
            cmd.CommandText <- "PRAGMA synchronous=OFF"
            cmd.ExecuteNonQuery() |> ignore
            cmd.CommandText <- "PRAGMA journal_mode=MEMORY"
            cmd.ExecuteNonQuery() |> ignore
            cmd.CommandText <- "PRAGMA temp_store=MEMORY"
            cmd.ExecuteNonQuery() |> ignore
            cmd.CommandText <- "PRAGMA ignore_check_constraints=OFF"
            cmd.ExecuteNonQuery() |> ignore
        )

    member private this.SqlInsert(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        
        //let mutable cmd = new SQLiteCommand()
        if not (currentScope.Value.TryGetCommand("INSERT_CHROMATOGRAM_CMD", & cmd)) then
            cmd <- currentScope.Value.PrepareCommand("INSERT_CHROMATOGRAM_CMD", "INSERT INTO Chromatogram VALUES(@runID, @chromatogramID, @description, @peakArray, @peakData)")
        cmd.Parameters.Clear()                                                      
        cmd.Parameters.AddWithValue("@runID", runID)                                 |> ignore
        cmd.Parameters.AddWithValue("@chromatogramID", chromatogram.ID)              |> ignore
        cmd.Parameters.AddWithValue("@description", MzIOJson.ToJson(chromatogram)) |> ignore
        cmd.Parameters.AddWithValue("@peakArray", MzIOJson.ToJson(peaks))          |> ignore
        cmd.Parameters.AddWithValue("@peakData", encoder.Encode(peaks))              |> ignore
        cmd.ExecuteNonQuery()                                                        |> ignore

    member private this.SqlInsert(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        
        //let mutable cmd = new SQLiteCommand()
        
        if not (currentScope.Value.TryGetCommand("INSERT_SPECTRUM_CMD", & cmd)) then
            cmd <- currentScope.Value.PrepareCommand("INSERT_SPECTRUM_CMD", "INSERT INTO Spectrum VALUES(@runID, @spectrumID, @description, @peakArray, @peakData)")
        cmd.Parameters.Clear()                                                      
        cmd.Parameters.AddWithValue("@runID", runID)                                |> ignore
        cmd.Parameters.AddWithValue("@spectrumID", spectrum.ID)                     |> ignore
        cmd.Parameters.AddWithValue("@description", MzIOJson.ToJson(spectrum))    |> ignore
        cmd.Parameters.AddWithValue("@peakArray", MzIOJson.ToJson(peaks))         |> ignore
        cmd.Parameters.AddWithValue("@peakData", encoder.Encode(peaks))             |> ignore
        cmd.ExecuteNonQuery()                                                       |> ignore


    //member private this.SqlInsert() =

    //    if not (currentScope.Value.TryGetCommand("INSERT_SPECTRUM_CMD", & cmd)) then
    //        cmd <- currentScope.Value.PrepareCommand("INSERT_SPECTRUM_CMD", "INSERT INTO Spectrum VALUES(@runID, @spectrumID, @description, @peakArray, @peakData)")       
    //    cmd.Parameters.Clear()
    //    cmd.Parameters.Add("@runID"         ,Data.DbType.String)   |> ignore
    //    cmd.Parameters.Add("@spectrumID"    ,Data.DbType.String)   |> ignore
    //    cmd.Parameters.Add("@description"   ,Data.DbType.String)   |> ignore
    //    cmd.Parameters.Add("@peakArray"     ,Data.DbType.String)   |> ignore
    //    cmd.Parameters.Add("@peakData"      ,Data.DbType.Binary)   |> ignore
    //    (fun (runID:string) (spectrum:MassSpectrum) (peaks:Peak1DArray) ->
    //        cmd.Parameters.["@runID"].Value         <- runID
    //        cmd.Parameters.["@spectrumID"].Value    <- spectrum.ID
    //        cmd.Parameters.["@description"].Value   <- MzIOJson.ToJson(spectrum)
    //        cmd.Parameters.["@peakArray"].Value     <- MzIOJson.ToJson(peaks)
    //        cmd.Parameters.["@peakData"].Value      <- encoder.Encode(peaks)
    //        cmd.ExecuteNonQuery()   |> ignore
    //    )

    static member SafeGetString (reader: SQLiteDataReader, colIndex: int)= 
        if not (reader.IsDBNull colIndex) then
            reader.GetString colIndex
        else String.Empty

    member private this.SqlSelectMassSpectra(runID: string) =
        //use cmd = currentScope.Value.CreateCommand("SELECT Description FROM Spectrum WHERE RunID = @runID")
        //(
        //    cmd.Parameters.AddWithValue("@runID", runID) |> ignore
        //    use reader = cmd.ExecuteReader()
        //    (
        //        seq{
        //            while reader.Read() do
        //                yield MzIOJson.FromJson<MassSpectrum>(reader.GetString(0))
        //           }    
        //           |> List.ofSeq
        //           |> (fun item -> item :> IEnumerable<MassSpectrum>)
        //    )
        //    //(
        //    //    let rec loop acc =

        //    //        if reader.Read() = false then
        //    //            (List.rev acc) :> IEnumerable<MassSpectrum>
        //    //        else 
        //    //            //loop (MzIOJson.FromJson<MassSpectrum>(reader.GetString(0))::acc)
        //    //            loop (MzIOJson.FromJson<MassSpectrum>(MzIOSQL.SafeGetString (reader, 0))::acc)
        //    //    loop []     
        //    //)
        //)
        let tmp =
            use cmd = currentScope.Value.CreateCommand("SELECT Description FROM Spectrum WHERE RunID = @runID")
            (
                cmd.Parameters.AddWithValue("@runID", runID) |> ignore
                use reader = cmd.ExecuteReader()
                (
                    seq{
                        while reader.Read() do
                            yield MzIOJson.FromJson<MassSpectrum>(reader.GetString(0))
                       }    
                       |> List.ofSeq
                       |> (fun item -> item :> IEnumerable<MassSpectrum>)
                )
            )
        tmp

    member private this.SqlTrySelect(spectrumID: string, ms: byref<MassSpectrum>) =

        //let mutable cmd = new SQLiteCommand()
        if not (currentScope.Value.TryGetCommand("SELECT_SPECTRUM_CMD", & cmd)) then
            cmd <- currentScope.Value.PrepareCommand("SELECT_SPECTRUM_CMD", "SELECT Description FROM Spectrum WHERE SpectrumID = @spectrumID")
        else
            cmd.Parameters.Clear()
        cmd.Parameters.AddWithValue("@spectrumID", spectrumID) |> ignore
        let desc = cmd.ExecuteScalar()
        if (desc :? string) then
            ms <- MzIOJson.FromJson<MassSpectrum>(desc :?> string)
            true
        else
            ms <- new MassSpectrum()
            false

    member private this.SqlTrySelect(spectrumID: string, peaks: byref<Peak1DArray>) =

        //let mutable cmd = new SQLiteCommand()
        if not (currentScope.Value.TryGetCommand("SELECT_SPECTRUM_PEAKS_CMD", & cmd)) then
            cmd <- currentScope.Value.PrepareCommand("SELECT_SPECTRUM_PEAKS_CMD", "SELECT PeakArray, PeakData FROM Spectrum WHERE SpectrumID = @spectrumID")
        else
            cmd.Parameters.Clear()
        cmd.Parameters.AddWithValue("@spectrumID", spectrumID) |> ignore
        use reader = cmd.ExecuteReader()
        (
            if reader.Read() then
                peaks <- MzIOJson.FromJson<Peak1DArray>(reader.GetString(0))
                decoder.Decode(reader.GetStream(1), peaks) |> ignore
                true
            else
                peaks <- new Peak1DArray()
                false
        )

    member private this.SqlTrySelect(chromatogramID: string, chromatogram: byref<Chromatogram>) =
        //let mutable cmd = new SQLiteCommand()
        if not (currentScope.Value.TryGetCommand("SELECT_CHROMATOGRAM_CMD", & cmd)) then
            cmd <- currentScope.Value.PrepareCommand("SELECT_CHROMATOGRAM_CMD", "SELECT Description FROM Chromatogram WHERE ChromatogramID = @chromatogramID")
        else
            cmd.Parameters.Clear()
        cmd.Parameters.AddWithValue("@chromatogramID", chromatogramID) |> ignore
        let desc = cmd.ExecuteScalar()
        if (desc :? string) then
            chromatogram <- MzIOJson.FromJson<Chromatogram>(desc :?> string)
            true
        else
            chromatogram <- new Chromatogram()
            false

    member private this.SqlTrySelect (chromatogramID: string, peaks: byref<Peak2DArray>) =
        //let mutable cmd = new SQLiteCommand()
        if not (currentScope.Value.TryGetCommand("SELECT_CHROMATOGRAM_PEAKS_CMD", & cmd)) then
            cmd <- currentScope.Value.PrepareCommand("SELECT_CHROMATOGRAM_PEAKS_CMD", "SELECT PeakArray, PeakData FROM Chromatogram WHERE ChromatogramID = @chromatogramID")
        else
            cmd.Parameters.Clear()
        cmd.Parameters.AddWithValue("@chromatogramID", chromatogramID) |> ignore
        use reader = cmd.ExecuteReader()
        (
            if reader.Read() then
                peaks <- MzIOJson.FromJson<Peak2DArray>(reader.GetString(0))
                decoder.Decode(reader.GetStream(1), peaks) |> ignore
                true
            else
                peaks <- new Peak2DArray()
                false
        )

    member private this.SqlSelectChromatograms(runID: string) =
        use cmd = currentScope.Value.CreateCommand("SELECT Description FROM Chromatogram WHERE RunID = @runID")
        (
            cmd.Parameters.AddWithValue("@runID", runID) |> ignore
            use reader = cmd.ExecuteReader()
            (
                let rec loop acc =

                    if reader.Read() = false then
                        (List.rev acc) :> IEnumerable<Chromatogram>
                    else 
                        loop (MzIOJson.FromJson<Chromatogram>(reader.GetString(0))::acc)
                loop []     
            )
        )

//    #region IMzIODataWriter Members

    interface IMzIODataWriter with

        member this.InsertMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            this.RaiseDisposed()
            this.RaiseNotInScope()
            this.SqlInsert(runID, spectrum, peaks)

        member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            this.RaiseDisposed()
            this.RaiseNotInScope()
            this.SqlInsert(runID, chromatogram, peaks)

        member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            async {return (this.Insert(runID, spectrum, peaks))}

        member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            async {return (this.Insert(runID, chromatogram, peaks))}

    //member this.PrepareInsert runID spectrum peaks = 
    //    this.SqlInsert() runID spectrum peaks

    member this.Insert(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertMass(runID, spectrum, peaks)

    member this.Insert(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertChrom(runID, chromatogram, peaks)

    member this.InsertAsync(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertAsyncMass(runID, spectrum, peaks)

    member this.InsertAsync(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertAsyncChrom(runID, chromatogram, peaks)

    interface IMzIODataReader with

        member this.ReadMassSpectra(runID: string) =
            this.RaiseDisposed()
            this.RaiseNotInScope()
            this.SqlSelectMassSpectra(runID)

        member this.ReadMassSpectrum(spectrumID: string) =
            this.RaiseDisposed()
            this.RaiseNotInScope()
            let mutable ms = new MassSpectrum()
            if (this.SqlTrySelect(spectrumID, & ms)) then ms
            else 
                failwith (String.Format("Spectrum for id '{0}' not found.", spectrumID))

        member this.ReadSpectrumPeaks(spectrumID: string) =
            this.RaiseDisposed()
            this.RaiseNotInScope()
            let mutable peaks = new Peak1DArray()
            if this.SqlTrySelect(spectrumID, & peaks) then peaks
            else 
                failwith (String.Format("Spectrum with id '{0}' not found.", spectrumID))

        member this.ReadMassSpectrumAsync(spectrumID:string) =        
            //let tmp = this :> IMzIODataReader
            //async
            //    {
            //        return tmp.ReadMassSpectrum(spectrumID)
            //    }

            Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSpectrum(spectrumID))

        member this.ReadSpectrumPeaksAsync(spectrumID:string) =            
            //let tmp = this :> IMzIODataReader
            //async
            //    {
            //        return tmp.ReadSpectrumPeaks(spectrumID)
            //    }

            Task<Peak1DArray>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))

        member this.ReadChromatograms(runID: string) =
            this.RaiseDisposed()
            this.RaiseNotInScope()
            this.SqlSelectChromatograms(runID)

        member this.ReadChromatogram(chromatogramID: string) =
            this.RaiseDisposed()
            this.RaiseNotInScope()
            let mutable ch = new Chromatogram()
            if this.SqlTrySelect(chromatogramID, & ch) then ch
            else
                failwith (String.Format("Chromatogram for id '{0}' not found.", chromatogramID))

        member this.ReadChromatogramPeaks(chromatogramID: string) =
            this.RaiseDisposed()
            this.RaiseNotInScope()
            let mutable peaks = new Peak2DArray()
            if this.SqlTrySelect(chromatogramID, & peaks) then peaks
            else
                failwith (String.Format("Chromatogram for id '{0}' not found.", chromatogramID))

        member this.ReadChromatogramAsync(spectrumID:string) =
           async {return this.ReadChromatogram(spectrumID)}
        
        member this.ReadChromatogramPeaksAsync(spectrumID:string) =
           async {return this.ReadChromatogramPeaks(spectrumID)}

    member this.ReadMassSpectra(runID: string) =
        (this :> IMzIODataReader).ReadMassSpectra(runID)

    member this.ReadMassSpectrum(spectrumID: string) =
        (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

    member this.ReadSpectrumPeaks(spectrumID: string) =
        (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

    member this.ReadMassSpectrumAsync(spectrumID: string) =
        (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

    member this.ReadSpectrumPeaksAsync(spectrumID: string) =
        (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

    member this.ReadChromatograms(runID: string) =
        (this :> IMzIODataReader).ReadChromatograms(runID)

    member this.ReadChromatogram(spectrumID: string) =
        (this :> IMzIODataReader).ReadChromatogram(spectrumID)

    member this.ReadChromatogramPeaks(spectrumID: string) =
        (this :> IMzIODataReader).ReadChromatogramPeaks(spectrumID)

    member this.ReadChromatogramAsync(spectrumID: string) =
        (this :> IMzIODataReader).ReadChromatogramAsync(spectrumID)

    member this.ReadChromatogramPeaksAsync(spectrumID: string) =
        (this :> IMzIODataReader).ReadChromatogramPeaksAsync(spectrumID)
