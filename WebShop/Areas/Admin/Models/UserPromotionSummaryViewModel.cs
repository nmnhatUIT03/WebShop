namespace WebShop.Models.ViewModels
{
    public class UserPromotionSummaryViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public int UsedCount { get; set; }
        public int UnusedCount { get; set; }
    }
}