using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PagedList.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WebShop.Areas.Admin.Models;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminAccountsController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notifyService;
        private readonly ILogger<AdminAccountsController> _logger;

        public AdminAccountsController(
            webshopContext context,
            INotyfService notifyService,
            ILogger<AdminAccountsController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notifyService = notifyService ?? throw new ArgumentNullException(nameof(notifyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: Admin/AdminAccounts
        public IActionResult Index(string searchString, int? page)
        {
            _logger.LogInformation("Truy cập Index với searchString: {SearchString}, page: {Page}", searchString, page);

            ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "Description");
            List<SelectListItem> lsTrangThai = new List<SelectListItem>
            {
                new SelectListItem { Text = "Active", Value = "1" },
                new SelectListItem { Text = "Block", Value = "0" }
            };
            ViewData["lsTrangThai"] = lsTrangThai;

            var accounts = _context.Accounts
                .Include(a => a.Role)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.Trim().ToLower();
                accounts = accounts.Where(a =>
                    (a.FullName != null && a.FullName.ToLower().Contains(searchString)) ||
                    (a.Email != null && a.Email.ToLower().Contains(searchString)) ||
                    (a.Phone != null && a.Phone.Contains(searchString)));
                ViewData["CurrentFilter"] = searchString;
                _logger.LogInformation("Áp dụng tìm kiếm với từ khóa: {SearchString}", searchString);
            }

            accounts = accounts.OrderByDescending(a => a.CreateDate);

            int pageSize = 20;
            int pageNumber = page == null || page <= 0 ? 1 : page.Value;
            ViewBag.CurrentPage = pageNumber;

            var pagedList = new PagedList<Account>(accounts, pageNumber, pageSize);

            return View(pagedList);
        }

        // GET: Admin/AdminAccounts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            _logger.LogInformation("Truy cập Details với id: {Id}", id);
            if (id == null)
            {
                _logger.LogWarning("ID tài khoản không được cung cấp");
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            var account = await _context.Accounts
                .Include(a => a.Role)
                .FirstOrDefaultAsync(m => m.AccountId == id);
            if (account == null)
            {
                _logger.LogWarning("Không tìm thấy tài khoản với id: {Id}", id);
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            return View(account);
        }

        // GET: Admin/AdminAccounts/Create
        [HttpGet]
        public IActionResult Create()
        {
            _logger.LogInformation("Truy cập Create GET");
            ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "Description");
            return View(new RegisterAdminVM { Active = true });
        }

        // POST: Admin/AdminAccounts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterAdminVM model)
        {
            _logger.LogInformation("Bắt đầu xử lý form Create với dữ liệu: FullName={FullName}, Email={Email}, Phone={Phone}, Active={Active}, RoleId={RoleId}",
                model.FullName, model.Email, model.Phone, model.Active, model.RoleId);
            try
            {
                if (!string.IsNullOrEmpty(model.Password) && model.Password != model.ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không khớp.");
                    _logger.LogWarning("Tạo tài khoản thất bại: Mật khẩu xác nhận không khớp");
                    _notifyService.Error("Mật khẩu xác nhận không khớp.");
                    ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "Description");
                    return View(model);
                }

                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    _logger.LogWarning("Thông tin tạo tài khoản không hợp lệ: {Errors}", errors);
                    _notifyService.Error($"Vui lòng kiểm tra lại thông tin: {errors}");
                    ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "Description");
                    return View(model);
                }

                var emailToCheck = model.Email?.Trim().ToLower();
                if (await _context.Accounts.AsNoTracking().AnyAsync(x => x.Email == emailToCheck))
                {
                    ModelState.AddModelError("Email", $"Email {model.Email} đã được sử dụng");
                    _logger.LogWarning("Tạo tài khoản thất bại: Email {Email} đã tồn tại", model.Email);
                    _notifyService.Error($"Email {model.Email} đã được sử dụng");
                    ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "Description");
                    return View(model);
                }

                var phoneToCheck = model.Phone?.Trim();
                if (!string.IsNullOrEmpty(phoneToCheck) && await _context.Accounts.AsNoTracking().AnyAsync(x => x.Phone == phoneToCheck))
                {
                    ModelState.AddModelError("Phone", $"Số điện thoại {model.Phone} đã được sử dụng");
                    _logger.LogWarning("Tạo tài khoản thất bại: Số điện thoại {Phone} đã tồn tại", model.Phone);
                    _notifyService.Error($"Số điện thoại {model.Phone} đã được sử dụng");
                    ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "Description");
                    return View(model);
                }

                if (model.RoleId == null || !await _context.Roles.AnyAsync(r => r.RoleId == model.RoleId))
                {
                    ModelState.AddModelError("RoleId", "Quyền truy cập không hợp lệ.");
                    _logger.LogWarning("Tạo tài khoản thất bại: Quyền truy cập không hợp lệ");
                    _notifyService.Error("Quyền truy cập không hợp lệ.");
                    ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "Description");
                    return View(model);
                }

                Account account = new()
                {
                    FullName = model.FullName?.Trim(),
                    Phone = phoneToCheck,
                    Email = emailToCheck,
                    Password = model.Password,
                    Active = model.Active,
                    Salt = null,
                    CreateDate = DateTime.Now,
                    RoleId = model.RoleId
                };

                _context.Add(account);
                await _context.SaveChangesAsync();

                var accountId = account.AccountId.ToString();
                HttpContext.Session.SetString("AccountId", accountId);
                _logger.LogInformation("Đã lưu Session AccountId: {AccountId}", accountId);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, account.FullName ?? ""),
                    new Claim("AccountId", accountId),
                    new Claim(ClaimTypes.Role, "Admin")
                };
                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, "AdminAuthentication");
                ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
                await HttpContext.SignInAsync("AdminAuthentication", claimsPrincipal);
                _notifyService.Success("Tạo tài khoản thành công!");
                _logger.LogInformation("Tạo tài khoản thành công với id: {AccountId}", account.AccountId);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình tạo tài khoản");
                _notifyService.Error($"Tạo tài khoản thất bại: {ex.Message}");
                ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "Description");
                return View(model);
            }
        }

        // GET: Admin/AdminAccounts/ValidateEmail
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ValidateEmail(string Email, int AccountId = 0)
        {
            _logger.LogInformation("Gọi ValidateEmail với Email: {Email}, AccountId: {AccountId}", Email, AccountId);
            try
            {
                var emailToCheck = Email?.Trim().ToLower();
                var account = _context.Accounts.AsNoTracking()
                    .Any(x => x.Email == emailToCheck && x.AccountId != AccountId);
                if (account)
                {
                    return Json($"Email {Email} đã được sử dụng.");
                }
                return Json(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra email: {Email}", Email);
                return Json(new { Success = false, Message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        // GET: Admin/AdminAccounts/ValidatePhone
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ValidatePhone(string Phone, int AccountId = 0)
        {
            _logger.LogInformation("Gọi ValidatePhone với Phone: {Phone}, AccountId: {AccountId}", Phone, AccountId);
            try
            {
                if (string.IsNullOrWhiteSpace(Phone))
                {
                    return Json(true);
                }

                var phoneToCheck = Phone.Trim();
                var account = _context.Accounts.AsNoTracking()
                    .Any(x => x.Phone == phoneToCheck && x.AccountId != AccountId);
                if (account)
                {
                    return Json($"Số điện thoại {Phone} đã được sử dụng.");
                }
                return Json(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra số điện thoại: {Phone}", Phone);
                return Json(new { Success = false, Message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        // GET: Admin/AdminAccounts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            _logger.LogInformation("Truy cập Edit GET với id: {Id}", id);
            if (id == null)
            {
                _logger.LogWarning("ID tài khoản không được cung cấp");
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            var account = await _context.Accounts
                .Include(a => a.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountId == id);
            if (account == null)
            {
                _logger.LogWarning("Không tìm thấy tài khoản với id: {Id}", id);
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            _logger.LogInformation("Tài khoản tìm thấy: ID={AccountId}, FullName={FullName}, Email={Email}, Phone={Phone}, Active={Active}, RoleId={RoleId}",
                account.AccountId, account.FullName, account.Email, account.Phone, account.Active, account.RoleId);

            var model = new RegisterAdminVM
            {
                AccountId = account.AccountId,
                Phone = account.Phone,
                Email = account.Email,
                FullName = account.FullName,
                Active = account.Active,
                Password = string.Empty,
                ConfirmPassword = string.Empty,
                RoleId = account.RoleId
            };

            ViewData["QuyenTruyCap"] = new SelectList(_context.Roles, "RoleId", "Description");
            var roleDescription = ((IEnumerable<SelectListItem>)ViewData["QuyenTruyCap"])
                .FirstOrDefault(r => r.Value == account.RoleId.ToString())?.Text ?? "Không xác định";
            ViewBag.RoleDescription = roleDescription;
            ViewBag.RoleId = account.RoleId;

            return View(model);
        }

        // POST: Admin/AdminAccounts/UpdateField
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateField(int AccountId, string FullName, string Email, string Phone, bool? Active, string Password, string ConfirmPassword)
        {
            _logger.LogInformation("Bắt đầu xử lý UpdateField với AccountId: {AccountId}, FullName: {FullName}, Email: {Email}, Phone: {Phone}, Active: {Active}, PasswordProvided: {PasswordProvided}, ConfirmPasswordProvided: {ConfirmPasswordProvided}",
                AccountId, FullName, Email, Phone, Active, !string.IsNullOrEmpty(Password), !string.IsNullOrEmpty(ConfirmPassword));

            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.AccountId == AccountId);
                if (account == null)
                {
                    _logger.LogWarning("Không tìm thấy tài khoản với id: {AccountId}", AccountId);
                    return Json(new { Success = false, Message = "Không tìm thấy tài khoản." });
                }

                bool hasChanges = false;

                // Xử lý các trường trong form chính
                if (!string.IsNullOrEmpty(FullName) && FullName != account.FullName)
                {
                    if (string.IsNullOrEmpty(FullName.Trim()))
                    {
                        return Json(new { Success = false, Message = "Họ và tên là bắt buộc.", Field = "FullName" });
                    }
                    account.FullName = FullName.Trim();
                    hasChanges = true;
                }

                if (!string.IsNullOrEmpty(Email) && Email != account.Email)
                {
                    var emailToCheck = Email.Trim().ToLower();
                    if (string.IsNullOrEmpty(emailToCheck))
                    {
                        return Json(new { Success = false, Message = "Email là bắt buộc.", Field = "Email" });
                    }
                    if (emailToCheck != account.Email?.Trim().ToLower() &&
                        await _context.Accounts.AsNoTracking().AnyAsync(x => x.Email == emailToCheck && x.AccountId != AccountId))
                    {
                        return Json(new { Success = false, Message = $"Email {Email} đã được sử dụng.", Field = "Email" });
                    }
                    account.Email = emailToCheck;
                    hasChanges = true;
                }

                if (Phone != null && Phone != account.Phone)
                {
                    var phoneToCheck = Phone?.Trim();
                    if (!string.IsNullOrEmpty(phoneToCheck) &&
                        phoneToCheck != account.Phone?.Trim() &&
                        await _context.Accounts.AsNoTracking().AnyAsync(x => x.Phone == phoneToCheck && x.AccountId != AccountId))
                    {
                        return Json(new { Success = false, Message = $"Số điện thoại {Phone} đã được sử dụng.", Field = "Phone" });
                    }
                    account.Phone = phoneToCheck;
                    hasChanges = true;
                }

                if (Active.HasValue && Active.Value != account.Active)
                {
                    account.Active = Active.Value;
                    hasChanges = true;
                }

                // Xử lý form mật khẩu
                if (!string.IsNullOrEmpty(Password))
                {
                    if (string.IsNullOrEmpty(ConfirmPassword))
                    {
                        return Json(new { Success = false, Message = "Vui lòng xác nhận mật khẩu.", Field = "ConfirmPassword" });
                    }
                    if (Password != ConfirmPassword)
                    {
                        return Json(new { Success = false, Message = "Mật khẩu xác nhận không khớp.", Field = "ConfirmPassword" });
                    }
                    if (Password.Length < 6)
                    {
                        return Json(new { Success = false, Message = "Mật khẩu phải có ít nhất 6 ký tự.", Field = "Password" });
                    }
                    account.Password = Password.Trim();
                    account.Salt = null;
                    hasChanges = true;
                }

                if (!hasChanges)
                {
                    return Json(new { Success = false, Message = "Không có thay đổi nào để cập nhật." });
                }

                _context.Update(account);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cập nhật trường thành công cho tài khoản id: {AccountId}", AccountId);
                return Json(new { Success = true, Message = !string.IsNullOrEmpty(Password) ? "Cập nhật mật khẩu thành công!" : "Cập nhật thông tin thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trường cho tài khoản id: {AccountId}", AccountId);
                return Json(new { Success = false, Message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        // GET: Admin/AdminAccounts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            _logger.LogInformation("Truy cập Delete với id: {Id}", id);
            if (id == null)
            {
                _logger.LogWarning("ID tài khoản không được cung cấp");
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            var account = await _context.Accounts
                .Include(a => a.Role)
                .FirstOrDefaultAsync(m => m.AccountId == id);
            if (account == null)
            {
                _logger.LogWarning("Không tìm thấy tài khoản với id: {Id}", id);
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            return View(account);
        }

        // POST: Admin/AdminAccounts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("Xác nhận xóa tài khoản với id: {Id}", id);
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
            {
                _logger.LogWarning("Không tìm thấy tài khoản với id: {Id}", id);
                _notifyService.Error("Không tìm thấy tài khoản.");
                return NotFound();
            }

            try
            {
                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();
                _notifyService.Success("Xóa tài khoản thành công!");
                _logger.LogInformation("Xóa tài khoản thành công với id: {Id}", id);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa tài khoản với id: {Id}", id);
                _notifyService.Error($"Xóa tài khoản thất bại: {ex.Message}");
                return View(account);
            }
        }

        private bool AccountExists(int id)
        {
            return _context.Accounts.Any(e => e.AccountId == id);
        }
    }
}