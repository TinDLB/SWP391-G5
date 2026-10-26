using System.ComponentModel.DataAnnotations;
using SWP391_G5.Models.Entities;

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

        [Display(Name = "Địa chỉ mặc định")]
        [StringLength(255, ErrorMessage = "Địa chỉ không vượt quá 255 ký tự.")]
        public string? Address { get; set; }

        public string Role { get; set; } = "Customer";

        public int LoyaltyPoints { get; set; } = 0;

        public ChangePasswordViewModel? ChangePassword { get; set; } = new ChangePasswordViewModel();

        public List<Address> Addresses { get; set; } = new List<Address>();

        public AddressInputModel NewAddress { get; set; } = new AddressInputModel();

        public string ActiveTab { get; set; } = "info";
    }

    public class AddressInputModel
    {
        public int AddressId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận.")]
        [StringLength(100, ErrorMessage = "Tên người nhận không vượt quá 100 ký tự.")]
        [Display(Name = "Họ tên người nhận")]
        public string ReceiverName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại người nhận.")]
        [RegularExpression(@"^(0[3|5|7|8|9])[0-9]{8}$", ErrorMessage = "Số điện thoại không hợp lệ (10 chữ số).")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ nhận hàng.")]
        [StringLength(255, ErrorMessage = "Địa chỉ không vượt quá 255 ký tự.")]
        [Display(Name = "Địa chỉ chi tiết")]
        public string AddressLine { get; set; } = string.Empty;

        [Display(Name = "Đặt làm địa chỉ mặc định")]
        public bool IsDefault { get; set; } = false;
    }
}
