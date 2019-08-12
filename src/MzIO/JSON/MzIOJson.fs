namespace MzIO.Json


open System
open System.IO
open System.Globalization
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Serialization
open MzIO.IO
open MzIO.Model
open MzIO.Model.CvParam


type MzIOJson =

    static member jsonSettings = 
        let tmp = new JsonSerializerSettings()
        //new method to preserve paramcontainer fields when serealizing type
        tmp.ReferenceLoopHandling       <- Newtonsoft.Json.ReferenceLoopHandling.Serialize
        tmp.PreserveReferencesHandling  <- Newtonsoft.Json.PreserveReferencesHandling.Objects
        //end of new method
        //tmp.ReferenceLoopHandling       <- Newtonsoft.Json.ReferenceLoopHandling.Ignore
        tmp.ContractResolver            <- new DefaultContractResolver()
        tmp.Culture <- new CultureInfo("en-US")    
        tmp


    static member SaveJsonFile(obj:Object, path:string) =
        if File.Exists(path) then
            File.Delete(path)

        use writer      = File.CreateText(path)
        use jsonWriter  = new JsonTextWriter(writer)
        (
            let serializer = JsonSerializer.Create(MzIOJson.jsonSettings)
            serializer.Formatting <- Formatting.Indented
            serializer.Serialize(jsonWriter, obj)
        )

    static member ReadJsonFile<'T>(path:string) =
        if File.Exists(path) = false then 
            raise (new FileNotFoundException(path))

        use reader = File.OpenText(path)
        use jsonReader = new JsonTextReader(reader)
        (
            let serializer = JsonSerializer.Create(MzIOJson.jsonSettings)
            let tmp = serializer.Deserialize<'T>(jsonReader)
            tmp
        )


    static member HandleExternalModelFile(io:IMzIOIO, path:string) =
        
        //to be safe it is false
        let mutable throwExceptionIfFileCouldNotRead = false

        if String.IsNullOrWhiteSpace(path) then 
            raise (ArgumentNullException("path"))
        else
            ()

        if not (File.Exists(path)) then
            let model = io.CreateDefaultModel()
            MzIOJson.SaveJsonFile(model, path)
            model
        
        else
            try
                MzIOJson.ReadJsonFile<MzIOModel>(path)
                |> fun item -> MzIOJson.deSerializeMzIOModel(MzIOJson.ToJson(item))
            with
                | :? Exception as ex
                    -> 
                        match throwExceptionIfFileCouldNotRead with
                        | true   -> raise ex
                        | false  ->
                            let mutable causalException = ex.InnerException
                            //let mutable backFile = String.Format("{0}.back", path)
                            let backFile = path + ".back"

                            if File.Exists(backFile) then
                                File.Delete(backFile)
                            File.Move(path, backFile)

                            //model <- io.CreateDefaultModel()
                            let model = io.CreateDefaultModel()
                            MzIOJson.SaveJsonFile(model, path)

                            let mutable msg = System.Text.StringBuilder()
                            //msg.AppendFormat
                            //    ("Could not read mz lite model file: '{0}'. ", 
                            //        path
                            //    ) |> ignore
                            //msg.AppendFormat
                            //    ("Causal exception is: '{0}' with message '{1}'. ", 
                            //        causalException.GetType().FullName, causalException.Message
                            //    ) |> ignore
                            //msg.AppendFormat
                            //    ("A new initial model file was created, the old file 
                            //        was renamed to: '{0}'.", backFile
                            //    ) |> ignore
                            Console.Error.WriteLine(msg.ToString())
                            model

    static member FromJson<'T>(json: string) =
        if String.IsNullOrWhiteSpace(json) then 
            raise (ArgumentNullException("json"))
        else
            JsonConvert.DeserializeObject<'T>(json)
            //let tmp = JsonConvert.DeserializeObject<'T>(json)
            //match tmp :> Object with
            //| :? DynamicObj as item -> MzIOJson.deserializeJObject(item, tmp :> Object)
            //| _                     -> ()
            //tmp

    static member deserializeJObject(baseObj:DynamicObj, jsonObj:Object) =
        match jsonObj with
        | :? DynamicObj as value ->
            value.GetProperties true
            |> Seq.iter (fun item -> MzIOJson.deserializeJObject(value, item.Value))
        | _ -> 
            if (jsonObj :? JObject) = true then
                if
                    (jsonObj :?> JObject).["CvAccession"] <> null && 
                    (jsonObj :?> JObject).["Type"] <> null then
                        let tmp = JsonConvert.DeserializeObject<CvParam<IConvertible>>(jsonObj.ToString())
                        baseObj.SetValue(tmp.CvAccession, tmp)
                else
                    if
                        (jsonObj :?> JObject).["Name"] <> null && 
                        (jsonObj :?> JObject).["Type"] <> null then
                            let tmp = JsonConvert.DeserializeObject<UserParam<IConvertible>>(jsonObj.ToString())
                            baseObj.SetValue(tmp.Name, tmp)
                    else 
                        let jString = jsonObj.ToString()
                        match baseObj.ToString() with
                        | "MzIO.Model.FileDescription" ->
                            let tmp = JsonConvert.DeserializeObject<FileDescription>(jString)
                            baseObj.SetValue(tmp.ToString(), tmp)
                        | "MzIO.Model.ScanList" -> 
                            let tmp = JsonConvert.DeserializeObject<Scan>(jString)
                            baseObj.SetValue(tmp.ToString(), tmp)
                            MzIOJson.deserializeJObject(tmp, tmp)
                        | "MzIO.Model.ProductList" -> 
                            let tmp = JsonConvert.DeserializeObject<Product>(jString)
                            baseObj.SetValue(tmp.ToString(), tmp)
                            MzIOJson.deserializeJObject(tmp, tmp)
                        | "MzIO.Model.PrecursorList" -> 
                            let tmp = JsonConvert.DeserializeObject<Precursor>(jString)
                            baseObj.SetValue(tmp.ToString(), tmp)
                            MzIOJson.deserializeJObject(tmp, tmp)
                        | "MzIO.Model.RunList" -> 
                            let tmp = JsonConvert.DeserializeObject<Run>(jString)
                            baseObj.SetValue(tmp.ToString(), tmp)
                            MzIOJson.deserializeJObject(tmp, tmp)
                        | _ -> ()
            else ()

    static member ToJson(obj:Object) =

        JsonConvert.SerializeObject(obj, MzIOJson.jsonSettings)

    static member private deSerializeFileDescription(fileDescription:FileDescription) =
        JsonConvert.DeserializeObject<FileDescription>(fileDescription.ToString())

    static member private deSerializeSample(sample:string) =    
        let sample = JsonConvert.DeserializeObject<Sample>(sample)
        let treatments   = 
            JsonConvert.DeserializeObject<SampleTreatmentList>(MzIOJson.ToJson(sample.Treatments))
        let preperations = 
            JsonConvert.DeserializeObject<SamplePreparationList>(MzIOJson.ToJson(sample.Preparations))
        new Sample(sample.ID, sample.Name, treatments, preperations)

    static member private deSerializeSamples(samples:SampleList) =
        samples.GetProperties false
        |> Seq.iter (fun sample -> samples.SetValue(sample.Key, JsonConvert.DeserializeObject<Sample>(sample.Value.ToString())))    
        samples

    static member private deSerializeSoftwares(softwares:SoftwareList) =
        softwares.GetProperties false
        |> Seq.iter (fun software -> softwares.SetValue(software.Key, JsonConvert.DeserializeObject<Software>(software.Value.ToString())))    
        softwares
    
    static member private deSerializeDataProcessings(dataProcessings:DataProcessingList) =
        dataProcessings.GetProperties false
        |> Seq.iter (fun dataProcessing -> dataProcessings.SetValue(dataProcessing.Key, JsonConvert.DeserializeObject<DataProcessing>(dataProcessing.Value.ToString())))    
        dataProcessings

    static member private deSerializeInstruments(instruments:InstrumentList) =
        instruments.GetProperties false
        |> Seq.iter (fun instrument -> instruments.SetValue(instrument.Key, JsonConvert.DeserializeObject<Instrument>(instrument.Value.ToString())))    
        instruments

    static member private deSerializeRuns(runs:RunList) =
        runs.GetProperties false
        |> Seq.iter (fun run -> runs.SetValue(run.Key, JsonConvert.DeserializeObject<Run>(run.Value.ToString())))    
        runs

    static member deSerializeMzIOModel(model:string) =

        let mzIOModel = JsonConvert.DeserializeObject<MzIOModel>(model)

        //let fileDescription = 
        //    (*deSerializeFileDescription*) mzIOModel.FileDescription
        //mzIOModel.FileDescription <- fileDescription

        let samples = 
            MzIOJson.deSerializeSamples mzIOModel.Samples
        mzIOModel.Samples <- samples

        let softwares =
            MzIOJson.deSerializeSoftwares mzIOModel.Softwares
        mzIOModel.Softwares <- softwares

        let dataProcessings = 
            MzIOJson.deSerializeDataProcessings mzIOModel.DataProcessings
        mzIOModel.DataProcessings <- dataProcessings

        let instruments = 
            MzIOJson.deSerializeInstruments mzIOModel.Instruments
        mzIOModel.Instruments <- instruments

        let runs = 
            MzIOJson.deSerializeRuns mzIOModel.Runs
        mzIOModel.Runs <- runs
        mzIOModel

    
    static member deSerializeParams(item:#DynamicObj) =
        item.GetProperties false
        |> Seq.iter (fun param -> 
            let tmp = param.Value :?> JObject
            match tmp.["Name"] with
            | null  -> item.SetValue(param.Key, JsonConvert.DeserializeObject<CvParam<IConvertible>>(tmp.ToString()))
            | _     -> item.SetValue(param.Key, JsonConvert.DeserializeObject<UserParam<IConvertible>>(tmp.ToString()))
                    )

    static member deSerializeProducts(products:ProductList) =
        products.GetProperties false
        |> Seq.iter (fun item -> 
            let tmp = item.Value :?> JObject
            let product = JsonConvert.DeserializeObject<Product>(tmp.ToString())
            MzIOJson.deSerializeParams(product)
            products.SetValue(item.Key, product)
                    )
        products

    static member deSerializeScans(scans:ScanList) =
        scans.GetProperties false
        |> Seq.iter (fun item -> 
            let tmp = item.Value :?> JObject
            let scan = JsonConvert.DeserializeObject<Scan>(tmp.ToString())
            MzIOJson.deSerializeParams(scan)
            scans.SetValue(item.Key, scan)
                    )
        scans

    static member deSerializePrecursors(precursors:PrecursorList) =
        precursors.GetProperties false
        |> Seq.iter (fun item -> 
            let tmp = item.Value :?> JObject
            let precursor = JsonConvert.DeserializeObject<Precursor>(tmp.ToString())
            MzIOJson.deSerializeParams(precursor)
            precursors.SetValue(item.Key, precursor)
                    )
        precursors

    static member deSerializeMassSpectrum (jsonString:string) =
        let jsonObj = JsonConvert.DeserializeObject<JObject>(jsonString)
        let precursors =
            match jsonObj.["Precursors"] with
            | null  -> failwith "Nope"
            | _     ->
                let tmp = jsonObj.["Precursors"] :?> JObject
                MzIOJson.deSerializePrecursors(JsonConvert.DeserializeObject<PrecursorList>(tmp.ToString()))
        let scans =
            match jsonObj.["Scans"] with
            | null  -> failwith "Nope"
            | _     ->
                let tmp = jsonObj.["Scans"] :?> JObject
                MzIOJson.deSerializeScans(JsonConvert.DeserializeObject<ScanList>(tmp.ToString()))
        let products =
            match jsonObj.["Products"] with
            | null  -> failwith "Nope"
            | _     ->
                let tmp = jsonObj.["Products"] :?> JObject
                MzIOJson.deSerializeProducts(JsonConvert.DeserializeObject<ProductList>(tmp.ToString()))
        let spectrum = JsonConvert.DeserializeObject<MassSpectrum>(jsonObj.ToString())
        new MassSpectrum(spectrum.ID, spectrum.DataProcessingReference, precursors, scans, products, spectrum.SourceFileReference)

