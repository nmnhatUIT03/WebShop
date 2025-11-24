using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Models;

namespace WebShop.Controllers
{
    [Authorize(AuthenticationSchemes = "CustomerAuthentication")]
    public class CheckInController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notyfService;
        private readonly ILogger<CheckInController> _logger;

        public CheckInController(webshopContext context, INotyfService notyfService, ILogger<CheckInController> logger)
        {
            _context = context;
            _notyfService = notyfService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int parsedCustomerId))
            {
                _logger.LogWarning("Session CustomerId không hợp lệ trong CheckIn/Index");
                _notyfService.Error("Vui lòng đăng nhập để điểm danh!");
                return RedirectToAction("Login", "Accounts");
            }

            var customer = await _context.Customers
                .AsNoTracking()
                .Include(c => c.CheckInHistory)
                .FirstOrDefaultAsync(x => x.CustomerId == parsedCustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Không tìm thấy khách hàng với CustomerId {CustomerId}", customerId);
                _notyfService.Error("Tài khoản không tồn tại!");
                return RedirectToAction("Login", "Accounts");
            }

            ViewBag.CheckIns = customer.CheckInHistory?.OrderByDescending(c => c.CheckInDate).ToList();

            return View(customer);
        }

        [HttpPost]
        public async Task<IActionResult> CheckIn()
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int parsedCustomerId))
            {
                _logger.LogWarning("Session CustomerId không hợp lệ trong CheckIn/CheckIn");
                _notyfService.Error("Vui lòng đăng nhập để điểm danh!");
                return RedirectToAction("Login", "Accounts");
            }

            var customer = await _context.Customers.FindAsync(parsedCustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Không tìm thấy khách hàng với CustomerId {CustomerId}", customerId);
                _notyfService.Error("Tài khoản không tồn tại!");
                return RedirectToAction("Login", "Accounts");
            }

            var result = await ProcessCheckIn(customer);
            if (result.Success)
            {
                _notyfService.Success(result.Message);
            }
            else
            {
                _notyfService.Warning(result.Message);
            }

            return RedirectToAction("Index");
        }

        private async Task<(bool Success, string Message, int PointsEarned)> ProcessCheckIn(Customer customer)
        {
            if (customer.LastCheckInDate.HasValue && customer.LastCheckInDate.Value.Date == DateTime.Today)
            {
                _logger.LogInformation("Khách hàng {CustomerId} đã điểm danh hôm nay", customer.CustomerId);
                return (false, "Bạn đã điểm danh hôm nay!", 0);
            }

            int pointsEarned = 5; // Mặc định 5 điểm mỗi ngày
            // Tùy chọn: Tăng điểm nếu điểm danh liên tiếp
            int consecutiveDays = CalculateConsecutiveDays(customer.LastCheckInDate);
            if (consecutiveDays > 0 && consecutiveDays % 5 == 0)
            {
                pointsEarned += 5; // Thêm 5 điểm cho ngày thứ 5, 10, 15, v.v.
                _logger.LogInformation("Khách hàng {CustomerId} nhận thêm 5 điểm do điểm danh liên tiếp", customer.CustomerId);
            }

            customer.Points += pointsEarned;
            customer.CheckInCount += 1;
            customer.LastCheckInDate = DateTime.Now;

            var checkIn = new CheckInHistory
            {
                CustomerId = customer.CustomerId,
                CheckInDate = DateTime.Now,
                PointsEarned = pointsEarned
            };
            _context.CheckInHistory.Add(checkIn);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Điểm danh thành công cho CustomerId {CustomerId}, Điểm nhận được: {PointsEarned}", customer.CustomerId, pointsEarned);
                return (true, $"Điểm danh thành công! Bạn nhận được {pointsEarned} điểm.", pointsEarned);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu điểm danh cho CustomerId {CustomerId}", customer.CustomerId);
                return (false, "Lỗi hệ thống khi điểm danh, vui lòng thử lại!", 0);
            }
        }

        private int CalculateConsecutiveDays(DateTime? lastCheckIn)
        {
            if (!lastCheckIn.HasValue) return 0;
            var daysDiff = (DateTime.Today - lastCheckIn.Value.Date).Days;
            return daysDiff == 1 ? 1 : 0; // Chỉ tính liên tiếp nếu cách 1 ngày
        }

        [HttpPost]
        public async Task<IActionResult> RedeemReward(int rewardCost)
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int parsedCustomerId))
            {
                _notyfService.Error("Vui lòng đăng nhập!");
                return RedirectToAction("Login", "Accounts");
            }

            var customer = await _context.Customers
                .Include(c => c.RewardHistories) // Đảm bảo tải RewardHistories
                .FirstOrDefaultAsync(c => c.CustomerId == parsedCustomerId);
            if (customer == null)
            {
                _notyfService.Error("Tài khoản không tồn tại!");
                return RedirectToAction("Login", "Accounts");
            }

            if (customer.Points < rewardCost)
            {
                _notyfService.Warning("Bạn không đủ Xu để đổi quà này!");
                return RedirectToAction("Index");
            }

            // Trừ điểm
            customer.Points -= rewardCost;

            // Lưu lịch sử đổi quà
            var rewardLog = new RewardHistory
            {
                CustomerId = customer.CustomerId,
                RewardName = $"Đổi quà trị giá {rewardCost} Xu",
                RedeemedAt = DateTime.Now,
                PointsUsed = rewardCost
            };
            _context.RewardHistories.Add(rewardLog); // Sửa lại thành RewardHistories

            try
            {
                await _context.SaveChangesAsync();
                _notyfService.Success($"Đổi quà thành công! Bạn đã sử dụng {rewardCost} Xu.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đổi quà cho CustomerId {CustomerId}", customer.CustomerId);
                _notyfService.Error("Đổi quà thất bại! Vui lòng thử lại sau.");
                // Hoàn lại điểm nếu lỗi
                customer.Points += rewardCost;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
        [HttpPost]
        public async Task<IActionResult> ResetCycle()
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int parsedCustomerId))
            {
                _notyfService.Error("Vui lòng đăng nhập!");
                return RedirectToAction("Login", "Accounts");
            }

            var customer = await _context.Customers.FindAsync(parsedCustomerId);
            if (customer == null)
            {
                _notyfService.Error("Tài khoản không tồn tại!");
                return RedirectToAction("Login", "Accounts");
            }

            // Reset chu kỳ
            customer.CheckInCount = 0;
            customer.LastCheckInDate = null;
            try
            {
                await _context.SaveChangesAsync();
                _notyfService.Success("Chu kỳ điểm danh đã được reset thành công!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi reset chu kỳ cho CustomerId {CustomerId}", customer.CustomerId);
                _notyfService.Error("Lỗi khi reset chu kỳ, vui lòng thử lại!");
            }

            return RedirectToAction("Index");
        }
    }
}