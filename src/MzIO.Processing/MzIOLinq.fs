namespace MzIO.Processing


open System
open System.Collections.Generic
open System.Linq
open MzIO.Binary
open MzIO.Commons.Arrays
open MzIO.Model
open MzIO.MetaData.PSIMSExtension
open MzIO.IO
open MzIO
open MzIO.Processing


///Module that contains classes with methods to search, sort and group mass spectra based on their properties, e.g. ms level.
module MzIOLinq =

    type IEnumerable<'T> with

        /// Find the first item at val function mininum value.
        member this.ItemAtMin<'T, 'TValue when 'TValue :> IComparable<'TValue>> (valFunc: Func<'T, 'TValue>) =
            let enumerator =  this.GetEnumerator()
            if not (enumerator.MoveNext()) then
                raise (new InvalidOperationException("Sequence contains no elements."))
            let rec loop min =
                if enumerator.MoveNext() then
                    let item = enumerator.Current
                    let value = valFunc.Invoke(item)
                    if (value.CompareTo(valFunc.Invoke(min)) < 0) then
                        loop item
                    else
                        loop min
                else
                    min
            loop enumerator.Current

        /// Find the first item at val function maximum value.
        member this.ItemAtMax<'T, 'TValue when 'TValue :> IComparable<'TValue>> (valFunc: Func<'T, 'TValue>) =
            let enumerator =  this.GetEnumerator()
            if not (enumerator.MoveNext()) then
                raise (new InvalidOperationException("Sequence contains no elements."))
            let rec loop max =
                if enumerator.MoveNext() then
                    let item = enumerator.Current
                    let value = valFunc.Invoke(item)
                    if (value.CompareTo(valFunc.Invoke(max)) > 0) then
                        loop item
                    else
                        loop max
                else
                    max
            loop enumerator.Current

    /// Connects retention times with spectrum ids.
    type RtIndexEntry(rt:float, spectrumID:string) =

        let spectrumID =
            if String.IsNullOrWhiteSpace(spectrumID) then
                raise (ArgumentNullException("runID"))
            else 
                spectrumID

        member this.Rt = rt

        member this.SpectrumID = spectrumID

        new() = new RtIndexEntry(0., "id")

        override this.ToString() =
            String.Format("[rt={0}, spectrumID='{1}']", rt, spectrumID)

        static member private trygetScan(item:Object) =
            match item:?Scan with
            | true  -> Some(item :?> Scan)
            | false -> None

        /// Tries to create a RtIndexEntry based on mass spectrum and ms level.
        static member TryCreateEntry(ms: MassSpectrum, msLevel: int, entry: byref<RtIndexEntry>) =
            let mutable msLevel' = 0
            if   ms.TryGetMsLevel(& msLevel') = false || msLevel' <> msLevel then false            
            elif ms.Scans.Count() < 1 then false
            else
                let scan = 
                    ms.Scans.GetProperties false
                    |> Seq.map (fun item -> RtIndexEntry.trygetScan(item.Value))
                    |> Seq.choose(fun item -> item)
                    |> Seq.head
                let mutable rt = 0.
                if scan.TryGetScanStartTime(& rt) then
                    entry <- new RtIndexEntry(rt, ms.ID)
                    true
                else
                    false

        /// Checks whether retention time lies within range of the query.
        static member RtSearchCompare1<'T when 'T :> RtIndexEntry>(entry: RtIndexEntry, rtRange: RangeQuery) =

            if   entry.Rt < rtRange.LowValue  then -1
            elif entry.Rt > rtRange.HighValue then 1
            else 0

        /// Checks whether m/z lies within range of the query.
        static member MzSearchCompare<'TPeak when 'TPeak :> Peak1D>(p: 'TPeak, mzRange: RangeQuery) =

            if   p.Mz < mzRange.LowValue  then -1
            elif p.Mz > mzRange.HighValue then 1
            else 0

        /// Checks whether retention time lies within range of the query.
        static member RtSearchCompare2<'TPeak when 'TPeak :> Peak2D>(p: 'TPeak, rtRange: RangeQuery) =

            if   p.Rt < rtRange.LowValue  then -1
            elif p.Rt > rtRange.HighValue then 1
            else 0

        /// Get all rt index entries by rt range.
        static member Search(rti: IMzIOArray<RtIndexEntry>, rtRange: RangeQuery) =
            BinarySearch.Search(rti, rtRange, RtIndexEntry.RtSearchCompare1)

        /// Get all peaks by rt range.
        static member RtSearch(peaks: IMzIOArray<'TPeak>, rtRange: RangeQuery) =
            BinarySearch.Search(peaks, rtRange, RtIndexEntry.RtSearchCompare2)

        /// Get all peaks by mz range.
        static member MzSearch(peaks: IMzIOArray<'TPeak>, mzRange: RangeQuery) =
            BinarySearch.Search(peaks, mzRange, RtIndexEntry.MzSearchCompare)

        /// Gets the peak closest to lock mz.
        static member ClosestMz<'TPeak when 'TPeak :> Peak1D>(peaks: IEnumerable<'TPeak>, lockMz: double) =
            peaks.ItemAtMin(fun x -> Math.Abs(x.Mz - lockMz))

        /// Gets the peak closest to lock rt.
        static member ClosestRt<'TPeak when 'TPeak :> Peak2D>(peaks: IEnumerable<'TPeak>, lockRt: double) =
            peaks.ItemAtMin(fun x -> Math.Abs(x.Rt - lockRt))

        /// Gets the peak at max intensity.
        static member MaxIntensity<'TPeak when 'TPeak :> Peak>(peaks: IEnumerable<'TPeak>) =
            peaks.ItemAtMax(fun x -> x.Intensity)

        /// Create Peak2D based on Peak1D and a retention time.
        static member AsPeak2D(p: Peak1D, rt: double) =
            new Peak2D(p.Intensity, p.Mz, rt)

    /// Type that has the rules for sorting retention time index entries.
    type private RtIndexEntrySorting() =

        interface IComparer<RtIndexEntry> with
            
            member this.Compare(x: RtIndexEntry, y: RtIndexEntry) =
                x.Rt.CompareTo(y.Rt)

    type IMzIODataReader with

        member this.ReadSpectrumPeaks(entry: RtIndexEntry) =
            this.ReadSpectrumPeaks(entry.SpectrumID)

        member this.ReadMassSpectrum(entry: RtIndexEntry) =
            this.ReadMassSpectrum(entry.SpectrumID)

        /// Builds an in memory retention time index of mass spectra ids.
        member this.BuildRtIndex(runID:string, ?msLevel: int) =
            let msLevel = defaultArg msLevel 1
            let runID =
                if String.IsNullOrWhiteSpace(runID) then
                    raise (ArgumentNullException("runID"))
                else
                    runID
            let massSpectra   = this.ReadMassSpectra(runID)
            let entries       = new List<RtIndexEntry>()
            let mutable entry = new RtIndexEntry()
            //for ms in massSpectra do
            //    if RtIndexEntry.TryCreateEntry(ms, msLevel, & entry) then
            //        entries.Add(entry)
            massSpectra
            |> Seq.iter (fun ms -> 
                            RtIndexEntry.TryCreateEntry(ms, msLevel, & entry) |> ignore
                            entries.Add(entry)
                        )
            entries.Sort(new RtIndexEntrySorting())
            MzIOArray.ToMzIOArray(entries)

        /// Extract a rt profile matrix for specified target masses and rt range.
        /// Mz range peak aggregation is closest lock mz.
        /// Profile matrix with first index corresponds to continous mass spectra over rt range
        /// and second index corresponds to mz ranges given.
        member this.RtProfiles(rtIndex: IMzIOArray<RtIndexEntry>, rtRange: RangeQuery, mzRanges: RangeQuery[]) =

            let entries = RtIndexEntry.Search(rtIndex, rtRange).ToArray()
            let profile = Array2D.zeroCreate<Peak2D> entries.Length mzRanges.Length
            for rtIdx = 0 to entries.Length-1 do
                let entry = entries.[rtIdx]
                let peaks = this.ReadSpectrumPeaks(entry).Peaks
                for mzIdx = 0 to mzRanges.Length do
                    let mzRange = mzRanges.[mzIdx]
                    let p = (RtIndexEntry.MzSearch (peaks, mzRange)).DefaultIfEmpty(new Peak1D(0., mzRange.LockValue))
                            |> fun x -> RtIndexEntry.ClosestMz (x, mzRange.LockValue)
                            |> fun x -> RtIndexEntry.AsPeak2D (x, entry.Rt)
                    profile.[rtIdx, mzIdx] <- p
            profile

        /// Extract a rt profile for specified target mass and rt range.
        /// Mz range peak aggregation is closest lock mz.
        /// Profile array with index corresponding to continous mass spectra over rt range and mz range given.
        member this.RtProfile(rtIndex: IMzIOArray<RtIndexEntry>, rtRange: RangeQuery, mzRange: RangeQuery) =

            let entries = RtIndexEntry.Search(rtIndex, rtRange).ToArray()

            let profile = Array.zeroCreate<Peak2D> entries.Length

            for rtIdx = 0 to entries.Length-1 do

                let entry = entries.[rtIdx]

                let peaks = this.ReadSpectrumPeaks(entry).Peaks

                let p = (RtIndexEntry.MzSearch (peaks, mzRange)).DefaultIfEmpty(new Peak1D(0., mzRange.LockValue))
                        |> fun x -> RtIndexEntry.ClosestMz (x, mzRange.LockValue)
                        |> fun x -> RtIndexEntry.AsPeak2D (x, entry.Rt)
                profile.[rtIdx] <- p

            profile
