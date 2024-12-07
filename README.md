# PACkager
Program that lets the user extract and create .PAC files used in games for the Tmr-Hiro ADV System (mostly Marigold produced games).

### Notes on its usage
1. If more than 1 file is selected, it will ***not*** report the version used for packing the file after completing the unpacking operation.
2. Due to GarBro's wonky implementation on detecting the extension of each file, GarBro is unable to detect the extensions on GRD files unless the file name has the word `grd`in it. The same logic somewhat applies to .srp files (the script files), but no to be worried, the files created with this program load in-game perfectly fine.

### How are PAC files structured?
While the code also documents how a .PAC file is structured, here it is also the same information on a more accessible manner:
The file is divided into 3 parts:
  * **Header**: Number of files (2 bytes) + Length of each file name (1 byte) + Raw data offset (4 bytes)
    * Number of files: each file is an entry, so we calculate how many of them the PAC file contains
    * File name length: each entry has its name (without the extension) listed in the index header
    * Raw data offset: the file specifies at what byte the raw data portion of the file starts
  * **Index**: File name (`Number of files` bytes) + Offset relative to `Raw data offset` (4 or 8 bytes) + File size in bytes (4 bytes)
    * File name: the name (without the extension) the file has
    * Offset: the location a file's raw data starts. The formula for determining where a file starts goes like this: `Header` (7 bytes) + Index header (File's position[^1] * (`File name length` + 4/8 + 4) bytes) + Offset
    * File size: the amount of bytes the file has
  * **Raw data**: its size cannot be determined without first looking at the index in itself.

#### What are the differences between each PAC version?
The offset for each file is set as an Int64 for version 2, instead of an Int32 in version 1. Besides that, there is nothing else different.

[^1]: It refers to the position a file in the index is located at. For example, if a file is listed in second place in the index, the value here it would be 2.
