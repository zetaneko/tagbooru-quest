using TagbooruQuest.Models;
using TagbooruQuest.Data;
using System.Text.Json;

namespace TagbooruQuest.Services;

public class PromptBuilderService
{
    private readonly TagGraphService _tagGraphService;
    private readonly Dictionary<BodyPartType, BodyPart> _bodyParts;
    // Store category roots per body part: BodyPart -> Category -> Roots
    private readonly Dictionary<BodyPartType, Dictionary<TagCategory, List<string>>> _customCategoryRoots = new();
    // Store custom category names per body part: BodyPart -> Category -> Name
    private readonly Dictionary<BodyPartType, Dictionary<TagCategory, string>> _customCategoryNames = new();

    public TagGraphService TagGraphService => _tagGraphService;

    public PromptBuilderService(TagGraphService tagGraphService)
    {
        _tagGraphService = tagGraphService;
        _bodyParts = InitializeBodyParts();
        LoadCategoryRootsConfig();
    }

    public event Action? OnPromptChanged;

    private Dictionary<BodyPartType, BodyPart> InitializeBodyParts()
    {
        var bodyParts = new Dictionary<BodyPartType, BodyPart>();

        var bodyPartConfigs = new[]
        {
            new { Type = BodyPartType.Head, Name = "head", Display = "Head" },
            new { Type = BodyPartType.Hair, Name = "hair", Display = "Hair" },
            new { Type = BodyPartType.HairColor, Name = "hair_color", Display = "Hair Color" },
            new { Type = BodyPartType.Ears, Name = "ears", Display = "Ears" },
            new { Type = BodyPartType.Eyes, Name = "eyes", Display = "Eyes" },
            new { Type = BodyPartType.EyeColor, Name = "eye_color", Display = "Eye Color" },
            new { Type = BodyPartType.Face, Name = "face", Display = "Face" },
            new { Type = BodyPartType.Neck, Name = "neck", Display = "Neck" },
            new { Type = BodyPartType.Shoulders, Name = "shoulders", Display = "Shoulders" },
            new { Type = BodyPartType.Arms, Name = "arms", Display = "Arms" },
            new { Type = BodyPartType.Hands, Name = "hands", Display = "Hands" },
            new { Type = BodyPartType.UpperTorso, Name = "upper_torso", Display = "Upper Torso" },
            new { Type = BodyPartType.LowerTorso, Name = "lower_torso", Display = "Lower Torso" },
            new { Type = BodyPartType.Hips, Name = "hips", Display = "Hips" },
            new { Type = BodyPartType.Legs, Name = "legs", Display = "Legs" },
            new { Type = BodyPartType.Feet, Name = "feet", Display = "Feet" },
            new { Type = BodyPartType.Tail, Name = "tail", Display = "Tail" }
        };

        foreach (var config in bodyPartConfigs)
        {
            bodyParts[config.Type] = new BodyPart
            {
                Type = config.Type,
                Name = config.Name,
                DisplayName = config.Display,
                SelectedTags = new List<string>(),
                AvailableNodes = new Dictionary<TagCategory, List<Node>>(),
                CurrentNodeIds = new Dictionary<TagCategory, int?>()
            };
        }

        return bodyParts;
    }

    public BodyPart GetBodyPart(BodyPartType type)
    {
        return _bodyParts[type];
    }

    public IEnumerable<BodyPart> GetAllBodyParts()
    {
        return _bodyParts.Values;
    }

    public void AddTagToBodyPart(BodyPartType bodyPart, string tag)
    {
        var part = _bodyParts[bodyPart];
        if (!part.SelectedTags.Contains(tag))
        {
            part.SelectedTags.Add(tag);
            OnPromptChanged?.Invoke();
        }
    }

    public void RemoveTagFromBodyPart(BodyPartType bodyPart, string tag)
    {
        var part = _bodyParts[bodyPart];
        if (part.SelectedTags.Remove(tag))
        {
            OnPromptChanged?.Invoke();
        }
    }

    public void ClearBodyPart(BodyPartType bodyPart)
    {
        var part = _bodyParts[bodyPart];
        if (part.SelectedTags.Count > 0)
        {
            part.SelectedTags.Clear();
            OnPromptChanged?.Invoke();
        }
    }

    public void ClearAllBodyParts()
    {
        bool hasChanges = false;
        foreach (var part in _bodyParts.Values)
        {
            if (part.SelectedTags.Count > 0)
            {
                part.SelectedTags.Clear();
                hasChanges = true;
            }
        }
        if (hasChanges)
        {
            OnPromptChanged?.Invoke();
        }
    }

    public async Task<List<Node>> GetTagsForBodyPart(BodyPartType bodyPart, TagCategory category)
    {
        var categoryRoots = GetCategoryRoots(bodyPart, category);
        var allNodes = new List<Node>();

        foreach (var rootPath in categoryRoots)
        {
            var node = FindNodeByPath(rootPath);
            if (node != null)
            {
                allNodes.Add(node);
            }
        }

        return allNodes;
    }

    private Node? FindNodeByPath(string path)
    {
        var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (pathParts.Length == 0) return null;

        // Find the root node
        var searchResults = _tagGraphService.Search(pathParts[0], 10);
        var currentNode = searchResults
            .Select(r => _tagGraphService.GetNodeById(r.Id))
            .FirstOrDefault(n => n != null && !n.IsTag &&
                           n.Text.Equals(pathParts[0], StringComparison.OrdinalIgnoreCase));

        if (currentNode == null) return null;

        // Navigate through the path
        for (int i = 1; i < pathParts.Length; i++)
        {
            var children = _tagGraphService.GetChildren(currentNode.Id);
            currentNode = children.FirstOrDefault(c =>
                c.Text.Equals(pathParts[i], StringComparison.OrdinalIgnoreCase) ||
                c.Text.Replace(" ", "_").Equals(pathParts[i], StringComparison.OrdinalIgnoreCase) ||
                c.Text.Replace("_", " ").Equals(pathParts[i], StringComparison.OrdinalIgnoreCase));

            if (currentNode == null) return null;
        }

        return currentNode;
    }

    private List<string> GetCategoryRoots(BodyPartType bodyPart, TagCategory category)
    {
        // Use custom roots if available for this specific body part, otherwise use defaults
        if (_customCategoryRoots.TryGetValue(bodyPart, out var bodyPartRoots) &&
            bodyPartRoots.TryGetValue(category, out var customRoots))
        {
            return customRoots;
        }

        return GetDefaultCategoryRoots(bodyPart, category);
    }

    public List<string> GetDefaultCategoryRoots(BodyPartType bodyPart, TagCategory category)
    {
        // Return different category roots based on both body part and category
        return (bodyPart, category) switch
        {
            // HEAD specific roots
            (BodyPartType.Head, TagCategory.Body) => new List<string> { "body/head", "body/anatomy/head" },
            (BodyPartType.Head, TagCategory.Attire) => new List<string> { "clothing/headwear", "accessories/headwear" },
            (BodyPartType.Head, TagCategory.Color) => new List<string> { "colors/skin_colors" },

            // HAIR specific roots
            (BodyPartType.Hair, TagCategory.Body) => new List<string> { "body/hair", "hair" },
            (BodyPartType.Hair, TagCategory.Attire) => new List<string> { "hair/accessories", "accessories/hair" },
            (BodyPartType.Hair, TagCategory.Color) => new List<string> { "colors/hair_colors", "hair/colors" },

            // EYES specific roots
            (BodyPartType.Eyes, TagCategory.Body) => new List<string> { "body/eyes", "facial_features/eyes" },
            (BodyPartType.Eyes, TagCategory.Attire) => new List<string> { "accessories/glasses", "clothing/eyewear" },
            (BodyPartType.Eyes, TagCategory.Color) => new List<string> { "colors/eye_colors", "eyes/colors" },

            // FACE specific roots
            (BodyPartType.Face, TagCategory.Body) => new List<string> { "body/face", "facial_features", "expressions" },
            (BodyPartType.Face, TagCategory.Attire) => new List<string> { "accessories/face", "clothing/masks" },
            (BodyPartType.Face, TagCategory.Color) => new List<string> { "colors/skin_colors" },

            // EARS specific roots
            (BodyPartType.Ears, TagCategory.Body) => new List<string> { "body/ears", "animal_ears" },
            (BodyPartType.Ears, TagCategory.Attire) => new List<string> { "accessories/earrings", "jewelry/ears" },
            (BodyPartType.Ears, TagCategory.BodyAccessories) => new List<string> { "accessories/earrings" },

            // UPPER TORSO specific roots
            (BodyPartType.UpperTorso, TagCategory.Body) => new List<string> { "body/torso", "body/chest" },
            (BodyPartType.UpperTorso, TagCategory.Attire) => new List<string> { "clothing/tops", "clothing/shirts", "clothing/jackets" },
            (BodyPartType.UpperTorso, TagCategory.BodyAccessories) => new List<string> { "accessories/necklaces", "jewelry/chest" },

            // LOWER TORSO specific roots
            (BodyPartType.LowerTorso, TagCategory.Body) => new List<string> { "body/waist", "body/stomach" },
            (BodyPartType.LowerTorso, TagCategory.Attire) => new List<string> { "clothing/belts", "clothing/waist" },
            (BodyPartType.LowerTorso, TagCategory.BodyAccessories) => new List<string> { "accessories/belts" },

            // HANDS specific roots
            (BodyPartType.Hands, TagCategory.Body) => new List<string> { "body/hands", "body/fingers" },
            (BodyPartType.Hands, TagCategory.Attire) => new List<string> { "clothing/gloves" },
            (BodyPartType.Hands, TagCategory.BodyAccessories) => new List<string> { "accessories/rings", "jewelry/hands" },

            // LEGS specific roots
            (BodyPartType.Legs, TagCategory.Body) => new List<string> { "body/legs", "body/thighs" },
            (BodyPartType.Legs, TagCategory.Attire) => new List<string> { "clothing/pants", "clothing/skirts", "clothing/stockings" },
            (BodyPartType.Legs, TagCategory.BodyAccessories) => new List<string> { "accessories/leg_accessories" },

            // FEET specific roots
            (BodyPartType.Feet, TagCategory.Body) => new List<string> { "body/feet" },
            (BodyPartType.Feet, TagCategory.Attire) => new List<string> { "clothing/shoes", "clothing/socks" },
            (BodyPartType.Feet, TagCategory.BodyAccessories) => new List<string> { "accessories/anklets" },

            // TAIL specific roots
            (BodyPartType.Tail, TagCategory.Body) => new List<string> { "animal_features/tail" },
            (BodyPartType.Tail, TagCategory.Attire) => new List<string> { "accessories/tail" },
            (BodyPartType.Tail, TagCategory.Color) => new List<string> { "colors/fur_colors" },

            // HAIR COLOR specific roots (popular standalone category)
            (BodyPartType.HairColor, TagCategory.Body) => new List<string> { "colors/hair_colors", "hair/colors" },
            (BodyPartType.HairColor, TagCategory.Color) => new List<string> { "colors/hair_colors", "hair/colors", "colors/natural_hair", "colors/fantasy_hair" },
            (BodyPartType.HairColor, TagCategory.Attire) => new List<string> { "hair/dyes", "cosmetics/hair" },

            // EYE COLOR specific roots (popular standalone category)
            (BodyPartType.EyeColor, TagCategory.Body) => new List<string> { "colors/eye_colors", "eyes/colors" },
            (BodyPartType.EyeColor, TagCategory.Color) => new List<string> { "colors/eye_colors", "eyes/colors", "colors/natural_eyes", "colors/fantasy_eyes" },
            (BodyPartType.EyeColor, TagCategory.Attire) => new List<string> { "accessories/contact_lenses" },

            // Default fallback
            _ => new List<string> { "body" }
        };
    }

    public void SetCustomCategoryRoots(BodyPartType bodyPart, TagCategory category, List<string> roots)
    {
        if (!_customCategoryRoots.ContainsKey(bodyPart))
        {
            _customCategoryRoots[bodyPart] = new Dictionary<TagCategory, List<string>>();
        }

        _customCategoryRoots[bodyPart][category] = roots.ToList();
        SaveCategoryRootsConfig();
    }

    public List<string> GetCurrentCategoryRoots(BodyPartType bodyPart, TagCategory category)
    {
        // Use custom roots if available for this specific body part, otherwise use defaults
        if (_customCategoryRoots.TryGetValue(bodyPart, out var bodyPartRoots) &&
            bodyPartRoots.TryGetValue(category, out var customRoots))
        {
            return customRoots;
        }

        return GetDefaultCategoryRoots(bodyPart, category);
    }

    private void LoadCategoryRootsConfig()
    {
        try
        {
            var configPath = GetConfigFilePath();
            System.Diagnostics.Debug.WriteLine($"Attempting to load config from: {configPath}");

            var fileExists = File.Exists(configPath);
            System.Diagnostics.Debug.WriteLine($"File exists: {fileExists}");

            if (fileExists)
            {
                var fileInfo = new FileInfo(configPath);
                System.Diagnostics.Debug.WriteLine($"File size: {fileInfo.Length} bytes");
                System.Diagnostics.Debug.WriteLine($"File last modified: {fileInfo.LastWriteTime}");
            }

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                System.Diagnostics.Debug.WriteLine($"Config file content: {json}");

                var config = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(json);

                _customCategoryRoots.Clear();
                if (config != null)
                {
                    foreach (var bodyPartKvp in config)
                    {
                        if (Enum.TryParse<BodyPartType>(bodyPartKvp.Key, out var bodyPart))
                        {
                            var categoryDict = new Dictionary<TagCategory, List<string>>();
                            foreach (var categoryKvp in bodyPartKvp.Value)
                            {
                                if (Enum.TryParse<TagCategory>(categoryKvp.Key, out var category))
                                {
                                    categoryDict[category] = categoryKvp.Value;
                                    System.Diagnostics.Debug.WriteLine($"Loaded {categoryKvp.Value.Count} roots for {bodyPart}.{category}");
                                }
                            }
                            _customCategoryRoots[bodyPart] = categoryDict;
                        }
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Successfully loaded config with {_customCategoryRoots.Count} body parts");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Config file does not exist, using defaults");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load category roots config: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private void SaveCategoryRootsConfig()
    {
        try
        {
            var configPath = GetConfigFilePath();
            System.Diagnostics.Debug.WriteLine($"Attempting to save config to: {configPath}");

            // Convert nested structure to serializable format
            var config = _customCategoryRoots.ToDictionary(
                bodyPartKvp => bodyPartKvp.Key.ToString(),
                bodyPartKvp => bodyPartKvp.Value.ToDictionary(
                    categoryKvp => categoryKvp.Key.ToString(),
                    categoryKvp => categoryKvp.Value
                )
            );

            System.Diagnostics.Debug.WriteLine($"Config data: {config.Count} body parts");

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var directory = Path.GetDirectoryName(configPath);
            if (directory != null && !Directory.Exists(directory))
            {
                System.Diagnostics.Debug.WriteLine($"Creating directory: {directory}");
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(configPath, json);
            System.Diagnostics.Debug.WriteLine($"Successfully saved config to: {configPath}");
            System.Diagnostics.Debug.WriteLine($"File exists after save: {File.Exists(configPath)}");

            // Verify we can read it back immediately
            if (File.Exists(configPath))
            {
                var verifyContent = File.ReadAllText(configPath);
                System.Diagnostics.Debug.WriteLine($"Verification read successful, length: {verifyContent.Length}");
                System.Diagnostics.Debug.WriteLine($"File content preview: {verifyContent.Substring(0, Math.Min(100, verifyContent.Length))}...");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WARNING: File does not exist immediately after save!");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save category roots config: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private string GetConfigFilePath()
    {
        try
        {
            // For MAUI apps, use FileSystem.AppDataDirectory which is reliable
            var appDataDir = FileSystem.AppDataDirectory;
            System.Diagnostics.Debug.WriteLine($"MAUI AppDataDirectory: {appDataDir}");

            var configPath = Path.Combine(appDataDir, "prompt-builder-config.json");

            // Ensure the directory exists
            var directory = Path.GetDirectoryName(configPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                System.Diagnostics.Debug.WriteLine($"Created directory: {directory}");
            }

            System.Diagnostics.Debug.WriteLine($"Using MAUI config path: {configPath}");
            return configPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error with MAUI FileSystem, falling back to manual path: {ex.Message}");

            // Fallback to manual path construction
            var fallbackPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TagbooruQuest",
                "prompt-builder-config.json");

            var directory = Path.GetDirectoryName(fallbackPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                System.Diagnostics.Debug.WriteLine($"Created fallback directory: {directory}");
            }

            System.Diagnostics.Debug.WriteLine($"Using fallback config path: {fallbackPath}");
            return fallbackPath;
        }
    }

    private IEnumerable<Node> FilterNodesForBodyPart(List<Node> nodes, BodyPartType bodyPart)
    {
        var bodyPartTerms = GetBodyPartFilterTerms(bodyPart);

        foreach (var node in nodes)
        {
            // Check if node text contains any relevant terms for this body part
            if (bodyPartTerms.Any(term => node.Text.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                yield return node;
            }

            // Also check children for relevant tags
            var children = _tagGraphService.GetChildren(node.Id);
            foreach (var child in children)
            {
                if (bodyPartTerms.Any(term => child.Text.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return child;
                }
            }
        }
    }

    private string[] GetBodyPartFilterTerms(BodyPartType bodyPart)
    {
        return bodyPart switch
        {
            BodyPartType.Hair => new[] { "hair", "bangs", "ponytail", "twintails", "braid" },
            BodyPartType.Eyes => new[] { "eye", "eyelash", "eyebrow", "pupil", "iris" },
            BodyPartType.Ears => new[] { "ear", "animal ears", "cat ears", "fox ears" },
            BodyPartType.Face => new[] { "face", "facial", "mouth", "nose", "cheek", "chin" },
            BodyPartType.Hands => new[] { "hand", "finger", "glove", "nail", "wrist" },
            BodyPartType.UpperTorso => new[] { "chest", "breast", "shirt", "top", "jacket", "torso" },
            BodyPartType.LowerTorso => new[] { "waist", "stomach", "abs", "midriff", "belly" },
            BodyPartType.Legs => new[] { "leg", "thigh", "pant", "skirt", "stocking", "knee" },
            BodyPartType.Feet => new[] { "foot", "feet", "shoe", "boot", "sock", "barefoot", "toe" },
            BodyPartType.Tail => new[] { "tail", "animal tail", "cat tail", "fox tail" },
            _ => new[] { bodyPart.ToString().ToLower() }
        };
    }


    public string GeneratePrompt()
    {
        var allTags = new List<string>();

        foreach (var bodyPart in _bodyParts.Values.OrderBy(bp => (int)bp.Type))
        {
            if (bodyPart.SelectedTags.Count > 0)
            {
                allTags.AddRange(bodyPart.SelectedTags);
            }
        }

        return string.Join(", ", allTags);
    }

    public Dictionary<string, object> ExportPromptData()
    {
        var data = new Dictionary<string, object>();

        foreach (var bodyPart in _bodyParts.Values)
        {
            if (bodyPart.SelectedTags.Count > 0)
            {
                data[bodyPart.Name] = bodyPart.SelectedTags.ToList();
            }
        }

        return data;
    }

    public void ImportPromptData(Dictionary<string, object> data)
    {
        ClearAllBodyParts();

        foreach (var kvp in data)
        {
            var bodyPartName = kvp.Key;
            var bodyPart = _bodyParts.Values.FirstOrDefault(bp => bp.Name == bodyPartName);

            if (bodyPart != null && kvp.Value is List<string> tags)
            {
                bodyPart.SelectedTags.AddRange(tags);
            }
        }

        OnPromptChanged?.Invoke();
    }
}