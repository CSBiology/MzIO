namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("MzIO")>]
[<assembly: AssemblyProductAttribute("MzIO")>]
[<assembly: AssemblyDescriptionAttribute("A short summary of your project.")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
