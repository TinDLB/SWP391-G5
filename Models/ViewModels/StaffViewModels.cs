using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels;

public class StaffListItemViewModel
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StaffEditViewModel : IValidatableObject
{
    public int UserId { get; set; }

    [Required(ErrorMessage = "Email không ???c ?? tr?ng.")]
    [EmailAddress(ErrorMessage = "Email không h?p l?.")]
    [StringLength(150, ErrorMessage = "Email không ???c v??t quá 150 ký t?.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "H? tên không ???c ?? tr?ng.")]
    [StringLength(100, ErrorMessage = "H? tên không ???c v??t quá 100 ký t?.")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "S? ?i?n tho?i không h?p l?.")]
    [StringLength(20, ErrorMessage = "S? ?i?n tho?i không ???c v??t quá 20 ký t?.")]
    public string? Phone { get; set; }

    [StringLength(255, ErrorMessage = "??a ch? không ???c v??t quá 255 ký t?.")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Vui lòng ch?n vai trò.")]
    public string Role { get; set; } = "Cashier";

    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "M?t kh?u ph?i có ít nh?t 6 ký t?.")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "M?t kh?u xác nh?n không kh?p.")]
    public string? ConfirmPassword { get; set; }

    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (UserId == 0 && string.IsNullOrWhiteSpace(Password))
            yield return new ValidationResult("M?t kh?u là b?t bu?c khi t?o nhân viên.", new[] { nameof(Password) });

        if (Role is not ("Cashier" or "Baker"))
            yield return new ValidationResult("Vai trò nhân viên không h?p l?.", new[] { nameof(Role) });
    }
}
