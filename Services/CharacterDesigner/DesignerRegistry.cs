using System.Text.Json;
using TagbooruQuest.Models.CharacterDesigner;
using TagbooruQuest.Data;

namespace TagbooruQuest.Services.CharacterDesigner;

public interface IDesignerRegistry
{
    DesignerConfig Config { get; }
    GroupConfig? GetGroup(string key);
    PanelConfig? GetPanel(string groupKey, string panelKey);
    Task ValidateConfigurationAsync();
}

public class DesignerRegistry : IDesignerRegistry
{
    private readonly DesignerConfig _config;
    private readonly TagGraphService _tagGraphService;

    public DesignerConfig Config => _config;

    public DesignerRegistry(TagGraphService tagGraphService)
    {
        _tagGraphService = tagGraphService;
        _config = LoadConfiguration();
    }

    public GroupConfig? GetGroup(string key)
    {
        return _config.Groups.FirstOrDefault(g => g.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public PanelConfig? GetPanel(string groupKey, string panelKey)
    {
        var group = GetGroup(groupKey);
        return group?.Panels.FirstOrDefault(p => p.Key.Equals(panelKey, StringComparison.OrdinalIgnoreCase));
    }

    public async Task ValidateConfigurationAsync()
    {
        var errors = new List<string>();

        // Check for unique keys
        var groupKeys = _config.Groups.Select(g => g.Key).ToList();
        var duplicateGroups = groupKeys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var duplicate in duplicateGroups)
        {
            errors.Add($"Duplicate group key: {duplicate}");
        }

        // Check panels
        foreach (var group in _config.Groups)
        {
            var panelKeys = group.Panels.Select(p => p.Key).ToList();
            var duplicatePanels = panelKeys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var duplicate in duplicatePanels)
            {
                errors.Add($"Duplicate panel key in group {group.Key}: {duplicate}");
            }

            // Validate DB queries
            foreach (var panel in group.Panels)
            {
                foreach (var source in panel.Sources.Where(s => s.Type == SourceType.DbQuery))
                {
                    if (source.DbPath == null || source.DbPath.Count == 0)
                    {
                        errors.Add($"Empty DbPath in panel {group.Key}.{panel.Key}");
                        continue;
                    }

                    try
                    {
                        var results = await GetDbQueryResultsAsync(source.DbPath);
                        if (results.Count == 0)
                        {
                            Console.WriteLine($"Warning: DbQuery in {group.Key}.{panel.Key} returned zero results for path: {string.Join("/", source.DbPath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"DbQuery failed in {group.Key}.{panel.Key}: {ex.Message}");
                    }
                }
            }
        }

        if (errors.Any())
        {
            throw new InvalidOperationException($"Configuration validation failed:\n{string.Join("\n", errors)}");
        }
    }

    private DesignerConfig LoadConfiguration()
    {

        try
        {
            string json;
            using (var stream = FileSystem.OpenAppPackageFileAsync("character-designer-config.json").Result)
            using (var reader = new StreamReader(stream))
            {
                json = reader.ReadToEnd();
            }


            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            var config = JsonSerializer.Deserialize<DesignerConfig>(json, options);
            return config ?? new DesignerConfig();
        }
        catch (Exception ex)
        {
            return CreateDefaultConfiguration();
        }
    }

    private async Task<List<Node>> GetDbQueryResultsAsync(List<string> path)
    {
        var results = new List<Node>();

        // Navigate to the target node using the path
        Node? currentNode = null;

        for (int i = 0; i < path.Count; i++)
        {
            if (currentNode == null)
            {
                // Find root node - ALWAYS prefer parent nodes over leaf nodes
                var searchResults = _tagGraphService.Search(path[i], 100);
                var allCandidates = searchResults
                    .Select(r => _tagGraphService.GetNodeById(r.Id))
                    .Where(n => n != null && IsExactMatch(n.Text, path[i]))
                    .Cast<Node>()
                    .ToList();

                currentNode = SelectBestCandidate(allCandidates);
            }
            else
            {
                // Find child node - ALWAYS prefer parent nodes over leaf nodes
                var children = _tagGraphService.GetChildren(currentNode.Id);
                var allCandidates = children.Where(c => IsExactMatch(c.Text, path[i])).ToList();

                currentNode = SelectBestCandidate(allCandidates);
            }

            if (currentNode == null)
            {
                return results; // Path not found
            }
        }

        // Get leaf tags from the final node
        if (currentNode != null)
        {
            var children = _tagGraphService.GetChildren(currentNode.Id);
            results.AddRange(children.Where(c => c.IsTag));
        }

        return results;
    }


    private bool IsExactMatch(string nodeText, string searchTerm)
    {
        return nodeText.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               nodeText.Replace(" ", "_").Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               nodeText.Replace("_", " ").Equals(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    private Node? SelectBestCandidate(List<Node> candidates)
    {
        if (!candidates.Any()) return null;
        if (candidates.Count == 1) return candidates[0];

        // ALWAYS prefer nodes that have children over leaf nodes
        var nodesWithChildren = candidates.Where(c => _tagGraphService.GetChildren(c.Id).Any()).ToList();
        if (nodesWithChildren.Any())
        {
            return nodesWithChildren.First(); // Return the first node that has children
        }

        // Only return leaf nodes if no parent nodes exist
        return candidates.First();
    }

    private DesignerConfig CreateDefaultConfiguration()
    {
        return new DesignerConfig
        {
            Groups = new List<GroupConfig>
            {
                new GroupConfig
                {
                    Key = "body",
                    Title = "Body",
                    Icon = "👤",
                    Panels = new List<PanelConfig>
                    {
                        new PanelConfig
                        {
                            Key = "body_type",
                            Title = "Body Type",
                            SelectionMode = Models.CharacterDesigner.SelectionMode.Single,
                            PromptOrderWeight = 10,
                            Sources = new List<SourceConfig>
                            {
                                new SourceConfig
                                {
                                    Type = SourceType.DbQuery,
                                    DbPath = new List<string> { "body", "body_type" }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}