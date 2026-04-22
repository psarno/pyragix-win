using System.Globalization;
using PyRagix.Net.Config;

namespace PyRagix.Win.Helpers;

/// <summary>
/// Writes a <see cref="PyRagixConfig"/> back to a TOML file.
/// PyRagixConfig has no write-back method, so we serialize manually to guarantee
/// key names match what Tomlyn.Extensions.Configuration expects on next load.
/// </summary>
public static class ConfigSerializer
{
    public static void SaveToToml(PyRagixConfig config, string path)
    {
        static string D(double v) => v.ToString("G", CultureInfo.InvariantCulture);
        static string B(bool v)   => v ? "true" : "false";
        static string S(string v) => v.Replace("\\", "\\\\").Replace("\"", "\\\"");

        var toml = $"""
            EmbeddingModelPath = "{S(config.EmbeddingModelPath)}"
            RerankerModelPath = "{S(config.RerankerModelPath)}"
            DatabasePath = "{S(config.DatabasePath)}"
            FaissIndexPath = "{S(config.FaissIndexPath)}"
            BM25IndexPath = "{S(config.BM25IndexPath)}"
            LuceneIndexPath = "{S(config.LuceneIndexPath)}"
            LlmEndpoint = "{S(config.LlmEndpoint)}"
            LlmModel = "{S(config.LlmModel)}"
            Temperature = {D(config.Temperature)}
            TopP = {D(config.TopP)}
            MaxTokens = {config.MaxTokens}
            RequestTimeout = {config.RequestTimeout}
            EnableSemanticChunking = {B(config.EnableSemanticChunking)}
            ChunkSize = {config.ChunkSize}
            ChunkOverlap = {config.ChunkOverlap}
            EmbeddingBatchSize = {config.EmbeddingBatchSize}
            EmbeddingDimension = {config.EmbeddingDimension}
            EnableQueryExpansion = {B(config.EnableQueryExpansion)}
            QueryExpansionCount = {config.QueryExpansionCount}
            EnableHybridSearch = {B(config.EnableHybridSearch)}
            HybridAlpha = {D(config.HybridAlpha)}
            EnableReranking = {B(config.EnableReranking)}
            RerankTopK = {config.RerankTopK}
            DefaultTopK = {config.DefaultTopK}
            ExecutionProviderPreference = "{config.ExecutionProviderPreference}"
            GpuDeviceId = {config.GpuDeviceId}
            OcrBaseDpi = {config.OcrBaseDpi}
            OcrMaxDpi = {config.OcrMaxDpi}
            """;

        File.WriteAllText(path, toml);
    }
}
