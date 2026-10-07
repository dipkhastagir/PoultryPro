using Dapper;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

public interface IFeedService
{
    Task<List<FeedFormulation>> GetAllAsync(bool activeOnly = false);
    Task<FeedFormulation?> GetByIdAsync(int id);
    Task<int> CreateAsync(FeedFormulation f);
    Task UpdateAsync(FeedFormulation f);
    /// <summary>Feed requirement plan for every active flock (current stage ration × breed-standard intake).</summary>
    Task<FeedPlannerVm> GetPlannerAsync();
    /// <summary>The ration that applies to a flock type at a given age (nearest stage when outside every range).</summary>
    FeedFormulation? StageFor(IEnumerable<FeedFormulation> formulations, string batchType, int ageDays);
}

public class FeedService(IDbConnectionFactory db) : IFeedService
{
    /// <summary>Typical intake of a laying hen in production (g/bird/day).</summary>
    public const decimal LayerDailyFeedGm = 112m;

    public async Task<List<FeedFormulation>> GetAllAsync(bool activeOnly = false)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<FeedFormulation>(
            @"SELECT * FROM FeedFormulations WHERE (@activeOnly = 0 OR IsActive = 1)
              ORDER BY CASE BatchType WHEN 'Broiler' THEN 0 WHEN 'Sonali' THEN 1 WHEN 'Layer' THEN 2 ELSE 3 END, FromAgeDay, Name",
            new { activeOnly });
        return rows.ToList();
    }

    public async Task<FeedFormulation?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<FeedFormulation>("SELECT * FROM FeedFormulations WHERE Id=@id", new { id });
    }

    public async Task<int> CreateAsync(FeedFormulation f)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO FeedFormulations (Name, Stage, BatchType, FromAgeDay, ToAgeDay, CrudeProteinPct, EnergyKcalPerKg, CalciumPct,
                                            PhosphorusPct, LysinePct, CostPerKg, Ingredients, Notes, IsActive)
              VALUES (@Name, @Stage, @BatchType, @FromAgeDay, @ToAgeDay, @CrudeProteinPct, @EnergyKcalPerKg, @CalciumPct,
                      @PhosphorusPct, @LysinePct, @CostPerKg, @Ingredients, @Notes, @IsActive);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", f);
    }

    public async Task UpdateAsync(FeedFormulation f)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE FeedFormulations SET Name=@Name, Stage=@Stage, BatchType=@BatchType, FromAgeDay=@FromAgeDay, ToAgeDay=@ToAgeDay,
                     CrudeProteinPct=@CrudeProteinPct, EnergyKcalPerKg=@EnergyKcalPerKg, CalciumPct=@CalciumPct,
                     PhosphorusPct=@PhosphorusPct, LysinePct=@LysinePct, CostPerKg=@CostPerKg, Ingredients=@Ingredients,
                     Notes=@Notes, IsActive=@IsActive
              WHERE Id=@Id", f);
    }

    public FeedFormulation? StageFor(IEnumerable<FeedFormulation> formulations, string batchType, int ageDays)
    {
        var list = formulations.Where(f => f.IsActive && string.Equals(f.BatchType, batchType, StringComparison.OrdinalIgnoreCase))
                               .OrderBy(f => f.FromAgeDay).ToList();
        if (list.Count == 0) return null;
        var exact = list.FirstOrDefault(f => ageDays >= f.FromAgeDay && ageDays <= f.ToAgeDay);
        if (exact != null) return exact;
        if (ageDays < list[0].FromAgeDay) return list[0];
        // past the last range or in a gap → the latest stage already started
        return list.Where(f => f.FromAgeDay <= ageDays).OrderByDescending(f => f.FromAgeDay).FirstOrDefault() ?? list[^1];
    }

    public async Task<FeedPlannerVm> GetPlannerAsync()
    {
        using var conn = db.Create();
        var batches = (await conn.QueryAsync<Batch>(
            "SELECT * FROM vw_BatchPerformance WHERE IsActive = 1 ORDER BY BatchType, FarmName, PlacementDate")).ToList();
        var standards = (await conn.QueryAsync<BreedStandard>("SELECT * FROM BreedStandards")).ToList();
        var formulations = await GetAllAsync(true);

        var vm = new FeedPlannerVm();
        foreach (var b in batches)
        {
            var age = Math.Max(0, b.AgeInDays);
            var row = new FeedPlanRow { Batch = b, AgeDays = age, Birds = b.CurrentCount };
            row.Formulation = StageFor(formulations, b.BatchType, age);
            row.NextFormulation = formulations
                .Where(f => string.Equals(f.BatchType, b.BatchType, StringComparison.OrdinalIgnoreCase) && f.FromAgeDay > age)
                .OrderBy(f => f.FromAgeDay).FirstOrDefault();
            row.DaysToNextStage = row.NextFormulation == null ? null : row.NextFormulation.FromAgeDay - age;

            if (string.Equals(b.BatchType, "Layer", StringComparison.OrdinalIgnoreCase))
            {
                row.FeedPerBirdGm = LayerDailyFeedGm;
                row.FeedBasis = "Layer standard 112 g/hen/day";
            }
            else
            {
                var curve = standards.Where(s => s.Breed == b.Breed).ToList();
                if (curve.Count == 0)
                    curve = standards.Where(s => s.Breed == (b.BatchType == "Sonali" ? "Sonali" : "Cobb 500")).ToList();
                var std = PoultryMath.Standard(curve, Math.Max(age, 1));
                if (std.HasValue && std.Value.DailyFeedGm > 0)
                {
                    row.FeedPerBirdGm = Math.Round(std.Value.DailyFeedGm, 1);
                    row.FeedBasis = $"{(curve.FirstOrDefault()?.Breed ?? b.Breed)} standard, day {age}";
                }
                else
                {
                    row.FeedPerBirdGm = Math.Round(b.AvgFeedPerBirdDayGm, 1);
                    row.FeedBasis = "Flock average to date";
                }
            }
            vm.Rows.Add(row);
        }

        vm.Totals = vm.Rows
            .GroupBy(r => r.Formulation == null ? ("(no ration defined)", "—") : (r.Formulation.Name, r.Formulation.Stage))
            .Select(g => new FeedPlanTotal
            {
                Formulation = g.Key.Item1, Stage = g.Key.Item2, Flocks = g.Count(), Birds = g.Sum(r => r.Birds),
                DailyKg = g.Sum(r => r.DailyKg), WeeklyKg = g.Sum(r => r.WeeklyKg), CostPerDay = g.Sum(r => r.CostPerDay)
            })
            .OrderByDescending(t => t.DailyKg).ToList();
        return vm;
    }
}
