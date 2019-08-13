namespace MzIO.Model


open System
open System.Globalization
open System.Dynamic
open System.Collections.Generic
open System.Collections.Specialized
open System.Collections.ObjectModel
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Serialization


module ReflectionHelper =
    
    /// Gets public properties including interface propterties.
    let getPublicProperties (t:Type) =
        [|
            for propInfo in t.GetProperties() -> propInfo
            for i in t.GetInterfaces() do yield! i.GetProperties()
        |]

    /// Creates an instance of the Object according to applyStyle and applies the function.
    let buildApply (applyStyle:'a -> 'a) =
        let instance =
            System.Activator.CreateInstance<'a>()
        applyStyle instance

    /// Applies 'applyStyle' to item option. If None it creates a new instance.
    let optBuildApply (applyStyle:'a -> 'a) (item:'a option) =
        match item with
        | Some item' -> applyStyle item'
        | None       -> buildApply applyStyle

    /// Applies Some 'applyStyle' to item. If None it returns 'item' unchanged.
    let optApply (applyStyle:('a -> 'a)  option) (item:'a ) =
        match applyStyle with
        | Some apply -> apply item
        | None       -> item

    /// Returns the proptery name from quotation expression.
    let tryGetPropertyName (expr : Microsoft.FSharp.Quotations.Expr) =
        match expr with
        | Microsoft.FSharp.Quotations.Patterns.PropertyGet (_,pInfo,_) -> Some pInfo.Name
        | _ -> None

    /// Try to get the PropertyInfo by name using reflection.
    let tryGetPropertyInfo (o:obj) (propName:string) =
        getPublicProperties (o.GetType())
        |> Array.tryFind (fun n -> n.Name = propName)        

    /// Sets property value using reflection.
    let trySetPropertyValue (o:obj) (propName:string) (value:obj) =
        match tryGetPropertyInfo o propName with 
        | Some property ->
            try 
                property.SetValue(o, value, null)
                Some o
            with
            | :? System.ArgumentException -> None
            | :? System.NullReferenceException -> None
        | None -> None

    /// Gets property value as option using reflection.
    let tryGetPropertyValue (o:obj) (propName:string) =
        try 
            match tryGetPropertyInfo o propName with 
            | Some v -> Some (v.GetValue(o, null))
            | None -> None
        with 
        | :? System.Reflection.TargetInvocationException -> None
        | :? System.NullReferenceException -> None
    
    /// Gets property value as 'a option using reflection. Cast to 'a.
    let tryGetPropertyValueAs<'a> (o:obj) (propName:string) =
        try 
            match tryGetPropertyInfo o propName with 
            | Some v -> Some (v.GetValue(o, null) :?> 'a)
            | None -> None
        with 
        | :? System.Reflection.TargetInvocationException -> None
        | :? System.NullReferenceException -> None

    /// Updates property value by given function.
    let tryUpdatePropertyValueFromName (o:obj) (propName:string) (f: 'a -> 'a) =
        let v = optBuildApply f (tryGetPropertyValueAs<'a> o propName)
        trySetPropertyValue o propName v 
        //o

    /// Updates property value by given function.
    let tryUpdatePropertyValue (o:obj) (expr : Microsoft.FSharp.Quotations.Expr) (f: 'a -> 'a) =
        let propName = tryGetPropertyName expr
        let g = (tryGetPropertyValueAs<'a> o propName.Value)
        let v = optBuildApply f g
        trySetPropertyValue o propName.Value v 
        //o

    /// Updates property value by given function and returns unit.
    let updatePropertyValueAndIgnore (o:obj) (expr : Microsoft.FSharp.Quotations.Expr) (f: 'a -> 'a) = 
        tryUpdatePropertyValue o expr f |> ignore


    /// Removes property 
    let removeProperty (o:obj) (propName:string) =        
        match tryGetPropertyInfo o propName with         
        | Some property ->
            try 
                property.SetValue(o, null, null)
                true
            with
            | :? System.ArgumentException -> false
            | :? System.NullReferenceException -> false
        | None -> false


module CvParam =

    // ########################################
    // ########################################
    // ########################################
    // Cv Param


    /// A struct used to save either value and/or unit accession.
    [<Struct>]
    type ParamValue<'T when 'T :> IConvertible> =
    | CvValue of 'T
    | WithCvUnitAccession of 'T * string

    /// An interface of a expansible description that can be referenced by an id and has an additional value of type ParamValue<'T>.
    type IParamBase<'T when 'T :> IConvertible> =        
        abstract member ID       : string    
        [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
        abstract member Value    : ParamValue<'T> option

    /// This class contains additional data or annotation. Only controlled values are allowed here. 
    [<JsonObject(MemberSerialization.OptIn)>]
    //This attribute gives json information of the way to de- and serialize objects of this type.
    [<JsonConverter(typeof<ParamBaseConverter>)>]
    type CvParam<'T when 'T :> IConvertible>(cvAccession:string, ?paramValue:ParamValue<'T>) =
            
        [<JsonConstructor>]
        new(cvAccession) = new CvParam<'T>(cvAccession)
        new() = new CvParam<'T>("CvAccession")

        interface IParamBase<'T> with
            [<JsonProperty(Required = Required.Always)>]
            member this.ID    = cvAccession
            [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
            member this.Value = if paramValue.IsSome then paramValue else None

        [<JsonProperty(Required = Required.Always)>]
        member this.CvAccession = cvAccession

        [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
        member this.Value = if paramValue.IsSome then paramValue else None

    
    and 
        /// This class contains additional data or annotation. Uncontrolled user parameters (essentially allowing free text). 
        [<JsonObject(MemberSerialization.OptIn)>]
        //This attribute gives json information of the way to de- and serialize objects of this type.
        [<JsonConverter(typeof<ParamBaseConverter>)>]
        UserParam<'T when 'T :> IConvertible>(name:string, ?paramValue:ParamValue<'T>) =
            
            [<JsonConstructor>]
            new(name) = new UserParam<'T>(name)
            new() = new UserParam<'T>("Name")

            interface IParamBase<'T> with
                [<JsonProperty(Required = Required.Always)>]
                member this.ID    = name
                [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
                member this.Value = if paramValue.IsSome then paramValue else None

            [<JsonProperty(Required = Required.Always)>]
            member this.Name = name

            [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
            member this.Value = if paramValue.IsSome then paramValue else None

    /// This class contains information of how cvparams and userparams shall be serialized and deserialized.
    and private ParamBaseConverter() =  

        inherit JsonConverter()

        /// Serializes value of either cv or user param to json string.
        static member private createJsonValue(item:string) =
            if item.StartsWith("WithCvUnitAccession") then
                let tmp = item.Substring(0, 19), item.Substring(20)
                let tmpID = sprintf "\"Type\":\"%s\"" (fst tmp)
                let values = if (snd tmp) :> Object :? UInt64 then decimal(Convert.ToUInt64 (snd tmp)).ToString() else snd tmp
                let tmpValues = sprintf ",\"Values\":[%s]" (((values).Remove(0, 1)).Remove((values).Length-2))
                sprintf "%s}" (sprintf "%s%s" tmpID tmpValues)
            else
                if item.StartsWith("CvValue") then
                    let tmp = item.Substring(0, 7), item.Substring(8)
                    let tmpID = sprintf "\"Type\":\"%s\"" (fst tmp)
                    let values = if (snd tmp) :> Object :? UInt64 then int64(Convert.ToUInt64 (snd tmp)).ToString() else snd tmp
                    let tmpValues = sprintf ",\"Values\":[%s]" (values)
                    sprintf "%s}" (sprintf "%s%s" tmpID tmpValues)
                else
                    let tmpID = "\"Type\":\"CvValue\""
                    let tmpValues = ",\"Values\":[null]"
                    sprintf "%s}" (sprintf "%s%s" tmpID tmpValues)

        /// Deserializes json string part that contains information about the value to object of type paramvalue<IConvertible>.
        static member private createParamValue(item:JObject) =
            match item.["Type"].ToString() with
            | "WithCvUnitAccession" -> 
                let tmp = item.["Values"] :?> JArray
                match tmp.First.ToString() with
                | ""    -> 
                    ParamValue.WithCvUnitAccession(
                        Unchecked.defaultof<IConvertible>, item.["Values"].Last.ToString()
                                                  )
                | null  ->
                    ParamValue.WithCvUnitAccession(
                        Unchecked.defaultof<IConvertible>, item.["Values"].Last.ToString()
                                                  )
                | _     ->
                    ParamValue.WithCvUnitAccession(
                        item.["Values"].First.ToString() :> IConvertible, 
                        item.["Values"].Last.ToString()
                                                  )
            | "CvValue" ->
                let tmp = item.["Values"] :?> JArray
                match tmp.First.ToString() with                
                | ""    -> ParamValue.CvValue(Unchecked.defaultof<IConvertible>)
                | null  -> ParamValue.CvValue(Unchecked.defaultof<IConvertible>)
                | _     -> ParamValue.CvValue(item.["Values"].First.ToString() :> IConvertible)
                
            | _     -> raise (new JsonSerializationException("Could not determine concrete param type."))

        /// Serializes cv or user param to json string.
        static member private createJsonParam<'T when 'T :> IConvertible>(param:Object) =
            match param with
            | :? CvParam<'T>    as item ->                
                let tmpID = sprintf "{\"%s\":\"%s\",\"CvAccession\":\"%s\"," "$id" "1" item.CvAccession
                let tmpValue = 
                    ParamBaseConverter.createJsonValue(
                        if item.Value.IsSome then
                            item.Value.Value.ToString() 
                        else "None")
                JObject.Parse(sprintf "%s%s" tmpID tmpValue)
            | :? UserParam<'T>  as item -> 
                let tmpID = sprintf "{\"%s\":\"%s\",\"Name\":\"%s\"," "$id" "1" item.Name
                let tmpValue = 
                    ParamBaseConverter.createJsonValue(
                        if item.Value.IsSome then
                            item.Value.Value.ToString() 
                            else "None")
                JObject.Parse(sprintf "%s%s" tmpID tmpValue)
            | _     -> raise (new JsonSerializationException("Could not determine concrete param type."))
        
        /// Not supported member application.
        override this.CanConvert(objectType:Type) =

            raise (new NotSupportedException("JsonConverter.CanConvert()"))

        /// Deserializes json string to either cv or user param.
        override this.ReadJson(reader:JsonReader, objectType:Type, existingValue:Object, serializer:JsonSerializer) =
            let jt = JToken.Load(reader)
            if jt.Type = JTokenType.Null then null
                else
                    if jt.Type = JTokenType.Object then
                        let jo = jt.ToObject<JObject>()
                        if jo.["CvAccession"] = null then
                            if jo.["Name"] = null then
                                raise (new JsonSerializationException("Could not determine concrete param type."))
                            else
                                let values = ParamBaseConverter.createParamValue jo
                                new UserParam<IConvertible>(jo.["Name"].ToString(), values) :> Object
                        else
                            let values = ParamBaseConverter.createParamValue jo
                            new CvParam<IConvertible>(jo.["CvAccession"].ToString(), values) :> Object
                    else 
                        raise (new JsonSerializationException("Object token expected."))

        /// Serializes cv or user param to json string.
        override this.WriteJson(writer: JsonWriter, value: Object, serializer: JsonSerializer) =

            if value = null then
                writer.WriteNull()
            else
                serializer.Serialize(writer, ParamBaseConverter.createJsonParam value)

    /// Retrives accession of cv or name of user param.
    let getCvAccessionOrName (param:#IParamBase<_>) =
        param.ID

    /// Tries to return value.
    let tryGetValue (param:#IParamBase<_>) =
        match param.Value with
        | Some value ->
            match value with
            | CvValue v                 -> Some v
            | WithCvUnitAccession (v,_) -> Some v
        | None      -> None

    /// Tries to return value.
    let tryGetCvUnitAccession (param:#IParamBase<_>) =
        match param.Value with
        |Some value ->
            match value with
            | CvValue _                 -> None
            | WithCvUnitAccession (_,a) -> Some a
        | None      -> None

    //TODO: Convert ID to fit property naming convention
    let getPropertyNameOf (param:#IParamBase<_>) =
        param.ID


    // ########################################
    // ########################################
    // ########################################
    // Cv Param

    /// Dynamic object with unspecic amount and typed fields, which can be added during runtime.
    /// Access specific item with its property name.
    /// Mainly used to save cv and user param.
    [<JsonObject(MemberSerialization.OptIn)>]
    type DynamicObj [<JsonConstructor>] internal (dict:Dictionary<string, obj>) = 
    
        inherit DynamicObject () 
        
        [<JsonProperty>]
        let properties = dict
        let collectionChanged  = new Event<_, _>()
        ///

        new () = DynamicObj(new Dictionary<string, obj>())

        interface INotifyCollectionChanged with
            [<CLIEvent>]
            member this.CollectionChanged = collectionChanged.Publish

        /// Gets property value.
        member this.TryGetValue name = 
            // first check the Properties collection for member
            match properties.TryGetValue name with
            | true,value ->  Some value
            // Next check for Public properties via Reflection
            | _ -> ReflectionHelper.tryGetPropertyValue this name

        /// Gets property value of specified type 'a.
        member this.TryGetTypedValue<'a> name = 
            match (this.TryGetValue name) with
            | None -> None
            | Some o -> 
                match o with
                | :? 'a -> o :?> 'a |> Some
                | _ -> None
        
        /// Sets property value, creating a new property if none exists
        member this.SetValue (name, value) = // private
            // first check to see if there's a native property to set

            match ReflectionHelper.tryGetPropertyInfo this name with
            | Some property ->
                try 
                    // let t = property.ReflectedType
                    // t.InvokeMember(name,Reflection.BindingFlags.SetProperty,null,this,[|value|]) |> ignore

                    //let tmp = Convert.ChangeType(this, property.ReflectedType)
                    //let tmp = downcast this : (typeof<t.GetType()>)
                    property.SetValue(this, value, null)
                with
                | :? System.ArgumentException       -> raise (System.ArgumentException("Readonly property - Property set method not found.")) 
                | :? System.NullReferenceException  -> raise (System.NullReferenceException())
        
            | None -> 
                // Next check the Properties collection for member
                match properties.TryGetValue name with            
                | true,_ -> properties.[name] <- value
                | _      -> properties.Add(name, value)

        /// Removes item from dynamic object when it existed.
        member this.Remove name =
            match ReflectionHelper.removeProperty this name with
            | true -> true
            // Maybe in map
            | false -> properties.Remove(name)

        /// Overrides existing object with one saved in the dynamic object, which is accesed by the property name.
        override this.TryGetMember(binder:GetMemberBinder, result:byref<obj> ) =     
            match this.TryGetValue binder.Name with
            | Some value -> result <- value; true
            | None -> false

        /// Overrides existing object with one saved in the dynamic object, which is accesed by the property name.
        override this.TrySetMember(binder:SetMemberBinder, value:obj) =        
            this.SetValue(binder.Name, value)
            true

        /// Returns the properties of the dynamic object.
        /// Classes which inherit the dynamic object and have fixed members can exclude the returning
        /// of their fixed members by setting the includeInstanceProperties to false.
        member this.GetProperties includeInstanceProperties =        
            seq [
                    if includeInstanceProperties then                
                        for prop in ReflectionHelper.getPublicProperties (this.GetType()) -> 
                            new KeyValuePair<string, obj>(prop.Name, prop.GetValue(this, null))
                    for key in properties.Keys ->
                        new KeyValuePair<string, obj>(key, properties.[key])
                ]

        /// Return both instance and dynamic names.
        /// Important to return both so JSON serialization with Json.NET works.
        override this.GetDynamicMemberNames() =
            this.GetProperties(true) |> Seq.map (fun pair -> pair.Key)

        /// Adds a cv param with id as property name and cv param as value (id is converted to fit property naming convention).
        member this.AddCvParam(param:CvParam<'T>) =
            //let param' = param :> IParamBase<'T>
            let value =
                match tryGetValue param with
                | Some value -> value :> IConvertible
                | None       -> Unchecked.defaultof<IConvertible>
            let paramBase =
                match tryGetCvUnitAccession param with
                | Some unit -> ParamValue.WithCvUnitAccession(value, unit)
                | None      -> ParamValue.CvValue(value)
            let param' = new CvParam<IConvertible>(param.CvAccession, paramBase)
            this.SetValue(getPropertyNameOf param', param')

        /// Adds a user param with name as property name and user param as value (name is converted to fit property naming convention).
        member this.AddUserParam(param:UserParam<'T>) =
            let value =
                match tryGetValue param with
                | Some value -> value :> IConvertible
                | None       -> Unchecked.defaultof<IConvertible>
            let paramBase =
                match tryGetCvUnitAccession param with
                | Some unit -> ParamValue.WithCvUnitAccession(value, unit)
                | None      -> ParamValue.CvValue(value)
            let param' = new UserParam<IConvertible>(param.Name, paramBase)
            this.SetValue(getPropertyNameOf param', param')

        /// Adds a cv or user param with id or name as property name and the object as value (id/name is converted to fit property naming convention).
        member this.AddIParamBase(param:IParamBase<'T>) =
            let value =
                match tryGetValue param with
                | Some value -> value :> IConvertible
                | None       -> Unchecked.defaultof<IConvertible>
            let paramBase =
                match tryGetCvUnitAccession param with
                | Some unit -> ParamValue.WithCvUnitAccession(value, unit)
                | None      -> ParamValue.CvValue(value)
            let param' = new UserParam<IConvertible>(param.ID, paramBase) :> IParamBase<IConvertible>
            this.SetValue(getPropertyNameOf param', param')

        /// Static member method to return value associated with the name in given dynamic object.
        static member (?) (lookup:#DynamicObj, name:string) =
            match lookup.TryGetValue name with
            | Some(value) -> value
            | None -> raise (System.MemberAccessException())

        /// Static member method to set value associated with the name in given dynamic object.
        static member (?<-) (lookup:#DynamicObj,name:string,value:'v) =
            lookup.SetValue (name, value)

        /// Static member method to return value associated with the name in given dynamic object.
        static member GetValue (lookup:DynamicObj, name) =
            lookup.TryGetValue(name).Value

        /// Static member method to remove value associated with the name in given dynamic object.
        static member Remove (lookup:DynamicObj, name) =
            lookup.Remove(name)

        /// Method to notify that value with name has been added to dynamic object.
        member private this.NotifyItemAdded(key:string, item:'T) =
            collectionChanged.Trigger(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, key))
        
        /// Method to notify that value of name has been set in dynamic object.
        member private  this.NotifyItemSet(key:string, item:'T) =
            collectionChanged.Trigger(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, key))

        /// Method to notify that value of name has been removed from dynamic object.
        member private  this.NotifyItemRemoved(key:string) =
            collectionChanged.Trigger(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, key))

        /// Method to notify that dynamic object has been reseted.
        member private  this.NotifyCollectionReset() =
            collectionChanged.Trigger(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset))

        /// Method to return value associated with the name in given dynamic object.
        member this.TryGetItemByKey(key:'TKey, item:'T) =
            if properties = null then
                false
            else 
                properties.TryGetValue(key, ref item)

        /// Method to remove all values of dynamic object and notify about resetting dynamic object.
        member this.ClearItems() =
            properties.Clear()
            this.NotifyCollectionReset()

        /// Method to add value  with name to dynamic object and notify about addition.
        member this.InsertItem(key:string, item:'T) =
            properties.Add(key, item)
            this.NotifyItemAdded(key, item)

        /// Method to remove value with name from dynamic object and notify about deletion.
        member this.RemoveItem(key:string) =
            properties.Remove(key)  |> ignore
            this.NotifyItemRemoved(key)

        /// Method to set value with name of dynamic object and notify about value change.
        member this.SetItem(key:string, item:'T) =
            properties.Item(key) <- item
            this.NotifyItemSet(key, item)
            
        /// Method to set replace property name with new name of dynamic object and notify about replacement.
        member this.Rename(item:'T, newName:string, oldName: string) =
            properties.Remove(oldName) |> ignore
            properties.Add (newName, item)

        /// Method to add value  with name to dynamic object.
        member this.Add(key:string, item:'T) =
            if this.TryGetItemByKey(key, item) = false then
                properties.Add(key, item)
            else
                failwith "Object with current key already exists"

        /// Method to try add value  with name to dynamic object and return bool.
        member this.TryAdd(key:string, item:'T) =
            if this.TryGetItemByKey(key, item) = false then
                properties.Add(key, item)
                true
            else
                false

        /// Method to call amount of not fixed members of dynamic object.
        member this.Count() =
            this.GetProperties false
            |> Seq.length
    













    // #####################################
    // Example usage



