
#r @"C:\Users\Student\AppData\Local\assembly\dl3\ZTWJVGVY.GBR\64GGVPLG.C52\6af6b02f\00391a6e_981fd501\ThermoFisher.CommonCore.Data.dll"
#r @"C:\Users\Student\AppData\Local\assembly\dl3\ZTWJVGVY.GBR\64GGVPLG.C52\4696de6a\00bbba2d_981fd501\ThermoFisher.CommonCore.BackgroundSubtraction.dll"
#r @"C:\Users\Student\AppData\Local\assembly\dl3\ZTWJVGVY.GBR\64GGVPLG.C52\1e5812bf\00365e82_981fd501\ThermoFisher.CommonCore.MassPrecisionEstimator.dll"
#r @"C:\Users\Student\AppData\Local\assembly\dl3\ZTWJVGVY.GBR\64GGVPLG.C52\38f29120\0022bfa3_981fd501\ThermoFisher.CommonCore.RawFileReader.dll"
//#r @"C:\Users\Student\AppData\Local\assembly\dl3\ZTWJVGVY.GBR\64GGVPLG.C52\6af6b02f\00391a6e_981fd501\ThermoFisher.CommonCore.Data.dll"


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


let uniPath     = "D:\Users\Patrick\Desktop\BioInformatik\MzIOTestFiles\RawTestFiles\small.RAW"
let homePath    = "D:\Users\Patrick\Desktop\Universität\CSB\ThermoRawFileReader\TestFile\Angiotensin_325-ETD.raw"


//let getOneFile  thermoPath  = FileHeaderReaderFactory.ReadFile(thermoPath)
//let getMyFile   thermoPath  = RawFileReaderFactory.ReadFile(thermoPath)
//let getMyThreadManager thermoPath = RawFileReaderFactory.CreateThreadManager(thermoPath)
//let getAccesForThMan() (thManager:IRawFileThreadManager) = thManager.CreateThreadAccessor()


let adapter = RawFileReaderAdapter.FileFactory(uniPath)

adapter.IsOpen
adapter.IsError

//let oneFile         = getOneFile uniPath
//let myFile          = getMyFile uniPath
//let myThreadManager = getMyThreadManager uniPath


//let header = RawFileReaderAdapter.FileHeaderFactory(homePath)
//let rawFileFactory = RawFileReaderAdapter.FileFactory(homePath)
//let instruments = InstrumentMethodFileReader.OpenMethod(homePath)
//let processings = ProcessingMethodFileReader.OpenProcessingMethod(homePath)
//let sequenceFiles = SequenceFileReader.OpenSequence(homePath)
