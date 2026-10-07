namespace PoultryPro.Models.ViewModels
{
    public class FieldOfficerEditVm
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public string? NewPassword { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Zone { get; set; }
        public string Designation { get; set; } = "Field Officer";
        public bool IsActive { get; set; } = true;
        public List<int> AssignedFarmIds { get; set; } = new();
    }
}