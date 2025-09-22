namespace TagbooruQuest.Services
{
    public interface IFileService
    {
        string AppDataDirectory { get; }
        Task<Stream> OpenAppPackageFileAsync(string filename);
    }
}