using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    public class FarmAuditSummary
    {
        public int Id { get; set; }
        public DateTime AuditDate { get; set; }
        public string AuditorName { get; set; } = "";
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public decimal ScorePct { get; set; }
        public string Grade { get; set; } = "";
        public string? Notes { get; set; }
    }

    public class FarmOfficerRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Zone { get; set; }
        public string Designation { get; set; } = "";
        public DateTime AssignedDate { get; set; }
        public int VisitCount { get; set; }
        public DateTime? LastVisit { get; set; }
    }

    public class FarmDetailVm
    {
        public Farm Farm { get; set; } = new();
        public FarmBenchmarkRow? Benchmark { get; set; }
        public List<Batch> ActiveBatches { get; set; } = new();
        public List<Batch> PastBatches { get; set; } = new();
        public List<Certification> Certifications { get; set; } = new();
        public FarmAuditSummary? LatestAudit { get; set; }
        public List<SensorDeviceCard> Sensors { get; set; } = new();
        public List<FarmVisit> RecentVisits { get; set; } = new();
        public List<FarmOfficerRow> Officers { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public List<Alert> OpenAlerts { get; set; } = new();
        public decimal ThiWarning { get; set; } = 80;
        public decimal ThiCritical { get; set; } = 85;
    }

    public class FarmMapPoint
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public string FarmType { get; set; } = "";
        public string? Zone { get; set; }
        public decimal Lat { get; set; }
        public decimal Lng { get; set; }
        public int Capacity { get; set; }
        public int CurrentBirds { get; set; }
        public decimal UtilizationPct { get; set; }
        public int ActiveBatches { get; set; }
        public decimal AvgFCR { get; set; }
        public decimal AvgEPEF { get; set; }
        public decimal AvgMortalityRate { get; set; }
        public decimal? BiosecurityPct { get; set; }
        public int CriticalAlerts { get; set; }
        public int WarningAlerts { get; set; }
        public int InfoAlerts { get; set; }
        public string AlertLevel => CriticalAlerts > 0 ? "Critical" : WarningAlerts > 0 ? "Warning" : InfoAlerts > 0 ? "Info" : "None";
    }

    public class VisitCheckInPoint
    {
        public int Id { get; set; }
        public string FarmName { get; set; } = "";
        public string FieldOfficerName { get; set; } = "";
        public DateTime VisitDate { get; set; }
        public string VisitPurpose { get; set; } = "";
        public string VisitStatus { get; set; } = "";
        public decimal Lat { get; set; }
        public decimal Lng { get; set; }
        public decimal? GpsAccuracyMeters { get; set; }
    }

    public class FarmMapVm
    {
        public List<FarmMapPoint> Farms { get; set; } = new();
        public List<VisitCheckInPoint> CheckIns { get; set; } = new();
        public int FarmsWithoutGps { get; set; }
    }
}
