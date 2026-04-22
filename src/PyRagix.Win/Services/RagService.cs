using PyRagix.Net.Config;
using PyRagix.Net.Core;
using PyRagix.Net.Ingestion;

namespace PyRagix.Win.Services;

/// <summary>
/// Application-wide singleton that owns the <see cref="RagEngine"/> lifetime.
/// Lazily initializes from a TOML settings file and exposes query/ingestion to ViewModels.
/// </summary>
public sealed partial class RagService : IDisposable
{
    private static readonly string DefaultSettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PyRagix");

    private static readonly string DefaultSettingsPath =
        Path.Combine(DefaultSettingsDir, "settings.toml");

    private readonly Lock _lock = new();
    private RagEngine? _engine;
    private string _settingsPath = DefaultSettingsPath;

    /// <summary>
    /// Whether the engine has been created (does not guarantee readiness).
    /// </summary>
    public bool IsInitialized => _engine is not null;

    /// <summary>
    /// The path to the active settings file.
    /// </summary>
    public string SettingsPath => _settingsPath;

    /// <summary>
    /// On first run, writes a clean <c>settings.toml</c> to <see cref="DefaultSettingsPath"/>
    /// with model paths set to absolute locations next to the exe.
    /// Data artifact paths (db, indexes) are left relative so they land in the settings directory
    /// once <see cref="EnsureEngine"/> sets the working directory.
    /// </summary>
    private static void EnsureSettingsFile()
    {
        if (File.Exists(DefaultSettingsPath))
            return;

        Directory.CreateDirectory(DefaultSettingsDir);

        var modelsBase = Path.Combine(AppContext.BaseDirectory, "Models");
        var embeddingPath = Path.Combine(modelsBase, "embeddings", "model.onnx").Replace('\\', '/');
        var rerankerPath  = Path.Combine(modelsBase, "reranker",   "model.onnx").Replace('\\', '/');

        var toml = $"""
            EmbeddingModelPath = "{embeddingPath}"
            RerankerModelPath = "{rerankerPath}"
            DatabasePath = "pyragix.db"
            FaissIndexPath = "faiss_index.bin"
            LuceneIndexPath = "lucene_index"
            LlmEndpoint = "http://localhost:5001"
            LlmModel = ""
            LlmTimeout = 180
            Temperature = 0.1
            TopP = 0.9
            MaxTokens = 500
            EnableQueryExpansion = true
            EnableHybridSearch = true
            EnableReranking = true
            EnableSemanticChunking = true
            EmbeddingBatchSize = 16
            EmbeddingDimension = 384
            DefaultTopK = 7
            HybridAlpha = 0.7
            QueryExpansionCount = 3
            RerankTopK = 20
            ChunkSize = 1600
            ChunkOverlap = 200
            OcrBaseDpi = 150
            OcrMaxDpi = 300
            ExecutionProviderPreference = "Cpu"
            GpuDeviceId = 0
            """;

        File.WriteAllText(DefaultSettingsPath, toml);
    }

    /// <summary>
    /// Ensures the engine is initialized, creating it from the settings file if needed.
    /// </summary>
    private RagEngine EnsureEngine()
    {
        if (_engine is not null)
            return _engine;

        lock (_lock)
        {
            if (_engine is not null)
                return _engine;

            EnsureSettingsFile();

            // Relative paths in settings.toml (indexes, db) resolve against the settings
            // directory so all generated artifacts land in LocalAppData, not the exe folder.
            var settingsDir = Path.GetDirectoryName(_settingsPath);
            if (settingsDir is not null)
            {
                Directory.CreateDirectory(settingsDir);
                Environment.CurrentDirectory = settingsDir;
            }

            // Pass just the filename — LoadFromToml resolves it against CWD (set above),
            // matching how the console app uses it. A full absolute path confuses
            // Tomlyn.Extensions.Configuration when SetBasePath is also active.
            _engine = RagEngine.FromSettings(Path.GetFileName(_settingsPath));
            return _engine;
        }
    }

    /// <summary>
    /// Re-creates the engine, typically after settings have been changed.
    /// </summary>
    public void Reinitialize(string? settingsPath = null)
    {
        lock (_lock)
        {
            _engine?.Dispose();
            _engine = null;

            if (settingsPath is not null)
                _settingsPath = settingsPath;

            EnsureEngine();
        }
    }

    /// <summary>
    /// Loads the active config from the settings file, ensuring the file exists and the CWD is
    /// set to the settings directory (required by <see cref="PyRagixConfig.LoadFromToml"/>).
    /// Safe to call before the engine is initialized.
    /// </summary>
    public PyRagixConfig LoadConfig()
    {
        EnsureSettingsFile();
        var settingsDir = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(settingsDir);
        Environment.CurrentDirectory = settingsDir;
        return PyRagixConfig.LoadFromToml(Path.GetFileName(_settingsPath));
    }

    /// <summary>
    /// Checks whether the RAG pipeline is ready to answer queries (Ollama reachable, indexes present).
    /// </summary>
    public async Task<bool> IsReadyAsync()
    {
        return await EnsureEngine().IsReadyAsync();
    }

    /// <summary>
    /// Sends a question through the full retrieval pipeline and returns the generated answer.
    /// </summary>
    public async Task<string> QueryAsync(string question, int? topK = null, CancellationToken cancellationToken = default)
    {
        return await EnsureEngine().QueryAsync(question, topK, cancellationToken);
    }

    /// <summary>
    /// Runs document ingestion on the specified folder.
    /// </summary>
    public async Task IngestDocumentsAsync(
        string folderPath,
        bool fresh = false,
        IProgress<IngestionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureEngine().IngestDocumentsAsync(folderPath, fresh, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            _engine?.Dispose();
            _engine = null;
        }
    }
}
