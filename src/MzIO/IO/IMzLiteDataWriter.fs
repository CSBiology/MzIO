namespace MzLiteFSharp.IO


open MzLiteFSharp.Model
open MzLiteFSharp.Binary
open MzLiteFSharp.IO


type IMzLiteDataWriter =

    inherit IMzLiteIO

    abstract member InsertMass          : string * MzLiteFSharp.Model.MassSpectrum * Peak1DArray -> unit

    abstract member InsertChrom         : string * Chromatogram * Peak2DArray -> unit

    abstract member InsertAsyncMass     : string * MzLiteFSharp.Model.MassSpectrum * Peak1DArray -> Async<unit>

    abstract member InsertAsyncChrom    : string * Chromatogram * Peak2DArray -> Async<unit>
