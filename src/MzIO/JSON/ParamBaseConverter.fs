namespace MzIO.Json


open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open MzIO.Model.CvParam


type ParamBaseConverter =

    inherit JsonConverter

    ///This function takes an object type and returns a boolean. The boolean indicates whether this instance can convert the specified object type.
    override this.CanConvert(objectType:Type) = 
        failwith ((new NotSupportedException("JsonConverter.CanConvert()")).ToString())

    ///This function takes a JSON reader, an object type, the JSON representation of an object and a JSON serializer. It returns an object, which is read from the JSON representation.
    override this.ReadJson(reader:JsonReader, objectType:Type, existingValue:Object, serializer:JsonSerializer) =

        let jt = JToken.Load(reader)

        if jt.Type = JTokenType.Null then null
            else
                if jt.Type = JTokenType.Object then
                    let jo = JObject(jt)
                    let mutable jtval = JToken.FromObject(jo)            
                    if jo.TryGetValue("Name", & jtval) then
                        jo.ToObject<UserParam<string>>(serializer) :> Object
                    else 
                        if jo.TryGetValue("CvAccession", & jtval) then
                            jo.ToObject<CvParam<string>>(serializer) :> Object
                        else 
                            failwith ((new JsonSerializationException("Could not determine concrete param type.")).ToString())
                else 
                    failwith ((new JsonSerializationException("Object token expected.")).ToString())

    ///This function takes a JSOn writer, an object and a JSON serializer. It returns the JSON representation of the object.
    override this.WriteJson(writer: JsonWriter, value: Object, serializer: JsonSerializer) =

        if value = null then
            writer.WriteNull()
        else
            match value with
            | :? CvParam<'T>    as value -> serializer.Serialize(writer, value)
            | :? UserParam<'T>  as value -> serializer.Serialize(writer, value)
            | _     -> failwith ((new JsonSerializationException("Type not supported: " + value.GetType().FullName)).ToString())
