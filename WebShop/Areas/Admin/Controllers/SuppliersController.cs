using System;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PagedList.Core;
using WebShop.Models;
using WebShop.Helpper;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class SuppliersController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notifyService;

        public SuppliersController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        public IActionResult Index(int? page, string searchString)
        {
            var pageNumber = page == null || page <= 0 ? 1 : page.Value;
            var pageSize = Utilities.PAGE_SIZE;
            var query = _context.Suppliers.AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s => s.Name.Contains(searchString));
                ViewData["CurrentFilter"] = searchString;
            }

            var lsSupplier = query.OrderByDescending(x => x.SupplierId);
            PagedList<Supplier> models = new PagedList<Supplier>(lsSupplier, pageNumber, pageSize);
            ViewBag.CurrentPage = pageNumber;
            return View(models);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy nhà cung cấp!");
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.SupplierId == id);
            if (supplier == null)
            {
                _notifyService.Error("Nhà cung cấp không tồn tại!");
                return NotFound();
            }

            // Tính số sản phẩm liên kết, chỉ đếm sản phẩm Active và còn hàng
            var productCount = await _context.Products
                .CountAsync(p => p.SupplierId == id && p.Active == true && p.UnitsInStock > 0);
            ViewBag.ProductCount = productCount;

            return View(supplier);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SupplierId,Name,Address")] Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(supplier);
                    await _context.SaveChangesAsync();
                    _notifyService.Success("Thêm nhà cung cấp thành công!");
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _notifyService.Error($"Lỗi khi thêm nhà cung cấp: {ex.Message}");
                    ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                }
            }
            return View(supplier);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }
            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SupplierId,Name,Address")] Supplier supplier)
        {
            if (id != supplier.SupplierId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                    _notifyService.Success("Cập nhật nhà cung cấp thành công!");
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(supplier.SupplierId))
                    {
                        _notifyService.Error("Nhà cung cấp không tồn tại!");
                        return NotFound();
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    _notifyService.Error($"Lỗi khi cập nhật: {ex.Message}");
                    ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                }
            }
            return View(supplier);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.SupplierId == id);
            if (supplier == null)
            {
                return NotFound();
            }

            var productCount = await _context.Products
                .CountAsync(p => p.SupplierId == id);

            ViewBag.ProductCount = productCount;
            ViewBag.CanDelete = productCount == 0;

            return View(supplier);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                _notifyService.Error("Nhà cung cấp không tồn tại!");
                return NotFound();
            }

            var productCount = await _context.Products
                .CountAsync(p => p.SupplierId == id);

            if (productCount > 0)
            {
                _notifyService.Error($"Không thể xóa nhà cung cấp vì có {productCount} sản phẩm đang liên kết.");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();
                _notifyService.Success("Xóa nhà cung cấp thành công!");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _notifyService.Error($"Lỗi khi xóa: {ex.Message}");
                return View(supplier);
            }
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.SupplierId == id);
        }
    }
}