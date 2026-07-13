using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class LoginViewModel
{
    [Required, Display(Name = "Email")]
    public string UserName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class ResendEmailConfirmationViewModel
{
    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
