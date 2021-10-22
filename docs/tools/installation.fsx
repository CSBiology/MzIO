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

* Install [.Net Core SDK](https://dotnet.microsoft.com/download)
* Install the dotnet tool fake cli by `dotnet tool install fake-cli -g` for global installation, or `dotnet tool install fake-cli --tool-path yourtoolpath`.
* Download the [RawFileReader nuget packages for the .net version](https://planetorbitrap.com/rawfilereader) from Thermo Fisher Scientific and place them in `lib/ThermoFisher`.
* Open the console and navigate to the root folder of the repository. Then run the command `fake build`

###Important notes:
####Wiff-Reader

* You have to run the projects in 32 bit mode for the Wiff-Reader to work properly.
* You need a Clearcore2 license for the Clearcore2 dlls used in this project.
    * A dummy file for the license which can be replaced is already located at `..\src\MzIO.Wiff\License`.

####Bruker-Reader

* You have to specify the platform on which the project runs, since bruker uses a different dll for 32 bit and 64 bit.
* You need to install [Visual C++ Redistributable for Visual Studio 2012 Update 4](https://www.microsoft.com/en-us/download/details.aspx?id=30679).


#Using the prerelease packages from the nuget branch

If you are using paket, add the following line to your `paket.dependencies` file:

`git https://github.com/CSBiology/MzIO.git nuget Packages: /`

you can then access the individual packages:

`nuget MzIO` <br>

`nuget MzIO.Processing` <br>

`nuget MzIO.Wiff` <br>

`nuget MzIO.Bruker` <br>

`nuget MzIO.Thermo` <br>

`nuget MzIO.MzML` <br>

###Important notes:

* The important notes for installing the binaries yourself apply to the nuget packages as well.
* For the package MzIO.Thermo you need to do the following steps if you are managing your dependencies with paket:
    * Download the [RawFileReader nuget packages for the .net version](https://planetorbitrap.com/rawfilereader) from Thermo Fisher Scientific and place them in `lib/ThermoFisher` or another folder of your choice
    * Add <br>
        `source lib/ThermoFisher` (source may vary based on your chosen location)<br>
         `nuget ThermoFisher.CommonCore.BackgroundSubtraction`<br>
         `nuget ThermoFisher.CommonCore.Data`<br>
         `nuget ThermoFisher.CommonCore.MassPrecisionEstimator`<br>
         `nuget ThermoFisher.CommonCore.RawFileReader`<br>
      to your paket.dependencies file
    * Run the command `paket update`, followed by `fake build`
*)
