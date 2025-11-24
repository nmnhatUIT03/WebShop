using System.ComponentModel.DataAnnotations;

namespace WebShop.ModelViews
{
    public class MuaHangVM
    {
        [Key]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Họ và Tên")]
        [MinLength(2, ErrorMessage = "Họ và tên phải có ít nhất 2 ký tự")]
        public string FullName { get; set; }

        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [MinLength(2, ErrorMessage = "Số điện thoại phải có ít nhất 2 ký tự")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Địa chỉ nhận hàng")]
        [MinLength(2, ErrorMessage = "Địa chỉ phải có ít nhất 2 ký tự")]
        public string Address { get; set; }

        // ✅ Giữ lại cho tương thích với code cũ
        public int TinhThanh { get; set; }

        // ✅ Thêm property mới cho demo (text input)
        [Required(ErrorMessage = "Vui lòng nhập Tỉnh/Thành")]
        [MinLength(2, ErrorMessage = "Tỉnh/Thành phải có ít nhất 2 ký tự")]
        public string TinhThanhText { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Quận/Huyện")]
        [MinLength(2, ErrorMessage = "Quận/Huyện phải có ít nhất 2 ký tự")]
        public string QuanHuyen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Phường/Xã")]
        [MinLength(2, ErrorMessage = "Phường/Xã phải có ít nhất 2 ký tự")]
        public string PhuongXa { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public int? PaymentID { get; set; }

        public string Note { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn sản phẩm")]
        public string SelectedProductDetailIds { get; set; }
        public int? VoucherId { get; set; }
        public int? PromotionId { get; set; }
    }
}