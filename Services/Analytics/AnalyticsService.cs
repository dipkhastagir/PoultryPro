using System.Data;
using Dapper;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>Precision-production analytics: growth vs standard, efficiency, anomaly detection, heat stress,
/// risk scoring, forecasting, benchmarking and carbon accounting.</summary>
public interface IAnalyticsService
{
    Task<AnalyticsHubVm> GetHubAsync();
    Task<GrowthCurveVm> GetGrowthCurveAsync(int? batchId);
    Task<EfficiencyVm> GetEfficiencyAsync();
    Task<AnomalyVm> GetAnomaliesAsync(int? batchId);
    Task<HeatStressVm> GetHeatStressAsync();
    Task<RiskScoreVm> GetRiskScoresAsync();
    Task<ForecastVm> GetForecastAsync(int? batchId);
    Task<FeedForecastVm> GetFeedForecastAsync();
    Task<BenchmarkVm> GetBenchmarkAsync();
    Task<CarbonVm> GetCarbonAsync();
}

public class AnalyticsService(IDbConnectionFactory db, ISettingsService settings) : IAnalyticsService
{
    // ════════════════════════════ HUB ════════════════════════════
    public async Task<AnalyticsHubVm> GetHubAsync()
    {
        using var conn = db.Create();
        var all = await AnalyticsCore.BatchesAsync(conn);
        var active = all.Where(b => b.IsActive).ToList();
        var meatActive = active.Where(AnalyticsCore.IsMeat).ToList();
        var risk = await GetRiskScoresAsync();

        var vm = new AnalyticsHubVm
        {
            ActiveFlocks = active.Count,
            ActiveMeatFlocks = meatActive.Count,
            ActiveBirds = active.Sum(b => b.CurrentCount),
            AvgFcr = meatActive.Where(b => b.FCR > 0).Select(b => b.FCR).DefaultIfEmpty(0).Average(),
            AvgEpef = meatActive.Where(b => b.EPEF > 0).Select(b => b.EPEF).DefaultIfEmpty(0).Average(),
            AvgLivability = active.Select(b => b.LivabilityPct).DefaultIfEmpty(0).Average(),
            FlocksAtRisk = risk.Flocks.Count(f => f.Score >= 45),
            TopRisks = risk.Flocks.Take(4).ToList(),
            EpefBatches = all.Where(b => AnalyticsCore.IsMeat(b) && b.EPEF > 0).OrderBy(b => b.PlacementDate).ToList()
        };
        vm.OpenAlerts = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Alerts WHERE IsResolved = 0");
        vm.RunningTrials = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM FieldTrials WHERE Status IN ('Running','Planned')");
        vm.StandardBreeds = await conn.ExecuteScalarAsync<int>("SELECT COUNT(DISTINCT Breed) FROM BreedStandards");
        return vm;
    }

    // ════════════════════════════ GROWTH CURVE ════════════════════════════
    public async Task<GrowthCurveVm> GetGrowthCurveAsync(int? batchId)
    {
        using var conn = db.Create();
        var vm = new GrowthCurveVm { Batches = await AnalyticsCore.BatchOptionsAsync(conn, meatOnly: true) };
        if (vm.Batches.Count == 0) return vm;

        var pick = batchId.HasValue ? vm.Batches.FirstOrDefault(b => b.Id == batchId.Value) : null;
        pick ??= vm.Batches.FirstOrDefault(b => b.IsActive && b.HasStandard && b.BatchType == "Broiler")
              ?? vm.Batches.FirstOrDefault(b => b.IsActive && b.HasStandard)
              ?? vm.Batches.FirstOrDefault(b => b.IsActive) ?? vm.Batches[0];

        vm.Selected = (await AnalyticsCore.BatchesAsync(conn, "Id = @id", new { id = pick.Id })).FirstOrDefault();
        if (vm.Selected == null) return vm;
        var std = await AnalyticsCore.StandardsAsync(conn);
        var curve = AnalyticsCore.Curve(std, vm.Selected.Breed);
        vm.HasStandard = curve != null;
        var rows = await AnalyticsCore.DailyAsync(conn, "d.BatchId = @id", new { id = pick.Id });

        double cumFeed = 0; int cumMort = 0; double? prevW = null; int prevAge = -99;
        var initial = Math.Max(1, vm.Selected.InitialCount);
        foreach (var r in rows.Where(r => r.AgeInDays >= 0))
        {
            cumFeed += r.FeedPerBirdGm;
            cumMort += r.MortalityCount;
            var p = new GrowthPoint
            {
                Age = r.AgeInDays, Date = r.RecordDate,
                ActualGm = r.AverageWeightGm.HasValue ? (double)r.AverageWeightGm.Value : null,
                CumFeedPerBirdGm = cumFeed,
                CumMortPct = cumMort * 100.0 / initial
            };
            if (curve != null)
            {
                var s = PoultryMath.Standard(curve, r.AgeInDays);
                if (s != null)
                {
                    p.StdGm = (double)s.Value.WeightGm;
                    p.FcrStd = s.Value.Fcr > 0 ? (double)s.Value.Fcr : null;
                    p.StdCumMortPct = (double)s.Value.CumMortPct;
                    if (p.ActualGm.HasValue && p.StdGm > 0) p.DeviationPct = (p.ActualGm - p.StdGm) / p.StdGm * 100;
                }
            }
            if (p.ActualGm > 0 && r.AgeInDays >= 1) p.FcrActual = cumFeed / p.ActualGm;
            if (p.ActualGm.HasValue && prevW.HasValue && r.AgeInDays - prevAge == 1) p.DailyGainGm = p.ActualGm - prevW;
            if (p.ActualGm.HasValue) { prevW = p.ActualGm; prevAge = r.AgeInDays; }
            vm.Points.Add(p);
        }

        var last = vm.Points.LastOrDefault(p => p.ActualGm.HasValue);
        if (last != null)
        {
            vm.LatestAge = last.Age;
            vm.CurrentWeightGm = last.ActualGm;
            vm.StdWeightGm = last.StdGm;
            vm.DeviationPct = last.DeviationPct;
            vm.CurrentFcr = last.FcrActual;
            vm.StdFcr = last.FcrStd;
            if (curve != null)
            {
                vm.EquivalentAge = AnalyticsCore.EquivalentAge(curve, last.ActualGm!.Value);
                if (vm.EquivalentAge.HasValue) vm.GrowthLagDays = Math.Round(last.Age - vm.EquivalentAge.Value, 1);
            }
            var wk = vm.Points.LastOrDefault(p => p.ActualGm.HasValue && p.Age <= last.Age - 7);
            if (wk != null && last.Age - wk.Age > 0) vm.Adg7 = (last.ActualGm - wk.ActualGm) / (last.Age - wk.Age);
        }
        vm.Weekly = vm.Points.Where(p => p.Age % 7 == 0 || p == vm.Points.Last()).ToList();
        return vm;
    }

    // ════════════════════════════ EFFICIENCY ════════════════════════════
    public async Task<EfficiencyVm> GetEfficiencyAsync()
    {
        using var conn = db.Create();
        var rows = (await AnalyticsCore.BatchesAsync(conn, "BatchType <> 'Layer'"))
            .OrderByDescending(b => b.EPEF).ThenBy(b => b.FCR).ToList();
        var vm = new EfficiencyVm { Rows = rows };
        var e = rows.Where(r => r.EPEF > 0).Select(r => r.EPEF).OrderBy(x => x).ToList();
        if (e.Count > 0)
        {
            vm.MeanEpef = e.Average();
            vm.MedianEpef = e.Count % 2 == 1 ? e[e.Count / 2] : (e[e.Count / 2 - 1] + e[e.Count / 2]) / 2;
        }
        vm.MeanFcr = rows.Where(r => r.FCR > 0).Select(r => r.FCR).DefaultIfEmpty(0).Average();
        vm.Excellent = rows.Count(r => r.EPEF > 400);
        vm.Good = rows.Count(r => r.EPEF >= 300 && r.EPEF <= 400);
        vm.Poor = rows.Count(r => r.EPEF < 300);
        return vm;
    }

    // ════════════════════════════ ANOMALIES ════════════════════════════
    public async Task<AnomalyVm> GetAnomaliesAsync(int? batchId)
    {
        using var conn = db.Create();
        var active = await AnalyticsCore.BatchesAsync(conn, "IsActive = 1");
        var daily = await AnalyticsCore.DailyAsync(conn, "b.IsActive = 1");
        var vm = new AnomalyVm { Lambda = AnalyticsCore.EwmaLambda, L = AnalyticsCore.EwmaL, Baseline = AnalyticsCore.EwmaBaseline, RobustZThreshold = AnalyticsCore.RobustZThreshold };
        foreach (var b in active)
        {
            var rows = daily.Where(d => d.BatchId == b.Id).ToList();
            if (rows.Count == 0) continue;
            vm.Batches.Add(AnalyticsCore.Anomaly(b, rows));
        }
        vm.Batches = vm.Batches.OrderByDescending(x => x.SignalsLast7).ThenByDescending(x => x.Signals).ThenBy(x => x.BatchCode).ToList();

        if (batchId.HasValue && vm.Batches.All(x => x.BatchId != batchId.Value))
        {
            // a closed batch can still be analysed retrospectively
            var b = (await AnalyticsCore.BatchesAsync(conn, "Id = @id", new { id = batchId.Value })).FirstOrDefault();
            if (b != null)
            {
                var rows = await AnalyticsCore.DailyAsync(conn, "d.BatchId = @id", new { id = b.Id });
                if (rows.Count > 0) vm.Selected = AnalyticsCore.Anomaly(b, rows);
            }
        }
        vm.Selected ??= batchId.HasValue ? vm.Batches.FirstOrDefault(x => x.BatchId == batchId.Value) : null;
        vm.Selected ??= vm.Batches.FirstOrDefault();
        return vm;
    }

    // ════════════════════════════ HEAT STRESS ════════════════════════════
    public async Task<HeatStressVm> GetHeatStressAsync()
    {
        using var conn = db.Create();
        var vm = new HeatStressVm
        {
            Warn = await settings.GetDecimalAsync("Alert.THIWarning", 80m),
            Crit = await settings.GetDecimalAsync("Alert.THICritical", 85m)
        };

        var farms = (await conn.QueryAsync<(int Id, string Name)>("SELECT Id, Name FROM Farms WHERE IsActive = 1 ORDER BY Name")).ToList();
        var latest = (await conn.QueryAsync<(int FarmId, int Devices, DateTime? LastReading, decimal? T, decimal? RH, decimal? NH3)>(
            @"SELECT d.FarmId, COUNT(DISTINCT d.Id) AS Devices, MAX(r.ReadingTime) AS LastReading,
                     AVG(r.TemperatureC) AS T, AVG(r.HumidityPercent) AS RH, AVG(r.AmmoniaPpm) AS NH3
              FROM SensorDevices d
              CROSS APPLY (SELECT TOP 1 sr.ReadingTime, sr.TemperatureC, sr.HumidityPercent, sr.AmmoniaPpm
                           FROM SensorReadings sr WHERE sr.DeviceId = d.Id ORDER BY sr.ReadingTime DESC) r
              WHERE d.IsActive = 1
              GROUP BY d.FarmId")).ToDictionary(x => x.FarmId);

        var hourly = (await conn.QueryAsync<(int FarmId, DateTime Hr, decimal? T, decimal? RH)>(
            @"SELECT FarmId, DATEADD(HOUR, DATEDIFF(HOUR, 0, ReadingTime), 0) AS Hr,
                     AVG(TemperatureC) AS T, AVG(HumidityPercent) AS RH
              FROM SensorReadings
              WHERE ReadingTime >= DATEADD(DAY, -7, GETDATE()) AND TemperatureC IS NOT NULL AND HumidityPercent IS NOT NULL
              GROUP BY FarmId, DATEADD(HOUR, DATEDIFF(HOUR, 0, ReadingTime), 0)")).ToList();

        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(-7 * 24);
        var hours = Enumerable.Range(0, 7 * 24 + 1).Select(h => start.AddHours(h)).ToList();
        vm.HourLabels = hours.Select(h => h.ToString("dd MMM HH:00")).ToList();

        foreach (var f in farms)
        {
            var row = new FarmThiRow { FarmId = f.Id, FarmName = f.Name };
            if (latest.TryGetValue(f.Id, out var l))
            {
                row.Devices = l.Devices; row.LastReading = l.LastReading;
                row.TemperatureC = l.T; row.HumidityPercent = l.RH; row.AmmoniaPpm = l.NH3;
                row.Fresh = l.LastReading.HasValue && l.LastReading.Value >= now.AddHours(-6);
                if (l.T.HasValue && l.RH.HasValue)
                {
                    row.Thi = PoultryMath.THI(l.T.Value, l.RH.Value);
                    row.Band = PoultryMath.HeatBand(row.Thi.Value, vm.Warn, vm.Crit);
                }
            }
            var fh = hourly.Where(h => h.FarmId == f.Id && h.T.HasValue && h.RH.HasValue)
                           .ToDictionary(h => h.Hr, h => (double)PoultryMath.THI(h.T!.Value, h.RH!.Value));
            if (fh.Count > 0)
            {
                row.HoursAboveWarn7d = fh.Values.Count(v => v >= (double)vm.Warn);
                var last24 = fh.Where(kv => kv.Key >= now.AddHours(-24)).Select(kv => kv.Value).ToList();
                if (last24.Count > 0) row.Max24hThi = (decimal)last24.Max();
                vm.Series.Add(new ThiSeries { FarmName = f.Name, Values = hours.Select(h => fh.TryGetValue(h, out var v) ? (double?)v : null).ToList() });
            }
            vm.Farms.Add(row);
        }

        // per batch, last 14 days of daily records (meat birds only from day 21)
        var active = await AnalyticsCore.BatchesAsync(conn, "IsActive = 1");
        var daily = await AnalyticsCore.DailyAsync(conn, "b.IsActive = 1 AND d.RecordDate >= DATEADD(DAY, -14, CAST(GETDATE() AS DATE))");
        foreach (var b in active)
        {
            var meat = AnalyticsCore.IsMeat(b);
            var rows = daily.Where(d => d.BatchId == b.Id && d.Thi.HasValue && (!meat || d.AgeInDays >= 21)).ToList();
            var br = new BatchThiRow { BatchId = b.Id, BatchCode = b.BatchCode, FarmName = b.FarmName, BatchType = b.BatchType, AgeInDays = b.AgeInDays, Eligible = rows.Count > 0, Days = rows.Count };
            if (rows.Count > 0)
            {
                br.MeanThi = rows.Average(r => r.Thi!.Value);
                br.MaxThi = rows.Max(r => r.Thi!.Value);
                br.DaysAlert = rows.Count(r => r.Thi >= (double)vm.Warn);
                br.DaysDanger = rows.Count(r => r.Thi >= (double)vm.Crit);
                var hot = rows.Where(r => r.Thi >= (double)vm.Warn).ToList();
                var cool = rows.Where(r => r.Thi < (double)vm.Warn).ToList();
                if (hot.Count > 0) br.MortHot = hot.Average(r => r.MortPct);
                if (cool.Count > 0) br.MortNormal = cool.Average(r => r.MortPct);
                br.Band = PoultryMath.HeatBand((decimal)br.MaxThi.Value, vm.Warn, vm.Crit);
            }
            vm.Batches.Add(br);
        }
        vm.Batches = vm.Batches.OrderByDescending(b => b.MaxThi ?? 0).ToList();

        // dose–response: all history, same eligibility rule
        var allBatches = (await AnalyticsCore.BatchesAsync(conn)).ToDictionary(b => b.Id);
        var hist = await AnalyticsCore.DailyAsync(conn, "d.TemperatureC IS NOT NULL AND d.HumidityPercent IS NOT NULL");
        var pts = hist.Where(d => allBatches.TryGetValue(d.BatchId, out var b) && (!AnalyticsCore.IsMeat(b) || d.AgeInDays >= 21) && d.StartCount > 0)
                      .Select(d => new[] { Math.Round(d.Thi!.Value, 1), Math.Round(d.MortPct, 3) }).ToList();
        vm.Scatter = pts;
        vm.ScatterN = pts.Count;
        if (pts.Count >= 3)
        {
            var x = pts.Select(p => p[0]).ToList(); var y = pts.Select(p => p[1]).ToList();
            vm.PearsonR = PoultryMath.Pearson(x, y);
            vm.PearsonP = AnalyticsCore.PearsonP(vm.PearsonR, pts.Count);
            var lr = PoultryMath.LinearRegression(x, y);
            vm.Slope = lr.Slope; vm.Intercept = lr.Intercept;
        }
        return vm;
    }

    // ════════════════════════════ RISK SCORE ════════════════════════════
    public async Task<RiskScoreVm> GetRiskScoresAsync()
    {
        using var conn = db.Create();
        var vm = new RiskScoreVm
        {
            Warn = await settings.GetDecimalAsync("Alert.THIWarning", 80m),
            Crit = await settings.GetDecimalAsync("Alert.THICritical", 85m)
        };
        var active = await AnalyticsCore.BatchesAsync(conn, "IsActive = 1");
        if (active.Count == 0) return vm;
        var daily = await AnalyticsCore.DailyAsync(conn, "b.IsActive = 1");
        var std = await AnalyticsCore.StandardsAsync(conn);

        var sensor = (await conn.QueryAsync<(int FarmId, DateTime? LastReading, decimal? T, decimal? RH)>(
            @"SELECT d.FarmId, MAX(r.ReadingTime) AS LastReading, AVG(r.TemperatureC) AS T, AVG(r.HumidityPercent) AS RH
              FROM SensorDevices d
              CROSS APPLY (SELECT TOP 3 sr.ReadingTime, sr.TemperatureC, sr.HumidityPercent FROM SensorReadings sr
                           WHERE sr.DeviceId = d.Id ORDER BY sr.ReadingTime DESC) r
              WHERE d.IsActive = 1 GROUP BY d.FarmId")).ToDictionary(x => x.FarmId);
        var vax = (await conn.QueryAsync<(int BatchId, int DaysOverdue, string VaccineName)>(
            "SELECT BatchId, DaysOverdue, VaccineName FROM vw_OverdueVaccinations")).ToList();
        var disease = (await conn.QueryAsync<(int BatchId, string DiseaseName, int AffectedCount)>(
            "SELECT BatchId, DiseaseName, AffectedCount FROM DiseaseCases WHERE Status <> 'Resolved'")).ToList();
        var bio = (await conn.QueryAsync<(int FarmId, decimal ScorePct, DateTime AuditDate)>(
            @"SELECT a.FarmId, a.ScorePct, a.AuditDate FROM BiosecurityAudits a
              WHERE a.Id = (SELECT TOP 1 x.Id FROM BiosecurityAudits x WHERE x.FarmId = a.FarmId ORDER BY x.AuditDate DESC, x.Id DESC)"))
            .ToDictionary(x => x.FarmId);

        foreach (var b in active)
        {
            var rows = daily.Where(d => d.BatchId == b.Id).OrderBy(d => d.RecordDate).ToList();
            var fr = new FlockRisk { BatchId = b.Id, BatchCode = b.BatchCode, FarmName = b.FarmName, Breed = b.Breed, BatchType = b.BatchType, AgeInDays = b.AgeInDays, Birds = b.CurrentCount };

            // 1. mortality surveillance (25)
            var an = rows.Count > 0 ? AnalyticsCore.Anomaly(b, rows) : null;
            var last7 = an?.Series.Where(p => p.Date >= DateTime.Today.AddDays(-7)).ToList() ?? new();
            double maxZ = last7.Select(p => p.RobustZ ?? 0).DefaultIfEmpty(0).Max();
            double s = (an?.SignalsLast7 ?? 0) > 0 ? 1 : 0;
            double mortF = AnalyticsCore.Clamp01(0.6 * s + 0.4 * AnalyticsCore.Clamp01(maxZ / 5.0));
            fr.Components.Add(new RiskComponent
            {
                Name = "Mortality trend", Icon = "bi-activity", Weight = 25, Fraction = mortF,
                Evidence = an == null ? "No daily records yet" :
                    $"{an.SignalsLast7} EWMA signal(s) in last 7 d; max robust z = {maxZ:0.0}; latest {an.LatestMortPct:0.00}%/day"
            });

            // 2. heat stress (20)
            double? thi = null; string thiSrc = "";
            if (sensor.TryGetValue(b.FarmId, out var sn) && sn.T.HasValue && sn.RH.HasValue && sn.LastReading >= DateTime.Now.AddHours(-6))
            { thi = (double)PoultryMath.THI(sn.T.Value, sn.RH.Value); thiSrc = "live sensors"; }
            else
            {
                var recent = rows.Where(r => r.Thi.HasValue).TakeLast(3).ToList();
                if (recent.Count > 0) { thi = recent.Average(r => r.Thi!.Value); thiSrc = "last 3 daily records"; }
            }
            double heatF = thi.HasValue ? AnalyticsCore.Clamp01((thi.Value - 75) / ((double)vm.Crit + 5 - 75)) : 0;
            bool young = AnalyticsCore.IsMeat(b) && b.AgeInDays < 21;
            if (young) heatF *= 0.5;
            fr.Components.Add(new RiskComponent
            {
                Name = "Heat stress (THI)", Icon = "bi-thermometer-sun", Weight = 20, Fraction = heatF,
                Evidence = thi.HasValue ? $"THI {thi:0.0} from {thiSrc} ({PoultryMath.HeatBand((decimal)thi.Value, vm.Warn, vm.Crit)}){(young ? "; ×0.5 for chicks < 21 d" : "")}" : "No climate data"
            });

            // 3. overdue vaccinations (15)
            var bv = vax.Where(v => v.BatchId == b.Id).ToList();
            double vaxF = bv.Count == 0 ? 0 : AnalyticsCore.Clamp01(0.5 * bv.Count + 0.05 * bv.Max(v => v.DaysOverdue));
            fr.Components.Add(new RiskComponent
            {
                Name = "Overdue vaccinations", Icon = "bi-shield-plus", Weight = 15, Fraction = vaxF,
                Evidence = bv.Count == 0 ? "Programme up to date" : $"{bv.Count} overdue ({string.Join(", ", bv.Select(v => $"{v.VaccineName} +{v.DaysOverdue} d"))})"
            });

            // 4. active disease (15)
            var bd = disease.Where(d => d.BatchId == b.Id).ToList();
            double affected = b.CurrentCount > 0 ? bd.Sum(d => d.AffectedCount) / (double)b.CurrentCount : 0;
            double disF = bd.Count == 0 ? 0 : AnalyticsCore.Clamp01(0.5 * bd.Count + 5 * affected);
            fr.Components.Add(new RiskComponent
            {
                Name = "Active disease", Icon = "bi-virus", Weight = 15, Fraction = disF,
                Evidence = bd.Count == 0 ? "No open cases" : $"{string.Join(", ", bd.Select(d => d.DiseaseName))} — {affected * 100:0.0}% of flock affected"
            });

            // 5. growth lag (15)
            var curve = AnalyticsCore.Curve(std, b.Breed);
            var lw = rows.LastOrDefault(r => r.AverageWeightGm > 0);
            double growF = 0; string growEv = "No breed standard";
            if (curve != null && lw != null && lw.AgeInDays >= 7)
            {
                var sw = AnalyticsCore.StdWeight(curve, lw.AgeInDays) ?? 0;
                if (sw > 0)
                {
                    var lag = (sw - (double)lw.AverageWeightGm!.Value) / sw * 100;
                    growF = AnalyticsCore.Clamp01(lag / 20.0);
                    growEv = $"{lw.AverageWeightGm:0} g vs standard {sw:0} g at day {lw.AgeInDays} ({(lag >= 0 ? lag.ToString("0.0") + "% behind" : (-lag).ToString("0.0") + "% ahead")})";
                }
            }
            else if (curve != null) growEv = "Too young / no weighing yet";
            fr.Components.Add(new RiskComponent { Name = "Growth lag vs standard", Icon = "bi-graph-down", Weight = 15, Fraction = growF, Evidence = growEv });

            // 6. farm biosecurity (10)
            double bioF; string bioEv;
            if (bio.TryGetValue(b.FarmId, out var ba)) { bioF = AnalyticsCore.Clamp01((100 - (double)ba.ScorePct) / 50.0); bioEv = $"Last audit {ba.ScorePct:0}% on {ba.AuditDate:dd MMM yyyy}"; }
            else { bioF = 0.5; bioEv = "No audit on record (scored as unknown = 50%)"; }
            fr.Components.Add(new RiskComponent { Name = "Farm biosecurity", Icon = "bi-shield-lock", Weight = 10, Fraction = bioF, Evidence = bioEv });

            var top = fr.Components.OrderByDescending(c => c.Points).First();
            fr.Recommendation = top.Points < 2 ? "No material risk factor — continue routine monitoring." : top.Name switch
            {
                "Mortality trend" => "Investigate the mortality rise: post-mortem 3–5 fresh carcasses, check water line, feed and ventilation.",
                "Heat stress (THI)" => "Increase air speed / foggers, add electrolytes + vitamin C, withdraw feed during the hottest hours.",
                "Overdue vaccinations" => "Administer the overdue vaccine(s) today and record the batch number in the Health module.",
                "Active disease" => "Follow the treatment plan, isolate affected birds and review biosecurity at the shed entrance.",
                "Growth lag vs standard" => "Check feed quality and intake, stocking density, water flow and brooding temperature.",
                _ => "Close the open biosecurity audit findings (footbaths, visitor control, rodent control)."
            };
            vm.Flocks.Add(fr);
        }
        vm.Flocks = vm.Flocks.OrderByDescending(f => f.Score).ToList();
        return vm;
    }

    // ════════════════════════════ HARVEST FORECAST ════════════════════════════
    public async Task<ForecastVm> GetForecastAsync(int? batchId)
    {
        using var conn = db.Create();
        var vm = new ForecastVm
        {
            BroilerTargetKg = await settings.GetDecimalAsync("Harvest.TargetWeightKg", 2.0m),
            SonaliTargetKg = await settings.GetDecimalAsync("Harvest.SonaliTargetWeightKg", 1.0m),
            BroilerPrice = await settings.GetDecimalAsync("Price.BroilerPerKg", 165m),
            SonaliPrice = await settings.GetDecimalAsync("Price.SonaliPerKg", 260m)
        };
        var active = (await AnalyticsCore.BatchesAsync(conn, "IsActive = 1 AND BatchType <> 'Layer'")).OrderBy(b => b.PlacementDate).ToList();
        if (active.Count == 0) return vm;
        var daily = await AnalyticsCore.DailyAsync(conn, "b.IsActive = 1 AND b.BatchType <> 'Layer'");
        var std = await AnalyticsCore.StandardsAsync(conn);
        var feedCost = (await conn.QueryAsync<(string BatchType, decimal Cost)>(
            "SELECT BatchType, AVG(CostPerKg) AS Cost FROM FeedFormulations WHERE IsActive = 1 AND CostPerKg > 0 GROUP BY BatchType"))
            .ToDictionary(x => x.BatchType, x => x.Cost, StringComparer.OrdinalIgnoreCase);
        var invCost = await conn.ExecuteScalarAsync<decimal?>("SELECT AVG(UnitCost) FROM InventoryItems WHERE Category = 'Feed' AND UnitCost > 0") ?? 63m;

        foreach (var b in active)
        {
            var rows = daily.Where(d => d.BatchId == b.Id).OrderBy(d => d.RecordDate).ToList();
            var curve = AnalyticsCore.Curve(std, b.Breed);
            var sonali = AnalyticsCore.IsSonali(b);
            var f = new HarvestForecast
            {
                BatchId = b.Id, BatchCode = b.BatchCode, FarmName = b.FarmName, Breed = b.Breed, BatchType = b.BatchType,
                AgeInDays = b.AgeInDays, PlacementDate = b.PlacementDate, Birds = b.CurrentCount,
                TargetKg = (double)(sonali ? vm.SonaliTargetKg : vm.BroilerTargetKg),
                PricePerKg = sonali ? vm.SonaliPrice : vm.BroilerPrice,
                HasStandard = curve != null,
                FeedCostPerKg = feedCost.TryGetValue(b.BatchType, out var fc) ? fc : invCost
            };
            var target = f.TargetKg * 1000;
            var weights = rows.Where(r => r.AverageWeightGm > 0).Select(r => new[] { (double)r.AgeInDays, (double)r.AverageWeightGm!.Value }).ToList();
            f.Actual = weights;
            var lastW = weights.LastOrDefault();
            if (lastW != null) { f.CurrentWeightGm = lastW[1]; f.LastWeighAge = (int)lastW[0]; }
            var a0 = f.LastWeighAge ?? b.AgeInDays;
            f.Ready = f.CurrentWeightGm >= target;

            // Method A — breed standard scaled by observed performance ratio
            if (curve != null)
            {
                f.PerfRatio = AnalyticsCore.PerformanceRatio(rows, curve);
                var ratio = f.PerfRatio ?? 1.0;
                if (f.Ready) f.DayA = a0;
                else
                    for (int t = a0; t <= a0 + 150; t++)
                        if (ratio * (AnalyticsCore.StdWeight(curve, t) ?? 0) >= target) { f.DayA = t; break; }
                var endA = (f.DayA ?? a0 + 14) + 4;
                for (int t = a0; t <= endA; t++) f.ProjA.Add(new[] { (double)t, Math.Round(ratio * (AnalyticsCore.StdWeight(curve, t) ?? 0), 1) });
            }

            // Method B — OLS on the last 7–10 daily weights
            var recent = weights.TakeLast(10).ToList();
            f.PointsB = recent.Count;
            if (recent.Count >= 3)
            {
                var lr = PoultryMath.LinearRegression(recent.Select(p => p[0]).ToList(), recent.Select(p => p[1]).ToList());
                f.SlopeB = lr.Slope; f.InterceptB = lr.Intercept; f.R2B = lr.R2;
                if (f.Ready) f.DayB = a0;
                else if (lr.Slope > 0)
                {
                    // guard the double → int conversion: a near-flat trend gives an astronomically late (or NaN) day
                    var dayB = Math.Ceiling((target - lr.Intercept) / lr.Slope);
                    if (!double.IsNaN(dayB) && dayB <= a0 + 200) f.DayB = Math.Max(a0, (int)dayB);
                }
                if (f.DayB.HasValue && f.DayB.Value - a0 > 200) f.DayB = null;
                var endB = (f.DayB ?? a0 + 14) + 4;
                for (int t = (int)recent[0][0]; t <= endB; t++) f.ProjB.Add(new[] { (double)t, Math.Round(lr.Intercept + lr.Slope * t, 1) });
            }

            if (curve != null)
            {
                var xmax = Math.Max(Math.Max(f.DayA ?? 0, f.DayB ?? 0), a0) + 5;
                for (int t = 0; t <= xmax; t++) f.Standard.Add(new[] { (double)t, Math.Round(AnalyticsCore.StdWeight(curve, t) ?? 0, 1) });
            }

            f.HarvestDay = f.DayA ?? f.DayB;
            f.Method = f.DayA.HasValue ? "A · scaled breed standard" : f.DayB.HasValue ? "B · linear regression" : "—";
            if (f.DayA.HasValue) { f.DateA = b.PlacementDate.AddDays(f.DayA.Value); f.DaysLeftA = Math.Max(0, (f.DateA.Value - DateTime.Today).Days); }
            if (f.DayB.HasValue) { f.DateB = b.PlacementDate.AddDays(f.DayB.Value); f.DaysLeftB = Math.Max(0, (f.DateB.Value - DateTime.Today).Days); }
            if (f.HarvestDay.HasValue)
            {
                f.HarvestDate = b.PlacementDate.AddDays(f.HarvestDay.Value);
                if (f.HarvestDate < DateTime.Today) f.HarvestDate = DateTime.Today;
                f.DaysLeft = Math.Max(0, (f.HarvestDate.Value - DateTime.Today).Days);
            }

            // survival, live mass, revenue and feed to harvest
            var mort7 = rows.TakeLast(7).ToList();
            f.DailyMortPct = mort7.Count > 0 ? mort7.Average(r => r.MortPct) : 0;
            var days = f.DaysLeft ?? 0;
            var surv = Math.Pow(1 - f.DailyMortPct / 100.0, days);
            f.ProjectedBirds = (int)Math.Round(b.CurrentCount * surv);
            if (f.Ready) f.ProjectedWeightKg = (f.CurrentWeightGm ?? 0) / 1000;
            else if (f.DayA.HasValue) f.ProjectedWeightKg = (f.PerfRatio ?? 1) * (AnalyticsCore.StdWeight(curve, f.DayA.Value) ?? target) / 1000;
            else if (f.DayB.HasValue) f.ProjectedWeightKg = ((f.InterceptB ?? 0) + (f.SlopeB ?? 0) * f.DayB.Value) / 1000;
            else f.ProjectedWeightKg = (f.CurrentWeightGm ?? 0) / 1000;
            f.ProjectedLiveKg = f.ProjectedBirds * f.ProjectedWeightKg;
            f.ProjectedRevenue = (decimal)f.ProjectedLiveKg * f.PricePerKg;

            var intake = AnalyticsCore.IntakeRatio(rows, curve);
            var obsFeed = rows.TakeLast(7).Where(r => r.FeedPerBirdGm > 0).Select(r => r.FeedPerBirdGm).DefaultIfEmpty(0).Average();
            double feed = 0;
            for (int d = 1; d <= days; d++)
            {
                var age = b.AgeInDays + d;
                var gpb = curve != null ? (AnalyticsCore.StdFeed(curve, age) ?? 0) * intake : obsFeed;
                feed += b.CurrentCount * Math.Pow(1 - f.DailyMortPct / 100.0, d) * gpb / 1000.0;
            }
            f.FeedToHarvestKg = feed;

            if (curve == null) f.Note = "No breed standard: only the regression method is available.";
            else if (f.PerfRatio.HasValue && Math.Abs(f.PerfRatio.Value - 1) > 0.08)
                f.Note = $"Flock is growing at {f.PerfRatio:P0} of the {b.Breed} standard.";
            if (f.DayA.HasValue && f.DayB.HasValue && Math.Abs(f.DayA.Value - f.DayB.Value) > 4)
                f.Note += (f.Note.Length > 0 ? " " : "") + $"Methods disagree by {Math.Abs(f.DayA.Value - f.DayB.Value)} days — linear trend ignores the curvature of growth.";
            if (sonali && curve != null && f.HarvestDay > curve[^1].AgeDay)
                f.Note += (f.Note.Length > 0 ? " " : "") + $"Target lies beyond the last tabulated standard age ({curve[^1].AgeDay} d); the curve was extrapolated.";
            vm.Rows.Add(f);
        }
        vm.Selected = (batchId.HasValue ? vm.Rows.FirstOrDefault(r => r.BatchId == batchId.Value) : null) ?? vm.Rows.FirstOrDefault(r => !r.Ready) ?? vm.Rows.First();
        return vm;
    }

    // ════════════════════════════ FEED FORECAST ════════════════════════════
    public class Formulation
    {
        public string Stage { get; set; } = "";
        public string BatchType { get; set; } = "";
        public int FromAgeDay { get; set; }
        public int ToAgeDay { get; set; }
        public decimal CostPerKg { get; set; }
    }

    private static string StageFor(List<Formulation> forms, string batchType, int age)
    {
        var mine = forms.Where(f => string.Equals(f.BatchType, batchType, StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.FromAgeDay).ToList();
        if (mine.Count > 0)
        {
            var hit = mine.FirstOrDefault(f => age >= f.FromAgeDay && age <= f.ToAgeDay);
            if (hit != null) return hit.Stage;
            return age > mine.Max(f => f.ToAgeDay) ? mine.OrderBy(f => f.ToAgeDay).Last().Stage : mine.First().Stage;
        }
        return batchType switch
        {
            "Layer" => "Layer",
            "Sonali" => "Grower",
            _ => age <= 10 ? "Pre-Starter" : age <= 22 ? "Starter" : age <= 32 ? "Grower" : "Finisher"
        };
    }

    private static bool ItemMatches(InventoryItem i, string batchType, string stage)
    {
        var n = i.Name;
        bool typeOk = n.Contains(batchType, StringComparison.OrdinalIgnoreCase);
        if (!typeOk) return false;
        if (stage.Equals("Pre-Starter", StringComparison.OrdinalIgnoreCase))
            return n.Contains("Pre-Starter", StringComparison.OrdinalIgnoreCase) || n.Contains("Starter", StringComparison.OrdinalIgnoreCase);
        if (stage.Contains("Layer", StringComparison.OrdinalIgnoreCase))
            return true; // any "Layer ..." feed item
        return n.Contains(stage, StringComparison.OrdinalIgnoreCase);
    }

    private static double KgOf(InventoryItem i) => i.Unit.Trim().ToLowerInvariant() switch
    {
        "ton" or "tons" or "t" or "tonne" => (double)i.Quantity * 1000,
        "bag" or "bags" => (double)i.Quantity * 50,
        "g" => (double)i.Quantity / 1000,
        _ => (double)i.Quantity
    };

    public async Task<FeedForecastVm> GetFeedForecastAsync()
    {
        using var conn = db.Create();
        var vm = new FeedForecastVm();
        vm.DayLabels = Enumerable.Range(1, 14).Select(d => DateTime.Today.AddDays(d).ToString("dd MMM")).ToList();
        var forms = (await conn.QueryAsync<Formulation>(
            "SELECT Stage, BatchType, FromAgeDay, ToAgeDay, CostPerKg FROM FeedFormulations WHERE IsActive = 1")).ToList();
        vm.FeedItems = (await conn.QueryAsync<InventoryItem>("SELECT * FROM vw_InventoryStatus WHERE Category = 'Feed' ORDER BY Name")).ToList();
        var active = await AnalyticsCore.BatchesAsync(conn, "IsActive = 1");
        var daily = await AnalyticsCore.DailyAsync(conn, "b.IsActive = 1 AND d.RecordDate >= DATEADD(DAY, -14, CAST(GETDATE() AS DATE))");
        var std = await AnalyticsCore.StandardsAsync(conn);

        foreach (var b in active)
        {
            var rows = daily.Where(d => d.BatchId == b.Id).OrderBy(d => d.RecordDate).ToList();
            var curve = AnalyticsCore.IsLayer(b) ? null : AnalyticsCore.Curve(std, b.Breed);
            var r = new FeedDemandRow { BatchId = b.Id, BatchCode = b.BatchCode, FarmName = b.FarmName, BatchType = b.BatchType, Breed = b.Breed, AgeInDays = b.AgeInDays, Birds = b.CurrentCount };
            var mort = rows.TakeLast(7).Select(x => x.MortPct).DefaultIfEmpty(0).Average() / 100.0;
            var obs = rows.TakeLast(7).Where(x => x.FeedPerBirdGm > 0).Select(x => x.FeedPerBirdGm).DefaultIfEmpty(0).Average();

            // meat flocks stop eating at harvest: expected harvest date, or within 3 days if already overdue
            int lastDay = 14;
            if (AnalyticsCore.IsMeat(b) && b.ExpectedHarvestDate.HasValue)
            {
                var dh = (b.ExpectedHarvestDate.Value.Date - DateTime.Today).Days;
                lastDay = Math.Clamp(dh < 1 ? 3 : dh, 0, 14);
            }

            if (AnalyticsCore.IsLayer(b)) r.Basis = $"{vm.LayerGmPerHen:0} g/hen/day (layer allowance)";
            else if (curve != null) { r.IntakeRatio = AnalyticsCore.IntakeRatio(rows, curve); r.Basis = $"{b.Breed} standard × intake ratio {r.IntakeRatio:0.00}"; }
            else r.Basis = obs > 0 ? $"Observed {obs:0} g/bird/day (no standard)" : "No standard and no intake records";

            for (int d = 1; d <= 14; d++)
            {
                var age = b.AgeInDays + d;
                r.DailyStage[d - 1] = StageFor(forms, b.BatchType, age);
                if (d > lastDay) { r.DailyKg[d - 1] = 0; continue; }
                var birds = b.CurrentCount * Math.Pow(1 - mort, d);
                double gpb = AnalyticsCore.IsLayer(b) ? vm.LayerGmPerHen
                           : curve != null ? (AnalyticsCore.StdFeed(curve, age) ?? obs) * r.IntakeRatio : obs;
                r.DailyKg[d - 1] = Math.Round(birds * gpb / 1000.0, 1);
            }
            if (lastDay < 14) r.Basis += $"; harvest expected in {lastDay} d";
            r.StagesText = string.Join(" → ", r.DailyStage.Where((s, i) => r.DailyKg[i] > 0).Distinct());
            vm.Rows.Add(r);
        }

        // stage totals → supply groups (stages sharing the same stock items are pooled)
        var stageDemand = new Dictionary<(string Type, string Stage), double[]>();
        foreach (var r in vm.Rows)
            for (int i = 0; i < 14; i++)
            {
                if (r.DailyKg[i] <= 0) continue;
                var key = (r.BatchType, r.DailyStage[i]);
                if (!stageDemand.ContainsKey(key)) stageDemand[key] = new double[14];
                stageDemand[key][i] += r.DailyKg[i];
            }

        var groups = new Dictionary<string, FeedStageTotal>();
        foreach (var kv in stageDemand.OrderBy(k => k.Key.Type).ThenBy(k => forms.FirstOrDefault(f => f.Stage == k.Key.Stage)?.FromAgeDay ?? 0))
        {
            var items = vm.FeedItems.Where(i => ItemMatches(i, kv.Key.Type, kv.Key.Stage)).ToList();
            var gkey = items.Count > 0 ? string.Join(",", items.Select(i => i.Id).OrderBy(x => x)) : "none:" + kv.Key.Type + kv.Key.Stage;
            if (!groups.TryGetValue(gkey, out var g))
            {
                g = new FeedStageTotal
                {
                    BatchType = kv.Key.Type, Stage = kv.Key.Stage,
                    StockKg = items.Sum(KgOf),
                    Items = items.Count > 0 ? string.Join(", ", items.Select(i => i.Name)) : "",
                    CostPerKg = items.Count > 0 ? items.Average(i => i.UnitCost)
                              : forms.FirstOrDefault(f => f.Stage == kv.Key.Stage && f.BatchType == kv.Key.Type)?.CostPerKg ?? 0,
                    ReorderKg = (double)items.Sum(i => i.MinimumLevel) // temporarily holds the minimum level
                };
                groups[gkey] = g;
            }
            else if (!g.Stage.Split(" + ").Contains(kv.Key.Stage)) g.Stage += " + " + kv.Key.Stage;
            g.DemandKg += kv.Value.Sum();
        }
        foreach (var g in groups.Values)
        {
            var minLevel = g.ReorderKg;
            g.AvgDailyKg = g.DemandKg / 14.0;
            g.DaysCover = g.AvgDailyKg > 0 ? g.StockKg / g.AvgDailyKg : null;
            var need = g.DemandKg + minLevel - g.StockKg;
            g.ReorderKg = g.DaysCover < 14 || g.StockKg < g.DemandKg ? Math.Ceiling(Math.Max(0, need) / 50.0) * 50 : 0;
            g.Status = string.IsNullOrEmpty(g.Items) ? "No stock item" : g.DaysCover < 7 ? "Critical" : g.DaysCover < 14 ? "Reorder" : "OK";
        }
        vm.Stages = groups.Values.OrderBy(g => g.DaysCover ?? -1).ToList();
        vm.TotalDemandKg = vm.Rows.Sum(r => r.TotalKg);
        vm.TotalStockKg = vm.FeedItems.Sum(KgOf);
        vm.ReorderCost = vm.Stages.Sum(s => (decimal)s.ReorderKg * s.CostPerKg);
        vm.Rows = vm.Rows.OrderByDescending(r => r.TotalKg).ToList();
        return vm;
    }

    // ════════════════════════════ BENCHMARK ════════════════════════════
    public async Task<BenchmarkVm> GetBenchmarkAsync()
    {
        using var conn = db.Create();
        var vm = new BenchmarkVm
        {
            Rows = (await conn.QueryAsync<FarmBenchmarkRow>("SELECT * FROM vw_FarmBenchmark ORDER BY AvgEPEF DESC")).ToList()
        };
        var meat = vm.Rows.Where(r => BenchmarkVm.IsMeat(r) && r.TotalBatches > 0).ToList();
        var fcrs = meat.Where(r => r.AvgFCR > 0).ToList();
        var epefs = meat.Where(r => r.AvgEPEF > 0).ToList();
        var bios = vm.Rows.Where(r => r.LastBiosecurityPct.HasValue).ToList();

        decimal bestFcr = fcrs.Count > 0 ? fcrs.Min(r => r.AvgFCR) : 0;
        decimal bestEpef = epefs.Count > 0 ? epefs.Max(r => r.AvgEPEF) : 0;
        decimal bestMort = meat.Count > 0 ? meat.Min(r => r.AvgMortalityRate) : 0;
        vm.BestFcrFarm = fcrs.OrderBy(r => r.AvgFCR).FirstOrDefault()?.FarmId;
        vm.BestEpefFarm = epefs.OrderByDescending(r => r.AvgEPEF).FirstOrDefault()?.FarmId;
        vm.BestMortFarm = meat.OrderBy(r => r.AvgMortalityRate).FirstOrDefault()?.FarmId;
        vm.BestBioFarm = bios.OrderByDescending(r => r.LastBiosecurityPct).FirstOrDefault()?.FarmId;

        foreach (var r in meat)
        {
            double sF = r.AvgFCR > 0 && bestFcr > 0 ? (double)(bestFcr / r.AvgFCR) * 100 : 0;
            double sE = bestEpef > 0 ? (double)(r.AvgEPEF / bestEpef) * 100 : 0;
            double sM = r.AvgMortalityRate <= 0 ? 100 : bestMort <= 0 ? 0 : (double)(bestMort / r.AvgMortalityRate) * 100;
            double sB = (double)(r.LastBiosecurityPct ?? 0);
            vm.Scores[r.FarmId] = new[] { Math.Round(sF, 1), Math.Round(sE, 1), Math.Round(sM, 1), Math.Round(sB, 1) };
            vm.Composite[r.FarmId] = Math.Round((sF + sE + sM + sB) / 4, 1);
        }
        vm.BestOverallFarm = vm.Composite.OrderByDescending(kv => kv.Value).Select(kv => (int?)kv.Key).FirstOrDefault();
        return vm;
    }

    // ════════════════════════════ CARBON ════════════════════════════
    public async Task<CarbonVm> GetCarbonAsync()
    {
        using var conn = db.Create();
        var vm = new CarbonVm
        {
            FeedFactor = await settings.GetDecimalAsync("Carbon.FeedFactor", 0.80m),
            ChickFactor = await settings.GetDecimalAsync("Carbon.ChickFactor", 0.12m),
            KwhPerBird = await settings.GetDecimalAsync("Carbon.ElectricityKwhPerBird", 0.35m),
            GridFactor = await settings.GetDecimalAsync("Carbon.GridFactor", 0.67m),
            ManureFactor = await settings.GetDecimalAsync("Carbon.ManureFactor", 0.08m)
        };
        var batches = (await AnalyticsCore.BatchesAsync(conn, "BatchType <> 'Layer'")).OrderBy(b => b.PlacementDate).ToList();
        foreach (var b in batches)
        {
            var share = b.IsActive ? Math.Min(1.0, b.AgeInDays / (double)AnalyticsCore.CycleDays(b)) : 1.0;
            var r = new CarbonRow
            {
                BatchId = b.Id, BatchCode = b.BatchCode, FarmName = b.FarmName, Breed = b.Breed, IsActive = b.IsActive,
                AgeInDays = b.AgeInDays, ChicksPlaced = b.InitialCount, CycleShare = share,
                FeedKg = (double)b.TotalFeedKg, LiveKg = (double)b.LiveMassKg
            };
            r.FeedCo2 = r.FeedKg * (double)vm.FeedFactor;
            r.ChickCo2 = b.InitialCount * (double)vm.ChickFactor;
            r.ElectricityCo2 = b.InitialCount * (double)vm.KwhPerBird * (double)vm.GridFactor * share;
            r.ManureCo2 = b.InitialCount * (double)vm.ManureFactor * share;
            vm.Rows.Add(r);
        }
        return vm;
    }
}
