namespace PoultryPro.Models.Entities
{
    public class VisitPhoto
    {
        public int Id { get; set; }
        public int VisitId { get; set; }
        public string PhotoType { get; set; } = "General";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public int? FileSizeKb { get; set; }
        public string? Caption { get; set; }
        public DateTime CapturedAt { get; set; }
        public string SyncStatus { get; set; } = "Pending";
    }
}