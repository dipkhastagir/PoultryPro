namespace PoultryPro.Models.Entities
{
    public class DailyFarmRecord
    {
        public string FieldOfficerName { get; set; } = "";
        public string? Breed { get; set; }
        public int FarmId { get; set; }
        public int Id { get; set; }
        public int VisitId { get; set; }
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string FarmName { get; set; } = "";
        public DateTime RecordDate { get; set; }
        public int BirdCount { get; set; }
        public int MortalityCount { get; set; }
        public int CullingCount { get; set; }
        public decimal FeedConsumptionKg { get; set; }
        public decimal WaterConsumptionLtr { get; set; }
        public decimal? TemperatureC { get; set; }
        public decimal? HumidityPercent { get; set; }
        public decimal? AverageWeightGm { get; set; }
        public decimal? LightHours { get; set; }
        // Treatments
        public string? MedicineUsed { get; set; }
        public string? MedicineDosage { get; set; }
        public string? VaccineUsed { get; set; }
        public string? Remarks { get; set; }
        public decimal FeedPerBird { get; set; }
        public string SyncStatus { get; set; } = "Pending";
        public DateTime? SyncedAt { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}