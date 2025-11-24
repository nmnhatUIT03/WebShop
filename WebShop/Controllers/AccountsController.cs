using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using WebShop.Extension;
using WebShop.Helpper;
using WebShop.Models;
using WebShop.ModelViews;
using static WebShop.Controllers.ShoppingCartController;

namespace WebShop.Controllers
{
    [Authorize(AuthenticationSchemes = "CustomerAuthentication")]
    public class AccountsController : Controller
    {
        private const bool V = true;
        private readonly webshopContext _context;
        private readonly INotyfService _notyfService;
        private readonly ILogger<AccountsController> _logger;
        private readonly IDataProtector _protector;
        private readonly object _cartLock = new object();

        public AccountsController(webshopContext context, INotyfService notyfService, ILogger<AccountsController> logger,
            IDataProtectionProvider provider)
        {
            _context = context;
            _notyfService = notyfService;
            _logger = logger;
            _protector = provider.CreateProtector("Cart");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ValidatePhone(string phone)
        {
            try
            {
                var phoneToCheck = phone?.Trim().ToLower();
                var customer = _context.Customers.AsNoTracking().Any(x => x.Phone == phoneToCheck);
                if (customer)
                {
                    return Json($"Số điện thoại {phone} đã được sử dụng");
                }
                return Json(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra số điện thoại: {Phone}", phone);
                return Json(new { Success = false, Message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ValidateEmail(string email)
        {
            try
            {
                var emailToCheck = email?.Trim().ToLower();
                var customer = _context.Customers.AsNoTracking().Any(x => x.Email == emailToCheck);
                if (customer)
                {
                    return Json($"Email {email} đã được sử dụng");
                }
                return Json(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra email: {Email}", email);
                return Json(new { Success = false, Message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [Route("tai-khoan-cua-toi.html", Name = "Dashboard")]
        public IActionResult Dashboard()
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int parsedCustomerId))
            {
                _logger.LogInformation("Session CustomerId không hợp lệ, chuyển hướng về trang đăng nhập");
                return RedirectToAction("Login");
            }

            var customer = _context.Customers.AsNoTracking().FirstOrDefault(x => x.CustomerId == parsedCustomerId);
            if (customer == null)
            {
                _logger.LogInformation("Không tìm thấy khách hàng với CustomerId {CustomerId}", customerId);
                return RedirectToAction("Login");
            }

            var lsDonHang = _context.Orders
                .Include(x => x.TransactStatus)
                .Include(x => x.Promotion)
                .Include(x => x.Voucher)
                .Include(x => x.OrderDetails)
                .AsNoTracking()
                .Where(x => x.CustomerId == parsedCustomerId && !x.Deleted)
                .OrderByDescending(x => x.OrderDate)
                .Take(5)
                .ToList();

            _logger.LogInformation("Số lượng đơn hàng lấy được: {Count}", lsDonHang.Count);

            ViewBag.DonHang = lsDonHang;
            ViewBag.CurrentCustomerFullName = customer.FullName;
            var sessionCartItems = GetSessionCartItems();
            _logger.LogInformation("Session hiện tại: CustomerId={CustomerId}, CouponDiscount={CouponDiscount}, SessionCart={SessionCart}",
                customerId, HttpContext.Session.GetString("CouponDiscount"),
                JsonSerializer.Serialize(sessionCartItems));
            return View(customer);
        }

        [HttpPost]
        [Route("tai-khoan-cua-toi/update-profile", Name = "UpdateProfile")]
        public async Task<IActionResult> UpdateProfile(Customer model, IFormFile AvatarFile)
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int parsedCustomerId))
            {
                _logger.LogWarning("Session CustomerId không hợp lệ hoặc không tồn tại");
                _notyfService.Error("Vui lòng đăng nhập để cập nhật thông tin!");
                return RedirectToAction("Login");
            }

            _logger.LogInformation("Dữ liệu nhận từ form: FullName={FullName}, Phone={Phone}, Address={Address}, Email={Email}, Birthday={Birthday}, AvatarFile={AvatarFile}",
                model.FullName, model.Phone, model.Address, model.Email ?? "null", model.Birthday?.ToString("yyyy-MM-dd"), AvatarFile?.FileName ?? "null");

            try
            {
                if (string.IsNullOrEmpty(model.FullName) || string.IsNullOrEmpty(model.Phone) || string.IsNullOrEmpty(model.Address))
                {
                    _logger.LogWarning("Thông tin cập nhật không hợp lệ: Thiếu trường bắt buộc");
                    _notyfService.Error("Vui lòng điền đầy đủ họ tên, số điện thoại và địa chỉ!");
                    return RedirectToAction("Dashboard");
                }

                var customer = await _context.Customers.FindAsync(parsedCustomerId);
                if (customer == null)
                {
                    _logger.LogWarning("Không tìm thấy khách hàng với CustomerId {CustomerId}", customerId);
                    _notyfService.Error("Tài khoản không tồn tại!");
                    return RedirectToAction("Login");
                }

                var phoneToCheck = model.Phone?.Trim().ToLower();
                if (!string.IsNullOrEmpty(phoneToCheck) && await _context.Customers
                    .AsNoTracking()
                    .AnyAsync(x => x.Phone == phoneToCheck && x.CustomerId != parsedCustomerId))
                {
                    _logger.LogWarning("Cập nhật thất bại: Số điện thoại {Phone} đã tồn tại", model.Phone);
                    _notyfService.Error($"Số điện thoại {model.Phone} đã được sử dụng!");
                    return RedirectToAction("Dashboard");
                }

                // Update fields
                customer.FullName = model.FullName?.Trim();
                customer.Phone = phoneToCheck;
                customer.Address = model.Address?.Trim();
                customer.Birthday = model.Birthday;

                // Handle avatar file upload
                if (AvatarFile != null && AvatarFile.Length > 0)
                {
                    _logger.LogInformation("Received AvatarFile: Name={FileName}, Size={FileSize}", AvatarFile.FileName, AvatarFile.Length);

                    // Validate file type and size
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var extension = Path.GetExtension(AvatarFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension))
                    {
                        _logger.LogWarning("Cập nhật thất bại: File avatar không hợp lệ {FileName}", AvatarFile.FileName);
                        _notyfService.Error("Vui lòng chọn file hình ảnh hợp lệ (.jpg, .jpeg, .png, .gif)!");
                        return RedirectToAction("Dashboard");
                    }

                    if (AvatarFile.Length > 5 * 1024 * 1024) // Limit to 5MB
                    {
                        _logger.LogWarning("Cập nhật thất bại: File avatar quá lớn {FileSize}", AvatarFile.Length);
                        _notyfService.Error("File ảnh không được lớn hơn 5MB!");
                        return RedirectToAction("Dashboard");
                    }

                    // Delete old avatar if exists
                    if (!string.IsNullOrEmpty(customer.Avatar) && System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", customer.Avatar.TrimStart('/'))))
                    {
                        System.IO.File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", customer.Avatar.TrimStart('/')));
                        _logger.LogInformation("Deleted old avatar: {OldAvatarPath}", customer.Avatar);
                    }

                    // Save new file
                    var fileName = $"{parsedCustomerId}_{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/avatars", fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)); // Ensure directory exists
                    _logger.LogInformation("Directory ensured at: {DirectoryPath}", Path.GetDirectoryName(filePath));
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await AvatarFile.CopyToAsync(stream);
                    }
                    customer.Avatar = $"/uploads/avatars/{fileName}";
                    _logger.LogInformation("Đã lưu avatar: {AvatarPath}", customer.Avatar);
                }
                else
                {
                    _logger.LogWarning("No AvatarFile received in UpdateProfile");
                }

                // Mark entity as Modified
                _context.Entry(customer).State = EntityState.Modified;
                _logger.LogInformation("Updating customer: Avatar={Avatar}, Birthday={Birthday}", customer.Avatar, customer.Birthday?.ToString("yyyy-MM-dd"));
                var rowsAffected = await _context.SaveChangesAsync();
                _logger.LogInformation("Cập nhật thành công, số hàng bị ảnh hưởng: {RowsAffected}, CustomerId={CustomerId}", rowsAffected, customerId);

                _notyfService.Success("Cập nhật thông tin thành công!");
                return RedirectToAction("Dashboard");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật cơ sở dữ liệu cho CustomerId {CustomerId}: {Message}", customerId, ex.InnerException?.Message ?? ex.Message);
                _notyfService.Error("Lỗi khi cập nhật cơ sở dữ liệu, vui lòng thử lại!");
                return RedirectToAction("Dashboard");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu file avatar cho CustomerId {CustomerId}: {Message}", customerId, ex.Message);
                _notyfService.Error("Lỗi khi lưu file avatar, vui lòng thử lại!");
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi cập nhật cho CustomerId {CustomerId}", customerId);
                _notyfService.Error("Cập nhật thông tin thất bại, vui lòng thử lại!");
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("dang-ky.html", Name = "DangKy")]
        public IActionResult DangKy()
        {
            return View("DangKyTaiKhoan");
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("dang-ky.html", Name = "DangKy")]
        public async Task<IActionResult> DangKy(RegisterVM taikhoan)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    _logger.LogWarning("Thông tin đăng ký không hợp lệ: {Errors}", errors);
                    _notyfService.Error("Vui lòng kiểm tra lại thông tin đăng ký");
                    return View("DangKyTaiKhoan", taikhoan);
                }

                var emailToCheck = taikhoan.Email?.Trim().ToLower();
                if (await _context.Customers.AsNoTracking().AnyAsync(x => x.Email == emailToCheck))
                {
                    ModelState.AddModelError("Email", $"Email {taikhoan.Email} đã được sử dụng");
                    _logger.LogWarning("Đăng ký thất bại: Email {Email} đã tồn tại", taikhoan.Email);
                    _notyfService.Error($"Email {taikhoan.Email} đã được sử dụng");
                    return View("DangKyTaiKhoan", taikhoan);
                }

                var phoneToCheck = taikhoan.Phone?.Trim().ToLower();
                if (!string.IsNullOrEmpty(phoneToCheck) && await _context.Customers.AsNoTracking().AnyAsync(x => x.Phone == phoneToCheck))
                {
                    ModelState.AddModelError("Phone", $"Số điện thoại {taikhoan.Phone} đã được sử dụng");
                    _logger.LogWarning("Đăng ký thất bại: Số điện thoại {Phone} đã tồn tại", taikhoan.Phone);
                    _notyfService.Error($"Số điện thoại {taikhoan.Phone} đã được sử dụng");
                    return View("DangKyTaiKhoan", taikhoan);
                }

                string salt = Utilities.GetRandomKey(8);
                Customer customer = new()
                {
                    FullName = taikhoan.FullName?.Trim(),
                    Phone = phoneToCheck,
                    Email = emailToCheck,
                    Password = (taikhoan.Password + salt.Trim()).ToMD5(),
                    Active = true,
                    Salt = salt,
                    CreateDate = DateTime.Now
                };

                _context.Add(customer);
                await _context.SaveChangesAsync();

                var customerId = customer.CustomerId.ToString();
                HttpContext.Session.SetString("CustomerId", customerId);
                _logger.LogInformation("Đã lưu Session CustomerId: {CustomerId}", customerId);

                await SyncCart(customer.CustomerId);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, customer.FullName ?? ""),
                    new Claim("CustomerId", customerId),
                    new Claim(ClaimTypes.Role, "Customer")
                };
                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, "CustomerAuthentication");
                ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
                await HttpContext.SignInAsync("CustomerAuthentication", claimsPrincipal);
                _notyfService.Success("Đăng ký tài khoản thành công!");

                var sessionCartItems = GetSessionCartItems();
                _logger.LogInformation("Session sau đăng ký: CustomerId={CustomerId}, CouponDiscount={CouponDiscount}, SessionCart={SessionCart}",
                    customerId, HttpContext.Session.GetString("CouponDiscount"),
                    JsonSerializer.Serialize(sessionCartItems));

                var buyNowCart = GetBuyNowFromCookie("Anonymous");
                if (buyNowCart != null && buyNowCart.Items.Any())
                {
                    var userBuyNowCart = new SimpleCart
                    {
                        CustomerId = customerId,
                        CartToken = buyNowCart.CartToken,
                        Items = buyNowCart.Items
                    };
                    SaveBuyNowToCookie(userBuyNowCart, customerId);
                    Response.Cookies.Delete("BuyNowCart_Anonymous");
                    _logger.LogInformation("Chuyển hướng đến checkout.html sau đăng ký do có giỏ Mua ngay");
                    return Redirect("/checkout.html");
                }

                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình đăng ký tài khoản");
                _notyfService.Error("Đăng ký thất bại, vui lòng thử lại");
                return View("DangKyTaiKhoan", taikhoan);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("dang-nhap.html", Name = "DangNhap")]
        [Route("/Accounts/Login")]
        public IActionResult Login(string returnUrl = null)
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            if (!string.IsNullOrEmpty(customerId) && User.Identity.IsAuthenticated)
            {
                _logger.LogInformation("Đã đăng nhập với CustomerId: {CustomerId}, xử lý returnUrl={ReturnUrl}", customerId, returnUrl);
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Dashboard");
            }
            ViewBag.ReturnUrl = returnUrl;
            _logger.LogInformation("Yêu cầu GET đăng nhập: returnUrl={ReturnUrl}", returnUrl);
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("dang-nhap.html", Name = "DangNhap")]
        [Route("/Accounts/Login")]
        public async Task<IActionResult> Login(LoginViewModel customer, string returnUrl = null)
        {
            try
            {
                bool isAjax = HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                _logger.LogInformation("Yêu cầu POST đăng nhập: UserName={UserName}, returnUrl={ReturnUrl}, IsAjax={IsAjax}", customer.UserName, returnUrl, isAjax);

                if (!ModelState.IsValid || string.IsNullOrEmpty(customer.UserName) || string.IsNullOrEmpty(customer.Password))
                {
                    _notyfService.Error("Vui lòng nhập đầy đủ email và mật khẩu");
                    if (isAjax)
                        return Json(new { success = false, message = "Thông tin đăng nhập không hợp lệ" });
                    return View(customer);
                }

                var customerEntity = await _context.Customers
                    .FirstOrDefaultAsync(x => x.Email.Trim().ToLower() == customer.UserName.Trim().ToLower());
                if (customerEntity == null)
                {
                    _notyfService.Error("Tài khoản không tồn tại");
                    if (isAjax)
                        return Json(new { success = false, message = "Tài khoản không tồn tại" });
                    return View(customer);
                }

                string pass = (customer.Password + (customerEntity.Salt?.Trim() ?? "")).ToMD5();
                if (customerEntity.Password != pass)
                {
                    _notyfService.Error("Mật khẩu không đúng");
                    if (isAjax)
                        return Json(new { success = false, message = "Mật khẩu không đúng" });
                    return View(customer);
                }

                if (!customerEntity.Active.GetValueOrDefault())
                {
                    _notyfService.Error("Tài khoản của bạn đã bị khóa");
                    if (isAjax)
                        return Json(new { success = false, message = "Tài khoản của bạn đã bị khóa" });
                    return RedirectToAction("ThongBao");
                }

                // Update LastLogin
                customerEntity.LastLogin = DateTime.Now;
                _context.Entry(customerEntity).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cập nhật LastLogin cho CustomerId {CustomerId} thành {LastLogin}", customerEntity.CustomerId, customerEntity.LastLogin);

                var customerId = customerEntity.CustomerId.ToString();
                HttpContext.Session.SetString("CustomerId", customerId);
                _logger.LogInformation("Đã lưu Session CustomerId: {CustomerId}", customerId);

                var syncedCart = await SyncCart(customerEntity.CustomerId);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, customerEntity.FullName ?? ""),
                    new Claim("CustomerId", customerId),
                    new Claim(ClaimTypes.Role, "Customer")
                };
                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, "CustomerAuthentication");
                ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
                await HttpContext.SignInAsync("CustomerAuthentication", claimsPrincipal);
                _notyfService.Success("Đăng nhập thành công!");

                var sessionCartItems = GetSessionCartItems();
                _logger.LogInformation("Session sau đăng nhập: CustomerId={CustomerId}, CouponDiscount={CouponDiscount}, SessionCart={SessionCart}",
                    customerId, HttpContext.Session.GetString("CouponDiscount"),
                    JsonSerializer.Serialize(sessionCartItems));

                // ✅ Xử lý BuyNow cart nếu có
                var buyNowCart = GetBuyNowFromCookie("Anonymous");
                if (buyNowCart != null && buyNowCart.Items.Any())
                {
                    var userBuyNowCart = new SimpleCart
                    {
                        CustomerId = customerId,
                        CartToken = buyNowCart.CartToken,
                        Items = buyNowCart.Items
                    };
                    SaveBuyNowToCookie(userBuyNowCart, customerId);
                    Response.Cookies.Delete("BuyNowCart_Anonymous");
                }

                // ✅ Ưu tiên returnUrl nếu có
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    _logger.LogInformation("Chuyển hướng đến returnUrl: {ReturnUrl}", returnUrl);
                    return isAjax
                        ? Json(new { success = true, message = "Đăng nhập thành công", redirectUrl = returnUrl })
                        : Redirect(returnUrl);
                }

                // ✅ Nếu có BuyNow cart -> chuyển đến checkout
                if (buyNowCart != null && buyNowCart.Items.Any())
                {
                    _logger.LogInformation("Chuyển hướng đến checkout.html do có giỏ Mua ngay");
                    return isAjax
                        ? Json(new { success = true, message = "Đăng nhập thành công", redirectUrl = "/checkout.html" })
                        : Redirect("/checkout.html");
                }

                // ✅ Mặc định chuyển đến Dashboard
                _logger.LogInformation("Chuyển hướng đến Dashboard do đăng nhập trực tiếp");
                return isAjax
                    ? Json(new { success = true, message = "Đăng nhập thành công", redirectUrl = "/tai-khoan-cua-toi.html" })
                    : RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình đăng nhập");
                _notyfService.Error("Đăng nhập thất bại, vui lòng thử lại");
                bool isAjax = HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                if (isAjax)
                    return Json(new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
                return View(customer);
            }
        }

        [HttpGet]
        [Route("dang-xuat.html", Name = "Logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var customerId = HttpContext.Session.GetString("CustomerId");
                var cart = GetCartFromCookie(customerId);
                if (cart != null && cart.Items.Any() && !string.IsNullOrEmpty(customerId))
                {
                    lock (_cartLock)
                    {
                        SaveCartToCookie(cart, customerId);
                    }
                }
                var buyNowCart = GetBuyNowFromCookie(customerId);
                if (buyNowCart != null && buyNowCart.Items.Any() && !string.IsNullOrEmpty(customerId))
                {
                    lock (_cartLock)
                    {
                        SaveBuyNowToCookie(buyNowCart, customerId);
                    }
                }
                var sessionCartItems = GetSessionCartItems();
                _logger.LogInformation("Session trước khi đăng xuất: CustomerId={CustomerId}, CouponDiscount={CouponDiscount}, SessionCart={SessionCart}",
                    customerId, HttpContext.Session.GetString("CouponDiscount"),
                    JsonSerializer.Serialize(sessionCartItems));

                HttpContext.Session.Remove("CustomerId");
                HttpContext.Session.Remove("CouponDiscount");
                ClearSessionCart();
                var cartToken = Request.Cookies["GlobalCartToken"];
                if (!string.IsNullOrEmpty(cartToken))
                {
                    Response.Cookies.Delete("GlobalCartToken");
                    Response.Cookies.Delete("Cart_Anonymous");
                    Response.Cookies.Delete("BuyNowCart_Anonymous");
                }
                await HttpContext.SignOutAsync("CustomerAuthentication");
                _logger.LogInformation("Đăng xuất thành công cho CustomerId: {CustomerId}", customerId ?? "unknown");
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình đăng xuất");
                _notyfService.Error("Đăng xuất thất bại, vui lòng thử lại");
                return RedirectToAction("Login");
            }
        }

        [HttpPost]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            try
            {
                if (string.IsNullOrEmpty(customerId))
                {
                    _logger.LogWarning("ChangePassword: Session CustomerId không tồn tại");
                    _notyfService.Error("Vui lòng đăng nhập để đổi mật khẩu");
                    return RedirectToAction("Login");
                }

                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    _logger.LogWarning("ChangePassword: Thông tin không hợp lệ: {Errors}", errors);
                    _notyfService.Error("Vui lòng kiểm tra lại thông tin");
                    return RedirectToAction("Dashboard");
                }

                var customer = _context.Customers.Find(Convert.ToInt32(customerId));
                if (customer == null)
                {
                    _logger.LogWarning("ChangePassword: Không tìm thấy khách hàng với CustomerId {CustomerId}", customerId);
                    _notyfService.Error("Tài khoản không tồn tại");
                    return RedirectToAction("Login");
                }

                var currentPass = (model.PasswordNow?.Trim() + (customer.Salt?.Trim() ?? "")).ToMD5();
                if (currentPass != customer.Password)
                {
                    _logger.LogWarning("ChangePassword: Mật khẩu hiện tại không khớp");
                    _notyfService.Error("Mật khẩu hiện tại không đúng");
                    return RedirectToAction("Dashboard");
                }

                string newPass = (model.Password?.Trim() + (customer.Salt?.Trim() ?? "")).ToMD5();
                customer.Password = newPass;
                _context.Update(customer);
                _context.SaveChanges();
                _logger.LogInformation("ChangePassword: Đổi mật khẩu thành công cho CustomerId {CustomerId}", customerId);
                _notyfService.Success("Đổi mật khẩu thành công!");
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đổi mật khẩu cho CustomerId {CustomerId}", customerId);
                _notyfService.Error("Đổi mật khẩu thất bại, vui lòng thử lại");
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult CheckLogin()
        {
            try
            {
                var customerId = HttpContext.Session.GetString("CustomerId");
                bool isLoggedIn = !string.IsNullOrEmpty(customerId) && User.Identity.IsAuthenticated;

                if (isLoggedIn)
                {
                    var customer = _context.Customers.AsNoTracking().FirstOrDefault(c => c.CustomerId == Convert.ToInt32(customerId));
                    if (customer == null || !customer.Active.GetValueOrDefault())
                    {
                        _logger.LogError("Tài khoản không hợp lệ hoặc bị khóa: {CustomerId}", customerId);
                        return Json(new { Success = false, Message = "Tài khoản không hợp lệ hoặc bị khóa" });
                    }
                    _logger.LogInformation("Kiểm tra tài khoản thành công: {CustomerId}", customerId);
                    return Json(new { Success = true, IsAuthenticated = true });
                }

                _logger.LogInformation("Chưa đăng nhập");
                return Json(new { Success = true, IsAuthenticated = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra trạng thái đăng nhập");
                return Json(new { Success = false, Message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("merge-cart")]
        public async Task<IActionResult> MergeCartFromClient([FromBody] SimpleCart clientCart)
        {
            try
            {
                var customerId = HttpContext.Session.GetString("CustomerId");
                if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int parsedCustomerId))
                {
                    _logger.LogWarning("MergeCartFromClient: Không tìm thấy CustomerId trong session");
                    return Json(new { success = false, message = "Vui lòng đăng nhập" });
                }

                _logger.LogInformation("Nhận giỏ hàng từ client cho CustomerId: {CustomerId}, số lượng sản phẩm: {ItemCount}", customerId, clientCart?.Items?.Count ?? 0);

                if (clientCart == null || clientCart.Items == null || !clientCart.Items.Any())
                {
                    _logger.LogInformation("Giỏ hàng từ client trống cho CustomerId: {CustomerId}", customerId);
                    return Json(new { success = true, message = "Không có giỏ hàng để hợp nhất" });
                }

                await MergeClientCart(parsedCustomerId, clientCart);

                _notyfService.Success("Hợp nhất giỏ hàng thành công!");
                return Json(new { success = true, message = "Hợp nhất giỏ hàng thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hợp nhất giỏ hàng từ client");
                return Json(new { success = false, message = $"Lỗi hợp nhất giỏ hàng: {ex.Message}" });
            }
        }

        private void AddToSessionCart(int productDetailId, int amount)
        {
            try
            {
                var key = $"CartItem_{productDetailId}";
                var currentAmount = HttpContext.Session.GetInt32(key) ?? 0;
                HttpContext.Session.SetInt32(key, currentAmount + amount);
                _logger.LogInformation("Đã thêm vào giỏ session: ProductDetailId={ProductDetailId}, Amount={Amount}, Total={Total}",
                    productDetailId, amount, currentAmount + amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm sản phẩm vào giỏ session: ProductDetailId={ProductDetailId}", productDetailId);
            }
        }

        private List<SimpleCartItem> GetSessionCartItems()
        {
            var cartItems = new List<SimpleCartItem>();
            foreach (var key in HttpContext.Session.Keys.Where(k => k.StartsWith("CartItem_")))
            {
                if (int.TryParse(key.Replace("CartItem_", ""), out int productDetailId))
                {
                    var amount = HttpContext.Session.GetInt32(key) ?? 0;
                    if (amount > 0)
                    {
                        cartItems.Add(new SimpleCartItem
                        {
                            ProductDetailId = productDetailId,
                            Amount = amount
                        });
                    }
                }
            }
            return cartItems;
        }

        private void ClearSessionCart()
        {
            foreach (var key in HttpContext.Session.Keys.Where(k => k.StartsWith("CartItem_")))
            {
                HttpContext.Session.Remove(key);
            }
            _logger.LogInformation("Đã xóa giỏ session");
        }

        private async Task<List<CartItem>> SyncCart(int customerId)
        {
            var userCart = GetCartFromCookie(customerId.ToString()) ?? new SimpleCart
            {
                CustomerId = customerId.ToString(),
                CartToken = Guid.NewGuid().ToString(),
                Items = new List<SimpleCartItem>()
            };

            var anonymousCart = GetCartFromCookie("Anonymous") ?? new SimpleCart
            {
                CustomerId = "Anonymous",
                CartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString(),
                Items = new List<SimpleCartItem>()
            };

            var sessionCartItems = GetSessionCartItems();

            foreach (var anonymousItem in anonymousCart.Items.Where(i => i.ProductDetailId > 0))
            {
                var existingItem = userCart.Items.FirstOrDefault(x => x.ProductDetailId == anonymousItem.ProductDetailId);
                if (existingItem != null)
                {
                    existingItem.Amount += anonymousItem.Amount;
                }
                else
                {
                    userCart.Items.Add(anonymousItem);
                }
            }

            foreach (var sessionItem in sessionCartItems.Where(i => i.ProductDetailId > 0))
            {
                var existingItem = userCart.Items.FirstOrDefault(x => x.ProductDetailId == sessionItem.ProductDetailId);
                if (existingItem != null)
                {
                    existingItem.Amount += sessionItem.Amount;
                }
                else
                {
                    userCart.Items.Add(sessionItem);
                }
            }

            var productDetailIds = userCart.Items.Select(i => i.ProductDetailId).ToList();
            var productDetails = await _context.ProductDetails
                .Include(pd => pd.Product)
                .Include(pd => pd.Size)
                .Include(pd => pd.Color)
                .Where(p => productDetailIds.Contains(p.ProductDetailId))
                .Select(pd => new WebShop.ModelViews.ProductDetailDTO
                {
                    ProductDetailId = pd.ProductDetailId,
                    ProductId = pd.ProductId,
                    SizeId = pd.SizeId,
                    SizeName = pd.Size != null ? pd.Size.SizeName : null,
                    ColorId = pd.ColorId,
                    ColorName = pd.Color != null ? pd.Color.ColorName : null,
                    Stock = pd.Stock,
                    ProductActive = pd.Product.Active == true,
                    ProductDetailActive = pd.Active,                    
                    ProductName = pd.Product.ProductName,
                    Price = pd.Product.Price,
                    Thumb = pd.Product.Thumb
                })
                .ToListAsync();

            var productDetailDict = productDetails.ToDictionary(p => p.ProductDetailId);
            var validCartItems = new List<CartItem>();
            var removedItems = new List<string>();

            foreach (var item in userCart.Items)
            {
                if (!productDetailDict.TryGetValue(item.ProductDetailId, out var detail))
                {
                    _logger.LogInformation("Xóa sản phẩm không tồn tại khỏi giỏ: ProductDetailId={ProductDetailId}", item.ProductDetailId);
                    removedItems.Add($"Sản phẩm ID {item.ProductDetailId}");
                    continue;
                }

                if (!detail.ProductActive || detail.Stock < item.Amount || detail.SizeId <= 0 || detail.ColorId <= 0 || detail.Price == null)
                {
                    _logger.LogInformation("Xóa sản phẩm không hợp lệ: ProductName={ProductName}, ProductActive={ProductActive}, Stock={Stock}",
                        detail.ProductName, detail.ProductActive, detail.Stock);
                    removedItems.Add(detail.ProductName);
                    continue;
                }

                validCartItems.Add(new CartItem
                {
                    productDetail = new ProductDetail
                    {
                        ProductDetailId = detail.ProductDetailId,
                        ProductId = detail.ProductId,
                        SizeId = detail.SizeId!.Value,
                        ColorId = detail.ColorId!.Value,
                        Stock = detail.Stock
                    },
                    product = new Product
                    {
                        ProductId = detail.ProductId,
                        ProductName = detail.ProductName,
                        Price = detail.Price!.Value,
                        Thumb = detail.Thumb
                    },
                    amount = item.Amount,
                    ColorName = detail.ColorName,
                    SizeName = detail.SizeName
                });
            }

            if (removedItems.Any())
            {
                _notyfService.Warning($"Đã xóa {removedItems.Count} sản phẩm không hợp lệ khỏi giỏ hàng: {string.Join(", ", removedItems)}");
            }

            userCart.Items = validCartItems.Select(item => new SimpleCartItem
            {
                ProductDetailId = item.productDetail.ProductDetailId,
                Amount = item.amount
            }).ToList();

            lock (_cartLock)
            {
                SaveCartToCookie(userCart, customerId.ToString());
                Response.Cookies.Delete("Cart_Anonymous");
                ClearSessionCart();
            }

            var anonymousBuyNowCart = GetBuyNowFromCookie("Anonymous");
            if (anonymousBuyNowCart != null && anonymousBuyNowCart.Items.Any())
            {
                var userBuyNowCart = GetBuyNowFromCookie(customerId.ToString()) ?? new SimpleCart
                {
                    CustomerId = customerId.ToString(),
                    CartToken = userCart.CartToken,
                    Items = new List<SimpleCartItem>()
                };

                foreach (var item in anonymousBuyNowCart.Items)
                {
                    var existingItem = userBuyNowCart.Items.FirstOrDefault(x => x.ProductDetailId == item.ProductDetailId);
                    if (existingItem != null)
                    {
                        existingItem.Amount = item.Amount;
                    }
                    else
                    {
                        userBuyNowCart.Items.Add(item);
                    }
                }

                lock (_cartLock)
                {
                    SaveBuyNowToCookie(userBuyNowCart, customerId.ToString());
                    Response.Cookies.Delete("BuyNowCart_Anonymous");
                }
            }

            _logger.LogInformation("Đồng bộ giỏ hàng: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, validCartItems.Count);
            return validCartItems;
        }

        private async Task MergeClientCart(int customerId, SimpleCart clientCart)
        {
            var userCart = GetCartFromCookie(customerId.ToString()) ?? new SimpleCart
            {
                CustomerId = customerId.ToString(),
                CartToken = Guid.NewGuid().ToString(),
                Items = new List<SimpleCartItem>()
            };

            foreach (var clientItem in clientCart.Items.Where(i => i.ProductDetailId > 0))
            {
                var productDetail = await _context.ProductDetails
                    .Include(pd => pd.Product)
                    .FirstOrDefaultAsync(p => p.ProductDetailId == clientItem.ProductDetailId);

                if (productDetail == null || !(productDetail.Product.Active ?? false) || !productDetail.Active || productDetail.Stock < clientItem.Amount)
                {
                    _logger.LogWarning("Sản phẩm không hợp lệ khi hợp nhất: ProductDetailId={ProductDetailId}, ProductActive={ProductActive}, ProductDetailActive={ProductDetailActive}, Stock={Stock}",
                        clientItem.ProductDetailId, productDetail?.Product.Active, productDetail?.Active, productDetail?.Stock);
                    continue;
                }

                var existingItem = userCart.Items.FirstOrDefault(x => x.ProductDetailId == clientItem.ProductDetailId);
                if (existingItem != null)
                {
                    existingItem.Amount += clientItem.Amount;
                }
                else
                {
                    userCart.Items.Add(clientItem);
                }
            }

            lock (_cartLock)
            {
                SaveCartToCookie(userCart, customerId.ToString());
            }

            _logger.LogInformation("Hợp nhất giỏ hàng từ client: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, userCart.Items.Count);
        }

        private SimpleCart GetCartFromCookie(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return null;

            var cookieKey = $"Cart_{customerId}";
            var cookieData = Request.Cookies[cookieKey];
            if (!string.IsNullOrEmpty(cookieData))
            {
                try
                {
                    var decryptedData = _protector.Unprotect(Convert.FromBase64String(cookieData));
                    var cart = JsonSerializer.Deserialize<SimpleCart>(System.Text.Encoding.UTF8.GetString(decryptedData));
                    if (cart != null)
                    {
                        cart.CustomerId = customerId;
                        cart.Items = cart.Items ?? new List<SimpleCartItem>();
                        _logger.LogInformation("Khôi phục giỏ hàng từ cookie: CustomerId={CustomerId}, Items={ItemCount}", customerId, cart.Items.Count);
                        return cart;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi khôi phục giỏ hàng từ cookie: CustomerId={CustomerId}", customerId);
                    Response.Cookies.Delete(cookieKey);
                }
            }
            return null;
        }

        private SimpleCart GetBuyNowFromCookie(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return null;

            var cookieKey = $"BuyNowCart_{customerId}";
            var cookieData = Request.Cookies[cookieKey];
            if (!string.IsNullOrEmpty(cookieData))
            {
                try
                {
                    var decryptedData = _protector.Unprotect(Convert.FromBase64String(cookieData));
                    var cart = JsonSerializer.Deserialize<SimpleCart>(System.Text.Encoding.UTF8.GetString(decryptedData));
                    if (cart != null)
                    {
                        cart.CustomerId = customerId;
                        cart.Items = cart.Items ?? new List<SimpleCartItem>();
                        _logger.LogInformation("Khôi phục giỏ Mua ngay từ cookie: CustomerId={CustomerId}, Items={ItemCount}", customerId, cart.Items.Count);
                        return cart;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi khôi phục giỏ Mua ngay từ cookie: CustomerId={CustomerId}", customerId);
                    Response.Cookies.Delete(cookieKey);
                }
            }
            return null;
        }

        private void SaveCartToCookie(SimpleCart cart, string customerId)
        {
            var cookieKey = $"Cart_{customerId}";
            try
            {
                if (string.IsNullOrEmpty(cart.CartToken))
                {
                    cart.CartToken = Guid.NewGuid().ToString();
                }

                var serializedCart = JsonSerializer.Serialize(cart, new JsonSerializerOptions { WriteIndented = false });
                var encryptedData = _protector.Protect(System.Text.Encoding.UTF8.GetBytes(serializedCart));
                var base64Data = Convert.ToBase64String(encryptedData);

                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddYears(10),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                };

                Response.Cookies.Append(cookieKey, base64Data, cookieOptions);
                Response.Cookies.Append("GlobalCartToken", cart.CartToken, cookieOptions);
                _logger.LogInformation("Lưu giỏ hàng vào cookie: CustomerId={CustomerId}, Items={ItemCount}", customerId, cart.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu giỏ hàng vào cookie: CustomerId={CustomerId}", customerId);
            }
        }

        private void SaveBuyNowToCookie(SimpleCart cart, string customerId)
        {
            var cookieKey = $"BuyNowCart_{customerId}";
            try
            {
                if (string.IsNullOrEmpty(cart.CartToken))
                {
                    cart.CartToken = Guid.NewGuid().ToString();
                }

                var serializedCart = JsonSerializer.Serialize(cart, new JsonSerializerOptions { WriteIndented = false });
                var encryptedData = _protector.Protect(System.Text.Encoding.UTF8.GetBytes(serializedCart));
                var base64Data = Convert.ToBase64String(encryptedData);

                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddHours(1),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                };

                Response.Cookies.Append(cookieKey, base64Data, cookieOptions);
                Response.Cookies.Append("GlobalCartToken", cart.CartToken, cookieOptions);
                _logger.LogInformation("Lưu giỏ Mua ngay vào cookie: CustomerId={CustomerId}, Items={ItemCount}", customerId, cart.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu giỏ Mua ngay vào cookie: CustomerId={CustomerId}", customerId);
            }
        }
    }
}