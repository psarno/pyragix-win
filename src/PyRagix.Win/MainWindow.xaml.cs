using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace PyRagix.Win;

/// <summary>
/// Main application window with Mica backdrop.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "PyRagix";
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();

        // Resize via AppWindow (not SetWindowPos) so the WinUI3 compositor's
        // DPI context and hit-test region stay in sync with the actual window size.
        // SetWindowPos in the constructor races against compositor DPI init, causing
        // PointerWheelChanged hit-testing to use the wrong coordinate space at >100% scaling.
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(1200, 800));
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
    }
}
