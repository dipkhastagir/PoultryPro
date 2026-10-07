using System.ComponentModel.DataAnnotations;

namespace PoultryPro.Models.ViewModels
{
    public class BatchCreateVm
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a farm.")]
        public int FarmId { get; set; }

        [Required, StringLength(20)]
        public string BatchCode { get; set; } = "";

        [Required, StringLength(100)]
        public string Breed { get; set; } = "Cobb 500";

        [Required, StringLength(30)]
        public string BatchType { get; set; } = "Broiler";

        [Range(1, 1_000_000, ErrorMessage = "Initial count must be at least 1.")]
        public int InitialCount { get; set; }

        public DateTime PlacementDate { get; set; } = DateTime.Today;
        public DateTime? ExpectedHarvestDate { get; set; }
        public string HealthStatus { get; set; } = "Healthy";

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(20)]
        public string? ShedNo { get; set; }

        [StringLength(150)]
        public string? HatcheryName { get; set; }

        [Range(0, 10_000)]
        public decimal ChickCostPerBird { get; set; }

        /// <summary>When true, the selected vaccine programme is applied right after the batch is created.</summary>
        public bool ApplyVaccineProgramme { get; set; } = true;
        public int? VaccineTemplateId { get; set; }

        /// <summary>When true, a 'Purchase' finance transaction is posted for the day-old chicks.</summary>
        public bool PostChickPurchase { get; set; } = true;
    }
}
