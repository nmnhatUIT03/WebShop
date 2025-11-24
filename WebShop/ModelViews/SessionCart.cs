using System.Collections.Generic;

namespace WebShop.ModelViews
{
    public class SessionCart
    {

        public string CustomerId { get; set; }
        public string CartToken { get; set; }
        public List<CartItem> Items { get; set; } = new List<CartItem>();
        public string Token { get; internal set; }
    }
}
