namespace MzIO.Processing

module PeakArray = 

    open MzIO.Binary
    open MzIO.Commons
    open MzIO.IO


    /// Returns a MzIO.Binary.Peak1DArray
    let getPeak1DArray (reader:IMzIODataReader) msID = 
        reader.ReadSpectrumPeaks(msID)

    /// Returns a mzData Array of a peak1DArray
    let mzDataOf (peak1DArray: Peak1DArray) =
        peak1DArray.Peaks 
        |> Seq.map (fun peak -> peak.Mz)
        |> Array.ofSeq

    /// Returns a intensityData Array of a peak1DArray
    let intensityDataOf (peak1DArray: Peak1DArray) =
        peak1DArray.Peaks 
        |> Seq.map (fun peak -> peak.Intensity)
        |> Array.ofSeq
        
    /// Returns tuple of a mzData Array and intensityData Array of a peak1DArray
    let mzIntensityArrayOf (peak1DArray: Peak1DArray) =
         peak1DArray.Peaks
         |> Seq.map (fun peak -> peak.Mz, peak.Intensity) //TODO  mutable Ansatz
         |> Array.ofSeq
         |> Array.unzip
    
    /// Creates Peak1DArray of mzData array and intensityData Array
    let createPeak1DArray compression mzBinaryDataType intensityBinaryDataType (mzData:float []) (intensityData:float []) =
        let peak1DArray = new Peak1DArray(compression,intensityBinaryDataType, mzBinaryDataType)
        let zipedData = Array.map2 (fun mz intz -> Peak1D(intz,mz)) mzData intensityData 
        let newPeakA = Arrays.MzIOArray.ToMzIOArray zipedData
        peak1DArray.Peaks <- newPeakA
        peak1DArray

