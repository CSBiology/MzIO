namespace MzLiteFSharp.MetaData


open System
open System.Globalization
open MzLiteFSharp.Model.CvParam


module ParamEditExtension =
    
    type IHasUnit<'TPC when 'TPC :> DynamicObj> =

        abstract member ParamContainer  : 'TPC

        abstract member Param           : IParamBase<IConvertible>

        abstract member SetUnit         : string -> 'TPC

        abstract member NoUnit          : unit -> 'TPC

    [<Sealed>]
    type HasUnit<'TPC when 'TPC :> DynamicObj> internal (pc:'TPC, p:IParamBase<IConvertible>) =

        inherit DynamicObj()

        let mutable paramContainer' = pc

        let mutable param'          = p

        //new () = HasUnit(pc:'TPC, CvParam<IConvertible>(null))

        interface IHasUnit<'TPC> with

            member this.ParamContainer  = paramContainer'
            member this.Param           = param'
            member this.SetUnit(unitAccession:string) =
                match unitAccession with
                | null  -> failwith (ArgumentNullException("unitAccession","accession may not be null or empty.").ToString())
                | ""    -> failwith (ArgumentNullException("unitAccession","accession may not be null or empty.").ToString())
                | " "   -> failwith (ArgumentNullException("unitAccession","accession may not be null or empty.").ToString())
                |   _   -> 
                    let paramValue = 
                        match tryGetValue param' with
                        | Some value -> new CvParam<IConvertible>(param'.ID, ParamValue.WithCvUnitAccession(value, unitAccession)) :> IParamBase<IConvertible>
                        | None       -> new CvParam<IConvertible>(param'.ID, ParamValue.WithCvUnitAccession(Unchecked.defaultof<IConvertible>, unitAccession)) :> IParamBase<IConvertible>
                    paramContainer'.SetValue(param'.ID, paramValue)
                    paramContainer'
            member this.NoUnit() =
                let paramValue = 
                    match tryGetValue param' with
                    | Some value -> new CvParam<IConvertible>(param'.ID, ParamValue.CvValue(value)) :> IParamBase<IConvertible>
                    | None       -> new CvParam<IConvertible>(param'.ID) :> IParamBase<IConvertible>
                paramContainer'.SetValue(param'.ID, paramValue)
                paramContainer'

    //Replace pc with this
    type DynamicObj with

        member this.SetCvParam(accession:string, value:IConvertible) =

            let mutable param' =
                match accession with
                | null  -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | ""    -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | " "   -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                |   _   ->
                    let paramValue = ParamValue.CvValue(value)
                    let mutable param = new CvParam<IConvertible>(accession, paramValue)
                    if this.TryGetTypedValue<CvParam<IConvertible>>(accession).IsNone then
                        this.AddCvParam(param)
                    else ()
                    param :> IParamBase<IConvertible>
            new HasUnit<_>(this, param')

        member this.SetCvParam(accession:string) =

            let mutable param' =
                match accession with
                | null  -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | ""    -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | " "   -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                |   _   ->
                    let mutable param = new CvParam<IConvertible>(accession)
                    if this.TryGetTypedValue<CvParam<IConvertible>>(accession).IsNone then
                        this.AddCvParam(param)
                    else ()
                    param
            new HasUnit<_>(this, param' :> IParamBase<IConvertible>)

        member this.HasCvParam(name:string) =
            
           match name with
                | null  -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | ""    -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | " "   -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                |   _   -> this.TryGetItemByKey(name, this)

        member this.SetUserParam(name:string, value:IConvertible) =

            let mutable param' =
                match name with
                | null  -> failwith (ArgumentNullException("name","accession may not be null or empty.").ToString())
                | ""    -> failwith (ArgumentNullException("name","accession may not be null or empty.").ToString())
                | " "   -> failwith (ArgumentNullException("name","accession may not be null or empty.").ToString())
                |   _   ->
                    let paramValue = ParamValue.CvValue(value)
                    let mutable param = new UserParam<IConvertible>(name, paramValue)
                    if this.TryGetTypedValue<UserParam<IConvertible>>(name).IsNone then
                        this.AddUserParam(param)
                    else ()
                    param
            new HasUnit<_>(this, param')

        member this.SetUserParam(name:string) =

            let mutable param' =
                match name with
                | null  -> failwith (ArgumentNullException("name","accession may not be null or empty.").ToString())
                | ""    -> failwith (ArgumentNullException("name","accession may not be null or empty.").ToString())
                | " "   -> failwith (ArgumentNullException("name","accession may not be null or empty.").ToString())
                |   _   ->
                    let mutable param = new UserParam<IConvertible>(name)
                    if this.TryGetTypedValue<UserParam<IConvertible>>(name).IsNone then
                        this.AddUserParam(param)
                    else ()
                    param
            new HasUnit<_>(this, param' :> IParamBase<IConvertible>)

        member this.HasUserParam(accession:string) =
            
           match accession with
                | null  -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | ""    -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | " "   -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                |   _   -> this.TryGetItemByKey(accession, this)


        /// Has functionality for User- and CvParam
        member this.TryGetParam(accession:string) =
            match accession with
                | null  -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | ""    -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                | " "   -> failwith (ArgumentNullException("accession","accession may not be null or empty.").ToString())
                |   _   -> this.TryGetItemByKey(accession, this)

    type IParamBase<'T when 'T :> IConvertible> with

        member this.HasValue() =
            if (tryGetValue this).IsSome then true
            else false

        member this.HasUnit() =            
            if (tryGetCvUnitAccession this).IsSome then true
            else false

        member this.HasUnit(unitAccession:string) =   
            let tmp = (tryGetCvUnitAccession this)
            if tmp.IsSome then tmp.Value = unitAccession
            else false

        member this.GetValueOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then tmp.Value
            else Unchecked.defaultof<'T>

        member this.GetStringOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then tmp.Value.ToString()
            else Unchecked.defaultof<string>

        member this.GetBooleanOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToBoolean(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<bool>
            else Unchecked.defaultof<bool>

        member this.GetByteOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToByte(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<Byte>
            else Unchecked.defaultof<Byte>

        member this.GetCharOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToChar(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<Char>
            else Unchecked.defaultof<Char>

        member this.GetDoubleOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToDouble(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<double>
            else Unchecked.defaultof<double>

        member this.GetInt32OrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToInt32(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<int32>
            else Unchecked.defaultof<int32>

        member this.GetInt64OrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToInt64(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<int64>
            else Unchecked.defaultof<int64>

        member this.GetSingleOrDefault() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                try Convert.ToSingle(tmp.Value)
                with 
                    | :? System.InvalidCastException -> Unchecked.defaultof<single>
            else Unchecked.defaultof<single>

        member this.GetString() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToString(new CultureInfo("en-US"))
            else
                failwith (InvalidOperationException("Param value not set.").ToString())

        member this.GetBoolean() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToBoolean(new CultureInfo("en-US"))
            else
                failwith (InvalidOperationException("Param value not set.").ToString())

        member this.GetByte() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToByte(new CultureInfo("en-US"))
            else
                failwith (InvalidOperationException("Param value not set.").ToString())

        member this.GetChar() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToChar(new CultureInfo("en-US"))
            else
                failwith (InvalidOperationException("Param value not set.").ToString())

        member this.GetInt32() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToInt32(new CultureInfo("en-US"))
            else
                failwith (InvalidOperationException("Param value not set.").ToString())

        member this.GetInt64() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToInt64(new CultureInfo("en-US"))
            else
                failwith (InvalidOperationException("Param value not set.").ToString())

        member this.GetSingle() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToSingle(new CultureInfo("en-US"))
            else
                failwith (InvalidOperationException("Param value not set.").ToString())

        member this.GetDouble() =
            let tmp = (tryGetValue this)
            if tmp.IsSome then 
                tmp.Value.ToDouble(new CultureInfo("en-US"))
            else
                failwith (InvalidOperationException("Param value not set.").ToString())
