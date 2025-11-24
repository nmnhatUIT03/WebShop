using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminRolesController : Controller
    {
        private readonly webshopContext _context;
        public INotyfService _notifyService { get; }

        public AdminRolesController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Roles.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var role = await _context.Roles
                .FirstOrDefaultAsync(m => m.RoleId == id);
            if (role == null)
            {
                return NotFound();
            }

            return View(role);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RoleId,RoleName,Description")] Role role)
        {
            if (ModelState.IsValid)
            {
                _context.Add(role);
                await _context.SaveChangesAsync();
                _notifyService.Success("Tạo mới thành công");
                return RedirectToAction(nameof(Index));
            }
            return View(role);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var role = await _context.Roles.FindAsync(id);
            if (role == null)
            {
                return NotFound();
            }
            return View(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RoleId,RoleName,Description")] Role role)
        {
            if (id != role.RoleId)
            {
                return Json(new { success = false, message = "ID không hợp lệ.", field = "" });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingRole = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleId == id);
                    if (existingRole == null)
                    {
                        return Json(new { success = false, message = "Quyền truy cập không tồn tại.", field = "" });
                    }

                    var entry = _context.Roles.Attach(existingRole);
                    if (!string.IsNullOrEmpty(role.RoleName) && role.RoleName != existingRole.RoleName)
                    {
                        entry.Property(x => x.RoleName).CurrentValue = role.RoleName;
                    }
                    if (!string.IsNullOrEmpty(role.Description) && role.Description != existingRole.Description)
                    {
                        entry.Property(x => x.Description).CurrentValue = role.Description;
                    }

                    await _context.SaveChangesAsync();
                    _notifyService.Success("Cập nhật thành công");
                    return Json(new { success = true, message = "Cập nhật quyền truy cập thành công!" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoleExists(role.RoleId))
                    {
                        return Json(new { success = false, message = "Có lỗi xảy ra.", field = "" });
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = string.Join(" ", errors), field = errors.Any() ? ModelState.Keys.FirstOrDefault(k => ModelState[k].Errors.Any()) : "" });
        }
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var role = await _context.Roles
                .FirstOrDefaultAsync(m => m.RoleId == id);
            if (role == null)
            {
                return NotFound();
            }

            return View(role);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            _notifyService.Success("Xóa quyền truy cập thành công");
            return RedirectToAction(nameof(Index));
        }

        private bool RoleExists(int id)
        {
            return _context.Roles.Any(e => e.RoleId == id);
        }
    }
}