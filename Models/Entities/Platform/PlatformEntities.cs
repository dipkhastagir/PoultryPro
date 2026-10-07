namespace PoultryPro.Models.Entities
{
    public class AppUser
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string PasswordHash { get; set; } = "";
        public string Role { get; set; } = "Viewer";
        public int? FieldOfficerId { get; set; }
        public string? FieldOfficerName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Initials => string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }

    public class AuditLog
    {
        public long Id { get; set; }
        public string? UserName { get; set; }
        public string Action { get; set; } = "";
        public string Entity { get; set; } = "";
        public string? EntityId { get; set; }
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Alert
    {
        public int Id { get; set; }
        public string RuleCode { get; set; } = "";
        public string Severity { get; set; } = "Warning";
        public string Title { get; set; } = "";
        public string? Message { get; set; }
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public int? BatchId { get; set; }
        public string? BatchCode { get; set; }
        public string? RefKey { get; set; }
        public string? Link { get; set; }
        public bool IsRead { get; set; }
        public bool IsResolved { get; set; }
        public string? ResolvedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AppSetting
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public string GroupName { get; set; } = "General";
        public string? Description { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class BreedStandard
    {
        public int Id { get; set; }
        public string Breed { get; set; } = "";
        public int AgeDay { get; set; }
        public decimal TargetWeightGm { get; set; }
        public decimal DailyFeedGm { get; set; }
        public decimal CumFeedGm { get; set; }
        public decimal TargetFCR { get; set; }
        public decimal TargetCumMortPct { get; set; }
    }
}
