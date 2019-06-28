
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.BackgroundSubtraction.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.Data.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.MassPrecisionEstimator.dll"
#r @"../MzIO.Thermo\bin\Release\net451\ThermoFisher.CommonCore.RawFileReader.dll"


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


let getOneFile  thermoPath  = FileHeaderReaderFactory.ReadFile(thermoPath)
let getMyFile   thermoPath  = RawFileReaderFactory.ReadFile(thermoPath)
let getMyThreadManager thermoPath = RawFileReaderFactory.CreateThreadManager(thermoPath)
let getAccesForThMan() (thManager:IRawFileThreadManager) = thManager.CreateThreadAccessor()


RawFileReaderAdapter.FileFactory(homePath)


let oneFile         = getOneFile homePath
let myFile          = getMyFile homePath
let myThreadManager = getMyThreadManager homePath


oneFile

//let header = RawFileReaderAdapter.FileHeaderFactory(homePath)
//let rawFileFactory = RawFileReaderAdapter.FileFactory(homePath)
//let instruments = InstrumentMethodFileReader.OpenMethod(homePath)
//let processings = ProcessingMethodFileReader.OpenProcessingMethod(homePath)
//let sequenceFiles = SequenceFileReader.OpenSequence(homePath)