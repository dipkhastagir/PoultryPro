using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    // ───────────────────────── field trials ─────────────────────────

    public class TrialMetricRow
    {
        public string Metric { get; set; } = "";
        public string Unit { get; set; } = "";
        public double? Treatment { get; set; }
        public double? Control { get; set; }
        public bool LowerIsBetter { get; set; }
        public int Decimals { get; set; } = 2;
        public bool IsPrimary { get; set; }
        public double? Diff => Treatment.HasValue && Control.HasValue ? Treatment - Control : null;
        public double? DiffPct => Treatment.HasValue && Control.HasValue && Control.Value != 0 ? (Treatment - Control) / Math.Abs(Control.Value) * 100 : null;
        /// <summary>Positive = treatment better than control.</summary>
        public double? ImprovementPct => DiffPct.HasValue ? (LowerIsBetter ? -DiffPct : DiffPct) : null;
    }

    public class TrialTestResult
    {
        public string Variable { get; set; } = "";
        public string Unit { get; set; } = "";
        public int NTreatment { get; set; }
        public int NControl { get; set; }
        public double MeanTreatment { get; set; }
        public double MeanControl { get; set; }
        public double SdTreatment { get; set; }
        public double SdControl { get; set; }
        public double T { get; set; }
        public double Df { get; set; }
        public double P { get; set; } = 1;
        public double CohensD { get; set; }
        public bool Significant => P < 0.05 && NTreatment >= 2 && NControl >= 2;
        public string Interpretation { get; set; } = "";
    }

    public class TrialDetailVm
    {
        public FieldTrial Trial { get; set; } = new();
        public Batch? Treatment { get; set; }
        public Batch? Control { get; set; }
        public List<TrialMetricRow> Metrics { get; set; } = new();
        public List<TrialTestResult> Tests { get; set; } = new();
        public List<int> Ages { get; set; } = new();
        public List<double?> WeightT { get; set; } = new();
        public List<double?> WeightC { get; set; } = new();
        public List<double?> CumMortT { get; set; } = new();
        public List<double?> CumMortC { get; set; } = new();
        public double? HypothesisThreshold { get; set; }
        public bool HypothesisIsPoints { get; set; }
        public double? ObservedEffect { get; set; }
        public string HypothesisVerdict { get; set; } = "";
        public string DraftConclusion { get; set; } = "";
        public string PrimaryDirection { get; set; } = "";
    }

    public class TrialFormVm
    {
        public FieldTrial Trial { get; set; } = new();
        public List<AnalyticsBatchOption> Batches { get; set; } = new();
        public bool IsEdit { get; set; }
        public static readonly string[] Metrics = { "FCR", "EPEF", "Weight", "ADG", "Mortality", "Livability" };
        public static readonly string[] Statuses = { "Planned", "Running", "Completed", "Abandoned" };
    }

    // ───────────────────────── research data ─────────────────────────

    public class ResearchDataset
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "bi-table";
        public string Description { get; set; } = "";
        public string Source { get; set; } = "";
        public string Unit { get; set; } = "";
        public int Rows { get; set; }
        public bool FarmFilter { get; set; } = true;
        public bool DateFilter { get; set; } = true;
    }

    public class ResearchIndexVm
    {
        public List<ResearchDataset> Datasets { get; set; } = new();
        public List<Farm> Farms { get; set; } = new();
        public DateTime? FirstRecord { get; set; }
        public DateTime? LastRecord { get; set; }
        public int Batches { get; set; }
        public int FarmCount { get; set; }
    }

    public class DictColumn
    {
        public string TableName { get; set; } = "";
        public string TableType { get; set; } = "";
        public string ColumnName { get; set; } = "";
        public int OrdinalPosition { get; set; }
        public string DataType { get; set; } = "";
        public int? CharacterMaximumLength { get; set; }
        public byte? NumericPrecision { get; set; }
        public int? NumericScale { get; set; }
        public string IsNullable { get; set; } = "YES";
        public string? ColumnDefault { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsComputed { get; set; }

        public string TypeText
        {
            get
            {
                var t = DataType.ToLowerInvariant();
                if (t is "nvarchar" or "varchar" or "char" or "nchar")
                    return $"{t}({(CharacterMaximumLength == -1 ? "max" : CharacterMaximumLength?.ToString())})";
                if (t is "decimal" or "numeric") return $"{t}({NumericPrecision},{NumericScale})";
                return t;
            }
        }
    }

    public class DictTable
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Module { get; set; } = "";
        public string Description { get; set; } = "";
        public long? RowCount { get; set; }
        public List<DictColumn> Columns { get; set; } = new();
    }

    public class CorrelationVm
    {
        public List<string> Variables { get; set; } = new();
        public List<string> Units { get; set; } = new();
        public double?[,] R { get; set; } = new double?[0, 0];
        public int[,] N { get; set; } = new int[0, 0];
        public double[,] P { get; set; } = new double[0, 0];
        public int Observations { get; set; }
        public string? BatchType { get; set; }
        public int? FarmId { get; set; }
        public List<Farm> Farms { get; set; } = new();
        public List<(string A, string B, double R, int N)> Strongest { get; set; } = new();
    }
}

namespace PoultryPro.Models.ViewModels
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>Validated form for one breed-standard point.</summary>
    public class BreedStandardFormVm
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Breed { get; set; } = "";

        [Range(0, 600), Display(Name = "Age (days)")]
        public int AgeDay { get; set; }

        [Range(1, 10000), Display(Name = "Target body weight (g)")]
        public decimal TargetWeightGm { get; set; }

        [Range(0, 1000), Display(Name = "Daily feed (g/bird/day)")]
        public decimal DailyFeedGm { get; set; }

        [Range(0, 100000), Display(Name = "Cumulative feed (g/bird)")]
        public decimal CumFeedGm { get; set; }

        [Range(0, 10), Display(Name = "Target FCR")]
        public decimal TargetFCR { get; set; }

        [Range(0, 100), Display(Name = "Target cumulative mortality (%)")]
        public decimal TargetCumMortPct { get; set; }

        public List<string> KnownBreeds { get; set; } = new();

        public PoultryPro.Models.Entities.BreedStandard ToEntity() => new()
        {
            Id = Id, Breed = Breed, AgeDay = AgeDay, TargetWeightGm = TargetWeightGm, DailyFeedGm = DailyFeedGm,
            CumFeedGm = CumFeedGm, TargetFCR = TargetFCR, TargetCumMortPct = TargetCumMortPct
        };

        public static BreedStandardFormVm From(PoultryPro.Models.Entities.BreedStandard s) => new()
        {
            Id = s.Id, Breed = s.Breed, AgeDay = s.AgeDay, TargetWeightGm = s.TargetWeightGm, DailyFeedGm = s.DailyFeedGm,
            CumFeedGm = s.CumFeedGm, TargetFCR = s.TargetFCR, TargetCumMortPct = s.TargetCumMortPct
        };
    }
}
