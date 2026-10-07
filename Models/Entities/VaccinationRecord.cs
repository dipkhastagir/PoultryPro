namespace PoultryPro.Models.Entities
{
    public class VaccinationRecord
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string Breed { get; set; } = "";
        public string FarmName { get; set; } = "";
        public int FarmId { get; set; }
        public string VaccineName { get; set; } = "";
        public DateTime DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Status { get; set; } = "Planned";
        public decimal? DoseMl { get; set; }
        public string? Route { get; set; }
        public string? AdministeredBy { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOverdue => CompletedDate == null && DueDate < DateTime.Today;
        public int DaysOverdue => IsOverdue ? (DateTime.Today - DueDate).Days : 0;
        /// <summary>Completed on or before the due date (+1 day grace).</summary>
        public bool OnTime => CompletedDate.HasValue && CompletedDate.Value.Date <= DueDate.Date.AddDays(1);
    }
}
