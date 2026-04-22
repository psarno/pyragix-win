using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PyRagix.Win.ViewModels;

namespace PyRagix.Win.Views;

/// <summary>
/// Settings page: edit and persist the PyRagixConfig via SettingsExpander groups.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Catch wheel events that bubble *through* each expander. This fires before
        // the event reaches the outer ScrollViewer, so we can intercept events that
        // were absorbed by child controls (e.g. ToggleSwitch marks Handled=True, which
        // causes the ScrollViewer's built-in handler to skip scrolling).
        var expanderHandler = new PointerEventHandler(OnExpanderWheel);
        LlmExpander.AddHandler(UIElement.PointerWheelChangedEvent, expanderHandler, handledEventsToo: true);
        FeaturesExpander.AddHandler(UIElement.PointerWheelChangedEvent, expanderHandler, handledEventsToo: true);

        // Catch events that hit the ScrollViewer directly (bypassed the StackPanel subtree).
        SettingsScrollViewer.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler(OnScrollViewerWheel),
            handledEventsToo: true);

        // Expand the two default-open groups across separate dispatcher ticks so each
        // expansion gets its own layout pass before the next one starts.
        //
        // ORDER MATTERS — Features must expand before LLM:
        // CommunityToolkit SettingsExpander uses an internal ItemsRepeater. When the
        // expander content becomes Visible, the ItemsRepeater connects to the outer
        // ScrollViewer and realizes only items that fall within its effective viewport
        // (intersection of the ScrollViewer's viewport with the ItemsRepeater's layout
        // bounds). If LLM (7 items, ~433 px) were above Features, the Features
        // ItemsRepeater would connect with only ~96 px of effective viewport → items
        // 3-4 (Hybrid Search, Reranking) never realized → hit-test returns null at
        // their coordinates → PointerWheelChanged fires nowhere → "absolute silence."
        // Features is placed first in XAML (Y≈78, full 681 px viewport available) so
        // all four items are always realized on first connect.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            FeaturesExpander.IsExpanded = true;

            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                LlmExpander.IsExpanded = true;
            });
        });
    }

    private void OnExpanderWheel(object sender, PointerRoutedEventArgs e)
    {
        // Unhandled — the normal bubble to ScrollViewer will take care of it.
        if (!e.Handled) return;

        // A descendant marked this Handled (e.g. ToggleSwitch prevents accidental toggle).
        // NumberBox legitimately uses scroll to change its value — don't override that.
        var src = e.OriginalSource as DependencyObject;
        while (src is not null)
        {
            if (src is NumberBox) return;
            src = VisualTreeHelper.GetParent(src);
        }

        // Forward to the outer ScrollViewer. The built-in handler will skip because
        // e.Handled is already true; our ChangeView call is the only one that fires.
        var delta = e.GetCurrentPoint(SettingsScrollViewer).Properties.MouseWheelDelta;
        if (delta != 0)
            SettingsScrollViewer.ChangeView(null, SettingsScrollViewer.VerticalOffset - delta, null);
    }

    private void OnScrollViewerWheel(object sender, PointerRoutedEventArgs e)
    {
        if (IsWithin(SettingsContent, e.OriginalSource as DependencyObject)) return;

        var delta = e.GetCurrentPoint(SettingsScrollViewer).Properties.MouseWheelDelta;
        if (delta != 0)
        {
            SettingsScrollViewer.ChangeView(null, SettingsScrollViewer.VerticalOffset - delta, null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Called by ShellPage when a wheel event was intercepted by a NavigationView-internal
    /// element and needs to be forwarded here manually.
    /// </summary>
    internal void ScrollBy(int delta) =>
        SettingsScrollViewer.ChangeView(null, SettingsScrollViewer.VerticalOffset - delta, null);

    private static bool IsWithin(DependencyObject ancestor, DependencyObject? node)
    {
        while (node is not null)
        {
            if (node == ancestor) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

}

/// <summary>Collapses the element when <c>false</c>, makes it Visible when <c>true</c>.</summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns a red <see cref="SolidColorBrush"/> when <c>true</c>, otherwise the normal
/// caption foreground brush from the theme.
/// </summary>
public sealed partial class BoolToErrorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true
            ? new SolidColorBrush(Microsoft.UI.Colors.IndianRed)
            : (object)Application.Current.Resources["TextFillColorSecondaryBrush"];

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts between <see cref="int"/> (ViewModel) and <see cref="double"/> (NumberBox.Value).
/// </summary>
public sealed partial class IntToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int i ? (double)i : 0.0;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is double d ? (int)Math.Round(d) : 0;
}
