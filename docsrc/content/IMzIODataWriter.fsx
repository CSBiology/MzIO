(***hide***)

#r @"../../../MzIO/src/MzIO/bin/Release/net45/MzIO.dll"
#r @"../../../MzIO/src/MzIO.MzML/bin/Release/net45/MzIO.MzML.dll"
#r @"../../../MzIO/src/MzIO.Processing/bin/Release/net45/MzIO.Processing.dll"
open MzIO
open MzIO.IO.MzML
let writer = new MzMLWriter() :> MzIO.IO.IMzIODataWriter
let peak1D = new Binary.Peak1DArray()
let peak2D = new Binary.Peak2DArray()
let massSpec = new Model.MassSpectrum()
let chromatogram = new Model.Chromatogram()

(**
All writers in this project implement the interface of the IMzIODataWriter. It contains abstract members 
to interact with the data. Thus all writers can be used with the same processing functions.

###InsertMass

Uses a spectrum ID to insert data from a Peak1DArray into a mass spectrum.
*)
writer.InsertMass("spectrumID", massSpec, peak1D)
(**
###InsertChrom

Uses a chromatogram ID to insert data from a Peak2DArray into a chromatogram.
*)
writer.InsertChrom("chromatogramID", chromatogram, peak2D)