using System.Text.Json;

namespace TagbooruQuest.Services.ComfyUI;

public class ComfySettingsService : IComfySettingsService
{
    private readonly string _settingsFilePath;
    private ComfySettings _settings;

    public ComfySettingsService()
    {
        _settingsFilePath = Path.Combine(FileSystem.AppDataDirectory, "comfy-settings.json");
        _settings = new ComfySettings();
    }

    public bool Enabled
    {
        get => _settings.Enabled;
        set
        {
            _settings.Enabled = value;
            _ = SaveAsync();
        }
    }

    public Uri BaseUrl
    {
        get => _settings.BaseUrl;
        set
        {
            _settings.BaseUrl = value;
            _ = SaveAsync();
        }
    }

    public string? CheckpointsDir
    {
        get => _settings.CheckpointsDir;
        set
        {
            _settings.CheckpointsDir = value;
            _ = SaveAsync();
        }
    }

    public int Seed
    {
        get => _settings.Seed;
        set
        {
            _settings.Seed = value;
            _ = SaveAsync();
        }
    }

    public int Steps
    {
        get => _settings.Steps;
        set
        {
            _settings.Steps = value;
            _ = SaveAsync();
        }
    }

    public double Cfg
    {
        get => _settings.Cfg;
        set
        {
            _settings.Cfg = value;
            _ = SaveAsync();
        }
    }

    public string SamplerName
    {
        get => _settings.SamplerName;
        set
        {
            _settings.SamplerName = value;
            _ = SaveAsync();
        }
    }

    public string Scheduler
    {
        get => _settings.Scheduler;
        set
        {
            _settings.Scheduler = value;
            _ = SaveAsync();
        }
    }

    public int Width
    {
        get => _settings.Width;
        set
        {
            _settings.Width = value;
            _ = SaveAsync();
        }
    }

    public int Height
    {
        get => _settings.Height;
        set
        {
            _settings.Height = value;
            _ = SaveAsync();
        }
    }

    public double Denoise
    {
        get => _settings.Denoise;
        set
        {
            _settings.Denoise = value;
            _ = SaveAsync();
        }
    }

    public bool CancelAndRestartOnPromptChange
    {
        get => _settings.CancelAndRestartOnPromptChange;
        set
        {
            _settings.CancelAndRestartOnPromptChange = value;
            _ = SaveAsync();
        }
    }

    public int DebounceMs
    {
        get => _settings.DebounceMs;
        set
        {
            _settings.DebounceMs = value;
            _ = SaveAsync();
        }
    }

    public string? SelectedCheckpoint
    {
        get => _settings.SelectedCheckpoint;
        set
        {
            _settings.SelectedCheckpoint = value;
            _ = SaveAsync();
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<ComfySettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                }
            }
        }
        catch
        {
            // If loading fails, keep default settings
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch
        {
            // Ignore save failures
        }
    }

    private class ComfySettings
    {
        public bool Enabled { get; set; } = false;
        public Uri BaseUrl { get; set; } = new Uri("http://127.0.0.1:8188");
        public string? CheckpointsDir { get; set; }

        // Generation defaults
        public int Seed { get; set; } = 1;
        public int Steps { get; set; } = 20;
        public double Cfg { get; set; } = 7.0;
        public string SamplerName { get; set; } = "euler";
        public string Scheduler { get; set; } = "normal";
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 768;
        public double Denoise { get; set; } = 1.0;

        // Behavior defaults
        public bool CancelAndRestartOnPromptChange { get; set; } = true;
        public int DebounceMs { get; set; } = 250;

        public string? SelectedCheckpoint { get; set; }
    }
}