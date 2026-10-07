using Dapper;
using PoultryPro.Models.Entities;

/// <summary>
/// Aggregates dated events from several modules into one farm calendar:
/// vaccinations due (not completed), task due dates, expected harvests of active batches,
/// sales-order deliveries (not delivered / cancelled) and certification expiries.
/// </summary>
public interface ICalendarService
{
    Task<List<FarmCalendarEvent>> GetEventsAsync(DateTime from, DateTime to);
    Task<int> CountOverdueVaccinationsAsync();
}

public class CalendarService(IDbConnectionFactory db) : ICalendarService
{
    public async Task<List<FarmCalendarEvent>> GetEventsAsync(DateTime from, DateTime to)
    {
        from = from.Date; to = to.Date;
        var today = DateTime.Today;
        using var conn = db.Create();
        var events = new List<FarmCalendarEvent>();

        var vax = await conn.QueryAsync<(int Id, string VaccineName, DateTime DueDate, string Status, int BatchId, string BatchCode, string FarmName)>(
            @"SELECT v.Id, v.VaccineName, v.DueDate, v.Status, b.Id, b.BatchCode, f.Name
              FROM VaccinationRecords v JOIN Batches b ON b.Id = v.BatchId JOIN Farms f ON f.Id = b.FarmId
              WHERE v.CompletedDate IS NULL AND v.Status NOT IN ('Completed','Done','Cancelled','Skipped')
                AND v.DueDate BETWEEN @from AND @to", new { from, to });
        events.AddRange(vax.Select(v => new FarmCalendarEvent
        {
            Date = v.DueDate, Kind = "vax", Title = $"{v.VaccineName} · {v.BatchCode}",
            Detail = $"Vaccination due for batch {v.BatchCode}", FarmName = v.FarmName,
            Controller = "Health", Action = "Index", IsOverdue = v.DueDate.Date < today
        }));

        var tasks = await conn.QueryAsync<(int Id, string Title, DateTime DueDate, string Priority, string Status, string? FarmName, string? Assignee)>(
            @"SELECT t.Id, t.Title, t.DueDate, t.Priority, t.Status, f.Name, e.FullName
              FROM FarmTasks t LEFT JOIN Farms f ON f.Id = t.FarmId LEFT JOIN Employees e ON e.Id = t.AssignedToEmployeeId
              WHERE t.DueDate BETWEEN @from AND @to AND t.Status <> 'Done'", new { from, to });
        events.AddRange(tasks.Select(t => new FarmCalendarEvent
        {
            Date = t.DueDate, Kind = "task", Title = t.Title,
            Detail = $"{t.Priority} priority · {t.Status}" + (t.Assignee != null ? $" · {t.Assignee}" : ""),
            FarmName = t.FarmName, Controller = "Tasks", Action = "Edit", RouteId = t.Id, IsOverdue = t.DueDate.Date < today
        }));

        var harvest = await conn.QueryAsync<(int Id, string BatchCode, string Breed, DateTime ExpectedHarvestDate, int CurrentCount, string FarmName)>(
            @"SELECT b.Id, b.BatchCode, b.Breed, b.ExpectedHarvestDate, b.CurrentCount, f.Name
              FROM Batches b JOIN Farms f ON f.Id = b.FarmId
              WHERE b.IsActive = 1 AND b.ExpectedHarvestDate BETWEEN @from AND @to", new { from, to });
        events.AddRange(harvest.Select(h => new FarmCalendarEvent
        {
            Date = h.ExpectedHarvestDate, Kind = "harvest", Title = $"Harvest {h.BatchCode}",
            Detail = $"{h.Breed} · {h.CurrentCount:N0} birds", FarmName = h.FarmName,
            Controller = "Batch", Action = "Detail", RouteId = h.Id, IsOverdue = h.ExpectedHarvestDate.Date < today
        }));

        var orders = await conn.QueryAsync<CalendarOrderRow>(
            @"SELECT o.Id, o.OrderCode, o.Product, o.Quantity, o.Unit, o.DeliveryDate, o.Status, c.Name AS Customer, f.Name AS FarmName
              FROM Orders o JOIN Customers c ON c.Id = o.CustomerId JOIN Farms f ON f.Id = o.FarmId
              WHERE o.Status NOT IN ('Delivered','Cancelled') AND o.DeliveryDate BETWEEN @from AND @to", new { from, to });
        events.AddRange(orders.Select(o => new FarmCalendarEvent
        {
            Date = o.DeliveryDate, Kind = "order", Title = $"{o.OrderCode} · {o.Customer}",
            Detail = $"{o.Product} {o.Quantity:0.##} {o.Unit} · {o.Status}", FarmName = o.FarmName,
            Controller = "Market", Action = "Detail", RouteId = o.Id, IsOverdue = o.DeliveryDate.Date < today
        }));

        var certs = await conn.QueryAsync<(int Id, string CertName, DateTime ExpiryDate, int FarmId, string FarmName)>(
            @"SELECT c.Id, c.CertName, c.ExpiryDate, c.FarmId, f.Name
              FROM Certifications c JOIN Farms f ON f.Id = c.FarmId
              WHERE c.ExpiryDate BETWEEN @from AND @to", new { from, to });
        events.AddRange(certs.Select(c => new FarmCalendarEvent
        {
            Date = c.ExpiryDate, Kind = "cert", Title = $"{c.CertName} expires",
            Detail = $"Certification expiry · {c.FarmName}", FarmName = c.FarmName,
            Controller = "Farm", Action = "Detail", RouteId = c.FarmId, IsOverdue = c.ExpiryDate.Date < today
        }));

        var order = new Dictionary<string, int> { ["vax"] = 0, ["harvest"] = 1, ["order"] = 2, ["task"] = 3, ["cert"] = 4 };
        return events.OrderBy(e => e.Date).ThenBy(e => order.GetValueOrDefault(e.Kind, 9)).ThenBy(e => e.Title).ToList();
    }

    public async Task<int> CountOverdueVaccinationsAsync()
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM VaccinationRecords v JOIN Batches b ON b.Id = v.BatchId
              WHERE v.CompletedDate IS NULL AND b.IsActive = 1 AND v.Status NOT IN ('Completed','Done','Cancelled','Skipped')
                AND v.DueDate < CAST(GETDATE() AS DATE)");
    }
}

/// <summary>Projection used by CalendarService for sales-order deliveries.</summary>
public class CalendarOrderRow
{
    public int Id { get; set; }
    public string OrderCode { get; set; } = "";
    public string Product { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public DateTime DeliveryDate { get; set; }
    public string Status { get; set; } = "";
    public string Customer { get; set; } = "";
    public string FarmName { get; set; } = "";
}
