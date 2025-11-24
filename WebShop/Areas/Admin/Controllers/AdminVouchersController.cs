using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PagedList.Core;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminVouchersController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notifyService;

        public AdminVouchersController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        // GET: Admin/AdminVouchers
        public async Task<IActionResult> Index(string searchString, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var vouchers = _context.Vouchers
                .Include(v => v.UserPromotions)
                .Include(v => v.Orders)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                vouchers = vouchers.Where(v => v.VoucherCode != null && v.VoucherCode.Contains(searchString));
            }

            var pagedVouchers = vouchers.ToPagedList(pageNumber, pageSize);
            ViewData["CurrentFilter"] = searchString;
            return View(pagedVouchers);
        }

        // GET: Admin/AdminVouchers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy voucher.");
                return NotFound();
            }

            var voucher = await _context.Vouchers
                .Include(v => v.UserPromotions)
                .Include(v => v.Orders)
                .FirstOrDefaultAsync(m => m.VoucherId == id);

            if (voucher == null)
            {
                _notifyService.Error("Không tìm thấy voucher.");
                return NotFound();
            }

            return View(voucher);
        }

        // GET: Admin/AdminVouchers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/AdminVouchers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VoucherViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var paramVoucherCode = new SqlParameter("@VoucherCode", model.VoucherCode);
                    var paramDiscountType = new SqlParameter("@DiscountType", model.DiscountType ?? "Percentage");
                    var paramDiscountValue = new SqlParameter("@DiscountValue", model.DiscountType == "Percentage" ? model.DiscountValue / 100m : model.DiscountValue);
                    var paramMaxUsage = new SqlParameter("@MaxUsage", model.MaxUsage);
                    var paramEndDate = new SqlParameter("@EndDate", model.EndDate);
                    var paramMinOrderValue = new SqlParameter("@MinOrderValue", model.MinOrderValue);
                    var paramDefaultUserMaxUsage = new SqlParameter("@DefaultUserMaxUsage", model.DefaultUserMaxUsage);

                    await _context.Database.ExecuteSqlRawAsync(
                        "EXEC sp_CreateSingleVoucher @VoucherCode, @DiscountType, @DiscountValue, @MaxUsage, @EndDate, @MinOrderValue, @DefaultUserMaxUsage",
                        paramVoucherCode, paramDiscountType, paramDiscountValue, paramMaxUsage, paramEndDate, paramMinOrderValue, paramDefaultUserMaxUsage);

                    _notifyService.Success("Tạo mới voucher thành công!");
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _notifyService.Error($"Lỗi khi tạo voucher: {ex.Message}");
                    return View(model);
                }
            }
            return View(model);
        }

        // GET: Admin/AdminVouchers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy voucher.");
                return NotFound();
            }

            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null)
            {
                _notifyService.Error("Không tìm thấy voucher.");
                return NotFound();
            }

            // Convert DiscountValue to percentage for display
            var model = new VoucherViewModel
            {
                VoucherId = voucher.VoucherId,
                VoucherCode = voucher.VoucherCode,
                DiscountType = voucher.DiscountType,
                DiscountValue = voucher.DiscountType == "Percentage" ? voucher.DiscountValue * 100 : voucher.DiscountValue,
                MaxUsage = voucher.MaxUsage,
                UsedCount = voucher.UsedCount,
                EndDate = voucher.EndDate,
                MinOrderValue = voucher.MinOrderValue,
                DefaultUserMaxUsage = voucher.DefaultUserMaxUsage
            };

            return View(model);
        }

        // POST: Admin/AdminVouchers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, VoucherViewModel model)
        {
            if (id != model.VoucherId)
            {
                _notifyService.Error("Không tìm thấy voucher.");
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var voucher = await _context.Vouchers.FindAsync(id);
                    if (voucher == null)
                    {
                        _notifyService.Error("Không tìm thấy voucher.");
                        return NotFound();
                    }

                    voucher.VoucherCode = model.VoucherCode;
                    voucher.DiscountType = model.DiscountType;
                    voucher.DiscountValue = model.DiscountType == "Percentage" ? model.DiscountValue / 100m : model.DiscountValue;
                    voucher.MaxUsage = model.MaxUsage;
                    voucher.UsedCount = model.UsedCount;
                    voucher.EndDate = model.EndDate;
                    voucher.MinOrderValue = model.MinOrderValue;
                    voucher.DefaultUserMaxUsage = model.DefaultUserMaxUsage;

                    _context.Update(voucher);
                    await _context.SaveChangesAsync();
                    _notifyService.Success("Cập nhật voucher thành công!");
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VoucherExists(model.VoucherId))
                    {
                        _notifyService.Error("Không tìm thấy voucher.");
                        return NotFound();
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    _notifyService.Error($"Lỗi khi cập nhật voucher: {ex.Message}");
                    return View(model);
                }
            }
            return View(model);
        }

        // GET: Admin/AdminVouchers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy voucher.");
                return NotFound();
            }

            var voucher = await _context.Vouchers
                .Include(v => v.UserPromotions)
                .Include(v => v.Orders)
                .FirstOrDefaultAsync(m => m.VoucherId == id);

            if (voucher == null)
            {
                _notifyService.Error("Không tìm thấy voucher.");
                return NotFound();
            }

            ViewBag.UserPromotionCount = voucher.UserPromotions.Count;
            ViewBag.OrderCount = voucher.Orders.Count;
            ViewBag.CanDelete = voucher.Orders.Count == 0;

            return View(voucher);
        }

        // POST: Admin/AdminVouchers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var voucher = await _context.Vouchers
                .Include(v => v.UserPromotions)
                .Include(v => v.Orders)
                .FirstOrDefaultAsync(m => m.VoucherId == id);

            if (voucher == null)
            {
                _notifyService.Error("Không tìm thấy voucher.");
                return NotFound();
            }

            if (voucher.Orders.Count > 0)
            {
                _notifyService.Error("Không thể xóa voucher vì đã được sử dụng trong đơn hàng.");
                return RedirectToAction(nameof(Index));
            }

            _context.Vouchers.Remove(voucher);
            await _context.SaveChangesAsync();
            _notifyService.Success("Xóa voucher thành công!");
            return RedirectToAction(nameof(Index));
        }

        private bool VoucherExists(int id)
        {
            return _context.Vouchers.Any(e => e.VoucherId == id);
        }
    }

    public class VoucherViewModel
    {
        public int VoucherId { get; set; }
        public string VoucherCode { get; set; }
        public string DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public int MaxUsage { get; set; }
        public int UsedCount { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? MinOrderValue { get; set; }
        public int DefaultUserMaxUsage { get; set; }
    }
}