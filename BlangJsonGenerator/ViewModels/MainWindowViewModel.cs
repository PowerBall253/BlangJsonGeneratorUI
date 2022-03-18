using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using BlangParser;

namespace BlangJsonGenerator.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        // Currently opened blang file
        public BlangFile? BlangFile;

        // Blang strings view for data grid
        private DataGridCollectionView? _blangStringsView;

        public DataGridCollectionView? BlangStringsView
        {
            get => _blangStringsView;
            set => this.RaiseAndSetIfChanged(ref _blangStringsView, value);
        }

        // App title
        private string _appTitle = "BlangJsonGenerator";

        public string AppTitle
        {
            get => _appTitle;
            set => this.RaiseAndSetIfChanged(ref _appTitle, value);
        }

        // If true, there's at least one modified blang string
        private bool _anyModified = false;

        public bool AnyModified
        {
            get => _anyModified;
            set => this.RaiseAndSetIfChanged(ref _anyModified, value);
        }

        // If ture, there is a blang file loaded
        private bool _isBlangLoaded = false;

        public bool IsBlangLoaded
        {
            get => _isBlangLoaded;
            set => this.RaiseAndSetIfChanged(ref _isBlangLoaded, value);
        }

        // If true, there are unsaved changes
        public bool UnsavedChanges = false;

        // Name of the language we're editing
        private string _blangLanguage = "";

        // If true, the program is not running on macOS
        public static bool IsNotMacOs
        {
            get => !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }

        // Index to set for next new added string
        private int _newStringIndex = 0;

        // Search bar filter for data grid
        private string _stringFilter = "";

        // If false, search box needs to be initialized
        private bool _isSearchBoxInit;

        // Open file dialog and return selected file
        private static async Task<string> OpenFileDialog(string title, string extension)
        {
            // Open file dialog
            var fileDialog = new OpenFileDialog()
            {
                Title = title,
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>()
                {
                    new FileDialogFilter()
                    {
                        Extensions = new List<string>() { extension }
                    }
                }
            };

            // Get selected file
            string[]? results = await fileDialog.ShowAsync((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!);

            if (results == null || results.Length == 0)
            {
                return "";
            }

            return results.FirstOrDefault(str => !string.IsNullOrEmpty(str))!;
        }

        // Load blang files from .resources file
        public Dictionary<string, byte[]>? LoadResourcesFile(string filePath)
        {
            // Get all blang files in .resources file
            var blangFiles = new Dictionary<string, byte[]>();

            try
            {
                // Open file
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var binaryReader = new BinaryReader(fileStream);

                // Check magic
                if (!binaryReader.ReadBytes(4).SequenceEqual(new byte[] {0x49, 0x44, 0x43, 0x4C}))
                {
                    return null;
                }

                // Read resource data
                fileStream.Seek(28, SeekOrigin.Current);
                uint fileCount = binaryReader.ReadUInt32();

                fileStream.Seek(4, SeekOrigin.Current);
                uint dummyCount = binaryReader.ReadUInt32();

                // Get offsets
                fileStream.Seek(20, SeekOrigin.Current);
                ulong namesOffset = binaryReader.ReadUInt64();

                fileStream.Seek(8, SeekOrigin.Current);
                ulong infoOffset = binaryReader.ReadUInt64();

                fileStream.Seek(8, SeekOrigin.Current);
                ulong dummyOffset = binaryReader.ReadUInt64() + dummyCount * 4;

                fileStream.Seek((long)namesOffset, SeekOrigin.Begin);

                // Get filenames for exporting
                ulong nameCount = binaryReader.ReadUInt64();
                var names = new List<string>((int)nameCount);
                var nameChars = new List<byte>(512);

                var currentPosition = fileStream.Position;

                for (ulong i = 0; i < nameCount; i++)
                {
                    fileStream.Seek(currentPosition + (long)i * 8, SeekOrigin.Begin);
                    ulong currentNameOffset = binaryReader.ReadUInt64();
                    fileStream.Seek((long)(namesOffset + nameCount * 8 + currentNameOffset + 8),
                        SeekOrigin.Begin);

                    while (binaryReader.PeekChar() != 0)
                    {
                        nameChars.Add(binaryReader.ReadByte());
                    }

                    string name = Encoding.UTF8.GetString(nameChars.ToArray());
                    names.Add(name);

                    nameChars.Clear();
                }

                fileStream.Seek((long)infoOffset, SeekOrigin.Begin);

                // Extract .blang files
                for (uint i = 0; i < fileCount; i++)
                {
                    // Read file info for extracting
                    fileStream.Seek(32, SeekOrigin.Current);
                    ulong nameIdOffset = binaryReader.ReadUInt64();

                    fileStream.Seek(16, SeekOrigin.Current);
                    ulong offset = binaryReader.ReadUInt64();
                    ulong zSize = binaryReader.ReadUInt64();
                    ulong size = binaryReader.ReadUInt64();

                    nameIdOffset = (nameIdOffset + 1) * 8 + dummyOffset;
                    currentPosition = fileStream.Position + 64;

                    // If the file is oodle compressed, continue
                    if (size != zSize)
                    {
                        fileStream.Seek(currentPosition, SeekOrigin.Begin);
                        continue;
                    }

                    fileStream.Seek((long)nameIdOffset, SeekOrigin.Begin);
                    ulong nameId = binaryReader.ReadUInt64();
                    string name = names[(int)nameId];

                    // Filter out non-blang files
                    if (!name.EndsWith(".blang"))
                    {
                        fileStream.Seek(currentPosition, SeekOrigin.Begin);
                        continue;
                    }

                    // Read blang bytes
                    fileStream.Seek((long)offset, SeekOrigin.Begin);
                    var blangBytes = binaryReader.ReadBytes((int)size);
                    blangFiles.Add(name[8..^0], blangBytes);

                    // Seek back to read next file
                    fileStream.Seek(currentPosition, SeekOrigin.Begin);
                }
            }
            catch
            {
                return null;
            }

            return blangFiles;
        }

        // Open and read the given blang file from memory
        public bool LoadBlangFile(byte[]? blangBytes, string language = "new")
        {
            // Reset values
            BlangStringsView = null;
            BlangFile = null;
            UnsavedChanges = false;
            AnyModified = false;

            if (blangBytes != null)
            {
                // Parse blang file
                try
                {
                    var decryptedBlangFile = BlangDecrypt.IdCrypt(blangBytes, $"strings/{language}.blang", true)!;
                    BlangFile = BlangFile.ParseFromMemory(decryptedBlangFile);
                }
                catch
                {
                    try
                    {
                        using var memoryStream = new MemoryStream(blangBytes);
                        BlangFile = BlangFile.ParseFromMemory(memoryStream);
                    }
                    catch
                    {
                        return false;
                    }
                }

                if (BlangFile == null || BlangFile.Strings.Count == 0)
                {
                    return false;
                }
            }
            else
            {
                // Init new blang file
                BlangFile = new BlangFile()
                {
                    UnknownData = 0,
                    Strings = new List<BlangString>()
                    {
                        new BlangString(0, $"#new_string_{_newStringIndex}", $"#new_string_{_newStringIndex}", "", "", "", false)
                    }
                };

                _newStringIndex += 1;
            }

            // Set language
            _blangLanguage = language;

            // Set blang loaded to true
            IsBlangLoaded = true;

            // Initialize blang strings view for data grid
            BlangStringsView = new DataGridCollectionView(BlangFile.Strings);

            // Deselect selected row
            var stringGrid = (Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.FindControl<DataGrid>("StringGrid")!;
            stringGrid.SelectedItem = null;

            // Initialize filtering system for search bar
            _stringFilter = "";
            var searchBox = (Application.Current.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.FindControl<TextBox>("SearchBox")!;
            searchBox.IsEnabled = true;
            searchBox.Text = "";
            BlangStringsView.Filter = bs => ((BlangString)bs).Identifier.Contains(_stringFilter) || ((BlangString)bs).Text.Contains(_stringFilter);

            // Init search bar
            if (!_isSearchBoxInit)
            {
                _isSearchBoxInit = true;
                searchBox.GetObservable(TextBox.TextProperty).Subscribe(text =>
                {
                    _stringFilter = text!;
                    BlangStringsView.Refresh();
                    stringGrid.SelectedItem = null;
                });
            }

            // Init search button
            var addButton = (Application.Current.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.FindControl<Button>("AddButton")!;
            addButton.IsEnabled = true;

            // Add filename to app title
            string fileName = blangBytes == null ? "New file" : _blangLanguage + ".blang";
            AppTitle = $"BlangJsonGenerator - {fileName}";

            return true;
        }

        // Load JSON changes into grid
        public bool LoadJson(string filePath)
        {
            // Read and serialize JSON
            BlangJson blangJson;

            try
            {
                var blangJsonString = File.ReadAllText(filePath);
                blangJson = JsonSerializer.Deserialize<BlangJson>(blangJsonString, new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                })!;

                if (blangJson == null || blangJson.Strings == null)
                {
                    throw new Exception();
                }
            }
            catch
            {
                return false;
            }

            // Assign changes from JSON
            AnyModified = false;

            foreach (var blangJsonString in blangJson.Strings)
            {
                // Find equivalent string in BlangFile.Strings
                bool found = false;

                foreach (var blangString in BlangFile!.Strings)
                {
                    if (blangString.Identifier.Equals(blangJsonString.Name))
                    {
                        found = true;
                        blangString.Text = blangJsonString.Text;

                        if (!blangString.Identifier.Equals(blangString.OriginalIdentifier) || !blangString.Text.Equals(blangString.OriginalText))
                        {
                            blangString.Modified = true;

                            // Modified string, set any modified to true
                            AnyModified = true;
                        }
                        else
                        {
                            blangString.Modified = false;
                        }

                        break;
                    }
                }

                if (!found)
                {
                    BlangFile.Strings.Add(new BlangString(0, blangJsonString.Name, blangJsonString.Name, blangJsonString.Text, blangJsonString.Text, "", true));
                }
            }

            // Check if there's any modified string
            if (!AnyModified)
            {
                foreach (var blangString in BlangFile!.Strings)
                {
                    if (blangString.Modified)
                    {
                        AnyModified = true;
                        break;
                    }
                }
            }

            // Refresh view
            BlangStringsView!.Refresh();
            var stringGrid = (Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.FindControl<DataGrid>("StringGrid")!;
            stringGrid.SelectedItem = null;

            return true;
        }

        // Save changes
        private bool SaveToJson(string filePath)
        {
            // Create object to serialize
            var blangJsonObject = new BlangJson()
            {
                Strings = new List<BlangJsonString>()
            };

            // Add modified strings to object
            foreach (var blangString in BlangFile!.Strings)
            {
                if (blangString.Modified)
                {
                    blangJsonObject.Strings.Add(new BlangJsonString()
                    {
                        Name = blangString.Identifier,
                        Text = blangString.Text.Replace("\r\n", "\n")
                    });
                }
            }

            // Serialize
            byte[] modJson = JsonSerializer.SerializeToUtf8Bytes(blangJsonObject, new JsonSerializerOptions()
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            // Write serialized JSON to file
            try
            {
                File.WriteAllBytes(filePath, modJson);
            }
            catch
            {
                return false;
            }

            return true;
        }

        // Constructor
        public MainWindowViewModel()
        {
            // Opens blang file and loads it into grid
            OpenBlangCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (UnsavedChanges && AnyModified)
                {
                    // Confirmation message box
                    var confirm = await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Warning", "Are you sure you want to open another file?\nAll unsaved changes will be lost.", Views.MessageBox.MessageButtons.YesCancel);

                    if (confirm == Views.MessageBox.MessageResult.Cancel)
                    {
                        return;
                    }
                }

                // Get filepath
                string filePath = await OpenFileDialog("Select the .blang file to load", "blang");

                if (String.IsNullOrEmpty(filePath))
                {
                    return;
                }

                // Read bytes from blang file
                byte[] blangFileBytes;

                try
                {
                    blangFileBytes = File.ReadAllBytes(filePath);
                }
                catch
                {
                    await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Error", "Failed to read from the blang file.\nMake sure the file exists and isn't being used by another process.", Views.MessageBox.MessageButtons.Ok);
                    return;
                }

                // Load blang file
                if (!LoadBlangFile(blangFileBytes, Path.GetFileNameWithoutExtension(filePath)))
                {
                    await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Error", "Failed to load the blang file.\nMake sure the file is valid, then try again.", Views.MessageBox.MessageButtons.Ok);
                    return;
                }
            });

            // Opens blang file and loads it into grid
            OpenResourcesCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (UnsavedChanges && AnyModified)
                {
                    // Confirmation message box
                    var confirm = await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Warning", "Are you sure you want to open another file?\nAll unsaved changes will be lost.", Views.MessageBox.MessageButtons.YesCancel);

                    if (confirm == Views.MessageBox.MessageResult.Cancel)
                    {
                        return;
                    }
                }

                // Get filepath
                string filePath = await OpenFileDialog("Select the .resources file to load", "resources");

                if (String.IsNullOrEmpty(filePath))
                {
                    return;
                }

                // Load .resources file
                var blangFiles = LoadResourcesFile(filePath);

                if (blangFiles == null)
                {
                    await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Error", "Failed to load the .resources file.\nMake sure the file is valid, then try again.", Views.MessageBox.MessageButtons.Ok);
                    return;
                }

                if (blangFiles.Count == 0)
                {
                    await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Error", "No blang files were found in the .resources file.\nMake sure you chose the right file, then try again.", Views.MessageBox.MessageButtons.Ok);
                    return;
                }

                // Let user select the blang to load
                string? selectedBlang = await Views.BlangSelection.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, blangFiles.Keys.ToArray());

                if (selectedBlang == null)
                {
                    return;
                }

                // Load the chosen blang
                if (!LoadBlangFile(blangFiles[selectedBlang], Path.GetFileNameWithoutExtension(selectedBlang)))
                {
                    await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Error", "Failed to load the blang file.\nMake sure the file is valid, then try again.", Views.MessageBox.MessageButtons.Ok);
                    return;
                }
            });

            // Create a new blang with one empty entry
            NewBlangCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (UnsavedChanges && AnyModified)
                {
                    // Confirmation message box
                    var confirm = await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Warning", "Are you sure you want to open another file?\nAll unsaved changes will be lost.", Views.MessageBox.MessageButtons.YesCancel);

                    if (confirm == Views.MessageBox.MessageResult.Cancel)
                    {
                        return;
                    }
                }

                // Create blang file
                LoadBlangFile(null);
            });

            // Load JSON file with changes
            LoadJsonCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!IsBlangLoaded)
                {
                    return;
                }

                if (UnsavedChanges && AnyModified)
                {
                    // Confirmation message box
                    var confirm = await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Warning", "Are you sure you want to load a JSON?\nSome unsaved changes may be lost.", Views.MessageBox.MessageButtons.YesCancel);

                    if (confirm == Views.MessageBox.MessageResult.Cancel)
                    {
                        return;
                    }
                }

                // Get filepath
                string filePath = await OpenFileDialog("Select the JSON file to load", "json");

                if (String.IsNullOrEmpty(filePath))
                {
                    return;
                }

                // If JSON is loaded with new file, remove blank cell
                BlangString? stringToRemove = null;

                if (BlangFile!.Strings.Count == 1 && !AnyModified)
                {
                    stringToRemove = BlangFile.Strings[0];
                }

                // Load JSON
                if (!LoadJson(filePath))
                {
                    await Views.MessageBox.Show((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Error", "Failed to load the JSON file.\nMake sure the file is valid, then try again.", Views.MessageBox.MessageButtons.Ok);
                    return;
                }

                if (stringToRemove != null)
                {
                    BlangFile.Strings.Remove(stringToRemove);
                }

                // Refresh view
                BlangStringsView!.Refresh();
                var stringGrid = (Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.FindControl<DataGrid>("StringGrid")!;
                stringGrid.SelectedItem = null;
            });

            // Save changes to mod JSON
            SaveCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!AnyModified)
                {
                    return;
                }

                // Open file dialog
                var fileDialog = new SaveFileDialog()
                {
                    Title = "Save JSON mod as...",
                    InitialFileName = $"{_blangLanguage}.json"
                };

                // Get save path
                string? filePath = await fileDialog.ShowAsync((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!);

                if (String.IsNullOrEmpty(filePath))
                {
                    return;
                }

                // Save to JSON
                if (!SaveToJson(filePath))
                {
                    await Views.MessageBox.Show((Application.Current.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!, "Error", "Failed to save the mod JSON.\nTry saving into another folder.", Views.MessageBox.MessageButtons.Ok);
                    return;
                }

                // Set unsaved changes to false
                UnsavedChanges = false;
            });

            // Close app
            CloseCommand = ReactiveCommand.Create(() =>
            {
                (Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.Close();
            });

            // Open string modding guide in browser
            OpenGuideCommand = ReactiveCommand.Create(() =>
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "https://wiki.eternalmods.com/books/2-how-to-create-mods/chapter/string-modding",
                    UseShellExecute = true
                });
            });

            // Open eternal modding hub discord invite in browser
            JoinHubCommand = ReactiveCommand.Create(() =>
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "https://discord.com/invite/FCdjqYDr5B",
                    UseShellExecute = true
                });
            });

            // Open 2016+ modding discord invite in browser
            Join2016Command = ReactiveCommand.Create(() =>
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "https://discord.com/invite/ymRvQaU",
                    UseShellExecute = true
                });
            });

            // Overrides enter key on data grid
            EnterKeyCommand = ReactiveCommand.Create(() => { });

            // Add new string to grid
            AddStringCommand = ReactiveCommand.Create(() =>
            {
                if (!IsBlangLoaded)
                {
                    return;
                }

                // Create new string
                var newBlangString = new BlangString(0, $"#new_string_{_newStringIndex}", $"#new_string_{_newStringIndex}", "", "", "", false);
                _newStringIndex += 1;

                // Add string and refresh grid to render changes
                BlangFile!.Strings.Add(newBlangString);
                BlangStringsView!.Refresh();

                // Scroll into added string
                var stringGrid = (Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow!.FindControl<DataGrid>("StringGrid")!;
                stringGrid.ScrollIntoView(newBlangString, null);
                stringGrid.SelectedItem = newBlangString;
            });
        }

        // Commands for binding in MainWindow
        public ReactiveCommand<Unit, Unit> OpenBlangCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenResourcesCommand { get; }

        public ReactiveCommand<Unit, Unit> NewBlangCommand { get; }

        public ReactiveCommand<Unit, Unit> LoadJsonCommand { get; }

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenGuideCommand { get; }

        public ReactiveCommand<Unit, Unit> CloseCommand { get; }

        public ReactiveCommand<Unit, Unit> JoinHubCommand { get; }

        public ReactiveCommand<Unit, Unit> Join2016Command { get; }

        public ReactiveCommand<Unit, Unit> EnterKeyCommand { get; }

        public ReactiveCommand<Unit, Unit> AddStringCommand { get; }
    }
}
