using System;

namespace WebShop.Models
{
    public class RewardHistory
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string RewardName { get; set; }
        public DateTime RedeemedAt { get; set; }
        public int PointsUsed { get; set; }
        public bool IsConfirmed { get; set; } // Trạng thái xác nhận giao quà

        public virtual Customer Customer { get; set; }
    }

}
