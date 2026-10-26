using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels
{
    public class ProfileViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(100, ErrorMessage = "{0} phải có độ dài từ {2} đến {1} ký tự.", MinimumLength = 2)]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Địa chỉ Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^(0[3|5|7|8|9])[0-9]{8}$", ErrorMessage = "Số điện thoại không hợp lệ (10 số, bắt đầu bằng 03, 05, 07, 08, 09).")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Địa chỉ nhận hàng")]
        [StringLength(255, ErrorMessage = "Địa chỉ không vượt quá 255 ký tự.")]
        public string? Address { get; set; }

        public string Role { get; set; } = "Customer";

        public int LoyaltyPoints { get; set; } = 0;

        public ChangePasswordViewModel? ChangePassword { get; set; } = new ChangePasswordViewModel();

        public string ActiveTab { get; set; } = "info";
    }
}
