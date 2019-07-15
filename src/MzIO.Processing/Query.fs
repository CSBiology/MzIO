namespace MzIO.Processing

module Query = 

    open MzIO.Processing
    open MzIO.IO
    open MzIO
    open MzIO.Processing.MzIOLinq
    
    ///
    let createRangeQuery v offset =
        new RangeQuery(v, offset)
    
    ///
    let getMS1RTIdx (reader:IMzIODataReader) runId = 
        reader.BuildRtIndex(runId)

    /// 
    let getXIC (reader:IMzIODataReader) (rtIdx:Commons.Arrays.IMzIOArray<MzIOLinq.RtIndexEntry>) (rtQuery:RangeQuery) (mzQuery:RangeQuery) = 
        reader.RtProfile(rtIdx, rtQuery, mzQuery) 

    ///
    let getXICs (reader:IMzIODataReader) (rtIdx:Commons.Arrays.IMzIOArray<MzIOLinq.RtIndexEntry>) (rtQuery:RangeQuery) (mzQueries:RangeQuery []) = 
        reader.RtProfiles(rtIdx, rtQuery, mzQueries) 
       
    ///       
    let createSwathQuery targetMz rtQuery ms2MzQueries =
        new SwathQuery(targetMz, rtQuery, ms2MzQueries)

    ///
    let getSwathIdx (reader:IMzIODataReader) runId =
        SwathIndexer.SwathIndexer.Create(reader, runId)

    ///
    let getSwathXics (reader:IMzIODataReader) (swathIdx:SwathIndexer.SwathIndexer) swathQuery = 
        swathIdx.GetMS2(reader, swathQuery)

    ///        
    let getSwathXICsBy (reader:IMzIODataReader) (swathIdx:SwathIndexer.SwathIndexer) (rtQuery:RangeQuery) (ms2MzQueries:RangeQuery []) tarMz = 
        let swathQ = createSwathQuery tarMz rtQuery ms2MzQueries
        getSwathXics reader swathIdx swathQ


