namespace PoultryPro.Models.ViewModels
{
    public class VisitCreateVm
    {
        public int FieldOfficerId { get; set; }
        public int FarmId { get; set; }
        public int? BatchId { get; set; }
        public DateTime VisitDate { get; set; } = DateTime.Today;
        public string VisitPurpose { get; set; } = "Routine Check";
        public string VisitStatus { get; set; } = "Pending";
        public decimal? CheckInLatitude { get; set; }
        public decimal? CheckInLongitude { get; set; }
        public decimal? GpsAccuracyMeters { get; set; }
        public string? OverallRemark { get; set; }
    }
}