
#r @"../MzIO.SQL\bin\Release\net45/System.Data.SQLite.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Muni.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Data.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Data.CommonInterfaces.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Data.AnalystDataProvider.dll"
#r @"../MzIO.Wiff\bin\Release\net45/Newtonsoft.Json.dll"
#r @"../MzIO.Wiff\bin\Release\net45/MzIO.dll"
#r @"../MzIO.Wiff\bin\Release\net45\MzIO.Wiff.dll"
#r @"../MzIO.SQL\bin\Release\net45\MzIO.SQL.dll"
#r @"../MzIO.Processing\bin\Release\net45\MzIO.Processing.dll"
#r @"../MzIO.Bruker\bin\Release\net45\MzIO.Bruker.dll"
#r @"../MzIO.MzML\bin\Release\net45\MzIO.MzML.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.BackgroundSubtraction.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.Data.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.MassPrecisionEstimator.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.RawFileReader.dll"
#r @"../MzIO.Thermo\bin\Release\net451\MzIO.Thermo.dll"


open System
open System.Data
open System.Data.SQLite
open System.IO
open System.Threading.Tasks
open System.Xml
open System.Collections.Generic
open System.Runtime.InteropServices
open System.IO.Compression
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open MzIO.Binary
open MzIO.Wiff
open MzIO.SQLReader
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.PSIMSExtension
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.MetaData.UO.UO
open MzIO.Processing.MzIOLinq
open MzIO.Json
open MzIO.Bruker
open MzIO.IO.MzML
open MzIO.IO.MzML.MzML
open MzIO.IO
open MzIO.Thermo


let fileDir             = __SOURCE_DIRECTORY__
let licensePath         = @"C:\Users\Student\source\repos\MzLiteFSharp\src\MzLiteFSharp.Wiff\License\Clearcore2.license.xml"
let licenseHome         = @"C:\Users\Patrick\source\repos\MzLiteFSharp\src\MzLiteFSharp.Wiff\License\Clearcore2.license.xml"

let wiffTestFileStudent = @"C:\Users\Student\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\wiffTestFiles\20171129 FW LWagg001.wiff"
let mzIOFileStudent     = @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff.mzIO"

let jonMzIO             = @"C:\Users\jonat\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\test180807_Cold1_2d_GC8_01_8599.mzIO"
let jonWiff             = @"C:\Users\jonat\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\20180301_MS_JT88mutID122.wiff"

let wiffTestPaeddetor   = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff"
let paddeTestPath       = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff.mzIO"

let mzIOFSharpDBPath    = @"C:\Users\Student\source\repos\wiffTestFiles\Databases\MzLiteFSHarpLWagg001.mzIO"


type MzIOHelper =
    {
        RunID           : string
        MassSpectrum    : seq<MzIO.Model.MassSpectrum>
        Peaks           : seq<Peak1DArray>
        Path            : string
    }

let createMzIOHelper (runID:string) (path:string) (spectrum:seq<MzIO.Model.MassSpectrum>) (peaks:seq<Peak1DArray>) =
    {
        MzIOHelper.RunID          = runID
        MzIOHelper.MassSpectrum   = spectrum
        MzIOHelper.Peaks          = peaks
        MzIOHelper.Path           = path
    }

//let wiffFilePaths =
//    [
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg002.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg003.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg004.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg005.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg006.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg007.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg008.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg009.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg010.wiff"
//        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg011.wiff"
//        //@"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg012.wiff"
//    ]

let getWiffFileReader (path:string) =
    new WiffFileReader(path, licensePath)

let getMassSpectra (wiffFileReader:WiffFileReader) =
    wiffFileReader.Model.Runs.GetProperties false
    |> Seq.collect (fun (run:KeyValuePair<string, obj>) -> wiffFileReader.ReadMassSpectra run.Key)

let getPeak1DArrays (wiffFileReader:WiffFileReader) =
    getMassSpectra wiffFileReader
    |> Seq.map (fun spectrum -> wiffFileReader.ReadSpectrumPeaks spectrum.ID)

let getMzIOHelper (path:string) (compressionType:BinaryDataCompressionType) =
    let wiffFileReader = new WiffFileReader(path, licensePath)
    let runIDMassSpectra =
        wiffFileReader.Model.Runs.GetProperties false
        |> Seq.map (fun (run:KeyValuePair<string, obj>) -> run.Key, wiffFileReader.ReadMassSpectra run.Key)
    let tmp =
        runIDMassSpectra
        |> Seq.map (fun (runID, massSpectra) ->
            massSpectra
            |> Seq.map (fun spectrum -> (wiffFileReader.ReadSpectrumPeaks spectrum.ID))
            |> Seq.map (fun peak -> peak.CompressionType <- compressionType
                                    peak)
            |> createMzIOHelper runID path massSpectra
            )
        |> Seq.head
    tmp
    
let insertWholeFileIntoDB (helper:MzIOHelper) =
    let mzIOSQL = new MzIOSQL(helper.Path + ".mzIO")
    let bn = mzIOSQL.BeginTransaction()
    Seq.map2 (fun (spectrum:MzIO.Model.MassSpectrum) (peak:Peak1DArray) -> mzIOSQL.Insert(helper.RunID, spectrum, peak)) helper.MassSpectrum helper.Peaks
    |> Seq.length |> ignore
    bn.Commit()
    bn.Dispose()
  
let insertIntoDB (amount:int) (helper:MzIOHelper) =
    let mzIOSQL = new MzIOSQL(helper.Path + ".mzIO")
    let bn = mzIOSQL.BeginTransaction()
    Seq.map2 (fun (spectrum:MzIO.Model.MassSpectrum) (peak:Peak1DArray) -> mzIOSQL.Insert(helper.RunID, spectrum, peak)) ((*Seq.take amount*) helper.MassSpectrum) ((*Seq.take amount*) helper.Peaks)
    |> Seq.length |> ignore
    bn.Commit()
    bn.Dispose()

let getSpectrum (path:string) (spectrumID:string) =
    let mzIOSQL = new MzIOSQL(path)
    let bn = mzIOSQL.BeginTransaction()
    mzIOSQL.ReadMassSpectrum spectrumID

let getSpectra (path:string) (helper:MzIOHelper) =
    let mzIOSQL = new MzIOSQL(path)
    let bn = mzIOSQL.BeginTransaction()
    let tmp = 
        mzIOSQL.ReadMassSpectra helper.RunID
        |> List.ofSeq
    bn.Commit()
    bn.Dispose()
    tmp

let getSpectrumPeaks (path:string) (spectrumID:string) =

    let mzIOSQL = new MzIOSQL(path)

    let bn = mzIOSQL.BeginTransaction()

    mzIOSQL.ReadSpectrumPeaks spectrumID

/// Create a new file instance of the DB schema. DELETES already existing instance
let initDB filePath =
    let _ = System.IO.File.Delete filePath  
    let db = new MzIOSQL(filePath)
    db

/// Returns the conncetion string to a existing MzLiteSQL DB
let getConnection filePath =
    match System.IO.File.Exists filePath with
    | true  -> let db = new MzIOSQL(filePath)
               db 
    | false -> initDB filePath

/// copies MassSpectrum into DB schema
let insertMSSpectrum (db: MzIOSQL) runID (reader:IMzIODataReader) (compress: string) (spectrum: MassSpectrum)= 
    let peakArray = reader.ReadSpectrumPeaks(spectrum.ID)
    match compress with 
    | "NoCompression"  -> 
        let clonedP = new Peak1DArray(BinaryDataCompressionType.NoCompression,peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        db.Insert(runID, spectrum, clonedP)
    | "ZLib" -> 
        let clonedP = new Peak1DArray(BinaryDataCompressionType.ZLib,peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        db.Insert(runID, spectrum, clonedP)
    | "NumPress" ->
        let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPress,peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        db.Insert(runID, spectrum, clonedP)
    | "NumPressZLib" ->
        let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPressZLib,peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        db.Insert(runID, spectrum, clonedP)
    | _ ->
        failwith "Not a valid compression Method"

/// Starts bulkinsert of mass spectra into a MzLiteSQL database
let insertMSSpectraBy insertSpectrumF outFilepath runID (reader:IMzIODataReader) (compress: string) (spectra: seq<MassSpectrum>) = 
    let db = getConnection outFilepath
    let bulkInsert spectra = 
        spectra
        |> Seq.iter (insertSpectrumF db runID reader compress)
    let trans = db.BeginTransaction()
    bulkInsert spectra
    trans.Commit()
    trans.Dispose() 
    db.Dispose()

let prepareSqlSelectMassSpectra (cn:SQLiteConnection) (tr:SQLiteTransaction) =
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
    fun id ->
    cmd.Parameters.["@runID"].Value <- id            
    use reader = cmd.ExecuteReader()            
    loop reader Seq.empty
    |> List.ofSeq
    |> (fun item -> item :> IEnumerable<MassSpectrum>)

let prepareSelectPeak1DArray (cn:SQLiteConnection) (tr:SQLiteTransaction) =
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
    
let selectPeak1DArrays (runID:string) path =
    let cn = new SQLiteConnection(sprintf "Data Source=%s;Version=3" path)
    cn.Open()
    let tr = cn.BeginTransaction()
    let selectSpectra = prepareSqlSelectMassSpectra cn tr
    let selectSQLPeak1DArray = prepareSelectPeak1DArray cn tr
    selectSpectra runID
    |> Seq.map (fun spectrum -> selectSQLPeak1DArray spectrum.ID)

let selectModel (cn:SQLiteConnection) (tr:SQLiteTransaction) =
    let querySelect = "SELECT Content FROM Model WHERE Lock = 0"
    let cmdSelect = new SQLiteCommand(querySelect, cn, tr)
    let selectReader = cmdSelect.ExecuteReader()
    let rec loopSelect (reader:SQLiteDataReader) model =
        match reader.Read() with
        | true  -> loopSelect reader (MzIOJson.deSerializeMzIOModel(reader.GetString(0)))
        | false -> model           
    loopSelect selectReader (new MzIOModel())

let prepareUpdateRunIDOfMzIOModel (cn:SQLiteConnection) (tr:SQLiteTransaction) =
    let querySelect = "SELECT Content FROM Model WHERE Lock = 0"
    let cmdSelect = new SQLiteCommand(querySelect, cn, tr)
    let selectReader = cmdSelect.ExecuteReader()
    let model =
        let rec loopSelect (reader:SQLiteDataReader) model =
            match reader.Read() with
            | true  -> loopSelect reader (MzIOJson.deSerializeMzIOModel(reader.GetString(0)))
            | false -> model           
        loopSelect selectReader (new MzIOModel())
    fun runID ->
        if model.Runs.TryAdd(runID, new Run(runID)) then
            let jsonModel = MzIOJson.ToJson(model)
            let queryUpdate = sprintf "UPDATE Model SET Content = '%s' WHERE Lock = 0" jsonModel
            let cmdUpdate = new SQLiteCommand(queryUpdate, cn, tr)
            cmdUpdate.ExecuteNonQuery() |> ignore
            //tr.Commit()
        else
            ()

///Prepare function to insert MzQuantMLDocument-record.
let prepareInsertMassSpectrum (cn:SQLiteConnection) (tr:SQLiteTransaction) =
    let encoder = new BinaryDataEncoder()
    let updateRunID = prepareUpdateRunIDOfMzIOModel cn tr
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
    (fun (runID:string) (spectrum:MassSpectrum) (peaks:Peak1DArray) ->
        updateRunID runID
        cmd.Parameters.["@runID"].Value         <- runID
        cmd.Parameters.["@spectrumID"].Value    <- spectrum.ID
        cmd.Parameters.["@description"].Value   <- MzIOJson.ToJson(spectrum)
        cmd.Parameters.["@peakArray"].Value     <- MzIOJson.ToJson(peaks)
        cmd.Parameters.["@peakData"].Value      <- encoder.Encode(peaks)
        cmd.ExecuteNonQuery() |> ignore
    )

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

type MzSQL(path) =

    let mutable disposed = false

    let encoder = new BinaryDataEncoder()

    let sqlitePath = 
        if String.IsNullOrWhiteSpace(path) then
                raise (ArgumentNullException("sqlitePath"))
            else
                path

    let mutable cn = new SQLiteConnection(sprintf "Data Source=%s;Version=3" sqlitePath)

    let createFile =
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
            if model.Runs.TryAdd(runID, new Run()) then
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

    member private this.InsertMassSpectrum =
        insertMassSpectrum

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

    member private this.SelectMassSpectrum =
        selectMassSpectrum

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

    member private this.SelectPeak1DArray =
        selectPeak1DArray

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
       
    member private this.InsertChromatogram =
        insertChromatogram

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

    member private this.SelectChromatogram =
        selectChromatogram

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

    member private this.SelectChromatograms =
        selectChromatograms

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

    member private this.SelectPeak2DArray =
        selectPeak2DArray

    interface IMzIODataReader with

        member this.ReadMassSpectra(runID: string) =
            this.RaiseDisposed()
            selectMassSpectra(runID)

        member this.ReadMassSpectrum(spectrumID: string) =
            this.RaiseDisposed()
            this.SelectMassSpectrum(spectrumID)

        member this.ReadSpectrumPeaks(spectrumID: string) =
            this.RaiseDisposed()
            this.SelectPeak1DArray(spectrumID)

        member this.ReadMassSpectrumAsync(spectrumID:string) =        
            Task<MzIO.Model.MassSpectrum>.Run(fun () -> this.ReadMassSpectrum(spectrumID))

        member this.ReadSpectrumPeaksAsync(spectrumID:string) =            
            Task<Peak1DArray>.Run(fun () -> this.ReadSpectrumPeaks(spectrumID))

        member this.ReadChromatograms(runID: string) =
            this.RaiseDisposed()
            this.SelectChromatograms(runID)

        member this.ReadChromatogram(chromatogramID: string) =
            this.RaiseDisposed()
            this.SelectChromatogram(chromatogramID)

        member this.ReadChromatogramPeaks(chromatogramID: string) =
            this.RaiseDisposed()
            this.SelectPeak2DArray(chromatogramID)

        member this.ReadChromatogramAsync(chromatogramID:string) =
           async {return this.SelectChromatogram(chromatogramID)}
        
        member this.ReadChromatogramPeaksAsync(chromatogramID:string) =
           async {return this.SelectPeak2DArray(chromatogramID)}

    member this.ReadMassSpectra(runID: string) =
            (this :> IMzIODataReader).ReadMassSpectra(runID)

    member this.ReadMassSpectrum(spectrumID: string) =
        (this :> IMzIODataReader).ReadMassSpectrum(spectrumID)

    member this.ReadSpectrumPeaks(spectrumID: string) =
        (this :> IMzIODataReader).ReadSpectrumPeaks(spectrumID)

    member this.ReadMassSpectrumAsync(spectrumID:string) =        
        (this :> IMzIODataReader).ReadMassSpectrumAsync(spectrumID)

    member this.ReadSpectrumPeaksAsync(spectrumID:string) =            
        (this :> IMzIODataReader).ReadSpectrumPeaksAsync(spectrumID)

    member this.ReadChromatograms(runID: string) =
        (this :> IMzIODataReader).ReadChromatograms(runID)

    member this.ReadChromatogram(chromatogramID: string) =
        (this :> IMzIODataReader).ReadChromatogram(chromatogramID)

    member this.ReadChromatogramPeaks(chromatogramID: string) =
        (this :> IMzIODataReader).ReadChromatogramPeaks(chromatogramID)

    member this.ReadChromatogramAsync(chromatogramID:string) =
        (this :> IMzIODataReader).ReadChromatogramAsync(chromatogramID)
        
    member this.ReadChromatogramPeaksAsync(chromatogramID:string) =
        (this :> IMzIODataReader).ReadChromatogramPeaksAsync(chromatogramID)

    interface IMzIODataWriter with

        member this.InsertMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            this.RaiseDisposed()
            this.InsertMassSpectrum encoder runID spectrum peaks

        member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            this.RaiseDisposed()
            this.InsertChromatogram runID chromatogram peaks

        member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
            async {return (this.InsertMass(runID, spectrum, peaks))}

        member this.InsertAsyncChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
            async {return (this.InsertChrom(runID, chromatogram, peaks))}

    member this.InsertMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertMass(runID, spectrum, peaks)

    member this.InsertChrom(runID: string, chromatogram: Chromatogram, peaks: Peak2DArray) =
        (this :> IMzIODataWriter).InsertChrom(runID, chromatogram, peaks)

    member this.InsertAsyncMass(runID: string, spectrum: MassSpectrum, peaks: Peak1DArray) =
        (this :> IMzIODataWriter).InsertAsyncMass(runID, spectrum, peaks)

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

    /// Starts bulkinsert of mass spectra into a MzLiteSQL database
    member this.insertMSSpectraBy insertSpectrumF (runID:string) (reader:IMzIODataReader) (compress: BinaryDataCompressionType) (spectra: seq<MassSpectrum>) = 
        let selectModel = MzSQL.prepareSelectModel(cn, &tr)
        let updateRunID = MzSQL.prepareUpdateRunIDOfMzIOModel(cn, &tr)
        updateRunID runID (selectModel())
        let bulkInsert spectra = 
            spectra
            |> Seq.iter (insertSpectrumF runID reader compress)
        bulkInsert spectra
        this.Commit()
        this.Dispose()
        this.Close()


#time
let rand = new System.Random()

let swap (a: _[]) x y =
    let tmp = a.[x]
    a.[x] <- a.[y]
    a.[y] <- tmp

// shuffle an array (in-place)
let shuffle a =
    Array.iteri (fun i _ -> swap a i (rand.Next(i, Array.length a))) a


let wiffTestUni     = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.wiff"
let wiffTestHome    = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff"
let mzMLOfWiffUni   = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.mzML"

let bafTestUni      = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.d\analysis.baf"
let bafTestHome     = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\BafTestFiles\analysis.baf"
let bafMzMLFile     = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.mzML"

let thermoUni       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.RAW"
let thermoHome      = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\RawTestFiles\small.RAW"
let termoMzML       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.mzML"


//let wiffReader          = new WiffFileReader(wiffTestHome, licenseHome)
//let wiffMzML            = new MzMLReader(mzMLOfWiffUni)

//let bafReader           = new BafFileReader(bafTestHome)
//let bafMzMLReader       = new MzMLReader(bafMzMLFile)

let thermoReader        = new ThermoRawFileReader(thermoHome)
//let thermoMzMLReader    = new MzMLReader(termoMzML)

let getSpectras (reader:#IMzIODataReader) =
    reader.Model.Runs.GetProperties false
    |> Seq.collect (fun run -> reader.ReadMassSpectra (run.Value :?> Run).ID)
    |> Seq.take 5000

//let rtIndexEntry = wiffReader.BuildRtIndex("sample=0")

//let rtProfile = wiffReader.RtProfile (rtIndexEntry, (new MzIO.Processing.RangeQuery(1., 300., 600.)), (new MzIO.Processing.RangeQuery(1., 300., 600.)))

let mzIOSQLNoCompression    = new MzSQL(thermoHome + "NoCompression.mzIO")
let mzIOSQLZLib             = new MzSQL(thermoHome + "ZLib.mzIO")
let mzIOSQLNumPress         = new MzSQL(thermoHome + "NumPress.mzIO")
let mzIOSQLNumPressZLib     = new MzSQL(thermoHome + "NumPressZLib.mzIO")


let spectra = getSpectras thermoReader

//mzIOSQLNoCompression.insertMSSpectraBy  (mzIOSQLNoCompression.insertMSSpectrum) "run_1" bafReader BinaryDataCompressionType.NoCompression spectra
//mzIOSQLZLib.insertMSSpectraBy           (mzIOSQLZLib.insertMSSpectrum)          "run_1" bafReader BinaryDataCompressionType.ZLib spectra
//mzIOSQLNumPress.insertMSSpectraBy       (mzIOSQLNumPress.insertMSSpectrum)      "run_1" bafReader BinaryDataCompressionType.NumPress spectra
//mzIOSQLNumPressZLib.insertMSSpectraBy   (mzIOSQLNumPressZLib.insertMSSpectrum)  "run_1" bafReader BinaryDataCompressionType.NumPressZLib spectra


//mzIOSQLNoCompression.ReadMassSpectra "run_1"
//|> Seq.length
//mzIOSQLNoCompression.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzIOSQLNoCompression.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzIOSQLNoCompression.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzIOSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzIOSQLZLib.ReadMassSpectra "run_1"
//|> Seq.length
//mzIOSQLZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzIOSQLZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzIOSQLZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzIOSQLZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzIOSQLNumPress.ReadMassSpectra "run_1"
//|> Seq.length
//mzIOSQLNumPress.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzIOSQLNumPress.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzIOSQLNumPress.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzIOSQLNumPress.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzIOSQLNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.length
//mzIOSQLNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzIOSQLNumPressZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzIOSQLNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzIOSQLNumPressZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length


//wiffReader.Model.Runs.GetProperties false 
//|> Seq.head
//|> fun item -> item.Value :?> Run

//let sqlSpectra =
//    mzSQLReader.BeginTransaction() |> ignore
//    mzSQLReader.ReadMassSpectra ("run_1")

//sqlSpectra
//|> Seq.length

//let randomBafIDs = 
//    randomSpectra (sqlSpectra |> Array.ofSeq)
//    |> Array.map (fun spectrum -> spectrum.ID)


//let randomBafSpectra =
//    randomBafIDs
//    |> Seq.map (fun id -> mzSQLReader.ReadMassSpectrum(id))

//randomBafSpectra
//|> Seq.length

//let bafPeaks =
//    sqlSpectra
//    |> Seq.map (fun spectrum -> mzSQLReader.ReadSpectrumPeaks spectrum.ID)

//let randomBafPeaks =
//    randomBafSpectra
//    |> Seq.map (fun spectrum -> mzSQLReader.ReadSpectrumPeaks spectrum.ID)

1+1