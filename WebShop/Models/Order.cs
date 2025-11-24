using System;
using System.Collections.Generic;

#nullable disable

namespace WebShop.Models
{
    public partial class Order
    {
        public Order()
        {
            OrderDetails = new HashSet<OrderDetail>();
        }

        public int OrderId { get; set; }
        public int? CustomerId { get; set; }
        public string ReceiverName { get; set; }
        public string Address { get; set; }
        public int? LocationId { get; set; }
        public DateTime? OrderDate { get; set; }
        public DateTime? ShipDate { get; set; }
        public int TransactStatusId { get; set; }
        public bool Deleted { get; set; }
        public bool Paid { get; set; }
        public DateTime? PaymentDate { get; set; }
        public decimal? TotalMoney { get; set; }
        public int? PaymentId { get; set; }
        public string Note { get; set; }
        public int? PromotionId { get; set; }
        public int? VoucherId { get; set; }
        public decimal TotalDiscount { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Location Location { get; set; }
        public virtual TransactStatus TransactStatus { get; set; }
        public virtual Promotion Promotion { get; set; }
        public virtual Voucher Voucher { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}