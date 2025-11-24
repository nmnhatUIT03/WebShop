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
    public class AdminCategoriesController : Controller
    {
        private readonly webshopContext _context;
        public INotyfService _notifyService { get; }

        public AdminCategoriesController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        public IActionResult Index(int? page, string searchString)
        {
            var pageNumber = page == null || page <= 0 ? 1 : page.Value;
            var pageSize = Utilities.PAGE_SIZE;
            var query = _context.Categories.AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                // Trim và làm tìm kiếm không phân biệt hoa/thường
                searchString = searchString.Trim();
                query = query.Where(c => c.CatName.ToLower().Contains(searchString.ToLower()));
                ViewData["CurrentFilter"] = searchString;
            }

            var lsCategory = query.OrderByDescending(x => x.CatId);
            PagedList<Category> models = new PagedList<Category>(lsCategory, pageNumber, pageSize);
            ViewBag.CurrentPage = pageNumber;

            // Debug: Kiểm tra số lượng danh mục trả về
            if (!models.Any() && !string.IsNullOrEmpty(searchString))
            {
                _notifyService.Information("Không tìm thấy danh mục nào khớp với '" + searchString + "'.");
            }

            return View(models);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(m => m.CatId == id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CatId,CatName,Description,ParentId,Levels,Ordering,Published,Thumb,Title,Alias,MetaDesc,MetaKey,Cover,SchemaMarkup")] Category category, Microsoft.AspNetCore.Http.IFormFile fThumb)
        {
            if (ModelState.IsValid)
            {
                if (fThumb != null)
                {
                    string extension = Path.GetExtension(fThumb.FileName);
                    string imageName = Utilities.SEOUrl(category.CatName) + extension;
                    category.Thumb = await Utilities.UploadFile(fThumb, "categories", imageName.ToLower());
                }
                if (string.IsNullOrEmpty(category.Thumb)) category.Thumb = "default.jpg";
                category.Published = true;
                _context.Add(category);
                await _context.SaveChangesAsync();
                _notifyService.Success("Thêm danh mục thành công");
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CatId,CatName,Description,ParentId,Levels,Ordering,Published,Thumb,Title,Alias,MetaDesc,MetaKey,Cover,SchemaMarkup")] Category category, Microsoft.AspNetCore.Http.IFormFile fThumb)
        {
            if (id != category.CatId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (fThumb != null)
                    {
                        string extension = Path.GetExtension(fThumb.FileName);
                        string imageName = Utilities.SEOUrl(category.CatName) + extension;
                        category.Thumb = await Utilities.UploadFile(fThumb, "categories", imageName.ToLower());
                    }
                    if (string.IsNullOrEmpty(category.Thumb)) category.Thumb = "default.jpg";
                    _context.Update(category);
                    _notifyService.Success("Cập nhật danh mục thành công");
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.CatId))
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
            return View(category);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(m => m.CatId == id);
            if (category == null)
            {
                return NotFound();
            }

            var productCount = await _context.Products
                .CountAsync(p => p.CatId == id);

            var parentCategoryName = category.ParentId.HasValue
                ? (await _context.Categories.FirstOrDefaultAsync(c => c.CatId == category.ParentId))?.CatName ?? "Không có"
                : "Không có";

            ViewBag.ProductCount = productCount;
            ViewBag.CanDelete = productCount == 0;
            ViewBag.ParentCategoryName = parentCategoryName;

            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            var productCount = await _context.Products
                .CountAsync(p => p.CatId == id);

            if (productCount > 0)
            {
                _notifyService.Error($"Không thể xóa danh mục vì có {productCount} sản phẩm đang thuộc danh mục này.");
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            _notifyService.Success("Xóa danh mục thành công");
            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.CatId == id);
        }
    }
}