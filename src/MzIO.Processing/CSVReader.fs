namespace MzIO.Processing

open System
open System.Collections.Generic
open System.Globalization
open System.IO


[<Sealed>]
type CSVRecord(columnIndexer:IDictionary<string, int>, lineNumber:int, culture:CultureInfo, values:string[]) =

    let mutable columnIndexer   = columnIndexer
    let mutable lineNumber      = lineNumber
    let mutable culture         = culture
    let mutable values          = values

    member this.LineNumber = lineNumber

    member this.GetValue(columnName:string) =

        let mutable idx = -1
        if columnIndexer.TryGetValue(columnName, & idx) then values.[idx]
        else
            raise (new KeyNotFoundException(String.Format("Column: '{0}' not found in record at line number: {1}.", columnName, this.LineNumber)))

    member this.GetValueNotNullOrEmpty(columnName:string) =
        
        let mutable value = columnName
        if String.IsNullOrWhiteSpace(value) then
            raise (new FormatException(String.Format("Value in column: '{0}' in record at line number: {1} is empty.", columnName, this.LineNumber)))
        else
            value

    /// Parse a bool value.        
    /// Returns true if value is 'trueValue', false if value is 'falseValue' or null.
    member this.GetBooleanOrNull(columnName:string, trueValue:string, falseValue:string) = 
        
        let mutable value = this.GetValue(columnName)

        match value with
        | null  -> None
        | ""    -> None
        | " "   -> None
        |   _   -> 
            if value.Equals(trueValue, StringComparison.InvariantCultureIgnoreCase) then Some true
            else 
                if (value.Equals(falseValue, StringComparison.InvariantCultureIgnoreCase)) then Some false
                else
                    None

    /// Parse a bool value.
    /// Returns true if value is 'trueValue' or false.
    member this.GetBoolean(columnName:string, trueValue:string) =
        
        let mutable value = this.GetValue(columnName)
        value.Equals(trueValue, StringComparison.InvariantCultureIgnoreCase)

    /// Parse an double value.    
    member private this.ParseDouble(columnName:string, value:string) =

        try Convert.ToDouble(value, culture)

        with
            | :? FormatException ->
                raise (new FormatException(String.Format("Number format error in column: '{0}' at line number: {1}.", columnName, this.LineNumber)))

    /// Parse an double value.
    /// Returns NaN if value is 'nanValue' or null if empty.
    member this.GetDoubleOrNull(columnName:string, nanValue:string) =
        
        let mutable value = this.GetValue(columnName)
        match value with
        | null  -> None
        | ""    -> None
        | " "   -> None
        |   _   -> 
            if (value.Equals(nanValue, StringComparison.InvariantCultureIgnoreCase)) then Some System.Double.NaN
            else 
                Some (this.ParseDouble(columnName, value))

    /// Parse an double value. 
    member this.GetDouble(columnName:string) =

        let mutable value = this.GetValueNotNullOrEmpty(columnName)
        this.ParseDouble(columnName, value)

    /// Parse an double value list.
    member this.GetDoubleArray(columnName:string, sep:char) =
        
        let mutable value = this.GetValue(columnName)
        match value with
        | null  -> Array.zeroCreate<float> 0
        | ""    -> Array.zeroCreate<float> 0
        | " "   -> Array.zeroCreate<float> 0
        |   _   -> 
            let mutable values = value.Split(sep)
            if values.Length = 0 then Array.zeroCreate<float> 0
            else 
                values
                |> Array.map (fun value -> this.ParseDouble(columnName, value))

    /// Parse an int value.  
    member private this.ParseInt(columnName:string, value:string) =
        try
            Convert.ToInt32(value, culture);

        with
            | :? FormatException ->
                raise (new FormatException(String.Format("Number format error in column: '{0}' at line number: {1}.", columnName, this.LineNumber)))

    /// Parse an int value.        
    /// Returns null if empty.
    member this.getIntOrNull(columnName:string) =

        let mutable value = this.GetValue(columnName)
        if String.IsNullOrWhiteSpace(value) then
            None
        else
            Some (this.ParseInt(columnName, value))

    /// Parse an int value.
    member this.GetInt(columnName:string) =

        let mutable value = this.GetValueNotNullOrEmpty(columnName)
        this.ParseInt(columnName, value)

    override this.ToString() =
    
        let mutable str = String.Empty

        for cn in columnIndexer.Keys do
            str + String.Format("{0}='{1}';", cn, this.GetValue(cn)) |> ignore

        str


[<Sealed>]
type CSVReader(filePath:string, separator:char, culture:CultureInfo) =

    //let mutable lineNumber = -1
    let mutable separator = separator
    let mutable isDisposed = false
    let mutable culture = new CultureInfo("en-US")

    let mutable check =
        if String.IsNullOrWhiteSpace(filePath) then
            raise (ArgumentNullException("filePath"))
        else
            if File.Exists(filePath) = false then 
                raise (FileNotFoundException("filePath"))
            else ()

    let fi = new FileInfo(filePath)
    let reader = fi.OpenText()

    let mutable columnIndex = CSVReader.ReadColumnHeaderIndex(reader, separator)
    let mutable lineNumber = 1

    interface IDisposable with
        member this.Dispose() =
         
            if isDisposed=false then
                reader.Close();                
                isDisposed <- true

    member private this.separator   = separator
    member private this.culture     = culture

    /// Read the column names from first line.
    static member private ReadColumnHeaderIndex(reader:StreamReader, separator:char) =

        let columnIndex = new Dictionary<string, int>()

        // read column names
        let line = reader.ReadLine();

        if line = null then 
            raise (IOException("Unexpected end of file."))
        else
            let columns = line.Split(separator)
            for c = 0 to columns.Length-1 do
                if columnIndex.ContainsKey(columns.[c]) then 
                    raise (IOException("Unexpected end of file."))
                else 
                    columnIndex.Item(columns.[c]) <- c
            columnIndex

    member this.GetTabReader(filePath:string) =
        new CSVReader(filePath, '\t', new CultureInfo("en-US"))

    /// Read the next record from stream.
    /// Next record or null at EOF.
    member this.ReadNext() =
        
        if isDisposed = true then 
            raise (ObjectDisposedException("Can't read record at disposed reader."))
        else 
            try
                
                //lineNumber++;
                let line = reader.ReadLine()
                if line = null then None // EOF
                else
                    let values = line.Split(this.separator)
                    Some (new CSVRecord(columnIndex, lineNumber, culture, values))

            with
                | :? Exception as ex ->
                    raise (new IOException(String.Format("Parse error at line {0}.", lineNumber, ex)))

    /// Read all records.
    member this.ReadAll() =
        seq
            {
                let mutable csvRec = this.ReadNext()
                while csvRec <> None do
                    yield csvRec
            }


    /// Read the next record from stream.
    /// Next record or null at EOF.
    member this.ReadNextAsync() =
        async 
            {
                if isDisposed = true then 
                    raise (new ObjectDisposedException("Can't read record at disposed reader."))
                else
            
                    try
                        let mutable line = 
                            reader.ReadLineAsync()(*.ConfigureAwait(false)*)
                            |> Async.AwaitTask
                            |> string
                        if line = null then None // EOF
                        else
                            let mutable values = line.Split(this.separator)
                            Some (new CSVRecord(columnIndex, lineNumber, culture, values))
                    with
                        | :? Exception as ex ->
                            raise (new IOException(String.Format("Parse error at line {0}.", lineNumber, ex)))
            }
