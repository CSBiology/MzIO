(**
# Installation Guide

MzIO contains 7 different projects. 3 of those projects are essential for most practical applications.
Those would be MzIO (basic functions), MzIO.Processing (data processing functions)
and MzIO.SQL (functions to interact with the SQLite database). The other 4 projects are for 4 different
file formats. Currently supported formats are Wiff (wiff), Bruker (baf) , Thermo Fisher (raw) and MzML (xml).
The readers for those formats can be installed individually.

Currently, the nuget packages are only available at our github [nuget branch](https://github.com/CSBiology/MzIO/tree/nuget). At a later point they will be released at [nuget.org](www.nuget.org).
You can either install them from there or build the binaries yourself.

#Building the binaries yourself

First steps:

* Install [.Net Core SDK](https://dotnet.microsoft.com/download)
* Install the dotnet tool fake cli by `dotnet tool install fake-cli -g` for global installation, or `dotnet tool install fake-cli --tool-path yourtoolpath`

##Wiff

To install the Wiff-Reader, you have to open the console and navigate to the folder of the repository.
Then you run the command:

`fake build -t Wiff`

This command builds the projects MzIO, MzIO.Processing, MzIO.SQL and MzIO.Wiff.

Important notes:

* You have to run the projects in 32 bit mode for the Wiff-Reader to work properly
* You need a Clearcore2 license for the Clearcore2 dlls used in this project
    * A dummy file for the license which can be replaced is already located at the correct position in the project

##Bruker

To install the Bruker-Reader, you have to open the console and navigate to the folder of the repository.
Then you run the command:

`fake build -t Bruker`

This command builds the projects MzIO, MzIO.Processing, MzIO.SQL and MzIO.Bruker.

Important notes:

* You have to specify the platform on which the project runs, since bruker uses a different dll for 32 bit and 64 bit.
* You need to install [Visual C++ Redistributable for Visual Studio 2012 Update 4](https://www.microsoft.com/en-us/download/details.aspx?id=30679)

##Thermo Fisher

To install the ThermoFisher-Reader, you have to open the console and navigate to the folder of the repository.
Then you run the command:

`fake build -t Thermo`

This command builds the projects MzIO, MzIO.Processing, MzIO.SQL and MzIO.Thermo.

Important notes:

* You need the [RawFileReader nuget packages for the .net version](https://planetorbitrap.com/rawfilereader) from Thermo Fisher Scientific

##MzML

To install the MzML-Reader, you have to open the console and navigate to the folder of the repository.
Then you run the command:

`fake build -t MzML`

This command builds the projects MzIO, MzIO.Processing, MzIO.SQL and MzIO.MzML.

##Other

All projects can be built at once if desired. The command for that is:

`fake build`

If you don't possess the dlls for the Thermo-Reader, this command end in an error due to missing dlls.

#Using the prerelease packages from the nuget branch

If you are using paket, add the following line to your `paket.dependencies` file:

`git https://github.com/CSBiology/BioFSharp.git nuget Packages: /`

you can then access the individual packages:

`nuget MzIO` <br>

`nuget MzIO.Processing` <br>

`nuget MzIO.Wiff` <br>

`nuget MzIO.Bruker` <br>

`nuget MzIO.Thermo` <br>

`nuget MzIO.MzML` <br>

Note: The important note for installing the binaries yourself apply to the nuget packages as well.
*)
