using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminLoginController : Controller
    {
        private readonly webshopContext _context;
        public INotyfService _notifyService { get; }

        public AdminLoginController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        [HttpGet]
        public IActionResult AdminLogin()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AdminLogin([FromForm] Account account)
        {
            Console.WriteLine($"Đăng nhập với Email: {account.Email}, Password: {account.Password}"); // Logging để debug
            var user = _context.Accounts
                .Where(u => u.Email == account.Email)
                .SingleOrDefault();

            // Kiểm tra tài khoản và mật khẩu (plaintext)
            if (user == null || user.Password != account.Password)
            {
                _notifyService.Error($"Đăng nhập thất bại. Email: {account.Email}");
                TempData["ErrorMessage"] = "Sai thông tin tài khoản hoặc mật khẩu";
                return RedirectToAction("AdminLogin");
            }

            // Kiểm tra tài khoản bị khóa
            if (user.RoleId == 3)
            {
                TempData["ErrorMessage"] = "Tài khoản bị khóa, vui lòng liên hệ quản trị viên";
                return RedirectToAction("AdminLogin");
            }

            // Kiểm tra vai trò admin (giả sử RoleId == 1 là admin)
            if (user.RoleId != 1)
            {
                TempData["ErrorMessage"] = "Tài khoản không có quyền truy cập khu vực admin";
                return RedirectToAction("AdminLogin");
            }

            // Tạo claims cho người dùng
            var userClaims = new List<Claim>
            {
                new Claim("FullName", user.FullName ?? string.Empty),
                new Claim("Email", user.Email ?? string.Empty),
                new Claim("Phone", user.Phone.ToString() ?? string.Empty),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? string.Empty)
            };

            var userIdentity = new ClaimsIdentity(userClaims, "AdminAuthentication");
            var userPrincipal = new ClaimsPrincipal(userIdentity);

            // Đăng nhập với scheme AdminAuthentication
            await HttpContext.SignInAsync("AdminAuthentication", userPrincipal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1)
            });

            HttpContext.Session.SetString("AccountId", user.AccountId.ToString());
            _notifyService.Success("Đăng nhập thành công");

            return RedirectToAction("Index", "HomeAdmin");
        }

        [AllowAnonymous]
        public IActionResult AccessDeny()
        {
            return View();
        }

        public IActionResult GetUser()
        {
            var user = HttpContext.User;
            if (!user.Identity.IsAuthenticated)
            {
                return RedirectToAction("AdminLogin");
            }

            string email = user.Claims.FirstOrDefault(c => c.Type == "Email")?.Value;
            string name = user.Claims.FirstOrDefault(c => c.Type == "FullName")?.Value;
            string phone = user.Claims.FirstOrDefault(c => c.Type == "Phone")?.Value;
            string roleId = user.Claims.FirstOrDefault(c => c.Type == "RoleId")?.Value;

            ViewBag.Email = email;
            ViewBag.Name = name;
            ViewBag.Phone = phone;
            ViewBag.RoleId = roleId;
            return View();
        }

        public async Task<IActionResult> LogOut()
        {
            await HttpContext.SignOutAsync("AdminAuthentication");
            HttpContext.Session.Clear();
            _notifyService.Success("Đăng xuất thành công");
            return RedirectToAction("AdminLogin");
        }
    }
}