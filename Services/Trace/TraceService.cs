using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>
/// Farm-to-fork traceability: internal lifecycle timeline of a flock and the public consumer page
/// (looked up by batch code) with a tamper-evident verification hash.
/// </summary>
public interface ITraceService
{
    Task<List<TraceBatchRow>> GetBatchesAsync(string? q = null, string? status = null);
    Task<TraceTimelineVm?> GetTimelineAsync(int batchId);
    Task<TracePublicVm> GetPublicAsync(string code);
    /// <summary>Short (16 hex) SHA-256 fingerprint of the batch master payload — kept for existing callers / QR labels.</summary>
    string ComputeHash(Batch b);
    /// <summary>
    /// Freezes the current chain head on the batch. Returns false when the batch does not exist or when
    /// <paramref name="expectedHead"/> is given and no longer matches (the record changed while being reviewed).
    /// </summary>
    Task<bool> SealAsync(int batchId, string sealedBy, string? expectedHead = null);
}

public class TraceService(IDbConnectionFactory db, ISettingsService settings) : ITraceService
{
    /// <summary>Withdrawal window (days before harvest) inspected for medicine use.</summary>
    public const int WithdrawalDays = 7;

    private class VaxRow { public string VaccineName { get; set; } = ""; public DateTime DueDate { get; set; } public DateTime? CompletedDate { get; set; } public string Status { get; set; } = ""; public string? Route { get; set; } public decimal? DoseMl { get; set; } public string? AdministeredBy { get; set; } }
    private class DiseaseRow { public string DiseaseName { get; set; } = ""; public DateTime ReportedDate { get; set; } public int AffectedCount { get; set; } public int DeathCount { get; set; } public string Treatment { get; set; } = ""; public string Status { get; set; } = ""; public DateTime? ResolvedDate { get; set; } public string? DiagnosedBy { get; set; } }
    private class WeightRow { public DateTime RecordDate { get; set; } public int AgeInDays { get; set; } public decimal AvgWeightKg { get; set; } public int SampleSize { get; set; } public decimal? UniformityPct { get; set; } }
    private class AuditRow { public int Id { get; set; } public DateTime AuditDate { get; set; } public string AuditorName { get; set; } = ""; public decimal ScorePct { get; set; } public string Grade { get; set; } = ""; }
    private class VisitRow { public DateTime VisitDate { get; set; } public string FieldOfficerName { get; set; } = ""; public string VisitPurpose { get; set; } = ""; public int DailyRecordCount { get; set; } public bool HasGps { get; set; } }
    private class MedRow { public DateTime RecordDate { get; set; } public string MedicineUsed { get; set; } = ""; public string? MedicineDosage { get; set; } }
    private class OrderRow { public int Id { get; set; } public string OrderCode { get; set; } = ""; public string CustomerName { get; set; } = ""; public string Product { get; set; } = ""; public decimal Quantity { get; set; } public string Unit { get; set; } = ""; public DateTime OrderDate { get; set; } public DateTime DeliveryDate { get; set; } public string Status { get; set; } = ""; public int? BirdCount { get; set; } }

    public async Task<List<TraceBatchRow>> GetBatchesAsync(string? q = null, string? status = null)
    {
        using var conn = db.Create();
        var sql = @"SELECT b.Id, b.BatchCode, b.Breed, b.BatchType, b.FarmName, b.FarmLocation, b.PlacementDate, b.ClosedDate,
                           b.AgeInDays, b.InitialCount, b.CurrentCount, b.IsActive,
                           (SELECT COUNT(*) FROM VaccinationRecords v WHERE v.BatchId = b.Id AND v.CompletedDate IS NOT NULL) AS VaxDone,
                           (SELECT COUNT(*) FROM VaccinationRecords v WHERE v.BatchId = b.Id) AS VaxTotal,
                           (SELECT COUNT(*) FROM Orders o WHERE o.BatchId = b.Id) AS OrderCount
                    FROM vw_BatchDetails b WHERE 1 = 1";
        if (!string.IsNullOrWhiteSpace(q)) sql += " AND (b.BatchCode LIKE @like OR b.Breed LIKE @like OR b.FarmName LIKE @like)";
        if (status == "active") sql += " AND b.IsActive = 1";
        if (status == "closed") sql += " AND b.IsActive = 0";
        sql += " ORDER BY b.IsActive DESC, b.PlacementDate DESC";
        return (await conn.QueryAsync<TraceBatchRow>(sql, new { like = $"%{q?.Trim()}%" })).ToList();
    }

    /// <summary>Canonical batch master payload (invariant culture) that seeds the hash chain.</summary>
    private static string BatchPayload(Batch b)
    {
        var inv = CultureInfo.InvariantCulture;
        return string.Join("|",
            b.BatchCode, b.FarmId.ToString(inv), b.Breed,
            b.PlacementDate.ToString("yyyy-MM-dd", inv), b.ClosedDate?.ToString("yyyy-MM-dd", inv) ?? "open",
            b.InitialCount.ToString(inv), b.HarvestedCount.ToString(inv),
            b.HatcheryName ?? "");
    }

    private static string Sha256Hex(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public string ComputeHash(Batch b) => Sha256Hex(BatchPayload(b))[..16];

    /// <summary>
    /// Orders the events chronologically (day, lifecycle order, timestamp, then kind/title/detail ordinal as a stable
    /// tie-break) and links them: genesis = SHA-256(batch payload);
    /// hash_i = SHA-256(hash_{i-1} | date ISO-8601 | kind | title | detail). Returns (ordered events, genesis, head).
    /// </summary>
    public static (List<TraceEvent> Events, string Genesis, string Head) BuildChain(Batch b, IEnumerable<TraceEvent> events)
    {
        var inv = CultureInfo.InvariantCulture;
        var ordered = events
            .OrderBy(e => e.Date.Date).ThenBy(e => e.Order).ThenBy(e => e.Date)
            .ThenBy(e => e.Kind, StringComparer.Ordinal)
            .ThenBy(e => e.ChainTitle ?? e.Title, StringComparer.Ordinal)
            .ThenBy(e => e.ChainDetail ?? e.Body ?? "", StringComparer.Ordinal)
            .ToList();
        var genesis = Sha256Hex(BatchPayload(b));
        var prev = genesis;
        foreach (var e in ordered)
        {
            e.Hash = Sha256Hex(string.Join("|",
                prev, e.Date.ToString("yyyy-MM-dd'T'HH:mm:ss", inv), e.Kind,
                e.ChainTitle ?? e.Title, e.ChainDetail ?? e.Body ?? ""));
            prev = e.Hash;
        }
        return (ordered, genesis, prev);
    }

    private static TraceSealInfo SealInfo(Batch b, string head) => new()
    {
        ChainHead = head, SealHash = b.TraceSealHash, SealedAt = b.TraceSealedAt, SealedBy = b.TraceSealedBy
    };

    public async Task<bool> SealAsync(int batchId, string sealedBy, string? expectedHead = null)
    {
        var tl = await GetTimelineAsync(batchId);
        if (tl == null) return false;
        if (!string.IsNullOrEmpty(expectedHead) && !string.Equals(expectedHead, tl.Seal.ChainHead, StringComparison.OrdinalIgnoreCase))
            return false;
        using var conn = db.Create();
        var n = await conn.ExecuteAsync(
            "UPDATE Batches SET TraceSealHash = @head, TraceSealedAt = GETDATE(), TraceSealedBy = @by WHERE Id = @batchId",
            new { head = tl.Seal.ChainHead, by = sealedBy.Length > 100 ? sealedBy[..100] : sealedBy, batchId });
        return n > 0;
    }

    /// <summary>Temporarily switches the current culture so event text (and therefore the hash chain) is culture-independent.</summary>
    private readonly struct CultureScope : IDisposable
    {
        private readonly CultureInfo _prev;
        public CultureScope(CultureInfo c) { _prev = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = c; }
        public void Dispose() => CultureInfo.CurrentCulture = _prev;
    }

    public async Task<TraceTimelineVm?> GetTimelineAsync(int batchId)
    {
        using var conn = db.Create();
        var b = await conn.QuerySingleOrDefaultAsync<Batch>("SELECT * FROM vw_BatchPerformance WHERE Id = @batchId", new { batchId });
        if (b == null) return null;
        var farm = await conn.QuerySingleOrDefaultAsync<Farm>("SELECT * FROM Farms WHERE Id = @id", new { id = b.FarmId });
        var end = b.ClosedDate ?? DateTime.Today;

        var vax = (await conn.QueryAsync<VaxRow>("SELECT * FROM VaccinationRecords WHERE BatchId = @batchId ORDER BY DueDate", new { batchId })).ToList();
        var dis = (await conn.QueryAsync<DiseaseRow>("SELECT * FROM DiseaseCases WHERE BatchId = @batchId ORDER BY ReportedDate", new { batchId })).ToList();
        var wts = (await conn.QueryAsync<WeightRow>("SELECT * FROM WeightRecords WHERE BatchId = @batchId ORDER BY RecordDate", new { batchId })).ToList();
        var audits = (await conn.QueryAsync<AuditRow>(
            "SELECT Id, AuditDate, AuditorName, ScorePct, Grade FROM BiosecurityAudits WHERE FarmId = @farmId AND AuditDate BETWEEN @from AND @to ORDER BY AuditDate",
            new { farmId = b.FarmId, from = b.PlacementDate, to = end })).ToList();
        var visits = (await conn.QueryAsync<VisitRow>(
            @"SELECT VisitDate, FieldOfficerName, VisitPurpose, DailyRecordCount,
                     CAST(CASE WHEN CheckInLatitude IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS HasGps
              FROM vw_FarmVisitDetails WHERE BatchId = @batchId ORDER BY VisitDate", new { batchId })).ToList();
        var meds = (await conn.QueryAsync<MedRow>(
            "SELECT RecordDate, MedicineUsed, MedicineDosage FROM DailyFarmRecords WHERE BatchId = @batchId AND MedicineUsed IS NOT NULL AND MedicineUsed <> '' ORDER BY RecordDate",
            new { batchId })).ToList();
        var orders = (await conn.QueryAsync<OrderRow>(
            @"SELECT o.Id, o.OrderCode, c.Name AS CustomerName, o.Product, o.Quantity, o.Unit, o.OrderDate, o.DeliveryDate, o.Status, o.BirdCount
              FROM Orders o INNER JOIN Customers c ON c.Id = o.CustomerId WHERE o.BatchId = @batchId ORDER BY o.OrderDate", new { batchId })).ToList();

        // No awaits below: event text is composed under the invariant culture so the hash chain is reproducible.
        using var cultureScope = new CultureScope(CultureInfo.InvariantCulture);
        var ev = new List<TraceEvent>
        {
            new()
            {
                Date = b.PlacementDate, Kind = "Placement", Icon = "bi-box-arrow-in-down", Color = "#0d9e75", Order = 0,
                Title = $"Placement — {b.InitialCount:N0} day-old {b.Breed} chicks",
                Body = $"Hatchery: {b.HatcheryName ?? "not recorded"} · Farm: {b.FarmName}{(string.IsNullOrEmpty(b.ShedNo) ? "" : $", shed {b.ShedNo}")}"
                       + (b.ChickCostPerBird > 0 ? $" · Chick cost ৳{b.ChickCostPerBird:N2}/bird" : ""),
                Badge = b.BatchType
            }
        };

        foreach (var v in vax)
        {
            var done = v.CompletedDate.HasValue;
            var overdue = !done && v.DueDate.Date < DateTime.Today;
            var details = string.Join(" · ", new[] { v.Route, v.DoseMl.HasValue ? $"{v.DoseMl:0.##} ml" : null, v.AdministeredBy }.Where(s => !string.IsNullOrEmpty(s)));
            ev.Add(new TraceEvent
            {
                Date = v.CompletedDate ?? v.DueDate, Kind = "Vaccination", Order = 2,
                Icon = done ? "bi-shield-check" : overdue ? "bi-shield-exclamation" : "bi-shield",
                Color = done ? "#3b82f6" : overdue ? "#ef4444" : "#94a3b8",
                Title = $"{v.VaccineName} — {(done ? "administered" : overdue ? "OVERDUE" : "scheduled")}",
                Body = (done ? $"Due {v.DueDate:dd MMM}, done {v.CompletedDate:dd MMM yyyy}" : $"Due {v.DueDate:dd MMM yyyy}")
                       + (details.Length > 0 ? " · " + details : ""),
                Badge = done ? "Done" : overdue ? "Overdue" : "Planned",
                // "overdue"/"scheduled" depends on today's date — hash a date-independent form instead
                ChainTitle = done ? null : $"{v.VaccineName} — not administered",
                ChainDetail = done ? null : $"Due {v.DueDate:yyyy-MM-dd}" + (details.Length > 0 ? " · " + details : "")
            });
        }

        foreach (var d in dis)
        {
            ev.Add(new TraceEvent
            {
                Date = d.ReportedDate, Kind = "Disease", Icon = "bi-virus", Color = "#ef4444", Order = 3,
                Title = $"Disease case — {d.DiseaseName}",
                Body = $"{d.AffectedCount:N0} birds affected, {d.DeathCount:N0} deaths · Treatment: {(string.IsNullOrWhiteSpace(d.Treatment) ? "—" : d.Treatment)}"
                       + (string.IsNullOrEmpty(d.DiagnosedBy) ? "" : $" · Diagnosed by {d.DiagnosedBy}"),
                Badge = d.Status
            });
            if (d.ResolvedDate.HasValue)
                ev.Add(new TraceEvent
                {
                    Date = d.ResolvedDate.Value, Kind = "Disease", Icon = "bi-check2-circle", Color = "#0d9e75", Order = 3,
                    Title = $"{d.DiseaseName} resolved",
                    Body = $"Resolved after {(d.ResolvedDate.Value - d.ReportedDate).Days} days.", Badge = "Resolved"
                });
        }

        foreach (var w in wts)
            ev.Add(new TraceEvent
            {
                Date = w.RecordDate, Kind = "Weight", Icon = "bi-speedometer", Color = "#8b5cf6", Order = 4,
                Title = $"Weight sample — {w.AvgWeightKg * 1000:N0} g at day {w.AgeInDays}",
                Body = $"Sample of {w.SampleSize} birds" + (w.UniformityPct.HasValue ? $" · uniformity {w.UniformityPct:0.#}%" : "")
            });

        foreach (var a in audits)
            ev.Add(new TraceEvent
            {
                Date = a.AuditDate, Kind = "Biosecurity", Icon = "bi-shield-lock", Color = "#14b8a6", Order = 5,
                Title = $"Biosecurity audit — grade {a.Grade} ({a.ScorePct:0.#}%)",
                Body = $"Farm-level audit by {a.AuditorName}", Badge = a.Grade
            });

        // Field visits summarised per ISO week to keep the timeline readable
        foreach (var g in visits.GroupBy(v => v.VisitDate.Date.AddDays(-(((int)v.VisitDate.DayOfWeek + 6) % 7))))
        {
            var list = g.ToList();
            var officers = string.Join(", ", list.Select(v => v.FieldOfficerName).Distinct());
            var purposes = string.Join(", ", list.GroupBy(v => v.VisitPurpose).Select(p => p.Count() > 1 ? $"{p.Key} ×{p.Count()}" : p.Key));
            ev.Add(new TraceEvent
            {
                Date = list.Max(v => v.VisitDate), Kind = "Visits", Icon = "bi-geo-alt", Color = "#f59e0b", Order = 6,
                Title = list.Count == 1 ? "Field visit" : $"{list.Count} field visits (week of {g.Key:dd MMM})",
                Body = $"{officers} · {purposes} · {list.Sum(v => v.DailyRecordCount)} daily records · {list.Count(v => v.HasGps)}/{list.Count} GPS-verified"
            });
        }

        foreach (var m in meds)
            ev.Add(new TraceEvent
            {
                Date = m.RecordDate, Kind = "Medicine", Icon = "bi-capsule", Color = "#ec4899", Order = 7,
                Title = $"Medication — {m.MedicineUsed}",
                Body = string.IsNullOrEmpty(m.MedicineDosage) ? "Recorded in daily flock log" : $"Dosage: {m.MedicineDosage}"
            });

        if (b.ClosedDate.HasValue)
            ev.Add(new TraceEvent
            {
                Date = b.ClosedDate.Value, Kind = "Harvest", Icon = "bi-flag-fill", Color = "#0f172a", Order = 9,
                Title = $"Harvest & closure at day {b.AgeInDays}",
                Body = $"{b.HarvestedCount:N0} birds harvested" + (b.HarvestWeightKg > 0 ? $" · {b.HarvestWeightKg:N0} kg live weight" : "")
                       + $" · livability {b.LivabilityPct:0.#}% · FCR {b.FCR:0.00} · EPEF {b.EPEF:0}",
                Badge = "Closed"
            });
        else if (b.ExpectedHarvestDate.HasValue)
            ev.Add(new TraceEvent
            {
                Date = b.ExpectedHarvestDate.Value, Kind = "Harvest", Icon = "bi-flag", Color = "#94a3b8", Order = 9,
                Title = "Expected harvest", Body = $"Planned harvest date · currently day {b.AgeInDays}, {b.AverageWeightKg * 1000:N0} g average",
                Badge = "Planned", ChainDetail = "Planned harvest date"
            });

        foreach (var o in orders)
            ev.Add(new TraceEvent
            {
                Date = o.OrderDate, Kind = "Sale", Icon = "bi-cart-check", Color = "#3b82f6", Order = 10,
                Title = $"Sales order {o.OrderCode} — {o.CustomerName}",
                Body = $"{o.Quantity:N0} {o.Unit} {o.Product}" + (o.BirdCount.HasValue ? $" ({o.BirdCount:N0} birds)" : "") + $" · delivery {o.DeliveryDate:dd MMM yyyy}",
                Badge = o.Status
            });

        var chain = BuildChain(b, ev);
        return new TraceTimelineVm
        {
            Batch = b, Farm = farm,
            Events = chain.Events,
            VerificationHash = ComputeHash(b),
            GenesisHash = chain.Genesis,
            Seal = SealInfo(b, chain.Head),
            VaccinationsDone = vax.Count(v => v.CompletedDate.HasValue),
            VaccinationsOverdue = vax.Count(v => !v.CompletedDate.HasValue && v.DueDate.Date < DateTime.Today),
            DiseaseCases = dis.Count, MedicineDays = meds.Select(m => m.RecordDate.Date).Distinct().Count(),
            Visits = visits.Count, SalesOrders = orders.Count
        };
    }

    public async Task<TracePublicVm> GetPublicAsync(string code)
    {
        var vm = new TracePublicVm { Code = code ?? "" };
        vm.CompanyName = await settings.GetAsync("Company.Name", "PoultryPro");
        if (string.IsNullOrWhiteSpace(code)) return vm;

        using var conn = db.Create();
        var b = await conn.QuerySingleOrDefaultAsync<Batch>("SELECT * FROM vw_BatchPerformance WHERE BatchCode = @code", new { code = code.Trim() });
        if (b == null) return vm;
        var farm = await conn.QuerySingleOrDefaultAsync<Farm>("SELECT * FROM Farms WHERE Id = @id", new { id = b.FarmId });
        var harvest = b.ClosedDate ?? DateTime.Today;

        vm.Found = true;
        vm.BatchCode = b.BatchCode; vm.Breed = b.Breed; vm.BatchType = b.BatchType;
        vm.FarmName = b.FarmName; vm.FarmLocation = b.FarmLocation; vm.Zone = farm?.Zone; vm.FarmType = farm?.FarmType;
        vm.HatcheryName = b.HatcheryName; vm.PlacementDate = b.PlacementDate;
        vm.IsHarvested = b.ClosedDate.HasValue; vm.HarvestDate = b.ClosedDate;
        vm.AgeAtHarvest = (harvest.Date - b.PlacementDate.Date).Days;
        vm.AverageWeightKg = b.HarvestedCount > 0 && b.HarvestWeightKg > 0 ? Math.Round(b.HarvestWeightKg / b.HarvestedCount, 3) : b.AverageWeightKg;
        vm.LivabilityPct = b.LivabilityPct;

        vm.Vaccinations = (await conn.QueryAsync<TraceVaccineLine>(
            "SELECT VaccineName, CompletedDate, Route FROM VaccinationRecords WHERE BatchId = @id AND CompletedDate IS NOT NULL ORDER BY CompletedDate",
            new { id = b.Id })).ToList();

        var windowStart = harvest.Date.AddDays(-WithdrawalDays);
        vm.LastMedicineDate = await conn.ExecuteScalarAsync<DateTime?>(
            "SELECT MAX(RecordDate) FROM DailyFarmRecords WHERE BatchId = @id AND MedicineUsed IS NOT NULL AND MedicineUsed <> '' AND RecordDate <= @harvest",
            new { id = b.Id, harvest = harvest.Date });
        vm.MedicineInWithdrawalWindow = vm.LastMedicineDate.HasValue && vm.LastMedicineDate.Value.Date >= windowStart;
        vm.WithdrawalStatement = vm.MedicineInWithdrawalWindow
            ? $"Veterinary medicine was recorded on {vm.LastMedicineDate:dd MMM yyyy}, within {WithdrawalDays} days of {(vm.IsHarvested ? "harvest" : "today")}. " +
              (vm.IsHarvested ? "This lot was released under the farm's veterinary withdrawal protocol." : "Birds will not be released for sale until the withdrawal period has elapsed.")
            : vm.LastMedicineDate.HasValue
                ? $"No medicine was used in the {WithdrawalDays} days before {(vm.IsHarvested ? "harvest" : "today")}. Last recorded treatment: {vm.LastMedicineDate:dd MMM yyyy} — the withdrawal period was respected."
                : $"No antibiotic or medicine treatment was recorded for this flock.";

        var audit = (await conn.QueryAsync<(DateTime AuditDate, string Grade, decimal ScorePct)>(
            "SELECT TOP 1 AuditDate, Grade, ScorePct FROM BiosecurityAudits WHERE FarmId = @farmId AND AuditDate <= @harvest ORDER BY AuditDate DESC",
            new { farmId = b.FarmId, harvest = harvest.Date })).ToList();
        if (audit.Count > 0)
        {
            vm.BiosecurityAuditDate = audit[0].AuditDate; vm.BiosecurityGrade = audit[0].Grade; vm.BiosecurityPct = audit[0].ScorePct;
        }

        vm.Certifications = (await conn.QueryAsync<string>(
            "SELECT CertName FROM Certifications WHERE FarmId = @farmId AND ExpiryDate >= @harvest AND IssuedDate <= @harvest ORDER BY CertName",
            new { farmId = b.FarmId, harvest = harvest.Date })).ToList();

        vm.VerificationHash = ComputeHash(b);
        var tl = await GetTimelineAsync(b.Id);
        if (tl != null) { vm.Seal = tl.Seal; vm.ChainLength = tl.Events.Count; }
        return vm;
    }
}
