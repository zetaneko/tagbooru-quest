using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using TagbooruQuest.Services.CharacterDesigner;
using Microsoft.Extensions.Logging;

namespace TagbooruQuest.Services.ComfyUI;

public interface IComfyPreviewController : IDisposable
{
    string? CurrentPromptId { get; }
    bool IsProcessing { get; }

    event Action<ProgressInfo> OnProgress;
    event Action<PreviewImage> OnPreview;
    event Action<ExecutedInfo> OnImageReady;
    event Action<Exception> OnError;
    event Action<bool> OnProcessingChanged;

    Task StartAsync();
    Task StopAsync();
}

public class ComfyPreviewController : IComfyPreviewController
{
    private readonly IComfyClient _comfyClient;
    private readonly IComfySettingsService _settings;
    private readonly IPromptJsonMapper _promptMapper;
    private readonly ICharacterBuildState _characterBuildState;
    private readonly ILogger<ComfyPreviewController>? _logger;

    private readonly Subject<PromptParts> _promptSubject = new();
    private IDisposable? _promptSubscription;
    private IDisposable? _characterStateSubscription;

    private readonly string _clientId = Guid.NewGuid().ToString();

    public string? CurrentPromptId { get; private set; }
    public bool IsProcessing { get; private set; }

    public event Action<ProgressInfo>? OnProgress;
    public event Action<PreviewImage>? OnPreview;
    public event Action<ExecutedInfo>? OnImageReady;
    public event Action<Exception>? OnError;
    public event Action<bool>? OnProcessingChanged;

    public ComfyPreviewController(
        IComfyClient comfyClient,
        IComfySettingsService settings,
        IPromptJsonMapper promptMapper,
        ICharacterBuildState characterBuildState,
        ILogger<ComfyPreviewController>? logger = null)
    {
        _comfyClient = comfyClient;
        _settings = settings;
        _promptMapper = promptMapper;
        _characterBuildState = characterBuildState;
        _logger = logger;

        // Wire up ComfyClient events
        _comfyClient.OnError += OnClientError;
    }

    public Task StartAsync()
    {
        if (_promptSubscription != null)
            return Task.CompletedTask;

        // Subscribe to character build state changes using a direct handler
        Action characterStateHandler = () => _promptSubject.OnNext(new PromptParts(_characterBuildState.BuildPrompt()));
        _characterBuildState.OnStateChanged += characterStateHandler;

        // Store the handler for cleanup
        _characterStateSubscription = Disposable.Create(() => _characterBuildState.OnStateChanged -= characterStateHandler);

        // Set up debounced prompt processing
        _promptSubscription = _promptSubject
            .DistinctUntilChanged(p => p.Positive.Trim()) // Only process if prompt actually changed
            .Throttle(TimeSpan.FromMilliseconds(_settings.DebounceMs))
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(async prompt =>
            {
                if (!_settings.Enabled || _comfyClient.Status != ConnectionStatus.Connected)
                    return;

                try
                {
                    await ProcessPromptAsync(prompt);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to process prompt");
                    OnError?.Invoke(ex);
                }
            });

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _promptSubscription?.Dispose();
        _promptSubscription = null;

        _characterStateSubscription?.Dispose();
        _characterStateSubscription = null;

        CurrentPromptId = null;
        SetProcessing(false);

        return Task.CompletedTask;
    }

    private async Task ProcessPromptAsync(PromptParts prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt.Positive))
            return;

        try
        {
            _logger?.LogDebug("Controller: Starting prompt processing");
            SetProcessing(true);

            // Cancel and restart behavior
            if (_settings.CancelAndRestartOnPromptChange)
            {
                _logger?.LogDebug("Controller: Canceling and clearing queue");
                await _comfyClient.InterruptAsync();
                await _comfyClient.ClearQueueAsync();
            }

            // Map prompt to workflow
            var workflow = await _promptMapper.MapPromptToWorkflowAsync(prompt, _settings);

            // Queue the prompt
            CurrentPromptId = await _comfyClient.QueuePromptAsync(_clientId, workflow);

            _logger?.LogDebug("Queued prompt {PromptId} for: {Prompt}", CurrentPromptId, prompt.Positive[..Math.Min(50, prompt.Positive.Length)]);

            // Start polling for completion in the background
            _ = Task.Run(async () =>
            {
                try
                {
                    var progress = new Progress<GenerationProgress>(p =>
                    {
                        OnProgress?.Invoke(new ProgressInfo(CurrentPromptId, p.Percentage, 100, p.CurrentStep));
                    });

                    var result = await _comfyClient.WaitForCompletionAsync(CurrentPromptId, progress);

                    if (result.Success && result.Images.Count > 0)
                    {
                        // Fetch the first image and display it
                        var firstImage = result.Images[0];
                        var imageBytes = await _comfyClient.GetImageAsync(firstImage.Filename, firstImage.Subfolder, firstImage.Type);
                        OnPreview?.Invoke(new PreviewImage(CurrentPromptId, imageBytes));
                        OnImageReady?.Invoke(new ExecutedInfo(CurrentPromptId, result.Images));
                    }
                    else if (!string.IsNullOrEmpty(result.Error))
                    {
                        OnError?.Invoke(new Exception(result.Error));
                    }

                    SetProcessing(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during polling for prompt {PromptId}", CurrentPromptId);
                    OnError?.Invoke(ex);
                    SetProcessing(false);
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to process prompt: {Prompt}", prompt.Positive);
            SetProcessing(false);
            throw;
        }
    }


    private void OnClientError(Exception error)
    {
        SetProcessing(false);
        OnError?.Invoke(error);
    }

    private void SetProcessing(bool processing)
    {
        if (IsProcessing != processing)
        {
            _logger?.LogDebug("Controller: Setting processing to {Processing}", processing);
            IsProcessing = processing;
            OnProcessingChanged?.Invoke(processing);
        }
    }

    public void Dispose()
    {
        _comfyClient.OnError -= OnClientError;

        _ = StopAsync();
        _promptSubject?.Dispose();
    }
}

// Helper class for Rx scheduling
internal static class TaskPoolScheduler
{
    public static IScheduler Default { get; } = System.Reactive.Concurrency.TaskPoolScheduler.Default;
}