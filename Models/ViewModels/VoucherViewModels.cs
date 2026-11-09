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

    [Required(ErrorMessage = "Mã voucher không được để trống.")]
    [StringLength(30, ErrorMessage = "Mã voucher không được vượt quá 30 ký tự.")]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Mã chỉ được chứa chữ, số, dấu gạch ngang hoặc gạch dưới.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loại giảm giá.")]
    public string DiscountType { get; set; } = "Percent";

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "Giá trị giảm phải lớn hơn 0.")]
    public decimal DiscountValue { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Giá trị đơn tối thiểu không hợp lệ.")]
    public decimal MinOrderValue { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "Mức giảm tối đa phải lớn hớn 0.")]
    public decimal? MaxDiscount { get; set; }

    [DataType(DataType.Date)]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [DataType(DataType.Date)]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

    [Range(1, int.MaxValue, ErrorMessage = "Giới hạn lượt dùng phải lớn hơn 0.")]
    public int? UsageLimit { get; set; }

    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate > EndDate)
            yield return new ValidationResult("Ngày bắt đầu không được sau ngày kết thúc.", new[] { nameof(StartDate), nameof(EndDate) });

        if (DiscountType == "Percent" && DiscountValue > 100)
            yield return new ValidationResult("Voucher phần trăm không được vượt quá 100%.", new[] { nameof(DiscountValue) });

        if (DiscountType == "Fixed" && MaxDiscount.HasValue)
            yield return new ValidationResult("Mức giảm tối đa chỉ áp dụng cho voucher phần trăm.", new[] { nameof(MaxDiscount) });

        if (DiscountType is not ("Percent" or "Fixed"))
            yield return new ValidationResult("Loại giảm giá không hợp lệ.", new[] { nameof(DiscountType) });
    }
}
