using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>Controlled field trials (treatment vs control flock) with Welch t-tests on age-aligned daily data.</summary>
public interface ITrialService
{
    Task<IEnumerable<FieldTrial>> GetAllAsync();
    Task<FieldTrial?> GetByIdAsync(int id);
    Task<int> CreateAsync(FieldTrial t);
    Task UpdateAsync(FieldTrial t);
    Task CompleteAsync(int id, string status, string? conclusion);
    Task<bool> CodeExistsAsync(string code, int exceptId = 0);
    Task<string> NextCodeAsync();
    Task<List<AnalyticsBatchOption>> GetBatchOptionsAsync();
    Task<TrialDetailVm?> GetDetailAsync(int id);
}

public class TrialService(IDbConnectionFactory db) : ITrialService
{
    private const string SelectSql = @"SELECT t.*, tb.BatchCode AS TreatmentBatchCode, cb.BatchCode AS ControlBatchCode,
                                              tb.Breed AS TreatmentBreed, cb.Breed AS ControlBreed,
                                              tf.Name AS TreatmentFarmName, cf.Name AS ControlFarmName
                                       FROM FieldTrials t
                                       INNER JOIN Batches tb ON tb.Id = t.TreatmentBatchId
                                       INNER JOIN Batches cb ON cb.Id = t.ControlBatchId
                                       INNER JOIN Farms tf ON tf.Id = tb.FarmId
                                       INNER JOIN Farms cf ON cf.Id = cb.FarmId";

    public async Task<IEnumerable<FieldTrial>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<FieldTrial>(SelectSql +
            " ORDER BY CASE t.Status WHEN 'Running' THEN 0 WHEN 'Planned' THEN 1 WHEN 'Completed' THEN 2 ELSE 3 END, t.StartDate DESC");
    }

    public async Task<FieldTrial?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<FieldTrial>(SelectSql + " WHERE t.Id = @id", new { id });
    }

    public async Task<int> CreateAsync(FieldTrial t)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO FieldTrials (TrialCode, Title, Objective, Hypothesis, Intervention, TreatmentBatchId, ControlBatchId,
                                       PrimaryMetric, StartDate, EndDate, Status, Conclusion, PrincipalInvestigator)
              VALUES (@TrialCode, @Title, @Objective, @Hypothesis, @Intervention, @TreatmentBatchId, @ControlBatchId,
                      @PrimaryMetric, @StartDate, @EndDate, @Status, @Conclusion, @PrincipalInvestigator);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", t);
    }

    public async Task UpdateAsync(FieldTrial t)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE FieldTrials SET TrialCode=@TrialCode, Title=@Title, Objective=@Objective, Hypothesis=@Hypothesis,
                     Intervention=@Intervention, TreatmentBatchId=@TreatmentBatchId, ControlBatchId=@ControlBatchId,
                     PrimaryMetric=@PrimaryMetric, StartDate=@StartDate, EndDate=@EndDate, Status=@Status,
                     Conclusion=@Conclusion, PrincipalInvestigator=@PrincipalInvestigator
              WHERE Id=@Id", t);
    }

    public async Task CompleteAsync(int id, string status, string? conclusion)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE FieldTrials SET Status=@status, Conclusion=@conclusion,
                     EndDate = CASE WHEN EndDate IS NULL OR EndDate > CAST(GETDATE() AS DATE) THEN CAST(GETDATE() AS DATE) ELSE EndDate END
              WHERE Id=@id", new { id, status, conclusion });
    }

    public async Task<bool> CodeExistsAsync(string code, int exceptId = 0)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM FieldTrials WHERE TrialCode=@code AND Id<>@exceptId", new { code, exceptId }) > 0;
    }

    public async Task<string> NextCodeAsync()
    {
        using var conn = db.Create();
        var prefix = $"TR-{DateTime.Today.Year}-";
        var codes = await conn.QueryAsync<string>("SELECT TrialCode FROM FieldTrials WHERE TrialCode LIKE @p", new { p = prefix + "%" });
        var max = codes.Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return prefix + (max + 1).ToString("00");
    }

    public async Task<List<AnalyticsBatchOption>> GetBatchOptionsAsync()
    {
        using var conn = db.Create();
        return await AnalyticsCore.BatchOptionsAsync(conn, meatOnly: false);
    }

    // ───────────────────────── analysis ─────────────────────────

    private static bool LowerIsBetter(string metric) => metric is "FCR" or "Mortality";

    public async Task<TrialDetailVm?> GetDetailAsync(int id)
    {
        var trial = await GetByIdAsync(id);
        if (trial == null) return null;
        using var conn = db.Create();
        var batches = await AnalyticsCore.BatchesAsync(conn, "Id IN @ids", new { ids = new[] { trial.TreatmentBatchId, trial.ControlBatchId } });
        var vm = new TrialDetailVm
        {
            Trial = trial,
            Treatment = batches.FirstOrDefault(b => b.Id == trial.TreatmentBatchId),
            Control = batches.FirstOrDefault(b => b.Id == trial.ControlBatchId)
        };
        if (vm.Treatment == null || vm.Control == null) return vm;
        var daily = await AnalyticsCore.DailyAsync(conn, "d.BatchId IN @ids", new { ids = new[] { trial.TreatmentBatchId, trial.ControlBatchId } });
        var dT = daily.Where(d => d.BatchId == trial.TreatmentBatchId && d.AgeInDays >= 0).ToList();
        var dC = daily.Where(d => d.BatchId == trial.ControlBatchId && d.AgeInDays >= 0).ToList();

        // ── side-by-side KPIs ──
        double? Mortality(Batch b) => b.InitialCount > 0 ? b.MortalityCount * 100.0 / b.InitialCount : null;
        void Add(string name, string unit, double? t, double? c, bool lower, int dec, string key)
            => vm.Metrics.Add(new TrialMetricRow { Metric = name, Unit = unit, Treatment = t, Control = c, LowerIsBetter = lower, Decimals = dec, IsPrimary = key == trial.PrimaryMetric });
        var T = vm.Treatment; var C = vm.Control;
        Add("Feed conversion ratio", "", Nz(T.FCR), Nz(C.FCR), true, 3, "FCR");
        Add("EPEF", "", Nz(T.EPEF), Nz(C.EPEF), false, 0, "EPEF");
        Add(T.IsActive || C.IsActive ? "Body weight (current)" : "Final body weight", "kg", Nz(T.AverageWeightKg), Nz(C.AverageWeightKg), false, 3, "Weight");
        Add("Average daily gain", "g/d", Nz(T.AvgDailyGainGm), Nz(C.AvgDailyGainGm), false, 1, "ADG");
        Add("Livability", "%", (double)T.LivabilityPct, (double)C.LivabilityPct, false, 2, "Livability");
        Add("Mortality", "%", Mortality(T), Mortality(C), true, 2, "Mortality");
        Add("Feed intake", "g/bird/d", Nz(T.AvgFeedPerBirdDayGm), Nz(C.AvgFeedPerBirdDayGm), true, 1, "-");
        Add("Age", "days", T.AgeInDays, C.AgeInDays, false, 0, "-");
        vm.Metrics.Last().LowerIsBetter = false;

        // ── age-aligned series ──
        var maxAge = Math.Max(dT.Select(d => d.AgeInDays).DefaultIfEmpty(0).Max(), dC.Select(d => d.AgeInDays).DefaultIfEmpty(0).Max());
        var byT = dT.GroupBy(d => d.AgeInDays).ToDictionary(g => g.Key, g => g.First());
        var byC = dC.GroupBy(d => d.AgeInDays).ToDictionary(g => g.Key, g => g.First());
        int cmT = 0, cmC = 0;
        for (int a = 1; a <= maxAge; a++)
        {
            vm.Ages.Add(a);
            byT.TryGetValue(a, out var rt); byC.TryGetValue(a, out var rc);
            vm.WeightT.Add(rt?.AverageWeightGm is decimal wt ? (double)wt : null);
            vm.WeightC.Add(rc?.AverageWeightGm is decimal wc ? (double)wc : null);
            if (rt != null) cmT += rt.MortalityCount;
            if (rc != null) cmC += rc.MortalityCount;
            vm.CumMortT.Add(rt != null ? Math.Round(cmT * 100.0 / Math.Max(1, T.InitialCount), 3) : null);
            vm.CumMortC.Add(rc != null ? Math.Round(cmC * 100.0 / Math.Max(1, C.InitialCount), 3) : null);
        }

        // ── Welch tests on ages observed in BOTH flocks ──
        var common = byT.Keys.Intersect(byC.Keys).OrderBy(a => a).ToList();
        List<double> Gains(Dictionary<int, BatchDayRecord> m) => common
            .Where(a => m.TryGetValue(a - 1, out var p) && p.AverageWeightGm > 0 && m[a].AverageWeightGm > 0 && byT.ContainsKey(a - 1) && byC.ContainsKey(a - 1))
            .Select(a => (double)(m[a].AverageWeightGm!.Value - m[a - 1].AverageWeightGm!.Value)).ToList();
        vm.Tests.Add(Test("Daily weight gain", "g/bird/day", Gains(byT), Gains(byC), lowerBetter: false));
        vm.Tests.Add(Test("Daily mortality", "% of flock", common.Select(a => byT[a].MortPct).ToList(), common.Select(a => byC[a].MortPct).ToList(), lowerBetter: true));
        vm.Tests.Add(Test("Daily feed intake", "g/bird/day", common.Where(a => byT[a].FeedPerBirdGm > 0 && byC[a].FeedPerBirdGm > 0).Select(a => byT[a].FeedPerBirdGm).ToList(),
                                                             common.Where(a => byT[a].FeedPerBirdGm > 0 && byC[a].FeedPerBirdGm > 0).Select(a => byC[a].FeedPerBirdGm).ToList(), lowerBetter: true));

        // ── hypothesis check on the primary metric ──
        var primary = vm.Metrics.FirstOrDefault(m => m.IsPrimary);
        vm.PrimaryDirection = LowerIsBetter(trial.PrimaryMetric) ? "lower is better" : "higher is better";
        if (primary != null)
        {
            var h = trial.Hypothesis ?? "";
            var mPct = Regex.Match(h, @"(\d+(?:\.\d+)?)\s*%");
            var mPts = Regex.Match(h, @"(\d+(?:\.\d+)?)\s*(points|pts|point)", RegexOptions.IgnoreCase);
            if (mPct.Success) vm.HypothesisThreshold = double.Parse(mPct.Groups[1].Value, CultureInfo.InvariantCulture);
            else if (mPts.Success) { vm.HypothesisThreshold = double.Parse(mPts.Groups[1].Value, CultureInfo.InvariantCulture); vm.HypothesisIsPoints = true; }

            vm.ObservedEffect = vm.HypothesisIsPoints
                ? (primary.Diff.HasValue ? (primary.LowerIsBetter ? -primary.Diff : primary.Diff) : null)
                : primary.ImprovementPct;
            var unit = vm.HypothesisIsPoints ? " points" : "%";
            if (!vm.ObservedEffect.HasValue) vm.HypothesisVerdict = "Insufficient data to evaluate the primary metric.";
            else if (vm.HypothesisThreshold.HasValue)
            {
                var ok = vm.ObservedEffect.Value >= vm.HypothesisThreshold.Value;
                vm.HypothesisVerdict = ok
                    ? $"Supported: the treatment improved {primary.Metric} by {vm.ObservedEffect:0.0}{unit}, meeting the hypothesised ≥ {vm.HypothesisThreshold:0.#}{unit}."
                    : vm.ObservedEffect.Value > 0
                        ? $"Not (yet) supported: improvement of {vm.ObservedEffect:0.0}{unit} is below the hypothesised {vm.HypothesisThreshold:0.#}{unit}."
                        : $"Not supported: the treatment performed {Math.Abs(vm.ObservedEffect.Value):0.0}{unit} worse than control on {primary.Metric}.";
                if (T.IsActive || C.IsActive) vm.HypothesisVerdict += " (Interim — flock(s) still running.)";
            }
            else vm.HypothesisVerdict = $"Observed effect on {primary.Metric}: {AnalyticsUi.Signed(vm.ObservedEffect, 1, unit)} in favour of the treatment (no numeric threshold found in the hypothesis).";
        }

        // ── auto-drafted conclusion ──
        var g = vm.Tests[0]; var mo = vm.Tests[1];
        vm.DraftConclusion =
            $"{trial.TrialCode}: {T.BatchCode} (treatment) vs {C.BatchCode} (control), {common.Count} age-matched days. " +
            (primary != null ? $"{primary.Metric}: {FmtNum(primary.Treatment, primary.Decimals)} vs {FmtNum(primary.Control, primary.Decimals)} ({AnalyticsUi.Signed(primary.ImprovementPct, 1, "%")} improvement). " : "") +
            $"Daily gain {g.MeanTreatment:0.0} vs {g.MeanControl:0.0} g/d (Welch t = {g.T:0.00}, df = {g.Df:0.0}, p = {AnalyticsUi.PValue(g.P)}); " +
            $"daily mortality {mo.MeanTreatment:0.000}% vs {mo.MeanControl:0.000}% (p = {AnalyticsUi.PValue(mo.P)}). " + vm.HypothesisVerdict;
        return vm;
    }

    private static double? Nz(decimal v) => v == 0 ? null : (double)v;
    private static string FmtNum(double? v, int dec) => v.HasValue ? v.Value.ToString("N" + dec, CultureInfo.InvariantCulture) : "—";

    private static TrialTestResult Test(string name, string unit, List<double> a, List<double> b, bool lowerBetter)
    {
        var r = new TrialTestResult
        {
            Variable = name, Unit = unit, NTreatment = a.Count, NControl = b.Count,
            MeanTreatment = AnalyticsCore.Mean(a), MeanControl = AnalyticsCore.Mean(b),
            SdTreatment = AnalyticsCore.Sd(a), SdControl = AnalyticsCore.Sd(b)
        };
        if (a.Count < 2 || b.Count < 2) { r.Interpretation = "Not enough paired observations for a test (need ≥ 2 per group)."; return r; }
        var (t, df) = PoultryMath.WelchT(a, b);
        r.T = t; r.Df = df; r.P = PoultryMath.PValueTwoSided(t, df);
        var pooled = Math.Sqrt((r.SdTreatment * r.SdTreatment + r.SdControl * r.SdControl) / 2);
        r.CohensD = pooled > 0 ? (r.MeanTreatment - r.MeanControl) / pooled : 0;
        var better = lowerBetter ? r.MeanTreatment < r.MeanControl : r.MeanTreatment > r.MeanControl;
        var size = Math.Abs(r.CohensD) >= 0.8 ? "large" : Math.Abs(r.CohensD) >= 0.5 ? "medium" : Math.Abs(r.CohensD) >= 0.2 ? "small" : "negligible";
        r.Interpretation = r.Significant
            ? $"Significant at α = 0.05: the treatment flock is {(better ? "better" : "worse")} ({size} effect, d = {r.CohensD:0.00})."
            : $"Not significant at α = 0.05 (p = {AnalyticsUi.PValue(r.P)}); the observed difference ({size} effect) may be due to chance.";
        return r;
    }
}
