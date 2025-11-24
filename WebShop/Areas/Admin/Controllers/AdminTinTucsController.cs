using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PagedList.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Helpper;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminTinTucsController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notifyService;

        public AdminTinTucsController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        // GET: Admin/AdminTinTucs
        public IActionResult Index(int? page)
        {
            var tinTucsToUpdate = _context.TinTucs
                .Where(t => t.CreatedDate == null)
                .ToList();
            if (tinTucsToUpdate.Any())
            {
                foreach (var item in tinTucsToUpdate)
                {
                    item.CreatedDate = DateTime.Now;
                }
                _context.SaveChanges();
            }

            var pageNumber = page == null || page <= 0 ? 1 : page.Value;
            var pageSize = 20;
            var lsTinTuc = _context.TinTucs
                .AsNoTracking()
                .OrderByDescending(x => x.PostId);
            PagedList<TinTuc> models = new PagedList<TinTuc>(lsTinTuc, pageNumber, pageSize);
            ViewBag.CurrentPage = pageNumber;
            return View(models);
        }

        // GET: Admin/AdminTinTucs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tinTuc = await _context.TinTucs
                .FirstOrDefaultAsync(m => m.PostId == id);
            if (tinTuc == null)
            {
                return NotFound();
            }

            return View(tinTuc);
        }

        // GET: Admin/AdminTinTucs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/AdminTinTucs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PostId,Title,Scontents,Contents,Thumb,Published,Alias,CreatedDate,Author,AccountId,Tags,CatId,IsHot,IsNewfeed,MetaKey,MetaDesc,Views")] TinTuc tinTuc, IFormFile fThumb)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = "Dữ liệu không hợp lệ: " + string.Join(", ", errors) });
            }

            try
            {
                if (fThumb != null)
                {
                    string extension = Path.GetExtension(fThumb.FileName);
                    string image = Utilities.SEOUrl(tinTuc.Title) + extension;
                    string newThumb = await Utilities.UploadFile(fThumb, "news", image.ToLower());
                    if (!string.IsNullOrEmpty(newThumb))
                    {
                        tinTuc.Thumb = newThumb;
                    }
                    else
                    {
                        return Json(new { success = false, message = "Không thể tải ảnh lên.", field = "fThumb" });
                    }
                }
                else
                {
                    tinTuc.Thumb = "default.jpg";
                }

                tinTuc.Alias = Utilities.SEOUrl(tinTuc.Title);
                tinTuc.CreatedDate = DateTime.Now;

                _context.Add(tinTuc);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Thêm tin tức thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi tạo tin tức: {ex.Message}" });
            }
        }

        // GET: Admin/AdminTinTucs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tinTuc = await _context.TinTucs.FindAsync(id);
            if (tinTuc == null)
            {
                return NotFound();
            }
            return View(tinTuc);
        }

        // POST: Admin/AdminTinTucs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PostId,Title,Scontents,Contents,Thumb,Published,Alias,CreatedDate,Author,AccountId,Tags,CatId,IsHot,IsNewfeed,MetaKey,MetaDesc,Views")] TinTuc tinTuc, IFormFile fThumb)
        {
            if (id != tinTuc.PostId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingTinTuc = await _context.TinTucs.AsNoTracking().FirstOrDefaultAsync(x => x.PostId == id);
                    if (existingTinTuc == null)
                    {
                        return NotFound();
                    }

                    if (fThumb != null)
                    {
                        string extension = Path.GetExtension(fThumb.FileName);
                        string image = Utilities.SEOUrl(tinTuc.Title) + extension;
                        string newThumb = await Utilities.UploadFile(fThumb, "news", image.ToLower());
                        if (!string.IsNullOrEmpty(newThumb))
                        {
                            tinTuc.Thumb = newThumb;
                        }
                        else
                        {
                            tinTuc.Thumb = existingTinTuc.Thumb;
                            ModelState.AddModelError("", "Không thể tải ảnh lên.");
                        }
                    }
                    else
                    {
                        tinTuc.Thumb = existingTinTuc.Thumb;
                    }

                    tinTuc.Alias = Utilities.SEOUrl(tinTuc.Title);
                    _context.Update(tinTuc);
                    await _context.SaveChangesAsync();
                    _notifyService.Success("Cập nhật tin tức thành công!");
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TinTucExists(tinTuc.PostId))
                    {
                        _notifyService.Error("Tin tức không tồn tại!");
                        return NotFound();
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    _notifyService.Error($"Lỗi khi cập nhật: {ex.Message}");
                    ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                    return View(tinTuc);
                }
            }
            return View(tinTuc);
        }

        // GET: Admin/AdminTinTucs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tinTuc = await _context.TinTucs
                .FirstOrDefaultAsync(m => m.PostId == id);
            if (tinTuc == null)
            {
                return NotFound();
            }

            return View(tinTuc);
        }

        // POST: Admin/AdminTinTucs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tinTuc = await _context.TinTucs.FindAsync(id);
            if (tinTuc != null)
            {
                // Xóa file ảnh nếu không phải default.jpg
                if (!string.IsNullOrEmpty(tinTuc.Thumb) && tinTuc.Thumb != "default.jpg")
                {
                    string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/news", tinTuc.Thumb);
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                _context.TinTucs.Remove(tinTuc);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Xóa tin tức thành công!" });
            }
            else
            {
                return Json(new { success = false, message = "Tin tức không tồn tại!" });
            }
        }
        private bool TinTucExists(int id)
        {
            return _context.TinTucs.Any(e => e.PostId == id);
        }
    }
}