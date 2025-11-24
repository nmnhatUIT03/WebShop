using System;

namespace WebShop.ModelViews
{
    public class MuaHangSuccessVM
    {
        public int OrderId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string PhuongXa { get; set; }
        public string QuanHuyen { get; set; }
        public string TinhThanh { get; set; }
        public int TotalMoney { get; set; }
    }
}