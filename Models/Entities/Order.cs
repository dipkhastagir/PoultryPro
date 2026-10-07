namespace PoultryPro.Models.Entities
{
    public class Order
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = "";
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string? CustomerEmail { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerType { get; set; }
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string? FarmLocation { get; set; }
        public int? BatchId { get; set; }
        public string? BatchCode { get; set; }
        public int? BirdCount { get; set; }
        public string Product { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "kg";
        public string Grade { get; set; } = "A";
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public string Status { get; set; } = "Pending";
        public string PaymentStatus { get; set; } = "Unpaid";
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Outstanding amount still owed by the customer.</summary>
        public decimal BalanceDue => Math.Max(0, TotalAmount - PaidAmount);
        public bool IsOpen => Status != "Delivered" && Status != "Cancelled";
        public bool IsLate => IsOpen && DeliveryDate.Date < DateTime.Today;
        /// <summary>Average live weight per bird when a bird count is known.</summary>
        public decimal? AvgKgPerBird => BirdCount is > 0 && Unit == "kg" ? Quantity / BirdCount.Value : null;
    }
}
