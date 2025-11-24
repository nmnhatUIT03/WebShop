using System;
using System.Collections.Generic;

namespace WebShop.Models.ViewModels
{
    public class UserPromotionDetailsViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public List<PromotionItem> UsedPromotions { get; set; }
        public List<PromotionItem> UnusedPromotions { get; set; }
    }

    public class PromotionItem
    {
        public int UserPromotionId { get; set; }
        public string Name { get; set; }
        public DateTime? UsedDate { get; set; }
    }
}