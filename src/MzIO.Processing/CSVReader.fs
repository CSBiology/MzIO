namespace MzIO.Processing

open System
open System.Collections.Generic
open System.Globalization
open System.IO
//open System.Threading.Tasks


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
            failwith ((new KeyNotFoundException(String.Format("Column: '{0}' not found in record at line number: {1}.", columnName, this.LineNumber))).ToString())

    member this.GetValueNotNullOrEmpty(columnName:string) =
        
        let mutable value = columnName
        match value with
        | null  -> failwith ((new FormatException(String.Format("Value in column: '{0}' in record at line number: {1} is empty.", columnName, this.LineNumber))).ToString())
        | ""    -> failwith ((new FormatException(String.Format("Value in column: '{0}' in record at line number: {1} is empty.", columnName, this.LineNumber))).ToString())
        | " "   -> failwith ((new FormatException(String.Format("Value in column: '{0}' in record at line number: {1} is empty.", columnName, this.LineNumber))).ToString())
        |   _   -> value

    /// <summary>
    /// Parse a bool value.
    /// </summary>        
    /// <returns>Returns true if value is 'trueValue', false if value is 'falseValue' or null.</returns>
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

    /// <summary>
    /// Parse a bool value.
    /// </summary>        
    /// <returns>Returns true if value is 'trueValue' or false.</returns>
    member this.GetBoolean(columnName:string, trueValue:string) =
        
        let mutable value = this.GetValue(columnName)
        value.Equals(trueValue, StringComparison.InvariantCultureIgnoreCase)

    /// <summary>
    /// Parse an double value.
    /// </summary>    
    member private this.ParseDouble(columnName:string, value:string) =

        try Convert.ToDouble(value, culture)

        with
            | :? FormatException ->
                failwith ((new FormatException(String.Format("Number format error in column: '{0}' at line number: {1}.", columnName, this.LineNumber))).ToString())

    /// <summary>
    /// Parse an double value.
    /// </summary>        
    /// <returns>Returns NaN if value is 'nanValue' or null if empty.</returns>
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

    /// <summary>
    /// Parse an double value.
    /// </summary> 
    member this.GetDouble(columnName:string) =

        let mutable value = this.GetValueNotNullOrEmpty(columnName)
        this.ParseDouble(columnName, value)

    /// <summary>
    /// Parse an double value list.
    /// </summary>        
    /// <returns></returns>
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

    /// <summary>
    /// Parse an int value.
    /// </summary>  
    member private this.ParseInt(columnName:string, value:string) =
        try
            Convert.ToInt32(value, culture);

        with
            | :? FormatException ->
                failwith ((new FormatException(String.Format("Number format error in column: '{0}' at line number: {1}.", columnName, this.LineNumber))).ToString())

    /// <summary>
    /// Parse an int value.
    /// </summary>        
    /// <returns>Returns null if empty.</returns>
    member this.getIntOrNull(columnName:string) =

        let mutable value = this.GetValue(columnName)
        match value with
        | null  -> None
        | ""    -> None
        | " "   -> None
        |   _   -> Some (this.ParseInt(columnName, value))

    /// <summary>
    /// Parse an int value.
    /// </summary> 
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
        match filePath with
        | null  -> failwith (ArgumentNullException("filePath").ToString())
        | ""    -> failwith (ArgumentNullException("filePath").ToString())
        | " "   -> failwith (ArgumentNullException("filePath").ToString())
        |   _   -> 
            if File.Exists(filePath) = false then failwith (FileNotFoundException("filePath").ToString())
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

    /// <summary>
    /// Read the column names from first line.
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    static member private ReadColumnHeaderIndex(reader:StreamReader, separator:char) =

        let columnIndex = new Dictionary<string, int>()

        // read column names
        let line = reader.ReadLine();

        if line = null then failwith (IOException("Unexpected end of file.").ToString())
        else
            let columns = line.Split(separator)
            for c = 0 to columns.Length-1 do
                if columnIndex.ContainsKey(columns.[c]) then failwith (IOException("Unexpected end of file.").ToString())
                else 
                    columnIndex.Item(columns.[c]) <- c
            columnIndex

    member this.GetTabReader(filePath:string) =
        new CSVReader(filePath, '\t', new CultureInfo("en-US"))

    /// <summary>
    /// Read the next record from stream.
    /// </summary>        
    /// <returns>Next record or null at EOF.</returns>
    member this.ReadNext() =
        
        if isDisposed = true then failwith (ObjectDisposedException("Can't read record at disposed reader.").ToString())
        else 
            try
                
                //lineNumber++;
                let line = reader.ReadLine()
                if line = null then None // EOF
                else
                    let values = line.Split(this.separator)
                    Some (new CSVRecord(columnIndex, lineNumber, culture, values))

            with
                | :? Exception ->
                    failwith ((new IOException(String.Format("Parse error at line {0}.", lineNumber))).ToString())

    /// <summary>
    /// Read all records.
    /// </summary>
    /// <returns></returns>
    member this.ReadAll() =
        seq
            {
                let mutable csvRec = this.ReadNext()
                while csvRec <> None do
                    yield csvRec
            }


    /// <summary>
    /// Read the next record from stream.
    /// </summary>        
    /// <returns>Next record or null at EOF.</returns>
    member this.ReadNextAsync() =
        async 
            {
                if isDisposed = true then failwith ((new ObjectDisposedException("Can't read record at disposed reader.")).ToString())
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
                        | :? Exception ->
                            failwith ((new IOException(String.Format("Parse error at line {0}.", lineNumber))).ToString())
            }
