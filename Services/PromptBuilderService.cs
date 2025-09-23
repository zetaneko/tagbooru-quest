using TagbooruQuest.Models;
using TagbooruQuest.Data;

namespace TagbooruQuest.Services;

public class PromptBuilderService
{
    private readonly TagGraphService _tagGraphService;
    private readonly Dictionary<BodyPartType, BodyPart> _bodyParts;

    public PromptBuilderService(TagGraphService tagGraphService)
    {
        _tagGraphService = tagGraphService;
        _bodyParts = InitializeBodyParts();
    }

    public event Action? OnPromptChanged;

    private Dictionary<BodyPartType, BodyPart> InitializeBodyParts()
    {
        var bodyParts = new Dictionary<BodyPartType, BodyPart>();

        var bodyPartConfigs = new[]
        {
            new { Type = BodyPartType.Head, Name = "head", Display = "Head" },
            new { Type = BodyPartType.Hair, Name = "hair", Display = "Hair" },
            new { Type = BodyPartType.Ears, Name = "ears", Display = "Ears" },
            new { Type = BodyPartType.Eyes, Name = "eyes", Display = "Eyes" },
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
                AvailableTags = new Dictionary<TagCategory, List<string>>()
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

    public async Task<List<string>> GetTagsForBodyPart(BodyPartType bodyPart, TagCategory category)
    {
        var part = _bodyParts[bodyPart];

        if (part.AvailableTags.ContainsKey(category))
        {
            return part.AvailableTags[category];
        }

        // Load tags from the database based on body part and category
        var tags = await LoadTagsFromDatabase(bodyPart, category);
        part.AvailableTags[category] = tags;
        return tags;
    }

    private async Task<List<string>> LoadTagsFromDatabase(BodyPartType bodyPart, TagCategory category)
    {
        var results = new List<string>();

        // Define search terms based on body part and category
        var searchTerms = GetSearchTermsForBodyPart(bodyPart, category);

        foreach (var term in searchTerms)
        {
            var searchResults = _tagGraphService.Search(term, 50);
            foreach (var result in searchResults)
            {
                // SearchResult has Text property directly, filtering by actual tags would need node lookup
                if (!results.Contains(result.Text))
                {
                    results.Add(result.Text);
                }
            }
        }

        return results.Distinct().OrderBy(t => t).ToList();
    }

    private List<string> GetSearchTermsForBodyPart(BodyPartType bodyPart, TagCategory category)
    {
        var terms = new List<string>();

        // Base terms for the body part
        var bodyPartTerms = bodyPart switch
        {
            BodyPartType.Hair => new[] { "hair", "hairstyle", "bangs", "ponytail", "twintails" },
            BodyPartType.Eyes => new[] { "eyes", "eye", "eyelashes", "eyebrows", "pupils" },
            BodyPartType.Ears => new[] { "ears", "animal ears", "cat ears", "fox ears" },
            BodyPartType.Face => new[] { "face", "facial", "expression", "mouth", "nose" },
            BodyPartType.Hands => new[] { "hands", "fingers", "gloves", "nails" },
            BodyPartType.UpperTorso => new[] { "chest", "breast", "shirt", "top", "jacket" },
            BodyPartType.LowerTorso => new[] { "waist", "stomach", "abs", "midriff" },
            BodyPartType.Legs => new[] { "legs", "thighs", "pants", "skirt", "stockings" },
            BodyPartType.Feet => new[] { "feet", "shoes", "boots", "socks", "barefoot" },
            BodyPartType.Tail => new[] { "tail", "animal tail", "cat tail", "fox tail" },
            _ => new[] { bodyPart.ToString().ToLower() }
        };

        // Category-specific modifiers
        var categoryModifiers = category switch
        {
            TagCategory.Body => new[] { "anatomy", "body", "physical" },
            TagCategory.Attire => new[] { "clothing", "clothes", "outfit", "wear" },
            TagCategory.BodyAccessories => new[] { "accessories", "jewelry", "ornament" },
            _ => Array.Empty<string>()
        };

        // Combine base terms with category modifiers
        terms.AddRange(bodyPartTerms);

        // Add some category-specific searches
        foreach (var bodyTerm in bodyPartTerms.Take(3)) // Limit to avoid too many searches
        {
            foreach (var modifier in categoryModifiers)
            {
                terms.Add($"{bodyTerm} {modifier}");
            }
        }

        return terms;
    }

    public string GeneratePrompt()
    {
        var sections = new List<string>();

        foreach (var bodyPart in _bodyParts.Values.OrderBy(bp => (int)bp.Type))
        {
            if (bodyPart.SelectedTags.Count > 0)
            {
                var tagString = string.Join(", ", bodyPart.SelectedTags);
                sections.Add($"{bodyPart.DisplayName}: {tagString}");
            }
        }

        return string.Join(", ", sections);
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