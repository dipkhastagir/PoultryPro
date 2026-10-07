using PoultryPro.Models.DTO;
using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    public class DashFlockRow
    {
        public Batch Batch { get; set; } = new();
        public decimal? StdWeightKg { get; set; }
        public decimal? WeightVsStdPct => StdWeightKg is > 0 ? Math.Round((Batch.AverageWeightKg - StdWeightKg.Value) / StdWeightKg.Value * 100, 1) : null;
        public decimal? StdFcr { get; set; }
    }

    public class DashTrendPoint
    {
        public DateTime Day { get; set; }
        public int Mortality { get; set; }
        public decimal FeedKg { get; set; }
        public int Birds { get; set; }
    }

    public class DashClimateTile
    {
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string? ShedNo { get; set; }
        public string DeviceCode { get; set; } = "";
        public DateTime ReadingTime { get; set; }
        public decimal? TemperatureC { get; set; }
        public decimal? HumidityPercent { get; set; }
        public decimal? AmmoniaPpm { get; set; }
        public decimal? Thi { get; set; }
        public string Band { get; set; } = "Normal";
        public bool Stale => DateTime.Now - ReadingTime > TimeSpan.FromHours(1);
    }

    public class DashUpcomingItem
    {
        public DateTime Date { get; set; }
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Sub { get; set; }
        public string Icon { get; set; } = "bi-calendar";
        public string Tone { get; set; } = "blue";
        public string? Url { get; set; }
    }

    public class DashboardVm
    {
        public DashboardSummary Summary { get; set; } = new();
        public bool ShowFinance { get; set; } = true;
        public decimal AvgFcr { get; set; }
        public decimal AvgEpef { get; set; }
        public int MeatFlocks { get; set; }
        public int MortalityToday { get; set; }
        public int MortalityYesterday { get; set; }
        public int EggsToday { get; set; }
        public DateTime? EggsDate { get; set; }
        public decimal HenDayPct { get; set; }
        public List<Alert> Alerts { get; set; } = new();
        public int OpenAlertCount { get; set; }
        public List<MonthlyFinancial> Monthly { get; set; } = new();
        public List<DashFlockRow> Flocks { get; set; } = new();
        public List<Farm> Farms { get; set; } = new();
        public List<DashTrendPoint> Trend { get; set; } = new();
        public List<DashClimateTile> Climate { get; set; } = new();
        public List<DashUpcomingItem> Upcoming { get; set; } = new();
        public decimal ThiWarning { get; set; } = 80;
        public decimal ThiCritical { get; set; } = 85;
    }
}
