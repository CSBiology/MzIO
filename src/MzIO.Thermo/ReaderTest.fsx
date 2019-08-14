
#r @"..\MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.Data.dll"
#r @"..\MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.BackgroundSubtraction.dll"
#r @"..\MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.MassPrecisionEstimator.dll"
#r @"..\MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.RawFileReader.dll"
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
#r @"..\MzIO.Thermo\bin\Release\net451\MzIO.Thermo.dll"


open ThermoFisher
open ThermoFisher.CommonCore
open ThermoFisher.CommonCore.RawFileReader
open ThermoFisher.CommonCore.RawFileReader.Writers
open ThermoFisher.CommonCore.Data
open ThermoFisher.CommonCore.Data.Business
open ThermoFisher.CommonCore.Data.FilterEnums
open ThermoFisher.CommonCore.Data.Interfaces
open ThermoFisher.CommonCore.BackgroundSubtraction
open ThermoFisher.CommonCore.MassPrecisionEstimator
open System
open System.Collections.Generic
open System.Runtime.InteropServices
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
open MzIO.IO.MzML.MzML
open MzIO.IO
open MzIO.Thermo


let thermoUni       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.RAW"
let termoMzML       = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.mzML"

let thermoReader        = new ThermoRawFileReader(thermoUni)
//let thermoMzMLReader    = new MzMLReader(termoMzML)

//let mzMLReader          = new MzMLReader(mzMLHome)

let getSpectras (reader:#IMzIODataReader) =
    reader.Model.Runs.GetProperties false
    |> Seq.collect (fun run -> reader.ReadMassSpectra (run.Value :?> Run).ID)

//let rtIndexEntry = wiffReader.BuildRtIndex("sample=0")

//let rtProfile = wiffReader.RtProfile (rtIndexEntry, (new MzIO.Processing.RangeQuery(1., 300., 600.)), (new MzIO.Processing.RangeQuery(1., 300., 600.)))

let mzIOSQLNoCompression    = new MzSQL(thermoUni + "NoCompression.mzIO")
let mzIOSQLZLib             = new MzSQL(thermoUni + "ZLib.mzIO")
let mzIOSQLNumPress         = new MzSQL(thermoUni + "NumPress.mzIO")
let mzIOSQLNumPressZLib     = new MzSQL(thermoUni + "NumPressZLib.mzIO")


let spectra = getSpectras thermoReader


mzIOSQLNoCompression.insertMSSpectraBy  (mzIOSQLNoCompression.insertMSSpectrum) "run_1" thermoReader BinaryDataCompressionType.NoCompression spectra
mzIOSQLZLib.insertMSSpectraBy           (mzIOSQLZLib.insertMSSpectrum)          "run_1" thermoReader BinaryDataCompressionType.ZLib spectra
mzIOSQLNumPress.insertMSSpectraBy       (mzIOSQLNumPress.insertMSSpectrum)      "run_1" thermoReader BinaryDataCompressionType.NumPress spectra
mzIOSQLNumPressZLib.insertMSSpectraBy   (mzIOSQLNumPressZLib.insertMSSpectrum)  "run_1" thermoReader BinaryDataCompressionType.NumPressZLib spectra


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

