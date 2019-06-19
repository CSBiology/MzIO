
#r @"../MzIO.SQL\bin\Release\net45/System.Data.SQLite.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Muni.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Data.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Data.CommonInterfaces.dll"
#r @"../MzIO.Wiff\bin\Release\net45\Clearcore2.Data.AnalystDataProvider.dll"
#r @"../MzIO.Wiff\bin\Release\net45/Newtonsoft.Json.dll"
#r @"../MzIO.Wiff\bin\Release\net45/MzIO.dll"
#r @"../MzIO.Wiff\bin\Release\net45\MzIO.Wiff.dll"
#r @"../MzIO.SQL\bin\Release\net45\MzIO.SQL.dll"
#r @"../MzIO.Processing\bin\Release\net45\MzIO.Processing.dll"


open System
open System.Collections.Generic
open MzIO.Binary
open MzIO.Wiff
open MzIO.SQLReader
open MzIO.MetaData.ParamEditExtension
open MzIO.MetaData.PSIMSExtension
open MzIO.Model
open MzIO.Model.CvParam
open MzIO.MetaData.UO.UO
open MzIO.Processing.MzIOLinq
open MzIO.Json
open Newtonsoft.Json
open Newtonsoft.Json.Linq


//let test = Software()
//let tests = SoftwareList()
//let paramValue = ParamValue.CvValue("value")
//let paramUnit = ParamValue.WithCvUnitAccession("value", "unit")

//test
//tests.Add(test)
//tests.Item("id")

//test.AddCvParam(CvParam.CvParam<string>("test", paramUnit))
//test.AddCvParam(CvParam.CvParam<string>("value", paramValue))
//test.AddCvParam(CvParam.UserParam<string>("testI", paramValue))


let fileDir             = __SOURCE_DIRECTORY__
let licensePath         = sprintf @"%s" (fileDir + "\License\Clearcore2.license.xml")

let wiffTestFileStudent = @"C:\Users\Student\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\wiffTestFiles\20171129 FW LWagg001.wiff"
let mzLiteFileStudent   = @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff.mzlite"

let jonMzLite           = @"C:\Users\jonat\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\test180807_Cold1_2d_GC8_01_8599.mzlite"
let jonWiff             = @"C:\Users\jonat\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\20180301_MS_JT88mutID122.wiff"

let wiffTestPaeddetor   = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff"
let paddeTestPath       = @"D:\Users\Patrick\Desktop\BioInformatik\MzLiteTestFiles\WiffTestFiles\20180301_MS_JT88mutID122.wiff.mzlite"

let wiffTestUni         = @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff"
let uniTestPath         = @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff.mzlite"


let mzLiteFSharpDBPath  = @"C:\Users\Student\source\repos\wiffTestFiles\Databases\MzLiteFSHarpLWagg001.mzlite"


//let wiffFileReader = new WiffFileReader(wiffTestUni, licensePath)
//let massSpectra = 
//    wiffFileReader.Model.Runs.GetProperties false
//    |> Seq.map (fun run -> wiffFileReader.ReadMassSpectra run.Key)
//    |> Seq.head
//    |> Array.ofSeq

//let massSpectrum = massSpectra.[0]

//let peaks = wiffFileReader.ReadSpectrumPeaks massSpectrum.ID

//massSpectrum.GetProperties false
//let peak = (Array.ofSeq peaks.Peaks).[0]
//peak

//let model = wiffFileReader.CreateDefaultModel()

//model.Runs.GetProperties false

//let testii = (new MzLiteSQL(uniTestPath))
//let testiii = testii.MzLiteSQL()

//let testVI = testii.BeginTransaction()
//testii.Insert("meh", massSpectra.[2], peaks)
//testii.Insert("RunTest", massSpectra.[3], peaks)
//let testIIII = testii.ReadMassSpectra("meh") |> Array.ofSeq
//let testV = testii.ReadMassSpectra("RunTest") |> Array.ofSeq
//testVI.Commit()
//testVI.Dispose()
//testIIII.[0]
//testV.[0]

type MzLiteHelper =
    {
        RunID           : string
        MassSpectrum    : seq<MzIO.Model.MassSpectrum>
        Peaks           : seq<Peak1DArray>
        Path            : string
    }

let createMzLiteHelper (runID:string) (path:string) (spectrum:seq<MzIO.Model.MassSpectrum>) (peaks:seq<Peak1DArray>) =
    {
        MzLiteHelper.RunID          = runID
        MzLiteHelper.MassSpectrum   = spectrum
        MzLiteHelper.Peaks          = peaks
        MzLiteHelper.Path           = path
    }

let wiffFilePaths =
    [
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg001.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg002.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg003.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg004.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg005.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg006.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg007.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg008.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg009.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg010.wiff"
        @"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg011.wiff"
        //@"C:\Users\Student\source\repos\wiffTestFiles\20171129 FW LWagg012.wiff"
    ]

let getWiffFileReader (path:string) =
    new WiffFileReader(path, licensePath)

let getMassSpectra (wiffFileReader:WiffFileReader) =
    wiffFileReader.Model.Runs.GetProperties false
    |> Seq.collect (fun (run:KeyValuePair<string, obj>) -> wiffFileReader.ReadMassSpectra run.Key)

let getPeak1DArrays (wiffFileReader:WiffFileReader) =
    getMassSpectra wiffFileReader
    |> Seq.map (fun spectrum -> wiffFileReader.ReadSpectrumPeaks spectrum.ID)

let getMzLiteHelper (path:string) (compressionType:BinaryDataCompressionType) =
    let wiffFileReader = new WiffFileReader(path, licensePath)
    let runIDMassSpectra =
        wiffFileReader.Model.Runs.GetProperties false
        |> Seq.map (fun (run:KeyValuePair<string, obj>) -> run.Key, wiffFileReader.ReadMassSpectra run.Key)
    let tmp =
        runIDMassSpectra
        |> Seq.map (fun (runID, massSpectra) ->
            massSpectra
            |> Seq.map (fun spectrum -> (wiffFileReader.ReadSpectrumPeaks spectrum.ID))
            |> Seq.map (fun peak -> peak.CompressionType <- compressionType
                                    peak)
            |> createMzLiteHelper runID path massSpectra
            )
        |> Seq.head
    tmp
    
let insertWholeFileIntoDB (helper:MzLiteHelper) =
    let mzLiteSQL = new MzLiteSQL(helper.Path + ".mzlite")
    let bn = mzLiteSQL.BeginTransaction()
    Seq.map2 (fun (spectrum:MzIO.Model.MassSpectrum) (peak:Peak1DArray) -> mzLiteSQL.Insert(helper.RunID, spectrum, peak)) helper.MassSpectrum helper.Peaks
    |> Seq.length |> ignore
    bn.Commit()
    bn.Dispose()
  
let insertIntoDB (amount:int) (helper:MzLiteHelper) =
    let mzLiteSQL = new MzLiteSQL(helper.Path + ".mzlite")
    let bn = mzLiteSQL.BeginTransaction()
    Seq.map2 (fun (spectrum:MzIO.Model.MassSpectrum) (peak:Peak1DArray) -> mzLiteSQL.Insert(helper.RunID, spectrum, peak)) (Seq.take amount helper.MassSpectrum) (Seq.take amount helper.Peaks)
    |> Seq.length |> ignore
    bn.Commit()
    bn.Dispose()

let getSpectrum (path:string) (spectrumID:string) =
    let mzLiteSQL = new MzLiteSQL(path)
    let bn = mzLiteSQL.BeginTransaction()
    mzLiteSQL.ReadMassSpectrum spectrumID

let getSpectra (path:string) (helper:MzLiteHelper) =
    let mzLiteSQL = new MzLiteSQL(path)
    let bn = mzLiteSQL.BeginTransaction()
    let tmp = 
        mzLiteSQL.ReadMassSpectra helper.RunID
        |> List.ofSeq
    bn.Commit()
    bn.Dispose()
    tmp

let getSpectrumPeaks (path:string) (spectrumID:string) =

    let mzLiteSQL = new MzLiteSQL(path)

    let bn = mzLiteSQL.BeginTransaction()

    mzLiteSQL.ReadSpectrumPeaks spectrumID

#time
let wiffFileReader =
    getWiffFileReader wiffTestUni

let massSpectra =
    getMassSpectra wiffFileReader

let helper = getMzLiteHelper wiffTestUni BinaryDataCompressionType.NoCompression

//let peak1DArrays = getPeak1DArrays wiffFileReader

//let insertDB =
//    getMzLiteHelper wiffTestUni BinaryDataCompressionType.NoCompression
//    |> (fun wiffFileReader -> insertIntoDB 100 wiffFileReader)

//let spectrum =
//    getSpectrum uniTestPath "sample=0 experiment=0 scan=0"

//let spectra =
//    getMzLiteHelper wiffTestUni BinaryDataCompressionType.ZLib
//    |> getSpectra uniTestPath 

//let peaks =
//    //spectra
//    //|> Seq.map (fun item -> getSpectrumPeaks uniTestPath item.ID)
//    getSpectrumPeaks uniTestPath "sample=0 experiment=0 scan=0"

//Seq.length peaks


//for i in peaks do
//    for peak in i.Peaks do
//        printfn "%f %f" peak.Mz peak.Intensity

//let tmp =
//    helper.MassSpectrum |> List.ofSeq
//    |> List.head


//let mutable idx = 0
//tmp.TryGetMsLevel(& idx)
//idx
//tmp.TryGetValue(PSIMS_Spectrum.MsLevel).Value


//let y = new CvParam<int>("test", ParamValue.WithCvUnitAccession(5, "SOME TEST"))
//let yIII = new CvParam<int>("testI")
//let text = MzIOJson.ToJson(y)
//MzIOJson.ToJson(yIII)
//MzIOJson.FromJson(text) :> CvParam<IConvertible>
////MzLiteJson.ToJson(yIII)

//MzIOJson.ToJson(ParamValue.WithCvUnitAccession(5, "SOME TEST").ToString())
//JsonConvert.SerializeObject(ParamValue.WithCvUnitAccession(5, "SOME TEST").ToString())

//"WithCvUnitAccession (5,\"SOME TEST\")"

//let cvTest = sprintf "{\"%s\":\"%s\",%s}" "$id" "1" "\"Type\":\"WithCvUnitAccession\",\"Values\":[5,\"SOME TEST\"]"
//let jsonX = JsonConvert.DeserializeObject<JObject>(cvTest)
//jsonX.ToString()
//jsonX.["Type"]
//jsonX.["Values"] :?> JArray



//"WithCvUnitAccession".Length
//"CvValue".Length
//let tmp = new MassSpectrum()

//tmp.AddCvParam(y)
//tmp.AddCvParam(yIII)
//tmp.AddCvParam(new CvParam<string>("testII", ParamValue.WithCvUnitAccession(null, "SOME TEST")))
//tmp.AddCvParam(new CvParam<string>("testIII", ParamValue.CvValue("SOME VAlue")))
//tmp.TryGetValue("test")
//tmp.TryGetValue("testI")
//tmp.TryGetValue("testII")
//tmp.TryGetValue("testIII")

//let tmpIII = MzIOJson.FromJson(MzIOJson.ToJson(tmp)) :> MassSpectrum

//for i in tmpIII.GetDynamicMemberNames() do
//    printfn "%s" i

//tmpIII.TryGetValue("test")
//tmpIII.TryGetValue("testI")
//tmpIII.TryGetValue("testII")
//tmpIII.TryGetValue("testIII")

////let text2 = MzLiteJson.ToJson(tmp)
////let tmp2 = MzLiteJson.FromJson(text2) :> DynamicObj

////MzLiteJson.ToJson(y)
////|> (fun item -> MzLiteJson.FromJson(item)) :> CvParam<int>

////let x = ((Seq.head (tmp2.GetProperties false)).Value.ToString())
////MzLiteJson.FromJson(x) :> CvParam<int64>

////let jObj = tmp2.TryGetValue("test").Value :?> JObject

////MzLiteJson.FromJson(jObj.ToString()) :> CvParam<int>
////jObj.First.ToString()
//////MzLiteJson.FromJson(jObj.First.Next.ToString())

////jObj.ToString()

////let xI = jObj.["Value"] :?> JObject

////xI.["Case"].ToString()
////let yI = xI.["Fields"]

////y.ToString()

////xI.Count
////xI.First.ToString()
////yI.First.["Case"].ToString()
////yI.First.["Fields"].First.ToString()
////yI.First.["Fields"].Last.ToString()
////xI.Last.ToString()

////deSerializeInPlaceParamValues tmp2

////tmp2.TryGetValue("test")
////tmp2.TryGetValue("testI")
////tmp2.TryGetValue("testII")
////tmp2.TryGetValue("testIII")


////tmp.AddCvParam y
////tmp.SetCvParam("test").UO_Liter()
////let zy = tryGetCvUnitAccession (tmp.TryGetTypedValue<CvParam<IConvertible>>("test").Value)
////zy


////JObject.Parse(text).First.Next.ToString()
////JObject.Parse(text).First.Next.Next.ToString()


//////let dbPath = @"C:\Users\Student\OneDrive\MP_Biotech\VP_Timo\MassSpecFiles\wiffTestFiles\20171129 FW LWagg001_testMzIO.mzlite"

let mzsqlreader = new MzLiteSQL(uniTestPath)

let tr = mzsqlreader.BeginTransaction()

let tmpX = mzsqlreader.ReadMassSpectra("sample=0") |> List.ofSeq |> List.head

let mutable idx1 = 0
tmpX.TryGetMsLevel(& idx1)
idx1

tmpX.TryGetValue(PSIMS_Spectrum.MsLevel)

tmpX.TryGetTypedValue<CvParam<IConvertible>>(PSIMS_Spectrum.MsLevel)

tmpX.Scans.GetProperties true

let rtIndexEntry = mzsqlreader.BuildRtIndex("sample=0")

let rtProfile = mzsqlreader.RtProfile (rtIndexEntry, (new MzIO.Processing.RangeQuery(1., 300., 600.)), (new MzIO.Processing.RangeQuery(1., 300., 600.)))

//let tmpXY = (new Scan())
//tmpXY.AddCvParam(new CvParam<string>("Test)"))

//let scan = Seq.head (tmpX.Scans.GetProperties false)
//MzIOJson.FromJson(scan.Value.ToString()) :> Scan


////let test = new MassSpectrum()
////let scanList = (new ScanList())
////scanList.Add(new Scan())
////test.Scans.Add(new Scan())
////test.Scans.Count
////let testJson = MzLiteJson.ToJson(test)
////let testUnJson = MzLiteJson.FromJson(testJson) :> MassSpectrum
//////MzLiteJson.ToJson(testUnJson)
////testUnJson.GetProperties false
////testUnJson.Scans.Count
//////let testJsonTextArray = (Seq.head (testUnJson.GetProperties false)).ToString()
//////JArray.Parse(testJsonTextArray)
 

//////let xI =
//////    JsonConvert.SerializeObject(test, MzLiteJson.jsonSettings)
//////    |> (fun item -> JsonConvert.DeserializeObject<MassSpectrum>(item))

//////xI.Scans.Count
