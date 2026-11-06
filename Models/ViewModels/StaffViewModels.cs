using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels;

public class StaffListItemViewModel
{
    // D? li?u hi?n th? danh sách nhân viên trên giao di?n, bao g?m các thông tin c? b?n nh? UserId, Email, FullName, Role, IsActive và CreatedAt.
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StaffEditViewModel : IValidatableObject
{
    public int UserId { get; set; }

    // Email ???c nh?n t? form và dùng làm ??nh danh ??ng nh?p duy nh?t.
    [Required(ErrorMessage = "Email không ???c ?? tr?ng.")]
    [EmailAddress(ErrorMessage = "Email không h?p l?.")]
    [StringLength(150, ErrorMessage = "Email không ???c v??t quá 150 ký t?.")]
    public string Email { get; set; } = string.Empty;

    // Thông tin tên và s? ?i?n tho?i ???c nh?n t? form nhân viên.
    [Required(ErrorMessage = "H? tên không ???c ?? tr?ng.")]
    [StringLength(100, ErrorMessage = "H? tên không ???c v??t quá 100 ký t?.")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "S? ?i?n tho?i không h?p l?.")]
    [StringLength(20, ErrorMessage = "S? ?i?n tho?i không ???c v??t quá 20 ký t?.")]
    public string? Phone { get; set; }

    [StringLength(20, ErrorMessage = "Gi?i tính không h?p l?.")]
    public string? Gender { get; set; }

    [StringLength(255, ErrorMessage = "??a ch? không ???c v??t quá 255 ký t?.")]
    public string? Address { get; set; }

    // Ch? cho phép t?o tài kho?n nhân viên Cashier ho?c Baker.
    [Required(ErrorMessage = "Vui lòng ch?n vai trò.")]
    public string Role { get; set; } = "Cashier";

    // Khi t?o m?i b?t bu?c có m?t kh?u; khi s?a có th? ?? tr?ng ?? gi? m?t kh?u c?.
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
