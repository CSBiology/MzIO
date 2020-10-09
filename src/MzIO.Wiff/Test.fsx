
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
#r @"C:\Users\jonat\source\repos\BioFSharp\bin\BioFSharp\net45\BioFSharp.dll"
#r @"C:\Users\jonat\source\repos\BioFSharp\bin\BioFSharp.IO\net45\BioFSharp.IO.dll"
#r @"C:\Users\jonat\source\repos\ms-numpress\src\main\csharp\MSNumpress.dll"

open MSNumpressFSharp
open NumpressHelper
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
open MzIO.MzSQL
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.PSIMSExtension
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.MetaData.UO.UO
open MzIO.Processing.MzIOLinq
open MzIO.Json
open MzIO.Bruker
open MzIO.IO.MzML
open MzIO.IO
open MzIO.Thermo
open BioFSharp
open BioFSharp.IO


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


let rand = new System.Random()

let swap (a: _[]) x y =
    let tmp = a.[x]
    a.[x] <- a.[y]
    a.[y] <- tmp

// shuffle an array (in-place)
let shuffle a =
    Array.iteri (fun i _ -> swap a i (rand.Next(i, Array.length a))) a

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

    member private this.SelectMassSpectra =
        selectMassSpectra

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
            this.SelectMassSpectra(runID)

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


#time
let wiffTestUni     = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.wiff"

let mzMLOfWiffUni   = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.mzML"

let bafTestFile     = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.d\analysis.baf"
let bafMzMLFile     = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.mzML"

let thermoUniPath   = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.RAW"
let termoMzML       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.mzML"


let wiffReader          = new WiffFileReader(wiffTestUni, licensePath)
//let wiffMzML            = new MzMLReader(mzMLOfWiffUni)

//let bafReader           = new BafFileReader(bafTestFile)
//let bafMzMLReader       = new MzMLReader(bafMzMLFile)

//let thermoReader        = new ThermoRawFileReader(thermoUniPath)
//let thermoMzMLReader    = new MzMLReader(termoMzML)

//let spectra =
//    wiffReader.Model.Runs.GetProperties false
//    |> Seq.collect (fun run -> wiffReader.ReadMassSpectra (run.Value :?> Run).ID)

//let massRange = 
//    for i in wiffReader.GetMassRange("sample=0 experiment=0 scan=0") do
//        printfn "%A" i
        
//wiffReader.Model.Runs.GetProperties false
//|> Seq.map (fun run -> wiffReader.GetTotalTIC (run.Value :?> Run).ID)

//let scanTime = wiffReader.GetScanTime("sample=0 experiment=0 scan=40")


let mzMLTestPath = "C:/Users/Student/source/repos/wiffTestFiles/MzML/tiny.pwiz.1.1.txt"

let mzMLReader = new MzMLReader(mzMLTestPath)
let mzMLWriter = new MzIOMLDataWriter(mzMLTestPath + ".mzml")

let run = (Seq.head(wiffReader.Model.Runs.GetProperties false)).Value :?> Run

let mzMLSpectra = mzMLReader.ReadMassSpectra(run.ID)        |> List.ofSeq
let mzMLpeaks   = mzMLReader.ReadAllSpectrumPeaks(run.ID)   |> List.ofSeq

//mzMLWriter.WriteMzMl(mzMLReader.Model, mzMLSpectra, mzMLpeaks)
mzMLWriter.insertMSSpectraBy(mzMLWriter.insertMSSpectrum) mzMLReader.Model ("sample_0") mzMLReader BinaryDataCompressionType.NoCompression (mzMLSpectra)

//mzMLWriter.Model
//mzMLWriter.UpdateModel(wiffReader.Model)
//mzMLWriter.SaveModel()
//mzMLWriter.insertMSSpectraBy(mzMLWriter.insertMSSpectrum) ("sample_0") wiffReader BinaryDataCompressionType.NoCompression (spectra |> Seq.take 100)


//let test =
//    spectra
//    |> Seq.take 2
//    |> Seq.collect (fun spectrum -> wiffReader.GetIsolationWindow(spectrum.ID))
//    |> Seq.concat
//    |> List.ofSeq

//test

//for i in test do
//    printfn "%A" i

let ms1 = 
    BioFSharp.IO.Mgf.readMgf (@"C:\Users\jonat\OneDrive\Desktop\NumpressUnitTest.mgf")
    |> List.head

let optimalLinearFixedPoint ((data: double[]), (dataSize:int)) =
    match dataSize with
    | 0 -> 0.
    | 1 -> Math.Floor((0x7FFFFFFFL |> double) / data.[0])
    | value when value > 1 -> 
        let rec loop i (maxDoubleV: double) =
            match i with
            | value when value >= 2 && value < dataSize->  
                let extrapol = data.[i - 1] + (data.[i - 1] - data.[i - 2])
                let diff = data.[i] - extrapol
                let maxDouble = Math.Max(maxDoubleV, Math.Ceiling(Math.Abs(diff) + 1.))
                loop (i + 1) maxDouble
            | value when value = dataSize->
                Math.Floor((0x7FFFFFFFL |> double) / maxDoubleV)
            | _ ->  failwith "Error in optimalLinearFixedPoint; case: dataSize > 1"
        loop 2 (Math.Max(data.[0], data.[1]))
    | _ -> failwith "Error in optimalLinearFixedPoint. dataSize has to be the length of the inputarray."
    //match dataSize with
    //| 0 -> 0.
    //| 1 -> Math.Floor((0x7FFFFFFFL |> double) / data.[0])
    //| _ ->
    //    let maxVal = data |> Array.max
    //    Math.Floor((0x7FFFFFFFL |> double) / maxVal)

printfn "%A" (0xFFFFFFFFL |> double)

let optimalLinearFixedPointMass ((data:double[]), (dataSize:int), (massAccuracy:double)) =
    if dataSize < 3 then 0.
    else
        let maxFp = 0.5 / massAccuracy
        let maxFpOverflow = optimalLinearFixedPoint (data,dataSize)
        if maxFp > maxFpOverflow then -1.
        else
            maxFp
printfn "%A" (0xf |> int64)
let encodeInt ((x: int64), (res: byte[]), (resOffset: int)) =
    let mask: int64 = 0xf0000000 |> int64
    let init: int64 = x &&& mask
    match init with
    | 0L ->  
            let rec loop i= 
                    match i with
                    | value when value >= 0 && value < 8 ->
                            let m = mask >>> (4 * i)
                            if (x &&& m) <> 0L then i
                            else loop (i + 1)
                    | value when value = 8 -> 8
                    | _ -> failwith "Error in encodeInt; case: 0L" 
            let l = loop 0
            res.[resOffset] <- l |> byte
            for i = l to 7 do
               res.[resOffset + 1 + i - l] <- byte (0xfL &&& (x >>> (4 * (i - l))))
            1 + 8 - l
    | mask ->
            let rec loop i= 
                match i with
                | value when value >= 0 && value < 8 ->
                        let m = mask >>> (4 * i)
                        if (x &&& m) <> m then i
                        else loop (i + 1)
                | value when value = 8 -> 7
                | _ -> failwith "Error in encodeInt; case: mask" 
            let l = loop 0
            res.[resOffset] <- byte ((l|> int) ||| 8)
            for i = l to 7 do
               res.[resOffset + 1 + i - l] <- byte (0xfL &&& (x >>> (4 * (i - l))))
            1 + 8 - l
    | _ ->
        res.[resOffset] <- (0 |> byte)
        for i = 0 to 7 do
            res.[resOffset + 1 + i] <- byte (0xfL &&& (x >>> (4 * i)))
        9

let encodeInt2 ((x: int64), (res: byte[]), (resOffset: int)) =
    // not sure if this should be cast to int64 or ...L, both seems to work
    let mask: int64 = 0xf0000000L
    let init: int64 = x &&& mask
    if init = 0L then
            let rec loop i= 
                    match i with
                    | value when value >= 0 && value < 8 ->
                            let m = mask >>> (4 * i)
                            if (x &&& m) <> 0L then i
                            else loop (i + 1)
                    | value when value = 8 -> 8
                    | _ -> failwith "Error in encodeInt; case: 0L" 
            let l = loop 0
            res.[resOffset] <- l |> byte
            for i = l to 7 do
               res.[resOffset + 1 + i - l] <- byte (0xfL &&& (x >>> (4 * (i - l))))
            1 + 8 - l
    elif init = mask then
            let rec loop i= 
                match i with
                | value when value >= 0 && value < 8 ->
                        let m = mask >>> (4 * i)
                        if (x &&& m) <> m then i
                        else loop (i + 1)
                | value when value = 8 -> 7
                | _ -> failwith "Error in encodeInt; case: mask" 
            let l = loop 0
            res.[resOffset] <- byte ((l|> int) ||| 8)
            for i = l to 7 do
               res.[resOffset + 1 + i - l] <- byte (0xfL &&& (x >>> (4 * (i - l))))
            1 + 8 - l
    else
        res.[resOffset] <- (0 |> byte)
        for i = 0 to 7 do
            res.[resOffset + 1 + i] <- byte (0xfL &&& (x >>> (4 * i)))
        9

let encodeFixedPoint ((fixedPoint: double), (result: byte[])) =
    let (fp: int64) = BitConverter.DoubleToInt64Bits(fixedPoint)
    for i = 0 to 7 do
        result.[7 - i] <- byte ((fp >>> (8 * i)) &&& 0xffL)

let encodeLinear ((data: double[]), (dataSize: int), (result: byte[]), (fixedPoint: double)) =
    let ints = Array.init 3 (fun x -> int64 0)
    let halfBytes = Array.init 10 (fun x -> byte(0))
    printfn "%A" halfBytes
    encodeFixedPoint (fixedPoint, result)
    match dataSize with
    | value when value = 0 -> 8
    | value when value = 1 ->  
            ints.[1] <- int64 (data.[0] * fixedPoint + 0.5)
            for i = 0 to 3 do
                result.[8 + i] <- byte ((ints.[1] >>> (i * 8)) &&& 0xffL)
            12
    | value when value > 1 ->   
            ints.[1] <- int64 (data.[0] * fixedPoint + 0.5)
            for i = 0 to 3 do
                result.[8 + i] <- byte ((ints.[1] >>> (i * 8)) &&& 0xffL)
            ints.[2] <- int64 (data.[1] * fixedPoint + 0.5)
            for i = 0 to 3 do
                result.[12 + i] <- byte ((ints.[2] >>> (i * 8)) &&& 0xffL)
            let rec loop i halfByteCountV riV =
                match i with
                | value when value < dataSize ->
                    ints.[0] <- ints.[1]
                    ints.[1] <- ints.[2]
                    ints.[2] <- int64 (data.[i] * fixedPoint + 0.5)
                    let extrapol = ints.[1] + (ints.[1] - ints.[0])
                    let diff = ints.[2] - extrapol
                    let halfByteCount = halfByteCountV + (encodeInt2 (diff, halfBytes, halfByteCountV))                
                    let rec loop2 hbi ri =
                        match hbi with
                        | value when value < halfByteCount ->
                                result.[ri] <- byte (((halfBytes.[hbi - 1]|> int) <<< 4) ||| ((halfBytes.[hbi]|> int) &&& 0xf))
                                loop2 (hbi + 2) (ri + 1)
                        | value when value >= halfByteCount -> ri
                        | _ -> failwith "Error in encodeLinear; case: dataSize > 1 & i < dataSize"
                    let riOut = loop2 1 riV
                    if   halfByteCount % 2 <> 0 then 
                         halfBytes.[0] <- halfBytes.[halfByteCount - 1]
                         loop (i + 1) 1 riOut
                    else loop (i + 1) 0 riOut
                | result when result = dataSize -> halfByteCountV, riV
                | _ -> failwith "Error in encodeLinear; case: dataSize"
            let (halfByteCountOut, riOut) = loop 2 0 16
            printfn "%A" halfBytes
            if   halfByteCountOut = 1 then
                 result.[riOut] <- byte ((halfBytes.[0]|> int) <<< 4)
                 riOut + 1
            else riOut
                
    | _ -> failwith "Error in encodeLinear. dataSize has to be the length of the inputarray."    

let encodeLin (array: float[]) =
    //empty array which gets filled by the encodePic function
    //maximal length of this array is original length * 5
    let (encodedByteArray: byte[]) =
        if array.Length < 20 then
            Array.zeroCreate 200
        else
            Array.zeroCreate (array.Length * 5)
    //gives optimal fixed point for encoding. Can also be set by hand.
    let fixedPoint = optimalLinearFixedPoint(array, array.Length)
    printfn "%f" fixedPoint
    //encoding happens here
    let encodeInt = encodeLinear (array, array.Length, encodedByteArray, fixedPoint)
    NumpressHelper.NumpressEncodingHelpers.createNumpressHelper (encodedByteArray |> Array.take (encodeInt + 5)) encodeInt array.Length

let decodeLin ((encodedByteArray, enc, length): (byte[] * int * int)) =
    //empty array which gets filled by the decodeLinear function
    let decodedArray = Array.init (length) (fun i -> 0.)
    //decoding happens here
    let decode = Decode.decodeLinear (encodedByteArray, enc, decodedArray)
    decodedArray

let testMass =
    [|354.3518131; 354.8040287; 355.0633232; 355.3465387; 356.0616964;
      357.0667788; 357.3215892; 358.0599902; 359.0519199; 360.8024357;
      363.8687143; 365.8189235; 369.1195284; 370.8484082; 371.0972668;
      371.3083209; 372.0935356; 372.3102925; 373.09114; 374.098229; 375.0876355;
      383.8309447; 385.8145542; 386.8497089; 390.8264846; 391.2791726;
      392.271558; 399.3376388; 401.844953; 402.0673915; 403.8350045; 405.8073382;
      406.8264657; 413.370581; 419.3052157; 421.8310856; 422.7950418;
      425.3545569; 428.889774; 429.0788462; 430.0801683; 430.878568; 431.0797416;
      431.2955451; 432.3286496; 432.8807284; 433.883549; 434.8699575;
      436.8696174; 440.7912881; 441.8032248; 444.7897183; 445.1155874;
      446.1146748; 446.8506236; 447.1030037; 447.340603; 448.1132366;
      449.1246095; 454.7674354; 455.7593013; 459.7947854; 461.829761;
      462.1376592; 463.1526378; 464.1233448; 472.7802264; 474.7794412;
      475.7591872; 477.7861724; 479.8205427; 481.8099104; 490.7663251;
      491.7935654; 492.7813501; 518.7540152; 519.1283314; 519.8901775;
      520.1399797; 521.1365824; 522.1309299; 523.126225; 536.1545568;
      537.1598682; 538.1498313; 539.7800423; 550.7396852; 557.7954749;
      568.7580689; 570.7627294; 571.8066809; 572.754111; 586.7738735;
      592.7834338; 593.1425184; 594.1416871; 595.1451228; 610.1664209;
      612.1836372; 628.7123315; 630.6929353; 667.1702329; 668.1572881; 669.1668706;
      741.2038739; 742.1906684; 743.1819479; 744.1892136; 801.1679261;
      815.2135926; 816.2083415; 817.2117265; 996.6839583; 998.6224541;
      998.9020722; 999.2394414; 999.6035088; 1001.229301; 1001.460395;
      1005.099193; 1006.577946; 1006.756183; 1007.340018; 1007.504948;
      1024.097647; 1025.792708; 1026.197571; 1026.42253; 1026.913026;
      1027.642236; 1028.164545; 1034.659434; 1035.49081; 1036.431028;
      1036.620932; 1037.276685; 1037.62952; 1038.964506; 1039.693453;
      1040.064816; 1040.409066; 1040.608394; 1041.265407; 1041.646116;
      1042.593659; 1050.535429; 1051.031652; 1051.505222; 1051.650957;
      1052.416233; 1052.521024; 1092.988728; 1093.401997; 1094.544697;
      1094.665505; 1095.571777; 1096.580737; 1101.287588; 1103.255249;
      1103.460509; 1104.183737; 1106.527687; 1106.733251; 1108.560809;
      1109.57573; 1109.921939; 1110.525594; 1111.045149; 1111.597602;
      1112.862207; 1114.56353; 1115.384192; 1115.82982; 1117.012339; 1123.659202;
      1124.120606; 1125.038988; 1125.698571; 1126.146255; 1127.112613;
      1127.650182; 1135.880551; 1136.155096; 1137.021552; 1137.623057;
      1139.234168; 1140.552335; 1141.097843; 1142.231968; 1177.572857;
      1227.036509; 1227.631843; 1227.887733; 1229.650163; 1230.014621;
      1230.581117; 1237.53745; 1238.525752; 1240.251283|]

let testMass2 =
    [|354.8729419; 355.0581611; 355.3652022; 356.0512298; 357.0589541;
    365.8217733; 368.8499289; 370.8296561; 371.0974469; 371.3112079;
    372.1018462; 372.3158962; 373.0886137; 373.3192284; 374.0875532;
    376.8197128; 383.8421708; 384.8250888; 385.8451256; 386.8609878;
    391.2766385; 392.2801498; 393.2905167; 397.8826628; 399.3407151;
    401.8508614; 402.8565582; 403.8381111; 404.8095555; 405.8246039;
    411.8418258; 412.0328047; 413.3651855; 419.3170537; 419.8492546;
    421.8285441; 422.8213781; 423.8529661; 425.1608693; 425.375187;
    428.8843232; 429.0908497; 429.2043149; 430.071801; 430.8847713;
    431.0742833; 432.8811079; 433.0769036; 434.8820584; 440.7975911;
    441.7977301; 444.8019852; 445.1160077; 445.403465; 446.1180648;
    447.1034307; 447.33509; 448.1166399; 449.1161144; 449.8128347; 454.7678882;
    455.7657539; 457.7617612; 461.803075; 462.138137; 463.1440518; 463.8182938;
    464.1571116; 472.7654721; 479.8272337; 482.812858; 486.8111934;
    489.8060009; 490.78868; 492.7881684; 500.7639118; 504.1007795; 510.8014091;
    518.757888; 519.1354065; 520.1246438; 521.1308528; 522.1316158;
    535.8301497; 536.1650485; 537.1606076; 538.1636062; 550.7437691;
    552.7395737; 554.738988; 557.8029223; 568.7622715; 569.7507583;
    570.7568794; 571.7403811; 588.7768564; 593.1537219; 594.1529029;
    595.1494987; 610.1778335; 611.1738858; 612.1707505; 630.7081248; 667.1823346;
    668.1621411; 669.1753631; 670.1784455; 727.1637017; 741.1862602;
    743.187288; 745.221684; 815.2113781; 816.2061286; 904.0744268; 922.6860691;
    923.0615162; 923.5864168; 926.2772714; 963.2614937; 964.6350052;
    964.9141875; 1175.480666; 1177.041266; 1177.508684; 1178.038859;
    1178.178652; 1178.564331; 1179.340701; 1181.420317; 1182.492203;
    1183.066973; 1183.574238; 1184.598763; 1185.135366; 1194.009277;
    1194.494613; 1195.281068; 1195.552989; 1197.058825; 1197.685726;
    1198.375993; 1198.512126|]

let testMass3 =
    [|120.0802488; 130.0621784; 173.1347074; 197.131058; 244.1699318;
    310.1891582; 363.1899845; 387.2211462; 429.0699926; 430.275327;
    449.2248044; 511.3396848; 542.8016093; 600.3729056; 637.4318806;
    714.4811173; 720.3800509; 736.2697555; 809.4668384; 988.5456103|]

(0x7FFFFFFFL |> double)/1240.251283

let encMass = NumpressHelper.NumpressEncodingHelpers.encodeLin testMass3

let equalWithinRange (n1: float) (n2: float) accuracy =
    let diff = abs (n1 - n2)
    diff < accuracy

let decMass = decodeLin (encMass.Bytes, encMass.NumberEncodedBytes, encMass.OriginalDataLength)

Array.map2 (fun x y ->
    equalWithinRange x y 0.0000001
)testMass3 decMass
|> Array.contains false

Array.map2 (fun x y-> if x=y then () else printfn "%f;%f"x y)(testMass |> Array.map (fun x -> Math.Round(x,5))) (decMass |> Array.map (fun x -> Math.Round(x,5))) 

let a = NumpressHelper.NumpressEncodingHelpers.encodeLin testMass3

let b = NumpressDecodingHelpers.decodeLin (a.Bytes, a.NumberEncodedBytes, a.OriginalDataLength)

Array.map2 (fun x y-> if x=y then () else printfn "%f;%f"x y)(testMass3 |> Array.map (fun x -> Math.Round(x,5))) (b |> Array.map (fun x -> Math.Round(x,5)))