namespace MzIO.IO


open System.Collections.Generic
open MzIO.IO
open MzIO.Binary
open MzIO.Model


//potential error source: replaced Task<'T> with Async<'T>
type IMzLiteDataReader =

    inherit IMzLiteIO

    abstract member ReadMassSpectra     : string -> IEnumerable<MassSpectrum>
    abstract member ReadMassSpectrum    : string -> MassSpectrum
    abstract member ReadSpectrumPeaks   : string -> Peak1DArray

    abstract member ReadMassSpectrumAsync   : string -> Async<MassSpectrum>
    abstract member ReadSpectrumPeaksAsync  : string -> Async<Peak1DArray>

    abstract member ReadChromatograms       : string -> IEnumerable<Chromatogram>
    abstract member ReadChromatogram        : string -> Chromatogram
    abstract member ReadChromatogramPeaks   : string -> Peak2DArray

    abstract member ReadChromatogramAsync       : string -> Async<Chromatogram>
    abstract member ReadChromatogramPeaksAsync  : string -> Async<Peak2DArray>
