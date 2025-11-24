using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace WebShop.Models
{
    public partial class Supplier
    {
        public Supplier()
        {
            Products = new HashSet<Product>();
        }

        public int SupplierId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        [NotMapped]
        public int? ProductQuantity { get; set; }

        public virtual ICollection<Product> Products { get; set; }
    }
}