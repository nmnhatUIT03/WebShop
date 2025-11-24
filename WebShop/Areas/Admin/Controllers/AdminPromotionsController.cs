using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Areas.Admin.Models;
using WebShop.Extension;
using WebShop.Helpper;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminPromotionsController : Controller
    {
        private readonly webshopContext _context;
        public INotyfService _notifyService { get; }

        public AdminPromotionsController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        // GET: Admin/AdminPromotions
        public async Task<IActionResult> Index()
        {
            var promotions = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .Include(p => p.UserPromotions)
                .Include(p => p.Orders)
                .ToListAsync();
            return View(promotions);
        }

        // GET: Admin/AdminPromotions/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/AdminPromotions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                // Convert percentage to decimal (e.g., 50% -> 0.5)
                promotion.Discount = promotion.Discount / 100m;

                var numberOfPromotions = 1;
                var paramNumber = new SqlParameter("@NumberOfPromotions", numberOfPromotions);
                var paramName = new SqlParameter("@PromotionName", promotion.PromotionName);
                var paramDiscount = new SqlParameter("@Discount", promotion.Discount);
                var paramStartDate = new SqlParameter("@StartDate", promotion.StartDate);
                var paramEndDate = new SqlParameter("@EndDate", promotion.EndDate);
                var paramIsActive = new SqlParameter("@IsActive", promotion.IsActive);
                var paramMaxUsage = new SqlParameter("@DefaultUserMaxUsage", promotion.DefaultUserMaxUsage);

                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_CreatePromotions @NumberOfPromotions, @PromotionName, @Discount, @StartDate, @EndDate, @IsActive, @DefaultUserMaxUsage",
                    paramNumber, paramName, paramDiscount, paramStartDate, paramEndDate, paramIsActive, paramMaxUsage);

                _notifyService.Success("Tạo mới khuyến mãi thành công");
                return RedirectToAction(nameof(Index));
            }
            // Convert discount back to percentage for display
            promotion.Discount = promotion.Discount * 100m;
            return View(promotion);
        }

        // GET: Admin/AdminPromotions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy khuyến mãi.");
                return NotFound();
            }

            var promotion = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .Include(p => p.UserPromotions)
                .Include(p => p.Orders)
                .FirstOrDefaultAsync(m => m.PromotionId == id);
            if (promotion == null)
            {
                _notifyService.Error("Không tìm thấy khuyến mãi.");
                return NotFound();
            }

            return View(promotion);
        }

        // GET: Admin/AdminPromotions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy khuyến mãi.");
                return NotFound();
            }

            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null)
            {
                _notifyService.Error("Không tìm thấy khuyến mãi.");
                return NotFound();
            }
            // Convert discount to percentage for display
            promotion.Discount = promotion.Discount * 100m;
            return View(promotion);
        }

        // POST: Admin/AdminPromotions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Promotion promotion)
        {
            if (id != promotion.PromotionId)
            {
                _notifyService.Error("Không tìm thấy khuyến mãi.");
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Convert percentage to decimal (e.g., 50% -> 0.5)
                    promotion.Discount = promotion.Discount / 100m;
                    _context.Update(promotion);
                    await _context.SaveChangesAsync();
                    _notifyService.Success("Cập nhật khuyến mãi thành công!");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PromotionExists(promotion.PromotionId))
                    {
                        _notifyService.Error("Không tìm thấy khuyến mãi.");
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            // Convert discount back to percentage for display
            promotion.Discount = promotion.Discount * 100m;
            return View(promotion);
        }

        // GET: Admin/AdminPromotions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy khuyến mãi.");
                return NotFound();
            }

            var promotion = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .FirstOrDefaultAsync(m => m.PromotionId == id);
            if (promotion == null)
            {
                _notifyService.Error("Không tìm thấy khuyến mãi.");
                return NotFound();
            }

            ViewBag.ProductDetailCount = promotion.PromotionProducts.Count;
            ViewBag.CanDelete = promotion.PromotionProducts.Count == 0;

            return View(promotion);
        }

        // POST: Admin/AdminPromotions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promotion = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .FirstOrDefaultAsync(m => m.PromotionId == id);
            if (promotion != null)
            {
                _context.Promotions.Remove(promotion);
                await _context.SaveChangesAsync();
                _notifyService.Success("Xóa khuyến mãi thành công!");
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PromotionExists(int id)
        {
            return _context.Promotions.Any(e => e.PromotionId == id);
        }
    }
}