namespace MzIOTest


module AccessMassSpectrum = 

    open System
    open MzIO.IO
    open MzIO.Model
    open System.Collections.Generic
    open MzIO.MetaData.PSIMSExtension
    open MzIO.Processing.Indexer  
    open MzIO.Model.CvParam

    /// accesses the Header of the WiffFile referenced by the path
    let getMassSpectraBy (reader:IMzIODataReader) runID = 
        reader.ReadMassSpectra(runID)

    let getMassSpectra (reader:IMzIODataReader) =
        reader.Model.Runs.GetProperties false
        |> Seq.collect (fun (run:KeyValuePair<string, obj>) -> reader.ReadMassSpectra run.Key)

    ///// accesses the Header of the WiffFile referenced by the path
    //let getMassSpectrAsyncBy (reader:IMzLiteDataReader) runID = 
    //    reader.ReadMassSpectra(runID)
        
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
            (tryGetValue cvParam).Value :?> int
        else 
            -1

    /// Returns a msLevel-indexed massSpectrum
    let createMsLevelIdxedMS (massSpectrum: MassSpectrum) =
        createIndexItemBy (getMsLevel massSpectrum) massSpectrum   

    /// Returns the ScanTime (formerly: RetentionTime) of the MassSpectrum
    let getScanTime (massSpectrum: MassSpectrum) =  
        let tmp =  massSpectrum.TryGetValue(PSIMS_Scan.ScanStartTime)

        if tmp.IsSome then
            let cvParam = tmp.Value :?> IParamBase<IConvertible>
            (tryGetValue cvParam).Value :?> double
        else 
            -1.
    
    /// Returns a msLevel-indexed massSpectrum
    let createScanTimeIdxedMS (massSpectrum: MassSpectrum) =
        createIndexItemBy (getScanTime massSpectrum) massSpectrum  

    /// Returns PrecursorMZ of MS2 spectrum
    let getPrecursorMZ (massSpectrum: MzIO.Model.MassSpectrum) =
        let tmp =  massSpectrum.TryGetValue(PSIMS_Precursor.SelectedIonMz)

        if tmp.IsSome then
            let cvParam = tmp.Value :?> IParamBase<IConvertible>
            (tryGetValue cvParam).Value :?> double
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

