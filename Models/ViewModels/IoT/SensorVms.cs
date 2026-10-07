using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    /// <summary>Device card on Sensors/Index: device + its most recent reading.</summary>
    public class SensorDeviceCard
    {
        public SensorDevice Device { get; set; } = new();
        public SensorReading? Last { get; set; }
        public decimal? Thi => Last?.Thi;
        public string Band { get; set; } = "—";
    }

    public class SensorIndexVm
    {
        public List<SensorDeviceCard> Cards { get; set; } = new();
        public List<Farm> Farms { get; set; } = new();
        public int? FarmId { get; set; }
        public decimal ThiWarning { get; set; } = 80;
        public decimal ThiCritical { get; set; } = 85;
        public decimal AmmoniaLimit { get; set; } = 25;
        public int ReadingsLast24h { get; set; }
    }

    public class SensorDeviceFormVm
    {
        public int Id { get; set; }
        public string DeviceCode { get; set; } = "";
        public string Name { get; set; } = "";
        public int FarmId { get; set; }
        public string? ShedNo { get; set; }
        public string DeviceType { get; set; } = "ESP32 Climate Node (DHT22 + MQ-137 + MH-Z19)";
        public string? FirmwareVersion { get; set; } = "v1.0.0";
        public bool IsActive { get; set; } = true;
        public string? ApiKey { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }

    public class SensorMetricStat
    {
        public string Metric { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Icon { get; set; } = "bi-activity";
        public int Samples { get; set; }
        public decimal? Min { get; set; }
        public decimal? Avg { get; set; }
        public decimal? Max { get; set; }
        public decimal? Limit { get; set; }
        public int Breaches { get; set; }
    }

    public class SensorReadingsVm
    {
        public int? FarmId { get; set; }
        public int? DeviceId { get; set; }
        public int Hours { get; set; } = 24;
        public List<Farm> Farms { get; set; } = new();
        public List<SensorDevice> Devices { get; set; } = new();
        public List<SensorReading> Readings { get; set; } = new();
        public List<SensorMetricStat> Stats { get; set; } = new();
        public decimal ThiWarning { get; set; } = 80;
        public decimal ThiCritical { get; set; } = 85;
        public decimal AmmoniaLimit { get; set; } = 25;
    }

    public class SensorApiDocsVm
    {
        public string BaseUrl { get; set; } = "";
        public List<SensorDevice> Devices { get; set; } = new();
        public SensorDevice? Example { get; set; }
        public bool CanSimulate { get; set; }
    }

    // ── JSON contracts for the attribute-routed APIs ────────────────────────
    public class SensorIngestRequest
    {
        public string? DeviceCode { get; set; }
        public decimal? TemperatureC { get; set; }
        public decimal? HumidityPercent { get; set; }
        public decimal? AmmoniaPpm { get; set; }
        public decimal? Co2Ppm { get; set; }
        public decimal? LightLux { get; set; }
        public decimal? WaterFlowLtr { get; set; }
        public decimal? FeedLevelPct { get; set; }
        public DateTime? ReadingTime { get; set; }
    }

    public class MobileLoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class MobileVisitRequest
    {
        public int FieldOfficerId { get; set; }
        public int FarmId { get; set; }
        public int? BatchId { get; set; }
        public DateTime? VisitDate { get; set; }
        public string? VisitPurpose { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal? GpsAccuracyMeters { get; set; }
        public string? Remark { get; set; }
    }

    public class MobileDailyRecordRequest
    {
        public int? BatchId { get; set; }
        public DateTime? RecordDate { get; set; }
        public int BirdCount { get; set; }
        public int MortalityCount { get; set; }
        public int CullingCount { get; set; }
        public decimal FeedConsumptionKg { get; set; }
        public decimal WaterConsumptionLtr { get; set; }
        public decimal? TemperatureC { get; set; }
        public decimal? HumidityPercent { get; set; }
        public decimal? LightHours { get; set; }
        public decimal? AverageWeightGm { get; set; }
        public string? MedicineUsed { get; set; }
        public string? MedicineDosage { get; set; }
        public string? VaccineUsed { get; set; }
        public string? Remarks { get; set; }
    }

    public class MobileCompleteRequest
    {
        public string? Remark { get; set; }
    }

    public class MobileFarmDto
    {
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string Location { get; set; } = "";
        public string FarmType { get; set; } = "";
        public string? Zone { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerPhone { get; set; }
        public List<MobileBatchDto> ActiveBatches { get; set; } = new();
    }

    public class MobileBatchDto
    {
        public int BatchId { get; set; }
        public int FarmId { get; set; }
        public string BatchCode { get; set; } = "";
        public string Breed { get; set; } = "";
        public string BatchType { get; set; } = "";
        public string? ShedNo { get; set; }
        public int AgeInDays { get; set; }
        public int CurrentCount { get; set; }
        public decimal AverageWeightKg { get; set; }
        public string HealthStatus { get; set; } = "";
    }
}
