using System.Text.Json.Serialization;

namespace TagbooruQuest.Models.CharacterDesigner;

public class DesignerConfig
{
    public List<GroupConfig> Groups { get; set; } = new();
}

public class GroupConfig
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public List<PanelConfig> Panels { get; set; } = new();
}

public class PanelConfig
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SelectionMode SelectionMode { get; set; } = SelectionMode.Single;
    public int PromptOrderWeight { get; set; } = 100;
    public List<SourceConfig> Sources { get; set; } = new();
}

public class SourceConfig
{
    public SourceType Type { get; set; }
    public List<string>? DbPath { get; set; }
    public string? FileGlob { get; set; }
    public string? Title { get; set; } // Display title for the source row
    public bool ExpandedByDefault { get; set; } = false; // Whether the source should be expanded by default
}

public enum SelectionMode
{
    Single,
    Multi
}

public enum SourceType
{
    DbQuery,
    FileGlob
}

public class TagOption
{
    public string Display { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string CanonicalTag { get; set; } = string.Empty;
    public bool HasChildren { get; set; } = false;
    public int? NodeId { get; set; } // For database navigation
    public string? GroupIcon { get; set; } // Icon from the parent group
}