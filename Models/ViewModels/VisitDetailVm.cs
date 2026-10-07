

using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    public class VisitDetailVm
    {
        public FarmVisit Visit { get; set; } = new();
        public List<DailyFarmRecord> DailyRecords { get; set; } = new();
        public List<VisitPhoto> Photos { get; set; } = new();
        public FarmerSignature? Signature { get; set; }
        public Batch? Batch { get; set; }
    }
}