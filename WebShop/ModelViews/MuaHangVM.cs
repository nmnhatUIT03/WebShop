using System.ComponentModel.DataAnnotations;

namespace WebShop.ModelViews
{
    public class MuaHangVM
    {
        [Key]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Họ và Tên")]
        public string FullName { get; set; }

        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Số điện thoại phải từ 10-11 chữ số")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Địa chỉ nhận hàng")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành")]
        public int TinhThanh { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Quận/Huyện")]
        public string QuanHuyen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Phường/Xã")]
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