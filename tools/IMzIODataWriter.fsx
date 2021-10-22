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