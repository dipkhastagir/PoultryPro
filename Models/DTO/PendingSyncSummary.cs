namespace PoultryPro.Models.DTO
{
    public class PendingSyncSummary
    {
        public int FieldOfficerId { get; set; }
        public string FullName { get; set; } = "";
        public int PendingVisits { get; set; }
        public int PendingDailyRecords { get; set; }
        public int PendingPhotos { get; set; }
        public int PendingSignatures { get; set; }
        public int TotalPending => PendingVisits + PendingDailyRecords + PendingPhotos + PendingSignatures;
    }
}