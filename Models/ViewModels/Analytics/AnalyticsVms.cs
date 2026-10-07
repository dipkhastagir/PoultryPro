using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    // ───────────────────────── shared helpers ─────────────────────────

    /// <summary>Small UI helpers shared by the analytics views.</summary>
    public static class AnalyticsUi
    {
        public static string EpefBand(decimal epef) => epef > 400 ? "Excellent" : epef >= 300 ? "Good" : "Poor";
        public static string EpefBadge(decimal epef) => epef > 400 ? "badge-healthy" : epef >= 300 ? "badge-ready" : "badge-danger-soft";
        public static string EpefColor(decimal epef) => epef > 400 ? "#0d9e75" : epef >= 300 ? "#3b82f6" : "#ef4444";

        public static string HeatBadge(string band) => band switch
        {
            "Normal" => "badge-healthy",
            "Alert" => "badge-warning-soft",
            "Danger" or "Emergency" => "badge-danger-soft",
            _ => "badge-gray"
        };

        public static string RiskLevel(double score) => score >= 65 ? "Critical" : score >= 45 ? "High" : score >= 25 ? "Moderate" : "Low";
        public static string RiskBadge(double score) => score >= 65 ? "badge-danger-soft" : score >= 45 ? "badge-warning-soft" : score >= 25 ? "badge-info-soft" : "badge-healthy";
        public static string RiskColor(double score) => score >= 65 ? "#ef4444" : score >= 45 ? "#f59e0b" : score >= 25 ? "#3b82f6" : "#0d9e75";

        /// <summary>Background colour for a correlation coefficient (blue = negative, red = positive).</summary>
        public static string CorrColor(double r)
        {
            var a = (Math.Min(1, Math.Abs(r)) * 0.85).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            return r >= 0 ? $"rgba(239,68,68,{a})" : $"rgba(59,130,246,{a})";
        }

        public static string D(double? v, int dec = 1) => v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value)
            ? v.Value.ToString("N" + dec, System.Globalization.CultureInfo.InvariantCulture) : "—";

        public static string Signed(double? v, int dec = 1, string suffix = "") => v.HasValue && !double.IsNaN(v.Value)
            ? (v.Value > 0 ? "+" : "") + v.Value.ToString("N" + dec, System.Globalization.CultureInfo.InvariantCulture) + suffix : "—";

        public static string PValue(double p) => p < 0.001 ? "< 0.001" : p.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
    }

    public class AnalyticsBatchOption
    {
        public int Id { get; set; }
        public string BatchCode { get; set; } = "";
        public string Breed { get; set; } = "";
        public string BatchType { get; set; } = "";
        public string FarmName { get; set; } = "";
        public int AgeInDays { get; set; }
        public bool IsActive { get; set; }
        public bool HasStandard { get; set; }
        public string Label => $"{BatchCode} · {Breed} · {FarmName} · day {AgeInDays}{(IsActive ? "" : " (closed)")}";
    }

    // ───────────────────────── hub ─────────────────────────

    public class AnalyticsHubVm
    {
        public int ActiveFlocks { get; set; }
        public int ActiveMeatFlocks { get; set; }
        public int ActiveBirds { get; set; }
        public decimal AvgFcr { get; set; }
        public decimal AvgEpef { get; set; }
        public decimal AvgLivability { get; set; }
        public int FlocksAtRisk { get; set; }
        public int OpenAlerts { get; set; }
        public int RunningTrials { get; set; }
        public int StandardBreeds { get; set; }
        public List<Batch> EpefBatches { get; set; } = new();
        public List<FlockRisk> TopRisks { get; set; } = new();
    }

    // ───────────────────────── growth curve ─────────────────────────

    public class GrowthPoint
    {
        public int Age { get; set; }
        public DateTime Date { get; set; }
        public double? ActualGm { get; set; }
        public double? StdGm { get; set; }
        public double? DeviationPct { get; set; }
        public double CumFeedPerBirdGm { get; set; }
        public double? FcrActual { get; set; }
        public double? FcrStd { get; set; }
        public double CumMortPct { get; set; }
        public double? StdCumMortPct { get; set; }
        public double? DailyGainGm { get; set; }
    }

    public class GrowthCurveVm
    {
        public List<AnalyticsBatchOption> Batches { get; set; } = new();
        public Batch? Selected { get; set; }
        public bool HasStandard { get; set; }
        public List<GrowthPoint> Points { get; set; } = new();
        public List<GrowthPoint> Weekly { get; set; } = new();
        public double? CurrentWeightGm { get; set; }
        public double? StdWeightGm { get; set; }
        public double? DeviationPct { get; set; }
        public double? EquivalentAge { get; set; }
        public double? GrowthLagDays { get; set; }
        public double? CurrentFcr { get; set; }
        public double? StdFcr { get; set; }
        public double? Adg7 { get; set; }
        public int LatestAge { get; set; }
    }

    // ───────────────────────── efficiency ─────────────────────────

    public class EfficiencyVm
    {
        public List<Batch> Rows { get; set; } = new();
        public decimal MeanEpef { get; set; }
        public decimal MedianEpef { get; set; }
        public decimal MeanFcr { get; set; }
        public int Excellent { get; set; }
        public int Good { get; set; }
        public int Poor { get; set; }
    }

    // ───────────────────────── anomalies ─────────────────────────

    public class AnomalyPoint
    {
        public DateTime Date { get; set; }
        public int Age { get; set; }
        public int Deaths { get; set; }
        public int StartBirds { get; set; }
        public double MortPct { get; set; }
        public double Ewma { get; set; }
        public double Ucl { get; set; }
        public bool Signal { get; set; }
        public double? RobustZ { get; set; }
    }

    public class AnomalyBatchRow
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public string Breed { get; set; } = "";
        public int AgeInDays { get; set; }
        public int Days { get; set; }
        public int Signals { get; set; }
        public int SignalsLast7 { get; set; }
        public DateTime? LastSignalDate { get; set; }
        public int? LastSignalAge { get; set; }
        public double MaxRobustZ14 { get; set; }
        public double LatestMortPct { get; set; }
        public double BaselineMean { get; set; }
        public double BaselineSd { get; set; }
        public bool BroodingExcluded { get; set; }
        public List<AnomalyPoint> Series { get; set; } = new();
    }

    public class AnomalyVm
    {
        public List<AnomalyBatchRow> Batches { get; set; } = new();
        public AnomalyBatchRow? Selected { get; set; }
        public double Lambda { get; set; } = 0.3;
        public double L { get; set; } = 3;
        public int Baseline { get; set; } = 7;
        public double RobustZThreshold { get; set; } = 3.5;
    }

    // ───────────────────────── heat stress ─────────────────────────

    public class FarmThiRow
    {
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public int Devices { get; set; }
        public DateTime? LastReading { get; set; }
        public decimal? TemperatureC { get; set; }
        public decimal? HumidityPercent { get; set; }
        public decimal? AmmoniaPpm { get; set; }
        public decimal? Thi { get; set; }
        public string Band { get; set; } = "No data";
        public bool Fresh { get; set; }
        public decimal? Max24hThi { get; set; }
        public double HoursAboveWarn7d { get; set; }
    }

    public class BatchThiRow
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public string BatchType { get; set; } = "";
        public int AgeInDays { get; set; }
        public bool Eligible { get; set; }
        public int Days { get; set; }
        public double? MeanThi { get; set; }
        public double? MaxThi { get; set; }
        public int DaysAlert { get; set; }
        public int DaysDanger { get; set; }
        public double? MortHot { get; set; }
        public double? MortNormal { get; set; }
        public string Band { get; set; } = "No data";
    }

    public class ThiSeries
    {
        public string FarmName { get; set; } = "";
        public List<double?> Values { get; set; } = new();
    }

    public class HeatStressVm
    {
        public decimal Warn { get; set; } = 80;
        public decimal Crit { get; set; } = 85;
        public List<FarmThiRow> Farms { get; set; } = new();
        public List<BatchThiRow> Batches { get; set; } = new();
        public List<string> HourLabels { get; set; } = new();
        public List<ThiSeries> Series { get; set; } = new();
        public List<double[]> Scatter { get; set; } = new();
        public double PearsonR { get; set; }
        public int ScatterN { get; set; }
        public double PearsonP { get; set; } = 1;
        public double Slope { get; set; }
        public double Intercept { get; set; }
    }

    // ───────────────────────── risk score ─────────────────────────

    public class RiskComponent
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "bi-circle";
        public double Weight { get; set; }
        public double Fraction { get; set; }
        public double Points => Math.Round(Weight * Fraction, 1);
        public string Evidence { get; set; } = "";
    }

    public class FlockRisk
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public string Breed { get; set; } = "";
        public string BatchType { get; set; } = "";
        public int AgeInDays { get; set; }
        public int Birds { get; set; }
        public List<RiskComponent> Components { get; set; } = new();
        public double Score => Math.Round(Components.Sum(c => c.Points), 1);
        public string Level => AnalyticsUi.RiskLevel(Score);
        public string Recommendation { get; set; } = "";
    }

    public class RiskScoreVm
    {
        public List<FlockRisk> Flocks { get; set; } = new();
        public decimal Warn { get; set; } = 80;
        public decimal Crit { get; set; } = 85;
    }

    // ───────────────────────── harvest forecast ─────────────────────────

    public class HarvestForecast
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public string Breed { get; set; } = "";
        public string BatchType { get; set; } = "";
        public int AgeInDays { get; set; }
        public DateTime PlacementDate { get; set; }
        public int Birds { get; set; }
        public double? CurrentWeightGm { get; set; }
        public int? LastWeighAge { get; set; }
        public double TargetKg { get; set; }
        public bool HasStandard { get; set; }
        public double? PerfRatio { get; set; }
        public int? DayA { get; set; }
        public DateTime? DateA { get; set; }
        public int? DaysLeftA { get; set; }
        public double? SlopeB { get; set; }
        public double? InterceptB { get; set; }
        public double? R2B { get; set; }
        public int PointsB { get; set; }
        public int? DayB { get; set; }
        public DateTime? DateB { get; set; }
        public int? DaysLeftB { get; set; }
        public bool Ready { get; set; }
        public int? HarvestDay { get; set; }
        public DateTime? HarvestDate { get; set; }
        public int? DaysLeft { get; set; }
        public double DailyMortPct { get; set; }
        public int ProjectedBirds { get; set; }
        public double ProjectedWeightKg { get; set; }
        public double ProjectedLiveKg { get; set; }
        public decimal PricePerKg { get; set; }
        public decimal ProjectedRevenue { get; set; }
        public double FeedToHarvestKg { get; set; }
        public decimal FeedCostPerKg { get; set; }
        public decimal FeedCostToHarvest => (decimal)FeedToHarvestKg * FeedCostPerKg;
        public string Method { get; set; } = "";
        public string Note { get; set; } = "";
        // chart series: [age, grams]
        public List<double[]> Actual { get; set; } = new();
        public List<double[]> Standard { get; set; } = new();
        public List<double[]> ProjA { get; set; } = new();
        public List<double[]> ProjB { get; set; } = new();
    }

    public class ForecastVm
    {
        public List<HarvestForecast> Rows { get; set; } = new();
        public HarvestForecast? Selected { get; set; }
        public decimal BroilerTargetKg { get; set; }
        public decimal SonaliTargetKg { get; set; }
        public decimal BroilerPrice { get; set; }
        public decimal SonaliPrice { get; set; }
    }

    // ───────────────────────── feed forecast ─────────────────────────

    public class FeedDemandRow
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public string BatchType { get; set; } = "";
        public string Breed { get; set; } = "";
        public int AgeInDays { get; set; }
        public int Birds { get; set; }
        public double IntakeRatio { get; set; } = 1;
        public string Basis { get; set; } = "";
        public double[] DailyKg { get; set; } = new double[14];
        public string[] DailyStage { get; set; } = new string[14];
        public double TotalKg => DailyKg.Sum();
        public string StagesText { get; set; } = "";
    }

    public class FeedStageTotal
    {
        public string BatchType { get; set; } = "";
        public string Stage { get; set; } = "";
        public string Key => BatchType + " · " + Stage;
        public double DemandKg { get; set; }
        public double AvgDailyKg { get; set; }
        public double StockKg { get; set; }
        public string Items { get; set; } = "";
        public double? DaysCover { get; set; }
        public double ReorderKg { get; set; }
        public decimal CostPerKg { get; set; }
        public string Status { get; set; } = "";
    }

    public class FeedForecastVm
    {
        public List<FeedDemandRow> Rows { get; set; } = new();
        public List<FeedStageTotal> Stages { get; set; } = new();
        public List<InventoryItem> FeedItems { get; set; } = new();
        public List<string> DayLabels { get; set; } = new();
        public double TotalDemandKg { get; set; }
        public double TotalStockKg { get; set; }
        public double LayerGmPerHen { get; set; } = 112;
        public decimal ReorderCost { get; set; }
    }

    // ───────────────────────── benchmark ─────────────────────────

    public class BenchmarkVm
    {
        public List<FarmBenchmarkRow> Rows { get; set; } = new();
        public Dictionary<int, double[]> Scores { get; set; } = new(); // FCR, EPEF, Mortality, Biosecurity → 0..100
        public Dictionary<int, double> Composite { get; set; } = new();
        public int? BestFcrFarm { get; set; }
        public int? BestEpefFarm { get; set; }
        public int? BestMortFarm { get; set; }
        public int? BestBioFarm { get; set; }
        public int? BestOverallFarm { get; set; }
        public static bool IsMeat(FarmBenchmarkRow r) => !string.Equals(r.FarmType, "Layer", StringComparison.OrdinalIgnoreCase);
    }

    // ───────────────────────── carbon ─────────────────────────

    public class CarbonRow
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public string Breed { get; set; } = "";
        public bool IsActive { get; set; }
        public int AgeInDays { get; set; }
        public int ChicksPlaced { get; set; }
        public double CycleShare { get; set; } = 1;
        public double FeedKg { get; set; }
        public double LiveKg { get; set; }
        public double FeedCo2 { get; set; }
        public double ChickCo2 { get; set; }
        public double ElectricityCo2 { get; set; }
        public double ManureCo2 { get; set; }
        public double TotalCo2 => FeedCo2 + ChickCo2 + ElectricityCo2 + ManureCo2;
        public double? Intensity => LiveKg > 0 ? TotalCo2 / LiveKg : null;
    }

    public class CarbonVm
    {
        public List<CarbonRow> Rows { get; set; } = new();
        public decimal FeedFactor { get; set; }
        public decimal ChickFactor { get; set; }
        public decimal KwhPerBird { get; set; }
        public decimal GridFactor { get; set; }
        public decimal ManureFactor { get; set; }
        public double TotalCo2 => Rows.Sum(r => r.TotalCo2);
        public double TotalLiveKg => Rows.Sum(r => r.LiveKg);
        public double? AvgIntensity => TotalLiveKg > 0 ? TotalCo2 / TotalLiveKg : null;
        public double FeedShare => TotalCo2 > 0 ? Rows.Sum(r => r.FeedCo2) / TotalCo2 * 100 : 0;
    }
}
