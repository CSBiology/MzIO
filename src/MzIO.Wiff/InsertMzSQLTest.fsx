

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
open MzIO.Processing.Indexer
open MzIO.Processing.MassSpectrum


let fileDir         = __SOURCE_DIRECTORY__
let licensePath     = @"C:\Users\Student\source\repos\MzLiteFSharp\src\MzLiteFSharp.Wiff\License\Clearcore2.license.xml"
let licenseHome     = @"C:\Users\Patrick\source\repos\MzLiteFSharp\src\MzLiteFSharp.Wiff\License\Clearcore2.license.xml"

let wiffTestUni     = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.wiff"
let wiffTestHome    = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff"
let mzMLOfWiffUni   = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.mzML"

let bafTestUni      = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.d\analysis.baf"
let bafTestHome     = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\BafTestFiles\analysis.baf"
let bafMzMLFile     = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.mzML"

let thermoUni       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.RAW"
let termoMzML       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.mzML"

let wiffReader          = new WiffFileReader(wiffTestHome, licenseHome)
//let bafReader           = new BafFileReader(bafTestHome)
//let thermoReader        = new ThermoRawFileReader(thermoUni)

let mzSQLNoCompression  = new MzSQL(wiffTestHome + "NoCompression.mzIO")
//let mzSQLZLib           = new MzSQL(wiffTestUni + "ZLib.mzIO")
//let mzSQLNumPress       = new MzSQL(wiffTestUni + "NumPress.mzIO")
//let mzSQLNumPressZLib   = new MzSQL(wiffTestUni + "NumPressZLib.mzIO")

//let mzMLNoCompression   = new MzMLWriter(wiffTestUni + "NoCompression.mzml")
//let mzMLZLib            = new MzMLWriter(wiffTestUni + "ZLib.mzml")
//let mzMLNumPress        = new MzMLWriter(wiffTestUni + "NumPress.mzml")
//let mzMLNumPressZLib    = new MzMLWriter(wiffTestUni + "NumPressZLib.mzml")

//let mzMLReaderNoCompression = new MzMLReader(wiffTestUni + "NoCompression.mzml")
//let mzMLReaderZLib          = new MzMLReader(wiffTestUni + "ZLib.mzml")
//let mzMLReaderNumPress      = new MzMLReader(wiffTestUni + "NumPress.mzml")
//let mzMLReaderNumPressZLib  = new MzMLReader(wiffTestUni + "NumPressZLib.mzml")

let spectra =
    wiffReader.Model.Runs.GetProperties false
    |> Seq.map (fun item -> item.Value :?> Run)
    |> Seq.head
    |> (fun run -> wiffReader.ReadMassSpectra run.ID)
    |> Array.ofSeq
    |> Array.filter (fun x -> MzIO.Processing.MassSpectrum.getMsLevel x = 1)
    |> Array.take 10

//let peaks =
//    spectra
//    |> Seq.map (fun item -> item, wiffReader.ReadSpectrumPeaks item.ID)
//    |> Seq.filter (fun item -> (snd item).Peaks.Length > 0 )
//    |> Seq.map (fun item -> fst item)

mzSQLNoCompression.Open()
let tr = mzSQLNoCompression.cn.BeginTransaction()
let stopWatch = System.Diagnostics.Stopwatch.StartNew()
insertMSSpectraBy insertMSSpectrum mzSQLNoCompression "run_1" wiffReader tr BinaryDataCompressionType.NoCompression spectra
let stopWatchFnished = stopWatch.Elapsed

//mzSQLZLib.Open()
//let trZLIB = mzSQLZLib.cn.BeginTransaction()
//insertMSSpectraBy insertMSSpectrum mzSQLZLib "run_1" wiffReader trZLIB BinaryDataCompressionType.ZLib spectra

//mzSQLNumPress.Open()
//let trNumPress = mzSQLNumPress.cn.BeginTransaction()
//insertMSSpectraBy insertMSSpectrum mzSQLNumPress "run_1" wiffReader trNumPress BinaryDataCompressionType.NumPress spectra

//mzSQLNumPressZLib.Open()
//let trNumPressZLIB = mzSQLNumPressZLib.cn.BeginTransaction()
//insertMSSpectraBy insertMSSpectrum mzSQLNumPressZLib "run_1" wiffReader trNumPressZLIB BinaryDataCompressionType.NumPressZLib spectra

//let runID =
//    wiffReader.Model.Runs.GetProperties false
//    |> Seq.map (fun item -> item.Value :?> Run)
//    |> Seq.head
//    |> (fun run -> run.ID)

//wiffReader.GetRSaturationValues(spectra.[0].ID)

mzSQLNoCompression.Model.Runs.GetProperties false
|> Seq.head
|> fun item -> item.Value :?> Run

