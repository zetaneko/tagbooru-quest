using TagbooruQuest.Models.CharacterDesigner;
using SelectionMode = TagbooruQuest.Models.CharacterDesigner.SelectionMode;

namespace TagbooruQuest.Services.CharacterDesigner;

public interface ICharacterBuildState
{
    void SetSelection(string panelKey, int promptOrderWeight, Models.CharacterDesigner.SelectionMode mode, IEnumerable<TagOption> options);
    void ToggleSelection(string panelKey, int promptOrderWeight, Models.CharacterDesigner.SelectionMode mode, TagOption option);
    void RemoveSelection(string panelKey, string canonicalTag);
    List<TagOption> GetSelections(string panelKey);
    List<TagOption> GetAllSelections();
    string BuildPrompt();
    void Reset();
    event Action? OnStateChanged;
}

public class CharacterBuildState : ICharacterBuildState
{
    private readonly Dictionary<string, PanelSelection> _selections = new();

    public event Action? OnStateChanged;

    public CharacterBuildState()
    {
    }

    public void SetSelection(string panelKey, int promptOrderWeight, Models.CharacterDesigner.SelectionMode mode, IEnumerable<TagOption> options)
    {
        _selections[panelKey] = new PanelSelection
        {
            PanelKey = panelKey,
            PromptOrderWeight = promptOrderWeight,
            SelectionMode = mode,
            Options = options.ToList()
        };

        OnStateChanged?.Invoke();
    }

    public void ToggleSelection(string panelKey, int promptOrderWeight, Models.CharacterDesigner.SelectionMode mode, TagOption option)
    {
        if (!_selections.TryGetValue(panelKey, out var selection))
        {
            selection = new PanelSelection
            {
                PanelKey = panelKey,
                PromptOrderWeight = promptOrderWeight,
                SelectionMode = mode,
                Options = new List<TagOption>()
            };
            _selections[panelKey] = selection;
        }

        if (mode == SelectionMode.Single)
        {
            // Single mode: replace selection
            var isCurrentlySelected = selection.Options.Any(o => o.CanonicalTag == option.CanonicalTag);
            if (isCurrentlySelected)
            {
                selection.Options.Clear(); // Deselect
            }
            else
            {
                selection.Options.Clear();
                selection.Options.Add(option); // Select new
            }
        }
        else
        {
            // Multi mode: toggle selection
            var existingOption = selection.Options.FirstOrDefault(o => o.CanonicalTag == option.CanonicalTag);
            if (existingOption != null)
            {
                selection.Options.Remove(existingOption);
            }
            else
            {
                selection.Options.Add(option);
            }
        }

        OnStateChanged?.Invoke();
    }

    public List<TagOption> GetSelections(string panelKey)
    {
        return _selections.TryGetValue(panelKey, out var selection) ? selection.Options : new List<TagOption>();
    }

    public List<TagOption> GetAllSelections()
    {
        return _selections.Values
            .OrderBy(s => s.PromptOrderWeight)
            .SelectMany(s => s.Options)
            .GroupBy(o => o.CanonicalTag)
            .Select(g => g.First())
            .ToList();
    }

    public string BuildPrompt()
    {
        var allSelections = GetAllSelections();
        var displays = allSelections.Select(o => o.Display);
        return string.Join(" ", displays);
    }

    public void RemoveSelection(string panelKey, string canonicalTag)
    {
        if (_selections.TryGetValue(panelKey, out var selection))
        {
            var optionToRemove = selection.Options.FirstOrDefault(o => o.CanonicalTag == canonicalTag);
            if (optionToRemove != null)
            {
                selection.Options.Remove(optionToRemove);

                // If this panel has no more selections, remove the entire panel entry
                if (selection.Options.Count == 0)
                {
                    _selections.Remove(panelKey);
                }

                OnStateChanged?.Invoke();
            }
        }
    }

    public void Reset()
    {
        _selections.Clear();
        OnStateChanged?.Invoke();
    }

    private class PanelSelection
    {
        public string PanelKey { get; set; } = string.Empty;
        public int PromptOrderWeight { get; set; }
        public SelectionMode SelectionMode { get; set; }
        public List<TagOption> Options { get; set; } = new();
    }
}