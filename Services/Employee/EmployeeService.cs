using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

public class EmployeeService(IDbConnectionFactory db) : IEmployeeService
{
    private const string Select = @"SELECT e.*, f.Name AS FarmName FROM Employees e LEFT JOIN Farms f ON e.FarmId = f.Id";

    public async Task<IEnumerable<Employee>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Employee>(Select + " WHERE e.IsActive = 1 ORDER BY e.FullName");
    }

    public async Task<IEnumerable<Employee>> GetDirectoryAsync(bool includeInactive)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Employee>(Select + (includeInactive ? "" : " WHERE e.IsActive = 1")
            + " ORDER BY e.IsActive DESC, e.FullName");
    }

    public async Task<Employee?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Employee>(Select + " WHERE e.Id = @id", new { id });
    }

    public async Task<int> CreateAsync(Employee employee)
    {
        if (string.IsNullOrWhiteSpace(employee.EmployeeCode)) employee.EmployeeCode = await NextCodeAsync();
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Employees(EmployeeCode,FullName,Role,FarmId,Phone,Email,Address,EmergencyContact,Status,MonthlySalary,JoinDate,IsActive)
              VALUES(@EmployeeCode,@FullName,@Role,@FarmId,@Phone,@Email,@Address,@EmergencyContact,@Status,@MonthlySalary,@JoinDate,1);
              DECLARE @newId INT = CAST(SCOPE_IDENTITY() AS INT);
              IF @FarmId IS NOT NULL
                  UPDATE Farms SET EmployeeCount = (SELECT COUNT(*) FROM Employees WHERE FarmId=@FarmId AND IsActive=1) WHERE Id=@FarmId;
              SELECT @newId;", employee);
    }

    public async Task UpdateAsync(Employee employee)
    {
        using var conn = db.Create();
        var oldFarm = await conn.ExecuteScalarAsync<int?>("SELECT FarmId FROM Employees WHERE Id=@Id", employee);
        await conn.ExecuteAsync(
            @"UPDATE Employees SET EmployeeCode=@EmployeeCode, FullName=@FullName, Role=@Role, FarmId=@FarmId, Phone=@Phone,
                     Email=@Email, Address=@Address, EmergencyContact=@EmergencyContact, Status=@Status,
                     MonthlySalary=@MonthlySalary, JoinDate=@JoinDate, IsActive=@IsActive
              WHERE Id=@Id", employee);
        await RecountFarmsAsync(conn, oldFarm, employee.FarmId);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        var farm = await conn.ExecuteScalarAsync<int?>("SELECT FarmId FROM Employees WHERE Id=@id", new { id });
        await conn.ExecuteAsync("UPDATE Employees SET IsActive=0, Status='Resigned' WHERE Id=@id", new { id });
        await RecountFarmsAsync(conn, farm, null);
    }

    private static async Task RecountFarmsAsync(System.Data.IDbConnection conn, int? a, int? b)
    {
        foreach (var farmId in new[] { a, b }.Where(x => x.HasValue).Distinct())
            await conn.ExecuteAsync(
                "UPDATE Farms SET EmployeeCount = (SELECT COUNT(*) FROM Employees WHERE FarmId=@farmId AND IsActive=1) WHERE Id=@farmId",
                new { farmId });
    }

    public async Task<bool> CodeExistsAsync(string code, int exceptId = 0)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Employees WHERE EmployeeCode=@code AND Id<>@exceptId", new { code, exceptId }) > 0;
    }

    public async Task<string> NextCodeAsync()
    {
        using var conn = db.Create();
        var codes = await conn.QueryAsync<string>("SELECT EmployeeCode FROM Employees WHERE EmployeeCode LIKE 'EMP-%'");
        var max = codes.Select(c => int.TryParse(c.AsSpan(4), out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"EMP-{max + 1:000}";
    }

    public async Task<(int Marked, decimal PresentWeight)> GetTodayAttendanceAsync()
    {
        using var conn = db.Create();
        var rows = (await conn.QueryAsync<string>(
            @"SELECT a.Status FROM Attendance a JOIN Employees e ON e.Id=a.EmployeeId
              WHERE e.IsActive=1 AND a.AttendanceDate = CAST(GETDATE() AS DATE)")).ToList();
        return (rows.Count, rows.Sum(AttendanceRecord.PresenceWeight));
    }

    public async Task<EmployeeProfileVm?> GetProfileAsync(int id)
    {
        var emp = await GetByIdAsync(id);
        if (emp == null) return null;
        using var conn = db.Create();
        var today = DateTime.Today;
        var from = today.AddDays(-29);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var att = (await conn.QueryAsync<AttendanceRecord>(
            @"SELECT * FROM Attendance WHERE EmployeeId=@id AND AttendanceDate BETWEEN @from AND @today ORDER BY AttendanceDate",
            new { id, from = from < monthStart ? from : monthStart, today })).ToList();

        var payroll = (await conn.QueryAsync<PayrollEntry>(
            @"SELECT TOP 12 p.*, e.EmployeeCode, e.FullName, e.Role FROM Payroll p JOIN Employees e ON e.Id=p.EmployeeId
              WHERE p.EmployeeId=@id ORDER BY p.PeriodYear DESC, p.PeriodMonth DESC", new { id })).ToList();

        var tasks = (await conn.QueryAsync<FarmTask>(
            @"SELECT t.*, f.Name AS FarmName, b.BatchCode, e.FullName AS AssigneeName FROM FarmTasks t
              LEFT JOIN Farms f ON f.Id=t.FarmId LEFT JOIN Batches b ON b.Id=t.BatchId
              LEFT JOIN Employees e ON e.Id=t.AssignedToEmployeeId
              WHERE t.AssignedToEmployeeId=@id AND t.Status<>'Done'
              ORDER BY CASE WHEN t.DueDate IS NULL THEN 1 ELSE 0 END, t.DueDate", new { id })).ToList();
        var done = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM FarmTasks WHERE AssignedToEmployeeId=@id AND Status='Done'", new { id });

        // this month's working days so far (Friday is the weekly holiday)
        var workingDays = 0;
        for (var d = monthStart; d <= today; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Friday && d >= emp.JoinDate.Date) workingDays++;
        var monthRows = att.Where(a => a.AttendanceDate >= monthStart).ToList();
        var presentDays = monthRows.Sum(a => AttendanceRecord.PresenceWeight(a.Status));

        var checkIns = att.Where(a => a.CheckIn.HasValue).Select(a => a.CheckIn!.Value.TotalMinutes).ToList();
        var hours = att.Select(a => a.HoursWorked).Where(h => h.HasValue).Select(h => h!.Value).ToList();

        return new EmployeeProfileVm
        {
            Employee = emp,
            Attendance30 = att.Where(a => a.AttendanceDate >= from).ToList(),
            PayrollHistory = payroll,
            OpenTasks = tasks,
            DoneTasks = done,
            MonthWorkingDays = workingDays,
            MonthPresentDays = presentDays,
            MonthAttendancePct = workingDays == 0 ? 0 : Math.Min(100m, presentDays / workingDays * 100m),
            MonthLate = monthRows.Count(a => a.Status == "Late"),
            MonthAbsent = monthRows.Count(a => a.Status == "Absent"),
            MonthLeave = monthRows.Count(a => a.Status == "Leave"),
            AvgCheckInMinutes = checkIns.Count == 0 ? null : checkIns.Average(),
            AvgHoursWorked = hours.Count == 0 ? null : hours.Average()
        };
    }
}
