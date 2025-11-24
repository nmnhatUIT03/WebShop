using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PagedList.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WebShop.Extension;
using WebShop.Helpper;
using WebShop.Models;
using WebShop.ModelViews;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminCustomersController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notyfService;
        private readonly ILogger<AdminCustomersController> _logger;
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public AdminCustomersController(
            webshopContext context,
            INotyfService notyfService,
            ILogger<AdminCustomersController> logger,
            IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _notyfService = notyfService;
            _logger = logger;
            _dataProtectionProvider = dataProtectionProvider;
        }

        public IActionResult Index(int? page)
        {
            _logger.LogInformation("Truy cập Index");
            var pageNumber = page == null || page <= 0 ? 1 : page.Value;
            var pageSize = 20;
            var lsCustomers = _context.Customers.AsNoTracking()
                .Include(x => x.Location)
                .OrderByDescending(x => x.CreateDate);
            PagedList<Customer> models = new PagedList<Customer>(lsCustomers, pageNumber, pageSize);
            ViewBag.CurrentPage = pageNumber;
            return View(models);
        }

        public async Task<IActionResult> Details(int? id)
        {
            _logger.LogInformation("Truy cập Details với id: {Id}", id);
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Location)
                .Include(c => c.Orders)
                .Include(c => c.Comments)
                .Include(c => c.UserPromotions)
                .FirstOrDefaultAsync(m => m.CustomerId == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpGet]
        public IActionResult Create()
        {
            _logger.LogInformation("Truy cập Create GET");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterVM model)
        {
            _logger.LogInformation("Bắt đầu xử lý form Create với dữ liệu: FullName={FullName}, Email={Email}, Phone={Phone}, Active={Active}",
                model.FullName, model.Email, model.Phone, model.Active);
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    _logger.LogWarning("Thông tin tạo khách hàng không hợp lệ: {Errors}", errors);
                    _notyfService.Error("Vui lòng kiểm tra lại thông tin");
                    return View(model);
                }

                var emailToCheck = model.Email?.Trim().ToLower();
                if (await _context.Customers.AsNoTracking().AnyAsync(x => x.Email == emailToCheck))
                {
                    ModelState.AddModelError("Email", $"Email {model.Email} đã được sử dụng");
                    _logger.LogWarning("Tạo khách hàng thất bại: Email {Email} đã tồn tại", model.Email);
                    _notyfService.Error($"Email {model.Email} đã được sử dụng");
                    return View(model);
                }

                var phoneToCheck = model.Phone?.Trim().ToLower();
                if (!string.IsNullOrEmpty(phoneToCheck) && await _context.Customers.AsNoTracking().AnyAsync(x => x.Phone == phoneToCheck))
                {
                    ModelState.AddModelError("Phone", $"Số điện thoại {model.Phone} đã được sử dụng");
                    _logger.LogWarning("Tạo khách hàng thất bại: Số điện thoại {Phone} đã tồn tại", model.Phone);
                    _notyfService.Error($"Số điện thoại {model.Phone} đã được sử dụng");
                    return View(model);
                }

                string salt = Utilities.GetRandomKey(8);
                Customer customer = new()
                {
                    FullName = model.FullName?.Trim(),
                    Phone = phoneToCheck,
                    Email = emailToCheck,
                    Password = (model.Password + salt.Trim()).ToMD5(),
                    Active = model.Active,
                    Salt = salt,
                    CreateDate = DateTime.Now
                };

                _context.Add(customer);
                await _context.SaveChangesAsync();

                var customerId = customer.CustomerId.ToString();
                HttpContext.Session.SetString("CustomerId", customerId);
                _logger.LogInformation("Đã lưu Session CustomerId: {CustomerId}", customerId);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, customer.FullName ?? ""),
                    new Claim("CustomerId", customerId),
                    new Claim(ClaimTypes.Role, "Customer")
                };
                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, "AdminAuthentication");
                ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
                await HttpContext.SignInAsync("AdminAuthentication", claimsPrincipal);
                _notyfService.Success("Tạo khách hàng thành công!");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình tạo khách hàng");
                _notyfService.Error("Tạo khách hàng thất bại, vui lòng thử lại");
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ValidateEmail(string Email, int CustomerId = 0)
        {
            _logger.LogInformation("Gọi ValidateEmail với Email: {Email}, CustomerId: {CustomerId}", Email, CustomerId);
            try
            {
                var emailToCheck = Email?.Trim().ToLower();
                var customer = _context.Customers.AsNoTracking()
                    .Any(x => x.Email == emailToCheck && x.CustomerId != CustomerId);
                if (customer)
                {
                    return Json($"Email {Email} đã được sử dụng");
                }
                return Json(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra email: {Email}", Email);
                return Json(new { Success = false, Message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ValidatePhone(string Phone, int CustomerId = 0)
        {
            _logger.LogInformation("Gọi ValidatePhone với Phone: {Phone}, CustomerId: {CustomerId}", Phone, CustomerId);
            try
            {
                var phoneToCheck = Phone?.Trim().ToLower();
                var customer = _context.Customers.AsNoTracking()
                    .Any(x => x.Phone == phoneToCheck && x.CustomerId != CustomerId);
                if (customer)
                {
                    return Json($"Số điện thoại {Phone} đã được sử dụng");
                }
                return Json(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra số điện thoại: {Phone}", Phone);
                return Json(new { Success = false, Message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            _logger.LogInformation("Truy cập Edit với id: {Id}", id);
            if (id == null)
            {
                _logger.LogWarning("ID khách hàng không được cung cấp");
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Orders)
                .Include(c => c.Comments)
                .Include(c => c.UserPromotions)
                .FirstOrDefaultAsync(m => m.CustomerId == id);

            if (customer == null)
            {
                _logger.LogWarning("Không tìm thấy khách hàng với id: {Id}", id);
                return NotFound();
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CustomerId,FullName,Birthday,Avatar,Address,Email,Phone,Active")] Customer customer, IFormFile AvatarFile)
        {
            _logger.LogInformation("Bắt đầu xử lý Edit với id: {Id}, dữ liệu: FullName={FullName}, Email={Email}, Phone={Phone}, Active={Active}",
                id, customer.FullName, customer.Email, customer.Phone, customer.Active);

            if (id != customer.CustomerId)
            {
                _logger.LogWarning("ID không khớp: {Id} != {CustomerId}", id, customer.CustomerId);
                return NotFound();
            }

            try
            {
                var existingCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerId == id);

                if (existingCustomer == null)
                {
                    _logger.LogWarning("Không tìm thấy khách hàng với id: {Id}", id);
                    return NotFound();
                }

                // Kiểm tra email trùng lặp
                var emailToCheck = customer.Email?.Trim().ToLower();
                if (!string.IsNullOrEmpty(emailToCheck) &&
                    emailToCheck != existingCustomer.Email?.Trim().ToLower() &&
                    await _context.Customers.AsNoTracking().AnyAsync(x => x.Email == emailToCheck && x.CustomerId != id))
                {
                    ModelState.AddModelError("Email", $"Email {customer.Email} đã được sử dụng");
                    _logger.LogWarning("Cập nhật thất bại: Email {Email} đã tồn tại", customer.Email);
                    _notyfService.Error($"Email {customer.Email} đã được sử dụng");
                    return View(customer);
                }

                // Kiểm tra số điện thoại trùng lặp
                var phoneToCheck = customer.Phone?.Trim().ToLower();
                if (!string.IsNullOrEmpty(phoneToCheck) &&
                    phoneToCheck != existingCustomer.Phone?.Trim().ToLower() &&
                    await _context.Customers.AsNoTracking().AnyAsync(x => x.Phone == phoneToCheck && x.CustomerId != id))
                {
                    ModelState.AddModelError("Phone", $"Số điện thoại {customer.Phone} đã được sử dụng");
                    _logger.LogWarning("Cập nhật thất bại: Số điện thoại {Phone} đã tồn tại", customer.Phone);
                    _notyfService.Error($"Số điện thoại {customer.Phone} đã được sử dụng");
                    return View(customer);
                }

                // Xử lý upload avatar
                if (AvatarFile != null && AvatarFile.Length > 0)
                {
                    var newFileName = $"{Guid.NewGuid()}{Path.GetExtension(AvatarFile.FileName)}";
                    var uploadResult = await Utilities.UploadFile(AvatarFile, @"customers\", newFileName);
                    if (string.IsNullOrEmpty(uploadResult))
                    {
                        ModelState.AddModelError("AvatarFile", "Lỗi khi tải lên ảnh");
                        _logger.LogWarning("Cập nhật thất bại: Lỗi khi tải lên ảnh avatar cho khách hàng id: {Id}", id);
                        _notyfService.Error("Lỗi khi tải lên ảnh avatar");
                        return View(customer);
                    }
                    // Xóa avatar cũ nếu có
                    if (!string.IsNullOrEmpty(existingCustomer.Avatar))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingCustomer.Avatar);
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }
                    customer.Avatar = uploadResult;
                }
                else
                {
                    customer.Avatar = existingCustomer.Avatar; // Giữ avatar cũ nếu không upload mới
                }

                // Giữ các trường không chỉnh sửa
                customer.CreateDate = existingCustomer.CreateDate;
                customer.LastLogin = existingCustomer.LastLogin;
                customer.Password = existingCustomer.Password;
                customer.Salt = existingCustomer.Salt;
                customer.LocationId = existingCustomer.LocationId;
                customer.District = existingCustomer.District;
                customer.Ward = existingCustomer.Ward;

                _context.Update(customer);
                await _context.SaveChangesAsync();
                _notyfService.Success("Cập nhật khách hàng thành công!");
                _logger.LogInformation("Cập nhật khách hàng thành công với id: {Id}", id);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Lỗi đồng bộ khi cập nhật khách hàng với id: {Id}", id);
                if (!CustomerExists(customer.CustomerId))
                {
                    return NotFound();
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật khách hàng với id: {Id}", id);
                _notyfService.Error("Cập nhật khách hàng thất bại, vui lòng thử lại");
                return View(customer);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            _logger.LogInformation("Truy cập Delete với id: {Id}", id);
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.CustomerId == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("Xác nhận xóa khách hàng với id: {Id}", id);
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Xóa khách hàng thành công với id: {Id}", id);
            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerId == id);
        }
    }
}