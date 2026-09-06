using System.ComponentModel.DataAnnotations;

namespace FutureTech.StudentManagement.Models.ViewModels;

public class LoginViewModel
{
    public string? ReturnUrl { get; set; }

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }
}

public class ExternalLoginViewModel
{
    public string Provider { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/";
}