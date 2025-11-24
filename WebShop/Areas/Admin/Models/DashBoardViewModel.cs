using System;
using System.Collections.Generic;

namespace WebShop.Areas.Admin.Models
{
    public class DashBoardViewModel
    {
        public string tongsp { get; set; }
        public string tongdh { get; set; }
        public string tongdhchuaduyet { get; set; }
        public string tongnguoidung { get; set; }
        public List<RevenueByWeek> RevenueByWeek { get; set; } // Dữ liệu doanh thu theo tuần
        public int SelectedMonth { get; set; } // Tháng được chọn
        public DateTime? FromDate { get; set; } // Ngày bắt đầu
        public DateTime? ToDate { get; set; } // Ngày kết thúc
    }

    public class RevenueByWeek
    {
        public string Week { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}