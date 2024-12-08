using Microsoft.Win32;
using PACkager.Pac;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PACkager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        String[] FilePaths = Array.Empty<string>();
        String[] FileNames = Array.Empty<string>();

        private void Button_Original_File_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.FileName = "";
            ofd.Multiselect = true;
            ofd.Filter = "Package file (*.pac)|*.pac|Scene file (*.srp)|*.srp|Variables file (*.var)|*.var|All files (*.*)|*.*";
            ofd.FilterIndex = ofd.Filter.Length;
            Nullable<bool> result = ofd.ShowDialog();

            if (result == true)
            {
                try
                {
                    //Copy the values for the selected files to an array in order to manage the
                    //files later on according to their extension
                    FilePaths = (string[])ofd.FileNames.Clone();
                    FileNames = new string[FilePaths.Length];
                    for (int CurrentFile = 0; CurrentFile < ofd.FileNames.Length; CurrentFile++)
                    {
                        FileNames[CurrentFile] = System.IO.Path.GetFileNameWithoutExtension(ofd.FileNames[CurrentFile]);
                    }

                    Button_Unpack.IsChecked = false;
                    Button_Pack.IsChecked = false;

                    Button_Unpack.IsEnabled = true;
                    Button_Pack.IsEnabled = true;

                    Button_Convert.IsEnabled = false;

                    for (int CurrentFile = 0; CurrentFile < FilePaths.Length; CurrentFile++)
                    {
                        string OriginalFileExtension = System.IO.Path.GetExtension(FilePaths[CurrentFile]);

                        //Check to see if the file is one that is already a compressed one
                        if (OriginalFileExtension != ".pac")
                        {
                            MessageBox.Show($"At least one selected file cannot be decompressed, if the option to unpack is selected" +
                                $" the conflicting files will not be processed.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                            break;
                        }
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Button_Convert_Click(object sender, RoutedEventArgs e)
        {
            if (Button_Unpack.IsChecked == true)
            {
                OpenFolderDialog ofd = new OpenFolderDialog();
                Nullable<bool> result = ofd.ShowDialog();

                if (result == true)
                {
                    try
                    {
                        for (int CurrentFile = 0; CurrentFile < FilePaths.Length; CurrentFile++)
                        {
                            //Check if the file is a PAC file, otherwise we skip it
                            if (System.IO.Path.GetExtension(FilePaths[CurrentFile]) == ".pac")
                            {
                                byte[] Data = File.ReadAllBytes(FilePaths[CurrentFile]);
                                Unpacker PacUnpacker = new Unpacker(Data);
                                if (FilePaths.Length == 1)
                                {
                                    PacUnpacker.Convert(ofd.FolderName, string.Empty);
                                }
                                //We pass the name of the file in order to create a folder for its
                                //contents when we have selected more than 1 file to extract
                                else
                                {
                                    PacUnpacker.Convert(ofd.FolderName, FileNames[CurrentFile]);
                                }
                                Data.Initialize();
                            }
                        }

                        //Reports the user that the process has finalized correctly (but only through this way
                        //if the user had multiple files to process, in order to avoid showing 1 message per file)
                        if (FilePaths.Length != 0)
                        {
                            MessageBox.Show($"Process completed successfully.", "Conversion completed.", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            if (Button_Pack.IsChecked == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Package file (*.pac)|*.pac|All files (*.*)|*.*";
                sfd.DefaultExt = ".pac";
                Nullable<bool> result = sfd.ShowDialog();

                if (result == true)
                {
                    try
                    {
                        int versionpac = -1;
                        var versionpacquestion = MessageBox.Show("Do you want to create a version 2 PAC file? (In case of doubt, " +
                            "check any original .pac file you may have from the game you are trying to modify with this program to " +
                            "know what version you need to choose.)", "Question",
                            MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (versionpacquestion == MessageBoxResult.Yes)
                        {
                            versionpac = 2;
                        }
                        else if (versionpacquestion == MessageBoxResult.No)
                        {
                            versionpac = 1;
                        }
                        Packer PacPacker = new Packer(FilePaths, versionpac);
                        PacPacker.Convert(sfd.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            
        }

        private void Button_Unpack_Checked(object sender, RoutedEventArgs e)
        {
            Button_Convert.IsEnabled = false;

            //Check if there are any pac files selected, so that way we know if
            //we can enable the convert button or not
            for (int CurrentFile = 0; CurrentFile < FilePaths.Length; CurrentFile++)
            {
                string OriginalFileExtension = System.IO.Path.GetExtension(FilePaths[CurrentFile]);

                //We see that there is a pac file selected, so we enable the button
                if (OriginalFileExtension == ".pac")
                {
                    Button_Convert.IsEnabled = true;
                    break;
                }
            }
        }

        private void Button_Pack_Checked(object sender, RoutedEventArgs e)
        {
            if (Button_Convert.IsEnabled == false)
            {
                Button_Convert.IsEnabled = true;
            }
        }
    }
}