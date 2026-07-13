using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class UserIndexViewModel
{
    public IReadOnlyList<DTOs.UserDto> Users { get; set; } = Array.Empty<DTOs.UserDto>();
}

public class UserCreateViewModel
{
    [Required, Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Email (used for login)")]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

public class UserDetailsViewModel
{
    public DTOs.UserDto? User { get; set; }
    public string? TemporaryPassword { get; set; }
    public bool EmailSent { get; set; }
    public IReadOnlyList<DTOs.SubjectDto> AssignedSubjects { get; set; } = Array.Empty<DTOs.SubjectDto>();
}

public class UserEditViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    [MinLength(8), DataType(DataType.Password), Display(Name = "New Password")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must include at least one uppercase letter, one lowercase letter, and one digit.")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password), Display(Name = "Confirm New Password")]
    public string? ConfirmNewPassword { get; set; }
}

public class UserProfileViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }

    [DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")]
    public string? CurrentPassword { get; set; }

    [MinLength(8), DataType(DataType.Password), Display(Name = "Mật khẩu mới")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must include at least one uppercase letter, one lowercase letter, and one digit.")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu mới")]
    public string? ConfirmNewPassword { get; set; }
}
