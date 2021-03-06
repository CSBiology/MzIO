﻿namespace MzIO.Json


open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq


type ConvertibleConverter() =

    inherit JsonConverter()

    override this.CanConvert(objectType:Type) = 
        raise (new NotSupportedException("JsonConverter.CanConvert()"))

    member this.ReadValue(reader:JsonReader, tc:TypeCode, jtval:JToken, serializer:JsonSerializer) =
        
        match tc with
        | TypeCode.Boolean  -> jtval.ToObject<bool>(serializer)     :> IConvertible
        | TypeCode.Byte     -> jtval.ToObject<byte>(serializer)     :> IConvertible
        | TypeCode.Char     -> jtval.ToObject<char>(serializer)     :> IConvertible
        | TypeCode.DateTime -> jtval.ToObject<DateTime>(serializer) :> IConvertible
        | TypeCode.DBNull   -> null
        | TypeCode.Decimal  -> jtval.ToObject<decimal>(serializer)  :> IConvertible
        | TypeCode.Double   -> jtval.ToObject<double>(serializer)   :> IConvertible
        | TypeCode.Empty    -> null
        | TypeCode.Int16    -> jtval.ToObject<Int16>(serializer)    :> IConvertible
        | TypeCode.Int32    -> jtval.ToObject<Int32>(serializer)    :> IConvertible
        | TypeCode.Int64    -> jtval.ToObject<Int64>(serializer)    :> IConvertible
        | TypeCode.Object   -> raise (new JsonSerializationException("Object type not supported."))
        | TypeCode.SByte    -> jtval.ToObject<sbyte>(serializer)    :> IConvertible
        | TypeCode.Single   -> jtval.ToObject<float>(serializer)    :> IConvertible
        | TypeCode.String   -> jtval.ToObject<string>(serializer)   :> IConvertible
        | TypeCode.UInt16   -> jtval.ToObject<UInt16>(serializer)   :> IConvertible
        | TypeCode.UInt32   -> jtval.ToObject<UInt32>(serializer)   :> IConvertible
        | TypeCode.UInt64   -> jtval.ToObject<UInt64>(serializer)   :> IConvertible
        |   _               -> raise (new JsonSerializationException(sprintf "%s%s" "Type not supported: " (tc.ToString())))

    override this.ReadJson(reader:JsonReader, objectType:Type, existingValue:Object, serializer:JsonSerializer) =
        
        let jt = JToken.Load(reader)
        
        if jt.Type = JTokenType.Null then null
        else
            if jt.Type = JTokenType.Object then
                let jo = JObject(jt)
                let mutable jtval = JToken.FromObject(jo)
                if jo.TryGetValue("$tc", & jtval) then
                    let tc = jtval.ToObject<TypeCode>(serializer)
                    if jo.TryGetValue("$val", & jtval) then
                        this.ReadValue(reader, tc, jtval, serializer) :> Object
                    else
                        raise (new JsonSerializationException("$val property expected."))
                else   
                    raise (new JsonSerializationException("$tc property expected."))
            else
                raise (new JsonSerializationException("Object token expected."))

    override this.WriteJson(writer: JsonWriter, value: Object, serializer: JsonSerializer) =
        if value = null then
            writer.WriteNull()
        else
            let ic = value :?> IConvertible
            if ic.GetTypeCode() = TypeCode.Object then
                failwith "Object type code not supported."
            else
                writer.WriteStartObject()
                writer.WritePropertyName("$tc")
                serializer.Serialize(writer, ic.GetTypeCode())
                writer.WritePropertyName("$val")
                serializer.Serialize(writer, ic)
                writer.WriteEndObject()
