using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    public class TraceBatchRow
    {
        public int Id { get; set; }
        public string BatchCode { get; set; } = "";
        public string Breed { get; set; } = "";
        public string BatchType { get; set; } = "";
        public string FarmName { get; set; } = "";
        public string FarmLocation { get; set; } = "";
        public DateTime PlacementDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public int AgeInDays { get; set; }
        public int InitialCount { get; set; }
        public int CurrentCount { get; set; }
        public bool IsActive { get; set; }
        public int VaxDone { get; set; }
        public int VaxTotal { get; set; }
        public int OrderCount { get; set; }
    }

    public class TraceEvent
    {
        public DateTime Date { get; set; }
        public string Kind { get; set; } = "";
        public string Icon { get; set; } = "bi-circle";
        public string Color { get; set; } = "var(--primary)";
        public string Title { get; set; } = "";
        public string? Body { get; set; }
        public string? Badge { get; set; }
        public string? Link { get; set; }
        public int Order { get; set; }

        /// <summary>Date-independent title/detail used for hashing when the displayed text depends on "today" (e.g. "overdue").</summary>
        public string? ChainTitle { get; set; }
        public string? ChainDetail { get; set; }
        /// <summary>SHA-256 link of this event in the batch hash chain (64 lower-case hex chars).</summary>
        public string Hash { get; set; } = "";
        public string ShortHash => Hash.Length >= 12 ? Hash[..12] : Hash;
    }

    /// <summary>Seal verdict for a batch's traceability hash chain.</summary>
    public class TraceSealInfo
    {
        public const string NotSealed = "Not sealed";
        public const string Intact    = "Intact";
        public const string Changed   = "Changed";

        public string ChainHead { get; set; } = "";
        public string? SealHash { get; set; }
        public DateTime? SealedAt { get; set; }
        public string? SealedBy { get; set; }

        public bool IsSealed => !string.IsNullOrEmpty(SealHash);
        public string Status => !IsSealed ? NotSealed
            : string.Equals(SealHash, ChainHead, StringComparison.OrdinalIgnoreCase) ? Intact : Changed;
    }

    public class TraceTimelineVm
    {
        public Batch Batch { get; set; } = new();
        public Farm? Farm { get; set; }
        public List<TraceEvent> Events { get; set; } = new();
        public string PublicUrl { get; set; } = "";
        public string VerificationHash { get; set; } = "";
        /// <summary>SHA-256 of the batch master payload — the first link of the chain.</summary>
        public string GenesisHash { get; set; } = "";
        public TraceSealInfo Seal { get; set; } = new();
        public int VaccinationsDone { get; set; }
        public int VaccinationsOverdue { get; set; }
        public int DiseaseCases { get; set; }
        public int MedicineDays { get; set; }
        public int Visits { get; set; }
        public int SalesOrders { get; set; }
    }

    public class TraceVaccineLine
    {
        public string VaccineName { get; set; } = "";
        public DateTime? CompletedDate { get; set; }
        public string? Route { get; set; }
    }

    public class TracePublicVm
    {
        public bool Found { get; set; }
        public string Code { get; set; } = "";
        public string BatchCode { get; set; } = "";
        public string Breed { get; set; } = "";
        public string BatchType { get; set; } = "";
        public string FarmName { get; set; } = "";
        public string FarmLocation { get; set; } = "";
        public string? Zone { get; set; }
        public string? FarmType { get; set; }
        public string? HatcheryName { get; set; }
        public DateTime PlacementDate { get; set; }
        public DateTime? HarvestDate { get; set; }
        public bool IsHarvested { get; set; }
        public int AgeAtHarvest { get; set; }
        public decimal AverageWeightKg { get; set; }
        public decimal LivabilityPct { get; set; }
        public List<TraceVaccineLine> Vaccinations { get; set; } = new();
        public bool MedicineInWithdrawalWindow { get; set; }
        public DateTime? LastMedicineDate { get; set; }
        public string WithdrawalStatement { get; set; } = "";
        public string? BiosecurityGrade { get; set; }
        public decimal? BiosecurityPct { get; set; }
        public DateTime? BiosecurityAuditDate { get; set; }
        public List<string> Certifications { get; set; } = new();
        public string VerificationHash { get; set; } = "";
        public TraceSealInfo Seal { get; set; } = new();
        public int ChainLength { get; set; }
        public string CompanyName { get; set; } = "PoultryPro";
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }
}
