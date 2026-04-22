using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using PyRagix.Win.Models;
using PyRagix.Win.Services;

namespace PyRagix.Win.ViewModels;

/// <summary>
/// Drives the chat page: manages the conversation history and dispatches queries to the RAG engine.
/// </summary>
public sealed partial class ChatViewModel(
    RagService ragService,
    SettingsViewModel settingsViewModel,
    DispatcherQueue dispatcherQueue) : ObservableObject
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Extracts the first &lt;think&gt; block from <paramref name="raw"/>.
    /// Returns (thinkingText, responseText) where responseText has all think blocks stripped.
    /// </summary>
    private static (string? thinking, string response) ExtractThinking(string raw)
    {
        var match = ThinkRegex().Match(raw);
        var thinking = match.Success ? match.Groups[1].Value.Trim() : null;
        var response = ThinkRegex().Replace(raw, string.Empty).Trim();
        return (string.IsNullOrEmpty(thinking) ? null : thinking, response);
    }

    /// <summary>
    /// The conversation history displayed in the chat list.
    /// </summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string QueryText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// True when no messages exist yet — used to show the empty-state placeholder.
    /// </summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var question = QueryText.Trim();
        if (string.IsNullOrEmpty(question))
            return;

        // Add user message and clear input
        Messages.Add(new ChatMessage { Content = question, DisplayContent = question, IsUser = true });
        QueryText = string.Empty;
        IsEmpty = false;
        ErrorMessage = null;
        IsBusy = true;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var answer = await Task.Run(() => ragService.QueryAsync(question, cancellationToken: token), token);

            var (thinking, response) = ExtractThinking(answer);
            dispatcherQueue.TryEnqueue(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Content = answer,
                    IsUser = false,
                    ThinkingContent = settingsViewModel.ShowThinkingBlocks ? thinking : null,
                    DisplayContent = response,
                });
            });
        }
        catch (OperationCanceledException)
        {
            // User cancelled — remove the pending user message so the conversation stays clean
            dispatcherQueue.TryEnqueue(() =>
            {
                if (Messages.Count > 0 && Messages[^1].IsUser)
                    Messages.RemoveAt(Messages.Count - 1);
                if (Messages.Count == 0)
                    IsEmpty = true;
            });
        }
        catch (Exception ex)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                ErrorMessage = ex.Message;
            });
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            dispatcherQueue.TryEnqueue(() =>
            {
                IsBusy = false;
            });
        }
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(QueryText);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsBusy;

    [RelayCommand]
    private void Clear()
    {
        Messages.Clear();
        QueryText = string.Empty;
        ErrorMessage = null;
        IsEmpty = true;
    }

    [GeneratedRegex(@"<think>([\s\S]*?)</think>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex ThinkRegex();
}
