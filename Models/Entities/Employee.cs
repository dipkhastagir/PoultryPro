namespace PoultryPro.Models.Entities
{
    public class Employee
    {
        public int Id { get; set; }
        public string? EmployeeCode { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? EmergencyContact { get; set; }
        public string Status { get; set; } = "On Duty";
        public decimal MonthlySalary { get; set; }
        public DateTime JoinDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        public string Initials
        {
            get
            {
                var s = string.Concat((FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpperInvariant(p[0])));
                return s.Length == 0 ? "?" : s;
            }
        }

        public int TenureMonths => JoinDate == default ? 0
            : Math.Max(0, (DateTime.Today.Year - JoinDate.Year) * 12 + DateTime.Today.Month - JoinDate.Month);
    }
}
