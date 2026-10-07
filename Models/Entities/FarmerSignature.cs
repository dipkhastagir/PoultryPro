namespace PoultryPro.Models.Entities
{
    public class FarmerSignature
    {
        public int Id { get; set; }
        public int VisitId { get; set; }
        public string FarmerName { get; set; } = "";
        public string? FarmerPhone { get; set; }
        public string SignatureFilePath { get; set; } = "";
        public bool IsVerifiedByFarmer { get; set; }
        public DateTime SignedAt { get; set; }
        public string SyncStatus { get; set; } = "Pending";
    }
}