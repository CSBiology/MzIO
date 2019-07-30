(***hide***)

#r @"../../../MzIO/src/MzIO/bin/Release/net45/MzIO.dll"
#r @"../../../MzIO/src/MzIO.MzML/bin/Release/net45/MzIO.MzML.dll"
#r @"../../../MzIO/src/MzIO.Processing/bin/Release/net45/MzIO.Processing.dll"
open MzIO
open MzIO.IO.MzML.MzML
let reader = new MzMLReader("samplestring") :> MzIO.IO.IMzIODataReader

(**
All readers in this project implement the interface of the IMzIODataReader. It contains abstract members 
to interact with the data. Thus all readers can be used with the same processing functions.

###ReadMassSpectrum

Uses a spectrum ID to get a specific spectrum contained by the model.
*)
reader.ReadMassSpectrum "spectrumID"
(**
Returns a MassSpectrum which ID is equal to spectrumID or fails if no MassSpectrum has a fitting ID. This MassSpectrum does not contain intensity or m/z values but
information about precusors, products and meta data information of the measured spectrum.

###ReadMassSpectra

Uses a run ID to get all spectra contained by the model.
*)
reader.ReadMassSpectra "runID"
(**
This function call returns a sequence of all spectra that are part of the same run. This doesn't include any intensity and m/z values but the metadata associated 
with the spectra measurments.

###ReadSpectrumPeaks

Uses a spectrum ID to get a Peak1DArray associated with a specific spectrum contained by the model.
*)
reader.ReadSpectrumPeaks "spectrumID"
(**
This function call returns a Peak1DArray of the MassSpectrum. This class contains the intensity and m/z values and additional information, like the compression mode and data format
of the values. By changing the compression mode the compression mode of the values will be altered when they get inserted into the SQLite database.

###ReadMassSpectrumAsync

Uses a spectrum ID to get a specific spectrum contained by the model. This function is executed multiple times in parallel.
*)
reader.ReadMassSpectrumAsync "spectrumID"
(**
Returns a MassSpectrum which ID is equal to spectrumID or fails if no MassSpectrum has a fitting ID. This MassSpectrum does not contain intensity or m/z values but
information about precusors, products and meta data information of the measured spectrum. The operation is executed in parellel to increase the speed.

###ReadSpectrumPeaksAsync

Uses a spectrum ID to get a Peak1DArray associated with a specific spectrum contained by the model. This function is executed multiple times in parallel.
*)
reader.ReadSpectrumPeaksAsync "spectrumID"
(**
This function call returns a Peak1DArray of the MassSpectrum. This class contains the intensity and m/z values and additional information, like the compression mode and data format
of the values. By changing the compression mode the compression mode of the values will be altered when they get inserted into the SQLite database.
The operation is executed in parellel to increase the speed.


###ReadChromatogram

The functions which are required to read a Chromatogram are not implemented yet.
*)
reader.ReadChromatograms            "runID"
reader.ReadChromatogram             "chromatogramID"
reader.ReadChromatogramPeaks        "chromatogramID"
reader.ReadChromatogramAsync        "chromatogramID"
reader.ReadChromatogramPeaksAsync   "chromatogramID"