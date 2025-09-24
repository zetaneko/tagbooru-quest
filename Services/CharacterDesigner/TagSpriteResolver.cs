using TagbooruQuest.Models.CharacterDesigner;

namespace TagbooruQuest.Services.CharacterDesigner;

public interface ITagSpriteResolver
{
    TagOption ResolveTag(string canonicalTag);
    TagOption ResolveTag(string canonicalTag, int? nodeId = null, bool hasChildren = false, string? groupIcon = null);
    Task<List<TagOption>> DiscoverByFileGlobAsync(string globPattern);
    string GetImageUrl(string displayName);
    string CanonicalToDisplay(string canonicalTag);
}

public class TagSpriteResolver : ITagSpriteResolver
{
    private const string SpriteBasePath = "/img/tagging_sprites";
    private readonly string _physicalSpritePath;

    public TagSpriteResolver()
    {
        // Multiple fallback strategies for finding sprite directory
        var possiblePaths = new[]
        {
            // MAUI packaged app paths
            Path.Combine(FileSystem.Current.CacheDirectory, "..", "..", "wwwroot", "img", "tagging_sprites"),
            Path.Combine(FileSystem.AppDataDirectory, "..", "wwwroot", "img", "tagging_sprites"),
            // Development paths
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "tagging_sprites"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "img", "tagging_sprites"),
            // Relative paths
            Path.Combine("wwwroot", "img", "tagging_sprites"),
            "wwwroot/img/tagging_sprites"
        };

        foreach (var path in possiblePaths)
        {
            var normalizedPath = Path.GetFullPath(path);
            if (Directory.Exists(normalizedPath))
            {
                _physicalSpritePath = normalizedPath;
                return;
            }
        }

        // If no path found, use first one as fallback
        _physicalSpritePath = Path.GetFullPath(possiblePaths[0]);
    }

    public TagOption ResolveTag(string canonicalTag)
    {
        return ResolveTag(canonicalTag, null, false, null);
    }

    public TagOption ResolveTag(string canonicalTag, int? nodeId = null, bool hasChildren = false, string? groupIcon = null)
    {
        var display = CanonicalToDisplay(canonicalTag);
        var imageUrl = GetImageUrl(display);

        return new TagOption
        {
            CanonicalTag = canonicalTag,
            Display = display,
            ImageUrl = imageUrl,
            NodeId = nodeId,
            HasChildren = hasChildren,
            GroupIcon = groupIcon
        };
    }

    public async Task<List<TagOption>> DiscoverByFileGlobAsync(string globPattern)
    {
        var options = new List<TagOption>();

        try
        {

            if (!Directory.Exists(_physicalSpritePath))
            {

                // Return test data for debugging
                return GetTestDataForPattern(globPattern);
            }

            // Convert glob pattern to search pattern (simplified)
            var searchPattern = globPattern.Replace("*", "*");

            var files = Directory.GetFiles(_physicalSpritePath, searchPattern, SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                return GetTestDataForPattern(globPattern);
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var display = fileName.Replace("-", " "); // Convert dashes back to spaces for display

                // Skip colorbase files
                if (fileName.Equals("colorbase", StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativeUrl = GetRelativeImageUrl(file);

                options.Add(new TagOption
                {
                    CanonicalTag = fileName, // Keep original filename as canonical
                    Display = display,
                    ImageUrl = relativeUrl
                });

            }

            // Sort alphabetically by display name
            options = options.OrderBy(o => o.Display).ToList();
        }
        catch (Exception ex)
        {
            return GetTestDataForPattern(globPattern);
        }

        return options;
    }

    private List<TagOption> GetTestDataForPattern(string pattern)
    {
        var testData = new List<TagOption>();

        var patternLower = pattern.ToLowerInvariant();

        if (patternLower.Contains("hair"))
        {
            testData.AddRange(new[]
            {
                new TagOption { CanonicalTag = "blonde_hair", Display = "blonde hair", ImageUrl = "/img/tagging_sprites/blonde hair.jpg" },
                new TagOption { CanonicalTag = "black_hair", Display = "black hair", ImageUrl = "/img/tagging_sprites/black hair.jpg" },
                new TagOption { CanonicalTag = "red_hair", Display = "red hair", ImageUrl = "/img/tagging_sprites/red hair.jpg" },
                new TagOption { CanonicalTag = "long_hair", Display = "long hair", ImageUrl = "/img/tagging_sprites/long hair.jpg" }
            });
        }
        else if (patternLower.Contains("eyes"))
        {
            testData.AddRange(new[]
            {
                new TagOption { CanonicalTag = "blue_eyes", Display = "blue eyes", ImageUrl = "/img/tagging_sprites/blue eyes.jpg" },
                new TagOption { CanonicalTag = "green_eyes", Display = "green eyes", ImageUrl = "/img/tagging_sprites/green eyes.jpg" },
                new TagOption { CanonicalTag = "brown_eyes", Display = "brown eyes", ImageUrl = "/img/tagging_sprites/brown eyes.jpg" }
            });
        }
        else if (patternLower.Contains("body"))
        {
            testData.AddRange(new[]
            {
                new TagOption { CanonicalTag = "slim", Display = "slim", ImageUrl = "/img/tagging_sprites/slim.jpg" },
                new TagOption { CanonicalTag = "curvy", Display = "curvy", ImageUrl = "/img/tagging_sprites/curvy.jpg" },
                new TagOption { CanonicalTag = "athletic", Display = "athletic", ImageUrl = "/img/tagging_sprites/athletic.jpg" },
                new TagOption { CanonicalTag = "petite", Display = "petite", ImageUrl = "/img/tagging_sprites/petite.jpg" }
            });
        }
        else
        {
            // Always return some test data for any pattern
            testData.AddRange(new[]
            {
                new TagOption { CanonicalTag = "smile", Display = "smile", ImageUrl = "/img/tagging_sprites/smile.jpg" },
                new TagOption { CanonicalTag = "happy", Display = "happy", ImageUrl = "/img/tagging_sprites/happy.jpg" },
                new TagOption { CanonicalTag = "sad", Display = "sad", ImageUrl = "/img/tagging_sprites/sad.jpg" },
                new TagOption { CanonicalTag = "angry", Display = "angry", ImageUrl = "/img/tagging_sprites/angry.jpg" },
                new TagOption { CanonicalTag = "neutral", Display = "neutral", ImageUrl = "/img/tagging_sprites/neutral.jpg" }
            });
        }

        return testData;
    }

    public string GetImageUrl(string displayName)
    {
        // Convert spaces to dashes to match your URL-friendly filenames
        var fileName = displayName.Replace(" ", "-");
        return $"{SpriteBasePath}/{fileName}.jpg";
    }

    public string CanonicalToDisplay(string canonicalTag)
    {
        // Remove underscores and convert to display format
        return canonicalTag.Replace("_", " ");
    }

    private string GetRelativeImageUrl(string physicalPath)
    {
        // Convert physical path to web URL
        var fileName = Path.GetFileNameWithoutExtension(physicalPath);

        // Convert spaces to dashes to match URL-friendly filenames
        var urlFileName = fileName.Replace(" ", "-");
        return $"{SpriteBasePath}/{urlFileName}.jpg";
    }
}

// Extension methods for better glob support
public static class GlobExtensions
{
    public static bool MatchesGlob(this string text, string pattern)
    {
        // Simple glob matching - can be enhanced with proper regex
        if (pattern == "*") return true;

        if (pattern.StartsWith("*") && pattern.EndsWith("*"))
        {
            var middle = pattern.Substring(1, pattern.Length - 2);
            return text.Contains(middle, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.StartsWith("*"))
        {
            var suffix = pattern.Substring(1);
            return text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.EndsWith("*"))
        {
            var prefix = pattern.Substring(0, pattern.Length - 1);
            return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}