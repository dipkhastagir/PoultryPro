namespace PoultryPro.Models.Entities
{
    public class FieldOfficer
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Zone { get; set; }
        public string Designation { get; set; } = "Field Officer";
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int AssignedFarmCount { get; set; }
        public int PendingVisits { get; set; }
        public string Initials
        {
            get
            {
                var s = string.Concat((FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpperInvariant(p[0])));
                return s.Length == 0 ? "?" : s;
            }
        }
    }
}