using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using PyRagix.Win.Models;
using PyRagix.Win.ViewModels;
using Windows.System;

namespace PyRagix.Win.Views;

/// <summary>
/// Chat page for querying the RAG engine.
/// </summary>
public sealed partial class ChatPage : Page
{
    public ChatViewModel ViewModel { get; }

    public ChatPage()
    {
        ViewModel = App.GetService<ChatViewModel>();
        InitializeComponent();

        ViewModel.Messages.CollectionChanged += (_, _) => ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        if (MessageList.Items.Count > 0)
        {
            MessageList.ScrollIntoView(MessageList.Items[^1]);
        }
    }

    private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SendCommand.CanExecute(null))
        {
            ViewModel.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}

/// <summary>
/// Selects the user or assistant bubble template based on <see cref="ChatMessage.IsUser"/>.
/// </summary>
public sealed partial class ChatTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserTemplate { get; set; }
    public DataTemplate? AssistantTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item is ChatMessage { IsUser: true }
            ? UserTemplate!
            : AssistantTemplate!;
    }
}

/// <summary>
/// Converts a nullable string to true when non-null/non-empty (for InfoBar IsOpen).
/// </summary>
public sealed partial class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is string s && !string.IsNullOrEmpty(s);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public sealed partial class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : value;
}

/// <summary>
/// Ensures a blank line precedes list blocks so MarkdownTextBlock parses them correctly.
/// LLM output often omits the blank line that CommonMark requires before a list.
/// </summary>
public sealed partial class MarkdownNormalizeConverter : IValueConverter
{
    private static readonly System.Text.RegularExpressions.Regex ListStart =
        ListStartRegex();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string text) return value;
        return ListStart.Replace(text, "\n$2$3");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    [System.Text.RegularExpressions.GeneratedRegex(@"(?<!\n\n)((\n)([ \t]*[-*+][ \t]|\d+\.[ \t]))", System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex ListStartRegex();
}
