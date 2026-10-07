namespace PoultryPro.Models.Entities
{
    public class MortalityLog
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public int FarmId { get; set; }
        public DateTime LogDate { get; set; }
        public int Count { get; set; }
        public string Cause { get; set; } = "";
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
