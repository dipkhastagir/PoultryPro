using System.Data;
using Dapper;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>
/// Shared data loaders and numerical helpers for the analytics, trial and research services.
/// Everything here is deterministic and documented so it can be reproduced in a methods section.
/// </summary>
public static class AnalyticsCore
{
    public const double EwmaLambda = 0.3;
    public const double EwmaL = 3.0;
    public const int EwmaBaseline = 7;
    public const double RobustZThreshold = 3.5;

    // ─────────────── loaders ───────────────

    public static async Task<List<Batch>> BatchesAsync(IDbConnection conn, string? where = null, object? param = null)
    {
        var sql = "SELECT * FROM vw_BatchPerformance" + (string.IsNullOrWhiteSpace(where) ? "" : " WHERE " + where) +
                  " ORDER BY IsActive DESC, PlacementDate DESC";
        return (await conn.QueryAsync<Batch>(sql, param)).ToList();
    }

    /// <summary>Daily records aggregated to one row per batch-day (protects against duplicate entries for the same day).</summary>
    public static async Task<List<BatchDayRecord>> DailyAsync(IDbConnection conn, string? where = null, object? param = null)
    {
        var sql = @"SELECT d.BatchId, MIN(d.FarmId) AS FarmId, d.RecordDate,
                           DATEDIFF(DAY, MIN(b.PlacementDate), d.RecordDate) AS AgeInDays,
                           MIN(d.BirdCount) AS BirdCount, SUM(d.MortalityCount) AS MortalityCount, SUM(d.CullingCount) AS CullingCount,
                           SUM(d.FeedConsumptionKg) AS FeedConsumptionKg, SUM(d.WaterConsumptionLtr) AS WaterConsumptionLtr,
                           AVG(d.TemperatureC) AS TemperatureC, AVG(d.HumidityPercent) AS HumidityPercent,
                           AVG(NULLIF(d.AverageWeightGm, 0)) AS AverageWeightGm
                    FROM DailyFarmRecords d INNER JOIN Batches b ON b.Id = d.BatchId" +
                  (string.IsNullOrWhiteSpace(where) ? "" : " WHERE " + where) +
                  " GROUP BY d.BatchId, d.RecordDate ORDER BY d.BatchId, d.RecordDate";
        return (await conn.QueryAsync<BatchDayRecord>(sql, param)).ToList();
    }

    public static async Task<Dictionary<string, List<BreedStandard>>> StandardsAsync(IDbConnection conn)
    {
        var rows = await conn.QueryAsync<BreedStandard>("SELECT * FROM BreedStandards ORDER BY Breed, AgeDay");
        return rows.GroupBy(r => r.Breed.Trim(), StringComparer.OrdinalIgnoreCase)
                   .ToDictionary(g => g.Key, g => g.OrderBy(x => x.AgeDay).ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public static List<BreedStandard>? Curve(Dictionary<string, List<BreedStandard>> std, string? breed)
        => breed != null && std.TryGetValue(breed.Trim(), out var c) && c.Count > 0 ? c : null;

    public static async Task<List<AnalyticsBatchOption>> BatchOptionsAsync(IDbConnection conn, bool meatOnly)
    {
        var std = await StandardsAsync(conn);
        var batches = await BatchesAsync(conn, meatOnly ? "BatchType <> 'Layer'" : null);
        return batches.Select(b => new AnalyticsBatchOption
        {
            Id = b.Id, BatchCode = b.BatchCode, Breed = b.Breed, BatchType = b.BatchType, FarmName = b.FarmName,
            AgeInDays = b.AgeInDays, IsActive = b.IsActive, HasStandard = Curve(std, b.Breed) != null
        }).ToList();
    }

    // ─────────────── classification ───────────────

    public static bool IsLayer(Batch b) => string.Equals(b.BatchType, "Layer", StringComparison.OrdinalIgnoreCase);
    public static bool IsMeat(Batch b) => !IsLayer(b);
    public static bool IsSonali(Batch b) => string.Equals(b.BatchType, "Sonali", StringComparison.OrdinalIgnoreCase)
                                            || b.Breed.Contains("Sonali", StringComparison.OrdinalIgnoreCase);

    /// <summary>Nominal grow-out length (days) used for pro-rating per-cycle factors.</summary>
    public static int CycleDays(Batch b)
    {
        if (b.ExpectedHarvestDate.HasValue)
        {
            var d = (b.ExpectedHarvestDate.Value - b.PlacementDate).Days;
            if (d > 7) return d;
        }
        return IsSonali(b) ? 63 : IsLayer(b) ? 500 : 35;
    }

    // ─────────────── breed-standard interpolation ───────────────

    /// <summary>
    /// Value of a breed-standard column at a (fractional) age. Inside the tabulated range this is the same linear
    /// interpolation as <see cref="PoultryMath.Standard"/>; beyond the last tabulated age the last segment's slope is
    /// extended (linear extrapolation), which is needed by the harvest forecaster for slow-growing breeds.
    /// </summary>
    public static double? StdAt(List<BreedStandard>? curve, double age, Func<BreedStandard, decimal> sel)
    {
        if (curve == null || curve.Count == 0) return null;
        if (age <= curve[0].AgeDay) return (double)sel(curve[0]);
        var last = curve[^1];
        if (age >= last.AgeDay)
        {
            if (curve.Count < 2) return (double)sel(last);
            var prev = curve[^2];
            var slope = (double)(sel(last) - sel(prev)) / Math.Max(1, last.AgeDay - prev.AgeDay);
            return Math.Max(0, (double)sel(last) + slope * (age - last.AgeDay));
        }
        for (int i = 0; i < curve.Count - 1; i++)
        {
            var lo = curve[i]; var hi = curve[i + 1];
            if (age >= lo.AgeDay && age <= hi.AgeDay)
            {
                if (hi.AgeDay == lo.AgeDay) return (double)sel(lo);
                var f = (age - lo.AgeDay) / (hi.AgeDay - lo.AgeDay);
                return (double)sel(lo) + ((double)sel(hi) - (double)sel(lo)) * f;
            }
        }
        return (double)sel(last);
    }

    public static double? StdWeight(List<BreedStandard>? curve, double age) => StdAt(curve, age, s => s.TargetWeightGm);
    public static double? StdFeed(List<BreedStandard>? curve, double age) => StdAt(curve, age, s => s.DailyFeedGm);

    /// <summary>Age (days) at which the standard reaches a given weight — the "physiological age" of a flock.</summary>
    public static double? EquivalentAge(List<BreedStandard>? curve, double weightGm)
    {
        if (curve == null || curve.Count == 0 || weightGm <= 0) return null;
        var maxAge = curve[^1].AgeDay * 2.0 + 30;
        for (double a = 0; a <= maxAge; a += 0.1)
        {
            var w = StdWeight(curve, a);
            if (w.HasValue && w.Value >= weightGm) return Math.Round(a, 1);
        }
        return null;
    }

    /// <summary>Observed feed intake ÷ standard intake over the last (up to) 7 records; 1 = on standard. Clamped 0.6–1.4.</summary>
    public static double IntakeRatio(List<BatchDayRecord> rows, List<BreedStandard>? curve)
    {
        if (curve == null) return 1;
        var recent = rows.OrderBy(r => r.RecordDate).TakeLast(7).ToList();
        double obs = 0, std = 0;
        foreach (var r in recent)
        {
            var s = StdFeed(curve, r.AgeInDays) ?? 0;
            if (s < 10 || r.BirdCount <= 0 || r.FeedConsumptionKg <= 0) continue;
            obs += r.FeedPerBirdGm; std += s;
        }
        return std > 0 ? Math.Clamp(obs / std, 0.6, 1.4) : 1;
    }

    /// <summary>Observed performance ratio = mean(actual ÷ standard weight) over the last 3 weighings.</summary>
    public static double? PerformanceRatio(List<BatchDayRecord> rows, List<BreedStandard>? curve)
    {
        if (curve == null) return null;
        var pts = rows.Where(r => r.AverageWeightGm > 0 && r.AgeInDays >= 3).OrderBy(r => r.RecordDate).TakeLast(3).ToList();
        var ratios = pts.Select(r => (double)r.AverageWeightGm!.Value / (StdWeight(curve, r.AgeInDays) ?? 0))
                        .Where(x => !double.IsInfinity(x) && !double.IsNaN(x) && x > 0).ToList();
        return ratios.Count == 0 ? null : ratios.Average();
    }

    // ─────────────── mortality surveillance ───────────────

    /// <summary>Baseline mean / SD exactly as used inside <see cref="PoultryMath.Ewma"/>.</summary>
    public static (double Mu, double Sd) EwmaBaselineStats(IReadOnlyList<double> series, int baseline = EwmaBaseline)
    {
        if (series.Count == 0) return (0, 0);
        var basePts = series.Take(Math.Max(2, Math.Min(baseline, series.Count))).ToList();
        double mu = basePts.Average();
        double sd = Math.Sqrt(basePts.Sum(v => (v - mu) * (v - mu)) / Math.Max(1, basePts.Count - 1));
        if (sd < 1e-9) sd = Math.Max(0.5, mu * 0.25);
        return (mu, sd);
    }

    /// <summary>
    /// Builds the EWMA control chart + robust z-scores for one flock's daily mortality %.
    /// Meat flocks with at least 7 post-brooding observations are monitored from day 8 onwards because first-week
    /// chick mortality follows a different (higher) regime and would inflate the Phase-I baseline.
    /// </summary>
    public static AnomalyBatchRow Anomaly(Batch b, List<BatchDayRecord> rows)
    {
        var ordered = rows.OrderBy(r => r.RecordDate).ToList();
        bool exclude = IsMeat(b) && ordered.Count(r => r.AgeInDays > 7) >= 7;
        var mon = exclude ? ordered.Where(r => r.AgeInDays > 7).ToList() : ordered;
        var vals = mon.Select(r => r.MortPct).ToList();
        var ew = PoultryMath.Ewma(vals, EwmaLambda, EwmaL, EwmaBaseline);
        var (mu, sd) = EwmaBaselineStats(vals);

        var row = new AnomalyBatchRow
        {
            BatchId = b.Id, BatchCode = b.BatchCode, FarmName = b.FarmName, Breed = b.Breed, AgeInDays = b.AgeInDays,
            Days = mon.Count, BaselineMean = mu, BaselineSd = sd, BroodingExcluded = exclude
        };
        for (int i = 0; i < mon.Count; i++)
        {
            double? z = null;
            if (i >= 7) z = PoultryMath.RobustZ(vals.Skip(i - 7).Take(7).ToList(), vals[i]);
            row.Series.Add(new AnomalyPoint
            {
                Date = mon[i].RecordDate, Age = mon[i].AgeInDays, Deaths = mon[i].MortalityCount, StartBirds = mon[i].StartCount,
                MortPct = Math.Round(vals[i], 4), Ewma = ew[i].Ewma, Ucl = ew[i].Ucl, Signal = ew[i].Signal, RobustZ = z.HasValue ? Math.Round(z.Value, 2) : null
            });
        }
        var sig = row.Series.Where(p => p.Signal).ToList();
        row.Signals = sig.Count;
        row.SignalsLast7 = sig.Count(p => p.Date >= DateTime.Today.AddDays(-7));
        row.LastSignalDate = sig.LastOrDefault()?.Date;
        row.LastSignalAge = sig.LastOrDefault()?.Age;
        row.MaxRobustZ14 = row.Series.TakeLast(14).Select(p => p.RobustZ ?? 0).DefaultIfEmpty(0).Max();
        row.LatestMortPct = row.Series.LastOrDefault()?.MortPct ?? 0;
        return row;
    }

    // ─────────────── statistics ───────────────

    public static double Mean(IReadOnlyList<double> v) => v.Count == 0 ? 0 : v.Average();
    public static double Sd(IReadOnlyList<double> v)
    {
        if (v.Count < 2) return 0;
        var m = v.Average();
        return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / (v.Count - 1));
    }

    /// <summary>Two-sided p-value of a Pearson r with n observations (t = r·√((n−2)/(1−r²)), df = n−2).</summary>
    public static double PearsonP(double r, int n)
    {
        if (n < 4) return 1;
        if (Math.Abs(r) >= 0.999999) return 0;
        var t = r * Math.Sqrt((n - 2) / (1 - r * r));
        return PoultryMath.PValueTwoSided(t, n - 2);
    }

    public static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1);
}
