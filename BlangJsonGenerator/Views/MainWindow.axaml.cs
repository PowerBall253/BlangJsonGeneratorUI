using System;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using BlangJsonGenerator.ViewModels;

namespace BlangJsonGenerator.Views
{
    public partial class MainWindow : Window
    {
        // Constructor
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        // Checks if blang string identifier has been modified on textbox input
        private void StringIdentifierBox_KeyUp(object? sender, KeyEventArgs e)
        {
            var stringBox = (TextBox)sender!;
            bool anyModified = false;

            // Look for string and set modified property
            foreach (var blangString in ((MainWindowViewModel)DataContext!).BlangFile!.Strings)
            {
                if (stringBox.Name!.Equals(blangString.OriginalIdentifier))
                {
                    blangString.Modified = !stringBox.Text.Equals(blangString.OriginalIdentifier);
                    ((MainWindowViewModel)DataContext).UnsavedChanges = true;
                }

                if (blangString.Modified)
                {
                    anyModified = true;
                }
            }

            ((MainWindowViewModel)DataContext).AnyModified = anyModified;
        }

        // Checks if blang string text has been modified on textbox input
        private void StringTextBox_KeyUp(object? sender, KeyEventArgs e)
        {
            var stringBox = (TextBox)sender!;

            // Add newline with enter key
            if (e.Key == Key.Enter)
            {
                stringBox.Text = stringBox.Text.Insert(stringBox.CaretIndex, "\n");
                stringBox.CaretIndex += 1;
            }

            bool anyModified = false;

            // Look for string and set modified property
            foreach (var blangString in ((MainWindowViewModel)DataContext!).BlangFile!.Strings)
            {
                if (stringBox.Name!.Substring(0, stringBox.Name.Length - 5).Equals(blangString.OriginalIdentifier))
                {
                    blangString.Modified = !stringBox.Text.Equals(blangString.OriginalText);
                    ((MainWindowViewModel)DataContext).UnsavedChanges = true;
                }

                if (blangString.Modified)
                {
                    anyModified = true;
                }
            }

            ((MainWindowViewModel)DataContext).AnyModified = anyModified;
        }

        // Handler for drag-and-drop
        private async void Drop(object? sender, DragEventArgs e)
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

            if (!Path.GetExtension(filePath).Equals(".blang", StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!File.Exists(filePath))
            {
                return;
            }

            // Check file type and load it
            if (Path.GetExtension(filePath).Equals(".blang", StringComparison.OrdinalIgnoreCase))
            {
                if (((MainWindowViewModel)DataContext!).UnsavedChanges && ((MainWindowViewModel)DataContext).AnyModified)
                {
                    // Confirmation message box
                    var confirm = await MessageBox.Show(this, "Warning", "Are you sure you want to open another file?\nAll unsaved changes will be lost.", MessageBox.MessageButtons.YesCancel);

                    if (confirm == MessageBox.MessageResult.Cancel)
                    {
                        return;
                    }
                }

                // Load blang file
                if (!((MainWindowViewModel)DataContext).OpenBlangFile(filePath))
                {
                    await MessageBox.Show(this, "Error", "Failed to load the blang file.\nMake sure the file is valid, then try again.", MessageBox.MessageButtons.Ok);
                    return;
                }
            }
            else if (Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase) && ((MainWindowViewModel)DataContext!).IsBlangLoaded)
            {
                if (((MainWindowViewModel)DataContext).UnsavedChanges && ((MainWindowViewModel)DataContext).AnyModified)
                {
                    // Confirmation message box
                    var confirm = await MessageBox.Show(this, "Warning", "Are you sure you want to load a JSON?\nSome unsaved changes may be lost.", MessageBox.MessageButtons.YesCancel);

                    if (confirm == MessageBox.MessageResult.Cancel)
                    {
                        return;
                    }
                }

                // Load json
                if (!((MainWindowViewModel)DataContext).LoadJson(filePath))
                {
                    await MessageBox.Show(this, "Error", "Failed to load the JSON file.\nMake sure the file is valid, then try again.", MessageBox.MessageButtons.Ok);
                    return;
                }
            }
        }

        // Do not allow drag and drop of non-blang files
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

            if (!Path.GetExtension(filePath).Equals(".blang", StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                e.DragEffects = DragDropEffects.None;
            }

            if (Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase) && !((MainWindowViewModel)DataContext!).IsBlangLoaded)
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        // If false, close window without confirmation
        private bool _cancelClose = true;

        // Show confirmation for closing
        private async void HandleClose(object? sender, CancelEventArgs e)
        {
            if (!_cancelClose)
            {
                return;
            }

            // Check if there are unsaved changes
            if (((MainWindowViewModel)DataContext!).UnsavedChanges && ((MainWindowViewModel)DataContext).AnyModified)
            {
                // Prevent window from closing
                e.Cancel = true;

                // Display confirmation
                var res = await MessageBox.Show(this, "Confirm close", "Are you sure you want to close?\nUnsaved changes will be lost.", MessageBox.MessageButtons.YesCancel);

                // Close the window
                if (res == MessageBox.MessageResult.Yes)
                {
                    _cancelClose = false;
                    this.Close();
                }
            }
        }

        // Initialize window
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Add drag and drop handlers
            this.AddHandler(DragDrop.DropEvent, Drop);
            this.AddHandler(DragDrop.DragOverEvent, FilterDrop);

            // Set key bindings, using CMD for macOS and CTRL for Windows
            var ctrlKey = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "CMD" : "CTRL";
            this.KeyBindings[0].Gesture = KeyGesture.Parse($"{ctrlKey} + O"); // Open
            this.KeyBindings[1].Gesture = KeyGesture.Parse($"{ctrlKey} + N"); // New
            this.KeyBindings[2].Gesture = KeyGesture.Parse($"{ctrlKey} + S"); // Save

            // Remove regular menu on macOS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                this.FindControl<DockPanel>("MainPanel").Children.Remove(this.FindControl<Menu>("WindowsMenu"));
            }

            // Remove title bar and disable acrylic blur on Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                this.FindControl<DockPanel>("MainPanel").Children.Remove(this.FindControl<TextBlock>("AppTitle"));
                this.TransparencyLevelHint = WindowTransparencyLevel.None;
            }
        }
    }
}
