using System;
using System.Collections.Generic;

namespace WebShop.Models
{
    public partial class Voucher
    {
        public Voucher()
        {
            UserPromotions = new HashSet<UserPromotion>();
            Orders = new HashSet<Order>();
        }
        public int VoucherId { get; set; }
        public string VoucherCode { get; set; }
        public string DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public int MaxUsage { get; set; }
        public int UsedCount { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? MinOrderValue { get; set; }
        public int DefaultUserMaxUsage { get; set; }

        public virtual ICollection<UserPromotion> UserPromotions { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
    }
}