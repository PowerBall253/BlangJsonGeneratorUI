using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using BlangJsonGenerator.ViewModels;

namespace BlangJsonGenerator.Views
{
    public partial class BlangSelection : Window
    {
        // Blang selection result
        private static string? _result = null;

        // Constructor
        public BlangSelection()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        // Make OK button close the window
        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            // Get pressed button and set result
            switch (((Button)sender!).Content)
            {
                case "OK":
                    int resultIndex = this.FindControl<ComboBox>("BlangOptionsComboBox")!.SelectedIndex;
                    _result = resultIndex == -1 ? null : (DataContext as BlangSelectionViewModel)!.BlangOptions[resultIndex];
                    break;
                case "Cancel":
                    _result = null;
                    break;
            }

            this.Close();
        }

        // Close window on close button click
        private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Custom window drag implementation for Windows
        // Taken from https://github.com/FrankenApps/Avalonia-CustomTitleBarTemplate
        private bool _isPointerPressed = false;

        private PixelPoint _startPosition = new PixelPoint(0, 0);

        private Point _mouseOffsetToOrigin = new Point(0, 0);

        private void HandlePotentialDrop(object? sender, PointerReleasedEventArgs e)
        {
            var pos = e.GetPosition(this.FindControl<Panel>("BlangSelectionTitleBar"));
            _startPosition = new PixelPoint((int)(_startPosition.X + pos.X - _mouseOffsetToOrigin.X), (int)(_startPosition.Y + pos.Y - _mouseOffsetToOrigin.Y));
            this.Position = _startPosition;
            _isPointerPressed = false;
        }

        private void HandlePotentialDrag(object? sender, PointerEventArgs e)
        {
            if (_isPointerPressed)
            {
                var pos = e.GetPosition(this.FindControl<Panel>("BlangSelectionTitleBar"));
                _startPosition = new PixelPoint((int)(_startPosition.X + pos.X - _mouseOffsetToOrigin.X), (int)(_startPosition.Y + pos.Y - _mouseOffsetToOrigin.Y));
                this.Position = _startPosition;
            }
        }

        private void BeginListenForDrag(object? sender, PointerPressedEventArgs e)
        {
            _startPosition = this.Position;
            _mouseOffsetToOrigin = e.GetPosition(this.FindControl<Panel>("BlangSelectionTitleBar"));
            _isPointerPressed = true;
        }

        // Create and display blang selection window
        public static async Task<string?> Show(Window parent, string[] blangOptions)
        {
            // Reset result
            _result = null;

            // Create window
            var blangSelection = new BlangSelection
            {
                DataContext = new BlangSelectionViewModel()
            };

            // Set options in the combo box
            (blangSelection.DataContext as BlangSelectionViewModel)!.BlangOptions = blangOptions;

            // Show window
            await blangSelection.ShowDialog(parent);
            return _result;
        }

        // Initialize window
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Windows-specific changes
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows requires a custom titlebar due to system chrome issues
                // Remove default titlebar buttons
                this.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

                // Make custom close button visible
                this.FindControl<Button>("CloseButton")!.IsVisible = true;

                // Set drag-and-drop for custom title bar
                var titleBar = this.FindControl<Panel>("BlangSelectionTitleBar")!;
                titleBar.IsHitTestVisible = true;
                titleBar.PointerPressed += BeginListenForDrag;
                titleBar.PointerMoved += HandlePotentialDrag;
                titleBar.PointerReleased += HandlePotentialDrop;
            }
            else
            {
                // Remove custom close button for Windows
                this.FindControl<Panel>("BlangSelectionTitleBar")!.Children.Remove(this.FindControl<Button>("CloseButton")!);

                // Linux specific changes
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Hide custom title
                    this.FindControl<TextBlock>("BlangSelectionTitle")!.IsVisible = false;

                    // Disable acrylic blur
                    this.TransparencyLevelHint = WindowTransparencyLevel.None;
                }
            }
        }
    }
}
