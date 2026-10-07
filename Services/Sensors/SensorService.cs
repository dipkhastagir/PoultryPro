using System.Security.Cryptography;
using Dapper;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>
/// IoT sensor network: device registry (with per-device API keys), telemetry ingest and queries.
/// </summary>
public interface ISensorService
{
    Task<List<SensorDevice>> GetDevicesAsync(int? farmId = null);
    Task<SensorDevice?> GetDeviceAsync(int id);
    Task<SensorDevice?> GetDeviceByCodeAsync(string deviceCode);
    Task<bool> DeviceCodeExistsAsync(string deviceCode, int exceptId = 0);
    Task<int> CreateDeviceAsync(SensorDeviceFormVm vm);
    Task UpdateDeviceAsync(SensorDeviceFormVm vm);
    Task DeleteDeviceAsync(int id);
    Task<string> RegenerateKeyAsync(int id);

    Task<List<SensorDeviceCard>> GetDeviceCardsAsync(int? farmId = null);
    Task<SensorReading?> GetLatestReadingAsync(int deviceId);
    Task<List<SensorReading>> GetReadingsAsync(int? farmId, int? deviceId, DateTime from);
    Task<List<SensorReading>> GetLatestPerDeviceAsync();
    Task<int> CountReadingsSinceAsync(DateTime from);
    Task<long> InsertReadingAsync(SensorDevice device, SensorIngestRequest r);
    Task<long> SimulateReadingAsync(int deviceId);
    Task<(decimal Warning, decimal Critical, decimal Ammonia)> ThresholdsAsync();
}

public class SensorService(IDbConnectionFactory db, ISettingsService settings) : ISensorService
{
    private const string DeviceSelect = @"SELECT d.*, f.Name AS FarmName,
        (SELECT COUNT(*) FROM SensorReadings r WHERE r.DeviceId = d.Id) AS ReadingCount
        FROM SensorDevices d INNER JOIN Farms f ON f.Id = d.FarmId";

    /// <summary>Cryptographically random 64-hex-character key (256 bits).</summary>
    public static string GenerateApiKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public async Task<(decimal Warning, decimal Critical, decimal Ammonia)> ThresholdsAsync()
        => (await settings.GetDecimalAsync("Alert.THIWarning", 80m),
            await settings.GetDecimalAsync("Alert.THICritical", 85m),
            await settings.GetDecimalAsync("Alert.AmmoniaPpm", 25m));

    public async Task<List<SensorDevice>> GetDevicesAsync(int? farmId = null)
    {
        using var conn = db.Create();
        var sql = DeviceSelect + (farmId.HasValue ? " WHERE d.FarmId = @farmId" : "") + " ORDER BY f.Name, d.ShedNo, d.DeviceCode";
        return (await conn.QueryAsync<SensorDevice>(sql, new { farmId })).ToList();
    }

    public async Task<SensorDevice?> GetDeviceAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<SensorDevice>(DeviceSelect + " WHERE d.Id = @id", new { id });
    }

    public async Task<SensorDevice?> GetDeviceByCodeAsync(string deviceCode)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<SensorDevice>(
            "SELECT d.*, f.Name AS FarmName FROM SensorDevices d INNER JOIN Farms f ON f.Id = d.FarmId WHERE d.DeviceCode = @deviceCode",
            new { deviceCode });
    }

    public async Task<bool> DeviceCodeExistsAsync(string deviceCode, int exceptId = 0)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SensorDevices WHERE DeviceCode = @deviceCode AND Id <> @exceptId",
            new { deviceCode, exceptId }) > 0;
    }

    public async Task<int> CreateDeviceAsync(SensorDeviceFormVm vm)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO SensorDevices (DeviceCode, Name, FarmId, ShedNo, DeviceType, ApiKey, FirmwareVersion, IsActive)
              VALUES (@DeviceCode, @Name, @FarmId, @ShedNo, @DeviceType, @ApiKey, @FirmwareVersion, @IsActive);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                DeviceCode = vm.DeviceCode.Trim(), Name = vm.Name.Trim(), vm.FarmId, vm.ShedNo,
                DeviceType = string.IsNullOrWhiteSpace(vm.DeviceType) ? "ESP32 Climate Node" : vm.DeviceType.Trim(),
                ApiKey = GenerateApiKey(), vm.FirmwareVersion, vm.IsActive
            });
    }

    public async Task UpdateDeviceAsync(SensorDeviceFormVm vm)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE SensorDevices SET DeviceCode=@DeviceCode, Name=@Name, FarmId=@FarmId, ShedNo=@ShedNo,
                     DeviceType=@DeviceType, FirmwareVersion=@FirmwareVersion, IsActive=@IsActive
              WHERE Id=@Id",
            new
            {
                vm.Id, DeviceCode = vm.DeviceCode.Trim(), Name = vm.Name.Trim(), vm.FarmId, vm.ShedNo,
                DeviceType = string.IsNullOrWhiteSpace(vm.DeviceType) ? "ESP32 Climate Node" : vm.DeviceType.Trim(),
                vm.FirmwareVersion, vm.IsActive
            });
    }

    public async Task DeleteDeviceAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("DELETE FROM SensorDevices WHERE Id=@id", new { id });
    }

    public async Task<string> RegenerateKeyAsync(int id)
    {
        var key = GenerateApiKey();
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE SensorDevices SET ApiKey=@key WHERE Id=@id", new { id, key });
        return key;
    }

    public async Task<List<SensorReading>> GetLatestPerDeviceAsync()
    {
        using var conn = db.Create();
        return (await conn.QueryAsync<SensorReading>(
            @"SELECT r.*, d.DeviceCode, d.Name AS DeviceName, d.ShedNo, f.Name AS FarmName
              FROM SensorDevices d
              INNER JOIN Farms f ON f.Id = d.FarmId
              CROSS APPLY (SELECT TOP 1 * FROM SensorReadings x WHERE x.DeviceId = d.Id ORDER BY x.ReadingTime DESC) r
              WHERE d.IsActive = 1
              ORDER BY f.Name, d.ShedNo")).ToList();
    }

    public async Task<List<SensorDeviceCard>> GetDeviceCardsAsync(int? farmId = null)
    {
        var devices = await GetDevicesAsync(farmId);
        var latest = (await GetLatestPerDeviceAsync()).ToDictionary(r => r.DeviceId);
        var (warn, crit, _) = await ThresholdsAsync();
        return devices.Select(d =>
        {
            latest.TryGetValue(d.Id, out var last);
            var card = new SensorDeviceCard { Device = d, Last = last };
            if (card.Thi.HasValue) card.Band = PoultryMath.HeatBand(card.Thi.Value, warn, crit);
            return card;
        }).ToList();
    }

    public async Task<SensorReading?> GetLatestReadingAsync(int deviceId)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<SensorReading>(
            @"SELECT TOP 1 r.*, d.DeviceCode, d.Name AS DeviceName, d.ShedNo, f.Name AS FarmName
              FROM SensorReadings r INNER JOIN SensorDevices d ON d.Id = r.DeviceId INNER JOIN Farms f ON f.Id = d.FarmId
              WHERE r.DeviceId = @deviceId ORDER BY r.ReadingTime DESC", new { deviceId });
    }

    public async Task<List<SensorReading>> GetReadingsAsync(int? farmId, int? deviceId, DateTime from)
    {
        using var conn = db.Create();
        var sql = @"SELECT r.*, d.DeviceCode, d.Name AS DeviceName, d.ShedNo, f.Name AS FarmName
                    FROM SensorReadings r
                    INNER JOIN SensorDevices d ON d.Id = r.DeviceId
                    INNER JOIN Farms f ON f.Id = r.FarmId
                    WHERE r.ReadingTime >= @from";
        if (farmId.HasValue) sql += " AND r.FarmId = @farmId";
        if (deviceId.HasValue) sql += " AND r.DeviceId = @deviceId";
        sql += " ORDER BY r.ReadingTime, d.DeviceCode";
        return (await conn.QueryAsync<SensorReading>(sql, new { farmId, deviceId, from })).ToList();
    }

    public async Task<int> CountReadingsSinceAsync(DateTime from)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SensorReadings WHERE ReadingTime >= @from", new { from });
    }

    public async Task<long> InsertReadingAsync(SensorDevice device, SensorIngestRequest r)
    {
        var time = r.ReadingTime ?? DateTime.Now;
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var id = await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO SensorReadings (DeviceId, FarmId, ReadingTime, TemperatureC, HumidityPercent, AmmoniaPpm, Co2Ppm, LightLux, WaterFlowLtr, FeedLevelPct)
              VALUES (@DeviceId, @FarmId, @time, @TemperatureC, @HumidityPercent, @AmmoniaPpm, @Co2Ppm, @LightLux, @WaterFlowLtr, @FeedLevelPct);
              SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
            new
            {
                DeviceId = device.Id, device.FarmId, time,
                TemperatureC = Round(r.TemperatureC), HumidityPercent = Round(r.HumidityPercent), AmmoniaPpm = Round(r.AmmoniaPpm),
                Co2Ppm = Round(r.Co2Ppm), LightLux = Round(r.LightLux), WaterFlowLtr = Round(r.WaterFlowLtr), FeedLevelPct = Round(r.FeedLevelPct)
            }, tx);
        await conn.ExecuteAsync(
            "UPDATE SensorDevices SET LastSeenAt = CASE WHEN LastSeenAt IS NULL OR LastSeenAt < @time THEN @time ELSE LastSeenAt END WHERE Id = @id",
            new { id = device.Id, time }, tx);
        tx.Commit();
        return id;
    }

    private static decimal? Round(decimal? v) => v.HasValue ? Math.Round(v.Value, 2) : null;

    /// <summary>
    /// Generates a realistic reading for demos: diurnal temperature/humidity curve, continuity with the
    /// device's previous reading, and plausible NH3 / CO2 / light / water / feed-hopper values.
    /// </summary>
    public async Task<long> SimulateReadingAsync(int deviceId)
    {
        var device = await GetDeviceAsync(deviceId) ?? throw new InvalidOperationException("Device not found.");
        var last = await GetLatestReadingAsync(deviceId);
        var rnd = Random.Shared;
        var now = DateTime.Now;
        double hourAngle = 2 * Math.PI * (now.Hour + now.Minute / 60.0 - 9) / 24.0;
        double baseT = 26.0 + 3.5 * Math.Sin(hourAngle);
        double baseRh = 70 - 12 * Math.Sin(hourAngle);
        double t = last?.TemperatureC is decimal lt ? 0.6 * (double)lt + 0.4 * baseT : baseT;
        double rh = last?.HumidityPercent is decimal lh ? 0.6 * (double)lh + 0.4 * baseRh : baseRh;
        t += (rnd.NextDouble() - 0.5) * 1.2;
        rh = Math.Clamp(rh + (rnd.NextDouble() - 0.5) * 4, 30, 98);
        double nh3 = last?.AmmoniaPpm is decimal ln ? (double)ln + (rnd.NextDouble() - 0.5) * 2 : 10 + rnd.NextDouble() * 6;
        double feed = last?.FeedLevelPct is decimal lf ? (double)lf - 1.5 - rnd.NextDouble() : 90;
        if (feed < 8) feed = 100;
        bool day = now.Hour is >= 6 and <= 18;
        var req = new SensorIngestRequest
        {
            DeviceCode = device.DeviceCode,
            TemperatureC = (decimal)Math.Round(t, 2),
            HumidityPercent = (decimal)Math.Round(rh, 2),
            AmmoniaPpm = (decimal)Math.Round(Math.Clamp(nh3, 2, 60), 2),
            Co2Ppm = (decimal)Math.Round(900 + rnd.NextDouble() * 900 + (now.Hour <= 5 ? 600 : 0), 0),
            LightLux = (decimal)Math.Round(day ? 45 + rnd.NextDouble() * 25 : 12 + rnd.NextDouble() * 6, 1),
            WaterFlowLtr = (decimal)Math.Round(35 + rnd.NextDouble() * 25 + (now.Hour is >= 11 and <= 16 ? 20 : 0), 1),
            FeedLevelPct = (decimal)Math.Round(Math.Clamp(feed, 0, 100), 1),
            ReadingTime = now
        };
        return await InsertReadingAsync(device, req);
    }
}
