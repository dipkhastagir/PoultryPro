using System.Globalization;
using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>Daily attendance sheet (upsert) and monthly attendance matrix. Friday is treated as the weekly holiday.</summary>
public interface IAttendanceService
{
    Task<AttendanceSheetVm> GetSheetAsync(DateTime date);
    Task<int> SaveSheetAsync(DateTime date, IEnumerable<AttendanceSaveRow> rows);
    Task<AttendanceMonthVm> GetMonthAsync(int year, int month);
    Task<IEnumerable<dynamic>> GetExportRowsAsync(int year, int month);
}

public class AttendanceService(IDbConnectionFactory db) : IAttendanceService
{
    public async Task<AttendanceSheetVm> GetSheetAsync(DateTime date)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<AttendanceSheetRow>(
            @"SELECT e.Id AS EmployeeId, e.EmployeeCode, e.FullName, e.Role, e.FarmId, f.Name AS FarmName, e.Status AS EmpStatus,
                     a.Id AS AttendanceId, a.Status, a.CheckIn, a.CheckOut, a.Notes
              FROM Employees e
              LEFT JOIN Farms f ON f.Id = e.FarmId
              LEFT JOIN Attendance a ON a.EmployeeId = e.Id AND a.AttendanceDate = @date
              WHERE e.IsActive = 1 AND e.JoinDate <= @date
              ORDER BY CASE WHEN f.Name IS NULL THEN 1 ELSE 0 END, f.Name, e.FullName", new { date = date.Date });
        return new AttendanceSheetVm { Date = date.Date, Rows = rows.ToList() };
    }

    public async Task<int> SaveSheetAsync(DateTime date, IEnumerable<AttendanceSaveRow> rows)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var saved = 0;
        foreach (var r in rows)
        {
            if (r.EmployeeId <= 0) continue;
            if (string.IsNullOrWhiteSpace(r.Status))
            {
                // blank status clears an existing mark
                saved += await conn.ExecuteAsync("DELETE FROM Attendance WHERE EmployeeId=@e AND AttendanceDate=@d",
                    new { e = r.EmployeeId, d = date.Date }, tx) > 0 ? 1 : 0;
                continue;
            }
            var status = AttendanceRecord.Statuses.Contains(r.Status) ? r.Status : "Present";
            var worked = status is "Present" or "Late" or "Half Day";
            var p = new
            {
                e = r.EmployeeId,
                d = date.Date,
                s = status,
                ci = worked ? ParseTime(r.CheckIn) : null,
                co = worked ? ParseTime(r.CheckOut) : null,
                n = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim()[..Math.Min(200, r.Notes.Trim().Length)]
            };
            await conn.ExecuteAsync(
                @"IF EXISTS (SELECT 1 FROM Attendance WHERE EmployeeId=@e AND AttendanceDate=@d)
                      UPDATE Attendance SET Status=@s, CheckIn=@ci, CheckOut=@co, Notes=@n WHERE EmployeeId=@e AND AttendanceDate=@d
                  ELSE
                      INSERT INTO Attendance(EmployeeId, AttendanceDate, Status, CheckIn, CheckOut, Notes) VALUES(@e, @d, @s, @ci, @co, @n)",
                p, tx);
            saved++;
        }
        tx.Commit();
        return saved;
    }

    private static TimeSpan? ParseTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string[] formats = { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" };
        return TimeSpan.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture, out var t) && t < TimeSpan.FromDays(1) ? t : null;
    }

    public async Task<AttendanceMonthVm> GetMonthAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var days = DateTime.DaysInMonth(year, month);
        var end = start.AddDays(days - 1);
        using var conn = db.Create();
        var employees = (await conn.QueryAsync<Employee>(
            @"SELECT e.*, f.Name AS FarmName FROM Employees e LEFT JOIN Farms f ON f.Id=e.FarmId
              WHERE (e.IsActive = 1 OR EXISTS (SELECT 1 FROM Attendance a WHERE a.EmployeeId=e.Id AND a.AttendanceDate BETWEEN @start AND @end))
                AND e.JoinDate <= @end
              ORDER BY CASE WHEN f.Name IS NULL THEN 1 ELSE 0 END, f.Name, e.FullName", new { start, end })).ToList();
        var att = (await conn.QueryAsync<AttendanceRecord>(
            "SELECT * FROM Attendance WHERE AttendanceDate BETWEEN @start AND @end", new { start, end })).ToList();
        var byEmp = att.ToLookup(a => a.EmployeeId);

        var today = DateTime.Today;
        var workingDays = Enumerable.Range(1, days).Count(d => start.AddDays(d - 1).DayOfWeek != DayOfWeek.Friday);
        var vm = new AttendanceMonthVm
        {
            Year = year, Month = month, DaysInMonth = days, WorkingDays = workingDays,
            ElapsedWorkingDays = Enumerable.Range(1, days).Select(d => start.AddDays(d - 1))
                .Count(d => d <= today && d.DayOfWeek != DayOfWeek.Friday)
        };

        foreach (var e in employees)
        {
            var row = new AttendanceMatrixRow
            {
                EmployeeId = e.Id, EmployeeCode = e.EmployeeCode, FullName = e.FullName, Role = e.Role, FarmName = e.FarmName
            };
            foreach (var a in byEmp[e.Id]) row.Days[a.AttendanceDate.Day] = a.Status;
            row.Present = row.Days.Values.Count(s => s == "Present");
            row.Late = row.Days.Values.Count(s => s == "Late");
            row.HalfDay = row.Days.Values.Count(s => s == "Half Day");
            row.Leave = row.Days.Values.Count(s => s == "Leave");
            row.Absent = row.Days.Values.Count(s => s == "Absent");
            var expected = Enumerable.Range(1, days).Select(d => start.AddDays(d - 1))
                .Count(d => d <= today && d >= e.JoinDate.Date && d.DayOfWeek != DayOfWeek.Friday);
            row.Unmarked = Math.Max(0, expected - row.Days.Count(kv => start.AddDays(kv.Key - 1).DayOfWeek != DayOfWeek.Friday));
            row.AttendancePct = expected == 0 ? 0 : Math.Min(100m, row.PresentDays / expected * 100m);
            vm.Rows.Add(row);
        }

        for (var d = 1; d <= days; d++)
        {
            var date = start.AddDays(d - 1);
            var marks = vm.Rows.Where(r => r.Days.ContainsKey(d)).ToList();
            vm.DailyPresentPct.Add(marks.Count == 0 ? 0
                : Math.Round(marks.Sum(r => AttendanceRecord.PresenceWeight(r.Days[d])) / marks.Count * 100m, 1));
        }
        return vm;
    }

    public async Task<IEnumerable<dynamic>> GetExportRowsAsync(int year, int month)
    {
        var vm = await GetMonthAsync(year, month);
        var start = new DateTime(year, month, 1);
        var list = new List<dynamic>();
        foreach (var r in vm.Rows)
        {
            IDictionary<string, object> row = new System.Dynamic.ExpandoObject();
            row["Code"] = r.EmployeeCode ?? "";
            row["Employee"] = r.FullName;
            row["Role"] = r.Role;
            row["Farm"] = r.FarmName ?? "All farms";
            for (var d = 1; d <= vm.DaysInMonth; d++)
            {
                var date = start.AddDays(d - 1);
                row[date.ToString("dd ddd", CultureInfo.InvariantCulture)] =
                    r.Days.TryGetValue(d, out var s) ? AttendanceRecord.Code(s) : (date.DayOfWeek == DayOfWeek.Friday ? "W" : "");
            }
            row["Present"] = r.Present;
            row["Late"] = r.Late;
            row["HalfDay"] = r.HalfDay;
            row["Leave"] = r.Leave;
            row["Absent"] = r.Absent;
            row["PresentDays"] = r.PresentDays;
            row["AttendancePct"] = Math.Round(r.AttendancePct, 1);
            list.Add(row);
        }
        return list;
    }
}
