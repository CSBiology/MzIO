
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
open System.IO
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
    new WiffFileReader(path, licenseHome)

let getMassSpectra (wiffFileReader:WiffFileReader) =
    wiffFileReader.Model.Runs.GetProperties false
    |> Seq.collect (fun (run:KeyValuePair<string, obj>) -> wiffFileReader.ReadMassSpectra run.Key)

let getPeak1DArrays (wiffFileReader:WiffFileReader) =
    getMassSpectra wiffFileReader
    |> Seq.map (fun spectrum -> wiffFileReader.ReadSpectrumPeaks spectrum.ID)

let getMzIOHelper (path:string) (compressionType:BinaryDataCompressionType) =
    let wiffFileReader = new WiffFileReader(path, licenseHome)
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

#time
let wiffTestUni     = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.wiff"
let mzMLOfWiffUni   = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.mzML"

let bafTestFile     = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.d\analysis.baf"
let bafMzMLFile     = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.mzML"

let thermoUniPath   = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.RAW"
let termoMzML       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.mzML"


//let wiffReader          = new WiffFileReader(wiffTestPaeddetor, licenseHome)
//let wiffMzML            = new MzMLReader(mzMLOfWiffUni)

//let bafReader           = new BafFileReader(bafTestFile)
//let bafMzMLReader       = new MzMLReader(bafMzMLFile)

//let thermoReader        = new ThermoRawFileReader(thermoUniPath)
//let thermoMzMLReader    = new MzMLReader(termoMzML)

//let spectra =
//    wiffReader.Model.Runs.GetProperties false
//    |> Seq.collect (fun run -> wiffReader.ReadMassSpectra (run.Value :?> Run).ID)

//spectra
//|> Seq.length

//let mzIOSQLReader = new MzIOSQL(wiffTestPaeddetor + ".mzIO")

//spectra
//|> Seq.length

//let seqPeaks =
//    spectra
//    |> Seq.map (fun spectrum -> wiffReader.ReadSpectrumPeaks(spectrum.ID))

//seqPeaks
//|> Seq.length

//let randomSpectra spectra=
//    let tmp = Array.copy spectra
//    shuffle tmp
//    tmp

//let readRandomSpectra =
//    randomSpectra
//    |> Seq.map (fun item -> bafReader.ReadMassSpectrum(item.ID))

//readRandomSpectra
//|> Seq.length

//let randomPeaks =
//    randomSpectra
//    |> Seq.map (fun spectrum -> wiffReader.ReadSpectrumPeaks(spectrum.ID))

//randomPeaks
//|> Seq.length


//let spectraMzML =
//    wiffReader.Model.Runs.GetProperties false
//    |> Seq.collect (fun item -> wiffReader.ReadMassSpectra (MzIOJson.FromJson<Run>(item.Value.ToString())).ID)
//    |> Array.ofSeq

//spectraMzML
//|> Seq.length

//let seqPeaksMzML =
//    wiffReader.Model.Runs.GetProperties false
//    |> Seq.collect (fun item -> wiffReader.ReadAllSpectrumPeaks (MzIOJson.FromJson<Run>(item.Value.ToString())).ID)

//seqPeaksMzML
//|> Seq.length

//let randomSpectraMzML =
//    let tmp = Array.copy spectraMzML
//    shuffle tmp
//    tmp

//let randomPeaksMzML =
//    randomSpectraMzML
//    |> Seq.map (fun item -> wiffReader.ReadSpectrumPeaks(item.ID))

//randomPeaksMzML
//|> Seq.length

//let rtIndexEntry = wiffReader.BuildRtIndex("sample=0")

//let rtProfile = wiffReader.RtProfile (rtIndexEntry, (new MzIO.Processing.RangeQuery(1., 300., 600.)), (new MzIO.Processing.RangeQuery(1., 300., 600.)))

//insertMSSpectraBy insertMSSpectrum (bafTestFile + "NoCompression.mzIO") "run_1" bafReader "NoCompression" spectra

//insertMSSpectraBy insertMSSpectrum (bafTestFile + "ZLib.mzIO") "run_1" bafReader "ZLib" spectra

//insertMSSpectraBy insertMSSpectrum (bafTestFile + "NumPress.mzIO") "run_1" bafReader "NumPress" spectra

//insertMSSpectraBy insertMSSpectrum (bafTestFile + "NumPressZLib.mzIO") "run_1" bafReader "NumPressZLib" spectra

//let mzSQLReader = new MzIOSQL(bafTestFile + "NoCompression.mzIO")

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


open System.Data.SQLite

let sqlFilePath = sprintf "%s%s" wiffTestPaeddetor "111.mzIO"

let prepareSqlSelectMassSpectra (cn:SQLiteConnection) (tr:SQLiteTransaction) =
    let querystring = "SELECT Description FROM Spectrum WHERE RunID = @runID"
    let cmd = new SQLiteCommand(querystring, cn, tr)
    cmd.Parameters.Add("@runID", Data.DbType.String) |> ignore
    let rec loop (reader:SQLiteDataReader) acc =
        seq
            {
                match reader.Read() with
                | true  -> 
                    yield MzIOJson.FromJson<MassSpectrum>(reader.GetString(0))
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
    let querystring = "SELECT PeakArray, PeakData FROM Spectrum WHERE SpectrumID = @spectrumID"
    let cmd = new SQLiteCommand(querystring, cn, tr)
    cmd.Parameters.Add("@spectrumID", Data.DbType.String) |> ignore
    let rec loop (reader:SQLiteDataReader) peaks =
        match reader.Read() with
        | true  -> loop reader (decoder.Decode(reader.GetStream(1), MzIOJson.FromJson<Peak1DArray>(reader.GetString(0))))
        | false -> peaks 
    fun id ->
    cmd.Parameters.["@spectrumID"].Value <- id            
    use reader = cmd.ExecuteReader()            
    loop reader (new Peak1DArray())
    
let selectPeak1DArrays (runID:string) =
    let cn = new SQLiteConnection(sprintf "Data Source=%s;Version=3" sqlFilePath)
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
    let querystring = 
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
    let cmd = new SQLiteCommand(querystring, cn, tr)
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

//let cn = new SQLiteConnection(sprintf "Data Source=%s;Version=3" (wiffTestPaeddetor + "222.mzIO"))
//cn.Open()
//let tr = cn.BeginTransaction()

//let insertMassSpectrum =
//    prepareInsertMassSpectrum cn tr

//let insertedPeaks =
//    spectra
//    |> Seq.take 100
//    |> Seq.iter (fun spectrum ->
//        let tmp =
//            let peakArray = (wiffReader.ReadSpectrumPeaks(spectrum.ID))
//            let clonedP = new Peak1DArray(BinaryDataCompressionType.NoCompression,peakArray.IntensityDataType,peakArray.MzDataType)
//            clonedP.Peaks <- peakArray.Peaks
//            clonedP
//        insertMassSpectrum "sample=0" spectrum tmp)

//tr.Commit()
//cn.Close()


type MzSQL(path) =

    let mutable disposed = false

    let sqlitePath = 
        if String.IsNullOrWhiteSpace(path) then
                raise (ArgumentNullException("sqlitePath"))
            else
                path

    let cn = new SQLiteConnection(sprintf "Data Source=%s;Version=3" sqlitePath)

    let bt = 
        cn.Open()
        cn.BeginTransaction()

    let model = 
        if (cn.State=ConnectionState.Open) then 
            ()
        else
            cn.Open()
        let tmp = new MzIOModel(Path.GetFileNameWithoutExtension(sqlitePath))
        MzSQL.SqlInitSchema(cn)
        MzSQL.insertModel(cn, bt, tmp)
        //bt.Commit()
        printfn "%A" tmp

    member this.createConnection() = new SQLiteConnection(sprintf "Data Source=%s;Version=3" sqlitePath)

    //member private this.cn = this.createConnection()

    //member private this.bt =  cn.BeginTransaction()

    static member SqlInitSchema(cn:SQLiteConnection) =
        //let cn = this.createConnection()
        if (cn.State=ConnectionState.Open) then 
            ()
        else
            cn.Open()
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Model (Lock INTEGER  NOT NULL PRIMARY KEY DEFAULT(0) CHECK (Lock=0), Content TEXT NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Spectrum (RunID TEXT NOT NULL, SpectrumID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore
        use cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Chromatogram (RunID TEXT NOT NULL, ChromatogramID TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL, PeakArray TEXT NOT NULL, PeakData BINARY NOT NULL)", cn)
        cmd.ExecuteNonQuery() |> ignore
        //cn.Close()

    static member selectModel (cn:SQLiteConnection) (tr:SQLiteTransaction) =
        let querySelect = "SELECT Content FROM Model WHERE Lock = 0"
        let cmdSelect = new SQLiteCommand(querySelect, cn, tr)
        let selectReader = cmdSelect.ExecuteReader()
        let rec loopSelect (reader:SQLiteDataReader) model =
            match reader.Read() with
            | true  -> loopSelect reader (MzIOJson.deSerializeMzIOModel(reader.GetString(0)))
            | false -> model           
        loopSelect selectReader (new MzIOModel())

    static member insertModel(cn:SQLiteConnection, tr:SQLiteTransaction, model:MzIOModel) =
        if (cn.State=ConnectionState.Open) then 
            ()
        else 
            cn.Open()
        let querystring = 
            "INSERT INTO Model (
                Lock,
                Content)
                VALUES(
                    @lock,
                    @content)"
        let cmd = new SQLiteCommand(querystring, cn, tr)
        cmd.Parameters.Add("@lock"      ,Data.DbType.Int64)     |> ignore
        cmd.Parameters.Add("@content"   ,Data.DbType.String)    |> ignore
        cmd.Parameters.["@lock"].Value      <- 0
        cmd.Parameters.["@content"].Value   <- MzIOJson.ToJson(model)
        cmd.ExecuteNonQuery() |> ignore
        tr.Commit()

    member this.Model = model

    static member prepareSelectMassSpectra(cn:SQLiteConnection, tr:SQLiteTransaction) =
        let querystring = "SELECT Description FROM Spectrum WHERE RunID = @runID"
        let cmd = new SQLiteCommand(querystring, cn, tr)
        cmd.Parameters.Add("@runID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) acc =
            seq
                {
                    match reader.Read() with
                    | true  -> 
                        yield MzIOJson.FromJson<MassSpectrum>(reader.GetString(0))
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

    static member prepareSelectPeak1DArray(cn:SQLiteConnection, tr:SQLiteTransaction) =
        let decoder = new BinaryDataDecoder()
        let querystring = "SELECT PeakArray, PeakData FROM Spectrum WHERE SpectrumID = @spectrumID"
        let cmd = new SQLiteCommand(querystring, cn, tr)
        cmd.Parameters.Add("@spectrumID", Data.DbType.String) |> ignore
        let rec loop (reader:SQLiteDataReader) peaks =
            match reader.Read() with
            | true  -> loop reader (decoder.Decode(reader.GetStream(1), MzIOJson.FromJson<Peak1DArray>(reader.GetString(0))))
            | false -> peaks 
        fun id ->
        cmd.Parameters.["@spectrumID"].Value <- id            
        use reader = cmd.ExecuteReader()            
        loop reader (new Peak1DArray())

    static member prepareUpdateRunIDOfMzIOModel(cn:SQLiteConnection, tr:SQLiteTransaction) =
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
            else
                ()

    ///Prepare function to insert MzQuantMLDocument-record.
    static member prepareInsertMassSpectrum(cn:SQLiteConnection, tr:SQLiteTransaction) =
        let encoder = new BinaryDataEncoder()
        let updateRunID = MzSQL.prepareUpdateRunIDOfMzIOModel(cn, tr)
        let querystring = 
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
        let cmd = new SQLiteCommand(querystring, cn, tr)
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




MzSQL(wiffTestPaeddetor + ".mzIO")
