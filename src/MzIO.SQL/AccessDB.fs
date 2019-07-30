namespace MzIO.Processing


module AccessDB = 

    open MzIO.Binary
    open MzIO.IO
    open MzIO.Model
    open MzIO.SQLReader

    /// Create a new file instance of the DB schema. DELETES already existing instance
    let initDB filePath =
        let _ = System.IO.File.Delete filePath  
        let db = new MzIOSQL(filePath)
        db

    /// Returns the conncetion string to a existing MzLiteSQL DB
    let getConnection filePath =
        match System.IO.File.Exists filePath with
        | true  -> let db = new MzIOSQL(filePath)
                   db 
        | false -> initDB filePath

    /// copies MassSpectrum into DB schema
    let insertMSSpectrum (db: MzIOSQL) runID (reader:IMzIODataReader) (compress: string) (spectrum: MassSpectrum)= 
        let peakArray = reader.ReadSpectrumPeaks(spectrum.ID)
        match compress with 
        | "NoCompression"  -> 
            let clonedP = new Peak1DArray(BinaryDataCompressionType.NoCompression,peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            db.Insert(runID, spectrum, clonedP)
        | "ZLib" -> 
            let clonedP = new Peak1DArray(BinaryDataCompressionType.ZLib,peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            db.Insert(runID, spectrum, clonedP)
        | "NumPress" ->
            let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPress,peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            db.Insert(runID, spectrum, clonedP)
        | "NumPressZLib" ->
            let clonedP = new Peak1DArray(BinaryDataCompressionType.NumPressZLib,peakArray.IntensityDataType,peakArray.MzDataType)
            clonedP.Peaks <- peakArray.Peaks
            db.Insert(runID, spectrum, clonedP)
        | _ ->
            failwith "Not a valid compression Method"

    /// modifies spectrum according to the used spectrumPeaksModifierF and inserts the result into the DB schema 
    let insertModifiedSpectrumBy (spectrumPeaksModifierF: IMzIODataReader -> MassSpectrum -> string -> Peak1DArray ) (db: MzIOSQL) runID (reader:IMzIODataReader) (compress: string) (spectrum: MassSpectrum) = 
        let modifiedP = 
            spectrumPeaksModifierF reader spectrum compress
        db.Insert(runID, spectrum, modifiedP)

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

