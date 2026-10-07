namespace PoultryPro.Models.Entities
{
    public class DiseaseCase
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public int FarmId { get; set; }
        public string DiseaseName { get; set; } = "";
        public DateTime ReportedDate { get; set; }
        public int AffectedCount { get; set; }
        public int DeathCount { get; set; }
        public string Treatment { get; set; } = "";
        public string Status { get; set; } = "Active";
        public DateTime? ResolvedDate { get; set; }
        public string? DiagnosedBy { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        /// <summary>Case fatality rate (% of affected birds that died).</summary>
        public decimal CaseFatalityPct => AffectedCount > 0 ? Math.Round((decimal)DeathCount / AffectedCount * 100, 1) : 0;
        public int DurationDays => ((ResolvedDate ?? DateTime.Today) - ReportedDate).Days;
    }
}
