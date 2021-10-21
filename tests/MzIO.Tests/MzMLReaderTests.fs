module MzMLReaderTests

open Expecto
open System
open System.IO
open MzIO.IO.MzML
open MzIO.Processing
open MzIO.Binary
open MzIO.Commons.Arrays

let getRelativePath (reference: string) (path: string) =
    Path.Combine(reference, path)

let relToDirectory = getRelativePath Environment.CurrentDirectory

let unzipIMzliteArray (a:IMzIOArray<Peak1D>) = 
    let mzData = Array.zeroCreate a.Length
    let intensityData = Array.zeroCreate a.Length
    for i = 0 to a.Length-1 do 
        let peak = a.[i]
        mzData.[i] <- peak.Mz
        intensityData.[i] <- peak.Intensity
    mzData,intensityData

let mzmlPath = relToDirectory "../../../data/MzMLReader/test.mzml"

let mzmlReader = new MzMLReader(mzmlPath)

[<Tests>]
let mzmlReaderTests =
    testList "MzML Reader" [
        testList "MzML Reader IMzIODataReader Functions" [
            testCase "ReadMassSpectra" <| fun () ->
                let mzmlReader = new MzMLReader(mzmlPath)
                let ms = 
                    mzmlReader.ReadMassSpectra("sample=0")
                    |> Seq.head
                Expect.isTrue(
                    MassSpectrum.getID ms = "sample=1 period=1 cycle=1 experiment=1" &&
                    MassSpectrum.getMsLevel ms = 1 &&
                    MassSpectrum.getScanTime ms = 0.00455 &&
                    MassSpectrum.getPrecursorMZ ms = -1.
                ) "Error in ReadMassSpectra"
            testCase "ReadMassSpectrum" <| fun () ->
                let mzmlReader = new MzMLReader(mzmlPath)
                let ms = mzmlReader.ReadMassSpectrum("sample=1 period=1 cycle=1 experiment=1")
                Expect.isTrue(
                    MassSpectrum.getID ms = "sample=1 period=1 cycle=1 experiment=1" &&
                    MassSpectrum.getMsLevel ms = 1 &&
                    MassSpectrum.getScanTime ms = 0.00455 &&
                    MassSpectrum.getPrecursorMZ ms = -1.
                ) "Error in ReadMassSpectrum"
            testCase "ReadSpectrumPeaks" <| fun () ->
                let mzmlReader = new MzMLReader(mzmlPath)
                let peak1D = mzmlReader.ReadSpectrumPeaks("sample=1 period=1 cycle=1 experiment=1")
                let referenceMz,referenceIntensity =
                    ([|355.0592303; 371.0840033; 371.1068972; 371.296526; 371.3126126;
                       372.0943959; 372.3256129; 373.0861547; 391.2679082; 401.838001;
                       430.8729637; 432.8753146; 445.1111714; 446.0982431; 446.1175246;
                       447.1047987; 519.1225583; 520.1170528; 593.1455627|],
                     [|1261.008545; 1740.160034; 3480.179688; 1289.520142; 4069.626221;
                       1161.824341; 516.5252075; 775.5787354; 993.0216064; 804.9103394;
                       1944.788208; 1044.487793; 5194.10791; 565.3863525; 282.6992798;
                       778.505249; 2668.755859; 1144.91394; 651.9445801|])
                let mz,intensity = unzipIMzliteArray peak1D.Peaks
                Expect.isTrue (
                    (referenceMz |> Array.map (fun mz -> Math.Round(mz,7))) = (mz |> Array.map (fun (mz:float) -> Math.Round(mz,7)))
                ) "Error in ReadSpectrumPeaks MZ Array"
                Expect.isTrue (
                    (referenceIntensity |> Array.map (fun itz -> Math.Round(itz,5))) = (intensity |> Array.map (fun itz -> Math.Round(itz,5)))
                ) "Error in ReadSpectrumPeaks Intensity Array"
                Expect.isTrue(
                    peak1D.CompressionType = BinaryDataCompressionType.NoCompression &&
                    peak1D.IntensityDataType = BinaryDataType.Float32 &&
                    peak1D.MzDataType = BinaryDataType.Float64
                ) "Error in ReadSpectrumPeaks"
        ]
        testList "MzML Reader Functions" [
            testCase "getSpecificPeak1DArraySequential" <| fun () ->
                let mzmlReader = new MzMLReader(mzmlPath)
                let peak1D = mzmlReader.getSpecificPeak1DArraySequential("sample=1 period=1 cycle=1 experiment=1")
                let referenceMz,referenceIntensity =
                    ([|355.0592303; 371.0840033; 371.1068972; 371.296526; 371.3126126;
                       372.0943959; 372.3256129; 373.0861547; 391.2679082; 401.838001;
                       430.8729637; 432.8753146; 445.1111714; 446.0982431; 446.1175246;
                       447.1047987; 519.1225583; 520.1170528; 593.1455627|],
                     [|1261.008545; 1740.160034; 3480.179688; 1289.520142; 4069.626221;
                       1161.824341; 516.5252075; 775.5787354; 993.0216064; 804.9103394;
                       1944.788208; 1044.487793; 5194.10791; 565.3863525; 282.6992798;
                       778.505249; 2668.755859; 1144.91394; 651.9445801|])
                let mz,intensity = unzipIMzliteArray peak1D.Peaks
                Expect.isTrue (
                    (referenceMz |> Array.map (fun mz -> Math.Round(mz,7))) = (mz |> Array.map (fun (mz:float) -> Math.Round(mz,7)))
                ) "Error in getSpecificPeak1DArraySequential MZ Array"
                Expect.isTrue (
                    (referenceIntensity |> Array.map (fun itz -> Math.Round(itz,5))) = (intensity |> Array.map (fun itz -> Math.Round(itz,5)))
                ) "Error in getSpecificPeak1DArraySequential Intensity Array"
                Expect.isTrue(
                    peak1D.CompressionType = BinaryDataCompressionType.NoCompression &&
                    peak1D.IntensityDataType = BinaryDataType.Float32 &&
                    peak1D.MzDataType = BinaryDataType.Float64
                ) "Error in getSpecificPeak1DArraySequential"
        ]
    ]