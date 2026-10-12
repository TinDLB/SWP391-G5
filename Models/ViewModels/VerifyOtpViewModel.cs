using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels
{
    public class VerifyOtpViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm đúng 6 chữ số.")]
        [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Mã OTP chỉ bao gồm các chữ số.")]
        [Display(Name = "Mã xác thực OTP")]
        public string OtpCode { get; set; } = string.Empty;

        public string Purpose { get; set; } = "Register"; // "Register" hoặc "ForgotPassword"
        public string? ReturnUrl { get; set; }
    }
}
