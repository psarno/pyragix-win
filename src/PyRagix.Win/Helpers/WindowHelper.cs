using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace PyRagix.Win.Helpers;

/// <summary>
/// Provides HWND access for unpackaged WinUI 3 apps.
/// </summary>
public static class WindowHelper
{
    /// <summary>
    /// Gets the HWND for a WinUI 3 Window.
    /// </summary>
    public static nint GetHwnd(Window window)
    {
        return WindowNative.GetWindowHandle(window);
    }

    /// <summary>
    /// Gets the HWND for the main application window.
    /// </summary>
    public static nint GetMainHwnd()
    {
        return App.MainWindow is not null ? GetHwnd(App.MainWindow) : nint.Zero;
    }

}
