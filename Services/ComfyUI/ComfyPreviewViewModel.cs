using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TagbooruQuest.Services.CharacterDesigner;

namespace TagbooruQuest.Services.ComfyUI;

public class ComfyPreviewViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IComfyClient _comfyClient;
    private readonly IComfySettingsService _settings;
    private readonly IComfyPreviewController _previewController;
    private readonly ICharacterBuildState _characterBuildState;
    private readonly ILogger<ComfyPreviewViewModel>? _logger;

    // Connection status
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
    private string _statusMessage = "Disconnected";

    // Image and progress
    private byte[]? _currentImage;
    private int _currentStep;
    private int _totalSteps = 1;
    private string _currentNode = "";
    private bool _isProcessing;
    private double _progressPercent;

    // Prompt display
    private string _positivePrompt = "";
    private string _negativePrompt = "";

    // Checkpoints
    private List<string> _availableCheckpoints = new();
    private string? _selectedCheckpoint;

    // Settings
    private bool _enabled;
    private string _serverUrl = "http://127.0.0.1:8188";
    private int _seed = -1;
    private int _steps = 20;
    private double _cfg = 7.0;
    private string _samplerName = "euler";
    private string _scheduler = "normal";
    private int _width = 1024;
    private int _height = 1024;
    private bool _cancelAndRestart = true;
    private int _debounceMs = 250;

    public ComfyPreviewViewModel(
        IComfyClient comfyClient,
        IComfySettingsService settings,
        IComfyPreviewController previewController,
        ICharacterBuildState characterBuildState,
        ILogger<ComfyPreviewViewModel>? logger = null)
    {
        _comfyClient = comfyClient;
        _settings = settings;
        _previewController = previewController;
        _characterBuildState = characterBuildState;
        _logger = logger;

        // Wire up events
        _comfyClient.OnStatusChanged += OnConnectionStatusChanged;
        _previewController.OnProgress += OnProgressChanged;
        _previewController.OnPreview += OnPreviewImageReceived;
        _previewController.OnImageReady += OnFinalImageReady;
        _previewController.OnProcessingChanged += OnProcessingChanged;
        _previewController.OnError += OnErrorOccurred;
        _characterBuildState.OnStateChanged += OnCharacterStateChanged;

        // Initialize from settings
        LoadSettings();
        UpdatePromptFromCharacterState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Connection properties
    public ConnectionStatus ConnectionStatus
    {
        get => _connectionStatus;
        private set
        {
            if (SetProperty(ref _connectionStatus, value))
            {
                StatusMessage = value switch
                {
                    ConnectionStatus.Connected => "Connected",
                    ConnectionStatus.Disconnected => "Disconnected",
                    ConnectionStatus.Error => "Connection Error",
                    _ => "Unknown"
                };
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // Image and progress properties
    public byte[]? CurrentImage
    {
        get => _currentImage;
        private set => SetProperty(ref _currentImage, value);
    }

    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                UpdateProgressPercent();
            }
        }
    }

    public int TotalSteps
    {
        get => _totalSteps;
        private set
        {
            if (SetProperty(ref _totalSteps, value))
            {
                UpdateProgressPercent();
            }
        }
    }

    public string CurrentNode
    {
        get => _currentNode;
        private set => SetProperty(ref _currentNode, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        private set => SetProperty(ref _isProcessing, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    // Prompt properties
    public string PositivePrompt
    {
        get => _positivePrompt;
        set => SetProperty(ref _positivePrompt, value);
    }

    public string NegativePrompt
    {
        get => _negativePrompt;
        set => SetProperty(ref _negativePrompt, value);
    }

    // Checkpoint properties
    public List<string> AvailableCheckpoints
    {
        get => _availableCheckpoints;
        private set => SetProperty(ref _availableCheckpoints, value);
    }

    public string? SelectedCheckpoint
    {
        get => _selectedCheckpoint;
        set
        {
            if (SetProperty(ref _selectedCheckpoint, value))
            {
                _settings.SelectedCheckpoint = value;
            }
        }
    }

    // Settings properties
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value))
            {
                _settings.Enabled = value;
                _ = value ? EnableAsync() : DisableAsync();
            }
        }
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set
        {
            if (SetProperty(ref _serverUrl, value) && Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                _settings.BaseUrl = uri;
            }
        }
    }

    public int Seed
    {
        get => _seed;
        set
        {
            if (SetProperty(ref _seed, value))
            {
                _settings.Seed = value;
            }
        }
    }

    public int Steps
    {
        get => _steps;
        set
        {
            if (SetProperty(ref _steps, value))
            {
                _settings.Steps = value;
            }
        }
    }

    public double Cfg
    {
        get => _cfg;
        set
        {
            if (SetProperty(ref _cfg, value))
            {
                _settings.Cfg = value;
            }
        }
    }

    public string SamplerName
    {
        get => _samplerName;
        set
        {
            if (SetProperty(ref _samplerName, value))
            {
                _settings.SamplerName = value;
            }
        }
    }

    public string Scheduler
    {
        get => _scheduler;
        set
        {
            if (SetProperty(ref _scheduler, value))
            {
                _settings.Scheduler = value;
            }
        }
    }

    public int Width
    {
        get => _width;
        set
        {
            if (SetProperty(ref _width, value))
            {
                _settings.Width = value;
            }
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            if (SetProperty(ref _height, value))
            {
                _settings.Height = value;
            }
        }
    }

    public bool CancelAndRestart
    {
        get => _cancelAndRestart;
        set
        {
            if (SetProperty(ref _cancelAndRestart, value))
            {
                _settings.CancelAndRestartOnPromptChange = value;
            }
        }
    }

    public int DebounceMs
    {
        get => _debounceMs;
        set
        {
            if (SetProperty(ref _debounceMs, value))
            {
                _settings.DebounceMs = value;
            }
        }
    }

    // Commands
    public async Task ConnectAsync()
    {
        try
        {
            await _comfyClient.ConnectAsync();
            await RefreshCheckpointsAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to ComfyUI");
            StatusMessage = $"Connection failed: {ex.Message}";
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            await _comfyClient.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to disconnect from ComfyUI");
        }
    }

    public async Task RefreshCheckpointsAsync()
    {
        try
        {
            var checkpoints = await _comfyClient.ListCheckpointsAsync();
            AvailableCheckpoints = checkpoints.ToList();

            // Select first checkpoint if none selected and checkpoints available
            if (string.IsNullOrEmpty(SelectedCheckpoint) && checkpoints.Count > 0)
            {
                SelectedCheckpoint = checkpoints[0];
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh checkpoints");
        }
    }

    public async Task InterruptAsync()
    {
        try
        {
            await _comfyClient.InterruptAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to interrupt generation");
        }
    }

    // Private methods
    private async Task EnableAsync()
    {
        try
        {
            await _comfyClient.ConnectAsync();
            await _previewController.StartAsync();
            await RefreshCheckpointsAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to enable ComfyUI integration");
            Enabled = false; // Reset the toggle
        }
    }

    private async Task DisableAsync()
    {
        try
        {
            await _previewController.StopAsync();
            await _comfyClient.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to disable ComfyUI integration");
        }
    }

    private void LoadSettings()
    {
        _enabled = _settings.Enabled;
        _serverUrl = _settings.BaseUrl.ToString();
        _seed = _settings.Seed;
        _steps = _settings.Steps;
        _cfg = _settings.Cfg;
        _samplerName = _settings.SamplerName;
        _scheduler = _settings.Scheduler;
        _width = _settings.Width;
        _height = _settings.Height;
        _cancelAndRestart = _settings.CancelAndRestartOnPromptChange;
        _debounceMs = _settings.DebounceMs;
        _selectedCheckpoint = _settings.SelectedCheckpoint;

        // Notify all properties changed
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(ServerUrl));
        OnPropertyChanged(nameof(Seed));
        OnPropertyChanged(nameof(Steps));
        OnPropertyChanged(nameof(Cfg));
        OnPropertyChanged(nameof(SamplerName));
        OnPropertyChanged(nameof(Scheduler));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(CancelAndRestart));
        OnPropertyChanged(nameof(DebounceMs));
        OnPropertyChanged(nameof(SelectedCheckpoint));
    }

    private void UpdateProgressPercent()
    {
        ProgressPercent = TotalSteps > 0 ? (double)CurrentStep / TotalSteps * 100.0 : 0.0;
    }

    // Event handlers
    private void OnConnectionStatusChanged(ConnectionStatus status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectionStatus = status;
        });
    }

    private void OnProgressChanged(ProgressInfo progress)
    {
        _logger?.LogDebug("Progress changed: {Current}/{Max} - {Node}", progress.Current, progress.Max, progress.Node);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentStep = progress.Current;
            TotalSteps = progress.Max;
            CurrentNode = progress.Node ?? "";
        });
    }

    private void OnPreviewImageReceived(PreviewImage preview)
    {
        _logger?.LogDebug("Preview image received: {ByteCount} bytes", preview.Bytes.Length);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentImage = preview.Bytes;
        });
    }

    private void OnFinalImageReady(ExecutedInfo executed)
    {
        // Fetch the final image
        if (executed.Images.Count > 0)
        {
            var image = executed.Images[0];
            _ = Task.Run(async () =>
            {
                try
                {
                    var imageBytes = await _comfyClient.GetImageAsync(image.Filename, image.Subfolder, image.Type);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        CurrentImage = imageBytes;
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to fetch final image");
                }
            });
        }
    }

    private void OnProcessingChanged(bool isProcessing)
    {
        _logger?.LogDebug("Processing changed: {IsProcessing}", isProcessing);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsProcessing = isProcessing;
            if (!isProcessing)
            {
                CurrentStep = 0;
                TotalSteps = 1;
                CurrentNode = "";
                _logger?.LogDebug("Processing completed, reset progress");
            }
        });
    }

    private void OnErrorOccurred(Exception error)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusMessage = $"Error: {error.Message}";
        });
        _logger?.LogError(error, "ComfyUI error occurred");
    }

    private void OnCharacterStateChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdatePromptFromCharacterState();
        });
    }

    private void UpdatePromptFromCharacterState()
    {
        PositivePrompt = _characterBuildState.BuildPrompt();
        // Keep negative prompt as is for now - could be extended to support negative tags from character state
    }

    // INotifyPropertyChanged implementation
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        _logger?.LogDebug("Property changed: {PropertyName}", propertyName);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        _comfyClient.OnStatusChanged -= OnConnectionStatusChanged;
        _previewController.OnProgress -= OnProgressChanged;
        _previewController.OnPreview -= OnPreviewImageReceived;
        _previewController.OnImageReady -= OnFinalImageReady;
        _previewController.OnProcessingChanged -= OnProcessingChanged;
        _previewController.OnError -= OnErrorOccurred;
        _characterBuildState.OnStateChanged -= OnCharacterStateChanged;
    }
}