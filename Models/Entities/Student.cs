namespace FutureTech.StudentManagement.Models.Entities;

public enum EnrolmentStatus
{
    Active,
    Inactive
}

public class Student
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public EnrolmentStatus EnrolmentStatus { get; set; } = EnrolmentStatus.Active;
    public string? ProfileImageBlobName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}