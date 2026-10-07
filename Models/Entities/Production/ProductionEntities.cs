using System.ComponentModel.DataAnnotations;

namespace PoultryPro.Models.Entities
{
    /// <summary>Vaccination programme template (dbo.VaccineScheduleTemplates).</summary>
    public class VaccineTemplate
    {
        public int Id { get; set; }
        [Required, StringLength(150)]
        public string Name { get; set; } = "";
        [Required, StringLength(30)]
        public string BatchType { get; set; } = "Broiler";
        [StringLength(500)]
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        // computed in queries
        public int ItemCount { get; set; }
        public int? FirstAgeDay { get; set; }
        public int? LastAgeDay { get; set; }
    }

    /// <summary>One vaccine on a programme (dbo.VaccineScheduleItems).</summary>
    public class VaccineTemplateItem
    {
        public int Id { get; set; }
        public int TemplateId { get; set; }
        [Range(0, 1000)]
        public int AgeDay { get; set; }
        [Required, StringLength(150)]
        public string VaccineName { get; set; } = "";
        [StringLength(50)]
        public string? Route { get; set; }
        public decimal? DoseMl { get; set; }
        [StringLength(300)]
        public string? Notes { get; set; }
    }

    /// <summary>Knowledge-base disease entry (dbo.DiseaseLibrary).</summary>
    public class DiseaseLibraryEntry
    {
        public int Id { get; set; }
        [Required, StringLength(150)]
        public string Name { get; set; } = "";
        [StringLength(150)]
        public string? LocalName { get; set; }
        [Required, StringLength(50)]
        public string Category { get; set; } = "Viral";
        [Required, StringLength(20)]
        public string Severity { get; set; } = "Medium";
        public bool IsZoonotic { get; set; }
        public bool IsNotifiable { get; set; }
        public string? Symptoms { get; set; }
        public string? Transmission { get; set; }
        public string? Prevention { get; set; }
        public string? Treatment { get; set; }
        [StringLength(100)]
        public string? AgeGroup { get; set; }
        [StringLength(50)]
        public string? MortalityRange { get; set; }
        public DateTime CreatedAt { get; set; }

        // computed
        public int CaseCount { get; set; }
        public int ActiveCaseCount { get; set; }
    }

    /// <summary>Farm biosecurity audit header (dbo.BiosecurityAudits).</summary>
    public class BiosecurityAudit
    {
        public int Id { get; set; }
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public DateTime AuditDate { get; set; }
        public string AuditorName { get; set; } = "";
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public decimal ScorePct { get; set; }
        public string Grade { get; set; } = "C";
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public int FailedCount { get; set; }
    }

    /// <summary>One checklist answer on an audit (dbo.BiosecurityAuditItems).</summary>
    public class BiosecurityAuditItem
    {
        public int Id { get; set; }
        public int AuditId { get; set; }
        public string Category { get; set; } = "";
        public string Question { get; set; } = "";
        public int Weight { get; set; } = 1;
        public bool Passed { get; set; }
        public string? Remark { get; set; }
    }

    /// <summary>Daily layer production record (dbo.EggProductionRecords / vw_EggProductionSummary).</summary>
    public class EggProductionRecord
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string Breed { get; set; } = "";
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public DateTime RecordDate { get; set; }
        public int AgeWeeks { get; set; }
        public int HenCount { get; set; }
        public int TotalEggs { get; set; }
        public int GradeA { get; set; }
        public int GradeB { get; set; }
        public int CrackedEggs { get; set; }
        public int DirtyEggs { get; set; }
        public decimal? AvgEggWeightGm { get; set; }
        public decimal? FeedKg { get; set; }
        public decimal HenDayPct { get; set; }
        public string? Notes { get; set; }
        public decimal FeedPerDozenGm { get; set; }
        public decimal CrackedPct { get; set; }
    }

    /// <summary>Ration / feed formulation (dbo.FeedFormulations).</summary>
    public class FeedFormulation
    {
        public int Id { get; set; }
        [Required, StringLength(150)]
        public string Name { get; set; } = "";
        [Required, StringLength(30)]
        public string Stage { get; set; } = "Starter";
        [Required, StringLength(30)]
        public string BatchType { get; set; } = "Broiler";
        [Range(0, 1000)]
        public int FromAgeDay { get; set; }
        [Range(0, 1000)]
        public int ToAgeDay { get; set; }
        [Range(0, 60)]
        public decimal CrudeProteinPct { get; set; }
        [Range(0, 5000)]
        public int EnergyKcalPerKg { get; set; }
        public decimal? CalciumPct { get; set; }
        public decimal? PhosphorusPct { get; set; }
        public decimal? LysinePct { get; set; }
        [Range(0, 100000)]
        public decimal CostPerKg { get; set; }
        public string? Ingredients { get; set; }
        [StringLength(500)]
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        /// <summary>Energy-to-protein ratio (kcal ME per % CP) — common ration-balance indicator.</summary>
        public decimal EnergyProteinRatio => CrudeProteinPct > 0 ? Math.Round(EnergyKcalPerKg / CrudeProteinPct, 1) : 0;
        /// <summary>Calcium : available phosphorus ratio.</summary>
        public decimal? CaPRatio => CalciumPct.HasValue && PhosphorusPct is > 0 ? Math.Round(CalciumPct.Value / PhosphorusPct.Value, 2) : null;
    }
}
