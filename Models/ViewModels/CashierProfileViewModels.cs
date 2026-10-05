using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels
{
    public class CashierProfileViewModel
    {
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;      // chỉ hiển thị

        [Display(Name = "Vai trò")]
        public string Role { get; set; } = string.Empty;       // chỉ hiển thị

        [Display(Name = "Ngày tạo tài khoản")]
        public DateTime CreatedAt { get; set; }                // chỉ hiển thị

        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(100, ErrorMessage = "{0} phải có độ dài từ {2} đến {1} ký tự.", MinimumLength = 2)]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [RegularExpression(@"^(0[3|5|7|8|9])[0-9]{8}$", ErrorMessage = "Số điện thoại không hợp lệ (gồm 10 chữ số, bắt đầu bằng 03, 05, 07, 08, 09).")]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StringLength(100, ErrorMessage = "{0} phải có ít nhất {2} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        [Display(Name = "Xác nhận mật khẩu mới")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}