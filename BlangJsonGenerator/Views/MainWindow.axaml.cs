using System;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using BlangJsonGenerator.ViewModels;

namespace BlangJsonGenerator.Views;

public partial class MainWindow : Window
{
    // Constructor
    public MainWindow()
    {
        InitializeComponent();

        // If file was provided in program arguments, load it
        Opened += async (_, _) =>
        {
            // Get args
            var envArgs = Environment.GetCommandLineArgs();

            if (envArgs.Length < 2)
            {
                return;
            }

            // Load file
            await LoadFileAsync(envArgs[1], false);
        };

#if DEBUG
        this.AttachDevTools();
#endif
    }

    // Load the given file
    private async Task LoadFileAsync(string filePath, bool allowJson)
    { 
        // Check file type and load it
        if (filePath.EndsWith(".blang", StringComparison.OrdinalIgnoreCase))
        {
            if ((DataContext as MainWindowViewModel)!.UnsavedChanges && (DataContext as MainWindowViewModel)!.AnyModified)
            {
                // Confirmation message box
                var confirm = await MessageBox.ShowAsync(this, "Warning", "Are you sure you want to open another file?\nAll unsaved changes will be lost.", MessageBox.MessageButtons.YesCancel);

                if (confirm == MessageBox.MessageResult.Cancel)
                {
                    return;
                }
            }

            // Read bytes from blang file
            byte[]? blangFileBytes = null;

            try
            {
                blangFileBytes = await File.ReadAllBytesAsync(filePath);
            }
            catch
            {
                await MessageBox.ShowAsync(this, "Error", "Failed to read from the blang file.\nMake sure the file exists and isn't being used by another process.", MessageBox.MessageButtons.Ok);
                return;
            }

            // Load blang file
            if (!(DataContext as MainWindowViewModel)!.LoadBlangFile(blangFileBytes, Path.GetFileNameWithoutExtension(filePath)))
            {
                await MessageBox.ShowAsync(this, "Error", "Failed to load the blang file.\nMake sure the file is valid, then try again.", MessageBox.MessageButtons.Ok);
                return;
            }
        }
        else if (filePath.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".resources.backup", StringComparison.OrdinalIgnoreCase))
        {
            if ((DataContext as MainWindowViewModel)!.UnsavedChanges && (DataContext as MainWindowViewModel)!.AnyModified)
            {
                // Confirmation message box
                var confirm = await MessageBox.ShowAsync(this, "Warning", "Are you sure you want to open another file?\nAll unsaved changes will be lost.", MessageBox.MessageButtons.YesCancel);

                if (confirm == MessageBox.MessageResult.Cancel)
                {
                    return;
                }
            }

            // Load blang files from .resources file
            var blangFiles = MainWindowViewModel.LoadResourcesFile(filePath);

            if (blangFiles == null)
            {
                await MessageBox.ShowAsync(this, "Error", "Failed to load the .resources file.\nMake sure the file is valid, then try again.", MessageBox.MessageButtons.Ok);
                return;
            }

            if (blangFiles.Count == 0)
            {
                await MessageBox.ShowAsync(this, "Error", "No blang files were found in the .resources file.\nMake sure you chose the right file, then try again.", MessageBox.MessageButtons.Ok);
                return;
            }

            // Let user select the blang to load
            string? selectedBlang = await BlangSelection.ShowAsync(this, blangFiles.Keys.ToArray());

            if (selectedBlang == null)
            {
                return;
            }

            // Load the chosen blang
            if (!(DataContext as MainWindowViewModel)!.LoadBlangFile(blangFiles[selectedBlang], Path.GetFileNameWithoutExtension(selectedBlang)))
            {
                await MessageBox.ShowAsync(this, "Error", "Failed to load the blang file.\nMake sure the file is valid, then try again.", MessageBox.MessageButtons.Ok);
                return;
            }
        }
        else if (allowJson && filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && (DataContext as MainWindowViewModel)!.IsBlangLoaded)
        {
            if ((DataContext as MainWindowViewModel)!.UnsavedChanges && (DataContext as MainWindowViewModel)!.AnyModified)
            {
                // Confirmation message box
                var confirm = await MessageBox.ShowAsync(this, "Warning", "Are you sure you want to load a JSON?\nSome unsaved changes may be lost.", MessageBox.MessageButtons.YesCancel);

                if (confirm == MessageBox.MessageResult.Cancel)
                {
                    return;
                }
            }

            // Load json
            if (!(DataContext as MainWindowViewModel)!.LoadJson(filePath))
            {
                await MessageBox.ShowAsync(this, "Error", "Failed to load the JSON file.\nMake sure the file is valid, then try again.", MessageBox.MessageButtons.Ok);
                return;
            }
        }
    }

    // Checks if blang string identifier has been modified on textbox input
    private void StringIdentifierBox_KeyUp(object? sender, KeyEventArgs e)
    {
        var stringBox = (TextBox)sender!;
        bool anyModified = false;

        // Look for string and set modified property
        foreach (var blangString in (DataContext as MainWindowViewModel)!.BlangFile!.Strings)
        {
            if (stringBox.Name!.Equals(blangString.OriginalIdentifier))
            {
                blangString.Modified = !stringBox.Text!.Equals(blangString.OriginalIdentifier);
                (DataContext as MainWindowViewModel)!.UnsavedChanges = true;
            }

            if (blangString.Modified)
            {
                anyModified = true;
            }
        }

        (DataContext as MainWindowViewModel)!.AnyModified = anyModified;
    }

    // Checks if blang string text has been modified on textbox input
    private void StringTextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        var stringBox = (TextBox)sender!;

        // Add newline with enter key
        if (e.Key == Key.Enter)
        {
            stringBox.Text = stringBox.Text!.Insert(stringBox.CaretIndex, "\n");
            stringBox.CaretIndex += 1;
        }

        bool anyModified = false;

        // Look for string and set modified property
        foreach (var blangString in (DataContext as MainWindowViewModel)!.BlangFile!.Strings)
        {
            if (stringBox.Name![..^5].Equals(blangString.OriginalIdentifier))
            {
                blangString.Modified = !stringBox.Text!.Equals(blangString.OriginalText);
                (DataContext as MainWindowViewModel)!.UnsavedChanges = true;
            }

            if (blangString.Modified)
            {
                anyModified = true;
            }
        }

        (DataContext as MainWindowViewModel)!.AnyModified = anyModified;
    }

    // Handler for drag-and-drop
    private async void DropAsync(object? sender, DragEventArgs e)
    {
        // Get filepath
        if (!e.Data.Contains(DataFormats.FileNames))
        {
            return;
        }

        string filePath = e.Data.GetFileNames()!.FirstOrDefault()!;

        // Filter out bad files
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            return;
        }

        // Load file
        await LoadFileAsync(filePath, true);
    }

    // Do not allow drag and drop of invalid files
    private void FilterDrop(object? sender, DragEventArgs e)
    {
        // Get filepath
        if (!e.Data.Contains(DataFormats.FileNames))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        string filePath = e.Data.GetFileNames()!.FirstOrDefault()!;

        // Filter out bad files
        if (string.IsNullOrEmpty(filePath))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (!filePath.EndsWith(".blang", StringComparison.OrdinalIgnoreCase) &&
            !filePath.ToLower().EndsWith(".resources", StringComparison.OrdinalIgnoreCase) &&
            !filePath.ToLower().EndsWith(".resources.backup", StringComparison.OrdinalIgnoreCase) &&
            !filePath.ToLower().EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase) && !(DataContext as MainWindowViewModel)!.IsBlangLoaded)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
    }

    // If false, close window without confirmation
    private bool _cancelClose = true;

    // Show confirmation for closing
    private async void HandleCloseAsync(object? sender, CancelEventArgs e)
    {
        if (!_cancelClose)
        {
            return;
        }

        // Check if there are unsaved changes
        if ((DataContext as MainWindowViewModel)!.UnsavedChanges && (DataContext as MainWindowViewModel)!.AnyModified)
        {
            // Prevent window from closing
            e.Cancel = true;

            // Display confirmation
            var res = await MessageBox.ShowAsync(this, "Confirm close", "Are you sure you want to close?\nUnsaved changes will be lost.", MessageBox.MessageButtons.YesCancel);

            // Close the window
            if (res == MessageBox.MessageResult.Yes)
            {
                _cancelClose = false;
                Close();
            }
        }
    }

    // Initialize window
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Add drag and drop handlers
        AddHandler(DragDrop.DropEvent, DropAsync);
        AddHandler(DragDrop.DragOverEvent, FilterDrop);

        // Set key bindings, using CMD for macOS and CTRL for Windows
        var ctrlKey = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "CMD" : "CTRL";
        KeyBindings[0].Gesture = KeyGesture.Parse($"{ctrlKey} + O"); // Open
        KeyBindings[1].Gesture = KeyGesture.Parse($"{ctrlKey} + N"); // New
        KeyBindings[2].Gesture = KeyGesture.Parse($"{ctrlKey} + S"); // Save

        // Remove regular menu on macOS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            this.FindControl<DockPanel>("MainPanel")!.Children.Remove(this.FindControl<Menu>("WindowsMenu")!);
        }

        // Remove title bar and disable acrylic blur on Linux
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            this.FindControl<DockPanel>("MainPanel")!.Children.Remove(this.FindControl<TextBlock>("AppTitle")!);
            TransparencyLevelHint = WindowTransparencyLevel.None;
        }
    }
}
