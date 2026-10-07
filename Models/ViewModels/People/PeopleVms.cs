using System.ComponentModel.DataAnnotations;
using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    // ---------------------------------------------------------------- Employees
    public class EmployeeDirectoryVm
    {
        public List<Employee> Employees { get; set; } = new();
        public List<Farm> Farms { get; set; } = new();
        public string? Q { get; set; }
        public int? FarmId { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public bool ShowInactive { get; set; }
        public Dictionary<string, int> RoleCounts { get; set; } = new();
        public int TotalActive { get; set; }
        public int OnDuty { get; set; }
        public int OnLeave { get; set; }
        public decimal MonthlyPayroll { get; set; }
        public decimal AttendanceTodayPct { get; set; }
        public int MarkedToday { get; set; }
    }

    public class EmployeeProfileVm
    {
        public Employee Employee { get; set; } = new();
        public List<AttendanceRecord> Attendance30 { get; set; } = new();
        public List<PayrollEntry> PayrollHistory { get; set; } = new();
        public List<FarmTask> OpenTasks { get; set; } = new();
        public int DoneTasks { get; set; }
        public decimal MonthAttendancePct { get; set; }
        public int MonthWorkingDays { get; set; }
        public decimal MonthPresentDays { get; set; }
        public int MonthLate { get; set; }
        public int MonthAbsent { get; set; }
        public int MonthLeave { get; set; }
        public double? AvgCheckInMinutes { get; set; }
        public double? AvgHoursWorked { get; set; }
    }

    // --------------------------------------------------------------- Attendance
    public class AttendanceSheetRow
    {
        public int EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public string? EmpStatus { get; set; }
        public int? AttendanceId { get; set; }
        public string? Status { get; set; }
        public TimeSpan? CheckIn { get; set; }
        public TimeSpan? CheckOut { get; set; }
        public string? Notes { get; set; }
        public string Initials => string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }

    public class AttendanceSheetVm
    {
        public DateTime Date { get; set; }
        public List<AttendanceSheetRow> Rows { get; set; } = new();
        public bool IsWeekend => Date.DayOfWeek == DayOfWeek.Friday;
        public int Marked => Rows.Count(r => r.AttendanceId.HasValue);
        public int Present => Rows.Count(r => r.Status == "Present");
        public int Late => Rows.Count(r => r.Status == "Late");
        public int HalfDay => Rows.Count(r => r.Status == "Half Day");
        public int Leave => Rows.Count(r => r.Status == "Leave");
        public int Absent => Rows.Count(r => r.Status == "Absent");
        public decimal PresentPct => Rows.Count == 0 ? 0
            : Rows.Sum(r => AttendanceRecord.PresenceWeight(r.Status)) / Rows.Count * 100m;
    }

    /// <summary>Posted row of the daily attendance sheet.</summary>
    public class AttendanceSaveRow
    {
        public int EmployeeId { get; set; }
        public string? Status { get; set; }
        public string? CheckIn { get; set; }
        public string? CheckOut { get; set; }
        public string? Notes { get; set; }
    }

    public class AttendanceMatrixRow
    {
        public int EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public string? FarmName { get; set; }
        public Dictionary<int, string> Days { get; set; } = new();
        public int Present { get; set; }
        public int Late { get; set; }
        public int HalfDay { get; set; }
        public int Leave { get; set; }
        public int Absent { get; set; }
        public int Unmarked { get; set; }
        public decimal PresentDays => Present + Late + HalfDay * 0.5m;
        public decimal AttendancePct { get; set; }
    }

    public class AttendanceMonthVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int DaysInMonth { get; set; }
        public int WorkingDays { get; set; }
        public int ElapsedWorkingDays { get; set; }
        public List<AttendanceMatrixRow> Rows { get; set; } = new();
        public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public decimal OverallPct => Rows.Count == 0 ? 0 : Rows.Average(r => r.AttendancePct);
        public List<decimal> DailyPresentPct { get; set; } = new();
    }

    // ------------------------------------------------------------------ Payroll
    public class PayrollRegisterVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<PayrollEntry> Rows { get; set; } = new();
        public int ActiveEmployees { get; set; }
        public int MissingRows { get; set; }
        public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public decimal TotalNet => Rows.Sum(r => r.NetPay);
        public decimal TotalBasic => Rows.Sum(r => r.BasicSalary);
        public decimal TotalAllowances => Rows.Sum(r => r.Allowances);
        public decimal TotalOvertime => Rows.Sum(r => r.Overtime);
        public decimal TotalDeductions => Rows.Sum(r => r.Deductions);
        public decimal PaidNet => Rows.Where(r => r.Status == "Paid").Sum(r => r.NetPay);
        public decimal PendingNet => Rows.Where(r => r.Status != "Paid").Sum(r => r.NetPay);
        public int PendingCount => Rows.Count(r => r.Status != "Paid");
        public List<(string Label, decimal Net)> Trend { get; set; } = new();
    }

    public class PayrollEditVm
    {
        public PayrollEntry Entry { get; set; } = new();
        [Range(0, 10_000_000)] public decimal Allowances { get; set; }
        [Range(0, 10_000_000)] public decimal Overtime { get; set; }
        [Range(0, 10_000_000)] public decimal Deductions { get; set; }
        [Range(0, 31)] public int DaysPresent { get; set; }
        [Range(0, 31)] public int DaysAbsent { get; set; }
    }

    public class PayslipVm
    {
        public PayrollEntry Entry { get; set; } = new();
        public string CompanyName { get; set; } = "";
        public string? CompanyAddress { get; set; }
        public string? CompanyPhone { get; set; }
        public int WorkingDays { get; set; }
        public decimal DailyRate { get; set; }
        public string AmountInWords { get; set; } = "";
    }

    // -------------------------------------------------------------------- Tasks
    public class TaskBoardVm
    {
        public List<FarmTask> Tasks { get; set; } = new();
        public List<Farm> Farms { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public int? FarmId { get; set; }
        public string? Priority { get; set; }
        public int? AssigneeId { get; set; }
        public string? Category { get; set; }
        public bool CanEdit { get; set; }
    }

    public class TaskFormVm
    {
        public int Id { get; set; }
        [Required, StringLength(200)] public string Title { get; set; } = "";
        [StringLength(1000)] public string? Description { get; set; }
        public int? FarmId { get; set; }
        public int? BatchId { get; set; }
        public int? AssignedToEmployeeId { get; set; }
        [Required] public string Category { get; set; } = "General";
        [Required] public string Priority { get; set; } = "Medium";
        [Required] public string Status { get; set; } = "Open";
        [DataType(DataType.Date)] public DateTime? DueDate { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class TaskBatchOption
    {
        public int Id { get; set; }
        public string BatchCode { get; set; } = "";
        public int FarmId { get; set; }
        public string? Breed { get; set; }
    }

    // ----------------------------------------------------------------- Calendar
    public class CalendarMonthVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime GridStart { get; set; }
        public DateTime GridEnd { get; set; }
        public List<FarmCalendarEvent> Events { get; set; } = new();
        public List<FarmCalendarEvent> Upcoming { get; set; } = new();
        public int OverdueVaccinations { get; set; }
        public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public ILookup<DateTime, FarmCalendarEvent> ByDay => Events.ToLookup(e => e.Date.Date);
        public int Count(string kind) => Events.Count(e => e.Kind == kind && e.Date.Month == Month && e.Date.Year == Year);
    }

    // -------------------------------------------------------------------- Users
    public class UserFormVm
    {
        public int Id { get; set; }
        [Required, StringLength(150), Display(Name = "Full name")] public string FullName { get; set; } = "";
        [Required, StringLength(60, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9_.\-]+$", ErrorMessage = "Letters, digits, dot, dash and underscore only.")]
        public string Username { get; set; } = "";
        [EmailAddress, StringLength(150)] public string? Email { get; set; }
        [StringLength(20)] public string? Phone { get; set; }
        [Required] public string Role { get; set; } = "Viewer";
        [Display(Name = "Linked field officer")] public int? FieldOfficerId { get; set; }
        public bool IsActive { get; set; } = true;
        [DataType(DataType.Password), StringLength(100, MinimumLength = 6)] public string? Password { get; set; }
        [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
        public string? ConfirmPassword { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class UsersIndexVm
    {
        public List<AppUser> Users { get; set; } = new();
        public int CurrentUserId { get; set; }
        public List<(string Area, string[] Roles)> Matrix { get; set; } = new();
    }

    // ---------------------------------------------------------------- Audit log
    public class AuditLogVm
    {
        public List<AuditLog> Rows { get; set; } = new();
        public List<string> Entities { get; set; } = new();
        public List<string> Users { get; set; } = new();
        public string? User { get; set; }
        public string? Entity { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<(DateTime Day, int Count)> PerDay { get; set; } = new();
        public List<(string Name, int Count)> TopEntities { get; set; } = new();
        public List<(string Name, int Count)> TopUsers { get; set; } = new();
    }

    // ----------------------------------------------------------------- Settings
    public class SettingsVm
    {
        public List<IGrouping<string, AppSetting>> Groups { get; set; } = new();
        public int Total { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    // ---------------------------------------------------- Field operations report
    public class FieldOpsOfficerRow
    {
        public int FieldOfficerId { get; set; }
        public string FullName { get; set; } = "";
        public string? Zone { get; set; }
        public int Visits30 { get; set; }
        public int Visits90 { get; set; }
        public int Completed90 { get; set; }
        public int Cancelled90 { get; set; }
        public double? AvgDurationMin { get; set; }
        public double? AvgGpsAccuracy { get; set; }
        public int GpsFixes { get; set; }
        public int FarmsCovered { get; set; }
        public int DailyRecords90 { get; set; }
        public int Photos90 { get; set; }
        public int PendingVisits { get; set; }
        public int PendingDailyRecords { get; set; }
        public int PendingPhotos { get; set; }
        public int PendingSignatures { get; set; }
        public DateTime? LastVisit { get; set; }
        /// <summary>Mean Visit Trust Score (0–100) over the report window; null when no visits.</summary>
        public double? AvgTrustScore { get; set; }
        /// <summary>% of visits in the window whose check-in falls inside the farm geofence; null when no visits.</summary>
        public double? GeoVerifiedPct { get; set; }
        public decimal CompletionRate => Visits90 == 0 ? 0 : (decimal)Completed90 / Visits90 * 100m;
        public int PendingTotal => PendingVisits + PendingDailyRecords + PendingPhotos + PendingSignatures;
    }

    public class FieldOpsDayPoint
    {
        public DateTime Day { get; set; }
        public int Visits { get; set; }
        public int Records { get; set; }
    }

    public class FieldOpsReportVm
    {
        public int Days { get; set; } = 90;
        public List<FieldOpsOfficerRow> Officers { get; set; } = new();
        public List<FieldOpsDayPoint> Daily { get; set; } = new();
        public List<(string Purpose, int Count)> Purposes { get; set; } = new();
        public List<(string Bucket, int Count)> GpsBuckets { get; set; } = new();
        public List<(string Farm, int Visits, DateTime? Last)> FarmCoverage { get; set; } = new();
        public int TotalVisits => Officers.Sum(o => o.Visits90);
        public int TotalVisits30 => Officers.Sum(o => o.Visits30);
        public decimal CompletionRate => TotalVisits == 0 ? 0 : (decimal)Officers.Sum(o => o.Completed90) / TotalVisits * 100m;
        public double? AvgDurationMin { get; set; }
        public double? AvgGpsAccuracy { get; set; }
        public int PendingSync => Officers.Sum(o => o.PendingTotal);
        public int TotalRecords => Daily.Sum(d => d.Records);
    }
}
