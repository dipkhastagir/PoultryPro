namespace PoultryPro.Models.Entities
{
    /// <summary>One row of dbo.Attendance (unique per employee and date).</summary>
    public class AttendanceRecord
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string Status { get; set; } = "Present";
        public TimeSpan? CheckIn { get; set; }
        public TimeSpan? CheckOut { get; set; }
        public string? Notes { get; set; }

        public static readonly string[] Statuses = { "Present", "Late", "Half Day", "Leave", "Absent" };

        /// <summary>Short code used in the monthly matrix.</summary>
        public static string Code(string? status) => status switch
        {
            "Present" => "P",
            "Late" => "L",
            "Half Day" => "H",
            "Leave" => "LV",
            "Absent" => "A",
            _ => ""
        };

        /// <summary>Present / Late = 1 day, Half Day = 0.5, otherwise 0.</summary>
        public static decimal PresenceWeight(string? status) => status switch
        {
            "Present" or "Late" => 1m,
            "Half Day" => 0.5m,
            _ => 0m
        };

        public double? HoursWorked => CheckIn.HasValue && CheckOut.HasValue && CheckOut > CheckIn
            ? (CheckOut.Value - CheckIn.Value).TotalHours : null;
    }

    /// <summary>One row of dbo.Payroll joined with employee information.</summary>
    public class PayrollEntry
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal Allowances { get; set; }
        public decimal Overtime { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetPay { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime? PaidDate { get; set; }
        public DateTime CreatedAt { get; set; }

        // joined
        public string? EmployeeCode { get; set; }
        public string FullName { get; set; } = "";
        public string? Role { get; set; }
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime? JoinDate { get; set; }

        public string PeriodLabel => new DateTime(PeriodYear, Math.Clamp(PeriodMonth, 1, 12), 1).ToString("MMMM yyyy");
        public string CodeOrId => string.IsNullOrWhiteSpace(EmployeeCode) ? $"EMP{EmployeeId}" : EmployeeCode!;
        public string PayReference => $"PAY-{PeriodYear:0000}{PeriodMonth:00}-{CodeOrId}";
        public decimal Gross => BasicSalary + Allowances + Overtime;
    }

    /// <summary>dbo.FarmTasks row with display joins.</summary>
    public class FarmTask
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public int? BatchId { get; set; }
        public string? BatchCode { get; set; }
        public int? AssignedToEmployeeId { get; set; }
        public string? AssigneeName { get; set; }
        public string Category { get; set; } = "General";
        public string Priority { get; set; } = "Medium";
        public string Status { get; set; } = "Open";
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public static readonly string[] Statuses = { "Open", "In Progress", "Done" };
        public static readonly string[] Priorities = { "Low", "Medium", "High", "Urgent" };
        public static readonly string[] Categories = { "General", "Cleaning", "Vaccination", "Feeding", "Maintenance", "Sales" };

        public bool IsOverdue => Status != "Done" && DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
        public bool IsDueToday => Status != "Done" && DueDate.HasValue && DueDate.Value.Date == DateTime.Today;
        public int PriorityRank => Priority switch { "Urgent" => 0, "High" => 1, "Medium" => 2, _ => 3 };
    }

    /// <summary>An event shown on the farm calendar (aggregated from several modules).</summary>
    public class FarmCalendarEvent
    {
        public DateTime Date { get; set; }
        /// <summary>vax | task | harvest | order | cert (matches .cal-ev CSS classes).</summary>
        public string Kind { get; set; } = "task";
        public string Title { get; set; } = "";
        public string? Detail { get; set; }
        public string Controller { get; set; } = "";
        public string Action { get; set; } = "Index";
        public int? RouteId { get; set; }
        public bool IsOverdue { get; set; }
        public string? FarmName { get; set; }

        public string KindLabel => Kind switch
        {
            "vax" => "Vaccination",
            "task" => "Task",
            "harvest" => "Harvest",
            "order" => "Delivery",
            "cert" => "Certification",
            _ => Kind
        };

        public string Icon => Kind switch
        {
            "vax" => "bi-shield-plus",
            "task" => "bi-check2-square",
            "harvest" => "bi-basket",
            "order" => "bi-truck",
            "cert" => "bi-patch-check",
            _ => "bi-circle"
        };
    }
}
