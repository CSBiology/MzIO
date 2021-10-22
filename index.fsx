(**
MzIO is a library that consists of a generic model which is based on the minimal amount of metadata, which different protein mass spectometry data formats share with each other. 
The model is built up of different classes, each with a different function. The MzIOModel holds the global metadata of the experiment, e.g. the with of the isolation window and 
is saved as a Json based shadow file. The runID of the MzIOModel links it with the MassSpectrum. 
The MassSpectrum is used to store the metadata of the different scans and is linked by its ID with the Peak1DArray. 
The Peak1DArray contains in addition to the inensity and m/z values the compression mode and the data format of the values, e.g. float 32 or float 64.


Samples & documentation
-----------------------

The library comes with comprehensible documentation. 
It can include tutorials automatically generated from `*.fsx` files in [the content folder][content]. 
The API reference is automatically generated from Markdown comments in the library implementation.

 * [Tutorial](tutorial.html) contains a further explanation of this sample library.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.
 
Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under Public Domain license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/fsprojects/MzIO/tree/master/docs/content
  [gh]: https://github.com/fsprojects/MzIO
  [issues]: https://github.com/fsprojects/MzIO/issues
  [readme]: https://github.com/fsprojects/MzIO/blob/master/README.md
  [license]: https://github.com/fsprojects/MzIO/blob/master/LICENSE.txt

*)