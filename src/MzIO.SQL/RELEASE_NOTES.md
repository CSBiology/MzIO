#### 0.1.4 - Friday, October 22, 2021
* Reference MzIO as nuget package

#### 0.1.3 - Tuesday, October 19, 2021
* Fix Dependency Versions

#### 0.1.2 - Tuesday, October 19, 2021
* Add cache size option to MzIO.SQL Reader

#### 0.1.1 - Thursday, May 6, 2021
* fix encoding errors in MzMLReader
* add MzMLReader function to retrieve Peaks with corresponding spectrum ID

#### 0.1.0 - Friday, February 25, 2021
* update buildchain
* unify dependency management
* create nuget prerelease

#### 0.0.18 - Friday, February 19, 2021
* Fix errors in MzIO model

#### 0.0.17 - Thursday, December 3, 2020
* Update lowest netframework verison to net47 

#### 0.0.16 - Wednesday, December 2, 2020
* Changed statement preparation, long running tasks now consume less Ram.  

#### 0.0.15 - Friday, Oktober 9, 2020
* Update build chain

#### 0.0.14 - Monday, October 5, 2020
* Add Target Framework "netstandard2.0" to the Wiff Filereader

#### 0.0.13 - Tuesday, Mai 12, 2020
* Replace 'null' in GetRTProfiles with option type

#### 0.0.12 - Wednesday, April 22, 2020
* Fix an error in the Numpress Linear Encoding

#### 0.0.11 - Sunday, April 12, 2020
* Improve ThermoRawFileReader
* Clean dependendies
* Add Targetframework netstandard2.0 to all combatible projects

#### 0.0.9.10 - Wednesday, February 12, 2020
* Fix a case where parameters were not saved correctly in the Isolation Window

#### 0.0.9.9 - Tuesday, February 11, 2020
* Add new functions to the SwathIndexer

#### 0.0.9.8 - Thursday, December 12, 2019
* Fix BuildRTIndex

#### 0.0.9.7 - Thursday, December 12, 2019
* Refactoring

#### 0.0.9.6 - Thursday, December 12, 2019
* Unification of scan time units

#### 0.0.9.5 - Wednesday, December 4, 2019
* Change clearcore license path to look under AppData/Local/IOMIQS/Clearcore2/Licensing
for the license

#### 0.0.9.4 - Tuesday, December 3, 2019
* Add new access to metadata and data for wiff files

#### 0.0.9.3 - Tuesday, October 9, 2019
* Fixed deserialize SelecteIon CvParams bug

#### 0.0.9.2 - Tuesday, October 9, 2019
* Fixed get PrecursorMz function

#### 0.0.9.1 - Tuesday, October 9, 2019
* Fixed bug with casting

#### 0.0.9 - Tuesday, October 9, 2019
* Changed namings

#### 0.0.8.2.1 - Tuesday, July 9, 2019
* Fixed getXICs

#### 0.0.8.2 - Tuesday, July 9, 2019
* Try remove NetStandard 2.0 from MzIO project

#### 0.0.8.1 - Tuesday, July 9, 2019
* Put SQLite transaction handling outsied the MzSQL class

#### 0.0.8 - Tuesday, July 9, 2019
* Reduze file size of MzSQL
* Improve speed of MzSQL accession and writing

#### 0.0.7 - Tuesday, July 9, 2019
* Add MzMLWriter
* Improve speed of MzMLReader

#### 0.0.6 - Tuesday, July 9, 2019
* Add Thermo-Reader

#### 0.0.5 - Tuesday, June 25, 2019
* Interface MzML-Reader with IMzIOReader
* Increase performance of the Bruker-Reader

#### 0.0.4 - Friday, June 21, 2019
* Add project for the MzML-Reader

#### 0.0.3 - Tuesday, June 18, 2019
* Add project for the Bruker-Reader
* Add project for the Thermo-Reader

#### 0.0.2 - Thursday, June 14, 2019
* Contains MzIO-Model
* Contains Binary decompressor and compressor for ZLib and NumPress
* Contains Wiff-Reader
* Contains MzML-Reader
* Contains CSV-Reader
* Contains SQL-Reader
* Contains Json-Reader with special JsonConverter for nested object of MassSpectrum, CvParams and UserParams
* Contains Functions to create Peak1D- and Peak2D-Arrays

#### 0.0.1 - Thursday, June 13, 2019
* Initial release