using System;


namespace WebShop.Models
{
    public class CheckInHistory
    {
        public int CheckInHistoryId { get; set; }
        public int CustomerId { get; set; }
        public DateTime CheckInDate { get; set; }
        public int PointsEarned { get; set; }

        public virtual Customer Customer { get; set; }
    }
}
