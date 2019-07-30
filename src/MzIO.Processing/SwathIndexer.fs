namespace MzIO.Processing

open System
open System.Linq
open System.Collections.Generic
open MzIO.IO
open MzIO.MetaData.PSIMSExtension(*.PSIMS*)
open MzIO.Model
open MzIO.Binary
open MzIO.Processing
open MzIO.Processing.MzIOLinq

[<Sealed>]
type SwathQuery(targetMz:double, rtRange:RangeQuery, ms2Masses:RangeQuery[]) =

        let failCheck =
            if Array.isEmpty ms2Masses then 
                raise (ArgumentNullException("ms2Masses"))
            else ()

        member this.TargetMz = targetMz
        member this.RtRange = rtRange
        member this.Ms2Masses = ms2Masses
        member this.CountMS2Masses = ms2Masses.Length

[<Sealed>]
type SwathQuerySorting() =

    member private this.Sort(queries:SwathQuery[]) =
        Array.Sort(queries, new SwathQuerySorting())

    interface IComparer<SwathQuery> with
        member this.Compare(x:SwathQuery, y:SwathQuery) =

            let res = x.TargetMz.CompareTo(y.TargetMz)
            if res <> 0 then res
            else
                x.RtRange.LockValue.CompareTo(y.RtRange.LockValue)

module SwathIndexer =

    type SwathWindow(targetMz:float, lowMz:float, heighMz:float) =

        let mutable targetMz    = targetMz
        let mutable lowMz       = lowMz
        let mutable heighMz     = heighMz

        new () = new SwathWindow(0., 0., 0.)

        member this.TargetMz
             with get() = targetMz
             and private set(value) = targetMz <- value

        member this.LowMz
             with get() = lowMz
             and private set(value) = lowMz <- value

        member this.HeighMz
             with get() = heighMz
             and private set(value) = heighMz <- value

    type SwathSpectrumEntry(spectrumID:string, targetMz:float, lowMz:float, heighMz:double, rt:double) =

        let mutable spectrumID  = spectrumID
        let mutable swathWindow = new SwathWindow(targetMz, lowMz, heighMz)
        let mutable rt          = rt

        member this.SpectrumID
             with get() = spectrumID
             and private set(value) = spectrumID <- value

        member this.SwathWindow
             with get() = swathWindow
             and private set(value) = swathWindow <- value

        member this.Rt
             with get() = rt
             and private set(value) = rt <- value

        static member TryCreateSwathSpectrum(ms:MassSpectrum) =

            let mutable msLevel = 0
            if ms.TryGetMsLevel(& msLevel) = false || msLevel <> 2 then None
            else
                if ms.Precursors.Count() < 1 || ((ms.Precursors.GetProperties false |> Seq.head).Value :?> Precursor).SelectedIons.Count() < 1 || ms.Scans.Count() < 1 then None
                else
                    let mutable rt = 0.
                    let mutable mz = 0.
                    let mutable mzLow = 0.
                    let mutable mzHeigh = 0.

                    let isoWin  = ((ms.Precursors.GetProperties false |> Seq.head).Value :?> Precursor).IsolationWindow
                    let scan    = ((ms.Precursors.GetProperties false |> Seq.head).Value :?> Scan)

                    if (scan.TryGetScanStartTime(& rt) &&
                        isoWin.TryGetIsolationWindowTargetMz(& mz) &&
                        isoWin.TryGetIsolationWindowLowerOffset(& mzLow) &&
                        isoWin.TryGetIsolationWindowUpperOffset(& mzHeigh))

                        then Some (new SwathSpectrumEntry(ms.ID, mz, mz - mzLow, mz + mzHeigh, rt))
                        else None

        static member Scan(spectra:IEnumerable<MassSpectrum>) =
            spectra
            |> Seq.map (fun ms -> SwathSpectrumEntry.TryCreateSwathSpectrum ms)
            |> Seq.choose (fun ms -> ms)

    type SwathSpectrumSortingComparer() =

        interface IComparer<SwathSpectrumEntry> with
            member this.Compare(x:SwathSpectrumEntry, y:SwathSpectrumEntry) =
                x.Rt.CompareTo(y.Rt)

    type MSSwath(sw:SwathWindow, swathSpectra:SwathSpectrumEntry[]) =

        let mutable swathWindow     = sw
        let mutable swathSpectra    = swathSpectra
        let mutable     x           = Array.Sort(swathSpectra, new SwathSpectrumSortingComparer())

        new(spectraLength) = new MSSwath(new SwathWindow(), Array.zeroCreate<SwathSpectrumEntry>(spectraLength))
        new() = new MSSwath(new SwathWindow(), [||])

        member this.SwathWindow
             with get() = swathWindow
             and private set(value) = swathWindow <- value

        member this.SwathSpectra
             with get() = swathSpectra
             //and private set(value) = swathSpectra <- value

        member this.SearchAllRt(query:SwathQuery) =

                BinarySearch.Search(this.SwathSpectra, query, MSSwath.RtRangeCompare)

        member this.SearchClosestRt(query:SwathQuery) =

            let mutable result = Some (new IndexRange())

            if BinarySearch.Search(this.SwathSpectra, query, MSSwath.RtRangeCompare, & result)=true then
                Some (IndexRange.EnumRange(this.SwathSpectra, result.Value).ItemAtMin(fun x -> MSSwath.CalcLockRtDiffAbs(x, query)))
            else None

        static member internal RtRangeCompare(item:SwathSpectrumEntry, query:SwathQuery) =
            if item.Rt < query.RtRange.LowValue then -1
            else
                if item.Rt > query.RtRange.HighValue then 1
                else 0

        static member internal CalcLockRtDiffAbs(swathSpectrum:SwathSpectrumEntry, query:SwathQuery) =
            Math.Abs(swathSpectrum.Rt - query.RtRange.LockValue)

    type MSSwathSortingComparer() =

        interface IComparer<MSSwath> with
            member this.Compare(x:MSSwath, y:MSSwath) =

                let c = x.SwathWindow.LowMz.CompareTo(y.SwathWindow.LowMz)
                if c <> 0 then c
                else
                    x.SwathWindow.HeighMz.CompareTo(y.SwathWindow.HeighMz)

    type SwathList(swathes:MSSwath[]) =

        let mutable swathes = swathes
        let mutable     x   = Array.Sort(swathes, new MSSwathSortingComparer())

        member this.Swathes = swathes

        member this.SearchAllTargetMz(targetMz:float) =

            BinarySearch.Search(swathes, targetMz, SwathList.SearchCompare)

        static member internal SearchCompare(item:MSSwath, targetMz:float) =
            if item.SwathWindow.HeighMz < targetMz then -1
            else
                if item.SwathWindow.LowMz > targetMz then 1
                else 0

        member this.SearchClosestTargetMz(query:SwathQuery) =

            let mutable result = Some (new IndexRange())
            if BinarySearch.Search(this.Swathes, query.TargetMz, SwathList.SearchCompare, & result)=true then
                Some (IndexRange.EnumRange(this.Swathes, result.Value).ItemAtMin(fun x -> SwathList.CalcTargetMzDiffAbs(x, query)))
            else None

        static member internal CalcTargetMzDiffAbs(swath:MSSwath, query:SwathQuery) =

            Math.Abs(swath.SwathWindow.TargetMz - query.TargetMz)

    type private SwathWindowGroupingComparer() =

        interface IEqualityComparer<SwathWindow> with

            member this.Equals(x:SwathWindow, y:SwathWindow) =

                x.LowMz.Equals(y.LowMz) && x.TargetMz.Equals(y.TargetMz) && x.HeighMz.Equals(y.HeighMz)

            member this.GetHashCode(item:SwathWindow) =

                (item.LowMz, item.TargetMz, item.HeighMz).GetHashCode()

    type SwathIndexer(swathList:SwathList) =

        member this.SwathList = swathList

        static member Create(dataReader:IMzIODataReader, runID:string) =

            let mutable runID =
                if String.IsNullOrWhiteSpace(runID) then
                    raise (ArgumentNullException("runID"))
                else
                    runID

            let spectra = SwathSpectrumEntry.Scan(dataReader.ReadMassSpectra(runID))
            let groups = (spectra.GroupBy(keySelector = (fun spectrum -> spectrum.SwathWindow), comparer = new SwathWindowGroupingComparer())).ToArray()
            let swathes =
                groups
                |> Array.map (fun group -> new MSSwath(group.Key, group.ToArray()))
            let swathList = new SwathList(swathes)
            new SwathIndexer(swathList)

        member this.GetMS2(dataReader:IMzIODataReader, query:SwathQuery(*, mzRangeSelector:Func<IEnumerable<Peak1D>, RangeQuery, Peak1D>*)) =

            let mutable mzRangeSelector = SwathIndexer.GetClosestMz

            let mutable swath = swathList.SearchClosestTargetMz(query)

            if swath.IsNone then Array.zeroCreate<Peak2D> 0
                else
                    let mutable swathSpec = swath.Value.SearchClosestRt(query)
                    if swathSpec.IsNone then Array.zeroCreate<Peak2D> 0
                    else
                        let mutable spectrumPeaks = dataReader.ReadSpectrumPeaks(swathSpec.Value.SpectrumID)
                        let mutable ms2Peaks = Array.create query.CountMS2Masses (new Peak2D())
                        query.Ms2Masses
                        |> Array.map (fun mzRange ->
                            let mutable mzPeaks = BinarySearch.Search(spectrumPeaks.Peaks, mzRange, SwathIndexer.MzRangeCompare)
                            let mutable (p:Peak1D) = mzRangeSelector (mzPeaks, mzRange)
                            new Peak2D(p.Intensity, p.Mz, swathSpec.Value.Rt)
                        )

        /// <summary>
        /// The default mz range peak selector function.
        /// </summary>
        static member internal GetClosestMz(peaks:IEnumerable<Peak1D>, mzRange:RangeQuery) =

            peaks.DefaultIfEmpty(new Peak1D(0., mzRange.LockValue)).ItemAtMin(fun x -> Math.Abs(x.Mz - mzRange.LockValue))

        static member internal MzRangeCompare(p:Peak1D, mzRange:RangeQuery) =
            if p.Mz < mzRange.LowValue then -1
            else
                if p.Mz > mzRange.HighValue then 1
                else 0

    //        //public Peak2D[,] GetRTProfiles(
    //        //    IMzIODataReader dataReader,
    //        //    SwathQuery query,
    //        //    bool getLockMz,
    //        //    Func<Peak1DArray, RangeQuery, Peak1D> mzRangeSelector)
    //        //{
    //        //    var swathSpectra = swathList.SearchAllTargetMz(query)
    //        //        .SelectMany(x => x.SearchAllRt(query))
    //        //        .ToArray();

    //        //    if (swathSpectra.Length > 0)
    //        //    {
    //        //        Peak2D[,] profile = new Peak2D[query.CountMS2Masses, swathSpectra.Length];

    //        //        for (int specIdx = 0; specIdx < swathSpectra.Length; specIdx++)
    //        //        {
    //        //            var swathSpec = swathSpectra[specIdx];
    //        //            var pa = dataReader.ReadSpectrumPeaks(swathSpec.SpectrumID);

    //        //            for (int ms2MassIndex = 0; ms2MassIndex < query.CountMS2Masses; ms2MassIndex++)
    //        //            {
    //        //                RangeQuery mzRange = query[ms2MassIndex];
    //        //                Peak1D p = mzRangeSelector(pa, mzRange);

    //        //                if (getLockMz)
    //        //                {
    //        //                    profile[ms2MassIndex, specIdx] = new Peak2D(p.Intensity, mzRange.LockValue, swathSpec.Rt);
    //        //                }
    //        //                else
    //        //                {
    //        //                    profile[ms2MassIndex, specIdx] = new Peak2D(p.Intensity, p.Mz, swathSpec.Rt);
    //        //                }
    //        //            }
    //        //        }

    //        //        return profile;
    //        //    }
    //        //    else
    //        //    {
    //        //        return empty2D;
    //        //    }
    //        //}