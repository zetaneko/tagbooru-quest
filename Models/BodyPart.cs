namespace TagbooruQuest.Models;

public enum BodyPartType
{
    Head,
    Hair,
    Ears,
    Eyes,
    Face,
    Neck,
    Shoulders,
    Arms,
    Hands,
    UpperTorso,
    LowerTorso,
    Hips,
    Legs,
    Feet,
    Tail
}

public class BodyPart
{
    public BodyPartType Type { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<string> SelectedTags { get; set; } = new();
    public Dictionary<TagCategory, List<string>> AvailableTags { get; set; } = new();
}

public enum TagCategory
{
    Body,
    Attire,
    BodyAccessories
}

public class PromptSection
{
    public BodyPartType BodyPart { get; set; }
    public string SectionName { get; set; } = "";
    public List<string> Tags { get; set; } = new();
}