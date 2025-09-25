using TagbooruQuest.Data;
using TagbooruQuest.Services;
using TagbooruQuest.Services.CharacterDesigner;
using TagbooruQuest.Services.ComfyUI;
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
            builder.Services.AddScoped<PromptBuilderService>();

            // Character Designer services
            builder.Services.AddSingleton<IDesignerRegistry, DesignerRegistry>();
            builder.Services.AddScoped<ITagSpriteResolver, TagSpriteResolver>();
            builder.Services.AddScoped<ICharacterBuildState, CharacterBuildState>();

            // ComfyUI services
            builder.Services.AddSingleton<IComfySettingsService, ComfySettingsService>();
            builder.Services.AddSingleton<IComfyClient>(serviceProvider =>
            {
                var settings = serviceProvider.GetRequiredService<IComfySettingsService>();
                var logger = serviceProvider.GetService<ILogger<ComfyClient>>();
                return new ComfyClient(settings, logger);
            });
            builder.Services.AddSingleton<IPromptJsonMapper, PromptJsonMapper>();
            builder.Services.AddScoped<IComfyPreviewController, ComfyPreviewController>();
            builder.Services.AddScoped<ComfyPreviewViewModel>(serviceProvider =>
            {
                var comfyClient = serviceProvider.GetRequiredService<IComfyClient>();
                var settings = serviceProvider.GetRequiredService<IComfySettingsService>();
                var previewController = serviceProvider.GetRequiredService<IComfyPreviewController>();
                var characterBuildState = serviceProvider.GetRequiredService<ICharacterBuildState>();
                var logger = serviceProvider.GetService<ILogger<ComfyPreviewViewModel>>();
                return new ComfyPreviewViewModel(comfyClient, settings, previewController, characterBuildState, logger);
            });


#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // Initialize ComfyUI settings
            var comfySettings = app.Services.GetRequiredService<IComfySettingsService>();
            _ = comfySettings.LoadAsync();

            return app;
        }
    }
}
