using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace PACkager.Pac
{
    internal class Unpacker
    {
        private byte[] Data;

        private int NumberofFiles;
        private int LengthFileName;
        private int RawDataOffset;

        private string[] FileName = Array.Empty<string>();
        private long[] FileOffset = Array.Empty<long>();
        private int[] FileLength = Array.Empty<int>();

        private byte[] FileData = Array.Empty<byte>();
        private string[] FileExtension = Array.Empty<string>();

        public Unpacker(byte[] OriginalData)
        {
            Data = OriginalData;
        }

        //Function that analyzes the contents of the header of the file.
        //Said header follows this structure: Number of files (2 bytes) + Length of each file
        //name (1 byte) + Raw data offset (4 bytes)
        //Number of files: each file is an entry, so we calculate how many of them
        //the PAC file contains
        //File name length: each entry has its name (without the extension) listed in
        //the index header (goes in between the header obtained here and where the raw data
        //for each file starts)
        //Raw data offset: the file specifies at what byte the raw data portion of the file starts
        protected void GetHeader(byte[] OriginalFile)
        {
            byte[] Header = new byte[8];
            //Initialize the byte array so we can work with the contents of the header
            Header.Initialize();
            Buffer.BlockCopy(OriginalFile, 0, Header, 0, 8);

            //Now we obtain all of the values for everything the main header has the information about
            NumberofFiles = BitConverter.ToInt16(Header);
            LengthFileName = Header[2];
            RawDataOffset = BitConverter.ToInt32(Header, 3);
        }

        //Function that obtains the version of the PAC file.
        //Currently, there are two versions that are known, and they only have 1 difference,
        //the offset of where the raw data portion beginning is.
        protected int GetVersion()
        {
            //Calculate expected index size based on version 1
            int HeaderandIndexSize = 7 + NumberofFiles * (LengthFileName + 8);

            // Determine version based on raw data offset
            if (RawDataOffset == HeaderandIndexSize)
            {
                return 1;
            }
            else if (RawDataOffset == HeaderandIndexSize + 4 * NumberofFiles)
            {
                return 2;
            }

            return -1;
        }

        //Function that obtains the information for each of the files stored in the PAC file
        //according to the following structure: File name (LengthFileName bytes) + Offset relative
        //to RawDataOffset (4/8 bytes) + File size in bytes (4 bytes)
        //File name: the name (without the extension) the file has
        //Offset: the location a file's raw data starts. The formula is like this: Header (7 bytes)
        //+ Index header (CurrentEntry * (LengthFileName + 4/8 + 4) bytes) + Offset obtained here
        //File size: the amount of bytes the file has
        //For optimization purposes, we just trim out both headers from the file, so that way
        //operating with the raw data is far easier and faster
        protected void GetIndex(byte[] OriginalFile, int Version)
        {
            int RawIndexSize;

            switch (Version)
            {
                case 1:
                    RawIndexSize = NumberofFiles * (LengthFileName + 8);
                    break;
                case 2:
                    RawIndexSize = NumberofFiles * (LengthFileName + 8 + 4);
                    break;
                default:
                    throw new ArgumentException("Invalid PAC file version.");
            }
            byte[] RawIndex = new byte[RawIndexSize];
            RawIndex.Initialize();
            Buffer.BlockCopy(OriginalFile, 7, RawIndex, 0, RawIndexSize);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            FileName = new string[NumberofFiles];
            FileName.Initialize();
            FileOffset = new long[NumberofFiles];
            FileOffset.Initialize();
            FileLength = new int[NumberofFiles];
            FileLength.Initialize();

            //We go through each file to get its information
            for (int CurrentEntry = 0; CurrentEntry < NumberofFiles; CurrentEntry++)
            {
                if (Version == 1)
                {
                    FileName[CurrentEntry] = Encoding.GetEncoding("shift-jis").GetString(RawIndex, CurrentEntry * (LengthFileName + 8), LengthFileName);
                    FileName[CurrentEntry] = FileName[CurrentEntry].Trim();
                    FileName[CurrentEntry] = FileName[CurrentEntry].Replace("\0", "");
                    FileOffset[CurrentEntry] = BitConverter.ToInt32(RawIndex, CurrentEntry * (LengthFileName + 8) + LengthFileName);
                    FileLength[CurrentEntry] = BitConverter.ToInt32(RawIndex, CurrentEntry * (LengthFileName + 8) + LengthFileName + 4);
                }
                if (Version == 2)
                {
                    FileName[CurrentEntry] = Encoding.GetEncoding("shift-jis").GetString(RawIndex, CurrentEntry * (LengthFileName + 8 + 4), LengthFileName);
                    FileName[CurrentEntry] = FileName[CurrentEntry].Trim();
                    FileName[CurrentEntry] = FileName[CurrentEntry].Replace("\0", "");
                    FileOffset[CurrentEntry] = BitConverter.ToInt64(RawIndex, CurrentEntry * (LengthFileName + 8 + 4) + LengthFileName);
                    FileLength[CurrentEntry] = BitConverter.ToInt32(RawIndex, CurrentEntry * (LengthFileName + 8 + 4) + LengthFileName + 8);
                }
            }
        }

        //Function that obtains the extension of the files stored in the PAC file
        //since those aren't stored anywhere on the file, they have to be guessed
        //based on their headers/footers
        protected void GetExtensions(byte[] OriginalFile)
        {
            FileData = new byte[Data.Length - RawDataOffset];
            FileData.Initialize();
            Buffer.BlockCopy(Data, RawDataOffset, FileData, 0, FileData.Length);
            FileExtension = new string[NumberofFiles];
            FileExtension.Initialize();

            for (int CurrentEntry = 0; CurrentEntry < NumberofFiles; CurrentEntry++)
            {

                int e = BitConverter.ToInt32(FileData, (int)FileOffset[CurrentEntry] + 4);

                //First we evaluate if it is a SRP file
                if (BitConverter.ToInt16(FileData, (int)FileOffset[CurrentEntry] + FileLength[CurrentEntry] - 2).CompareTo(0x03) == 0 && BitConverter.ToInt16(FileData, (int)(FileOffset[CurrentEntry] + FileLength[CurrentEntry] - 4)).CompareTo(0x10) == 0)
                {
                    FileExtension[CurrentEntry] = ".srp";
                }

                //Now we evaluate if the file is a GRD image. The important bits to take into consideration
                //from the header are the following ones:
                //Index 0: 01 or 02 (File version)
                else if (FileData[FileOffset[CurrentEntry]] == 1 || FileData[FileOffset[CurrentEntry]] == 2)
                {
                    //Index 1: A1, A2 or 01
                    if (FileData[FileOffset[CurrentEntry] + 1].CompareTo(0xA1) == 0 || FileData[FileOffset[CurrentEntry] + 1].CompareTo(0xA2) == 0 || FileData[FileOffset[CurrentEntry] + 1] == 1)
                    {
                        //Index 5-6: 24 or 32 (Bit per pixel)
                        if (BitConverter.ToInt16(FileData, (int)FileOffset[CurrentEntry] + 6) == 24 || BitConverter.ToInt16(FileData, (int)FileOffset[CurrentEntry] + 6) == 32)
                        {
                            //If the mentioned indexes do coincide with the ones on the file, now we need to obtain
                            //the following parameters:
                            int ScreenWidth = BitConverter.ToInt16(FileData, (int)FileOffset[CurrentEntry] + 2);
                            int ScreenHeight = BitConverter.ToInt16(FileData, (int)FileOffset[CurrentEntry] + 4);
                            int Left = BitConverter.ToInt16(FileData, (int)FileOffset[CurrentEntry] + 8);
                            int Right = BitConverter.ToInt16(FileData, (int)FileOffset[CurrentEntry] + 10);
                            int Top = BitConverter.ToInt16(FileData, (int)FileOffset[CurrentEntry] + 12);
                            int Bottom = BitConverter.ToInt16(FileData, (int)FileOffset[CurrentEntry] + 14);
                            int AlphaChannelSize = BitConverter.ToInt32(FileData, (int)FileOffset[CurrentEntry] + 16);
                            int RedChannelSize = BitConverter.ToInt32(FileData, (int)FileOffset[CurrentEntry] + 20);
                            int GreenChannelSize = BitConverter.ToInt32(FileData, (int)FileOffset[CurrentEntry] + 24);
                            int BlueChannelSize = BitConverter.ToInt32(FileData, (int)FileOffset[CurrentEntry] + 28);

                            int ImageWidth = Right - Left;
                            int ImageHeight = Bottom - Top;

                            //Not mandatory, but useful for recreating a GRP image
                            int OffSetX = Left;
                            int OffSetY = ScreenHeight - Bottom;

                            if (32 + AlphaChannelSize + RedChannelSize + GreenChannelSize + BlueChannelSize == FileLength[CurrentEntry])
                            {
                                FileExtension[CurrentEntry] = ".grd";
                            }
                        }
                    }
                }

                //We check if the file is an .ogg by checking its signature
                else if (FileData[FileOffset[CurrentEntry]] == 0x4F && FileData[FileOffset[CurrentEntry] + 1] == 0x67 && FileData[FileOffset[CurrentEntry] + 2] == 0x67 && FileData[FileOffset[CurrentEntry] + 3] == 0x53)
                {
                    FileExtension[CurrentEntry] = ".ogg";
                }

                //Var files serve to give name to specific variables
                //They always end in "[END]"
                else if (BitConverter.ToInt32(FileData, (int)FileOffset[CurrentEntry] + FileLength[CurrentEntry] - 5).CompareTo(0x444E455B) == 0 && FileData[FileOffset[CurrentEntry] + FileLength[CurrentEntry] - 1] == 0x5D)
                {
                    FileExtension[CurrentEntry] = ".var";
                }

                //Custom WAV file, it has to follow these parameters:
                //Index 0: 0x44 (Custom signature)
                //Index 5-8: File size (in bytes) - 9 bytes
                //This format requires the file to be in this format: PCM, Stereo, 44.1KHz of sampling rate, 16 bits per sample
                else if (FileData[FileOffset[CurrentEntry]].CompareTo(0x44) == 0 && FileLength[CurrentEntry] - 9 == BitConverter.ToInt32(FileData, (int)FileOffset[CurrentEntry] + 5))
                {
                    FileExtension[CurrentEntry] = ".twav";
                }
            }
        }

        //Function that returns a byte array of the contents of the file specified
        protected byte[] CreateFile(long FileOffset, int FileLength, byte FileData)
        {
            byte[] NewData = new byte[FileLength];
            NewData.Initialize();
            Buffer.BlockCopy(this.FileData, (int)FileOffset, NewData, 0, FileLength);
            return NewData;
        }
        public void Convert(string NewFilePath, string NewFolderName)
        {
            GetHeader(Data);
            GetIndex(Data, GetVersion());
            GetExtensions(Data);

            for (int CurrentEntry = 0; CurrentEntry < NumberofFiles; CurrentEntry++)
            {
                //Check if there are multiple files selected to extract, if yes, we create
                //a folder automatically with the name of the PAC file to avoid mixing all
                //of the contents
                if (NewFolderName != string.Empty)
                {
                    if (Path.Exists(NewFilePath + "\\" + NewFolderName) == false)
                    {
                        Directory.CreateDirectory(NewFilePath + "\\" + NewFolderName);
                    }
                    File.WriteAllBytes(NewFilePath + "\\" + NewFolderName + "\\" + FileName[CurrentEntry] + FileExtension[CurrentEntry], CreateFile(FileOffset[CurrentEntry], FileLength[CurrentEntry], FileData[CurrentEntry]));
                }
                else
                {
                    File.WriteAllBytes(NewFilePath + "\\" + FileName[CurrentEntry] + FileExtension[CurrentEntry], CreateFile(FileOffset[CurrentEntry], FileLength[CurrentEntry], FileData[CurrentEntry]));
                }
            }

            //Inform the user that the process finalized successfully and the version used for the .PAC file
            if (NewFolderName == string.Empty)
            {
                MessageBox.Show($"Process completed successfully. If you need to know, this file is packaged " +
                        $"using version " + GetVersion() + " standards.", "Conversion completed.", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
