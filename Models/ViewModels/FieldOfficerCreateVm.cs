namespace PoultryPro.Models.ViewModels
{
    public class FieldOfficerCreateVm
    {
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Zone { get; set; }
        public string Designation { get; set; } = "Field Officer";
        public List<int> AssignedFarmIds { get; set; } = new();
    }
}