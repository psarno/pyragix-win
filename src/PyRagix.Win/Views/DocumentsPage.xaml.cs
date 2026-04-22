using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PyRagix.Win.Helpers;
using PyRagix.Win.Models;
using PyRagix.Win.ViewModels;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace PyRagix.Win.Views;

/// <summary>
/// Documents page: folder selection and real-time ingestion progress.
/// </summary>
public sealed partial class DocumentsPage : Page
{
    public DocumentsViewModel ViewModel { get; }

    public DocumentsPage()
    {
        ViewModel = App.GetService<DocumentsViewModel>();
        InitializeComponent();

        ViewModel.Log.CollectionChanged += (_, _) => ScrollLogToBottom();
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        InitializeWithWindow.Initialize(picker, WindowHelper.GetMainHwnd());

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            ViewModel.SelectedFolder = folder.Path;
    }

    private void ScrollLogToBottom()
    {
        if (LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }
}

/// <summary>
/// Maps <see cref="LogLevel"/> to a foreground <see cref="SolidColorBrush"/>.
/// </summary>
public sealed partial class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is LogLevel level
            ? new SolidColorBrush(level switch
            {
                LogLevel.Success => Color.FromArgb(255, 76,  175,  80),   // Green 500
                LogLevel.Warning => Color.FromArgb(255, 255, 152,   0),   // Orange 500
                LogLevel.Error   => Color.FromArgb(255, 244,  67,  54),   // Red 500
                _                => Color.FromArgb(255, 180, 180, 180)    // Neutral gray
            })
            : new SolidColorBrush(Color.FromArgb(255, 180, 180, 180));

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns Visible when an integer is non-zero, Collapsed otherwise.
/// </summary>
public sealed partial class NonZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int n && n != 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns Visible when an integer is zero, Collapsed otherwise (for empty-state overlays).
/// </summary>
public sealed partial class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns Collapsed when a bool is true, Visible when false (inverted bool-to-visibility).
/// </summary>
public sealed partial class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
