namespace PoultryPro.Models.Entities
{
    public class WeightRecord
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public DateTime RecordDate { get; set; }
        public int AgeInDays { get; set; }
        public decimal AvgWeightKg { get; set; }
        public int SampleSize { get; set; }
        /// <summary>Flock uniformity: % of sampled birds within ±10% of the mean weight.</summary>
        public decimal? UniformityPct { get; set; }
    }
}
