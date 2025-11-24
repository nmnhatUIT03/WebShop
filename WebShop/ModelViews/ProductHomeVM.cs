using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Models;

namespace WebShop.ModelViews
{
    public class ProductHomeVM
    {
        public int ProductId { get; set; } // ID sản phẩm
        public string ProductName { get; set; } // Tên sản phẩm
        public string Thumb { get; set; } // Đường dẫn ảnh đại diện
        public int? Price { get; set; } // Giá gốc
        public decimal? DiscountPrice { get; set; } // Giá sau khi giảm (nếu có)
        public int UnitsInStock { get; set; } // Số lượng tồn kho
        public Category category { get; set; } // Danh mục sản phẩm
        public List<Product> lsProducts { get; set; } // Danh sách sản phẩm trong danh mục
        public string Description { get; set; } // Mô tả sản phẩm
        public string Tags { get; set; } // Từ khóa
        public string Alias { get; set; }
    }
}