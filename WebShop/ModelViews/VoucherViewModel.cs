using System;

namespace WebShop.ModelViews
{
    public class VoucherViewModel
    {
        public int VoucherId { get; set; }
        public string VoucherCode { get; set; }
        public decimal DiscountValue { get; set; }
        public string DiscountType { get; set; }
        public decimal? MinOrderValue { get; set; }
        public int MaxUsage { get; set; }
        public int UsedCount { get; set; }
        public DateTime? EndDate { get; set; }
        public int UsedCountByUser { get; set; }
        public int DefaultUserMaxUsage { get; set; }
        public bool IsApplicable { get; set; }
    }
}
