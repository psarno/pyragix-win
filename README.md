# PyRagix Win

Native Windows 11 desktop frontend for [PyRagix.Net](https://github.com/psarno/pyragix-net), a local-first RAG (Retrieval Augmented Generation) pipeline. Built with WinUI 3, unpackaged, targeting Windows 11.

![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

## Features

- **Chat** - query your document knowledge base via a local LLM, with a conversational bubble UI
- **Ingest** - point at a folder and run the full PyRagix.Net ingestion pipeline with real-time progress tracking and cancellation support
- **Settings** - edit the full PyRagixConfig surface (28 properties across 6 groups), test your LLM connection, and persist changes to `settings.toml`

Everything runs locally. No cloud APIs, no data leaving your machine.

## Prerequisites

1. **Windows 11** (build 22621 or later)
2. **.NET 10.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
3. **Visual Studio 2022 or later** with the Windows App SDK workload (for the WinUI 3 build toolchain)
4. **PyRagix.Net 0.3.2** - available on [NuGet.org](https://www.nuget.org/packages/PyRagix.Net)
5. **ONNX models** - see [ONNX Models](#onnx-models) below
6. **Local LLM server** (any OpenAI-compatible endpoint):
   - [Ollama](https://ollama.com)
   - [LM Studio](https://lmstudio.ai)
   - [llamacpp](https://github.com/ggerganov/llama.cpp) server mode
   - [KoboldCpp](https://github.com/LostRuins/koboldcpp), [vLLM](https://github.com/vllm-project/vllm), [LocalAI](https://github.com/mudler/LocalAI), or any `/v1/chat/completions`-compatible server

## Building

```bash
git clone https://github.com/psarno/pyragix-win.git
cd pyragix-win
dotnet restore
dotnet build src/PyRagix.Win/PyRagix.Win.csproj -c Debug
```

Or open `pyragix-win.sln` in Visual Studio and build from there. The project targets `x64` only (required for FaissNet and ONNX Runtime native binaries).

## ONNX Models

PyRagix.Net requires two ONNX models for embedding and reranking. Export them once with Python:

```bash
pip install optimum[exporters-onnx]

# Embedding model
optimum-cli export onnx \
  --model sentence-transformers/all-MiniLM-L6-v2 \
  --task feature-extraction \
  src/PyRagix.Win/Models/embeddings

# Reranker model
optimum-cli export onnx \
  --model cross-encoder/ms-marco-MiniLM-L-6-v2 \
  --task text-classification \
  src/PyRagix.Win/Models/reranker
```

The app copies these to the output directory at build time. On first launch, it writes a `settings.toml` to `%LOCALAPPDATA%\PyRagix\` with model paths pre-set to the output directory locations.

See [PyRagix.Net docs/ONNX_SETUP.md](https://github.com/psarno/pyragix-net/blob/main/docs/ONNX_SETUP.md) for full model export details.

## Running

After building, launch the executable directly:

```
bin\Debug\net10.0-windows10.0.22621.0\win-x64\PyRagix.Win.exe
```

Or press F5 in Visual Studio.

On first launch, a default `settings.toml` is written to `%LOCALAPPDATA%\PyRagix\`. Open the Settings page to configure your LLM endpoint and model name before querying.

## Configuration

Settings are stored at `%LOCALAPPDATA%\PyRagix\settings.toml`. The Settings page in the app provides a full GUI editor. You can also edit the file directly - see `src/PyRagix.Win/settings.example.toml` for the full schema with comments.

Key settings:

```toml
# LLM (any OpenAI-compatible server)
LlmEndpoint = "http://localhost:11434"   # Ollama default
LlmModel    = "qwen2.5:7b"

# Paths (set automatically on first run)
EmbeddingModelPath = "C:/path/to/Models/embeddings/model.onnx"
RerankerModelPath  = "C:/path/to/Models/reranker/model.onnx"

# RAG pipeline features
EnableQueryExpansion   = true
EnableHybridSearch     = true
EnableReranking        = true
EnableSemanticChunking = true
```

## Project Structure

```
pyragix-win/
├── pyragix-win.sln
└── src/PyRagix.Win/
    ├── App.xaml / App.xaml.cs          # Bootstraps services and ViewModels
    ├── MainWindow.xaml / .cs           # Mica backdrop, DPI-aware sizing
    ├── Program.cs                       # Custom entry point (PerMonitorV2 DPI)
    ├── app.manifest                     # DPI awareness + Windows 11 compatibility
    ├── settings.example.toml           # Configuration template
    ├── Assets/                          # AppIcon.ico, SVG source
    ├── Services/
    │   └── RagService.cs               # Thread-safe RagEngine singleton
    ├── ViewModels/
    │   ├── ChatViewModel.cs
    │   ├── DocumentsViewModel.cs
    │   └── SettingsViewModel.cs
    ├── Views/
    │   ├── ShellPage.xaml / .cs        # NavigationView shell
    │   ├── ChatPage.xaml / .cs         # Chat bubbles + query input
    │   ├── DocumentsPage.xaml / .cs    # Folder picker + ingestion progress
    │   └── SettingsPage.xaml / .cs     # Config editor (SettingsExpander groups)
    ├── Models/                          # ChatMessage.cs, IngestionLogEntry.cs
    │                                    # (embeddings/ and reranker/ dirs are gitignored)
    └── Helpers/
        ├── ConfigSerializer.cs          # Tomlyn write-back for settings
        └── WindowHelper.cs              # HWND + DPI-aware window sizing
```

## Architecture

Standard MVVM using CommunityToolkit.Mvvm v8.4 with partial properties (AOT-compatible, no MVVMTK0045 warnings on WinUI 3).

`RagService` is a lazy-initialized singleton that owns the `RagEngine` lifetime. ViewModels call it from background threads via `Task.Run` and marshal results back to the UI thread with `DispatcherQueue.TryEnqueue`. `IProgress<IngestionProgressUpdate>` callbacks follow the same pattern.

The app is unpackaged - no AppX, no MSIX, no Package.appxmanifest. File pickers are initialized with the window HWND via `WinRT.Interop.InitializeWithWindow`.

## Dependencies

| Package | Version |
|---|---|
| Microsoft.WindowsAppSDK | 1.8.260317003 |
| CommunityToolkit.Mvvm | 8.4.2 |
| CommunityToolkit.WinUI.Controls.SettingsControls | 8.2.251219 |
| Tomlyn | 0.19.0 |
| PyRagix.Net | 0.3.2 |

## License

MIT License - see [LICENSE](LICENSE) for details.
