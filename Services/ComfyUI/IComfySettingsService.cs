namespace TagbooruQuest.Services.ComfyUI;

public interface IComfySettingsService
{
    bool Enabled { get; set; }
    Uri BaseUrl { get; set; }
    string? CheckpointsDir { get; set; }

    // Generation settings with defaults
    int Seed { get; set; }
    int Steps { get; set; }
    double Cfg { get; set; }
    string SamplerName { get; set; }
    string Scheduler { get; set; }
    int Width { get; set; }
    int Height { get; set; }
    double Denoise { get; set; }

    // Behavior settings
    bool CancelAndRestartOnPromptChange { get; set; }
    int DebounceMs { get; set; }

    // Selected model
    string? SelectedCheckpoint { get; set; }

    // Persistence
    Task LoadAsync();
    Task SaveAsync();
}