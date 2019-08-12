namespace MzIO.Bruker


open System
open System.Data
open System.Data.Linq
open System.Data.Linq.Mapping
open System.Data.SQLite
open System.Linq
open System.Collections.Generic

module Linq2BafSql =

    [<Sealed>]
    [<Table(Name = "Spectra")>]
    type BafSqlSpectrum(
                        id:Nullable<UInt64>, rt:Nullable<float>, seg:Nullable<UInt64>, aqk:Nullable<UInt64>, parent:Nullable<UInt64>, mzAqRL:Nullable<UInt64>,
                        mzAqRUpper:Nullable<UInt64>, sumInt:Nullable<float>, maxInt:Nullable<float>, tranForId:Nullable<UInt64>, profMzId:Nullable<UInt64>, 
                        profIntId:Nullable<UInt64>, lineIndexId:Nullable<UInt64>, lineMzId:Nullable<UInt64>, lineIntId:Nullable<UInt64>,
                        lineIdxWithId:Nullable<UInt64>, linePeakAreaId:Nullable<UInt64>, lineSnrId:Nullable<UInt64>
                       ) =
        
        let mutable id              = id                (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable rt              = rt                (*Unchecked.defaultof<Nullable<float>>*)
        let mutable seg             = seg               (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable aqk             = aqk               (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable parent          = parent            (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable mzAqRL          = mzAqRL            (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable mzAqRUpper      = mzAqRUpper        (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable sumInt          = sumInt            (*Unchecked.defaultof<Nullable<float>>*)
        let mutable maxInt          = maxInt            (*Unchecked.defaultof<Nullable<float>>*)
        let mutable tranForId       = tranForId         (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable profMzId        = profMzId          (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable profIntId       = profIntId         (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable lineIndexId     = lineIndexId       (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable lineMzId        = lineMzId          (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable lineIntId       = lineIntId         (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable lineIdxWithId   = lineIdxWithId     (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable linePeakAreaId  = linePeakAreaId    (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable lineSnrId       = lineSnrId         (*Unchecked.defaultof<Nullable<UInt64>>*)

        new() = new BafSqlSpectrum(
                                   System.Nullable(), System.Nullable(), System.Nullable(), System.Nullable(), System.Nullable(), System.Nullable(), System.Nullable(), 
                                   System.Nullable(), System.Nullable(), System.Nullable(), System.Nullable(), System.Nullable(), System.Nullable(), System.Nullable(), 
                                   System.Nullable(), System.Nullable(), System.Nullable(), System.Nullable()
                                  )

        [<Column(IsPrimaryKey = true)>]
        member this.Id 
            with get() = id 
            and  set(value) = id <- value
            
        [<Column>]                              
        member this.Rt
            with get() = rt 
            and  set(value) = rt <- value
        [<Column>]                              
        member this.Segment
            with get() = seg 
            and  set(value) = seg <- value
        [<Column>]                              
        member this.AcquisitionKey
            with get() = aqk 
            and  set(value) = aqk <- value
        [<Column>]                              
        member this.Parent
            with get() = parent 
            and  set(value) = parent <- value
        [<Column>]                              
        member this.MzAcqRangeLower
            with get() = mzAqRL 
            and  set(value) = mzAqRL <- value
        [<Column>]                              
        member this.MzAcqRangeUpper
            with get() = mzAqRUpper 
            and  set(value) = mzAqRUpper <- value
        [<Column>]                              
        member this.SumIntensity
            with get() = sumInt 
            and  set(value) = sumInt <- value
        [<Column>]                              
        member this.MaxIntensity
            with get() = maxInt 
            and  set(value) = maxInt <- value
        [<Column>]                              
        member this.TransformatorId
            with get() = tranForId 
            and  set(value) = tranForId <- value
        [<Column>]                              
        member this.ProfileMzId
            with get() = profMzId 
            and  set(value) = profMzId <- value
        [<Column>]                              
        member this.ProfileIntensityId
            with get() = profIntId 
            and  set(value) = profIntId <- value
        [<Column>]                               
        member this.LineIndexId
            with get() = lineIndexId 
            and  set(value) = lineIndexId <- value
        [<Column>]                              
        member this.LineMzId
            with get() = lineMzId 
            and  set(value) = lineMzId <- value
        [<Column>]                               
        member this.LineIntensityId     
            with get() = lineIntId 
            and  set(value) = lineIntId <- value
        [<Column>]                              
        member this.LineIndexWidthId
            with get() = lineIdxWithId 
            and  set(value) = lineIdxWithId <- value
        [<Column>]                              
        member this.LinePeakAreaId
            with get() = linePeakAreaId 
            and  set(value) = linePeakAreaId <- value
        [<Column>]                              
        member this.LineSnrId
            with get() = lineSnrId 
            and  set(value) = lineSnrId <- value

    [<Sealed>]
    [<Table(Name = "AcquisitionKeys")>]
    type BafSqlAcquisitionKey(id:Nullable<UInt64>, poalrity:Nullable<int64>, scanMode:Nullable<int64>, aqMode:Nullable<int64>, msLevel:Nullable<int64>) =

        let mutable id          = id        (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable poalrity    = poalrity  (*Unchecked.defaultof<Nullable<int64>>*)
        let mutable scanMode    = scanMode  (*Unchecked.defaultof<Nullable<int64>>*)
        let mutable aqMode      = aqMode    (*Unchecked.defaultof<Nullable<int64>>*)
        let mutable msLevel     = msLevel   (*Unchecked.defaultof<Nullable<int64>>*)

        new() = new BafSqlAcquisitionKey(System.Nullable(), System.Nullable(),System.Nullable(), System.Nullable(), System.Nullable())

        [<Column(IsPrimaryKey = true)>]
        member this.Id
            with get() = id 
            and  set(value) = id <- value
        [<Column>]                      
        member this.Polarity
            with get() = poalrity 
            and  set(value) = poalrity <- value
        [<Column>]                      
        member this.ScanMode
            with get() = scanMode 
            and  set(value) = scanMode <- value
        [<Column>]                      
        member this.AcquisitionMode
            with get() = aqMode 
            and  set(value) = aqMode <- value
        [<Column>]                      
        member this.MsLevel
            with get() = msLevel 
            and  set(value) = msLevel <- value

    [<Sealed>]
    [<Table(Name = "PerSpectrumVariables")>]
    type BafSqlPerSpectrumVariable(spec:Nullable<UInt64>, var:Nullable<UInt64>, value':Nullable<Decimal>) =

        let mutable spec    = spec      (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable var     = var       (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable value'  = value'    (*Unchecked.defaultof<Nullable<Decimal>>*)

        [<Column(IsPrimaryKey = true)>]
        member this.Spectrum
            with get() = spec 
            and  set(value) = spec <- value
        [<Column(IsPrimaryKey = true)>]
        member this.Variable
            with get() = var 
            and  set(value) = var <- value
        [<Column>]                      
        member this.Value
            with get() = value' 
            and  set(value) = value' <- value

    [<Sealed>]
    [<Table(Name = "SupportedVariables")>]
    type BafSqlSupportedVariable() =

        let mutable var             = Unchecked.defaultof<Nullable<UInt64>>
        let mutable permName        = Unchecked.defaultof<string>
        let mutable type'           = Unchecked.defaultof<Nullable<UInt64>>
        let mutable disGroupName    = Unchecked.defaultof<string>
        let mutable disName         = Unchecked.defaultof<string>
        let mutable disValueText    = Unchecked.defaultof<string>
        let mutable disFor          = Unchecked.defaultof<string>
        let mutable disDim          = Unchecked.defaultof<string>

        [<Column(IsPrimaryKey = true)>]
        member this.Variable
            with get() = var
            and  set(value) = var <- value
        [<Column>]                              
        member this.PermanentName
            with get() = permName
            and  set(value) = permName <- value
        [<Column>]                              
        member this.Type
            with get() = type'
            and  set(value) = type' <- value
        [<Column>]                              
        member this.DisplayGroupName
            with get() = disGroupName
            and  set(value) = disGroupName <- value
        [<Column>]                              
        member this.DisplayName
            with get() = disName
            and  set(value) = disName <- value
        [<Column>]                              
        member this.DisplayValueText
            with get() = disValueText
            and  set(value) = disValueText <- value
        [<Column>]                              
        member this.DisplayFormat
            with get() = disFor
            and  set(value) = disFor <- value
        [<Column>]                              
        member this.DisplayDimension
            with get() = disDim
            and  set(value) = disDim <- value


    [<Sealed>]
    [<Table(Name = "Steps")>]
    type BafSqlStep(tarSpec:Nullable<UInt64>, num:Nullable<int64>, isoType:Nullable<int64>, reaType:Nullable<int64>, msLvl:Nullable<int64>, mass:Nullable<float>) =
        
        let mutable tarSpec = tarSpec   (*Unchecked.defaultof<Nullable<UInt64>>*)
        let mutable num     = num       (*Unchecked.defaultof<Nullable<int64>>*)
        let mutable isoType = isoType   (*Unchecked.defaultof<Nullable<int64>>*)
        let mutable reaType = reaType   (*Unchecked.defaultof<Nullable<int64>>*)
        let mutable msLvl   = msLvl     (*Unchecked.defaultof<Nullable<int64>>*)
        let mutable mass    = mass      (*Unchecked.defaultof<Nullable<float>>*)

        [<Column>]
        member this.TargetSpectrum
            with get() = tarSpec
            and  set(value) = tarSpec <- value
        [<Column>]                          
        member this.Number
            with get() = num
            and  set(value) = num <- value
        [<Column>]                          
        member this.IsolationType
            with get() = isoType
            and  set(value) = isoType <- value
        [<Column>]                          
        member this.ReactionType
            with get() = reaType
            and  set(value) = reaType <- value
        [<Column>]                          
        member this.MsLevel
            with get() = msLvl
            and  set(value) = msLvl <- value
        [<Column>]                          
        member this.Mass
            with get() = mass
            and  set(value) = mass <- value

    [<Sealed>]
    type Linq2BafSql(sqlFilePath:string) =

        let mapping = new AttributeMappingSource()
        let mutable isDisposed = false
        let cn = new SQLiteConnection(sprintf "%s%s%s" "Data Source=" sqlFilePath ";Version=3")
        let core = new DataContext(cn, mapping)
        let mutable changes =
            core.DeferredLoadingEnabled <- false
            core.ObjectTrackingEnabled <- false

        // opening a connection at this point leads to a 7 fold speed increase when looking up mass spectra.
        let cnOpen = core.Connection.Open()

        // increases speed of massspectra look up but is not compatible with a
        // function in baf2sql_c.dll (probably when creating the sqlite cache) because they are blocking the db access of each another
        // TODO: Examine which baf2sql_c method is causing this.    
        //System.Data.Common.DbTransaction tn = core.Connection.BeginTransaction();
        let checkInDexStepsID =
            try
                core.ExecuteQuery<int>("CREATE INDEX StepsID ON Steps (TargetSpectrum)")
            with
                | :? Exception ->
                    failwith "INDEX On TargedSpectrum in Steps table already exists, creation is skipped" 
        
        let checkIndexSpectrumID =
            try
                core.ExecuteQuery<int>("CREATE INDEX SpectrumID ON PerSpectrumVariables (Spectrum)")
            with
                | :? Exception ->
                    failwith "INDEX On SpectrumID in PerSpectrumVariables table already exists, creation is skipped"

        interface IDisposable with

            member this.Dispose() =
                if isDisposed then ()
                else
                    if core <> null then core.Dispose()
                isDisposed <- true

        member this.Core  = core

        member this.Spectra = core.GetTable<BafSqlSpectrum>()

        member this.AcquisitionKeys = core.GetTable<BafSqlAcquisitionKey>()

        member this.PerSpectrumVariables = core.GetTable<BafSqlPerSpectrumVariable>()

        member this.SupportedVariables = core.GetTable<BafSqlSupportedVariable>()

        member this.Steps = core.GetTable<BafSqlStep>()

        //member this.GetBafSqlSpectrum(context:#DataContext, id:Nullable<UInt64>) =
            ////CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlSpectrum>().Where<BafSqlSpectrum>(fun x -> x.Id = id).SingleOrDefault<BafSqlSpectrum>()).Invoke(context, id)
            //CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlSpectrum>().AsEnumerable<BafSqlSpectrum>().Select(fun x -> x, x.Id=id)).Invoke(context, id)

        static member private GetNullableUInt (reader:SQLiteDataReader) n =

            if reader.IsDBNull(n) then 
                System.Nullable()
            else
                Nullable(Convert.ToUInt64(reader.GetInt64(n)))

        ///Prepare function to select all ProteinList-entities by FKMzQuantMLDocument as records.
        member this.PrepareGetBafSqlSpectrum (cn:SQLiteConnection) =
            Linq2BafSql.RaiseConnectionState(cn)
            let querystring = 
                "SELECT * FROM Spectra WHERE Id = @fk"
            let cmd = new SQLiteCommand(querystring, cn)
            cmd.Parameters.Add("@fk", Data.DbType.String) |> ignore
            let rec readerloop (reader:SQLiteDataReader) (acc) =
                match reader.Read() with
                | true  -> 
                    readerloop reader 
                        (new BafSqlSpectrum(
                            Linq2BafSql.GetNullableUInt reader 0, Nullable(reader.GetDouble(1)), Linq2BafSql.GetNullableUInt reader 2, 
                            Linq2BafSql.GetNullableUInt reader 3, Linq2BafSql.GetNullableUInt reader 4, Linq2BafSql.GetNullableUInt reader 5, 
                            Linq2BafSql.GetNullableUInt reader 6, Nullable(reader.GetDouble(7)), Nullable(reader.GetDouble(8)), 
                            Linq2BafSql.GetNullableUInt reader 9, Linq2BafSql.GetNullableUInt reader 10, Linq2BafSql.GetNullableUInt reader 11, 
                            Linq2BafSql.GetNullableUInt reader 12, Linq2BafSql.GetNullableUInt reader 13, Linq2BafSql.GetNullableUInt reader 14, 
                            Linq2BafSql.GetNullableUInt reader 15, Linq2BafSql.GetNullableUInt reader 16, Linq2BafSql.GetNullableUInt reader 17)
                        )
                | false -> acc 
            fun (fk:Nullable<UInt64>) ->
            cmd.Parameters.["@fk"].Value <- fk
            use reader = cmd.ExecuteReader()
            readerloop reader (new BafSqlSpectrum())

        member this.PrepareGetBafSqlAcquisitionKey (cn:SQLiteConnection) =
            Linq2BafSql.RaiseConnectionState(cn)
            //CompiledQuery.Compile(fun _ -> context.GetTable<BafSqlAcquisitionKey>().Where(fun x -> x.Id = id).SingleOrDefault())

            let querystring = 
                "SELECT * FROM AcquisitionKeys WHERE Id = @fk"
            let cmd = new SQLiteCommand(querystring, cn)
            cmd.Parameters.Add("@fk", Data.DbType.String) |> ignore
            let rec readerloop (reader:SQLiteDataReader) (acc:BafSqlAcquisitionKey) =
                match reader.Read() with
                | true  -> 
                    readerloop reader 
                        (new BafSqlAcquisitionKey(
                            Linq2BafSql.GetNullableUInt reader 0, Nullable(reader.GetInt64(1)), Nullable(reader.GetInt64(2)), 
                            Nullable(reader.GetInt64(3)), Nullable(reader.GetInt64(4)))
                        )
                | false -> acc 
            fun (fk:Nullable<UInt64>) ->
            cmd.Parameters.["@fk"].Value <- fk
            use reader = cmd.ExecuteReader()
            //reader.Dispose()
            readerloop reader (new BafSqlAcquisitionKey())

        member this.PrepareGetBafSqlSteps (cn:SQLiteConnection) =

            //CompiledQuery.Compile(fun _ -> context.GetTable<BafSqlStep>().Where(fun x -> x.TargetSpectrum = id))
            Linq2BafSql.RaiseConnectionState(cn)
            let querystring = 
                "SELECT * FROM Steps WHERE TargetSpectrum = @fk"
            let cmd = new SQLiteCommand(querystring, cn)
            cmd.Parameters.Add("@fk", Data.DbType.String) |> ignore
            let rec readerloop (reader:SQLiteDataReader) (acc:seq<BafSqlStep>) =
                seq
                    {
                        match reader.Read() with
                        | true  -> 
                            yield (new BafSqlStep(Linq2BafSql.GetNullableUInt reader 0, Nullable(reader.GetInt64(1)), Nullable(reader.GetInt64(2)), Nullable(reader.GetInt64(3)), Nullable(reader.GetInt64(4)), Nullable(reader.GetDouble(5))))
                            yield! readerloop reader acc
                                
                        | false -> yield! acc
                    }
                |> List.ofSeq
            fun (fk:Nullable<UInt64>) ->
            cmd.Parameters.["@fk"].Value <- fk
            use reader = cmd.ExecuteReader()
            //reader.Dispose()
            readerloop reader Seq.empty

        //member this.GetPerSpectrumVariables(context, id) =
        //    CompiledQuery.Compile(fun db id -> db.GetTable<BafSqlPerSpectrumVariable>().Where(fun x -> x.Spectrum = id).SingleOrDefault())

        static member private getDecimal(reader:SQLiteDataReader, n) =

            if reader.IsDBNull(n) then 
                System.Nullable() 
            else 
                Nullable(reader.GetDecimal(n))

        member this.PrepareGetPerSpectrumVariables(cn:SQLiteConnection) =

            //CompiledQuery.Compile(fun _ -> context.GetTable<BafSqlPerSpectrumVariable>().Where(fun x -> x.Spectrum.HasValue && x.Spectrum.Value = id))
            Linq2BafSql.RaiseConnectionState(cn)
            let querystring = 
                "SELECT * FROM PerSpectrumVariables WHERE Spectrum = @fk"
            let cmd = new SQLiteCommand(querystring, cn(*, tr*))
            cmd.Parameters.Add("@fk", Data.DbType.String) |> ignore
            let rec readerloop (reader:SQLiteDataReader) (acc:seq<BafSqlPerSpectrumVariable>) =
                seq
                    {
                        match reader.Read() with
                        | true  -> 
                            yield (new BafSqlPerSpectrumVariable(Linq2BafSql.GetNullableUInt reader 0, Linq2BafSql.GetNullableUInt reader 1, Linq2BafSql.getDecimal(reader, 2)))
                            yield! readerloop reader acc
                        | false -> yield! acc
                    }
                |> List.ofSeq
            fun (fk:UInt64) ->
            cmd.Parameters.["@fk"].Value <- fk
            use reader = cmd.ExecuteReader()
            //reader.Dispose()
            readerloop reader []

        static member RaiseConnectionState(cn:SQLiteConnection) =
            if (cn.State=ConnectionState.Open) then 
                ()
            else
                cn.Open()
