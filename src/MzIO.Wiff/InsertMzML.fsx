

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

let mzMLNoCompression   = new MzMLWriter(wiffTestUni + "NoCompression.mzml")
//let mzMLZLib            = new MzMLWriter(wiffTestUni + "ZLib.mzml")
//let mzMLNumPress        = new MzMLWriter(wiffTestUni + "NumPress.mzml")
//let mzMLNumPressZLib    = new MzMLWriter(wiffTestUni + "NumPressZLib.mzml")


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


let stopWatch = System.Diagnostics.Stopwatch.StartNew()
mzMLNoCompression.insertMSSpectraBy mzMLNoCompression.insertMSSpectrum "run_1" wiffReader BinaryDataCompressionType.NoCompression spectra
let stopWatchFnished = stopWatch.Elapsed

//Seq.length peaks

5+5 

let instrument =
    wiffReader.Model.Instruments.GetProperties false
    |>Seq.head
    |> fun item -> item.Value :?> Instrument

let software = instrument.Software

let fileContent = wiffReader.Model.FileDescription

fileContent.Contact.Count()
