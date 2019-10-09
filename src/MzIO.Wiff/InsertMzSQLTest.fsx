

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


let fileDir             = __SOURCE_DIRECTORY__
let licensePath         = @"C:\Users\Student\source\repos\MzLiteFSharp\src\MzLiteFSharp.Wiff\License\Clearcore2.license.xml"

let wiffTestUni     = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.wiff"
let wiffTestHome    = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff"
let mzMLOfWiffUni   = @"C:\Users\Student\source\repos\wiffTestFiles\WiffFiles\20171129 FW LWagg001.mzML"

let bafTestUni      = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.d\analysis.baf"
let bafTestHome     = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\BafTestFiles\analysis.baf"
let bafMzMLFile     = @"C:\Users\Student\source\repos\wiffTestFiles\Bruker\170922_4597.mzML"

let thermoUni       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.RAW"
let termoMzML       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.mzML"

let wiffReader          = new WiffFileReader(wiffTestUni, licensePath)
//let bafReader           = new BafFileReader(bafTestHome)
//let thermoReader        = new ThermoRawFileReader(thermoUni)

let mzSQLNoCompression  = new MzSQL(wiffTestUni + "NoCompression.mzIO")
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
    |> Array.filter (fun x -> MzIO.Processing.MassSpectrum.getMsLevel x = 2)

let peaks =
    spectra
    |> Seq.map (fun item -> item, wiffReader.ReadSpectrumPeaks item.ID)
    |> Seq.filter (fun item -> (snd item).Peaks.Length > 0 )
    |> Seq.map (fun item -> fst item)

mzSQLNoCompression.Open()
let tr = mzSQLNoCompression.cn.BeginTransaction()
let stopWatch = System.Diagnostics.Stopwatch.StartNew()
insertMSSpectraBy insertMSSpectrum mzSQLNoCompression "run_1" wiffReader tr BinaryDataCompressionType.NoCompression peaks
let stopWatchFnished = stopWatch.Elapsed

Seq.length peaks

//mzSQLZLib.insertMSSpectraBy          (mzSQLZLib.insertMSSpectrum)           "run_1" wiffReader BinaryDataCompressionType.ZLib          spectra
//mzSQLNumPress.insertMSSpectraBy      (mzSQLNumPress.insertMSSpectrum)       "run_1" wiffReader BinaryDataCompressionType.NumPress      spectra
//mzSQLNumPressZLib.insertMSSpectraBy  (mzSQLNumPressZLib.insertMSSpectrum)   "run_1" wiffReader BinaryDataCompressionType.NumPressZLib  spectra

5+5