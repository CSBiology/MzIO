
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
open MzIO.Thermo



let uniPath         = "D:\Users\Patrick\Desktop\BioInformatik\MzIOTestFiles\RawTestFiles\small.RAW"
let homePath        = "D:\Users\Patrick\Desktop\Universität\CSB\ThermoRawFileReader\TestFile\Angiotensin_325-ETD.raw"
let thermoUniPath   = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.RAW"


//let getOneFile  thermoPath  = FileHeaderReaderFactory.ReadFile(thermoPath)
//let getMyFile   thermoPath  = RawFileReaderFactory.ReadFile(thermoPath)
//let getMyThreadManager thermoPath = RawFileReaderFactory.CreateThreadManager(thermoPath)
//let getAccesForThMan() (thManager:IRawFileThreadManager) = thManager.CreateThreadAccessor()


let rawFileReader = new ThermoRawFileReader(thermoUniPath)

rawFileReader.Model.Runs.GetProperties false
|> Seq.collect (fun (run:KeyValuePair<string, obj>) -> rawFileReader.ReadMassSpectra run.Key)
|> Seq.length

1+1

