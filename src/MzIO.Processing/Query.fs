namespace MzIO.Processing

module Query = 

    open MzIO.Processing
    open MzIO.IO
    open MzIO
    open MzIO.Processing.MzIOLinq
    
    /// Creates range query with equivalent distances to high and low value.
    let createRangeQuery v offset =
        new RangeQuery(v, offset)
    
    /// Creates list of retention time index entires.
    let getMS1RTIdx (reader:IMzIODataReader) runId = 
        reader.BuildRtIndex(runId)

    /// Calculate XIC of retention time index entries and m/z range.
    let getXIC (reader:IMzIODataReader) (rtIdx:Commons.Arrays.IMzIOArray<MzIOLinq.RtIndexEntry>) (rtQuery:RangeQuery) (mzQuery:RangeQuery) = 
        reader.RtProfile(rtIdx, rtQuery, mzQuery) 

    /// Calculate XICs of retention time index entries and m/z ranges.
    let getXICs (reader:IMzIODataReader) (rtIdx:Commons.Arrays.IMzIOArray<MzIOLinq.RtIndexEntry>) (rtQuery:RangeQuery) (mzQueries:RangeQuery []) = 
        reader.RtProfiles(rtIdx, rtQuery, mzQueries) 
       
    /// Creates a swath query.
    let createSwathQuery targetMz rtQuery ms2MzQueries =
        new SwathQuery(targetMz, rtQuery, ms2MzQueries)

    /// Creates a swath indexer.
    let getSwathIdx (reader:IMzIODataReader) runId =
        SwathIndexer.SwathIndexer.Create(reader, runId)

    /// Calcualtes  an array of Peak2D.
    let getSwathXics (reader:IMzIODataReader) (swathIdx:SwathIndexer.SwathIndexer) swathQuery = 
        swathIdx.GetMS2(reader, swathQuery)

    /// Calcualtes  an array of Peak2D for specific m/z range.
    let getSwathXICsBy (reader:IMzIODataReader) (swathIdx:SwathIndexer.SwathIndexer) (rtQuery:RangeQuery) (ms2MzQueries:RangeQuery []) tarMz = 
        let swathQ = createSwathQuery tarMz rtQuery ms2MzQueries
        getSwathXics reader swathIdx swathQ


