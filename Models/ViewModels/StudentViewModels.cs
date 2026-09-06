using System.ComponentModel.DataAnnotations;
using FutureTech.StudentManagement.Models.Entities;

namespace FutureTech.StudentManagement.Models.ViewModels;

// List/Index ViewModel
public class StudentListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string EnrolmentStatus { get; set; } = string.Empty;
    public string? ProfileImageSasUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StudentListViewModel
{
    public List<StudentListItemViewModel> Students { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// Create ViewModel
public class CreateStudentViewModel
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required")]
    [Phone(ErrorMessage = "Invalid phone number")]
    [Display(Name = "Mobile Number")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Enrolment Status")]
    public EnrolmentStatus EnrolmentStatus { get; set; } = EnrolmentStatus.Active;

    [Display(Name = "Profile Picture")]
    [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png" }, ErrorMessage = "Only JPEG and PNG files are allowed")]
    [MaxFileSize(5 * 1024 * 1024, ErrorMessage = "File size cannot exceed 5MB")]
    public IFormFile? ProfileImage { get; set; }
}

// Edit ViewModel
public class EditStudentViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required")]
    [Phone]
    public string MobileNumber { get; set; } = string.Empty;

    [Required]
    public EnrolmentStatus EnrolmentStatus { get; set; }

    public string? ExistingImageSasUrl { get; set; }

    [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png" })]
    [MaxFileSize(5 * 1024 * 1024)]
    public IFormFile? NewProfileImage { get; set; }
}

// Custom Validation Attributes
public class AllowedExtensionsAttribute : ValidationAttribute
{
    private readonly string[] _extensions;

    public AllowedExtensionsAttribute(string[] extensions)
    {
        _extensions = extensions;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_extensions.Contains(extension))
                return new ValidationResult(ErrorMessage ?? $"Only {string.Join(", ", _extensions)} files are allowed.");
        }
        return ValidationResult.Success;
    }
}

public class MaxFileSizeAttribute : ValidationAttribute
{
    private readonly long _maxFileSize;

    public MaxFileSizeAttribute(long maxFileSize)
    {
        _maxFileSize = maxFileSize;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file && file.Length > _maxFileSize)
        {
            var maxSizeMB = _maxFileSize / (1024 * 1024);
            return new ValidationResult(ErrorMessage ?? $"File size cannot exceed {maxSizeMB}MB.");
        }
        return ValidationResult.Success;
    }
}