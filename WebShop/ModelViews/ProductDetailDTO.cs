namespace WebShop.ModelViews
{
    public class ProductDetailDTO
    {
        public int ProductDetailId { get; set; }
        public int ProductId { get; set; }
        public int? SizeId { get; set; }
        public string SizeName { get; set; }
        public int? ColorId { get; set; }
        public string ColorName { get; set; }
        public int? Stock { get; set; }
        public bool ProductActive { get; set; }
        public bool ProductDetailActive { get; set; }
        public string ProductName { get; set; }
        public int? Price { get; set; }
        public string Thumb { get; set; }
    }
}