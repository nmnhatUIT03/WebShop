using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebShop.Models
{
    public partial class Promotion
    {
        public Promotion()
        {
            PromotionProducts = new HashSet<PromotionProduct>();
            UserPromotions = new HashSet<UserPromotion>();
            Orders = new HashSet<Order>();
        }

        public int PromotionId { get; set; }
        public string PromotionName { get; set; }
        [Range(0, 50, ErrorMessage = "Giảm giá phải nằm trong khoảng từ 0% đến 50%.")]
        public decimal Discount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public int DefaultUserMaxUsage { get; set; } // Thêm thuộc tính này

        public virtual ICollection<PromotionProduct> PromotionProducts { get; set; }
        public virtual ICollection<UserPromotion> UserPromotions { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
    }
}