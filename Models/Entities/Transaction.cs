namespace PoultryPro.Models.Entities
{
    public class Transaction
    {
        public int Id { get; set; }
        public DateTime TxnDate { get; set; }
        public string TxnType { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public string PaymentStatus { get; set; } = "Paid";
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public int? BatchId { get; set; }
        public string? BatchCode { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Reference { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool IsIncome => Amount >= 0;
    }
}
