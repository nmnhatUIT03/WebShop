using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PagedList.Core;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Models;
using WebShop.ModelViews;

namespace WebShop.Controllers
{
    public class ProductController : Controller
    {
        private readonly webshopContext _context;
        private const int IndexPageSize = 12; // 12 s?n ph?m/trang cho Index
        private const int ListPageSize = 20;  // 20 s?n ph?m/trang cho List

        public ProductController(webshopContext context)
        {
            _context = context;
        }

        [Route("shop.html", Name = "ShopProduct")]
        public async Task<IActionResult> Index(string currentFilter, string searchString, int? pageNumber, string sortOrder, int? categoryId, int? supplierId, string priceRange, bool? isDiscounted, bool? isBestSeller)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["CategoryId"] = categoryId;
            ViewData["SupplierId"] = supplierId;
            ViewData["PriceRange"] = priceRange;
            ViewData["IsDiscounted"] = isDiscounted;
            ViewData["IsBestSeller"] = isBestSeller;

            if (searchString != null)
            {
                pageNumber = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            // L?y d? li?u cho sidebar
            ViewBag.Categories = await _context.Categories.Where(c => c.Published).ToListAsync();
            ViewBag.Suppliers = await _context.Suppliers.ToListAsync();

            var products = _context.Products
                .AsNoTracking()
                .Where(p => p.Active == true && p.UnitsInStock > 0);

            // T�m ki?m
            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(s => s.ProductName.Contains(searchString) || s.Price.ToString().Contains(searchString));
            }

            // B? l?c danh m?c
            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CatId == categoryId.Value);
            }

            // B? l?c nh� cung c?p
            if (supplierId.HasValue)
            {
                products = products.Where(p => p.SupplierId == supplierId.Value);
            }

            // B? l?c gi�
            if (!string.IsNullOrEmpty(priceRange))
            {
                try
                {
                    var prices = priceRange.Split('-').Select(x => decimal.Parse(x)).ToArray();
                    if (prices.Length == 2)
                    {
                        products = products.Where(p => p.Discount.HasValue
                            ? (p.Price * (1 - (p.Discount.Value / 100m))) >= prices[0] && (p.Price * (1 - (p.Discount.Value / 100m))) <= prices[1]
                            : p.Price >= prices[0] && p.Price <= prices[1]);
                    }
                }
                catch (FormatException)
                {
                    // B? qua n?u priceRange kh�ng h?p l?
                }
            }

            // B? l?c s?n ph?m gi?m gi�
            if (isDiscounted.HasValue && isDiscounted.Value)
            {
                products = products.Where(p => p.Discount.HasValue && p.Discount.Value > 0);
            }

            // B? l?c s?n ph?m n?i b?t
            if (isBestSeller.HasValue && isBestSeller.Value)
            {
                products = products.Where(p => p.BestSellers.HasValue && p.BestSellers.Value);
            }

            // S?p x?p
            switch (sortOrder)
            {
                case "low-to-high":
                    products = products.OrderBy(p => p.Discount.HasValue ? p.Price * (1 - (p.Discount.Value / 100m)) : p.Price);
                    break;
                case "high-to-low":
                    products = products.OrderByDescending(p => p.Discount.HasValue ? p.Price * (1 - (p.Discount.Value / 100m)) : p.Price);
                    break;
                default:
                    products = products.OrderByDescending(p => p.BestSellers.HasValue && p.BestSellers.Value);
                    break;
            }

            var pagedProducts = new PagedList<Product>(products, pageNumber ?? 1, IndexPageSize);
            return View(pagedProducts);
        }

        [Route("/{Alias}", Name = "ListProduct")]
        public IActionResult List(string Alias, int? pageNumber)
        {
            try
            {
                var category = _context.Categories.AsNoTracking().FirstOrDefault(x => x.Alias == Alias);
                if (category == null)
                {
                    return RedirectToAction("Index", "Home");
                }

                var products = _context.Products
                    .AsNoTracking()
                    .Where(x => x.CatId == category.CatId && x.Active == true)
                    .OrderByDescending(x => x.DateCreated);

                var pagedProducts = new PagedList<Product>(products, pageNumber ?? 1, ListPageSize);
                ViewBag.CurrentPage = pageNumber;
                ViewBag.CurrentCat = category;

                return View(pagedProducts);
            }
            catch
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [Route("/{Alias}-{id}.html", Name = "ProductDetails")]
        public IActionResult Details(int id, string Alias)
        {
            try
            {
                Console.WriteLine($"Details called with id={id}, alias={Alias}");
                var product = _context.Products
                    .Include(x => x.Cat)
                    .FirstOrDefault(x => x.ProductId == id);
                if (product == null)
                {
                    Console.WriteLine($"Product with id={id} not found");
                    return NotFound();
                }

                // Ki?m tra Alias n?u c?n thi?t
                if (!string.IsNullOrEmpty(Alias) && product.Alias != Alias)
                {
                    Console.WriteLine($"Alias mismatch: expected={product.Alias}, received={Alias}");
                    return RedirectToRoute("ProductDetails", new { Alias = product.Alias, id = product.ProductId });
                }

                var relatedProducts = _context.Products
                    .AsNoTracking()
                    .Where(x => x.CatId == product.CatId && x.ProductId != id && x.Active == true)
                    .OrderByDescending(x => x.DateCreated)
                    .Take(4)
                    .ToList();

                // Load ProductDetails v?i Size v� Color cho popup
                var productDetails = _context.ProductDetails
                    .Include(pd => pd.Size)
                    .Include(pd => pd.Color)
                    .AsNoTracking()
                    .Where(pd => pd.ProductId == id && pd.Active)
                    .ToList();
                
                ViewBag.ProductDetails = productDetails;

                var productVM = new ProductHomeVM
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    Thumb = !string.IsNullOrEmpty(product.Thumb) ? product.Thumb : "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7",
                    Price = product.Price,
                    DiscountPrice = product.Discount.HasValue ? product.Price * (1 - (product.Discount.Value / 100m)) : product.Price,
                    UnitsInStock = product.UnitsInStock ?? 0,
                    Description = product.Description,
                    Tags = product.Tags,
                    category = product.Cat,
                    lsProducts = relatedProducts.Select(p => new Product
                    {
                        ProductId = p.ProductId,
                        ProductName = p.ProductName,
                        Thumb = !string.IsNullOrEmpty(p.Thumb) ? p.Thumb : "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7",
                        Price = p.Price,
                        Discount = p.Discount,
                        Alias = p.Alias
                    }).ToList()
                };

                Console.WriteLine($"Product Thumb: {productVM.Thumb}");
                return View(productVM);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Details: {ex.Message}");
                return NotFound();
            }
        }
    }
}