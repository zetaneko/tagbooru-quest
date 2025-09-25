using Newtonsoft.Json.Linq;

namespace TagbooruQuest.Services.ComfyUI;

public interface IComfyClient : IDisposable
{
    ConnectionStatus Status { get; }

    // Events
    event Action<Exception> OnError;
    event Action<ConnectionStatus> OnStatusChanged;

    // Connection
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();

    // ComfyUI API operations
    Task<IReadOnlyList<string>> ListCheckpointsAsync(CancellationToken cancellationToken = default);
    Task<string> QueuePromptAsync(string clientId, JObject workflow, CancellationToken cancellationToken = default);
    Task InterruptAsync(CancellationToken cancellationToken = default);
    Task ClearQueueAsync(CancellationToken cancellationToken = default);

    // Image retrieval
    Task<byte[]> GetImageAsync(string filename, string subfolder = "", string type = "output", CancellationToken cancellationToken = default);

    // Simplified polling approach
    Task<GenerationResult> WaitForCompletionAsync(string promptId, IProgress<GenerationProgress>? progress = null, CancellationToken cancellationToken = default);
}