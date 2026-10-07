namespace PoultryPro.Models.Entities
{
    /// <summary>An IoT node (e.g. ESP32 climate node) installed in a shed.</summary>
    public class SensorDevice
    {
        public int Id { get; set; }
        public string DeviceCode { get; set; } = "";
        public string Name { get; set; } = "";
        public int FarmId { get; set; }
        public string? FarmName { get; set; }
        public string? ShedNo { get; set; }
        public string DeviceType { get; set; } = "ESP32 Climate Node";
        public string ApiKey { get; set; } = "";
        public string? FirmwareVersion { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastSeenAt { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>A device is online when it reported within the last hour.</summary>
        public bool IsOnline => IsActive && LastSeenAt.HasValue && DateTime.Now - LastSeenAt.Value < TimeSpan.FromHours(1);
        public int ReadingCount { get; set; }
    }

    /// <summary>One telemetry sample posted by a sensor device.</summary>
    public class SensorReading
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public int FarmId { get; set; }
        public string? DeviceCode { get; set; }
        public string? DeviceName { get; set; }
        public string? FarmName { get; set; }
        public string? ShedNo { get; set; }
        public DateTime ReadingTime { get; set; }
        public decimal? TemperatureC { get; set; }
        public decimal? HumidityPercent { get; set; }
        public decimal? AmmoniaPpm { get; set; }
        public decimal? Co2Ppm { get; set; }
        public decimal? LightLux { get; set; }
        public decimal? WaterFlowLtr { get; set; }
        public decimal? FeedLevelPct { get; set; }

        public decimal? Thi => TemperatureC.HasValue && HumidityPercent.HasValue
            ? PoultryPro.Infrastructure.PoultryMath.THI(TemperatureC.Value, HumidityPercent.Value) : null;
    }
}
