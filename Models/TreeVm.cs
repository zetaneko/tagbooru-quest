namespace DanbooruTaggingUI.Models
{
    public class TreeVm
    {
        public int Id { get; set; }
        public string Slug { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsTag { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsLoaded { get; set; }
        public List<TreeVm> Children { get; set; } = new();
    }
}