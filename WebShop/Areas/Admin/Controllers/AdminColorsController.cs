using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PagedList.Core;
using WebShop.Helpper;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminColorsController : Controller
    {
        private readonly webshopContext _context;
        public INotyfService _notifyService { get; }

        public AdminColorsController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        public IActionResult Index(int? page, string searchString)
        {
            var pageNumber = page == null || page <= 0 ? 1 : page.Value;
            var pageSize = Utilities.PAGE_SIZE;
            var query = _context.Colors.AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.Trim();
                query = query.Where(c => c.ColorName.ToLower().Contains(searchString.ToLower()));
                ViewData["CurrentFilter"] = searchString;
            }

            var lsColor = query.OrderByDescending(x => x.ColorId);
            PagedList<Color> models = new PagedList<Color>(lsColor, pageNumber, pageSize);
            ViewBag.CurrentPage = pageNumber;

            if (!models.Any() && !string.IsNullOrEmpty(searchString))
            {
                _notifyService.Information("Không tìm thấy màu sắc nào khớp với '" + searchString + "'.");
            }

            return View(models);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var color = await _context.Colors
                .FirstOrDefaultAsync(m => m.ColorId == id);
            if (color == null)
            {
                return NotFound();
            }

            return View(color);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ColorId,ColorName")] Color color)
        {
            if (ModelState.IsValid)
            {
                _context.Add(color);
                await _context.SaveChangesAsync();
                _notifyService.Success("Thêm màu sắc thành công");
                return RedirectToAction(nameof(Index));
            }
            return View(color);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var color = await _context.Colors.FindAsync(id);
            if (color == null)
            {
                return NotFound();
            }
            return View(color);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ColorId,ColorName")] Color color)
        {
            if (id != color.ColorId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(color);
                    _notifyService.Success("Cập nhật màu sắc thành công");
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ColorExists(color.ColorId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(color);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var color = await _context.Colors
                .FirstOrDefaultAsync(m => m.ColorId == id);
            if (color == null)
            {
                return NotFound();
            }

            var productDetailCount = await _context.ProductDetails
                .CountAsync(p => p.ColorId == id);

            ViewBag.ProductDetailCount = productDetailCount;
            ViewBag.CanDelete = productDetailCount == 0;

            return View(color);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var color = await _context.Colors.FindAsync(id);
            if (color == null)
            {
                return NotFound();
            }

            var productDetailCount = await _context.ProductDetails
                .CountAsync(p => p.ColorId == id);

            if (productDetailCount > 0)
            {
                _notifyService.Error($"Không thể xóa màu sắc vì có {productDetailCount} chi tiết sản phẩm đang sử dụng màu này.");
                return RedirectToAction(nameof(Index));
            }

            _context.Colors.Remove(color);
            await _context.SaveChangesAsync();
            _notifyService.Success("Xóa màu sắc thành công");
            return RedirectToAction(nameof(Index));
        }

        private bool ColorExists(int id)
        {
            return _context.Colors.Any(e => e.ColorId == id);
        }
    }
}