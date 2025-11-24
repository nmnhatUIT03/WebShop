using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Models;
using WebShop.Models.ViewModels;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminUserPromotionsController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notifyService;
        private readonly DateTime _unusedDate = new DateTime(9999, 12, 31, 0, 0, 0);

        public AdminUserPromotionsController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        // GET: Admin/AdminUserPromotions/Index
        public async Task<IActionResult> Index(string searchString)
        {
            try
            {
                var customers = _context.Customers
                    .Include(c => c.UserPromotions)
                        .ThenInclude(up => up.Promotion)
                    .Include(c => c.UserPromotions)
                        .ThenInclude(up => up.Voucher)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchString))
                {
                    customers = customers.Where(c => c.FullName != null && c.FullName.Contains(searchString, StringComparison.OrdinalIgnoreCase));
                }

                var result = await customers.Select(c => new UserPromotionSummaryViewModel
                {
                    CustomerId = c.CustomerId,
                    FullName = c.FullName ?? "Chưa xác định",
                    UsedCount = c.UserPromotions.Count(up => up.UsedDate != _unusedDate),
                    UnusedCount = c.UserPromotions.Count(up => up.UsedDate == _unusedDate)
                }).ToListAsync();

                ViewData["CurrentFilter"] = searchString;
                return View(result);
            }
            catch (Exception ex)
            {
                _notifyService.Error($"Lỗi khi tải dữ liệu: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Index: Lỗi - {ex.Message}, StackTrace: {ex.StackTrace}");
                return View(new List<UserPromotionSummaryViewModel>());
            }
        }

        // GET: Admin/AdminUserPromotions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("ID khách hàng không hợp lệ!");
                System.Diagnostics.Debug.WriteLine("Details: ID khách hàng là null");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var customer = await _context.Customers
                    .Include(c => c.UserPromotions)
                        .ThenInclude(up => up.Promotion)
                    .Include(c => c.UserPromotions)
                        .ThenInclude(up => up.Voucher)
                    .FirstOrDefaultAsync(c => c.CustomerId == id);

                if (customer == null)
                {
                    _notifyService.Error($"Không tìm thấy khách hàng với ID {id}!");
                    System.Diagnostics.Debug.WriteLine($"Details: Không tìm thấy khách hàng với ID {id}");
                    return RedirectToAction(nameof(Index));
                }

                var model = new UserPromotionDetailsViewModel
                {
                    CustomerId = customer.CustomerId,
                    FullName = customer.FullName ?? $"Khách hàng #{customer.CustomerId}",
                    UsedPromotions = customer.UserPromotions
                        .Where(up => up.UsedDate != _unusedDate)
                        .Select(up => new PromotionItem
                        {
                            UserPromotionId = up.UserPromotionId,
                            Name = up.Promotion != null ? up.Promotion.PromotionName : up.Voucher != null ? up.Voucher.VoucherCode : "Chưa xác định",
                            UsedDate = up.UsedDate
                        }).ToList(),
                    UnusedPromotions = customer.UserPromotions
                        .Where(up => up.UsedDate == _unusedDate)
                        .Select(up => new PromotionItem
                        {
                            UserPromotionId = up.UserPromotionId,
                            Name = up.Promotion != null ? up.Promotion.PromotionName : up.Voucher != null ? up.Voucher.VoucherCode : "Chưa xác định",
                            UsedDate = null
                        }).ToList()
                };

                // Log để debug
                System.Diagnostics.Debug.WriteLine($"Details: CustomerId={model.CustomerId}, FullName={model.FullName}, UsedPromotionsCount={model.UsedPromotions.Count}, UnusedPromotionsCount={model.UnusedPromotions.Count}");

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong Details (CustomerId={id}): {ex.Message}, StackTrace: {ex.StackTrace}");
                _notifyService.Error($"Lỗi khi tải chi tiết khách hàng ID {id}: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}