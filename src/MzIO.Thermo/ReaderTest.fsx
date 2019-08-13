
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


let homePath        = "D:\Users\Patrick\Desktop\Universität\CSB\ThermoRawFileReader\TestFile\Angiotensin_325-ETD.raw"
let thermoUniPath   = @"C:\Users\Student\source\repos\wiffTestFiles\Thermo\data02.RAW"


let getOneFile  thermoPath  = RawFileReaderAdapter.FileFactory(thermoUniPath)
//let getMyFile   thermoPath  = RawFileReaderFactory.ReadFile(thermoPath)
//let getMyThreadManager thermoPath = RawFileReaderFactory.CreateThreadManager(thermoPath)
//let getAccesForThMan() (thManager:IRawFileThreadManager) = thManager.CreateThreadAccessor()

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

let rawFileReader = new ThermoRawFileReader(thermoUniPath)

let peaks =
    rawFileReader.Model.Runs.GetProperties false
    |> Seq.collect (fun (run:KeyValuePair<string, obj>) -> rawFileReader.ReadMassSpectra run.Key)
    |> Seq.map (fun spectrum -> rawFileReader.ReadSpectrumPeaks spectrum.ID)

//insertMSSpectraBy insertMSSpectrum (thermoUniPath + ".mzio") ("run_1") rawFileReader "NoCompression" spectra
peaks
|> Seq.length

1+1

let brukaRTIndex = rawFileReader.BuildRtIndex("run_1")
let brukaRT = rawFileReader.RtProfile(brukaRTIndex, (new MzIO.Processing.RangeQuery(1., 300., 600.)), (new MzIO.Processing.RangeQuery(1., 300., 600.)))
