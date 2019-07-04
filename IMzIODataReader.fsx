

#r @"../MzIO/src/MzIO/bin/Release/net45/MzIO.dll"
#r @"../MzIO/src/MzIO.MzML/bin/Release/net45/MzIO.MzML.dll"
open MzIO
open MzIO.IO.MzML.MzML
let reader = new MzMLReader("samplestring")

(**
All readers in this project implement the interface of the IMzIODataReader. It contains abstract members 
to interact with the data. Thus all readers can be used with the same processing functions.

###ReadMassSpectra

Uses a run ID to get all spectra saved in the model.
*)
reader.ReadMassSpectra "runID"
(**
###ReadMassSpectrum

Uses a spectrum ID to get the spectrum with this ID saved in the model.
*)
reader.ReadMassSpectrum "spectrumID"