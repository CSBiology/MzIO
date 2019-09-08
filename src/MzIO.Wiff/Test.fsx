
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
open MzIO.IO.MzML
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
    let mzIOSQL = new MzSQL(helper.Path + ".mzIO")
    let bn = mzIOSQL.BeginTransaction()
    Seq.map2 (fun (spectrum:MzIO.Model.MassSpectrum) (peak:Peak1DArray) -> mzIOSQL.InsertMass(helper.RunID, spectrum, peak)) helper.MassSpectrum helper.Peaks
    |> Seq.length |> ignore
    bn.Commit()
    bn.Dispose()
  
let insertIntoDB (amount:int) (helper:MzIOHelper) =
    let mzIOSQL = new MzSQL(helper.Path + ".mzIO")
    let bn = mzIOSQL.BeginTransaction()
    Seq.map2 (fun (spectrum:MzIO.Model.MassSpectrum) (peak:Peak1DArray) -> mzIOSQL.InsertMass(helper.RunID, spectrum, peak)) ((*Seq.take amount*) helper.MassSpectrum) ((*Seq.take amount*) helper.Peaks)
    |> Seq.length |> ignore
    bn.Commit()
    bn.Dispose()

let getSpectrum (path:string) (spectrumID:string) =
    let mzIOSQL = new MzSQL(path)
    let bn = mzIOSQL.BeginTransaction()
    mzIOSQL.ReadMassSpectrum spectrumID

let getSpectra (path:string) (helper:MzIOHelper) =
    let mzIOSQL = new MzSQL(path)
    let bn = mzIOSQL.BeginTransaction()
    let tmp = 
        mzIOSQL.ReadMassSpectra helper.RunID
        |> List.ofSeq
    bn.Commit()
    bn.Dispose()
    tmp

let getSpectrumPeaks (path:string) (spectrumID:string) =

    let mzIOSQL = new MzSQL(path)

    let bn = mzIOSQL.BeginTransaction()

    mzIOSQL.ReadSpectrumPeaks spectrumID

/// Create a new file instance of the DB schema. DELETES already existing instance
let initDB filePath =
    let _ = System.IO.File.Delete filePath  
    let db = new MzSQL(filePath)
    db

/// Returns the conncetion string to a existing MzLiteSQL DB
let getConnection filePath =
    match System.IO.File.Exists filePath with
    | true  -> let db = new MzSQL(filePath)
               db 
    | false -> initDB filePath

/// copies MassSpectrum into DB schema
let insertMSSpectrum (db: MzSQL) runID (reader:IMzIODataReader) (compress: string) (spectrum: MassSpectrum)= 
    let peakArray = reader.ReadSpectrumPeaks(spectrum.ID)
    match compress with 
    | "NoCompression"  -> 
        let clonedP = new Peak1DArray(BinaryDataCompressionType.NoCompression,peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        db.InsertMass(runID, spectrum, clonedP)
    | "ZLib" -> 
        let clonedP = new Peak1DArray(BinaryDataCompressionType.ZLib,peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        db.InsertMass(runID, spectrum, clonedP)
    | "NumPress" ->
        let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPress,peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        db.InsertMass(runID, spectrum, clonedP)
    | "NumPressZLib" ->
        let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPressZLib,peakArray.IntensityDataType,peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        db.InsertMass(runID, spectrum, clonedP)
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
let termoMzML       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.mzML"

//let mzMLHome        = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\MzMLTestFiles\tiny.pwiz.1.1.txt"
let mzMLHome    = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\MzMLTestFiles\small_miape.pwiz.1.1.txt"

//let wiffReader          = new WiffFileReader(wiffTestUni, licensePath)
//let wiffMzML            = new MzMLReader(mzMLOfWiffUni)

//let bafReader           = new BafFileReader(bafTestUni)
//let bafMzMLReader       = new MzMLReader(bafMzMLFile)

//let thermoReader        = new ThermoRawFileReader(thermoUni)
//let thermoMzMLReader    = new MzMLReader(termoMzML)

let mzMLReader          = new MzMLReader(mzMLHome)

let getSpectras (reader:#IMzIODataReader) =
    reader.Model.Runs.GetProperties false
    |> Seq.collect (fun run -> reader.ReadMassSpectra (run.Value :?> Run).ID)

//let rtIndexEntry = wiffReader.BuildRtIndex("sample=0")
//let rtProfile = wiffReader.RtProfile (rtIndexEntry, (new MzIO.Processing.RangeQuery(1., 300., 600.)), (new MzIO.Processing.RangeQuery(1., 300., 600.)))

let mzSQLNoCompression  = new MzSQL(mzMLHome + "NoCompression.mzIO")
let mzSQLZLib           = new MzSQL(mzMLHome + "ZLib.mzIO")
let mzSQLNumPress       = new MzSQL(mzMLHome + "NumPress.mzIO")
let mzSQLNumPressZLib   = new MzSQL(mzMLHome + "NumPressZLib.mzIO")

//let mzMLNoCompression   = new MzMLWriter(mzMLHome + "NoCompression.mzml")
//let mzMLZLib            = new MzMLWriter(mzMLHome + "ZLib.mzml")
//let mzMLNumPress        = new MzMLWriter(mzMLHome + "NumPress.mzml")
//let mzMLNumPressZLib    = new MzMLWriter(mzMLHome + "NumPressZLib.mzml")

let mzMLReaderNoCompression = new MzMLReader(mzMLHome + "NoCompression.mzml")
let mzMLReaderZLib          = new MzMLReader(mzMLHome + "ZLib.mzml")
let mzMLReaderNumPress      = new MzMLReader(mzMLHome + "NumPress.mzml")
let mzMLReaderNumPressZLib  = new MzMLReader(mzMLHome + "NumPressZLib.mzml")

let spectra =
    mzMLReader.Model.Runs.GetProperties false
    |> Seq.map (fun item -> item.Value :?> Run)
    |> Seq.head
    |> (fun run -> mzMLReader.ReadMassSpectra run.ID)
    |> Seq.take 10
    |> List.ofSeq
    //|> (fun run -> mzMLReader.ReadMassSpectra run.ID)
    //|> Seq.map (fun spectrum -> mzMLReader.ReadSpectrumPeaks spectrum.ID)
    
spectra
|> Seq.map (fun spectrum -> mzMLReader.ReadSpectrumPeaks spectrum.ID)
|> Seq.length

//mzSQLNoCompression.insertMSSpectraBy (mzSQLNoCompression.insertMSSpectrum)  "run_1" mzMLReader BinaryDataCompressionType.NoCompression spectra
//mzSQLZLib.insertMSSpectraBy          (mzSQLZLib.insertMSSpectrum)           "run_1" mzMLReader BinaryDataCompressionType.ZLib          spectra
//mzSQLNumPress.insertMSSpectraBy      (mzSQLNumPress.insertMSSpectrum)       "run_1" mzMLReader BinaryDataCompressionType.NumPress      spectra
//mzSQLNumPressZLib.insertMSSpectraBy  (mzSQLNumPressZLib.insertMSSpectrum)   "run_1" mzMLReader BinaryDataCompressionType.NumPressZLib  spectra

//mzSQLNoCompression.ReadMassSpectra "run_1"
//|> Seq.length
//mzSQLNoCompression.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzSQLNoCompression.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzSQLNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzSQLZLib.ReadMassSpectra "run_1"
//|> Seq.length
//mzSQLZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzSQLZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzSQLZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzSQLZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzSQLNumPress.ReadMassSpectra "run_1"
//|> Seq.length
//mzSQLNumPress.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzSQLNumPress.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzSQLNumPress.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzSQLNumPress.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzSQLNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.length
//mzSQLNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzSQLNumPressZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzSQLNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzSQLNumPressZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzMLNoCompression.insertMSSpectraBy   (mzMLNoCompression.insertMSSpectrum)  "run_1" mzMLReader BinaryDataCompressionType.NoCompression spectra
//mzMLZLib.insertMSSpectraBy            (mzMLZLib.insertMSSpectrum)           "run_1" mzMLReader BinaryDataCompressionType.ZLib          spectra
//mzMLNumPress.insertMSSpectraBy        (mzMLNumPress.insertMSSpectrum)       "run_1" mzMLReader BinaryDataCompressionType.NumPress      spectra
//mzMLNumPressZLib.insertMSSpectraBy    (mzMLNumPressZLib.insertMSSpectrum)   "run_1" mzMLReader BinaryDataCompressionType.NumPressZLib  spectra

 
//mzMLReaderNoCompression.ReadMassSpectra "run_1"
//|> Seq.length
//mzMLReaderNoCompression.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNoCompression.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzMLReaderNoCompression.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNoCompression.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzMLReaderZLib.ReadMassSpectra "run_1"
//|> Seq.length
//mzMLReaderZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzMLReaderZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzMLReaderNumPress.ReadMassSpectra "run_1"
//|> Seq.length
//mzMLReaderNumPress.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNumPress.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzMLReaderNumPress.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNumPress.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length

//mzMLReaderNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.length
//mzMLReaderNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNumPressZLib.ReadMassSpectrum spectrum.ID)
//|> Seq.length
//mzMLReaderNumPressZLib.ReadMassSpectra "run_1"
//|> Seq.map (fun spectrum -> mzMLReaderNumPressZLib.ReadSpectrumPeaks spectrum.ID)
//|> Seq.length


mzSQLNoCompression  .BuildRtIndex ("run_1")
mzSQLZLib           .BuildRtIndex ("run_1")
mzSQLNumPress       .BuildRtIndex ("run_1")
mzSQLNumPressZLib   .BuildRtIndex ("run_1")

mzSQLNoCompression  .RtProfile  (mzSQLNoCompression.BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
mzSQLZLib           .RtProfile  (mzSQLZLib.BuildRtIndex         ("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
mzSQLNumPress       .RtProfile  (mzSQLNumPress.BuildRtIndex     ("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
mzSQLNumPressZLib   .RtProfile  (mzSQLNumPressZLib.BuildRtIndex ("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))

mzMLReaderNoCompression  .BuildRtIndex ("run_1")
mzMLReaderZLib           .BuildRtIndex ("run_1")
mzMLReaderNumPress       .BuildRtIndex ("run_1")
mzMLReaderNumPressZLib   .BuildRtIndex ("run_1")

mzMLReaderNoCompression  .RtProfile  (mzMLReaderNoCompression  .BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
mzMLReaderZLib           .RtProfile  (mzMLReaderZLib           .BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
mzMLReaderNumPress       .RtProfile  (mzMLReaderNumPress       .BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))
mzMLReaderNumPressZLib   .RtProfile  (mzMLReaderNumPressZLib   .BuildRtIndex("run_1"), (new MzIO.Processing.RangeQuery(1., 0., 3000.)), (new MzIO.Processing.RangeQuery(1., 0., 3000.)))

