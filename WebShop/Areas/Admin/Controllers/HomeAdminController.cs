using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // Thêm để dùng CountAsync
using WebShop.Areas.Admin.Models;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class HomeAdminController : Controller
    {
        private readonly webshopContext _context;
        public INotyfService _notifyService { get; }

        public HomeAdminController(webshopContext context, INotyfService notifyService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notifyService = notifyService ?? throw new ArgumentNullException(nameof(notifyService));
        }

        public async Task<IActionResult> Index(int? month)
        {
            try
            {
                int selectedMonth = month ?? DateTime.Now.Month;
                DashBoardViewModel model = new DashBoardViewModel
                {
                    tongdh = (await _context.Orders.CountAsync(x => !x.Deleted)).ToString(),
                    tongdhchuaduyet = (await _context.Orders.CountAsync(x => x.TransactStatusId == 1 && !x.Deleted)).ToString(),
                    tongnguoidung = (await _context.Customers.CountAsync(x => x.Active == true || x.Active == null)).ToString(),
                    tongsp = (await _context.Products.CountAsync(x => x.Active == true)).ToString(),
                    SelectedMonth = selectedMonth,
                    FromDate = null,
                    ToDate = null,
                    RevenueByWeek = new List<RevenueByWeek>()
                };

                // Log để kiểm tra giá trị
                Console.WriteLine($"Tổng đơn hàng: {model.tongdh}");
                Console.WriteLine($"Đơn hàng chưa duyệt (TransactStatusId=1): {model.tongdhchuaduyet}");
                Console.WriteLine($"Tổng khách hàng: {model.tongnguoidung}");
                Console.WriteLine($"Tổng sản phẩm: {model.tongsp}");

                // Log chi tiết mẫu đơn hàng chưa duyệt
                var pendingOrders = await _context.Orders
                    .Where(x => x.TransactStatusId == 1 && !x.Deleted)
                    .Take(5)
                    .Select(x => new { x.OrderId, x.TransactStatusId, x.Deleted })
                    .ToListAsync();
                Console.WriteLine("Mẫu đơn hàng chưa duyệt: " +
                    (pendingOrders.Any() ? string.Join(", ", pendingOrders.Select(o => $"ID={o.OrderId}, Status={o.TransactStatusId}, Deleted={o.Deleted}")) : "Không có đơn hàng chưa duyệt"));

                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong Index: {ex.Message}");
                _notifyService.Error("Đã xảy ra lỗi khi tải dữ liệu dashboard.");
                return View(new DashBoardViewModel { RevenueByWeek = new List<RevenueByWeek>() });
            }
        }

        [HttpGet]
        public IActionResult GetRevenueByDateRange(int month, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                DateTime utcNow = DateTime.UtcNow;
                TimeZoneInfo vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                DateTime vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, vietnamZone);

                DateTime startDate = fromDate ?? new DateTime(vietnamNow.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime endDate = toDate ?? new DateTime(vietnamNow.Year, month, DateTime.DaysInMonth(vietnamNow.Year, month), 23, 59, 59, DateTimeKind.Utc);

                startDate = TimeZoneInfo.ConvertTimeFromUtc(startDate, vietnamZone);
                endDate = TimeZoneInfo.ConvertTimeFromUtc(endDate, vietnamZone);

                if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
                {
                    return BadRequest("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
                }

                var orders = _context.Orders
                    .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.Paid)
                    .ToList();

                Console.WriteLine($"Query: Orders from {startDate} to {endDate}, Count: {orders.Count}");
                foreach (var order in orders.Take(5))
                {
                    Console.WriteLine($"Order: ID={order.OrderId}, Date={order.OrderDate}, TotalMoney={order.TotalMoney}, Paid={order.Paid}, Status={order.TransactStatusId}");
                }

                var revenueByWeek = new List<RevenueByWeek>();
                int daysInRange = (endDate - startDate).Days + 1;
                int weekCount = Math.Min((int)Math.Ceiling(daysInRange / 7.0), 4);

                for (int i = 0; i < weekCount; i++)
                {
                    DateTime weekStart = startDate.AddDays(i * 7);
                    DateTime weekEnd = weekStart.AddDays(6) > endDate ? endDate : weekStart.AddDays(6);

                    var weekOrders = orders
                        .Where(o => o.OrderDate >= weekStart && o.OrderDate <= weekEnd)
                        .Sum(o => o.TotalMoney ?? 0);

                    revenueByWeek.Add(new RevenueByWeek
                    {
                        Week = $"Tuần {i + 1}",
                        TotalRevenue = weekOrders
                    });
                }

                if (!revenueByWeek.Any(r => r.TotalRevenue > 0))
                {
                    revenueByWeek = new List<RevenueByWeek> { new RevenueByWeek { Week = "Không có dữ liệu", TotalRevenue = 0 } };
                }

                return Ok(revenueByWeek);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetRevenueByDateRange: {ex.Message}");
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        [HttpGet]
        public IActionResult GetSalesByCategory(int month, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                DateTime utcNow = DateTime.UtcNow;
                TimeZoneInfo vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                DateTime vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, vietnamZone);

                DateTime startDate = fromDate ?? new DateTime(vietnamNow.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime endDate = toDate ?? new DateTime(vietnamNow.Year, month, DateTime.DaysInMonth(vietnamNow.Year, month), 23, 59, 59, DateTimeKind.Utc);

                startDate = TimeZoneInfo.ConvertTimeFromUtc(startDate, vietnamZone);
                endDate = TimeZoneInfo.ConvertTimeFromUtc(endDate, vietnamZone);

                if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
                {
                    return BadRequest("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
                }

                var salesData = (from o in _context.Orders
                                 join od in _context.OrderDetails on o.OrderId equals od.OrderId
                                 join pd in _context.ProductDetails on od.ProductDetailId equals pd.ProductDetailId
                                 join p in _context.Products on pd.ProductId equals p.ProductId
                                 join c in _context.Categories on p.CatId equals c.CatId
                                 where o.OrderDate >= startDate && o.OrderDate <= endDate && o.Paid
                                 group od by c.CatName into g
                                 select new
                                 {
                                     Category = g.Key,
                                     Quantity = g.Sum(od => od.Quantity ?? 0)
                                 }).ToList();

                Console.WriteLine($"Sales Query: From {startDate} to {endDate}");
                foreach (var item in salesData.Take(5))
                {
                    Console.WriteLine($"Category: {item.Category}, Quantity: {item.Quantity}");
                }

                var result = salesData.Select(s => new
                {
                    Category = s.Category,
                    Quantity = s.Quantity
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSalesByCategory: {ex.Message}");
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        public IActionResult Menu()
        {
            return View();
        }
    }
}