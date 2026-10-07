namespace PoultryPro.Models.Entities
{
    public class FarmVisit
    {
        public string? OverallRemark { get; set; }
        public int Id { get; set; }
        public int FieldOfficerId { get; set; }
        public string FieldOfficerName { get; set; } = "";
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string FarmLocation { get; set; } = "";
        public int? BatchId { get; set; }
        public string? BatchCode { get; set; }
        public int? AgeInDays { get; set; }
        public DateTime VisitDate { get; set; }
        public DateTime? CheckInTime { get; set; }
        public decimal? CheckInLatitude { get; set; }
        public decimal? CheckInLongitude { get; set; }
        public decimal? GpsAccuracyMeters { get; set; }
        public string VisitStatus { get; set; } = "Pending";
        public string SyncStatus { get; set; } = "Pending";
        public DateTime? SyncedAt { get; set; }
        public string VisitPurpose { get; set; } = "Routine Check";
        public int DailyRecordCount { get; set; }
        public int PhotoCount { get; set; }
        public int SignatureCount { get; set; }
        public string? Breed { get; set; }
        public DateTime? CheckOutTime { get; set; }

        // ── geofence (vw_FarmVisitDetails) ──
        public decimal? FarmLatitude { get; set; }
        public decimal? FarmLongitude { get; set; }

        /// <summary>Geofence check + Visit Trust Score; filled by VisitTrust.Apply (not a database column).</summary>
        public PoultryPro.Infrastructure.VisitTrustResult? Trust { get; set; }

    }
}