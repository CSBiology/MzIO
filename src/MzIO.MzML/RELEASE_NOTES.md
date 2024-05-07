### 0.1.6 - (Released 2023/06/13)
* Change FSharp.Core version back to 5.0.0

### 0.1.5 - (Released 2023/06/13)
* Add MzML MIRIM Reader

### 0.1.4 - (Released 2021/10/22)
* Reference MzIO and MzIO.Processing as nuget package

### 0.1.3 - (Released 2021/10/19)
* Fix Dependency Versions

### 0.1.2 - (Released 2021/10/19)
* Add option to read spectra sequentially in MzMLReader, improving speed

### 0.1.1 - (Released 2021/05/06)
* fix encoding errors in MzMLReader
* add MzMLReader function to retrieve Peaks with corresponding spectrum ID

### 0.1.0 - (Released 2021/02/25)
* update buildchain
* unify dependency management
* create nuget prerelease

### 0.0.18 - (Released 2021/02/19)
* Fix errors in MzIO model

### 0.0.17 - (Released 2020/12/03)
* Update lowest netframework verison to net47 

### 0.0.16 - (Released 2020/12/02)
* Changed statement preparation, long running tasks now consume less Ram.  

### 0.0.15 - (Released 2020/10/09)
* Update build chain

### 0.0.14 - (Released 2020/10/05)
* Add Target Framework "netstandard2.0" to the Wiff Filereader

### 0.0.13 - (Released 2020/05/12)
* Replace 'null' in GetRTProfiles with option type

### 0.0.12 - (Released 2020/04/22)
* Fix an error in the Numpress Linear Encoding

### 0.0.11 - (Released 2020/04/12)
* Improve ThermoRawFileReader
* Clean dependendies
* Add Targetframework netstandard2.0 to all combatible projects

### 0.0.9.10 - (Released 2020/02/12)
* Fix a case where parameters were not saved correctly in the Isolation Window

### 0.0.9.9 - (Released 2020/02/11)
* Add new functions to the SwathIndexer

### 0.0.9.8 - (Released 2019/12/12)
* Fix BuildRTIndex

### 0.0.9.7 - (Released 2019/12/12)
* Refactoring

### 0.0.9.6 - (Released 2019/12/12)
* Unification of scan time units

### 0.0.9.5 - (Released 2019/12/04)
* Change clearcore license path to look under AppData/Local/IOMIQS/Clearcore2/Licensing
for the license

### 0.0.9.4 - (Released 2019/12/03)
* Add new access to metadata and data for wiff files

### 0.0.9.3 - (Released 2019/10/09)
* Fixed deserialize SelecteIon CvParams bug

### 0.0.9.2 - (Released 2019/10/09)
* Fixed get PrecursorMz function

### 0.0.9.1 - (Released 2019/10/09)
* Fixed bug with casting

### 0.0.9 - (Released 2019/10/09)
* Changed namings

### 0.0.8.2.1 - (Released 2019/07/09)
* Fixed getXICs

### 0.0.8.2 - (Released 2019/07/09)
* Try remove NetStandard 2.0 from MzIO project

### 0.0.8.1 - (Released 2019/07/09)
* Put SQLite transaction handling outsied the MzSQL class

### 0.0.8 - (Released 2019/07/09)
* Reduze file size of MzSQL
* Improve speed of MzSQL accession and writing

### 0.0.7 - (Released 2019/07/09)
* Add MzMLWriter
* Improve speed of MzMLReader

### 0.0.6 - (Released 2019/07/09)
* Add Thermo-Reader

### 0.0.5 - (Released 2019/06/25)
* Interface MzML-Reader with IMzIOReader
* Increase performance of the Bruker-Reader

### 0.0.4 - (Released 2019/06/21)
* Add project for the MzML-Reader

### 0.0.3 - (Released 2019/06/18)
* Add project for the Bruker-Reader
* Add project for the Thermo-Reader

### 0.0.2 - (Released 2019/06/14)
* Contains MzIO-Model
* Contains Binary decompressor and compressor for ZLib and NumPress
* Contains Wiff-Reader
* Contains MzML-Reader
* Contains CSV-Reader
* Contains SQL-Reader
* Contains Json-Reader with special JsonConverter for nested object of MassSpectrum, CvParams and UserParams
* Contains Functions to create Peak1D- and Peak2D-Arrays

### 0.0.1 - (Released 2019/06/13)
* Initial release