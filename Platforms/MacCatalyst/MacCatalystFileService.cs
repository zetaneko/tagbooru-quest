using Foundation;
using TagbooruQuest.Services;

namespace TagbooruQuest.Platforms.MacCatalyst
{
    public class MacCatalystFileService : IFileService
    {
        public string AppDataDirectory
        {
            get
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, "..", "Library", "Application Support");
            }
        }

        public async Task<Stream> OpenAppPackageFileAsync(string filename)
        {
            var path = NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(filename), Path.GetExtension(filename).TrimStart('.'));
            if (path != null && File.Exists(path))
            {
                return File.OpenRead(path);
            }

            throw new FileNotFoundException($"Could not find {filename} in app bundle");
        }
    }
}