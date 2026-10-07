namespace PoultryPro.Models.DTO
{
    public class FarmBatchSummary
    {
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string Location { get; set; } = "";
        public int TotalBatches { get; set; }
        public int ActiveBatches { get; set; }
        public int TotalBirds { get; set; }
        public decimal AvgMortalityRate { get; set; }
        public DateTime? LastPlacementDate { get; set; }
    }
}