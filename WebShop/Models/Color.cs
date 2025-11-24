using System;
using System.Collections.Generic;

#nullable disable

namespace WebShop.Models
{
    public partial class Color
    {
        public Color()
        {
            ProductDetails = new HashSet<ProductDetail>();
        }

        public int ColorId { get; set; }
        public string ColorName { get; set; }

        public virtual ICollection<ProductDetail> ProductDetails { get; set; }
    }
}