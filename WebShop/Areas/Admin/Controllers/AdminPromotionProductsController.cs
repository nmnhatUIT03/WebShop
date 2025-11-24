using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Areas.Admin.Models;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminPromotionProductsController : Controller
    {
        private readonly webshopContext _context;
        public INotyfService _notifyService { get; }

        public AdminPromotionProductsController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        // GET: Admin/AdminPromotionProducts
        public async Task<IActionResult> Index(string searchString)
        {
            var promotions = _context.Promotions
                .Include(p => p.PromotionProducts)
                .ThenInclude(pp => pp.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                promotions = promotions.Where(p => p.PromotionName.Contains(searchString));
            }

            var result = await promotions.ToListAsync();
            ViewData["CurrentFilter"] = searchString;
            return View(result);
        }

        // GET: Admin/AdminPromotionProducts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy chương trình khuyến mãi.");
                return NotFound();
            }

            var promotion = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .ThenInclude(pp => pp.Product)
                .FirstOrDefaultAsync(p => p.PromotionId == id);
            if (promotion == null)
            {
                _notifyService.Error("Không tìm thấy chương trình khuyến mãi.");
                return NotFound();
            }

            return View(promotion);
        }

        // GET: Admin/AdminPromotionProducts/Create
        public IActionResult Create()
        {
            var promotions = _context.Promotions
                .Select(p => new PromotionViewModel { PromotionId = p.PromotionId, PromotionName = p.PromotionName })
                .ToList();
            var products = _context.Products
                .Select(p => new ProductViewModel { ProductId = p.ProductId, ProductName = p.ProductName })
                .ToList();

            if (!promotions.Any())
            {
                _notifyService.Warning("Không có chương trình khuyến mãi nào trong hệ thống.");
            }
            if (!products.Any())
            {
                _notifyService.Warning("Không có sản phẩm nào trong hệ thống.");
            }

            ViewBag.Promotions = promotions;
            ViewBag.Products = products;
            return View();
        }

        // POST: Admin/AdminPromotionProducts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int promotionId, List<int> productIds)
        {
            if (promotionId == 0 || productIds == null || !productIds.Any())
            {
                _notifyService.Error("Vui lòng chọn chương trình khuyến mãi và ít nhất một sản phẩm.");
                ViewBag.Promotions = _context.Promotions
                    .Select(p => new PromotionViewModel { PromotionId = p.PromotionId, PromotionName = p.PromotionName })
                    .ToList();
                ViewBag.Products = _context.Products
                    .Select(p => new ProductViewModel { ProductId = p.ProductId, ProductName = p.ProductName })
                    .ToList();
                return View();
            }

            try
            {
                foreach (var productId in productIds)
                {
                    if (!_context.PromotionProducts.Any(pp => pp.PromotionId == promotionId && pp.ProductId == productId))
                    {
                        var promotionProduct = new PromotionProduct
                        {
                            PromotionId = promotionId,
                            ProductId = productId
                        };
                        _context.Add(promotionProduct);
                    }
                }

                await _context.SaveChangesAsync();
                _notifyService.Success("Thêm sản phẩm khuyến mãi thành công!");
                return Json(new { success = true, message = "Thêm sản phẩm khuyến mãi thành công!", redirectTo = Url.Action("Index") });
            }
            catch
            {
                _notifyService.Error("Có lỗi xảy ra khi thêm sản phẩm khuyến mãi.");
                ViewBag.Promotions = _context.Promotions
                    .Select(p => new PromotionViewModel { PromotionId = p.PromotionId, PromotionName = p.PromotionName })
                    .ToList();
                ViewBag.Products = _context.Products
                    .Select(p => new ProductViewModel { ProductId = p.ProductId, ProductName = p.ProductName })
                    .ToList();
                return View();
            }
        }

        // GET: Admin/AdminPromotionProducts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy chương trình khuyến mãi.");
                return NotFound();
            }

            var promotion = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .ThenInclude(pp => pp.Product)
                .FirstOrDefaultAsync(p => p.PromotionId == id);
            if (promotion == null)
            {
                _notifyService.Error("Không tìm thấy chương trình khuyến mãi.");
                return NotFound();
            }

            ViewBag.Products = _context.Products
                .Select(p => new ProductViewModel { ProductId = p.ProductId, ProductName = p.ProductName })
                .ToList();
            return View(promotion);
        }

        // POST: Admin/AdminPromotionProducts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int promotionId, List<int> productIds)
        {
            if (promotionId == 0 || productIds == null)
            {
                _notifyService.Error("Vui lòng chọn ít nhất một sản phẩm.");
                ViewBag.Products = _context.Products
                    .Select(p => new ProductViewModel { ProductId = p.ProductId, ProductName = p.ProductName })
                    .ToList();
                return View();
            }

            try
            {
                // Xóa các sản phẩm khuyến mãi hiện tại của chương trình
                var existingProducts = _context.PromotionProducts.Where(pp => pp.PromotionId == promotionId);
                _context.PromotionProducts.RemoveRange(existingProducts);

                // Thêm các sản phẩm mới
                foreach (var productId in productIds)
                {
                    if (!_context.PromotionProducts.Any(pp => pp.PromotionId == promotionId && pp.ProductId == productId))
                    {
                        var promotionProduct = new PromotionProduct
                        {
                            PromotionId = promotionId,
                            ProductId = productId
                        };
                        _context.Add(promotionProduct);
                    }
                }

                await _context.SaveChangesAsync();
                _notifyService.Success("Cập nhật chương trình khuyến mãi thành công!");
                return Json(new { success = true, message = "Cập nhật chương trình khuyến mãi thành công!", redirectTo = Url.Action("Index") });
            }
            catch
            {
                _notifyService.Error("Có lỗi xảy ra khi cập nhật chương trình khuyến mãi.");
                ViewBag.Products = _context.Products
                    .Select(p => new ProductViewModel { ProductId = p.ProductId, ProductName = p.ProductName })
                    .ToList();
                return View();
            }
        }

        // GET: Admin/AdminPromotionProducts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy sản phẩm khuyến mãi.");
                return NotFound();
            }

            var promotionProduct = await _context.PromotionProducts
                .Include(pp => pp.Promotion)
                .Include(pp => pp.Product)
                .FirstOrDefaultAsync(pp => pp.PromotionProductId == id);
            if (promotionProduct == null)
            {
                _notifyService.Error("Không tìm thấy sản phẩm khuyến mãi.");
                return NotFound();
            }

            return View(promotionProduct);
        }

        // POST: Admin/AdminPromotionProducts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promotionProduct = await _context.PromotionProducts.FindAsync(id);
            if (promotionProduct != null)
            {
                _context.PromotionProducts.Remove(promotionProduct);
                await _context.SaveChangesAsync();
                _notifyService.Success("Xóa sản phẩm khuyến mãi thành công!");
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PromotionProductExists(int id)
        {
            return _context.PromotionProducts.Any(e => e.PromotionProductId == id);
        }
    }
}