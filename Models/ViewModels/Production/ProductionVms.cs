using System.ComponentModel.DataAnnotations;
using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    // ─────────────────────────── Flock register / lifecycle ───────────────────────────

    public class FlockRegisterVm
    {
        public List<Batch> Batches { get; set; } = new();
        public List<Farm> Farms { get; set; } = new();
        public int? FarmId { get; set; }
        public string Status { get; set; } = "active";
        public int ActiveCount { get; set; }
        public int ClosedCount { get; set; }
        public int AllCount { get; set; }
        /// <summary>BatchId → open health issues (overdue vaccinations, active disease cases).</summary>
        public Dictionary<int, FlockHealthFlag> Health { get; set; } = new();
    }

    public class FlockHealthFlag
    {
        public int BatchId { get; set; }
        public int OverdueVaccines { get; set; }
        public int ActiveDiseases { get; set; }
        public int DueSoonVaccines { get; set; }
    }

    public class BatchCloseVm
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public int FarmId { get; set; }
        public string Breed { get; set; } = "";
        public string BatchType { get; set; } = "Broiler";
        public int CurrentCount { get; set; }
        public int AgeInDays { get; set; }
        public decimal TotalFeedKg { get; set; }
        public decimal LivabilityPct { get; set; }
        public DateTime PlacementDate { get; set; }

        public DateTime HarvestDate { get; set; } = DateTime.Today;
        [Range(0, 1_000_000)]
        public int HarvestedCount { get; set; }
        [Range(0.01, 20, ErrorMessage = "Enter the average live weight in kg (e.g. 2.15).")]
        public decimal AvgWeightKg { get; set; }
        [StringLength(300)]
        public string? Notes { get; set; }
        public bool CreateSalesOrder { get; set; }
    }

    public class BatchDetailVm
    {
        public Batch Batch { get; set; } = new();
        public List<DailyFarmRecord> DailyRecords { get; set; } = new();
        public List<BreedStandard> Standard { get; set; } = new();
        public List<VaccinationRecord> Vaccinations { get; set; } = new();
        public List<DiseaseCase> DiseaseCases { get; set; } = new();
        public List<WeightRecord> Weights { get; set; } = new();
        public List<FarmVisit> Visits { get; set; } = new();
        public List<BatchOrderRow> Orders { get; set; } = new();
        public List<EggProductionRecord> EggRecords { get; set; } = new();
        public List<MortalityLog> MortalityLogs { get; set; } = new();
        public decimal ChickCostTotal { get; set; }
        public decimal SalesTotal { get; set; }
        public decimal? StdWeightTodayGm { get; set; }
        public decimal? StdFcrToday { get; set; }
        public decimal? StdCumMortToday { get; set; }
    }

    public class BatchOrderRow
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Product { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "kg";
        public int? BirdCount { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public string Status { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
    }

    // ─────────────────────────── Vaccine programmes ───────────────────────────

    public class VaccineTemplateDetailVm
    {
        public VaccineTemplate Template { get; set; } = new();
        public List<VaccineTemplateItem> Items { get; set; } = new();
        public List<Batch> ActiveBatches { get; set; } = new();
        public VaccineTemplateItem NewItem { get; set; } = new();
    }

    // ─────────────────────────── Disease library ───────────────────────────

    public class DiseaseLibraryIndexVm
    {
        public List<DiseaseLibraryEntry> Entries { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public string? Category { get; set; }
        public string? Q { get; set; }
        public int TotalEntries { get; set; }
    }

    public class DiseaseLibraryDetailVm
    {
        public DiseaseLibraryEntry Entry { get; set; } = new();
        public List<DiseaseCase> Cases { get; set; } = new();
    }

    // ─────────────────────────── Biosecurity ───────────────────────────

    public class BiosecurityIndexVm
    {
        public List<BiosecurityAudit> Audits { get; set; } = new();
        public List<Farm> Farms { get; set; } = new();
        public int? FarmId { get; set; }
        public List<BiosecurityAudit> LatestPerFarm { get; set; } = new();
    }

    public class BiosecurityAnswerVm
    {
        public string Category { get; set; } = "";
        public string Question { get; set; } = "";
        public int Weight { get; set; }
        public bool Passed { get; set; }
        [StringLength(300)]
        public string? Remark { get; set; }
    }

    public class BiosecurityChecklistVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Select a farm.")]
        public int FarmId { get; set; }
        public DateTime AuditDate { get; set; } = DateTime.Today;
        [Required, StringLength(150)]
        public string AuditorName { get; set; } = "";
        [StringLength(1000)]
        public string? Notes { get; set; }
        public List<BiosecurityAnswerVm> Items { get; set; } = new();
    }

    public class BiosecurityCategoryScore
    {
        public string Category { get; set; } = "";
        public int Score { get; set; }
        public int Max { get; set; }
        public decimal Pct => Max > 0 ? Math.Round((decimal)Score / Max * 100, 1) : 0;
        public decimal? PreviousPct { get; set; }
    }

    public class BiosecurityDetailVm
    {
        public BiosecurityAudit Audit { get; set; } = new();
        public List<BiosecurityAuditItem> Items { get; set; } = new();
        public BiosecurityAudit? Previous { get; set; }
        public List<BiosecurityAuditItem> PreviousItems { get; set; } = new();
        public List<BiosecurityCategoryScore> Categories { get; set; } = new();
    }

    // ─────────────────────────── Egg production ───────────────────────────

    public class EggProductionIndexVm
    {
        public List<Batch> LayerBatches { get; set; } = new();
        public int? BatchId { get; set; }
        public Batch? Batch { get; set; }
        public List<EggProductionRecord> Records { get; set; } = new();
        public EggProductionRecord? Latest { get; set; }
        public decimal AvgHenDay7 { get; set; }
        public decimal PeakHenDay { get; set; }
        public int Eggs30 { get; set; }
        public decimal FeedPerDozen30 { get; set; }
    }

    public class EggProductionFormVm
    {
        public int Id { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Select a layer flock.")]
        public int BatchId { get; set; }
        public DateTime RecordDate { get; set; } = DateTime.Today;
        [Range(1, 1_000_000)]
        public int HenCount { get; set; }
        [Range(0, 1_000_000)]
        public int TotalEggs { get; set; }
        [Range(0, 1_000_000)]
        public int GradeA { get; set; }
        [Range(0, 1_000_000)]
        public int GradeB { get; set; }
        [Range(0, 1_000_000)]
        public int CrackedEggs { get; set; }
        [Range(0, 1_000_000)]
        public int DirtyEggs { get; set; }
        [Range(0, 120)]
        public decimal? AvgEggWeightGm { get; set; }
        [Range(0, 100_000)]
        public decimal? FeedKg { get; set; }
        [StringLength(300)]
        public string? Notes { get; set; }
    }

    // ─────────────────────────── Feed planner ───────────────────────────

    public class FeedPlanRow
    {
        public Batch Batch { get; set; } = new();
        public int AgeDays { get; set; }
        public int Birds { get; set; }
        public FeedFormulation? Formulation { get; set; }
        public decimal FeedPerBirdGm { get; set; }
        public string FeedBasis { get; set; } = "";
        public decimal DailyKg => Math.Round(Birds * FeedPerBirdGm / 1000m, 1);
        public decimal WeeklyKg => Math.Round(DailyKg * 7, 0);
        public decimal CostPerDay => Formulation == null ? 0 : Math.Round(DailyKg * Formulation.CostPerKg, 0);
        public int? DaysToNextStage { get; set; }
        public FeedFormulation? NextFormulation { get; set; }
    }

    public class FeedPlanTotal
    {
        public string Formulation { get; set; } = "";
        public string Stage { get; set; } = "";
        public int Flocks { get; set; }
        public int Birds { get; set; }
        public decimal DailyKg { get; set; }
        public decimal WeeklyKg { get; set; }
        public decimal CostPerDay { get; set; }
        public decimal WeeklyBags50 => Math.Ceiling(WeeklyKg / 50m);
    }

    public class FeedPlannerVm
    {
        public List<FeedPlanRow> Rows { get; set; } = new();
        public List<FeedPlanTotal> Totals { get; set; } = new();
    }

    // ─────────────────────────── Health ───────────────────────────

    public class VaccinationComplianceRow
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public int FarmId { get; set; }
        public int DueToDate { get; set; }
        public int Done { get; set; }
        public int OnTime { get; set; }
        public int Overdue { get; set; }
        public int Upcoming { get; set; }
        public decimal CompliancePct => DueToDate > 0 ? Math.Round((decimal)Done / DueToDate * 100, 1) : 100;
        public decimal OnTimePct => Done > 0 ? Math.Round((decimal)OnTime / Done * 100, 1) : 0;
    }

    public class HealthReportVm
    {
        public List<VaccinationComplianceRow> FarmCompliance { get; set; } = new();
        public List<HealthDiseaseRow> DiseaseByName { get; set; } = new();
        public List<HealthDiseaseFarmRow> DiseaseByFarm { get; set; } = new();
        public List<HealthCauseRow> MortalityCauses { get; set; } = new();
        public List<HealthMonthRow> MonthlyMortality { get; set; } = new();
        public int TotalCases { get; set; }
        public int ActiveCases { get; set; }
        public int TotalDeathsLogged { get; set; }
        public decimal OverallCompliance { get; set; }
    }

    public class HealthDiseaseRow
    {
        public string DiseaseName { get; set; } = "";
        public int Cases { get; set; }
        public int Affected { get; set; }
        public int Deaths { get; set; }
        public int Active { get; set; }
        public decimal FatalityPct => Affected > 0 ? Math.Round((decimal)Deaths / Affected * 100, 1) : 0;
    }

    public class HealthDiseaseFarmRow
    {
        public string FarmName { get; set; } = "";
        public string DiseaseName { get; set; } = "";
        public int Cases { get; set; }
        public int Affected { get; set; }
        public int Deaths { get; set; }
    }

    public class HealthCauseRow
    {
        public string Cause { get; set; } = "";
        public int Birds { get; set; }
        public int Events { get; set; }
    }

    public class HealthMonthRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Deaths { get; set; }
        public int Culls { get; set; }
        public long BirdDays { get; set; }
        public string Label => new DateTime(Year, Month, 1).ToString("MMM yyyy");
        /// <summary>Deaths per 1,000 bird-days.</summary>
        public decimal RatePer1000 => BirdDays > 0 ? Math.Round(Deaths * 1000m / BirdDays, 3) : 0;
    }
}
