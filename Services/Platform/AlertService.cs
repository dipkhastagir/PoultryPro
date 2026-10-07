using Dapper;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;

/// <summary>
/// Rule-based early-warning engine. Each rule scans live data and raises de-duplicated alerts
/// (RefKey). Alerts whose condition has cleared are resolved automatically by "system".
/// Rules: MORT_SPIKE, HEAT_STRESS, AMMONIA, VAX_OVERDUE, LOW_STOCK, EXPIRY, CERT_EXPIRY,
///        HARVEST_READY, GROWTH_LAG, SYNC_STALE, DEVICE_OFFLINE, DISEASE_ACTIVE.
/// </summary>
public interface IAlertService
{
    Task<int> RefreshAsync(bool force = false);
    Task<IEnumerable<Alert>> GetAsync(bool includeResolved = false, string? severity = null, string? rule = null, int top = 300);
    Task<IEnumerable<Alert>> GetOpenAsync(int top = 10);
    Task<int> CountOpenAsync();
    Task MarkReadAsync(int id);
    Task MarkAllReadAsync();
    Task ResolveAsync(int id, string user);
}

public class AlertService(IDbConnectionFactory db, ISettingsService settings) : IAlertService
{
    private static DateTime _lastRun = DateTime.MinValue;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private record Candidate(string Rule, string Severity, string Title, string Message, int? FarmId, int? BatchId, string RefKey, string Link);

    private const string SelectAlerts = @"SELECT a.*, f.Name AS FarmName, b.BatchCode FROM Alerts a
                                          LEFT JOIN Farms f ON f.Id = a.FarmId LEFT JOIN Batches b ON b.Id = a.BatchId";

    public async Task<IEnumerable<Alert>> GetAsync(bool includeResolved = false, string? severity = null, string? rule = null, int top = 300)
    {
        using var conn = db.Create();
        var sql = SelectAlerts + " WHERE 1=1";
        if (!includeResolved) sql += " AND a.IsResolved=0";
        if (!string.IsNullOrEmpty(severity)) sql += " AND a.Severity=@severity";
        if (!string.IsNullOrEmpty(rule)) sql += " AND a.RuleCode=@rule";
        sql = sql.Replace("SELECT a.*", "SELECT TOP (@top) a.*") +
              " ORDER BY a.IsResolved, CASE a.Severity WHEN 'Critical' THEN 0 WHEN 'Warning' THEN 1 ELSE 2 END, a.CreatedAt DESC";
        return await conn.QueryAsync<Alert>(sql, new { severity, rule, top });
    }

    public Task<IEnumerable<Alert>> GetOpenAsync(int top = 10) => GetAsync(false, null, null, top);

    public async Task<int> CountOpenAsync()
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Alerts WHERE IsResolved=0");
    }

    public async Task MarkReadAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE Alerts SET IsRead=1 WHERE Id=@id", new { id });
    }

    public async Task MarkAllReadAsync()
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE Alerts SET IsRead=1 WHERE IsResolved=0");
    }

    public async Task ResolveAsync(int id, string user)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE Alerts SET IsResolved=1, IsRead=1, ResolvedBy=@user, ResolvedAt=GETDATE() WHERE Id=@id", new { id, user });
    }

    public async Task<int> RefreshAsync(bool force = false)
    {
        if (!force && DateTime.Now - _lastRun < TimeSpan.FromMinutes(3)) return 0;
        if (!await Gate.WaitAsync(0)) return 0;
        try
        {
            _lastRun = DateTime.Now;
            var c = new List<Candidate>();
            using var conn = db.Create();

            var spikeFactor = await settings.GetDecimalAsync("Alert.MortalitySpikeFactor", 2.5m);
            var spikeMinPct = await settings.GetDecimalAsync("Alert.MortalityMinPct", 0.30m);
            var thiWarn     = await settings.GetDecimalAsync("Alert.THIWarning", 80m);
            var thiCrit     = await settings.GetDecimalAsync("Alert.THICritical", 85m);
            var nh3Limit    = await settings.GetDecimalAsync("Alert.AmmoniaPpm", 25m);
            var lagPct      = await settings.GetDecimalAsync("Alert.GrowthLagPct", 12m);
            var staleDays   = (int)await settings.GetDecimalAsync("Alert.SyncStaleDays", 3m);
            var targetKg    = await settings.GetDecimalAsync("Harvest.TargetWeightKg", 2.0m);

            var batches = (await conn.QueryAsync<Batch>("SELECT * FROM vw_BatchDetails WHERE IsActive=1")).ToList();

            // ---- MORT_SPIKE: any day in the last 7 days vs. the 7 days before it ----
            var daily = (await conn.QueryAsync<(int BatchId, DateTime RecordDate, int MortalityCount, int BirdCount)>(
                @"SELECT BatchId, RecordDate, MortalityCount, BirdCount FROM DailyFarmRecords
                  WHERE RecordDate >= DATEADD(DAY,-21,CAST(GETDATE() AS DATE))
                    AND BatchId IN (SELECT Id FROM Batches WHERE IsActive=1)
                  ORDER BY BatchId, RecordDate")).ToList();
            foreach (var g in daily.GroupBy(d => d.BatchId))
            {
                var rows = g.ToList();
                var b = batches.FirstOrDefault(x => x.Id == g.Key);
                if (b == null) continue;
                (DateTime Date, decimal Pct, decimal Ratio)? worst = null;
                for (int i = 7; i < rows.Count; i++)
                {
                    if (rows[i].RecordDate < DateTime.Today.AddDays(-7)) continue;
                    var baseline = rows.Skip(i - 7).Take(7).Average(r => (decimal)r.MortalityCount);
                    var cur = rows[i].MortalityCount;
                    var pct = rows[i].BirdCount > 0 ? (decimal)cur / rows[i].BirdCount * 100 : 0;
                    var ratio = baseline > 0 ? cur / baseline : cur;
                    if (ratio >= spikeFactor && pct >= spikeMinPct && (worst == null || ratio > worst.Value.Ratio))
                        worst = (rows[i].RecordDate, pct, ratio);
                }
                if (worst != null)
                    c.Add(new("MORT_SPIKE", worst.Value.Ratio >= spikeFactor * 1.5m ? "Critical" : "Warning",
                        $"Mortality spike in {b.BatchCode}",
                        $"{worst.Value.Date:dd MMM}: daily mortality {worst.Value.Pct:0.00}% = {worst.Value.Ratio:0.0}× the 7-day baseline at {b.FarmName}. Investigate cause (heat, disease, water, feed).",
                        b.FarmId, b.Id, $"MORT_SPIKE:{b.Id}:{worst.Value.Date:yyyyMMdd}", $"/Batch/Detail/{b.Id}"));
            }

            // ---- HEAT_STRESS (sensor THI, last 3 readings) & AMMONIA ----
            var sensor = (await conn.QueryAsync<(int FarmId, string FarmName, decimal? T, decimal? RH, decimal? NH3)>(
                @"SELECT d.FarmId, f.Name AS FarmName,
                         AVG(r.TemperatureC) AS T, AVG(r.HumidityPercent) AS RH, AVG(r.AmmoniaPpm) AS NH3
                  FROM SensorDevices d
                  INNER JOIN Farms f ON f.Id = d.FarmId
                  CROSS APPLY (SELECT TOP 3 * FROM SensorReadings sr WHERE sr.DeviceId = d.Id ORDER BY sr.ReadingTime DESC) r
                  WHERE d.IsActive = 1 AND d.LastSeenAt >= DATEADD(HOUR,-6,GETDATE())
                  GROUP BY d.FarmId, f.Name")).ToList();
            foreach (var s in sensor)
            {
                if (s.T.HasValue && s.RH.HasValue)
                {
                    var thi = PoultryMath.THI(s.T.Value, s.RH.Value);
                    if (thi >= thiWarn)
                        c.Add(new("HEAT_STRESS", thi >= thiCrit ? "Critical" : "Warning", $"Heat stress risk at {s.FarmName}",
                            $"Live sensors: {s.T:0.0} °C, {s.RH:0}% RH → THI {thi:0.0} ({PoultryMath.HeatBand(thi, thiWarn, thiCrit)}). Increase ventilation, use foggers, give electrolytes, avoid feeding in peak heat.",
                            s.FarmId, null, $"HEAT_STRESS:{s.FarmId}", $"/Sensors/Readings?farmId={s.FarmId}"));
                }
                if (s.NH3.HasValue && s.NH3.Value > nh3Limit)
                    c.Add(new("AMMONIA", s.NH3.Value > nh3Limit * 1.4m ? "Critical" : "Warning", $"High ammonia at {s.FarmName}",
                        $"Average NH₃ {s.NH3:0.0} ppm exceeds the {nh3Limit:0} ppm limit. Improve ventilation and litter management.",
                        s.FarmId, null, $"AMMONIA:{s.FarmId}", $"/Sensors/Readings?farmId={s.FarmId}"));
            }

            // ---- VAX_OVERDUE ----
            var vax = await conn.QueryAsync<OverdueVaxRow>(
                "SELECT Id, VaccineName, DueDate, DaysOverdue, BatchId, BatchCode, FarmId, FarmName FROM vw_OverdueVaccinations");
            foreach (var v in vax)
                c.Add(new("VAX_OVERDUE", v.DaysOverdue > 3 ? "Critical" : "Warning", $"{v.VaccineName} overdue for {v.BatchCode}",
                    $"Due {v.DueDate:dd MMM yyyy} ({v.DaysOverdue} day(s) overdue) at {v.FarmName}.",
                    v.FarmId, v.BatchId, $"VAX_OVERDUE:{v.Id}", "/Health"));

            // ---- LOW_STOCK & EXPIRY ----
            var inv = await conn.QueryAsync<InventoryItem>("SELECT * FROM vw_InventoryStatus");
            foreach (var i in inv)
            {
                if (i.StockStatus != "In Stock")
                    c.Add(new("LOW_STOCK", i.StockStatus == "Out of Stock" ? "Critical" : "Warning", $"{i.Name}: {i.StockStatus}",
                        $"{i.Quantity:0.##} {i.Unit} on hand (minimum {i.MinimumLevel:0.##}). Raise a purchase order.",
                        null, null, $"LOW_STOCK:{i.Id}", $"/Inventory/Edit/{i.Id}"));
                if (i.ExpiryDate.HasValue && i.ExpiryDate.Value <= DateTime.Today.AddDays(30) && i.Quantity > 0)
                    c.Add(new("EXPIRY", i.ExpiryDate.Value < DateTime.Today ? "Critical" : "Info",
                        $"{i.Name} {(i.ExpiryDate.Value < DateTime.Today ? "expired" : "expiring soon")}",
                        $"Expiry {i.ExpiryDate:dd MMM yyyy}; {i.Quantity:0.##} {i.Unit} in stock.",
                        null, null, $"EXPIRY:{i.Id}", "/Inventory"));
            }

            // ---- CERT_EXPIRY ----
            var certs = await conn.QueryAsync<Certification>(
                @"SELECT c.*, f.Name AS FarmName FROM Certifications c INNER JOIN Farms f ON f.Id=c.FarmId
                  WHERE f.IsActive=1 AND c.ExpiryDate <= DATEADD(DAY,30,GETDATE())");
            foreach (var ce in certs)
                c.Add(new("CERT_EXPIRY", ce.ExpiryDate < DateTime.Today ? "Critical" : "Warning",
                    $"{ce.CertName} {(ce.ExpiryDate < DateTime.Today ? "expired" : "expiring")} – {ce.FarmName}",
                    $"Expiry date {ce.ExpiryDate:dd MMM yyyy}. Start the renewal process.",
                    ce.FarmId, null, $"CERT_EXPIRY:{ce.Id}", $"/Farm/Detail/{ce.FarmId}"));

            // ---- HARVEST_READY & GROWTH_LAG ----
            var standards = (await conn.QueryAsync<BreedStandard>("SELECT * FROM BreedStandards")).ToList();
            foreach (var b in batches)
            {
                if ((b.BatchType == "Broiler" && (b.AverageWeightKg >= targetKg || b.AgeInDays >= 35)) ||
                    (b.BatchType == "Sonali" && b.AgeInDays >= 60))
                    c.Add(new("HARVEST_READY", "Info", $"{b.BatchCode} ready for harvest",
                        $"Age {b.AgeInDays} d, average weight {b.AverageWeightKg:0.000} kg, {b.CurrentCount:N0} birds at {b.FarmName}. Plan catching and sales.",
                        b.FarmId, b.Id, $"HARVEST_READY:{b.Id}", "/Market/Create"));

                var curve = standards.Where(s => s.Breed == b.Breed).ToList();
                if (curve.Count > 0 && b.AgeInDays >= 7 && b.AverageWeightKg > 0)
                {
                    var std = PoultryMath.Standard(curve, b.AgeInDays);
                    if (std != null && std.Value.WeightGm > 0)
                    {
                        var lag = (std.Value.WeightGm - b.AverageWeightKg * 1000) / std.Value.WeightGm * 100;
                        if (lag >= lagPct)
                            c.Add(new("GROWTH_LAG", lag >= lagPct * 1.5m ? "Critical" : "Warning", $"{b.BatchCode} below growth standard",
                                $"Body weight {b.AverageWeightKg * 1000:0} g vs {b.Breed} standard {std.Value.WeightGm:0} g at day {b.AgeInDays} ({lag:0.0}% behind).",
                                b.FarmId, b.Id, $"GROWTH_LAG:{b.Id}", $"/Analytics/GrowthCurve?batchId={b.Id}"));
                    }
                }
            }

            // ---- SYNC_STALE ----
            var stale = await conn.QueryAsync<(int FieldOfficerId, string FullName, int Cnt)>(
                @"SELECT v.FieldOfficerId, fo.FullName, COUNT(*) AS Cnt FROM FarmVisits v
                  INNER JOIN FieldOfficers fo ON fo.Id=v.FieldOfficerId
                  WHERE v.SyncStatus='Pending' AND v.VisitDate < DATEADD(DAY,-@staleDays,CAST(GETDATE() AS DATE))
                  GROUP BY v.FieldOfficerId, fo.FullName", new { staleDays });
            foreach (var s in stale)
                c.Add(new("SYNC_STALE", "Warning", $"{s.FullName} has unsynced field data",
                    $"{s.Cnt} visit(s) older than {staleDays} days are still pending synchronisation.",
                    null, null, $"SYNC_STALE:{s.FieldOfficerId}", $"/FieldOfficers/Detail/{s.FieldOfficerId}"));

            // ---- DEVICE_OFFLINE ----
            var offline = await conn.QueryAsync<(int Id, string DeviceCode, string Name, int FarmId, DateTime? LastSeenAt)>(
                "SELECT Id, DeviceCode, Name, FarmId, LastSeenAt FROM SensorDevices WHERE IsActive=1 AND (LastSeenAt IS NULL OR LastSeenAt < DATEADD(HOUR,-6,GETDATE()))");
            foreach (var d in offline)
                c.Add(new("DEVICE_OFFLINE", "Warning", $"Sensor {d.DeviceCode} offline",
                    $"{d.Name} last reported {(d.LastSeenAt.HasValue ? d.LastSeenAt.Value.ToString("dd MMM HH:mm") : "never")}. Check power and Wi-Fi.",
                    d.FarmId, null, $"DEVICE_OFFLINE:{d.Id}", "/Sensors"));

            // ---- DISEASE_ACTIVE ----
            var dis = await conn.QueryAsync<DiseaseCase>(
                @"SELECT dc.*, b.BatchCode, f.Name AS FarmName FROM DiseaseCases dc
                  INNER JOIN Batches b ON b.Id=dc.BatchId INNER JOIN Farms f ON f.Id=b.FarmId
                  WHERE dc.Status <> 'Resolved' AND b.IsActive=1");
            foreach (var d in dis)
                c.Add(new("DISEASE_ACTIVE", "Warning", $"{d.DiseaseName} – {d.BatchCode}",
                    $"Active case since {d.ReportedDate:dd MMM}: {d.AffectedCount} birds affected at {d.FarmName}. Treatment: {d.Treatment}",
                    null, d.BatchId, $"DISEASE_ACTIVE:{d.Id}", "/Health"));

            // ---- persist (insert new, auto-resolve cleared) ----
            var open = (await conn.QueryAsync<(int Id, string RefKey, string RuleCode)>(
                "SELECT Id, RefKey, RuleCode FROM Alerts WHERE IsResolved=0 AND RefKey IS NOT NULL")).ToList();
            var openKeys = open.Select(o => o.RefKey).ToHashSet();
            int inserted = 0;
            foreach (var a in c.GroupBy(x => x.RefKey).Select(g => g.First()))
            {
                if (openKeys.Contains(a.RefKey)) continue;
                // do not re-raise a historical spike that a user already resolved
                if (a.Rule == "MORT_SPIKE" && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Alerts WHERE RefKey=@k", new { k = a.RefKey }) > 0) continue;
                await conn.ExecuteAsync(
                    @"INSERT INTO Alerts(RuleCode,Severity,Title,Message,FarmId,BatchId,RefKey,Link)
                      VALUES(@Rule,@Severity,@Title,@Message,@FarmId,@BatchId,@RefKey,@Link)", a);
                inserted++;
            }
            var currentKeys = c.Select(x => x.RefKey).ToHashSet();
            var cleared = open.Where(o => o.RuleCode != "MORT_SPIKE" && !currentKeys.Contains(o.RefKey)).Select(o => o.Id).ToList();
            if (cleared.Count > 0)
                await conn.ExecuteAsync("UPDATE Alerts SET IsResolved=1, ResolvedBy='system', ResolvedAt=GETDATE() WHERE Id IN @cleared", new { cleared });
            return inserted;
        }
        finally
        {
            Gate.Release();
        }
    }
}

/// <summary>Row of vw_OverdueVaccinations used by the VAX_OVERDUE alert rule.</summary>
public class OverdueVaxRow
{
    public int Id { get; set; }
    public string VaccineName { get; set; } = "";
    public DateTime DueDate { get; set; }
    public int DaysOverdue { get; set; }
    public int BatchId { get; set; }
    public string BatchCode { get; set; } = "";
    public int FarmId { get; set; }
    public string FarmName { get; set; } = "";
}
