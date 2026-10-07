using System.ComponentModel.DataAnnotations;

namespace PoultryPro.Models.Entities
{
    /// <summary>Controlled field trial: a treatment flock compared with a control flock (dbo.FieldTrials).</summary>
    public class FieldTrial
    {
        public int Id { get; set; }

        [Required, StringLength(30), Display(Name = "Trial code")]
        public string TrialCode { get; set; } = "";

        [Required, StringLength(200)]
        public string Title { get; set; } = "";

        [StringLength(1000)]
        public string? Objective { get; set; }

        [StringLength(1000)]
        public string? Hypothesis { get; set; }

        [StringLength(1000)]
        public string? Intervention { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Choose the treatment flock."), Display(Name = "Treatment batch")]
        public int TreatmentBatchId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Choose the control flock."), Display(Name = "Control batch")]
        public int ControlBatchId { get; set; }

        [Required, StringLength(50), Display(Name = "Primary metric")]
        public string PrimaryMetric { get; set; } = "FCR";

        [Required, DataType(DataType.Date), Display(Name = "Start date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date), Display(Name = "End date")]
        public DateTime? EndDate { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = "Running";

        public string? Conclusion { get; set; }

        [StringLength(150), Display(Name = "Principal investigator")]
        public string? PrincipalInvestigator { get; set; }

        public DateTime CreatedAt { get; set; }

        // joined (read-only) columns
        public string? TreatmentBatchCode { get; set; }
        public string? ControlBatchCode { get; set; }
        public string? TreatmentBreed { get; set; }
        public string? ControlBreed { get; set; }
        public string? TreatmentFarmName { get; set; }
        public string? ControlFarmName { get; set; }
    }

    /// <summary>One aggregated batch-day observation from dbo.DailyFarmRecords (analytics input row).</summary>
    public class BatchDayRecord
    {
        public int BatchId { get; set; }
        public int FarmId { get; set; }
        public DateTime RecordDate { get; set; }
        public int AgeInDays { get; set; }
        public int BirdCount { get; set; }
        public int MortalityCount { get; set; }
        public int CullingCount { get; set; }
        public decimal FeedConsumptionKg { get; set; }
        public decimal WaterConsumptionLtr { get; set; }
        public decimal? TemperatureC { get; set; }
        public decimal? HumidityPercent { get; set; }
        public decimal? AverageWeightGm { get; set; }

        /// <summary>Birds alive at the start of the day (closing count + losses of the day).</summary>
        public int StartCount => BirdCount + MortalityCount + CullingCount;

        /// <summary>Daily mortality as % of birds alive at the start of the day.</summary>
        public double MortPct => StartCount > 0 ? MortalityCount * 100.0 / StartCount : 0;

        /// <summary>Feed intake per bird (g/bird/day).</summary>
        public double FeedPerBirdGm => BirdCount > 0 ? (double)FeedConsumptionKg * 1000.0 / BirdCount : 0;

        public double? Thi => TemperatureC.HasValue && HumidityPercent.HasValue
            ? (double)Infrastructure.PoultryMath.THI(TemperatureC.Value, HumidityPercent.Value) : null;
    }

    /// <summary>Row of dbo.vw_FarmBenchmark.</summary>
    public class FarmBenchmarkRow
    {
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string? Location { get; set; }
        public string? FarmType { get; set; }
        public int Capacity { get; set; }
        public int TotalBatches { get; set; }
        public int ActiveBatches { get; set; }
        public int CurrentBirds { get; set; }
        public decimal AvgMortalityRate { get; set; }
        public decimal AvgFCR { get; set; }
        public decimal AvgEPEF { get; set; }
        public decimal? LastBiosecurityPct { get; set; }
    }
}
