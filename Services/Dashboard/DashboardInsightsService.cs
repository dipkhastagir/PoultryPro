using Dapper;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>
/// Operational analytics for the upgraded dashboard: flock performance vs. breed standard, 14-day
/// farm-wide mortality/feed trend, egg output, live climate per shed and the 7-day agenda.
/// </summary>
public interface IDashboardInsightsService
{
    Task FillAsync(DashboardVm vm, int trendDays = 14);
}

public class DashboardInsightsService(IDbConnectionFactory db, ISensorService sensors, ICalendarService calendar) : IDashboardInsightsService
{
    private static readonly string[] MeatTypes = { "Broiler", "Sonali" };

    public async Task FillAsync(DashboardVm vm, int trendDays = 14)
    {
        var today = DateTime.Today;
        using var conn = db.Create();

        // ---- Active flocks with breed-standard comparison ----
        var active = (await conn.QueryAsync<Batch>(
            "SELECT * FROM vw_BatchPerformance WHERE IsActive = 1 ORDER BY PlacementDate")).ToList();
        var standards = (await conn.QueryAsync<BreedStandard>("SELECT * FROM BreedStandards ORDER BY Breed, AgeDay"))
            .GroupBy(s => s.Breed).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        vm.Flocks = active.Select(b =>
        {
            var row = new DashFlockRow { Batch = b };
            if (standards.TryGetValue(b.Breed, out var curve) && b.AgeInDays > 0)
            {
                var std = PoultryMath.Standard(curve, b.AgeInDays);
                if (std != null)
                {
                    row.StdWeightKg = std.Value.WeightGm > 0 ? Math.Round(std.Value.WeightGm / 1000m, 3) : null;
                    row.StdFcr = std.Value.Fcr > 0 ? std.Value.Fcr : null;
                }
            }
            return row;
        }).ToList();

        var meat = active.Where(b => MeatTypes.Contains(b.BatchType, StringComparer.OrdinalIgnoreCase)).ToList();
        vm.MeatFlocks = meat.Count;
        var fcrs = meat.Where(b => b.FCR > 0).Select(b => b.FCR).ToList();
        var epefs = meat.Where(b => b.EPEF > 0).Select(b => b.EPEF).ToList();
        vm.AvgFcr = fcrs.Count > 0 ? Math.Round(fcrs.Average(), 2) : 0;
        vm.AvgEpef = epefs.Count > 0 ? Math.Round(epefs.Average(), 0) : 0;

        // ---- 14-day farm-wide mortality & feed trend (from daily flock records) ----
        var from = today.AddDays(-(trendDays - 1));
        var daily = (await conn.QueryAsync<DashTrendPoint>(
            @"SELECT RecordDate AS Day, SUM(MortalityCount) AS Mortality,
                     CAST(SUM(FeedConsumptionKg) AS DECIMAL(14,2)) AS FeedKg, SUM(BirdCount) AS Birds
              FROM DailyFarmRecords WHERE RecordDate BETWEEN @from AND @today
              GROUP BY RecordDate", new { from, today })).ToDictionary(p => p.Day.Date);
        vm.Trend = Enumerable.Range(0, trendDays)
            .Select(i => from.AddDays(i))
            .Select(d => daily.TryGetValue(d, out var p) ? p : new DashTrendPoint { Day = d })
            .ToList();
        vm.MortalityToday = vm.Trend.LastOrDefault()?.Mortality ?? 0;
        vm.MortalityYesterday = vm.Trend.Count > 1 ? vm.Trend[^2].Mortality : 0;

        // ---- Egg production: latest recorded day ----
        var egg = (await conn.QueryAsync<(DateTime RecordDate, int Eggs, int Hens)>(
            @"SELECT TOP 1 RecordDate, SUM(TotalEggs) AS Eggs, SUM(HenCount) AS Hens
              FROM EggProductionRecords WHERE RecordDate <= @today
              GROUP BY RecordDate ORDER BY RecordDate DESC", new { today })).ToList();
        if (egg.Count > 0)
        {
            vm.EggsDate = egg[0].RecordDate;
            vm.EggsToday = egg[0].Eggs;
            vm.HenDayPct = egg[0].Hens > 0 ? Math.Round((decimal)egg[0].Eggs / egg[0].Hens * 100, 1) : 0;
        }

        // ---- Live climate: latest reading per active device ----
        var (warn, crit, _) = await sensors.ThresholdsAsync();
        vm.ThiWarning = warn; vm.ThiCritical = crit;
        vm.Climate = (await sensors.GetLatestPerDeviceAsync()).Select(r => new DashClimateTile
        {
            FarmId = r.FarmId, FarmName = r.FarmName ?? "", ShedNo = r.ShedNo, DeviceCode = r.DeviceCode ?? "",
            ReadingTime = r.ReadingTime, TemperatureC = r.TemperatureC, HumidityPercent = r.HumidityPercent,
            AmmoniaPpm = r.AmmoniaPpm, Thi = r.Thi,
            Band = r.Thi.HasValue ? PoultryMath.HeatBand(r.Thi.Value, warn, crit) : "—"
        }).ToList();

        // ---- Next 7 days: vaccinations, harvests, deliveries, tasks, certificate expiries ----
        var events = await calendar.GetEventsAsync(today, today.AddDays(7));
        vm.Upcoming = events
            .Where(e => vm.ShowFinance || e.Kind != "order")
            .Select(e => new DashUpcomingItem
            {
                Date = e.Date, Kind = e.KindLabel, Title = e.Title,
                Sub = string.Join(" · ", new[] { e.Detail, e.FarmName }.Where(s => !string.IsNullOrEmpty(s))),
                Icon = e.Kind switch
                {
                    "vax" => "bi-shield-plus", "harvest" => "bi-flag", "order" => "bi-truck",
                    "task" => "bi-check2-square", "cert" => "bi-patch-check", _ => "bi-calendar-event"
                },
                Tone = e.Kind switch { "vax" => "blue", "harvest" => "green", "order" => "violet", "task" => "amber", _ => "slate" },
                Url = "/" + e.Controller + "/" + e.Action + (e.RouteId.HasValue ? "/" + e.RouteId : "")
            })
            .ToList();
    }
}
