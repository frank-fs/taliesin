namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Taliesin")>]
[<assembly: AssemblyProductAttribute("Taliesin")>]
[<assembly: AssemblyDescriptionAttribute("Web application routing library using F# Agents")>]
[<assembly: AssemblyVersionAttribute("0.1.0")>]
[<assembly: AssemblyFileVersionAttribute("0.1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.1.0"
