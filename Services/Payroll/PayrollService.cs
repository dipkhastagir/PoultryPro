using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>
/// Monthly payroll register. Generation rules:
///   Basic = MonthlySalary, Allowances = 10 % of basic, Daily rate = MonthlySalary / 26,
///   Deductions = absent days x daily rate, DaysPresent = Present + Late + 0.5 x Half Day (from Attendance).
/// Paying a row posts one 'Salary' expense transaction to finance (reference PAY-yyyyMM-EmpCode, never duplicated).
/// </summary>
public interface IPayrollService
{
    Task<PayrollRegisterVm> GetRegisterAsync(int year, int month);
    Task<PayrollEntry?> GetByIdAsync(int id);
    Task<int> GenerateAsync(int year, int month);
    Task UpdateAsync(int id, decimal allowances, decimal overtime, decimal deductions, int daysPresent, int daysAbsent);
    Task<int> MarkPaidAsync(IEnumerable<int> ids);
    Task<int> MarkMonthPaidAsync(int year, int month);
    Task DeleteAsync(int id);
}

public class PayrollService(IDbConnectionFactory db) : IPayrollService
{
    public const decimal AllowanceRate = 0.10m;
    public const int WorkingDaysPerMonth = 26;

    private const string Select = @"SELECT p.*, e.EmployeeCode, e.FullName, e.Role, e.FarmId, f.Name AS FarmName, e.Phone, e.Email, e.JoinDate
                                    FROM Payroll p JOIN Employees e ON e.Id = p.EmployeeId LEFT JOIN Farms f ON f.Id = e.FarmId";

    public async Task<PayrollRegisterVm> GetRegisterAsync(int year, int month)
    {
        using var conn = db.Create();
        var rows = (await conn.QueryAsync<PayrollEntry>(Select +
            " WHERE p.PeriodYear=@year AND p.PeriodMonth=@month ORDER BY CASE WHEN f.Name IS NULL THEN 1 ELSE 0 END, f.Name, e.FullName",
            new { year, month })).ToList();
        var periodEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        var active = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Employees WHERE IsActive=1 AND JoinDate<=@periodEnd", new { periodEnd });
        var missing = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Employees e WHERE e.IsActive=1 AND e.JoinDate<=@periodEnd
              AND NOT EXISTS (SELECT 1 FROM Payroll p WHERE p.EmployeeId=e.Id AND p.PeriodYear=@year AND p.PeriodMonth=@month)",
            new { periodEnd, year, month });

        var first = new DateTime(year, month, 1).AddMonths(-5);
        var trendRows = (await conn.QueryAsync<(int Y, int M, decimal Net)>(
            @"SELECT PeriodYear, PeriodMonth, SUM(NetPay) FROM Payroll
              WHERE PeriodYear*100+PeriodMonth BETWEEN @a AND @b GROUP BY PeriodYear, PeriodMonth",
            new { a = first.Year * 100 + first.Month, b = year * 100 + month })).ToList();
        var trend = new List<(string, decimal)>();
        for (var d = first; d <= new DateTime(year, month, 1); d = d.AddMonths(1))
            trend.Add((d.ToString("MMM yy"), trendRows.Where(t => t.Y == d.Year && t.M == d.Month).Sum(t => t.Net)));

        return new PayrollRegisterVm { Year = year, Month = month, Rows = rows, ActiveEmployees = active, MissingRows = missing, Trend = trend };
    }

    public async Task<PayrollEntry?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<PayrollEntry>(Select + " WHERE p.Id=@id", new { id });
    }

    public async Task<int> GenerateAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var employees = (await conn.QueryAsync<Employee>(
            @"SELECT e.* FROM Employees e WHERE e.IsActive=1 AND e.JoinDate<=@end
              AND NOT EXISTS (SELECT 1 FROM Payroll p WHERE p.EmployeeId=e.Id AND p.PeriodYear=@year AND p.PeriodMonth=@month)",
            new { end, year, month }, tx)).ToList();
        if (employees.Count == 0) { tx.Commit(); return 0; }

        var counts = (await conn.QueryAsync<(int EmployeeId, string Status, int N)>(
            @"SELECT EmployeeId, Status, COUNT(*) FROM Attendance WHERE AttendanceDate BETWEEN @start AND @end
              GROUP BY EmployeeId, Status", new { start, end }, tx)).ToLookup(c => c.EmployeeId);

        foreach (var e in employees)
        {
            var c = counts[e.Id].ToDictionary(x => x.Status, x => x.N);
            int Get(string s) => c.TryGetValue(s, out var n) ? n : 0;
            var present = Get("Present") + Get("Late") + Get("Half Day") * 0.5m;
            var absent = Get("Absent");
            var dailyRate = e.MonthlySalary / WorkingDaysPerMonth;
            await conn.ExecuteAsync(
                @"INSERT INTO Payroll(EmployeeId, PeriodYear, PeriodMonth, BasicSalary, Allowances, Overtime, Deductions, DaysPresent, DaysAbsent, Status)
                  VALUES(@EmployeeId, @year, @month, @basic, @allow, 0, @ded, @present, @absent, 'Pending')",
                new
                {
                    EmployeeId = e.Id, year, month,
                    basic = e.MonthlySalary,
                    allow = Math.Round(e.MonthlySalary * AllowanceRate, 0),
                    ded = Math.Round(absent * dailyRate, 0),
                    present = (int)Math.Round(present, MidpointRounding.AwayFromZero),
                    absent
                }, tx);
        }
        tx.Commit();
        return employees.Count;
    }

    public async Task UpdateAsync(int id, decimal allowances, decimal overtime, decimal deductions, int daysPresent, int daysAbsent)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Payroll SET Allowances=@allowances, Overtime=@overtime, Deductions=@deductions,
                     DaysPresent=@daysPresent, DaysAbsent=@daysAbsent
              WHERE Id=@id AND Status<>'Paid'", new { id, allowances, overtime, deductions, daysPresent, daysAbsent });
    }

    public async Task<int> MarkMonthPaidAsync(int year, int month)
    {
        using var conn = db.Create();
        var ids = await conn.QueryAsync<int>(
            "SELECT Id FROM Payroll WHERE PeriodYear=@year AND PeriodMonth=@month AND Status<>'Paid'", new { year, month });
        return await MarkPaidAsync(ids);
    }

    public async Task<int> MarkPaidAsync(IEnumerable<int> ids)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return 0;
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var rows = (await conn.QueryAsync<PayrollEntry>(Select + " WHERE p.Id IN @list AND p.Status<>'Paid'", new { list }, tx)).ToList();
        foreach (var r in rows)
        {
            await conn.ExecuteAsync("UPDATE Payroll SET Status='Paid', PaidDate=CAST(GETDATE() AS DATE) WHERE Id=@Id", new { r.Id }, tx);
            await conn.ExecuteAsync(
                @"IF NOT EXISTS (SELECT 1 FROM Transactions WHERE Reference=@reference)
                  INSERT INTO Transactions(TxnDate, TxnType, Category, Description, Amount, PaymentStatus, FarmId, Reference, PaymentMethod)
                  VALUES(CAST(GETDATE() AS DATE), 'Salary', 'Salary', @description, @amount, 'Paid', @FarmId, @reference, 'Bank Transfer')",
                new
                {
                    reference = r.PayReference,
                    description = $"Salary {r.PeriodLabel} - {r.FullName}".Length > 300 ? $"Salary {r.PeriodLabel}" : $"Salary {r.PeriodLabel} - {r.FullName}",
                    amount = -Math.Abs(r.NetPay),
                    r.FarmId
                }, tx);
        }
        tx.Commit();
        return rows.Count;
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("DELETE FROM Payroll WHERE Id=@id AND Status<>'Paid'", new { id });
    }

    /// <summary>English words for an amount in Taka (Bangladeshi lakh/crore grouping).</summary>
    public static string AmountInWords(decimal amount)
    {
        var taka = (long)Math.Floor(Math.Abs(amount));
        var paisa = (int)Math.Round((Math.Abs(amount) - taka) * 100);
        var words = taka == 0 ? "Zero" : Words(taka);
        var s = $"Taka {words}";
        if (paisa > 0) s += $" and {Words(paisa)} Paisa";
        return s + " Only";
    }

    private static readonly string[] Ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
    private static readonly string[] Tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

    private static string Words(long n)
    {
        if (n == 0) return "";
        if (n < 20) return Ones[n];
        if (n < 100) return (Tens[n / 10] + " " + Ones[n % 10]).Trim();
        if (n < 1000) return (Ones[n / 100] + " Hundred " + Words(n % 100)).Trim();
        if (n < 100_000) return (Words(n / 1000) + " Thousand " + Words(n % 1000)).Trim();
        if (n < 10_000_000) return (Words(n / 100_000) + " Lakh " + Words(n % 100_000)).Trim();
        return (Words(n / 10_000_000) + " Crore " + Words(n % 10_000_000)).Trim();
    }
}
