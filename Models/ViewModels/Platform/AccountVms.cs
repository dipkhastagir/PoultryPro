using System.ComponentModel.DataAnnotations;

namespace PoultryPro.Models.ViewModels
{
    public class LoginVm
    {
        [Required(ErrorMessage = "Username is required")] public string Username { get; set; } = "";
        [Required(ErrorMessage = "Password is required"), DataType(DataType.Password)] public string Password { get; set; } = "";
        public bool RememberMe { get; set; } = true;
        public string? ReturnUrl { get; set; }
    }

    public class ChangePasswordVm
    {
        [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = "";
        [Required, MinLength(6, ErrorMessage = "Use at least 6 characters"), DataType(DataType.Password)] public string NewPassword { get; set; } = "";
        [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match"), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = "";
    }

    public class ProfileVm
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        [Required] public string FullName { get; set; } = "";
        [EmailAddress] public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
