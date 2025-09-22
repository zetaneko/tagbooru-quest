using TagbooruQuest.Data;
using Microsoft.Extensions.Logging;

namespace TagbooruQuest
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Initialize the Tag DB
            SQLitePCL.Batteries_V2.Init();
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "tags.db");
            TagDbInitializer.Initialize(dbPath);

            // CSV file path (bundle it with app or load externally)
            var csvPath = Path.Combine(FileSystem.AppDataDirectory, "tags.csv");

            // Example: copy an embedded resource CSV into AppData on first run
            if (!File.Exists(csvPath))
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("tags.csv").Result;
                using var outFile = File.Create(csvPath);
                stream.CopyTo(outFile);
            }

            // Run import service
            var importer = new TagImportService(dbPath, csvPath);
            importer.ImportIfNeeded();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddSingleton(new TagGraphService(Path.Combine(FileSystem.AppDataDirectory, "tags.db")));


#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
