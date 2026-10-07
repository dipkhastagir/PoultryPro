namespace PoultryPro.Models.Entities
{
    public class Farm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public int Capacity { get; set; }
        public int CurrentStock { get; set; }
        public int EmployeeCount { get; set; }
        public string CertStatus { get; set; } = "Valid";
        public DateTime? CertExpiry { get; set; }
        public bool IsActive { get; set; } = true;
        public string FarmType { get; set; } = "Broiler";
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerPhone { get; set; }
        public string? Zone { get; set; }
        public int ShedCount { get; set; } = 1;
        public int? AreaSqFt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public double UtilizationPercent => Capacity > 0 ? (double)CurrentStock / Capacity * 100 : 0;
    }
}