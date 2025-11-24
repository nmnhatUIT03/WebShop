using System;
using System.Text.Json.Serialization;
using WebShop.Models;

namespace WebShop.ModelViews
{
    public class CartItem
    {
        public Product product { get; set; }
        public ProductDetail productDetail { get; set; }
        public int amount { get; set; }

        [JsonIgnore]
        public double TotalMoney => amount * (product?.Price ?? 0);
        // Thêm thuộc tính ColorName để lưu thông tin màu sắc
        public string ColorName { get; set; }

        // Tùy chọn: Thêm SizeName nếu muốn hiển thị size trực tiếp
        public string SizeName { get; set; }
    }
}