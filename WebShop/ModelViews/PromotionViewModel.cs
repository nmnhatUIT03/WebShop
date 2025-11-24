using System;

namespace WebShop.ModelViews
{
    public class PromotionViewModel
    {
        public int PromotionId { get; set; }
        public string PromotionName { get; set; }
        public decimal Discount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public int UsedCountByUser { get; set; }
        public int DefaultUserMaxUsage { get; set; }
        public bool IsApplicable { get; set; }
    }
}
