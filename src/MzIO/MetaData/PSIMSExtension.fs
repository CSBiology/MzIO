namespace MzIO.MetaData

open System
open System.Collections.ObjectModel
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.UO.UO
open MzIO.Binary

module PSIMSExtension =

    type private PSIMS_Units =

        static member Mz             = "MS:1000040"
        static member NumberOfCounts = "MS:1000131"

    type IHasUnit<'TPC when 'TPC :> DynamicObj> with
        member this.PSIMS_Mz()           =
            this.SetUnit(PSIMS_Units.Mz)
        member this.PSIMS_NumberOfCounts =
            this.SetUnit(PSIMS_Units.NumberOfCounts)

    type private PSIMS_Spectrum =

        static member MsLevel = "MS:1000511"
        static member CentroidSpectrum = "MS:1000127"
        static member ProfileSpectrum = "MS:1000128"
        static member MS1Spectrum = "MS:1000579"
        static member MSnSpectrum = "MS:1000580"

    type MassSpectrum with

        /// Mass spectrum created by a single-stage MS experiment
        /// or the first stage of a multi-stage experiment. [PSI:MS]
        member this.SetMS1Spectrum() =
            (this.SetCvParam(PSIMS_Spectrum.MS1Spectrum) :> IHasUnit<DynamicObj>).NoUnit() |> ignore
            this

        /// Mass spectrum created by a single-stage MS experiment
        /// or the first stage of a multi-stage experiment. [PSI:MS]
        member this.IsMS1Spectrum() =
            this.HasCvParam(PSIMS_Spectrum.MS1Spectrum)

        /// MSn refers to multi-stage MS2 experiments designed to record product ion spectra
        /// where n is the number of product ion stages (progeny ions).
        /// For ion traps, sequential MS/MS experiments can be undertaken where n > 2
        /// whereas for a simple triple quadrupole system n=2. Use the term ms level (MS:1000511)
        /// for specifying n. [PSI:MS]
        member this.SetMSnSpectrum() =
            (this.SetCvParam(PSIMS_Spectrum.MSnSpectrum) :> IHasUnit<DynamicObj>).NoUnit() |> ignore
            this

        /// MSn refers to multi-stage MS2 experiments designed to record product ion spectra
        /// where n is the number of product ion stages (progeny ions).
        /// For ion traps, sequential MS/MS experiments can be undertaken where n > 2
        /// whereas for a simple triple quadrupole system n=2. Use the term ms level (MS:1000511)
        /// for specifying n. [PSI:MS]
        member this.IsMSnSpectrum() =
            this.HasCvParam(PSIMS_Spectrum.MSnSpectrum)

        /// Stages of ms achieved in a multi stage mass spectrometry experiment. [PSI:MS]
        member this.SetMsLevel (level:int) =
            if level < 1 then
                raise (ArgumentOutOfRangeException("level"))
            (this.SetCvParam(PSIMS_Spectrum.MsLevel, level) :> IHasUnit<DynamicObj>).NoUnit() |> ignore
            this

        /// Stages of ms achieved in a multi stage mass spectrometry experiment. [PSI:MS]
        member this.TryGetMsLevel(msLevel: byref<int>) =
            if this.TryGetParam(PSIMS_Spectrum.MsLevel) then
                let tmp =
                    this.TryGetTypedValue<CvParam<IConvertible>>(PSIMS_Spectrum.MsLevel).Value
                    |> tryGetValue
                msLevel <- Convert.ToInt32 (tmp.Value)
                true
            else
                msLevel <- Unchecked.defaultof<int32>
                false

        /// Processing of profile data to produce spectra that contains discrete peaks of zero width.
        /// Often used to reduce the size of dataset. [PSI:MS]
        member this.SetCentroidSpectrum() =
            (this.SetCvParam(PSIMS_Spectrum.CentroidSpectrum):> IHasUnit<DynamicObj>).NoUnit() |> ignore
            this

        /// Processing of profile data to produce spectra that contains discrete peaks of zero width.
        /// Often used to reduce the size of dataset. [PSI:MS]
        member this.IsCentroidSpectrum() =
            this.HasCvParam(PSIMS_Spectrum.CentroidSpectrum)

        /// A profile mass spectrum is created when data is recorded with ion current (counts per second)
        /// on one axis and mass/charge ratio on another axis. [PSI:MS]
        member this.SetProfileSpectrum() =
            (this.SetCvParam(PSIMS_Spectrum.ProfileSpectrum):> IHasUnit<DynamicObj>).NoUnit() |> ignore
            this

        /// A profile mass spectrum is created when data is recorded with ion current (counts per second)
        /// on one axis and mass/charge ratio on another axis. [PSI:MS]
        member this.IsProfileSpectrum() =
            this.HasCvParam(PSIMS_Spectrum.ProfileSpectrum)

    ///Contains the accessions for different params important for isolation window defintions.
    type private PSIMS_IsolationWindow =

        static member IsolationWindowTargetMz = "MS:1000827"
        static member IsolationWindowLowerOffset = "MS:1000828"
        static member IsolationWindowUpperOffset = "MS:1000829"

    ///Add new methods to the pre defined IsolationWindow.
    type IsolationWindow with

        /// The primary or reference m/z about which the isolation window is defined. [PSI:MS]
        member this.SetIsolationWindowTargetMz(mz: double) =
            if mz < 0. then
                raise (ArgumentOutOfRangeException("mz"))
            this.SetCvParam(PSIMS_IsolationWindow.IsolationWindowTargetMz, mz).PSIMS_Mz() |> ignore
            this

        /// The primary or reference m/z about which the isolation window is defined. [PSI:MS]
        member this.TryGetIsolationWindowTargetMz(mz: byref<double>) =
            if this.TryGetParam(PSIMS_IsolationWindow.IsolationWindowTargetMz) then
                let tmp =
                    this.TryGetTypedValue<IParamBase<IConvertible>>(PSIMS_IsolationWindow.IsolationWindowTargetMz).Value
                    |> tryGetValue
                mz <- Convert.ToDouble (tmp.Value)
                true
            else
                mz <- Unchecked.defaultof<double>
                false

        /// The extent of the isolation window in m/z below the isolation window target m/z.
        /// The lower and upper offsets may be asymmetric about the target m/z. [PSI:MS]
        member this.SetIsolationWindowLowerOffset(offset: double) =
            if offset < 0. then
                raise (ArgumentOutOfRangeException("offset"))
            this.SetCvParam(PSIMS_IsolationWindow.IsolationWindowLowerOffset, offset).PSIMS_Mz() |> ignore
            this

        /// The extent of the isolation window in m/z below the isolation window target m/z.
        /// The lower and upper offsets may be asymmetric about the target m/z. [PSI:MS]
        member this.TryGetIsolationWindowLowerOffset(offset: byref<double>) =
            if this.TryGetParam(PSIMS_IsolationWindow.IsolationWindowLowerOffset) then
                let tmp =
                    this.TryGetTypedValue<IParamBase<IConvertible>>(PSIMS_IsolationWindow.IsolationWindowLowerOffset).Value
                    |> tryGetValue
                offset <- Convert.ToDouble (tmp.Value)
                true
            else
                offset <- Unchecked.defaultof<double>
                false

        /// The extent of the isolation window in m/z above the isolation window target m/z.
        /// The lower and upper offsets may be asymmetric about the target m/z. [PSI:MS]
        member this.SetIsolationWindowUpperOffset(offset: double) =
            if offset < 0. then
                raise (ArgumentOutOfRangeException("offset"))
            this.SetCvParam(PSIMS_IsolationWindow.IsolationWindowUpperOffset, offset).PSIMS_Mz() |> ignore
            this

        /// The extent of the isolation window in m/z above the isolation window target m/z.
        /// The lower and upper offsets may be asymmetric about the target m/z. [PSI:MS]
        member this.TryGetIsolationWindowUpperOffset(offset: byref<double>) =
            if this.TryGetParam(PSIMS_IsolationWindow.IsolationWindowUpperOffset) then
                let tmp =
                    this.TryGetTypedValue<IParamBase<IConvertible>>(PSIMS_IsolationWindow.IsolationWindowUpperOffset).Value
                    |> tryGetValue
                offset <- Convert.ToDouble (tmp.Value)
                true
            else
                offset <- Unchecked.defaultof<double>
                false

    ///Contains the accessions for different params important for precursor defintions.
    type PSIMS_Precursor =

        static member SelectedIonMz = "MS:1002234"
        static member ChargeState = "MS:1000041"
        static member CollisionEnergy = "MS:1000045"

    ///Add new methods to the pre defined SelectedIon.
    type SelectedIon with

        /// Mass-to-charge ratio of a precursor ion selected for fragmentation. [PSI:PI]
        member this.SetSelectedIonMz(mz: double) =
            if mz < 0. then
                raise (ArgumentOutOfRangeException("mz"))
            this.SetCvParam(PSIMS_Precursor.SelectedIonMz, mz).PSIMS_Mz() |> ignore
            this

        /// Mass-to-charge ratio of a precursor ion selected for fragmentation. [PSI:PI]
        member this.TryGetSelectedIonMz(mz: byref<double>) =
            if this.TryGetParam(PSIMS_Precursor.SelectedIonMz) then
                let tmp =
                    this.TryGetTypedValue<IParamBase<IConvertible>>(PSIMS_Precursor.SelectedIonMz).Value
                    |> tryGetValue
                mz <- Convert.ToDouble (tmp.Value)
                true
            else
                mz <- Unchecked.defaultof<double>
                false

        /// The charge state of the ion, single or multiple and positive or negatively charged. [PSI:MS]
        member this.SetChargeState(state: int) =
            (this.SetCvParam(PSIMS_Precursor.ChargeState, state):> IHasUnit<DynamicObj>).NoUnit() |> ignore
            this

    ///Add new methods to the pre defined Activation.
    type Activation with

        /// Energy for an ion experiencing collision with a stationary gas particle resulting in dissociation of the ion. [PSI:MS]
        member this.SetCollisionEnergy (ce: double) =
            if ce < 0. then
                raise (ArgumentOutOfRangeException("ce"))
            this.SetCvParam(PSIMS_Precursor.CollisionEnergy, ce).UO_Electronvolt() |> ignore
            this

    ///Contains the accessions for different params important for scan defintions.
    type PSIMS_Scan =

        static member ScanStartTime = "MS:1000016"

        static member FilterString = "MS:1000512"

    //Add new methods to the pre defined Scan.
    type Scan with

        /// The time that an analyzer started a scan, relative to the start of the MS run. [PSI:MS]
        member this.SetScanStartTime(value:double) =
            this.SetCvParam(PSIMS_Scan.ScanStartTime, value)

        /// The time that an analyzer started a scan, relative to the start of the MS run. [PSI:MS]
        member this.TryGetScanStartTime(rt:byref<double>) =
            if this.TryGetParam(PSIMS_Scan.ScanStartTime) then
                let tmp =
                    this.TryGetTypedValue<IParamBase<IConvertible>>(PSIMS_Scan.ScanStartTime).Value
                    |> tryGetValue
                rt <- Convert.ToDouble (tmp.Value)
                true
            else
                rt <- Unchecked.defaultof<double>
                false

        /// A string unique to Thermo instrument describing instrument settings for the scan. [PSI:MS]
        member this.SetFilterString(filterString:string) =
            if String.IsNullOrWhiteSpace(filterString) then                
                raise (ArgumentNullException("filterString","filterString may not be null or empty."))
            else
                (this.SetCvParam(PSIMS_Scan.FilterString, filterString) :> IHasUnit<DynamicObj>).NoUnit()    |> ignore

        /// A string unique to Thermo instrument describing instrument settings for the scan. [PSI:MS]
        member this.TryGetFilterString(filterString:byref<string>) =
            if this.TryGetParam(PSIMS_Scan.FilterString) then
                let tmp =
                    this.TryGetTypedValue<IParamBase<IConvertible>>(PSIMS_Scan.FilterString).Value
                    |> tryGetValue
                filterString <- Convert.ToString (tmp.Value)
                true
            else
                filterString <- Unchecked.defaultof<string>
                false

    //PSIMS_BinaryDataArray
    //Add new methods to the pre defined DynamicObj.
    type DynamicObj with

        /// A data array of m/z values. [PSI:MS]
        member this.SetMzArray() =
            (this.SetCvParam("MS:1000514") :> IHasUnit<DynamicObj>).PSIMS_Mz()

        /// Check wheter the dnymic object contains a cv param with specific accession or not.
        member this.IsMzArray() =
            this.HasCvParam("MS:1000514")

        /// A data array of intensity values. [PSI:MS]
        member this.SetIntensityArray() =
            this.SetCvParam("MS:1000515") :> IHasUnit<DynamicObj>

        /// Check wheter the dnymic object contains a cv param with specific accession or not.
        member this.IsIntensityArray() =
            this.HasCvParam("MS:1000515")

        /// A data array of relative time offset values from a reference time. [PSI:MS]
        member this.MS_TimeArray() =
            this.SetCvParam("MS:1000595") :> IHasUnit<DynamicObj>

        /// Check wheter the dnymic object contains a cv param with specific accession or not.
        member this.IsTimeArray() =
            this.HasCvParam("MS:1000595")

        /// No Compression. [PSI:MS]
        member this.SetNoCompression() =
            (this.SetCvParam("MS:1000576") :> IHasUnit<DynamicObj>).NoUnit()

        /// Check wheter the dnymic object contains a cv param with specific accession or not.
        member this.IsNoCompression() =
            this.HasCvParam("MS:1000576")

        /// Zlib (gzip) Compression. [PSI:MS]
        member this.SetZlibCompression() =
            (this.SetCvParam("MS:MS_1000574") :> IHasUnit<DynamicObj>).NoUnit()

        /// Check wheter the dnymic object contains a cv param with specific accession or not.
        member this.IsZlibCompression() =
            this.HasCvParam("MS:MS_1000574")

        /// Compression using MS-Numpress linear prediction compression. [https://github.com/fickludd/ms-numpress]
        member this.SetMSNumpressLinearPredictionCompression() =
            (this.SetCvParam("MS:1002312") :> IHasUnit<DynamicObj>).NoUnit()

        /// Compression using MS-Numpress positive integer compression. [https://github.com/fickludd/ms-numpress]
        member this.SetMSNumpressPositiveIntegerCompression() =
            (this.SetCvParam("MS:1002313") :> IHasUnit<DynamicObj>).NoUnit()

        /// Compression using MS-Numpress short logged float compression. [https://github.com/fickludd/ms-numpress]
        member this.SetMSNumpressShortLoggedFloatCompression() =
            (this.SetCvParam("MS:1002314") :> IHasUnit<DynamicObj>).NoUnit()

        /// Compression using both numpress compressions. [https://github.com/fickludd/ms-numpress]
        member this.SetNumpressCompression() =
            (this.SetUserParam("NumPressZLib") :> IHasUnit<DynamicObj>).NoUnit()

        /// Compression using both numpress compressions and zlib compression. [https://github.com/fickludd/ms-numpress]
        member this.SetNumpressZLibCompression() =
            (this.SetUserParam("NumPressZLib") :> IHasUnit<DynamicObj>).NoUnit()

        /// Set compression type of dynamic object.
        member this.SetCompression(compressionType:BinaryDataCompressionType) =

            match compressionType with
            | BinaryDataCompressionType.NoCompression   -> this.SetNoCompression()
            | BinaryDataCompressionType.ZLib            -> this.SetZlibCompression()
            | BinaryDataCompressionType.NumPressPic     -> this.SetMSNumpressPositiveIntegerCompression()
            | BinaryDataCompressionType.NumPressLin     -> this.SetMSNumpressLinearPredictionCompression()
            | BinaryDataCompressionType.NumPressZLib    -> this.SetNumpressZLibCompression()
            |   _                                       -> raise (NotSupportedException(sprintf "%s%s" "Compression type not supported: " (compressionType.ToString())))

        /// 64-bit precision little-endian floating point conforming to IEEE-754. [PSI:MS]
        member this.Set64BitFloat() =
            (this.SetCvParam("MS:1000523") :> IHasUnit<DynamicObj>).NoUnit()

        /// Check wheter the dnymic object contains a cv param with specific accession or not.
        member this.Is64BitFloat() =
            this.HasCvParam("MS:1000523")

        /// 32-bit precision little-endian floating point conforming to IEEE-754. [PSI:MS]
        member this.Set32BitFloat() =
            (this.SetCvParam("MS:1000521") :> IHasUnit<DynamicObj>).NoUnit()

        /// Check wheter the dnymic object contains a cv param with specific accession or not.
        member this.Is32BitFloat() =
            this.HasCvParam("MS:1000521")

        /// Signed 64-bit little-endian integer. [PSI:MS]
        member this.Set64BitInteger() =
            (this.SetCvParam("MS:1000522") :> IHasUnit<DynamicObj>).NoUnit()

        /// Check wheter the dnymic object contains a cv param with specific accession or not.
        member this.Is64BitInteger() =
            this.HasCvParam("MS:1000522")

        /// Signed 32-bit little-endian integer. [PSI:MS]
        member this.Set32BitInteger() =
            (this.SetCvParam("MS:1000519") :> IHasUnit<DynamicObj>).NoUnit()

        /// Check wheter the dnymic object contains a cv param with specific accession or not.
        member this.Set32BitInteger() =
            this.HasCvParam("MS:1000519")

        /// Set bianry data type of dynamic object.
        member this.SetBinaryDataType(binaryDataType:BinaryDataType) =
            match binaryDataType with
            | BinaryDataType.Float32    -> this.Set32BitFloat()
            | BinaryDataType.Float64    -> this.Set64BitFloat()
            | BinaryDataType.Int32      -> this.Set32BitInteger()
            | BinaryDataType.Int64      -> this.Set64BitInteger()
            | _                         -> raise (NotSupportedException(sprintf "%s%s" "Data type not supported: " (binaryDataType.ToString())))
