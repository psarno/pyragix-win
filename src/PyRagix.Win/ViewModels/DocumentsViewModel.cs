using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using PyRagix.Net.Ingestion;
using PyRagix.Win.Models;
using PyRagix.Win.Services;

namespace PyRagix.Win.ViewModels;

/// <summary>
/// Drives the Documents page: folder selection, ingestion progress, and activity log.
/// </summary>
public sealed partial class DocumentsViewModel(RagService ragService, DispatcherQueue dispatcherQueue) : ObservableObject
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Activity log entries shown in the log list.
    /// </summary>
    public ObservableCollection<IngestionLogEntry> Log { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IngestCommand))]
    public partial string SelectedFolder { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool FreshIngestion { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IngestCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyPropertyChangedFor(nameof(IsProgressIndeterminate))]
    public partial bool IsIngesting { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentFile { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressIndeterminate))]
    public partial int TotalFiles { get; set; }

    [ObservableProperty]
    public partial int FilesCompleted { get; set; }

    /// <summary>
    /// True during the scanning/discovery phases before total file count is known.
    /// </summary>
    public bool IsProgressIndeterminate => IsIngesting && TotalFiles == 0;

    [RelayCommand(CanExecute = nameof(CanIngest))]
    private async Task IngestAsync()
    {
        _cts = new CancellationTokenSource();
        IsIngesting = true;
        ProgressPercent = 0;
        StatusText = "Starting...";
        CurrentFile = string.Empty;
        FilesCompleted = 0;
        TotalFiles = 0;
        Log.Clear();

        var progress = new Progress<IngestionProgressUpdate>(update =>
            dispatcherQueue.TryEnqueue(() => ApplyUpdate(update)));

        try
        {
            await Task.Run(() => ragService.IngestDocumentsAsync(
                SelectedFolder, FreshIngestion, progress, _cts.Token));
        }
        catch (OperationCanceledException)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                StatusText = "Cancelled";
                AddLog("Ingestion cancelled by user.", LogLevel.Warning);
            });
        }
        catch (Exception ex)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                StatusText = "Failed";
                AddLog($"Error: {ex.Message}", LogLevel.Error);
            });
        }
        finally
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                IsIngesting = false;
                _cts?.Dispose();
                _cts = null;
            });
        }
    }

    private bool CanIngest() => !IsIngesting && !string.IsNullOrWhiteSpace(SelectedFolder);

    [RelayCommand(CanExecute = nameof(IsIngesting))]
    private void Cancel() => _cts?.Cancel();

    private void ApplyUpdate(IngestionProgressUpdate update)
    {
        StatusText = update.Message ?? update.Stage.ToString();
        CurrentFile = update.CurrentFile ?? string.Empty;
        FilesCompleted = update.FilesCompleted;
        TotalFiles = update.TotalFiles;

        if (update.TotalFiles > 0)
            ProgressPercent = (double)update.FilesCompleted / update.TotalFiles * 100;

        var level = update.Stage switch
        {
            IngestionStage.Error => LogLevel.Error,
            IngestionStage.FileSkipped => LogLevel.Warning,
            IngestionStage.Completed or IngestionStage.FileCompleted => LogLevel.Success,
            _ => LogLevel.Info
        };

        // Only log meaningful stage transitions, not every chunk/embedding tick
        if (update.Stage is IngestionStage.FileStarted or IngestionStage.FileCompleted
            or IngestionStage.FileSkipped or IngestionStage.Completed
            or IngestionStage.Error or IngestionStage.Persisting
            or IngestionStage.Discovery)
        {
            AddLog(update.Message ?? $"{update.Stage}: {update.CurrentFile}", level);
        }
    }

    private void AddLog(string message, LogLevel level) =>
        Log.Add(new IngestionLogEntry { Message = message, Level = level });
}
