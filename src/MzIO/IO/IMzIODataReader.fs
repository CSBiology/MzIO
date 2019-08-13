namespace MzIO.IO


open System.Threading.Tasks
open System.Collections.Generic
open MzIO.IO
open MzIO.Binary
open MzIO.Model


//potential error source: replaced Task<'T> with Async<'T>
///Interface for the readers to share a set of basic functions that are associated with reading spectra and peak arrays.
type IMzIODataReader =

    inherit IMzIOIO

    abstract member ReadMassSpectra     : string -> IEnumerable<MassSpectrum>
    abstract member ReadMassSpectrum    : string -> MassSpectrum
    abstract member ReadSpectrumPeaks   : string -> Peak1DArray

    abstract member ReadMassSpectrumAsync   : string -> Task<MassSpectrum>  (*Async<MassSpectrum>*)
    abstract member ReadSpectrumPeaksAsync  : string -> Task<Peak1DArray>   (*Async<Peak1DArray>*)

    abstract member ReadChromatograms       : string -> IEnumerable<Chromatogram>
    abstract member ReadChromatogram        : string -> Chromatogram
    abstract member ReadChromatogramPeaks   : string -> Peak2DArray

    abstract member ReadChromatogramAsync       : string -> Async<Chromatogram>  (*Async<Chromatogram>*)
    abstract member ReadChromatogramPeaksAsync  : string -> Async<Peak2DArray>   (*Async<Peak2DArray>*)
