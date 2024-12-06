using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PACkager.Pac
{
    internal class Packer
    {
        private string[] FilePath;
        int Version;

        private int NumberofFiles;
        private int LengthFileName;
        private int RawDataOffset;

        private string[] FileName = Array.Empty<string>();
        private long[] FileOffset = Array.Empty<long>();
        private int[] FileLength = Array.Empty<int>();

        private byte[][] FileData;

        public Packer(string[] OriginalFilePaths, int Version)
        {
            FilePath = OriginalFilePaths;
            NumberofFiles = FilePath.Length;
            this.Version = Version;

            //Initialize all of the arrays with the corresponding size
            FileName = new string[NumberofFiles];
            FileLength = new int[NumberofFiles];
            FileOffset = new long[NumberofFiles];
            FileData = new byte[NumberofFiles][];

            for (int CurrentFile = 0; CurrentFile < NumberofFiles; CurrentFile++)
            {
                FileName[CurrentFile] = Path.GetFileNameWithoutExtension(FilePath[CurrentFile]);
                LengthFileName = Math.Max(LengthFileName, FileName[CurrentFile].Length);
                FileLength[CurrentFile] = (int)new FileInfo(FilePath[CurrentFile]).Length;
                
                if (CurrentFile == 0)
                {
                    FileOffset[CurrentFile] = 0;
                }
                else
                {
                    FileOffset[CurrentFile] = FileOffset[CurrentFile - 1] + FileLength[CurrentFile - 1];
                }

                //Obtain all of the raw data from each file so we can store it inside an array
                using (FileStream fs = new FileStream(FilePath[CurrentFile], FileMode.Open, FileAccess.Read))
                {
                    FileData[CurrentFile] = new byte[FileLength[CurrentFile]];
                    fs.Read(FileData[CurrentFile], 0, FileLength[CurrentFile]);
                }

            }
            switch (Version)
            {
                case 1:
                    RawDataOffset = 7 + NumberofFiles * (LengthFileName + 8);
                    break;
                case 2:
                    RawDataOffset = 7 + NumberofFiles * (LengthFileName + 8 + 4);
                    break;
            }
        }

        //Function that creates the contents of the header of the file.
        protected byte[] CreateHeader()
        {
            byte[] Header = new byte[7];
            Header.Initialize();

            Buffer.BlockCopy(BitConverter.GetBytes(NumberofFiles), 0, Header, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(LengthFileName), 0, Header, 2, 1);
            Buffer.BlockCopy(BitConverter.GetBytes(RawDataOffset), 0, Header, 3, 4);
            return Header;
        }
        
        //Function that creates the contents of the index of the file
        protected byte[] CreateIndex(int Version)
        {
            int IndexSize;
            switch (Version)
            {
                case 1:
                    IndexSize = NumberofFiles * (LengthFileName + 8);
                    break;
                case 2:
                    IndexSize = NumberofFiles * (LengthFileName + 8 + 4);
                    break;
                default:
                    throw new ArgumentException("Invalid PAC file version.");
            }

            byte[] Index = new byte[IndexSize];
            Index.Initialize();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            int LastOffsetUsed = 0;

            for (int CurrentFile = 0; CurrentFile < NumberofFiles; CurrentFile++)
            {
                int LengthCurrentFileName = Encoding.GetEncoding("shift-jis").GetBytes(FileName[CurrentFile]).Length;
                
                //If the file name is shorter than the length each file name has in the index,
                //we fill the remaining bytes with 0xFF bytes
                if (LengthCurrentFileName < LengthFileName)
                {
                    byte[] FileNameBytes = new byte[LengthFileName];
                    Array.Fill(FileNameBytes, (byte)0x00);
                    Buffer.BlockCopy(Encoding.GetEncoding("shift-jis").GetBytes(FileName[CurrentFile]), 0, FileNameBytes, 0, LengthCurrentFileName);
                    Buffer.BlockCopy(FileNameBytes, 0, Index, LastOffsetUsed, LengthFileName);
                }
                else
                {
                    Buffer.BlockCopy(Encoding.GetEncoding("shift-jis").GetBytes(FileName[CurrentFile]), 0, Index, LastOffsetUsed, LengthFileName);
                }

                LastOffsetUsed = LastOffsetUsed + LengthFileName;

                switch (Version)
                {
                    case 1:
                        Buffer.BlockCopy(BitConverter.GetBytes(FileOffset[CurrentFile]), 0, Index, LastOffsetUsed, 4);
                        LastOffsetUsed = LastOffsetUsed + 4;
                        break;
                    case 2:
                        Buffer.BlockCopy(BitConverter.GetBytes(FileOffset[CurrentFile]), 0, Index, LastOffsetUsed, 8);
                        LastOffsetUsed = LastOffsetUsed + 8;
                        break;
                }

                Buffer.BlockCopy(BitConverter.GetBytes(FileLength[CurrentFile]), 0, Index, LastOffsetUsed, 4);
                LastOffsetUsed = LastOffsetUsed + 4;
            }
            return Index;
        }

        //Function that obtains the binary data of each of the files that are going to be
        //stored inside the PAC file
        protected byte[] CreateRawData()
        {
            //First we obtain the size of the byte array
            int RawDataSize = 0;
            for (int CurrentFile = 0; CurrentFile < NumberofFiles; CurrentFile++)
            {
                RawDataSize = RawDataSize + FileLength[CurrentFile];
            }
            byte[] RawData = new byte[RawDataSize];

            //Once the array has been declared, we copy all of the files onto the array
            for (int CurrentFile = 0; CurrentFile < NumberofFiles; CurrentFile++)
            {
                Buffer.BlockCopy(FileData[CurrentFile], 0, RawData, (int)FileOffset[CurrentFile], FileLength[CurrentFile]);
            }

            return RawData;
        }
        public void Convert(string NewFilePath)
        {
            byte[] Header = CreateHeader();
            byte[] Index = CreateIndex(Version);
            byte[] RawData = CreateRawData();

            byte[] NewFileData = new byte[Header.Length + Index.Length + RawData.Length];
            Buffer.BlockCopy(Header, 0, NewFileData, 0, Header.Length);
            Buffer.BlockCopy(Index, 0, NewFileData, Header.Length, Index.Length);
            Buffer.BlockCopy(RawData, 0, NewFileData, Header.Length + Index.Length, RawData.Length);
            File.WriteAllBytes(NewFilePath, NewFileData);
            MessageBox.Show($"Process completed successfully.", "Conversion completed.", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
