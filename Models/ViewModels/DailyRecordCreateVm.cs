namespace PoultryPro.Models.ViewModels
{
    public class DailyRecordCreateVm
    {
        public int VisitId { get; set; }
        public int BatchId { get; set; }
        public int FarmId { get; set; }
        public DateTime RecordDate { get; set; } = DateTime.Today;
        public int BirdCount { get; set; }
        public int MortalityCount { get; set; }
        public int CullingCount { get; set; }
        public decimal FeedConsumptionKg { get; set; }
        public decimal WaterConsumptionLtr { get; set; }
        public decimal? TemperatureC { get; set; }
        public decimal? HumidityPercent { get; set; }
        public decimal? LightHours { get; set; }
        public decimal? AverageWeightGm { get; set; }
        public string? MedicineUsed { get; set; }
        public string? MedicineDosage { get; set; }
        public string? VaccineUsed { get; set; }
        public string? Remarks { get; set; }
    }
}