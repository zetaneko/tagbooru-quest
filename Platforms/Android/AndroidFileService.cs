using TagbooruQuest.Services;

namespace TagbooruQuest.Platforms.Android
{
    public class AndroidFileService : IFileService
    {
        public string AppDataDirectory =>
            global::Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath ??
            global::Android.App.Application.Context.FilesDir?.AbsolutePath ??
            "/data/data/" + global::Android.App.Application.Context.PackageName + "/files";

        public async Task<Stream> OpenAppPackageFileAsync(string filename)
        {
            var context = global::Android.App.Application.Context;
            return context.Assets.Open(filename);
        }
    }
}