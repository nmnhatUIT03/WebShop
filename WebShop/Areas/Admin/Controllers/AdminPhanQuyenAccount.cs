using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebShop.Areas.Admin.Models;
using WebShop.Extension;
using WebShop.Helpper;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminPhanQuyenAccount : Controller
    {
        private readonly webshopContext _context;
        public INotyfService _notifyService { get; }

        public AdminPhanQuyenAccount(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "RoleName");
            List<SelectListItem> lsTrangThai = new List<SelectListItem>
            {
                new SelectListItem { Text = "Active", Value = "1" },
                new SelectListItem { Text = "Block", Value = "0" }
            };
            ViewData["lsTrangThai"] = lsTrangThai;
            var accounts = await _context.Accounts
                .Include(a => a.Role)
                .ToListAsync();
            return View(accounts);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            var account = await _context.Accounts
                .Include(a => a.Role)
                .FirstOrDefaultAsync(m => m.AccountId == id);
            if (account == null)
            {
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            if (account.RoleId != null && account.Role == null)
            {
                _notifyService.Warning($"Không tìm thấy vai trò với RoleId: {account.RoleId}. Vui lòng kiểm tra dữ liệu bảng Roles.");
            }

            return View(account);
        }

        public IActionResult Create()
        {
            ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "RoleName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AccountId,Phone,Email,Password,Salt,Active,FullName,RoleId,LastLogin,CreateDate")] Account account)
        {
            if (ModelState.IsValid)
            {
                string salt = Utilities.GetRandomKey();
                account.Salt = salt;
                account.Active = true;
                account.Password = (account.Phone + salt.Trim()).ToMD5();
                account.CreateDate = DateTime.Now;
                _context.Add(account);
                await _context.SaveChangesAsync();
                _notifyService.Success("Tạo mới thành công");
                return RedirectToAction(nameof(Index));
            }
            ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "RoleName", account.RoleId);
            return View(account);
        }

        public IActionResult ChangePassword()
        {
            ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "RoleName");
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var taikhoan = _context.Accounts.AsNoTracking().SingleOrDefault(x => x.Email == model.Email);
                if (taikhoan == null) return RedirectToAction("Login", "Accounts");

                var pass = (model.PasswordNow.Trim() + taikhoan.Salt.Trim()).ToMD5();
                if (pass == taikhoan.Password)
                {
                    string passnew = (model.Password.Trim() + taikhoan.Salt.Trim()).ToMD5();
                    taikhoan.Password = passnew;
                    taikhoan.LastLogin = DateTime.Now;
                    _context.Update(taikhoan);
                    _context.SaveChanges();
                    _notifyService.Success("Thay mật khẩu thành công");
                    return RedirectToAction("Login", "Accounts", new { Area = "Admin" });
                }
            }
            return RedirectToAction("Login", "Accounts", new { Area = "Admin" });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            var account = await _context.Accounts
                .Include(a => a.Role)
                .FirstOrDefaultAsync(a => a.AccountId == id);
            if (account == null)
            {
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }
            ViewData["DanhMuc"] = new SelectList(_context.Roles, "RoleId", "RoleName", account.RoleId);
            return View(account);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AccountId,FullName,Email,Phone,LastLogin,RoleId")] Account account)
        {
            if (id != account.AccountId)
            {
                return Json(new { success = false, message = "Không tìm thấy tài khoản." });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingAccount = await _context.Accounts.FindAsync(id);
                    if (existingAccount == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy tài khoản." });
                    }

                    // Chỉ cập nhật RoleId
                    existingAccount.RoleId = account.RoleId;
                    _context.Update(existingAccount);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Cập nhật quyền thành công!" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountExists(account.AccountId))
                    {
                        return Json(new { success = false, message = "Không tìm thấy tài khoản." });
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Lỗi khi cập nhật: " + ex.Message });
                }
            }
            return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            var account = await _context.Accounts
                .Include(a => a.Role)
                .FirstOrDefaultAsync(m => m.AccountId == id);
            if (account == null)
            {
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            return View(account);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
            {
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }
            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();
            _notifyService.Success("Xóa tài khoản thành công!");
            return RedirectToAction(nameof(Index));
        }

        private bool AccountExists(int id)
        {
            return _context.Accounts.Any(e => e.AccountId == id);
        }
    }
}