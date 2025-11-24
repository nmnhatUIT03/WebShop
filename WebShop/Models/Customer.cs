using System;
using System.Collections.Generic;

#nullable disable

namespace WebShop.Models
{
    public partial class Customer
    {
        public Customer()
        {
            Comments = new HashSet<Comment>();
            Orders = new HashSet<Order>();
            UserPromotions = new HashSet<UserPromotion>();
            CheckInHistory = new HashSet<CheckInHistory>(); // Thêm thuộc tính này
        }

        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public DateTime? Birthday { get; set; }
        public string Avatar { get; set; }
        public string Address { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public int? LocationId { get; set; }
        public int? District { get; set; }
        public int? Ward { get; set; }
        public DateTime? CreateDate { get; set; }
        public string Password { get; set; }
        public string Salt { get; set; }
        public DateTime? LastLogin { get; set; }
        public bool? Active { get; set; }
        public int Points { get; set; } // Đã thêm từ trước cho tích điểm
        public DateTime? LastCheckInDate { get; set; } // Ngày điểm danh gần nhất
        public int CheckInCount { get; set; } // Tổng số ngày đã điểm danh

        public virtual Location Location { get; set; }
        public virtual ICollection<Comment> Comments { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
        public virtual ICollection<UserPromotion> UserPromotions { get; set; }
        public virtual ICollection<CheckInHistory> CheckInHistory { get; set; } // Thêm mối quan hệ với CheckInHistory
        public virtual ICollection<RewardHistory> RewardHistories { get; set; } // Thêm dòng này
    }
}