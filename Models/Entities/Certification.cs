namespace PoultryPro.Models.Entities
{
    public class Certification
    {
        public int Id { get; set; }
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string CertName { get; set; } = "";
        public DateTime IssuedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Status { get; set; } = "Valid";
        public string? IssuingAuthority { get; set; }
        public string? CertificateNo { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Status derived from the expiry date (Expired / Expiring Soon within 30 days / stored status).</summary>
        public string EffectiveStatus =>
            ExpiryDate.Date < DateTime.Today ? "Expired"
            : ExpiryDate.Date <= DateTime.Today.AddDays(30) ? "Expiring Soon"
            : Status;

        public int DaysToExpiry => (int)(ExpiryDate.Date - DateTime.Today).TotalDays;
    }
}
