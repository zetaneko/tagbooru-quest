using TagbooruQuest.Services;

namespace TagbooruQuest.Platforms.Windows
{
    public class WindowsFileService : IFileService
    {
        public string AppDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TagbooruQuest");

        public async Task<Stream> OpenAppPackageFileAsync(string filename)
        {
            var packagePath = Path.Combine(AppContext.BaseDirectory, filename);
            if (File.Exists(packagePath))
            {
                return File.OpenRead(packagePath);
            }

            // Try in wwwroot
            var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", filename);
            if (File.Exists(wwwrootPath))
            {
                return File.OpenRead(wwwrootPath);
            }

            throw new FileNotFoundException($"Could not find {filename} in app package");
        }
    }
}