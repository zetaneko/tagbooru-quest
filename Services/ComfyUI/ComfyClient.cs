using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace TagbooruQuest.Services.ComfyUI;

public class ComfyClient : IComfyClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ComfyClient>? _logger;

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

    public event Action<Exception>? OnError;
    public event Action<ConnectionStatus>? OnStatusChanged;

    public ComfyClient(IComfySettingsService settings, ILogger<ComfyClient>? logger = null)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = settings.BaseUrl,
            Timeout = TimeSpan.FromSeconds(30)
        };
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test HTTP connectivity
            var response = await _httpClient.GetAsync("/", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"HTTP connection failed: {response.StatusCode}");
            }

            SetStatus(ConnectionStatus.Connected);
            _logger?.LogInformation("Connected to ComfyUI at {BaseAddress}", _httpClient.BaseAddress);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to ComfyUI");
            SetStatus(ConnectionStatus.Error);
            OnError?.Invoke(ex);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        SetStatus(ConnectionStatus.Disconnected);
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<string>> ListCheckpointsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try newer API first
            var response = await _httpClient.GetAsync("/models/checkpoints", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var checkpoints = JsonConvert.DeserializeObject<string[]>(json);
                return checkpoints ?? Array.Empty<string>();
            }

            // Fallback to older API or empty list
            return Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list checkpoints");
            OnError?.Invoke(ex);
            return Array.Empty<string>();
        }
    }

    public async Task<string> QueuePromptAsync(string clientId, JObject workflow, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new QueuePromptRequest(clientId, workflow);
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/prompt", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonConvert.DeserializeObject<JObject>(responseJson);

            return result?["prompt_id"]?.ToString() ?? throw new Exception("No prompt_id returned");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to queue prompt");
            OnError?.Invoke(ex);
            throw;
        }
    }

    public async Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync("/interrupt", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to interrupt");
            OnError?.Invoke(ex);
        }
    }

    public async Task ClearQueueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clearRequest = new { clear = true };
            var json = JsonConvert.SerializeObject(clearRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/queue", content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clear queue");
            OnError?.Invoke(ex);
        }
    }

    public async Task<byte[]> GetImageAsync(string filename, string subfolder = "", string type = "output", CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/view?filename={Uri.EscapeDataString(filename)}";
            if (!string.IsNullOrEmpty(subfolder))
                url += $"&subfolder={Uri.EscapeDataString(subfolder)}";
            if (!string.IsNullOrEmpty(type))
                url += $"&type={Uri.EscapeDataString(type)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get image {Filename}", filename);
            OnError?.Invoke(ex);
            throw;
        }
    }

    public async Task<GenerationResult> WaitForCompletionAsync(string promptId, IProgress<GenerationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Waiting for completion of prompt {PromptId}", promptId);

            // Poll history until completion
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await _httpClient.GetAsync($"/history/{promptId}", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JObject.Parse(json);

                if (!root.TryGetValue(promptId, out var entryToken))
                {
                    // Not found yet, wait and retry
                    await Task.Delay(400, cancellationToken);
                    continue;
                }

                var entry = entryToken as JObject;
                if (entry == null)
                {
                    await Task.Delay(400, cancellationToken);
                    continue;
                }

                // Look for images in outputs
                var outputs = entry["outputs"] as JObject;
                var foundImages = new List<ImageMeta>();

                if (outputs != null)
                {
                    foreach (var outputKv in outputs)
                    {
                        var outputValue = outputKv.Value as JObject;
                        if (outputValue == null) continue;

                        if (outputValue.TryGetValue("images", out var imagesToken) && imagesToken is JArray imagesArray)
                        {
                            foreach (var imageToken in imagesArray)
                            {
                                if (imageToken is JObject imageObj)
                                {
                                    var filename = imageObj["filename"]?.ToString() ?? "";
                                    var subfolder = imageObj["subfolder"]?.ToString() ?? "";
                                    var type = imageObj["type"]?.ToString() ?? "output";

                                    foundImages.Add(new ImageMeta(filename, subfolder, type));
                                }
                            }
                        }
                    }
                }

                if (foundImages.Count > 0)
                {
                    _logger?.LogDebug("Generation completed with {ImageCount} images", foundImages.Count);
                    progress?.Report(new GenerationProgress(100, "Completed"));
                    return new GenerationResult(true, foundImages, null);
                }

                // Still processing, report progress if we can estimate it
                progress?.Report(new GenerationProgress(50, "Processing..."));
                await Task.Delay(400, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return new GenerationResult(false, new List<ImageMeta>(), "Cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to wait for completion of prompt {PromptId}", promptId);
            return new GenerationResult(false, new List<ImageMeta>(), ex.Message);
        }
    }


    private void SetStatus(ConnectionStatus status)
    {
        if (Status != status)
        {
            Status = status;
            OnStatusChanged?.Invoke(status);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}