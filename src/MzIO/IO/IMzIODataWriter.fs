namespace MzIO.IO


open MzIO.Model
open MzIO.Binary
open MzIO.IO


type IMzIODataWriter =

    inherit IMzIOIO

    abstract member InsertMass          : string * MzIO.Model.MassSpectrum * Peak1DArray -> unit

    abstract member InsertChrom         : string * Chromatogram * Peak2DArray -> unit

    abstract member InsertAsyncMass     : string * MzIO.Model.MassSpectrum * Peak1DArray -> Async<unit>

    abstract member InsertAsyncChrom    : string * Chromatogram * Peak2DArray -> Async<unit>
