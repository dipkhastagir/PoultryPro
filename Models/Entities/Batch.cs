namespace PoultryPro.Models.Entities
{
    public class Batch
    {
        public int Id { get; set; }
        public string BatchCode { get; set; } = "";
        public string Breed { get; set; } = "";
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string FarmLocation { get; set; } = "";
        public int InitialCount { get; set; }
        public int CurrentCount { get; set; }
        public int MortalityCount { get; set; }
        public DateTime PlacementDate { get; set; }
        public int CullingCount { get; set; }
        public string? Notes { get; set; }
        public int TotalVisits { get; set; }
        public DateTime? ExpectedHarvestDate { get; set; }
        public int AgeInDays { get; set; }
        public string HealthStatus { get; set; } = "Healthy";
        public decimal AverageWeightKg { get; set; }
        public decimal TotalFeedKg { get; set; }
        public decimal MortalityRate { get; set; }
        public decimal FCR { get; set; }
        public bool IsActive { get; set; } = true;

        public string BatchType { get; set; } = "Broiler";

        public DateTime? LastVisitDate { get; set; }

        // ── extended fields (vw_BatchDetails / vw_BatchPerformance) ──
        public string? FarmType { get; set; }
        public string? ShedNo { get; set; }
        public string? HatcheryName { get; set; }
        public decimal ChickCostPerBird { get; set; }
        public int HarvestedCount { get; set; }
        public decimal HarvestWeightKg { get; set; }
        public DateTime? ClosedDate { get; set; }
        public decimal LivabilityPct { get; set; }
        public decimal LiveMassKg { get; set; }
        public decimal EPEF { get; set; }
        public decimal AvgDailyGainGm { get; set; }
        public decimal AvgFeedPerBirdDayGm { get; set; }

        // ── traceability seal (head of the event hash chain at sealing time) ──
        public string? TraceSealHash { get; set; }
        public DateTime? TraceSealedAt { get; set; }
        public string? TraceSealedBy { get; set; }
    }
}