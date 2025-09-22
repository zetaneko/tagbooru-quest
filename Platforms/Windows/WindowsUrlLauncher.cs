using TagbooruQuest.Services;

namespace TagbooruQuest.Platforms.Windows
{
    public class WindowsUrlLauncher : IUrlLauncher
    {
        public async Task<bool> OpenUrlAsync(string url)
        {
            try
            {
                var uri = new Uri(url);
                var result = await global::Windows.System.Launcher.LaunchUriAsync(uri);
                return result;
            }
            catch
            {
                return false;
            }
        }
    }
}