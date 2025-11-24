using System.Collections.Generic;
using WebShop.Models;

namespace WebShop.ModelViews
{
    public class HomeViewVM
    {
        public List<ProductHomeVM> Products { get; set; } // Danh sách sản phẩm theo danh mục
        public List<TinTuc> TinTucs { get; set; } // Danh sách tin tức
        public List<ProductHomeVM> TopProducts { get; set; } // Danh sách 9 sản phẩm nổi bật

    }
}