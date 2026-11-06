using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels;

public class VoucherListItemViewModel
{
    public int VoucherId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class VoucherEditViewModel : IValidatableObject
{
    public int VoucherId { get; set; }

    [Required(ErrorMessage = "Mã voucher không ???c ?? tr?ng.")]
    [StringLength(30, ErrorMessage = "Mã voucher không ???c v??t quá 30 ký t?.")]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Mã ch? ???c ch?a ch?, s?, d?u g?ch ngang ho?c g?ch d??i.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng ch?n lo?i gi?m giá.")]
    public string DiscountType { get; set; } = "Percent";

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "Giá tr? gi?m ph?i l?n h?n 0.")]
    public decimal DiscountValue { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Giá tr? ??n t?i thi?u không h?p l?.")]
    public decimal MinOrderValue { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "M?c gi?m t?i ?a ph?i l?n h?n 0.")]
    public decimal? MaxDiscount { get; set; }

    [DataType(DataType.Date)]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [DataType(DataType.Date)]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

    [Range(1, int.MaxValue, ErrorMessage = "Gi?i h?n l??t dùng ph?i l?n h?n 0.")]
    public int? UsageLimit { get; set; }

    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate > EndDate)
            yield return new ValidationResult("Ngày b?t ??u không ???c sau ngày k?t thúc.", new[] { nameof(StartDate), nameof(EndDate) });

        if (DiscountType == "Percent" && DiscountValue > 100)
            yield return new ValidationResult("Voucher ph?n tr?m không ???c v??t quá 100%.", new[] { nameof(DiscountValue) });

        if (DiscountType == "Fixed" && MaxDiscount.HasValue)
            yield return new ValidationResult("M?c gi?m t?i ?a ch? áp d?ng cho voucher ph?n tr?m.", new[] { nameof(MaxDiscount) });

        if (DiscountType is not ("Percent" or "Fixed"))
            yield return new ValidationResult("Lo?i gi?m giá không h?p l?.", new[] { nameof(DiscountType) });
    }
}
