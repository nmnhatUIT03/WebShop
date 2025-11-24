using System;
using System.Collections.Generic;

namespace WebShop.Areas.Admin.Models
{

    public class CustomerPointsSummaryViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public int TotalCheckIns { get; set; }
        public int TotalRewardsRedeemed { get; set; }
    }

    public class CustomerPointsDetailsViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public List<CheckInItem> CheckIns { get; set; }
        public List<RewardItem> RedeemedRewards { get; set; }
        public List<RewardItem> UnconfirmedRewards { get; set; }
    }

    public class CheckInItem
    {
        public int CheckInHistoryId { get; set; }
        public DateTime CheckInDate { get; set; }
        public int PointsEarned { get; set; }
    }

    public class RewardItem
    {
        public int Id { get; set; }
        public string RewardName { get; set; }
        public DateTime RedeemedAt { get; set; }
        public int PointsUsed { get; set; }
        public bool IsConfirmed { get; set; }
    }
}