open MzIO.IO
open MzIO.MzSQL
open MzIO.IO.MzML

module Binning =
    
    let binBy (projection: 'a -> float) bandwidth (data: seq<'a>) =
        if bandwidth = 0. then raise (System.DivideByZeroException("Bandwidth cannot be 0."))
        let halfBw = bandwidth / 2.0
        let decBandwidth = decimal bandwidth
        let tmp = 
            data
            |> Seq.groupBy (fun x -> (decimal (projection x) / decBandwidth) |> float |> floor) 
            |> Seq.map (fun (k,values) -> 
                let count = (Seq.length(values)) |> float
                if k < 0. then
                    ((k  * bandwidth) + halfBw, values)   
                else
                    ((k + 1.) * bandwidth) - halfBw, values)
            |> Seq.sortBy fst
        tmp    
        |> Map.ofSeq

// check mirim distribution 

let sw = new System.Diagnostics.Stopwatch()
sw.Start()

let instrumentOutput2 = @"C:\Users\schne\source\repos\kMutagene\proteomiqon-tdf\testfile_combined.mzML"

let getDefaultRunID (mzReader:IMzIODataReader) = 
    match mzReader with
    | _ -> "sample=0"

let inReaderMS = new MzMLReaderMIRIM(instrumentOutput2)
let inRunID  = getDefaultRunID inReaderMS
let inTrMS = inReaderMS.BeginTransaction()

printfn $"{sw.Elapsed}: reading spectra"

let spectra = inReaderMS.ReadMassSpectra(inRunID) |> Array.ofSeq
inReaderMS.ResetReader()

let model = inReaderMS.Model
inReaderMS.ResetReader()


let fixSpectrum (m:MzIO.Model.MassSpectrum) =
    if isNull(m.Precursors) then
        m.Precursors <- new MzIO.Model.PrecursorList()
    if isNull(m.Scans) then
        m.Scans <- new MzIO.Model.ScanList()
    if isNull(m.Products) then
        m.Products <- new MzIO.Model.ProductList()
    //m.Remove("ion mobility lower limit") |> ignore    
    //m.Remove("ion mobility upper limit") |> ignore
    m

let createPeak1DArrayCopy (source: MzIO.Binary.Peak1DArray) =
    let pa = MzIO.Binary.Peak1DArray()
    pa.CompressionType <- source.CompressionType
    pa.IntensityDataType <- source.IntensityDataType
    pa.MzDataType <- source.MzDataType
    pa

let createBinnedPeaks (copyMirim: bool) (binSize: float) (peakArray: MzIO.Binary.Peak1DArray) = 

    let zippedPeaks = peakArray.Peaks |> Seq.zip (peakArray?Mirim |> unbox<float array>)

    let binnedPeakData =
        zippedPeaks
        |> Binning.binBy (fun (mirim, peak) -> mirim) binSize

    binnedPeakData
    |> Map.map(fun bin binnedData ->
        let pa = createPeak1DArrayCopy peakArray
        pa.Peaks <-
            MzIO.Commons.Arrays.ArrayWrapper(
                binnedData
                |> Seq.map snd
                |> Seq.toArray
            )
        if copyMirim then
            pa?Mirim <-
                binnedData
                |> Seq.map fst
                |> Array.ofSeq
        pa
    )


open System.IO

//let ensureFile (path:string) = if not (File.Exists(path)) then File.Create(path) |> ignore


open System.Collections.Generic

let insertBinnedSpectra (outputDirectory:string) (model: MzIO.Model.MzIOModel) (spectrumMetadata: MzIO.Model.MassSpectrum) (binnedData: Map<float,MzIO.Binary.Peak1DArray>) =

    let spectrumMetadata = fixSpectrum spectrumMetadata

    binnedData
    |> Map.iter (fun bin peaks -> 
        let outFile = Path.Combine(outputDirectory, $"binned_spectra_%.3f{bin}.mzlite")
        //printfn $"creating/writing to file {outFile}"

        let fileExisted = File.Exists(outFile)

        let outReader = new MzSQL(outFile)

        //printfn "beginning transaction"

        let outRunID  = "sample=0" //Core.MzIO.Reader.getDefaultRunID outReader
        let _ = outReader.Open()
        let outTr = outReader.BeginTransaction()

        if not fileExisted then 
            printfn "Try inserting Model."
            try
                outReader.InsertModel model
                printfn "Model inserted."
            with
            | ex -> printfn $"Inserting model failed: {ex}"

        //printfn "Try inserting spectrum."
        try 
            outReader.Insert(outRunID, spectrumMetadata, peaks)
        with
        | ex -> 
            printfn $"Inserting model failed: {ex}"

        outTr.Commit()
        outTr.Dispose()
        //printfn "Done."
    )

let createSpectraMap (outputDirectory:string) (spectra: MzIO.Model.MassSpectrum array) =
    let spectrumMap = new Dictionary<string,ResizeArray<MzIO.Model.MassSpectrum * MzIO.Binary.Peak1DArray>>()
    spectra
    |> Array.iteri (fun i spectrum -> 
        //printfn $"{sw.Elapsed}: binning spectrum {i}"
        let data = inReaderMS.getSpecificPeak1DArraySequentialWithMIRIM(spectrum.ID)
        let binResult = createBinnedPeaks false 0.002 data
        binResult
        |> Map.iter(fun bin peaks ->
            let outFile = Path.Combine(outputDirectory, $"binned_spectra_%.3f{bin}.mzlite")
            if spectrumMap.ContainsKey(outFile) then
                spectrumMap[outFile].Add(spectrum, peaks)
            else
                spectrumMap.Add(outFile, new ResizeArray<MzIO.Model.MassSpectrum * MzIO.Binary.Peak1DArray>([spectrum, peaks]))
        )
    )
    spectrumMap

open Newtonsoft.Json

//spectra
////|> Array.take 3
//|> Array.iteri(fun i spectrum -> 

//    printfn $"{sw.Elapsed}: spectrum {i}"

//    let data = inReaderMS.getSpecificPeak1DArraySequentialWithMIRIM(spectrum.ID)
//    let binResult = createBinnedPeaks false 0.002 data
//    let spectrum = fixSpectrum spectrum
//    insertBinnedSpectra
//        @"C:\Users\schne\source\repos\kMutagene\proteomiqon-tdf\src\IonMobilityBinning\test_output"
//        model
//        spectrum
//        binResult
//)

printfn $"{sw.Elapsed}: creating spectrum map"
let spectrumMap = createSpectraMap @"C:\Users\schne\source\repos\kMutagene\proteomiqon-tdf\src\IonMobilityBinning\test_output" spectra
printfn $"done creating spectrum map after {sw.Elapsed}"

printfn $"{sw.Elapsed}: starting writing binned mzlize files"
printfn $"total number of binned files: {spectrumMap.Count}"
let mutable counter = 1
for x in spectrumMap do
    let outFile = x.Key
    printfn $"[{counter}/{spectrumMap.Count}]: {outFile}"
    let spectra = x.Value
    let outReader = new MzSQL(outFile)
    let outRunID  = "sample=0" //Core.MzIO.Reader.getDefaultRunID outReader
    let _ = outReader.Open()
    let outTr = outReader.BeginTransaction()
    printfn "Try inserting Model."
    try
        outReader.InsertModel model
        printfn "Model inserted."
    with
    | ex -> printfn $"Inserting model failed: {ex}"
    for (spectrumMetadata, peaks) in spectra do
        outReader.Insert(outRunID, spectrumMetadata, peaks)
    printfn $"done after {sw.Elapsed}"
    counter <- counter + 1
    outTr.Commit()
    outTr.Dispose()

sw.Stop()
printfn $"done after {sw.Elapsed}"