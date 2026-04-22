using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using PyRagix.Net.Config;
using PyRagix.Win.Helpers;
using PyRagix.Win.Services;

namespace PyRagix.Win.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly RagService _ragService;

    private static readonly string UiPrefsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PyRagix", "ui-prefs.toml");

    public SettingsViewModel(RagService ragService)
    {
        _ragService = ragService;
        LoadFromDisk();
        LoadUiPrefs();
    }

    // ── Paths ────────────────────────────────────────────────────────────────
    [ObservableProperty] public partial string EmbeddingModelPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string RerankerModelPath  { get; set; } = string.Empty;
    [ObservableProperty] public partial string DatabasePath       { get; set; } = string.Empty;
    [ObservableProperty] public partial string FaissIndexPath     { get; set; } = string.Empty;
    [ObservableProperty] public partial string BM25IndexPath      { get; set; } = string.Empty;
    [ObservableProperty] public partial string LuceneIndexPath    { get; set; } = string.Empty;

    // ── LLM ──────────────────────────────────────────────────────────────────
    [ObservableProperty] public partial string LlmEndpoint    { get; set; } = string.Empty;
    [ObservableProperty] public partial string LlmModel       { get; set; } = string.Empty;
    [ObservableProperty] public partial double Temperature    { get; set; }
    [ObservableProperty] public partial double TopP           { get; set; }
    [ObservableProperty] public partial int    MaxTokens      { get; set; }
    [ObservableProperty] public partial int    RequestTimeout { get; set; }

    // ── Features ─────────────────────────────────────────────────────────────
    [ObservableProperty] public partial bool EnableSemanticChunking { get; set; }
    [ObservableProperty] public partial bool EnableQueryExpansion   { get; set; }
    [ObservableProperty] public partial bool EnableHybridSearch     { get; set; }
    [ObservableProperty] public partial bool EnableReranking        { get; set; }

    // ── Performance ───────────────────────────────────────────────────────────
    [ObservableProperty] public partial int    EmbeddingBatchSize  { get; set; }
    [ObservableProperty] public partial int    EmbeddingDimension  { get; set; }
    [ObservableProperty] public partial int    QueryExpansionCount { get; set; }
    [ObservableProperty] public partial double HybridAlpha        { get; set; }
    [ObservableProperty] public partial int    RerankTopK         { get; set; }
    [ObservableProperty] public partial int    DefaultTopK        { get; set; }
    [ObservableProperty] public partial int    ChunkSize          { get; set; }
    [ObservableProperty] public partial int    ChunkOverlap       { get; set; }

    // ── GPU ───────────────────────────────────────────────────────────────────
    [ObservableProperty] public partial OnnxExecutionProvider ExecutionProviderPreference { get; set; }
    [ObservableProperty] public partial int GpuDeviceId { get; set; }

    // ── OCR ───────────────────────────────────────────────────────────────────
    [ObservableProperty] public partial int OcrBaseDpi { get; set; }
    [ObservableProperty] public partial int OcrMaxDpi  { get; set; }

    // ── Chat UI ───────────────────────────────────────────────────────────────
    [ObservableProperty] public partial bool ShowThinkingBlocks { get; set; } = true;

    partial void OnShowThinkingBlocksChanged(bool value) => SaveUiPrefs();

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveStatus))]
    [NotifyPropertyChangedFor(nameof(SaveStatusSeverity))]
    public partial string SaveStatus { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveStatusSeverity))]
    public partial bool IsSaveError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnectionError))]
    [NotifyPropertyChangedFor(nameof(HasConnectionStatus))]
    public partial string ConnectionStatus    { get; set; } = string.Empty;
    [ObservableProperty] public partial bool   IsConnectionTesting { get; set; }

    public bool IsConnectionError   => ConnectionStatus.StartsWith("Failed", StringComparison.Ordinal);
    public bool HasConnectionStatus => !string.IsNullOrEmpty(ConnectionStatus);
    public bool HasSaveStatus => !string.IsNullOrEmpty(SaveStatus);
    public InfoBarSeverity SaveStatusSeverity => IsSaveError ? InfoBarSeverity.Error : InfoBarSeverity.Success;

    /// <summary>Items for the ExecutionProvider ComboBox.</summary>
    public IReadOnlyList<OnnxExecutionProvider> ExecutionProviders { get; } =
        Enum.GetValues<OnnxExecutionProvider>();

    // ── Commands ──────────────────────────────────────────────────────────────

    private CancellationTokenSource? _dismissCts;

    [RelayCommand]
    private async Task SaveAsync()
    {
        // Cancel any pending auto-dismiss from the previous save so it doesn't
        // clobber the new status message.
        _dismissCts?.Cancel();
        _dismissCts = null;

        try
        {
            var config = BuildConfig();
            config.Validate();
            ConfigSerializer.SaveToToml(config, _ragService.SettingsPath);
            _ragService.Reinitialize();
            SaveStatus = "Settings saved and engine restarted.";
            IsSaveError = false;
        }
        catch (Exception ex)
        {
            SaveStatus = ex.Message;
            IsSaveError = true;
            return; // Errors stay until the next user action.
        }

        // Auto-dismiss the success banner after 3 seconds.
        using var cts = new CancellationTokenSource();
        _dismissCts = cts;
        try
        {
            await Task.Delay(3000, cts.Token);
            SaveStatus = string.Empty;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_dismissCts == cts) _dismissCts = null;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        LoadFromDisk();
        SaveStatus = string.Empty;
        ConnectionStatus = string.Empty;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsConnectionTesting = true;
        ConnectionStatus = string.Empty;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync(LlmEndpoint);
            ConnectionStatus = response.IsSuccessStatusCode
                ? $"Connected — HTTP {(int)response.StatusCode}"
                : $"Reachable but returned HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Failed: {ex.Message}";
        }
        finally
        {
            IsConnectionTesting = false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void LoadFromDisk()
    {
        var cfg = _ragService.LoadConfig();
        EmbeddingModelPath          = cfg.EmbeddingModelPath;
        RerankerModelPath           = cfg.RerankerModelPath;
        DatabasePath                = cfg.DatabasePath;
        FaissIndexPath              = cfg.FaissIndexPath;
        BM25IndexPath               = cfg.BM25IndexPath;
        LuceneIndexPath             = cfg.LuceneIndexPath;
        LlmEndpoint                 = cfg.LlmEndpoint;
        LlmModel                    = cfg.LlmModel;
        Temperature                 = cfg.Temperature;
        TopP                        = cfg.TopP;
        MaxTokens                   = cfg.MaxTokens;
        RequestTimeout              = cfg.RequestTimeout;
        EnableSemanticChunking      = cfg.EnableSemanticChunking;
        EnableQueryExpansion        = cfg.EnableQueryExpansion;
        EnableHybridSearch          = cfg.EnableHybridSearch;
        EnableReranking             = cfg.EnableReranking;
        EmbeddingBatchSize          = cfg.EmbeddingBatchSize;
        EmbeddingDimension          = cfg.EmbeddingDimension;
        QueryExpansionCount         = cfg.QueryExpansionCount;
        HybridAlpha                 = cfg.HybridAlpha;
        RerankTopK                  = cfg.RerankTopK;
        DefaultTopK                 = cfg.DefaultTopK;
        ChunkSize                   = cfg.ChunkSize;
        ChunkOverlap                = cfg.ChunkOverlap;
        ExecutionProviderPreference = cfg.ExecutionProviderPreference;
        GpuDeviceId                 = cfg.GpuDeviceId;
        OcrBaseDpi                  = cfg.OcrBaseDpi;
        OcrMaxDpi                   = cfg.OcrMaxDpi;
    }

    private void LoadUiPrefs()
    {
        if (!File.Exists(UiPrefsPath)) return;
        var toml = File.ReadAllText(UiPrefsPath);
        var match = ShowThinkingBlocksRegex().Match(toml);
        if (match.Success)
            ShowThinkingBlocks = match.Groups[1].Value == "true";
    }

    private void SaveUiPrefs()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UiPrefsPath)!);
        File.WriteAllText(UiPrefsPath, $"show_thinking_blocks = {ShowThinkingBlocks.ToString().ToLower()}\n");
    }

    private PyRagixConfig BuildConfig() => new()
    {
        EmbeddingModelPath          = EmbeddingModelPath,
        RerankerModelPath           = RerankerModelPath,
        DatabasePath                = DatabasePath,
        FaissIndexPath              = FaissIndexPath,
        BM25IndexPath               = BM25IndexPath,
        LuceneIndexPath             = LuceneIndexPath,
        LlmEndpoint                 = LlmEndpoint,
        LlmModel                    = LlmModel,
        Temperature                 = Temperature,
        TopP                        = TopP,
        MaxTokens                   = MaxTokens,
        RequestTimeout              = RequestTimeout,
        EnableSemanticChunking      = EnableSemanticChunking,
        EnableQueryExpansion        = EnableQueryExpansion,
        EnableHybridSearch          = EnableHybridSearch,
        EnableReranking             = EnableReranking,
        EmbeddingBatchSize          = EmbeddingBatchSize,
        EmbeddingDimension          = EmbeddingDimension,
        QueryExpansionCount         = QueryExpansionCount,
        HybridAlpha                 = HybridAlpha,
        RerankTopK                  = RerankTopK,
        DefaultTopK                 = DefaultTopK,
        ChunkSize                   = ChunkSize,
        ChunkOverlap                = ChunkOverlap,
        ExecutionProviderPreference = ExecutionProviderPreference,
        GpuDeviceId                 = GpuDeviceId,
        OcrBaseDpi                  = OcrBaseDpi,
        OcrMaxDpi                   = OcrMaxDpi,
    };
    [GeneratedRegex(@"show_thinking_blocks\s*=\s*(true|false)")]
    private static partial Regex ShowThinkingBlocksRegex();
}
