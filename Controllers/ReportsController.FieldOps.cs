using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

public partial class ReportsController
{
    /// <summary>Field operations report: visit productivity, completion, duration, GPS quality and sync backlog per officer.</summary>
    public async Task<IActionResult> FieldOps(int days = 90)
    {
        days = days is 30 or 60 or 90 or 180 or 365 ? days : 90;
        var db = HttpContext.RequestServices.GetRequiredService<IDbConnectionFactory>();
        using var conn = db.Create();
        var today = DateTime.Today;
        var from = today.AddDays(-(days - 1));
        var from30 = today.AddDays(-29);

        var officers = (await conn.QueryAsync<FieldOpsOfficerRow>(
            @"SELECT fo.Id AS FieldOfficerId, fo.FullName, fo.Zone,
                     (SELECT COUNT(*) FROM FarmVisits v WHERE v.FieldOfficerId = fo.Id AND v.VisitDate >= @from30) AS Visits30,
                     (SELECT COUNT(*) FROM FarmVisits v WHERE v.FieldOfficerId = fo.Id AND v.VisitDate >= @from) AS Visits90,
                     (SELECT COUNT(*) FROM FarmVisits v WHERE v.FieldOfficerId = fo.Id AND v.VisitDate >= @from AND v.VisitStatus = 'Completed') AS Completed90,
                     (SELECT COUNT(*) FROM FarmVisits v WHERE v.FieldOfficerId = fo.Id AND v.VisitDate >= @from AND v.VisitStatus = 'Cancelled') AS Cancelled90,
                     (SELECT AVG(CAST(DATEDIFF(MINUTE, v.CheckInTime, v.CheckOutTime) AS FLOAT)) FROM FarmVisits v
                        WHERE v.FieldOfficerId = fo.Id AND v.VisitDate >= @from AND v.CheckInTime IS NOT NULL AND v.CheckOutTime > v.CheckInTime
                          AND DATEDIFF(MINUTE, v.CheckInTime, v.CheckOutTime) <= 720) AS AvgDurationMin,
                     (SELECT AVG(CAST(v.GpsAccuracyMeters AS FLOAT)) FROM FarmVisits v
                        WHERE v.FieldOfficerId = fo.Id AND v.VisitDate >= @from AND v.GpsAccuracyMeters IS NOT NULL) AS AvgGpsAccuracy,
                     (SELECT COUNT(*) FROM FarmVisits v WHERE v.FieldOfficerId = fo.Id AND v.VisitDate >= @from AND v.CheckInLatitude IS NOT NULL) AS GpsFixes,
                     (SELECT COUNT(DISTINCT v.FarmId) FROM FarmVisits v WHERE v.FieldOfficerId = fo.Id AND v.VisitDate >= @from) AS FarmsCovered,
                     (SELECT COUNT(*) FROM DailyFarmRecords d JOIN FarmVisits v ON v.Id = d.VisitId WHERE v.FieldOfficerId = fo.Id AND d.RecordDate >= @from) AS DailyRecords90,
                     (SELECT COUNT(*) FROM VisitPhotos p JOIN FarmVisits v ON v.Id = p.VisitId WHERE v.FieldOfficerId = fo.Id AND v.VisitDate >= @from) AS Photos90,
                     (SELECT MAX(v.VisitDate) FROM FarmVisits v WHERE v.FieldOfficerId = fo.Id) AS LastVisit,
                     ISNULL(ps.PendingVisits, 0) AS PendingVisits, ISNULL(ps.PendingDailyRecords, 0) AS PendingDailyRecords,
                     ISNULL(ps.PendingPhotos, 0) AS PendingPhotos, ISNULL(ps.PendingSignatures, 0) AS PendingSignatures
              FROM FieldOfficers fo
              LEFT JOIN vw_FieldOfficerPendingSync ps ON ps.FieldOfficerId = fo.Id
              WHERE fo.IsActive = 1
              ORDER BY fo.FullName", new { from, from30 })).ToList();

        // Geofence verification + Visit Trust Score per officer (same rules as the Visit pages)
        var radius = await HttpContext.RequestServices.GetRequiredService<IVisitService>().GetGeofenceRadiusAsync();
        var windowVisits = VisitTrust.Apply(await conn.QueryAsync<FarmVisit>(
            "SELECT * FROM vw_FarmVisitDetails WHERE VisitDate >= @from", new { from }), radius);
        var trustByOfficer = windowVisits.GroupBy(v => v.FieldOfficerId).ToDictionary(g => g.Key, g => VisitTrust.Summarise(g, radius));
        foreach (var o in officers)
            if (trustByOfficer.TryGetValue(o.FieldOfficerId, out var t)) { o.AvgTrustScore = t.AvgScore; o.GeoVerifiedPct = t.GeoVerifiedPct; }
        ViewBag.GeofenceRadius = radius;

        var visitsPerDay = (await conn.QueryAsync<(DateTime Day, int N)>(
            "SELECT VisitDate, COUNT(*) FROM FarmVisits WHERE VisitDate >= @from GROUP BY VisitDate", new { from }))
            .ToDictionary(x => x.Day.Date, x => x.N);
        var recordsPerDay = (await conn.QueryAsync<(DateTime Day, int N)>(
            "SELECT RecordDate, COUNT(*) FROM DailyFarmRecords WHERE RecordDate >= @from GROUP BY RecordDate", new { from }))
            .ToDictionary(x => x.Day.Date, x => x.N);

        var purposes = (await conn.QueryAsync<(string Purpose, int N)>(
            "SELECT VisitPurpose, COUNT(*) FROM FarmVisits WHERE VisitDate >= @from GROUP BY VisitPurpose ORDER BY COUNT(*) DESC",
            new { from })).ToList();

        var gps = (await conn.QueryAsync<decimal>(
            "SELECT GpsAccuracyMeters FROM FarmVisits WHERE VisitDate >= @from AND GpsAccuracyMeters IS NOT NULL", new { from })).ToList();

        var coverage = (await conn.QueryAsync<(string Farm, int Visits, DateTime? Last)>(
            @"SELECT f.Name, COUNT(v.Id), MAX(v.VisitDate)
              FROM Farms f LEFT JOIN FarmVisits v ON v.FarmId = f.Id AND v.VisitDate >= @from
              WHERE f.IsActive = 1 GROUP BY f.Name ORDER BY COUNT(v.Id) DESC, f.Name", new { from })).ToList();

        var durationAll = await conn.ExecuteScalarAsync<double?>(
            @"SELECT AVG(CAST(DATEDIFF(MINUTE, CheckInTime, CheckOutTime) AS FLOAT)) FROM FarmVisits
              WHERE VisitDate >= @from AND CheckInTime IS NOT NULL AND CheckOutTime > CheckInTime AND DATEDIFF(MINUTE, CheckInTime, CheckOutTime) <= 720",
            new { from });

        var vm = new FieldOpsReportVm
        {
            Days = days,
            Officers = officers,
            Purposes = purposes.Select(p => (p.Purpose, p.N)).ToList(),
            GpsBuckets = new List<(string, int)>
            {
                ("≤ 10 m", gps.Count(g => g <= 10)),
                ("10–25 m", gps.Count(g => g > 10 && g <= 25)),
                ("25–50 m", gps.Count(g => g > 25 && g <= 50)),
                ("50–100 m", gps.Count(g => g > 50 && g <= 100)),
                ("> 100 m", gps.Count(g => g > 100))
            },
            FarmCoverage = coverage,
            AvgDurationMin = durationAll,
            AvgGpsAccuracy = gps.Count == 0 ? null : (double)gps.Average()
        };
        for (var d = from; d <= today; d = d.AddDays(1))
            vm.Daily.Add(new FieldOpsDayPoint { Day = d, Visits = visitsPerDay.GetValueOrDefault(d), Records = recordsPerDay.GetValueOrDefault(d) });

        ViewData["Title"] = "Field Operations Report";
        ViewData["Subtitle"] = $"Officer productivity, visit quality and data sync · last {days} days";
        return View(vm);
    }
}
