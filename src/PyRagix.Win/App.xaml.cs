using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PyRagix.Win.Services;
using PyRagix.Win.ViewModels;

namespace PyRagix.Win;

/// <summary>
/// Application entry point. Bootstraps the main window and owns shared services.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private static RagService? _ragService;
    private static ChatViewModel? _chatViewModel;
    private static DocumentsViewModel? _documentsViewModel;
    private static SettingsViewModel? _settingsViewModel;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ragService = new RagService();
        _settingsViewModel = new SettingsViewModel(_ragService);
        _chatViewModel = new ChatViewModel(_ragService, _settingsViewModel, DispatcherQueue.GetForCurrentThread());
        _documentsViewModel = new DocumentsViewModel(_ragService, DispatcherQueue.GetForCurrentThread());

        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>
    /// Gets the main window instance. Used by helpers that need the HWND.
    /// </summary>
    public static Window? MainWindow => ((App)Current)._window;

    /// <summary>
    /// Simple service locator for ViewModels and services.
    /// </summary>
    public static T GetService<T>() where T : class
    {
        var app = (App)Current;

        if (typeof(T) == typeof(RagService))
            return (_ragService as T)!;
        if (typeof(T) == typeof(ChatViewModel))
            return (_chatViewModel as T)!;
        if (typeof(T) == typeof(DocumentsViewModel))
            return (_documentsViewModel as T)!;
        if (typeof(T) == typeof(SettingsViewModel))
            return (_settingsViewModel as T)!;

        throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }
}
