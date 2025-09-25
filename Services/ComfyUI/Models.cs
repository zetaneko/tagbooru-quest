namespace TagbooruQuest.Services.ComfyUI;

public record PromptParts(string Positive, string Negative = "");

public record ProgressInfo(string PromptId, int Current, int Max, string? Node = null);

public record PreviewImage(string PromptId, byte[] Bytes);

public record ImageMeta(string Filename, string Subfolder, string Type);

public record ExecutedInfo(string PromptId, List<ImageMeta> Images);

public record QueuePromptRequest(string client_id, object prompt);

public record WsMsg(string type, string? prompt_id = null, object? data = null);

// Simplified polling models
public record GenerationProgress(int Percentage, string? CurrentStep);
public record GenerationResult(bool Success, List<ImageMeta> Images, string? Error);

public enum ConnectionStatus
{
    Disconnected,
    Connected,
    Error
}