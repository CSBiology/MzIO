﻿namespace MzIO.Processing


open System
open MzIO.IO
open MzIO.Model
open System.Collections.Generic
open MzIO.MetaData.PSIMSExtension
open MzIO.Processing.Indexer  
open MzIO.Model.CvParam


module MassSpectrum = 

    type private PSIMS_Spectrum =

        static member MsLevel = "MS:1000511"
        static member CentroidSpectrum = "MS:1000127"
        static member ProfileSpectrum = "MS:1000128"
        static member MS1Spectrum = "MS:1000579"
        static member MSnSpectrum = "MS:1000580"

    /// accesses the Header of the WiffFile referenced by the path
    let getMassSpectraBy (reader:IMzIODataReader) runID = 
        reader.ReadMassSpectra(runID)

    let getMassSpectra (reader:IMzIODataReader) =
        reader.Model.Runs.GetProperties false
        |> Seq.collect (fun (run:KeyValuePair<string, obj>) -> reader.ReadMassSpectra run.Key)

    /// accesses the Header of the WiffFile referenced by the path
    let getMassSpectrAsyncBy (reader:IMzIODataReader) runID = 
        reader.ReadMassSpectra(runID)
        
    /// Returns the ID of the MassSpectrum
    let getID (massSpectrum: MassSpectrum) =
        massSpectrum.ID  

    /// Returns a id-indexed massSpectrum
    let createIDIdxedMS (massSpectrum: MassSpectrum) =
        createIndexItemBy (getID massSpectrum) massSpectrum    
        
    /// Returns the MsLevel of the MassSpectrum 
    let getMsLevel (massSpectrum: MassSpectrum) =

        let tmp =  massSpectrum.TryGetValue(PSIMS_Spectrum.MsLevel)

        if tmp.IsSome then
            let cvParam = tmp.Value :?> IParamBase<IConvertible>
            Convert.ToInt32((tryGetValue cvParam).Value)
        else 
            -1

    /// Returns a msLevel-indexed massSpectrum
    let createMsLevelIdxedMS (massSpectrum: MassSpectrum) =
        createIndexItemBy (getMsLevel massSpectrum) massSpectrum   

    /// Returns the ScanTime (formerly: RetentionTime) of the MassSpectrum
    let getScanTime (massSpectrum: MassSpectrum) =  
        let scans =  
            massSpectrum.Scans.GetProperties false
            |> Seq.choose (fun scan -> 
                try Some (scan.Value :?> Scan)
                with
                | _ -> None
            )
        let tmp =
            let tmp2 =
                scans
                |> Seq.map (fun scan -> scan.TryGetValue(PSIMS_Scan.ScanStartTime))
                |> Seq.choose (fun param -> param)
            if Seq.isEmpty tmp2 then None
            else Seq.head tmp2 |> fun item -> Some item

        if tmp.IsSome then
            let cvParam = tmp.Value :?> IParamBase<IConvertible>
            Convert.ToDouble((tryGetValue cvParam).Value)
        else 
            -1.
    
    /// Returns a msLevel-indexed massSpectrum
    let createScanTimeIdxedMS (massSpectrum: MassSpectrum) =
        createIndexItemBy (getScanTime massSpectrum) massSpectrum  

    /// Returns PrecursorMZ of MS2 spectrum. If no PrecursorMz is present but IonMz is found, IonMz is given instead.
    let getPrecursorMZ (massSpectrum: MzIO.Model.MassSpectrum) =
        let precursors =  
            massSpectrum.Precursors.GetProperties false 
            |> Seq.map (fun precursor -> precursor.Value :?> Precursor)
        let tmp =
            let tmp2 =
                precursors
                |> Seq.collect (fun precursor -> 
                    precursor.SelectedIons.GetProperties false
                    |> Seq.map (fun selectedIon -> 
                        let selectedPrecursorMz = (selectedIon.Value :?> SelectedIon).TryGetValue(PSIMS_Precursor.SelectedPrecursorMz)
                        let selectedIonMz = (selectedIon.Value :?> SelectedIon).TryGetValue(PSIMS_Precursor.SelectedIonMz)
                        match selectedPrecursorMz,selectedIonMz with
                        | Some precMz, _ -> Some precMz
                        | None, Some ionMz -> Some ionMz
                        | None, None -> None
                    ))
                |> Seq.choose (fun param -> param)
            if Seq.isEmpty tmp2 then None
            else Seq.head tmp2 |> fun item -> Some item

        if tmp.IsSome then
            let cvParam = tmp.Value :?> IParamBase<IConvertible>
            Convert.ToDouble((tryGetValue cvParam).Value)
        else 
            -1.

    /// Returns a precursorMZ-indexed massSpectrum
    let createPrecursorMZIdxedMS (massSpectrum: MassSpectrum) =
        createIndexItemBy (getScanTime massSpectrum) massSpectrum 
    
    /// Returns Range between two Features of two MassSpectra.  
    let initCreateRange (getter: MassSpectrum -> 'b) (ms: MassSpectrum) (consMS: MassSpectrum) =
        getter ms, getter consMS
    
    /// Returns Range theoretical Range between a real feature of the last MassSpectra and a type dependend infinityValue
    let initCreateLastRange (getter: MassSpectrum -> 'b) (infinityVal: 'b) (lastMS: MassSpectrum) =
        getter lastMS, infinityVal
    
    /// Returns function which can be used to determine the range between the scanTime of two MassSpectra. 
    let createScanTimeRange =
        initCreateRange getScanTime
