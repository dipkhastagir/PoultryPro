namespace PoultryPro.Models.Entities
{
    public class FieldOfficerFarmAssignment
    {
        public int Id { get; set; }
        public int FieldOfficerId { get; set; }
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string FarmLocation { get; set; } = "";
        public DateTime AssignedDate { get; set; }
        public bool IsActive { get; set; } = true;
    }
}