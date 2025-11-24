using System;
using System.Collections.Generic;

#nullable disable

namespace WebShop.Models
{
    public partial class ProductDetail
    {
        public ProductDetail()
        {
            OrderDetails = new HashSet<OrderDetail>(); // Khởi tạo tập hợp
        }

        public int ProductDetailId { get; set; }
        public int ProductId { get; set; }
        public int? SizeId { get; set; }
        public int? ColorId { get; set; }
        public int? Stock { get; set; }
        public bool Active { get; set; } // Thêm thuộc tính Active
        public virtual Product Product { get; set; }
        public virtual Size Size { get; set; }
        public virtual Color Color { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; } // Thêm thuộc tính này
    }
}