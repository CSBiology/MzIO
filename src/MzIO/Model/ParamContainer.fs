namespace MzIO.Model


open Newtonsoft.Json
open System
open System.Globalization
open System.Dynamic
open System.Collections.Generic
open System.Collections.Specialized
open System.Collections.ObjectModel
open Newtonsoft.Json
open Newtonsoft.Json.Linq
//open System.Reflection
// System.Collections.ICollection //in ICollection


module ReflectionHelper =
    
    // Gets public properties including interface propterties
    let getPublicProperties (t:Type) =
        [|
            for propInfo in t.GetProperties() -> propInfo
            for i in t.GetInterfaces() do yield! i.GetProperties()
        |]

    /// Creates an instance of the Object according to applyStyle and applies the function..
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

    /// Returns the proptery name from quotation expression
    let tryGetPropertyName (expr : Microsoft.FSharp.Quotations.Expr) =
        match expr with
        | Microsoft.FSharp.Quotations.Patterns.PropertyGet (_,pInfo,_) -> Some pInfo.Name
        | _ -> None

    /// Try to get the PropertyInfo by name using reflection
    let tryGetPropertyInfo (o:obj) (propName:string) =
        getPublicProperties (o.GetType())
        |> Array.tryFind (fun n -> n.Name = propName)        

    /// Sets property value using reflection
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

    /// Gets property value as option using reflection
    let tryGetPropertyValue (o:obj) (propName:string) =
        try 
            match tryGetPropertyInfo o propName with 
            | Some v -> Some (v.GetValue(o, null))
            | None -> None
        with 
        | :? System.Reflection.TargetInvocationException -> None
        | :? System.NullReferenceException -> None
    
    /// Gets property value as 'a option using reflection. Cast to 'a
    let tryGetPropertyValueAs<'a> (o:obj) (propName:string) =
        try 
            match tryGetPropertyInfo o propName with 
            | Some v -> Some (v.GetValue(o, null) :?> 'a)
            | None -> None
        with 
        | :? System.Reflection.TargetInvocationException -> None
        | :? System.NullReferenceException -> None

    /// Updates property value by given function
    let tryUpdatePropertyValueFromName (o:obj) (propName:string) (f: 'a -> 'a) =
        let v = optBuildApply f (tryGetPropertyValueAs<'a> o propName)
        trySetPropertyValue o propName v 
        //o

    /// Updates property value by given function
    let tryUpdatePropertyValue (o:obj) (expr : Microsoft.FSharp.Quotations.Expr) (f: 'a -> 'a) =
        let propName = tryGetPropertyName expr
        let g = (tryGetPropertyValueAs<'a> o propName.Value)
        let v = optBuildApply f g
        trySetPropertyValue o propName.Value v 
        //o

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


    [<Struct>]
    //[<JsonConverter(typeof<ConvertibleConverter>)>]
    type ParamValue<'T when 'T :> IConvertible> =
        | CvValue of 'T
        | WithCvUnitAccession of 'T * string

    //[<StructuralEquality;StructuralComparison>]
    type IParamBase<'T when 'T :> IConvertible> =        
        abstract member ID       : string    
        [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
        abstract member Value    : ParamValue<'T> option

    type 
        [<JsonObject(MemberSerialization.OptIn)>]
        //[<JsonConverter(typeof<ParamBaseConverter>)>]
        CvParam<'T when 'T :> IConvertible>[<JsonConstructor>](cvAccession:string, ?paramValue:ParamValue<'T>) =
            
            new(cvAccession) = new CvParam<'T>(cvAccession)
            new() = new CvParam<'T>("CvAccession")

            //[<JsonConstructor>]
            //new(cvAccession:string) = new CvParam<'T>(cvAccession)

            interface IParamBase<'T> with
                [<JsonProperty(Required = Required.Always)>]
                member this.ID    = cvAccession
                [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
                member this.Value = paramValue

            [<JsonProperty(Required = Required.Always)>]
            member this.CvAccession = cvAccession

            [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
            member this.Value = paramValue

    
    type 
        [<JsonObject(MemberSerialization.OptIn)>]
        //[<JsonConverter(typeof<ParamBaseConverter>)>]
        UserParam<'T when 'T :> IConvertible>[<JsonConstructor>](name:string, ?paramValue:ParamValue<'T>) =
         
            new(name) = new UserParam<'T>(name)
            new() = new UserParam<'T>("Name")

            //[<JsonConstructor>]
            //new(name:string) = new UserParam<'T>(name, None)

            interface IParamBase<'T> with
                [<JsonProperty(Required = Required.Always)>]
                member this.ID    = name
                [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
                member this.Value = paramValue

            [<JsonProperty(Required = Required.Always)>]
            member this.Name = name

            [<JsonProperty(NullValueHandling = NullValueHandling.Ignore)>]
            member this.Value = paramValue

    //and ParamBaseConverter() =  

    //    inherit JsonConverter()

    //    override this.CanConvert(objectType:Type) =

    //        failwith ((new NotSupportedException("JsonConverter.CanConvert()")).ToString())

    //    override this.ReadJson(reader:JsonReader, objectType:Type, existingValue:Object, serializer:JsonSerializer) =

    //        let jt = JToken.Load(reader)

    //        if jt.Type = JTokenType.Null then null
    //            else
    //                if jt.Type = JTokenType.Object then
    //                    let jo = JObject(jt)
    //                    let mutable jtval = JToken.FromObject(jo)            
    //                    if jo.TryGetValue("Name", & jtval) then
    //                        jo.ToObject<UserParam<string>>(serializer) :> Object
    //                    else 
    //                        if jo.TryGetValue("CvAccession", & jtval) then
    //                            jo.ToObject<CvParam<string>>(serializer) :> Object
    //                        else 
    //                            failwith ((new JsonSerializationException("Could not determine concrete param type.")).ToString())
    //                else 
    //                    failwith ((new JsonSerializationException("Object token expected.")).ToString())

    //    override this.WriteJson(writer: JsonWriter, value: Object, serializer: JsonSerializer) =

    //        if value = null then
    //            writer.WriteNull()
    //        else
    //            match value with
    //            | :? CvParam<'T>    as value -> serializer.Serialize(writer, value)
    //            | :? UserParam<'T>  as value -> serializer.Serialize(writer, value)
    //            | _     -> failwith ((new JsonSerializationException("Type not supported: " + value.GetType().FullName)).ToString())

    let getCvAccessionOrName (param:#IParamBase<_>) =
        param.ID

    let tryGetValue (param:#IParamBase<_>) =
        match param.Value with
        | Some value ->
            match value with
            | CvValue v                 -> Some v
            | WithCvUnitAccession (v,_) -> Some v
        | None      -> None

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

    [<JsonObject(MemberSerialization.OptIn)>]
    //[<JsonConverter(typeof<ParamBaseConverter>)>]
    type DynamicObj [<JsonConstructor>] internal (dict:Dictionary<string, obj>) = 
    
        inherit DynamicObject () 
        
        [<JsonProperty>]
        let properties = dict//new Dictionary<string, obj>()
        let collectionChanged  = new Event<_, _>()
        ///

        //[<JsonConstructor>]
        new () = DynamicObj(new Dictionary<string, obj>())

        interface INotifyCollectionChanged with
            [<CLIEvent>]
            member this.CollectionChanged = collectionChanged.Publish

        /// Gets property value
        member this.TryGetValue name = 
            // first check the Properties collection for member
            match properties.TryGetValue name with
            | true,value ->  Some value
            // Next check for Public properties via Reflection
            | _ -> ReflectionHelper.tryGetPropertyValue this name


        /// Gets property value
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

        member this.Remove name =
            match ReflectionHelper.removeProperty this name with
            | true -> true
            // Maybe in map
            | false -> properties.Remove(name)


        override this.TryGetMember(binder:GetMemberBinder, result:byref<obj> ) =     
            match this.TryGetValue binder.Name with
            | Some value -> result <- value; true
            | None -> false

        override this.TrySetMember(binder:SetMemberBinder, value:obj) =        
            this.SetValue(binder.Name, value)
            true

        /// Returns and the properties of
        member this.GetProperties includeInstanceProperties =        
            seq [
                if includeInstanceProperties then                
                    for prop in ReflectionHelper.getPublicProperties (this.GetType()) -> 
                        new KeyValuePair<string, obj>(prop.Name, prop.GetValue(this, null))
                for key in properties.Keys ->
                    new KeyValuePair<string, obj>(key, properties.[key]);
            ]

        /// Return both instance and dynamic names.
        /// Important to return both so JSON serialization with Json.NET works.
        override this.GetDynamicMemberNames() =
            this.GetProperties(true) |> Seq.map (fun pair -> pair.Key)

        /// Adds a CvParam with ID as property name and CvParam as value (ID is converted to fit property naming convention)
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

        static member (?) (lookup:#DynamicObj, name:string) =
            match lookup.TryGetValue name with
            | Some(value) -> value
            | None -> raise (System.MemberAccessException())
        static member (?<-) (lookup:#DynamicObj,name:string,value:'v) =
            lookup.SetValue (name, value)

        static member GetValue (lookup:DynamicObj, name) =
            lookup.TryGetValue(name).Value

        static member Remove (lookup:DynamicObj, name) =
            lookup.Remove(name)

        member private this.NotifyItemAdded(key:string, item:'T) =
            collectionChanged.Trigger(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, key))
        
        member private  this.NotifyItemSet(key:string, item:'T) =
            collectionChanged.Trigger(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, key))

        member private  this.NotifyItemRemoved(key:string) =
            collectionChanged.Trigger(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, key))

        member private  this.NotifyCollectionReset() =
            collectionChanged.Trigger(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset))

        member this.TryGetItemByKey(key:'TKey, item:'T) =
            if properties = null then
                false
            else 
                properties.TryGetValue(key, ref item)

        member this.ClearItems() =
            properties.Clear()
            this.NotifyCollectionReset()

        member this.InsertItem(key:string, item:'T) =
            properties.Add(key, item)
            this.NotifyItemAdded(key, item)

        member this.RemoveItem(key:string) =
            properties.Remove(key)  |> ignore
            this.NotifyItemRemoved(key)

        member this.SetItem(key:string, item:'T) =
            properties.Item(key) <- item
            this.NotifyItemSet(key, item)

        member this.Rename(item:'T, newName:string, oldName: string) =
            properties.Remove(oldName) |> ignore
            properties.Add (newName, item)

        member this.Add(key:string, item:'T) =
            properties.Add(key, item)


    













    // #####################################
    // Example usage



