namespace MzIO.IO


open System.Threading.Tasks
open MzIO.Model
open MzIO.Binary
open MzIO.IO


//potential error source: replaced Task<'T> with Async<'T>
///Interface for the writers to share a set of basic functions that are associated with writing spectra and peak arrays.
type IMzIODataWriter =

    inherit IMzIOIO

    abstract member InsertMass          : string * MzIO.Model.MassSpectrum * Peak1DArray -> unit

    abstract member InsertChrom         : string * Chromatogram * Peak2DArray -> unit

    abstract member InsertAsyncMass     : string * MzIO.Model.MassSpectrum * Peak1DArray -> Task<unit>

    abstract member InsertAsyncChrom    : string * Chromatogram * Peak2DArray -> Task<unit>
