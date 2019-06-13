namespace MzLiteFSharp.Json


open System
open System.IO
open System.Globalization
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open MzLiteFSharp.IO
open MzLiteFSharp.Model


type MzLiteJson =

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
            let serializer = JsonSerializer.Create(MzLiteJson.jsonSettings)
            serializer.Formatting <- Formatting.Indented
            serializer.Serialize(jsonWriter, obj)
        )

    static member ReadJsonFile<'T>(path:string) =
        if File.Exists(path) = false then 
            failwith ((new FileNotFoundException(path)).ToString())

        use reader = File.OpenText(path)
        use jsonReader = new JsonTextReader(reader)
        (
            let serializer = JsonSerializer.Create(MzLiteJson.jsonSettings)
            let tmp = serializer.Deserialize<'T>(jsonReader)
            tmp
        )


    static member HandleExternalModelFile(io:IMzLiteIO, path:string) =
        
        //to be safe it is false
        let mutable throwExceptionIfFileCouldNotRead = false

        match path with
        | null  -> failwith (ArgumentNullException("path").ToString())
        | ""    -> failwith (ArgumentNullException("path").ToString())
        | " "   -> failwith (ArgumentNullException("path").ToString())
        |   _   -> ()

        if not (File.Exists(path)) then
            let model = io.CreateDefaultModel()
            MzLiteJson.SaveJsonFile(model, path)
            model
        
        else
            let ex = new Exception()
            try
                MzLiteJson.ReadJsonFile<MzLiteModel>(path)
            with
                | :? Exception
                    -> 
                        match throwExceptionIfFileCouldNotRead with
                        | true   -> failwith (ex.ToString())
                        | false  ->
                            let mutable causalException = ex.InnerException
                            //let mutable backFile = String.Format("{0}.back", path)
                            let backFile = path + ".back"

                            if File.Exists(backFile) then
                                File.Delete(backFile)
                            File.Move(path, backFile)

                            //model <- io.CreateDefaultModel()
                            let model = io.CreateDefaultModel()
                            MzLiteJson.SaveJsonFile(model, path)

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
        match json with
        | null  -> failwith (ArgumentNullException("sqlFilePath").ToString())      
        | ""    -> failwith (ArgumentNullException("sqlFilePath").ToString())      
        | " "   -> failwith (ArgumentNullException("sqlFilePath").ToString())      
        |   _   -> JsonConvert.DeserializeObject<'T>(json)

    static member ToJson(obj:Object) =

        JsonConvert.SerializeObject(obj, MzLiteJson.jsonSettings)        
