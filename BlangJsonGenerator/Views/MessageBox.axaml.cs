using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;

namespace BlangJsonGenerator.Views
{
    public partial class MessageBox : Window
    {
        // Message box result
        private static MessageResult _result;

        // Message button combinations enum
        public enum MessageButtons
        {
            Ok,
            YesCancel
        }

        // Message box result enum
        public enum MessageResult
        {
            Ok,
            Yes,
            Cancel
        }

        // Constructor
        public MessageBox()
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
                    _result = MessageResult.Ok;
                    break;
                case "Yes":
                    _result = MessageResult.Yes;
                    break;
                case "Cancel":
                    _result = MessageResult.Cancel;
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
            var pos = e.GetPosition(this.FindControl<Panel>("MessageTitleBar"));
            _startPosition = new PixelPoint((int)(_startPosition.X + pos.X - _mouseOffsetToOrigin.X), (int)(_startPosition.Y + pos.Y - _mouseOffsetToOrigin.Y));
            this.Position = _startPosition;
            _isPointerPressed = false;
        }

        private void HandlePotentialDrag(object? sender, PointerEventArgs e)
        {
            if (_isPointerPressed)
            {
                var pos = e.GetPosition(this.FindControl<Panel>("MessageTitleBar"));
                _startPosition = new PixelPoint((int)(_startPosition.X + pos.X - _mouseOffsetToOrigin.X), (int)(_startPosition.Y + pos.Y - _mouseOffsetToOrigin.Y));
                this.Position = _startPosition;
            }
        }

        private void BeginListenForDrag(object? sender, PointerPressedEventArgs e)
        {
            _startPosition = this.Position;
            _mouseOffsetToOrigin = e.GetPosition(this.FindControl<Panel>("MessageTitleBar"));
            _isPointerPressed = true;
        }

        // Create and display message box
        public static async Task<MessageResult> Show(Window parent, string title, string text, MessageButtons buttons)
        {
            // Set title and text
            var msgbox = new MessageBox();
            msgbox.FindControl<TextBlock>("MessageTitle")!.Text = title;
            msgbox.FindControl<TextBlock>("Text")!.Text = text;

            // Set buttons and default result
            var okButton = msgbox.FindControl<Button>("OkButton")!;
            var cancelButton = msgbox.FindControl<Button>("CancelButton")!;

            switch (buttons)
            {
                case MessageButtons.Ok:
                    _result = MessageResult.Ok;
                    break;
                case MessageButtons.YesCancel:
                    okButton.Content = "Yes";
                    cancelButton.IsVisible = true;
                    _result = MessageResult.Cancel;
                    break;
            }

            // Show window
            await msgbox.ShowDialog(parent);
            return _result;
        }

        // Initialize window
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Windows-specific changes
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows requires a custom titlebar due to a bug with Avalonia
                // See https://github.com/AvaloniaUI/Avalonia/issues/6942 for more info

                // Remove default titlebar buttons
                this.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

                // Make custom close button visible
                this.FindControl<Button>("CloseButton")!.IsVisible = true;

                // Set drag-and-drop for custom title bar
                var titleBar = this.FindControl<Panel>("MessageTitleBar")!;
                titleBar.IsHitTestVisible = true;
                titleBar.PointerPressed += BeginListenForDrag;
                titleBar.PointerMoved += HandlePotentialDrag;
                titleBar.PointerReleased += HandlePotentialDrop;
            }
            else
            {
                // Remove custom close button for Windows
                this.FindControl<Panel>("MessageTitleBar")!.Children.Remove(this.FindControl<Button>("CloseButton")!);

                // Linux specific changes
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Hide custom title
                    this.FindControl<TextBlock>("MessageTitle")!.IsVisible = false;

                    // Disable acrylic blur
                    this.TransparencyLevelHint = WindowTransparencyLevel.None;
                }
            }
        }
    }
}
