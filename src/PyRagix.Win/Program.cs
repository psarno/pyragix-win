using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PyRagix.Win;

/// <summary>
/// Custom entry point that sets PerMonitorV2 DPI awareness before WinUI3 initialises.
/// The app.manifest declaration alone is insufficient for unpackaged WinUI3 apps —
/// GetDpiForWindow returns 96 (DPI-unaware) without this explicit call, causing
/// blurry rendering and a compositor hit-test dead zone for PointerWheelChanged.
/// </summary>
public static partial class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }

    private static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAwarenessContext(nint value);
}
