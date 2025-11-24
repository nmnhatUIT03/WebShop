using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Models;

namespace WebShop.Controllers
{
    public class DonHangController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notyfService;

        public DonHangController(webshopContext context, INotyfService notyfService)
        {
            _context = context;
            _notyfService = notyfService;
        }

        public async Task<IActionResult> Index()
        {
            var taikhoanID = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(taikhoanID))
            {
                _notyfService.Warning("Vui lòng đăng nhập để xem đơn hàng!");
                return RedirectToAction("Login", "Accounts");
            }

            var customerId = Convert.ToInt32(taikhoanID);
            var donhangs = await _context.Orders
                .Include(x => x.Customer) // Thêm để nạp thông tin khách hàng
                .Include(x => x.TransactStatus)
                .Include(x => x.Voucher) // Thêm để hiển thị mã voucher
                .Include(x => x.Promotion) // Thêm để hiển thị tên khuyến mãi
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId && !x.Deleted)
                .OrderByDescending(x => x.OrderDate)
                .ToListAsync();

            return View(donhangs);
        }

        [HttpGet]
        [Route("DonHang/Details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _notyfService.Error("Không tìm thấy đơn hàng!");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var taikhoanID = HttpContext.Session.GetString("CustomerId");
                if (string.IsNullOrEmpty(taikhoanID))
                {
                    _notyfService.Warning("Vui lòng đăng nhập!");
                    return RedirectToAction("Login", "Accounts");
                }

                var customerId = Convert.ToInt32(taikhoanID);
                var khachhang = await _context.Customers
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.CustomerId == customerId);
                if (khachhang == null)
                {
                    _notyfService.Error("Không tìm thấy khách hàng!");
                    return RedirectToAction(nameof(Index));
                }

                var donhang = await _context.Orders
                    .Include(x => x.TransactStatus)
                    .Include(x => x.Customer)
                    .Include(x => x.Promotion)
                    .Include(x => x.Voucher)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.OrderId == id && m.CustomerId == customerId && !m.Deleted);
                if (donhang == null)
                {
                    _notyfService.Error("Không tìm thấy đơn hàng hoặc bạn không có quyền xem!");
                    return RedirectToAction(nameof(Index));
                }

                var chitietdonhang = await _context.OrderDetails
                    .Include(x => x.ProductDetail)
                        .ThenInclude(pd => pd.Product)
                    .Include(x => x.ProductDetail)
                        .ThenInclude(pd => pd.Size)
                    .Include(x => x.ProductDetail)
                        .ThenInclude(pd => pd.Color)
                    .AsNoTracking()
                    .Where(x => x.OrderId == id)
                    .OrderBy(x => x.OrderDetailId)
                    .ToListAsync();

                ViewBag.ChiTiet = chitietdonhang;
                return View(donhang);
            }
            catch (Exception ex)
            {
                _notyfService.Error($"Lỗi: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}