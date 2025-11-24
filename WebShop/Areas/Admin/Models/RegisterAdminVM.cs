using System.ComponentModel.DataAnnotations;

namespace WebShop.Areas.Admin.Models
{
    public class RegisterAdminVM
    {
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Họ và tên là bắt buộc")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string Phone { get; set; }

        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 100 ký tự")]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Quyền truy cập là bắt buộc")]
        public int? RoleId { get; set; }

        [Required(ErrorMessage = "Trạng thái là bắt buộc")]
        public bool Active { get; set; }
    }
}