using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Areas.Admin.Models;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminCustomerPointsController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notifyService;

        public AdminCustomerPointsController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
            System.Diagnostics.Debug.WriteLine("Kết nối cơ sở dữ liệu: " + _context.Database.CanConnect());
        }

        // GET: Admin/AdminCustomerPoints/Index
        public async Task<IActionResult> Index(string searchString)
        {
            try
            {
                // Lấy tất cả khách hàng
                var customers = await _context.Customers
                    .Select(c => new { c.CustomerId, c.FullName })
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"Index: Số khách hàng thô: {customers.Count}");
                foreach (var c in customers)
                {
                    System.Diagnostics.Debug.WriteLine($"Customer: CustomerId={c.CustomerId}, FullName={c.FullName}");
                }

                // Lọc theo searchString nếu có
                if (!string.IsNullOrEmpty(searchString))
                {
                    customers = customers
                        .Where(c => (c.FullName != null && c.FullName.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                            || (c.FullName == null && $"Khách hàng #{c.CustomerId}".Contains(searchString, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    System.Diagnostics.Debug.WriteLine($"Index: Số khách hàng sau khi lọc: {customers.Count}, Chuỗi tìm kiếm: {searchString}");
                }

                // Lấy dữ liệu check-in và reward từ cơ sở dữ liệu
                var checkInData = await _context.CheckInHistory
                    .GroupBy(ch => ch.CustomerId)
                    .Select(g => new { CustomerId = g.Key, TotalCheckIns = g.Count() })
                    .ToListAsync();

                var rewardData = await _context.RewardHistories
                    .Where(rh => rh.IsConfirmed)
                    .GroupBy(rh => rh.CustomerId)
                    .Select(g => new { CustomerId = g.Key, TotalRewardsRedeemed = g.Count() })
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"Index: Số bản ghi check-in: {checkInData.Count}");
                System.Diagnostics.Debug.WriteLine($"Index: Số bản ghi reward: {rewardData.Count}");

                // Tạo danh sách kết quả
                var result = customers.Select(c => new CustomerPointsSummaryViewModel
                {
                    CustomerId = c.CustomerId,
                    FullName = c.FullName ?? $"Khách hàng #{c.CustomerId}",
                    TotalCheckIns = checkInData.FirstOrDefault(ch => ch.CustomerId == c.CustomerId)?.TotalCheckIns ?? 0,
                    TotalRewardsRedeemed = rewardData.FirstOrDefault(rh => rh.CustomerId == c.CustomerId)?.TotalRewardsRedeemed ?? 0
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"Index: Tìm thấy {result.Count} khách hàng.");
                foreach (var item in result)
                {
                    System.Diagnostics.Debug.WriteLine($"CustomerId={item.CustomerId}, FullName={item.FullName}, TotalCheckIns={item.TotalCheckIns}, TotalRewardsRedeemed={item.TotalRewardsRedeemed}");
                }

                ViewData["CurrentFilter"] = searchString;
                return View(result);
            }
            catch (Exception ex)
            {
                _notifyService.Error($"Lỗi khi tải dữ liệu: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Index: Lỗi - {ex.Message}, StackTrace: {ex.StackTrace}");
                return View(new List<CustomerPointsSummaryViewModel>());
            }
        }

        // GET: Admin/AdminCustomerPoints/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _notifyService.Error("ID khách hàng không hợp lệ!");
                System.Diagnostics.Debug.WriteLine("Details: ID khách hàng là null");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var customer = await _context.Customers
                    .Include(c => c.RewardHistories)
                    .Include(c => c.CheckInHistory)
                    .FirstOrDefaultAsync(c => c.CustomerId == id);

                if (customer == null)
                {
                    _notifyService.Error($"Không tìm thấy khách hàng với ID {id}!");
                    System.Diagnostics.Debug.WriteLine($"Details: Không tìm thấy khách hàng với ID {id}");
                    return RedirectToAction(nameof(Index));
                }

                var model = new CustomerPointsDetailsViewModel
                {
                    CustomerId = customer.CustomerId,
                    FullName = customer.FullName ?? $"Khách hàng #{customer.CustomerId}",
                    CheckIns = customer.CheckInHistory != null ? customer.CheckInHistory.Select(ch => new CheckInItem
                    {
                        CheckInHistoryId = ch.CheckInHistoryId,
                        CheckInDate = ch.CheckInDate,
                        PointsEarned = ch.PointsEarned
                    }).ToList() : new List<CheckInItem>(),
                    RedeemedRewards = customer.RewardHistories != null ? customer.RewardHistories.Where(rh => rh.IsConfirmed).Select(rh => new RewardItem
                    {
                        Id = rh.Id,
                        RewardName = rh.RewardName,
                        RedeemedAt = rh.RedeemedAt,
                        PointsUsed = rh.PointsUsed,
                        IsConfirmed = rh.IsConfirmed
                    }).ToList() : new List<RewardItem>(),
                    UnconfirmedRewards = customer.RewardHistories != null ? customer.RewardHistories.Where(rh => !rh.IsConfirmed).Select(rh => new RewardItem
                    {
                        Id = rh.Id,
                        RewardName = rh.RewardName,
                        RedeemedAt = rh.RedeemedAt,
                        PointsUsed = rh.PointsUsed,
                        IsConfirmed = rh.IsConfirmed
                    }).ToList() : new List<RewardItem>()
                };

                System.Diagnostics.Debug.WriteLine($"Details: CustomerId={model.CustomerId}, FullName={model.FullName}, CheckInsCount={model.CheckIns.Count}, RedeemedRewardsCount={model.RedeemedRewards.Count}, UnconfirmedRewardsCount={model.UnconfirmedRewards.Count}");
                foreach (var checkIn in model.CheckIns)
                {
                    System.Diagnostics.Debug.WriteLine($"CheckIn: ID={checkIn.CheckInHistoryId}, Date={checkIn.CheckInDate}, Points={checkIn.PointsEarned}");
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _notifyService.Error($"Lỗi khi tải chi tiết khách hàng ID {id}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Lỗi trong Details (CustomerId={id}): {ex.Message}, StackTrace: {ex.StackTrace}");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/AdminCustomerPoints/ConfirmReward/5
        [HttpPost]
        public async Task<IActionResult> ConfirmReward(int rewardId)
        {
            try
            {
                var reward = await _context.RewardHistories.FindAsync(rewardId);
                if (reward == null)
                {
                    _notifyService.Error("Không tìm thấy yêu cầu đổi quà!");
                    return RedirectToAction(nameof(Index));
                }

                reward.IsConfirmed = true;
                await _context.SaveChangesAsync();
                _notifyService.Success("Đã xác nhận giao quà thành công!");
                return RedirectToAction(nameof(Details), new { id = reward.CustomerId });
            }
            catch (Exception ex)
            {
                _notifyService.Error($"Lỗi khi xác nhận quà: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"ConfirmReward: Lỗi - {ex.Message}, StackTrace: {ex.StackTrace}");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}