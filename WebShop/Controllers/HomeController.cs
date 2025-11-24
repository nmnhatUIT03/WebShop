using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PagedList.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Models;
using WebShop.ModelViews;

namespace WebShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly webshopContext _context;
        public INotyfService _notifyService { get; }

        public HomeController(ILogger<HomeController> logger, webshopContext context, INotyfService notifyService)
        {
            _logger = logger;
            _context = context;
            _notifyService = notifyService;
        }

        public async Task<IActionResult> Index(int? page, string searchString, string currentFilter, int? pageNumber)
        {
            HomeViewVM model = new HomeViewVM();

            // Lấy tất cả sản phẩm - chỉ lấy những sản phẩm active
            var lsProducts = await _context.Products
                .AsNoTracking()
                .Where(x => x.Active == true)
                .OrderByDescending(x => x.DateCreated)
                .ToListAsync();

            // Lấy 12 sản phẩm nổi bật, bỏ qua sản phẩm hết hàng
            var topProducts = await _context.Products
                .AsNoTracking()
                .Where(x => x.Active == true && (x.UnitsInStock ?? 0) > 0)
                .OrderByDescending(x => x.BestSellers)
                .ThenByDescending(x => x.DateCreated)
                .Take(12)
                .Select(x => new ProductHomeVM
                {
                    ProductId = x.ProductId,
                    ProductName = x.ProductName,
                    Thumb = x.Thumb,
                    Price = x.Price,
                    DiscountPrice = x.Discount != null ? x.Price * (1 - (x.Discount / 100)) : x.Price,
                    UnitsInStock = x.UnitsInStock ?? 0
                })
                .ToListAsync();

            // Bổ sung thêm sản phẩm nếu không đủ 12
            if (topProducts.Count < 12)
            {
                var additionalProducts = await _context.Products
                    .AsNoTracking()
                    .Where(x => x.Active == true && (x.UnitsInStock ?? 0) > 0 && !topProducts.Select(p => p.ProductId).Contains(x.ProductId))
                    .OrderByDescending(x => x.DateCreated)
                    .Take(12 - topProducts.Count)
                    .Select(x => new ProductHomeVM
                    {
                        ProductId = x.ProductId,
                        ProductName = x.ProductName,
                        Thumb = x.Thumb,
                        Price = x.Price,
                        DiscountPrice = x.Discount != null ? x.Price * (1 - (x.Discount / 100)) : x.Price,
                        UnitsInStock = x.UnitsInStock ?? 0
                    })
                    .ToListAsync();

                topProducts.AddRange(additionalProducts);
            }

            // Lấy sản phẩm theo danh mục
            List<ProductHomeVM> lsProductView = new List<ProductHomeVM>();
            var lsCats = await _context.Categories
                .AsNoTracking()
                .Where(x => x.Published == true)
                .ToListAsync();
            foreach (var item in lsCats)
            {
                ProductHomeVM productHome = new ProductHomeVM();
                productHome.category = item;
                productHome.lsProducts = lsProducts.Where(x => x.CatId == item.CatId).ToList();
                lsProductView.Add(productHome);
            }

            // Lấy 3 tin tức mới nhất
            var TinTuc = await _context.TinTucs
                .AsNoTracking()
                .Where(x => x.Published == true)
                .OrderByDescending(x => x.CreatedDate)
                .Take(3)
                .ToListAsync();

            // Lấy các sản phẩm có PromotionId - TẠM THỜI COMMENT DO THIẾU BẢNG PromotionProducts
            // TODO: Tạo bảng PromotionProducts trong DB hoặc chạy migration
            /*
            var currentDate = DateTime.Now;
            var promotedProducts = await (from pp in _context.PromotionProducts
                                          join p in _context.Promotions on pp.PromotionId equals p.PromotionId
                                          join prod in _context.Products on pp.ProductId equals prod.ProductId
                                          where p.IsActive && p.EndDate > currentDate && p.Discount > 0
                                          orderby p.EndDate
                                          select new PromotionProductVM
                                          {
                                              Product = prod,
                                              Promotion = p
                                          }).Take(10).ToListAsync();

            // Debug log để kiểm tra dữ liệu
            _logger.LogInformation($"Found {promotedProducts.Count} promoted products at {DateTime.Now}");
            if (!promotedProducts.Any())
            {
                _logger.LogWarning("No promoted products found with active promotions.");
            }
            else
            {
                foreach (var item in promotedProducts)
                {
                    _logger.LogInformation($"Product {item.Product.ProductId}: Promotion EndDate = {item.Promotion.EndDate}");
                }
            }

            ViewBag.PromotionProducts = promotedProducts;
            */
            
            // Tạm thời dùng list rỗng để tránh lỗi
            ViewBag.PromotionProducts = new List<PromotionProductVM>();
            _logger.LogInformation("PromotionProducts feature is temporarily disabled - table not found in database");

            // Gán dữ liệu vào model
            model.Products = lsProductView;
            model.TinTucs = TinTuc;
            model.TopProducts = topProducts;

            ViewBag.AllProducts = lsProducts;

            return View(model);
        }

        public async Task<IActionResult> DanhMuc(int page = 1, int CatID = 0)
        {
            var pageNumber = page;
            var pageSize = 6;
            List<Product> lsProducts = new List<Product>();

            if (CatID != 0)
            {
                lsProducts = await _context.Products
                    .AsNoTracking()
                    .Where(x => x.CatId == CatID && (x.Active ?? false) == true)
                    .Include(x => x.Cat)
                    .OrderByDescending(x => x.ProductId)
                    .ToListAsync();
            }
            else
            {
                lsProducts = await _context.Products
                    .AsNoTracking()
                    .Where(x => (x.Active ?? false) == true)
                    .Include(x => x.Cat)
                    .OrderByDescending(x => x.ProductId)
                    .ToListAsync();
            }

            var lsCats = await _context.Categories
                .AsNoTracking()
                .Where(x => x.Published == true)
                .ToListAsync();
            PagedList<Product> models = new PagedList<Product>(lsProducts.AsQueryable(), pageNumber, pageSize);
            ViewBag.CurrentCateID = CatID;
            ViewBag.CurrentPage = pageNumber;

            ViewData["DanhMuc"] = new SelectList(_context.Categories, "CatId", "CatName", CatID);
            return View(models);
        }

        public IActionResult Filtter(int CatID = 0)
        {
            var url = $"/Admin/AdminProducts?CatID={CatID}";
            if (CatID == 0)
            {
                url = $"/Admin/AdminProducts";
            }
            return Json(new { status = "success", redirectUrl = url });
        }

        public async Task<IActionResult> GetVariantPopup(int productId)
        {
            try
            {
                var product = await _context.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProductId == productId && (p.Active ?? false) == true);
                if (product == null)
                {
                    _logger.LogWarning("Product not found for ProductId: {productId}", productId);
                    _notifyService.Error("Sản phẩm không tồn tại.");
                    return Json(new { success = false, message = "Sản phẩm không tồn tại." });
                }

                var productDetails = await _context.ProductDetails
                    .Include(pd => pd.Size)
                    .Include(pd => pd.Color)
                    .AsNoTracking()
                    .Where(pd => pd.ProductId == productId)
                    .ToListAsync();

                if (productDetails == null || !productDetails.Any())
                {
                    _logger.LogWarning("No product details found for productId: {productId}", productId);
                    _notifyService.Error("Không có biến thể nào cho sản phẩm này.");
                    return Json(new { success = false, message = "Không có biến thể nào cho sản phẩm này." });
                }

                ViewBag.ProductDetails = productDetails;
                return PartialView("_ProductVariantPopup", product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering variant popup for productId: {productId}", productId);
                _notifyService.Error($"Lỗi server: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = "CustomerAuthentication")]
        public async Task<IActionResult> CheckInView()
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int parsedCustomerId))
            {
                _logger.LogWarning("Session CustomerId không hợp lệ trong CheckInView");
                _notifyService.Error("Vui lòng đăng nhập để xem điểm danh!");
                return RedirectToAction("Login", "Accounts");
            }

            var customer = await _context.Customers
                .Include(c => c.CheckInHistory)
                .FirstOrDefaultAsync(x => x.CustomerId == parsedCustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Không tìm thấy khách hàng với CustomerId {CustomerId}", customerId);
                _notifyService.Error("Tài khoản không tồn tại!");
                return RedirectToAction("Login", "Accounts");
            }

            var checkIns = customer.CheckInHistory
                .OrderByDescending(c => c.CheckInDate)
                .ToList();

            // Gán trực tiếp vào Model
            return View(customer);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = "CustomerAuthentication")]
        public async Task<IActionResult> CheckIn()
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int parsedCustomerId))
            {
                _logger.LogWarning("Session CustomerId không hợp lệ trong CheckIn");
                _notifyService.Error("Vui lòng đăng nhập để điểm danh!");
                return RedirectToAction("CheckInView");
            }

            var customer = await _context.Customers.FindAsync(parsedCustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Không tìm thấy khách hàng với CustomerId {CustomerId}", customerId);
                _notifyService.Error("Tài khoản không tồn tại!");
                return RedirectToAction("CheckInView");
            }

            if (customer.LastCheckInDate.HasValue && customer.LastCheckInDate.Value.Date == DateTime.Today)
            {
                _logger.LogInformation("Khách hàng {CustomerId} đã điểm danh hôm nay", customer.CustomerId);
                _notifyService.Warning("Bạn đã điểm danh hôm nay!");
                return RedirectToAction("CheckInView");
            }

            int pointsEarned = 5; // Mặc định 5 điểm mỗi ngày
            int consecutiveDays = CalculateConsecutiveDays(customer.LastCheckInDate);
            if (consecutiveDays > 0 && consecutiveDays % 5 == 0)
            {
                pointsEarned += 5; // Thêm 5 điểm cho ngày thứ 5, 10, 15, v.v.
                _logger.LogInformation("Khách hàng {CustomerId} nhận thêm 5 điểm do điểm danh liên tiếp", customer.CustomerId);
            }

            customer.Points += pointsEarned;
            customer.CheckInCount += 1;
            customer.LastCheckInDate = DateTime.Now;

            var checkIn = new CheckInHistory
            {
                CustomerId = customer.CustomerId,
                CheckInDate = DateTime.Now,
                PointsEarned = pointsEarned
            };
            _context.CheckInHistory.Add(checkIn);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Điểm danh thành công cho CustomerId {CustomerId}, Điểm nhận được: {PointsEarned}", customer.CustomerId, pointsEarned);
                _notifyService.Success($"Điểm danh thành công! Bạn nhận được {pointsEarned} điểm.");
                return RedirectToAction("CheckInView");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu điểm danh cho CustomerId {CustomerId}", customer.CustomerId);
                _notifyService.Error("Lỗi hệ thống khi điểm danh, vui lòng thử lại!");
                return RedirectToAction("CheckInView");
            }
        }

        private int CalculateConsecutiveDays(DateTime? lastCheckIn)
        {
            if (!lastCheckIn.HasValue) return 0;
            var daysDiff = (DateTime.Today - lastCheckIn.Value.Date).Days;
            return daysDiff == 1 ? 1 : 0; // Chỉ tính liên tiếp nếu cách 1 ngày
        }
    }
}