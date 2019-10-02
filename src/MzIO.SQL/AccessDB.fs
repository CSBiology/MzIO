namespace MzIO.Processing


open System.Data.SQLite
open MzIO.Model
open MzIO.Binary
open MzIO.IO
open MzIO.MzSQL


module MassSpectrum = 


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
    let insertMSSpectrum (db: MzSQL) runID (reader:IMzIODataReader) (compress:BinaryDataCompressionType) (spectrum: MassSpectrum)= 
        let peakArray = reader.ReadSpectrumPeaks(spectrum.ID)
        let clonedP = new Peak1DArray(compress,peakArray.IntensityDataType, peakArray.MzDataType)
        clonedP.Peaks <- peakArray.Peaks
        db.Insert(runID, spectrum, clonedP)


    /// Starts bulkinsert of mass spectra into a MzLiteSQL database
    let insertMSSpectraBy insertSpectrumF (db:MzSQL) runID (reader:IMzIODataReader) (tr:SQLiteTransaction) (compress: BinaryDataCompressionType) (spectra: seq<MassSpectrum>) = 
        //let db = getConnection outFilepath
        let bulkInsert spectra = 
            spectra
            |> Seq.iter (insertSpectrumF db runID reader compress)
        bulkInsert spectra
        tr.Commit()
        tr.Dispose() 
        db.Dispose()
