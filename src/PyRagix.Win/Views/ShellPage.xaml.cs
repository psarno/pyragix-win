using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace PyRagix.Win.Views;

/// <summary>
/// Shell page hosting the NavigationView and content Frame.
/// </summary>
public sealed partial class ShellPage : Page
{
    public ShellPage()
    {
        InitializeComponent();

        // Select Chat page by default
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void ShellPage_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = Helpers.WindowHelper.GetHwnd(App.MainWindow!);
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));

        // Caption buttons blend with Mica — no system-colored rectangle floating over content
        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        // Push content frame below the caption button zone.
        double scale = XamlRoot.RasterizationScale;
        double titleBarHeight = appWindow.TitleBar.Height / scale;
        if (titleBarHeight > 0)
            ContentFrame.Margin = new Thickness(0, titleBarHeight, 0, 0);

        // Use explicit drag rectangles instead of SetTitleBar(NavView).
        // SetTitleBar(NavView) registers the entire window as a potential drag area and relies on
        // WinUI's punch-through logic to restore interactivity for child elements — but this
        // fails for ScrollViewer (wheel events are swallowed, scrollbar drags move the window).
        // SetDragRectangles precisely scopes the drag region to the title strip only.
        UpdateDragRects(appWindow);
        App.MainWindow!.SizeChanged += (_, _) => UpdateDragRects(appWindow);

        // Some NavigationView-internal element absorbs PointerWheelChanged events for
        // positions below the LLM section of the Settings page, preventing them from
        // reaching the SettingsPage's ScrollViewer. Hook at the NavView root to catch
        // and forward any wheel events that originated outside the ContentFrame.
        NavView.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler(OnNavViewWheel),
            handledEventsToo: true);

    }

    private void OnNavViewWheel(object sender, PointerRoutedEventArgs e)
    {
        // Event came from within the Frame's content — normal routing, don't touch it.
        if (IsWithin(ContentFrame, e.OriginalSource as DependencyObject)) return;

        // Source is outside ContentFrame: a NavigationView-internal element intercepted
        // the event instead of letting it reach the page. Forward it manually.
        if (ContentFrame.Content is SettingsPage sp)
        {
            var delta = e.GetCurrentPoint(NavView).Properties.MouseWheelDelta;
            if (delta != 0)
            {
                sp.ScrollBy(delta);
                e.Handled = true;
            }
        }
    }

    private static bool IsWithin(DependencyObject ancestor, DependencyObject? node)
    {
        while (node is not null)
        {
            if (node == ancestor) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private void UpdateDragRects(AppWindow appWindow)
    {
        var titleBar   = appWindow.TitleBar;
        var scale      = XamlRoot?.RasterizationScale ?? 1.0;
        int titleBarH  = titleBar.Height;                          // physical pixels
        int rightInset = (int)titleBar.RightInset;                 // caption buttons, physical px
        int paneW      = (int)(NavView.CompactPaneLength * scale); // hamburger button area, physical px
        int windowW    = appWindow.Size.Width;

        int dragW = windowW - paneW - rightInset;
        if (dragW <= 0) return;

        // Drag strip: right of the hamburger button → left of the caption buttons
        titleBar.SetDragRectangles([new RectInt32(paneW, 0, dragW, titleBarH)]);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
            return;

        var tag = item.Tag as string;
        var pageType = tag switch
        {
            "chat" => typeof(ChatPage),
            "documents" => typeof(DocumentsPage),
            "settings" => typeof(SettingsPage),
            _ => null
        };

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
