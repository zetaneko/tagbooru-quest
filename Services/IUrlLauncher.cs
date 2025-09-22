namespace TagbooruQuest.Services
{
    public interface IUrlLauncher
    {
        Task<bool> OpenUrlAsync(string url);
    }
}