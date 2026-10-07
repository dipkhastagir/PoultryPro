namespace PoultryPro.Models.ViewModels
{
    public class SearchHit
    {
        public string Group { get; set; } = "";
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public string? Badge { get; set; }
        public string Url { get; set; } = "#";
    }

    public class SearchGroup
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Icon { get; set; } = "bi-search";
        public string Tone { get; set; } = "slate";
        public List<SearchHit> Hits { get; set; } = new();
    }

    public class SearchResultVm
    {
        public string Query { get; set; } = "";
        public List<SearchGroup> Groups { get; set; } = new();
        public int Total => Groups.Sum(g => g.Hits.Count);
        public bool TooShort { get; set; }
    }
}
