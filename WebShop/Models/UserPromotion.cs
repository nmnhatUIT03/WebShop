using System;

namespace WebShop.Models
{
    public partial class UserPromotion
    {
        public int UserPromotionId { get; set; }

        public int CustomerId { get; set; }

        public int? PromotionId { get; set; }

        public int? VoucherId { get; set; }

        public DateTime UsedDate { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Promotion Promotion { get; set; }
        public virtual Voucher Voucher { get; set; }
    }
}