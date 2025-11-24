using AspNetCoreHero.ToastNotification.Abstractions;
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
using System.Threading.Tasks;
using WebShop.Models;
using Newtonsoft.Json;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminOrdersController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notifyService;
        private readonly ILogger<AdminOrdersController> _logger;

        public AdminOrdersController(webshopContext context, INotyfService notifyService, ILogger<AdminOrdersController> logger)
        {
            _context = context;
            _notifyService = notifyService;
            _logger = logger;
        }

        // GET: Admin/AdminOrders
        public IActionResult Index(int? page, string search, int? status)
        {
            var pageNumber = page ?? 1;
            var pageSize = 20;
            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.TransactStatus)
                .Include(o => o.Promotion)
                .Include(o => o.Voucher)
                .AsNoTracking()
                .Select(o => new Order
                {
                    OrderId = o.OrderId,
                    CustomerId = o.CustomerId,
                    ReceiverName = o.ReceiverName,
                    OrderDate = o.OrderDate,
                    TransactStatus = o.TransactStatus,
                    TransactStatusId = o.TransactStatusId,
                    Paid = o.Paid,
                    Deleted = o.Deleted,
                    TotalMoney = o.TotalMoney ?? 0,
                    Promotion = o.Promotion,
                    Voucher = o.Voucher
                });

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o => o.ReceiverName != null && o.ReceiverName.Contains(search));
            }

            if (status.HasValue && status > 0)
            {
                query = query.Where(o => o.TransactStatusId == status);
            }

            query = query.OrderByDescending(x => x.OrderDate);
            PagedList<Order> models = new PagedList<Order>(query, pageNumber, pageSize);

            ViewBag.CurrentPage = pageNumber;
            ViewBag.Search = search;
            ViewBag.Status = status?.ToString();
            ViewBag.Statuses = _context.TransactStatuses.ToList();
            return View(models);
        }

        // POST: Admin/AdminOrders/FindOrder
        [HttpPost]
        public IActionResult FindOrder(string search, int? status, int? page = 1)
        {
            try
            {
                var pageNumber = page ?? 1;
                var pageSize = 20;
                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.TransactStatus)
                    .Include(o => o.Promotion)
                    .Include(o => o.Voucher)
                    .AsNoTracking()
                    .Select(o => new Order
                    {
                        OrderId = o.OrderId,
                        CustomerId = o.CustomerId,
                        ReceiverName = o.ReceiverName,
                        OrderDate = o.OrderDate,
                        TransactStatus = o.TransactStatus,
                        TransactStatusId = o.TransactStatusId,
                        Paid = o.Paid,
                        Deleted = o.Deleted,
                        TotalMoney = o.TotalMoney ?? 0,
                        Promotion = o.Promotion,
                        Voucher = o.Voucher
                    });

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(o => o.ReceiverName != null && o.ReceiverName.Contains(search));
                }

                if (status.HasValue && status > 0)
                {
                    query = query.Where(o => o.TransactStatusId == status);
                }

                query = query.OrderByDescending(x => x.OrderDate);
                var orders = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

                if (orders.Any())
                {
                    var html = "";
                    int index = (pageNumber - 1) * pageSize + 1;
                    foreach (var item in orders)
                    {
                        string badgeClass = item.TransactStatusId switch
                        {
                            1 => "badge-primary",
                            2 => "badge-info",
                            3 => "badge-success",
                            4 => "badge-success",
                            5 => "badge-danger",
                            _ => "badge-secondary"
                        };

                        html += $"<tr>" +
                                $"<td>{index}</td>" +
                                $"<td>{item.OrderId}</td>" +
                                $"<td>{(item.ReceiverName ?? "N/A")}</td>" +
                                $"<td>{(item.CustomerId?.ToString() ?? "N/A")}</td>" +
                                $"<td>{(item.OrderDate?.ToString("dd/MM/yyyy HH:mm") ?? "N/A")}</td>" +
                                $"<td>{(item.Promotion?.PromotionName ?? "Không có")}</td>" +
                                $"<td>{(item.Voucher?.VoucherCode ?? "Không có")}</td>" +
                                $"<td>{(item.TotalMoney ?? 0).ToString("#,##0")} VNĐ</td>" +
                                $"<td><div class='badge {badgeClass}'>{item.TransactStatus?.Status ?? "N/A"}</div></td>" +
                                $"<td>" +
                                $"<a class='btn btn-primary btn-sm' href='/Admin/AdminOrders/Details/{item.OrderId}'><i class='far fa-eye'></i></a>" +
                                $"<a class='btn btn-secondary btn-sm' href='/Admin/AdminOrders/ChangeStatus/{item.OrderId}'><i class='fas fa-sync'></i></a>" +
                                $"<a class='btn btn-secondary btn-sm' href='/Admin/AdminOrders/Edit/{item.OrderId}'><i class='far fa-edit'></i></a>" +
                                $"<a class='btn btn-danger btn-sm delete-order' href='#' data-id='{item.OrderId}' onclick='confirmDelete({item.OrderId})'><i class='far fa-trash-alt'></i></a>" +
                                $"</td>" +
                                $"</tr>";
                        index++;
                    }
                    return Json(new { success = true, html });
                }
                else
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng nào!" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FindOrder: Lỗi khi tìm kiếm đơn hàng: {Message}", ex.Message);
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // GET: Admin/AdminOrders/FilterOrder
        [HttpGet]
        public IActionResult FilterOrder(int? status, string search, int? page = 1)
        {
            try
            {
                var pageNumber = page ?? 1;
                var pageSize = 20;
                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.TransactStatus)
                    .Include(o => o.Promotion)
                    .Include(o => o.Voucher)
                    .AsNoTracking()
                    .Select(o => new Order
                    {
                        OrderId = o.OrderId,
                        CustomerId = o.CustomerId,
                        ReceiverName = o.ReceiverName,
                        OrderDate = o.OrderDate,
                        TransactStatus = o.TransactStatus,
                        TransactStatusId = o.TransactStatusId,
                        Paid = o.Paid,
                        Deleted = o.Deleted,
                        TotalMoney = o.TotalMoney ?? 0,
                        Promotion = o.Promotion,
                        Voucher = o.Voucher
                    });

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(o => o.ReceiverName != null && o.ReceiverName.Contains(search));
                }

                if (status.HasValue && status > 0)
                {
                    query = query.Where(o => o.TransactStatusId == status);
                }

                query = query.OrderByDescending(x => x.OrderDate);
                var orders = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

                if (orders.Any())
                {
                    var html = "";
                    int index = (pageNumber - 1) * pageSize + 1;
                    foreach (var item in orders)
                    {
                        string badgeClass = item.TransactStatusId switch
                        {
                            1 => "badge-primary",
                            2 => "badge-info",
                            3 => "badge-success",
                            4 => "badge-success",
                            5 => "badge-danger",
                            _ => "badge-secondary"
                        };

                        html += $"<tr>" +
                                $"<td>{index}</td>" +
                                $"<td>{item.OrderId}</td>" +
                                $"<td>{(item.ReceiverName ?? "N/A")}</td>" +
                                $"<td>{(item.CustomerId?.ToString() ?? "N/A")}</td>" +
                                $"<td>{(item.OrderDate?.ToString("dd/MM/yyyy HH:mm") ?? "N/A")}</td>" +
                                $"<td>{(item.Promotion?.PromotionName ?? "Không có")}</td>" +
                                $"<td>{(item.Voucher?.VoucherCode ?? "Không có")}</td>" +
                                $"<td>{(item.TotalMoney ?? 0).ToString("#,##0")} VNĐ</td>" +
                                $"<td><div class='badge {badgeClass}'>{item.TransactStatus?.Status ?? "N/A"}</div></td>" +
                                $"<td>" +
                                $"<a class='btn btn-primary btn-sm m-r-5' href='/Admin/AdminOrders/Details/{item.OrderId}'><i class='far fa-eye'></i></a>" +
                                $"<a class='btn btn-secondary btn-sm m-r-5' href='/Admin/AdminOrders/ChangeStatus/{item.OrderId}'><i class='fas fa-sync'></i></a>" +
                                $"<a class='btn btn-secondary btn-sm m-r-5' href='/Admin/AdminOrders/Edit/{item.OrderId}'><i class='far fa-edit'></i></a>" +
                                $"<a class='btn btn-danger btn-sm m-r-5' href='/Admin/AdminOrders/Delete/{item.OrderId}'><i class='far fa-trash-alt'></i></a>" +
                                $"</td>" +
                                $"</tr>";
                        index++;
                    }
                    return Json(new { success = true, html });
                }
                else
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng nào!" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FilterOrder: Lỗi khi lọc đơn hàng: {Message}", ex.Message);
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // GET: Admin/AdminOrders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Details: OrderId is null");
                _notifyService.Error("Không tìm thấy đơn hàng!");
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.TransactStatus)
                .Include(o => o.Promotion)
                .Include(o => o.Voucher)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Product)
                    .ThenInclude(p => p.Cat)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Size)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Color)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null)
            {
                _logger.LogWarning("Details: Order not found for OrderId={OrderId}", id);
                _notifyService.Error("Đơn hàng không tồn tại!");
                return NotFound();
            }

            ViewBag.ChiTiet = order.OrderDetails.OrderBy(x => x.OrderDetailId).ToList();
            return View(order);
        }

        // GET: Admin/AdminOrders/ChangeStatus/5
        public async Task<IActionResult> ChangeStatus(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("ChangeStatus: OrderId is null");
                _notifyService.Error("Không tìm thấy đơn hàng!");
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.TransactStatus)
                .Include(o => o.Promotion)
                .Include(o => o.Voucher)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Size)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Color)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null)
            {
                _logger.LogWarning("ChangeStatus: Order not found for OrderId={OrderId}", id);
                _notifyService.Error("Đơn hàng không tồn tại!");
                return NotFound();
            }

            ViewData["TrangThai"] = new SelectList(_context.TransactStatuses, "TransactStatusId", "Status", order.TransactStatusId);
            ViewBag.ChiTiet = order.OrderDetails.OrderBy(x => x.OrderDetailId).ToList();
            return View(order);
        }

        // POST: Admin/AdminOrders/ChangeStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, [Bind("OrderId,TransactStatusId,Deleted,Paid")] Order order)
        {
            if (id != order.OrderId)
            {
                _logger.LogWarning("ChangeStatus: OrderId mismatch. Provided={Provided}, Expected={Expected}", id, order.OrderId);
                _notifyService.Error("ID đơn hàng không khớp!");
                return NotFound();
            }

            try
            {
                var donhang = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(m => m.OrderId == id);
                if (donhang == null)
                {
                    _logger.LogWarning("ChangeStatus: Order not found for OrderId={OrderId}", id);
                    _notifyService.Error("Đơn hàng không tồn tại!");
                    return NotFound();
                }

                donhang.Paid = order.Paid;
                donhang.Deleted = order.Deleted;
                donhang.TransactStatusId = order.TransactStatusId;

                if (order.Paid)
                {
                    donhang.PaymentDate = DateTime.Now;
                }
                if (order.TransactStatusId == 5) // Giả sử 5 là trạng thái hủy
                {
                    donhang.Deleted = true;
                }
                if (order.TransactStatusId == 3) // Giả sử 3 là trạng thái đang giao
                {
                    donhang.ShipDate = DateTime.Now;
                }

                donhang.TotalMoney = donhang.OrderDetails.Sum(od => (od.Amount ?? 0) * (od.Price ?? 0));

                if (donhang.TotalMoney < 0)
                {
                    donhang.TotalMoney = 0;
                }

                if (donhang.TransactStatusId == 4 && donhang.Paid)
                {
                    donhang.TotalMoney = 0;
                }

                _context.Update(donhang);
                await _context.SaveChangesAsync();
                _logger.LogInformation("ChangeStatus: Successfully updated order status for OrderId={OrderId}", id);
                _notifyService.Success("Cập nhật trạng thái đơn hàng thành công!");
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "ChangeStatus: Concurrency error for OrderId={OrderId}", id);
                if (!OrderExists(order.OrderId))
                {
                    _notifyService.Error("Đơn hàng không tồn tại!");
                    return NotFound();
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangeStatus: Error updating order status for OrderId={OrderId}: {Message}", id, ex.Message);
                _notifyService.Error($"Lỗi khi cập nhật trạng thái: {ex.Message}");
                ViewData["TrangThai"] = new SelectList(_context.TransactStatuses, "TransactStatusId", "Status", order.TransactStatusId);
                ViewBag.ChiTiet = await _context.OrderDetails
                    .Include(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Product)
                    .ThenInclude(p => p.Cat)
                    .Include(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Size)
                    .Include(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Color)
                    .Where(od => od.OrderId == id)
                    .OrderBy(od => od.OrderDetailId)
                    .ToListAsync();
                return View(order);
            }
        }

        // GET: Admin/AdminOrders/Create
        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "CustomerId", "FullName");
            ViewData["TransactStatusId"] = new SelectList(_context.TransactStatuses, "TransactStatusId", "Status");
            ViewData["PromotionId"] = new SelectList(_context.Promotions, "PromotionId", "PromotionName");
            ViewData["VoucherId"] = new SelectList(_context.Vouchers, "VoucherId", "VoucherCode");

            var products = _context.Products
                .Where(p => (p.Active ?? false))
                .Select(p => new { p.ProductId, p.ProductName })
                .ToList();
            ViewBag.Products = products.Any()
                ? new SelectList(products, "ProductId", "ProductName")
                : new SelectList(new List<SelectListItem>(), "Value", "Text");
            _logger.LogInformation("Create: Loaded {Count} products for ViewBag.Products", products.Count);

            var variants = _context.ProductDetails
                .Where(pd => pd.Active && (pd.Product.Active ?? false))
                .Include(pd => pd.Size)
                .Include(pd => pd.Color)
                .Include(pd => pd.Product)
                .Select(pd => new
                {
                    ProductDetailId = pd.ProductDetailId,
                    ProductId = pd.ProductId,
                    SizeId = pd.SizeId ?? 0,
                    SizeName = pd.Size != null ? pd.Size.SizeName : "Không xác định",
                    ColorId = pd.ColorId ?? 0,
                    ColorName = pd.Color != null ? pd.Color.ColorName : "Không xác định",
                    Stock = pd.Stock ?? 0,
                    Price = pd.Product.Price ?? pd.Product.Price ?? 0,
                    ProductName = pd.Product.ProductName
                })
                .Distinct()
                .ToList();
            _logger.LogInformation("Create: Loaded {Count} variants for ViewBag.Variants", variants.Count);

            try
            {
                ViewBag.Variants = JsonConvert.SerializeObject(variants, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Error = (sender, args) =>
                    {
                        _logger.LogError("Create: Error serializing variants: {Error}", args.ErrorContext.Error.Message);
                        args.ErrorContext.Handled = true;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create: Failed to serialize variants: {Message}", ex.Message);
                ViewBag.Variants = JsonConvert.SerializeObject(new List<object>());
            }

            return View();
        }

        // POST: Admin/AdminOrders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [FromForm] Order order,
            [FromForm] List<int> ProductDetailIds,
            [FromForm] List<int> Quantities,
            [FromForm] List<decimal> Prices)
        {
            _logger.LogInformation("Create: Starting action. Order: {Order}, ProductDetailIds: {ProductDetailIds}, Quantities: {Quantities}, Prices: {Prices}",
                JsonConvert.SerializeObject(order), ProductDetailIds, Quantities, Prices);

            ProductDetailIds = ProductDetailIds?.Where(id => id != 0).ToList() ?? new List<int>();
            Quantities = Quantities?.Where(q => q > 0).ToList() ?? new List<int>();
            Prices = Prices?.Where(p => p > 0).ToList() ?? new List<decimal>();

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Create: Invalid model state: {Errors}", string.Join(", ", errors));
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = $"Dữ liệu đầu vào không hợp lệ! Chi tiết: {string.Join(", ", errors)}" });
                }
                _notifyService.Error($"Dữ liệu đầu vào không hợp lệ! Chi tiết: {string.Join(", ", errors)}");
                return ViewWithData(order);
            }

            if (ProductDetailIds.Count == 0 || Quantities.Count == 0 || Prices.Count == 0 ||
                ProductDetailIds.Count != Quantities.Count || Quantities.Count != Prices.Count)
            {
                var message = "Danh sách sản phẩm không hợp lệ hoặc trống!";
                _logger.LogWarning("Create: {Message}", message);
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message });
                }
                _notifyService.Error(message);
                return ViewWithData(order);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Create: Bắt đầu tạo đơn hàng mới. Transaction started.");

                order.OrderDate = order.OrderDate ?? DateTime.Now;
                order.TotalMoney = 0;
                order.OrderDetails = new HashSet<OrderDetail>();
                _logger.LogDebug("Create: Order initialized: OrderDate={OrderDate}, TotalMoney={TotalMoney}, CustomerId={CustomerId}", order.OrderDate, order.TotalMoney, order.CustomerId);

                if (!order.CustomerId.HasValue || order.CustomerId <= 0)
                {
                    var defaultCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.FullName == "Đơn hàng từ Admin");
                    if (defaultCustomer == null)
                    {
                        defaultCustomer = new Customer
                        {
                            FullName = "Đơn hàng từ Admin",
                            Email = "admin@webshop.com",
                            Phone = "0000000000",
                            Active = true
                        };
                        _context.Customers.Add(defaultCustomer);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Create: Đã tạo khách hàng mặc định với CustomerId: {CustomerId}", defaultCustomer.CustomerId);
                    }
                    order.CustomerId = defaultCustomer.CustomerId;
                }
                else
                {
                    if (!await _context.Customers.AnyAsync(c => c.CustomerId == order.CustomerId))
                    {
                        var message = $"CustomerId {order.CustomerId} không tồn tại!";
                        _logger.LogWarning("Create: {Message}", message);
                        if (Request.IsAjaxRequest())
                        {
                            return Json(new { success = false, message });
                        }
                        _notifyService.Error(message);
                        return ViewWithData(order);
                    }
                }

                order.PromotionId = null;
                order.VoucherId = null;

                for (int i = 0; i < ProductDetailIds.Count; i++)
                {
                    var productDetail = await _context.ProductDetails
                        .Include(pd => pd.Product)
                        .FirstOrDefaultAsync(pd => pd.ProductDetailId == ProductDetailIds[i]);

                    if (productDetail == null)
                    {
                        var message = $"Sản phẩm với ID {ProductDetailIds[i]} không tồn tại!";
                        _logger.LogWarning("Create: {Message}", message);
                        if (Request.IsAjaxRequest())
                        {
                            return Json(new { success = false, message });
                        }
                        _notifyService.Error(message);
                        return ViewWithData(order);
                    }

                    if (!productDetail.Active)
                    {
                        var message = $"Sản phẩm {ProductDetailIds[i]} không hoạt động!";
                        _logger.LogWarning("Create: {Message}", message);
                        if (Request.IsAjaxRequest())
                        {
                            return Json(new { success = false, message });
                        }
                        _notifyService.Error(message);
                        return ViewWithData(order);
                    }

                    if (productDetail.Stock < Quantities[i])
                    {
                        var message = $"Sản phẩm {ProductDetailIds[i]} không đủ hàng (còn {productDetail.Stock})!";
                        _logger.LogWarning("Create: {Message}", message);
                        if (Request.IsAjaxRequest())
                        {
                            return Json(new { success = false, message });
                        }
                        _notifyService.Error(message);
                        return ViewWithData(order);
                    }

                    var orderDetail = new OrderDetail
                    {
                        ProductDetailId = ProductDetailIds[i],
                        Amount = Quantities[i],
                        Price = (int?)Prices[i],
                        Total = (int?)(Quantities[i] * Prices[i]),
                        Quantity = Quantities[i]
                    };
                    order.OrderDetails.Add(orderDetail);
                    order.TotalMoney += Quantities[i] * Prices[i];

                    productDetail.Stock -= Quantities[i];
                    _context.ProductDetails.Update(productDetail);
                }

                if (order.TotalMoney < 0)
                {
                    order.TotalMoney = 0;
                }

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Create: Tạo đơn hàng thành công với OrderId: {OrderId}", order.OrderId);

                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true, redirectTo = Url.Action("Index", "AdminOrders", new { area = "Admin" }) });
                }
                _notifyService.Success("Tạo đơn hàng thành công!");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Create: Lỗi khi tạo đơn hàng: {Message}", ex.Message);
                var message = $"Lỗi khi tạo đơn hàng: {ex.Message}";
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message });
                }
                _notifyService.Error(message);
                return ViewWithData(order);
            }
        }

        // GET: Admin/AdminOrders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Edit: OrderId is null");
                _notifyService.Error("Không tìm thấy đơn hàng!");
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Size)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Color)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null)
            {
                _logger.LogWarning("Edit: Order not found for OrderId={OrderId}", id);
                _notifyService.Error("Đơn hàng không tồn tại!");
                return NotFound();
            }

            if (order.Deleted || order.TransactStatusId == 3 || order.TransactStatusId == 4)
            {
                _logger.LogWarning("Edit: Cannot edit order with OrderId={OrderId}. Status={TransactStatusId}, Deleted={Deleted}", id, order.TransactStatusId, order.Deleted);
                _notifyService.Error("Không thể chỉnh sửa đơn hàng đã hủy hoặc đang giao/hoàn thành!");
                return RedirectToAction(nameof(Index));
            }

            var defaultCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.FullName == "Đơn hàng từ Admin");
            bool isGuest = order.CustomerId == defaultCustomer?.CustomerId;

            var customers = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Khách vãng lai (do admin tạo)", Selected = isGuest }
            };
            customers.AddRange(new SelectList(_context.Customers, "CustomerId", "FullName", order.CustomerId));
            ViewData["CustomerId"] = customers;

            ViewData["TransactStatusId"] = new SelectList(_context.TransactStatuses.Where(ts => ts.TransactStatusId == 1 || ts.TransactStatusId == 2), "TransactStatusId", "Status", order.TransactStatusId);

            if (isGuest)
            {
                ViewData["PromotionId"] = new SelectList(new List<SelectListItem>(), "Value", "Text");
                ViewData["VoucherId"] = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }
            else
            {
                ViewData["PromotionId"] = new SelectList(_context.Promotions.Where(p => p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now), "PromotionId", "PromotionName", order.PromotionId);

                var usedVoucher = await _context.UserPromotions
                    .Where(up => up.CustomerId == order.CustomerId && up.UsedDate < new DateTime(9999, 12, 31))
                    .Select(up => up.Voucher)
                    .FirstOrDefaultAsync();
                var voucherList = new List<SelectListItem>();
                if (usedVoucher != null)
                {
                    voucherList.Add(new SelectListItem { Value = usedVoucher.VoucherId.ToString(), Text = usedVoucher.VoucherCode, Selected = order.VoucherId == usedVoucher.VoucherId });
                }
                else
                {
                    voucherList.Add(new SelectListItem { Value = "", Text = "Chưa sử dụng voucher", Selected = !order.VoucherId.HasValue });
                }
                ViewData["VoucherId"] = new SelectList(voucherList, "Value", "Text", order.VoucherId);
            }

            var products = _context.Products
                .Where(p => (p.Active ?? false))
                .Select(p => new { p.ProductId, p.ProductName })
                .ToList();
            ViewBag.Products = products.Any()
                ? new SelectList(products, "ProductId", "ProductName")
                : new SelectList(new List<SelectListItem>(), "Value", "Text");
            _logger.LogInformation("Edit: Loaded {Count} products for ViewBag.Products", products.Count);

            var variants = _context.ProductDetails
                .Where(pd => pd.Active && (pd.Product.Active ?? false))
                .Include(pd => pd.Size)
                .Include(pd => pd.Color)
                .Include(pd => pd.Product)
                .Select(pd => new
                {
                    ProductDetailId = pd.ProductDetailId,
                    ProductId = pd.ProductId,
                    SizeId = pd.SizeId ?? 0,
                    SizeName = pd.Size != null ? pd.Size.SizeName : "Không xác định",
                    ColorId = pd.ColorId ?? 0,
                    ColorName = pd.Color != null ? pd.Color.ColorName : "Không xác định",
                    Stock = pd.Stock ?? 0,
                    Price = pd.Product.Price ?? 0,
                    ProductName = pd.Product.ProductName
                })
                .Distinct()
                .ToList();
            _logger.LogInformation("Edit: Loaded {Count} variants for ViewBag.Variants", variants.Count);

            try
            {
                ViewBag.Variants = JsonConvert.SerializeObject(variants, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Error = (sender, args) =>
                    {
                        _logger.LogError("Edit: Error serializing variants: {Error}", args.ErrorContext.Error.Message);
                        args.ErrorContext.Handled = true;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edit: Failed to serialize variants: {Message}", ex.Message);
                ViewBag.Variants = JsonConvert.SerializeObject(new List<object>());
            }

            ViewBag.OrderDetails = order.OrderDetails?.ToList() ?? new List<OrderDetail>();
            return View(order);
        }

        // POST: Admin/AdminOrders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("OrderId,ReceiverName,TransactStatusId,Paid,Note,Address,LocationId,PromotionId,VoucherId,PaymentDate")] Order order,
            List<int> ProductDetailIds,
            List<int> Quantities,
            List<decimal> Prices)
        {
            if (id != order.OrderId)
            {
                _logger.LogWarning("Edit: OrderId mismatch. Provided={Provided}, Expected={Expected}", id, order.OrderId);
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "ID đơn hàng không khớp!" });
                _notifyService.Error("ID đơn hàng không khớp!");
                return NotFound();
            }

            ProductDetailIds = ProductDetailIds?.Where(id => id != 0).ToList() ?? new List<int>();
            Quantities = Quantities?.Where(q => q > 0).ToList() ?? new List<int>();
            Prices = Prices?.Where(p => p > 0).ToList() ?? new List<decimal>();

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Edit: Invalid model state: {Errors}", string.Join(", ", errors));
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = $"Dữ liệu đầu vào không hợp lệ! Chi tiết: {string.Join(", ", errors)}" });
                _notifyService.Error($"Dữ liệu đầu vào không hợp lệ! Chi tiết: {string.Join(", ", errors)}");
                return ViewWithData(order);
            }

            if (ProductDetailIds.Count == 0 || Quantities.Count == 0 || Prices.Count == 0 ||
                ProductDetailIds.Count != Quantities.Count || Quantities.Count != Prices.Count)
            {
                _logger.LogWarning("Edit: Danh sách sản phẩm không hợp lệ hoặc trống!");
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Danh sách sản phẩm không hợp lệ hoặc trống!" });
                _notifyService.Error("Danh sách sản phẩm không hợp lệ hoặc trống!");
                return ViewWithData(order);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingOrder = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Product)
                    .Include(o => o.Promotion)
                    .Include(o => o.Voucher)
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(m => m.OrderId == id);
                if (existingOrder == null)
                {
                    _logger.LogWarning("Edit: Order not found for OrderId={OrderId}", id);
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "Đơn hàng không tồn tại!" });
                    _notifyService.Error("Đơn hàng không tồn tại!");
                    return NotFound();
                }
                if (existingOrder.Deleted || existingOrder.TransactStatusId == 3 || existingOrder.TransactStatusId == 4)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Edit: Cannot edit order with OrderId={OrderId}. Status={TransactStatusId}, Deleted={Deleted}", id, existingOrder.TransactStatusId, existingOrder.Deleted);
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "Không thể chỉnh sửa đơn hàng đã hủy hoặc đang giao/hoàn thành!" });
                    _notifyService.Error("Không thể chỉnh sửa đơn hàng đã hủy hoặc đang giao/hoàn thành!");
                    return RedirectToAction(nameof(Index));
                }

                var defaultCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.FullName == "Đơn hàng từ Admin");
                bool isGuest = existingOrder.CustomerId == defaultCustomer?.CustomerId;
                if (isGuest && (order.PromotionId.HasValue || order.VoucherId.HasValue))
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Edit: Guest order (CustomerId={CustomerId}) cannot use Promotion or Voucher", existingOrder.CustomerId, order.PromotionId);
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "Đơn hàng của khách vãng lai không được sử dụng khuyến mãi hoặc mã giảm giá!" });
                    _notifyService.Error("Đơn hàng của khách vãng lai không được sử dụng khuyến mãi hoặc mã giảm giá!");
                    return ViewWithData(order);
                }

                // Kiểm tra thay đổi danh sách sản phẩm
                var originalProductDetailIds = existingOrder.OrderDetails
                    .Select(od => od.ProductDetailId ?? 0)
                    .Where(id => id != 0)
                    .OrderBy(id => id)
                    .ToList();
                var newProductDetailIds = ProductDetailIds
                    .Where(id => id != 0)
                    .OrderBy(id => id)
                    .ToList();
                bool isProductListChanged = !originalProductDetailIds.SequenceEqual(newProductDetailIds);

                // Xử lý Promotion nếu danh sách sản phẩm thay đổi hoặc PromotionId thay đổi
                if (existingOrder.PromotionId != order.PromotionId || isProductListChanged)
                {
                    if (existingOrder.PromotionId.HasValue)
                    {
                        // Reset UsedDate của UserPromotion về 9999-12-31 nếu PromotionId bị gỡ
                        var oldUserPromotion = await _context.UserPromotions
                            .FirstOrDefaultAsync(up => up.CustomerId == existingOrder.CustomerId &&
                                                      up.PromotionId == existingOrder.PromotionId &&
                                                      up.UsedDate < new DateTime(9999, 12, 31));
                        if (oldUserPromotion != null)
                        {
                            oldUserPromotion.UsedDate = new DateTime(9999, 12, 31);
                            _context.UserPromotions.Update(oldUserPromotion);
                            _logger.LogInformation("Edit: Reset UsedDate for PromotionId={PromotionId}, CustomerId={CustomerId}", oldUserPromotion.PromotionId, oldUserPromotion.CustomerId);
                        }
                    }

                    if (order.PromotionId.HasValue)
                    {
                        // Kiểm tra xem các sản phẩm mới có thuộc chương trình khuyến mãi không
                        var productIds = await _context.ProductDetails
                            .Where(pd => ProductDetailIds.Contains(pd.ProductDetailId))
                            .Select(pd => pd.ProductId)
                            .Distinct()
                            .ToListAsync();

                        bool hasEligibleProduct = await _context.PromotionProducts
                            .AnyAsync(pp => pp.PromotionId == order.PromotionId && productIds.Contains(pp.ProductId));

                        if (!hasEligibleProduct)
                        {
                            _notifyService.Warning("Không sử dụng chương trình khuyến mãi do sản phẩm không còn nằm trong chương trình!");
                            order.PromotionId = null;
                        }
                        else
                        {
                            // Cập nhật hoặc tạo mới UserPromotion
                            var userPromotion = await _context.UserPromotions
                                .FirstOrDefaultAsync(up => up.CustomerId == existingOrder.CustomerId &&
                                                          up.PromotionId == order.PromotionId);
                            if (userPromotion != null)
                            {
                                if (userPromotion.UsedDate < new DateTime(9999, 12, 31))
                                {
                                    await transaction.RollbackAsync();
                                    _logger.LogWarning("Edit: CustomerId={CustomerId} already used PromotionId={PromotionId}", existingOrder.CustomerId, order.PromotionId);
                                    if (Request.IsAjaxRequest())
                                        return Json(new { success = false, message = "Khách hàng đã sử dụng khuyến mãi này!" });
                                    _notifyService.Error("Khách hàng đã sử dụng khuyến mãi này!");
                                    return ViewWithData(order);
                                }
                                userPromotion.UsedDate = DateTime.Now;
                                _context.UserPromotions.Update(userPromotion);
                            }
                            else
                            {
                                var userPromotionNew = new UserPromotion
                                {
                                    CustomerId = existingOrder.CustomerId.Value,
                                    PromotionId = order.PromotionId,
                                    UsedDate = DateTime.Now
                                };
                                _context.UserPromotions.Add(userPromotionNew);
                            }
                        }
                    }
                    else
                    {
                        existingOrder.PromotionId = null;
                        existingOrder.TotalDiscount = 0;
                    }
                }

                // Hoàn trả số lượng tồn kho cho các sản phẩm cũ
                foreach (var detail in existingOrder.OrderDetails)
                {
                    var productDetail = await _context.ProductDetails
                        .FirstOrDefaultAsync(pd => pd.ProductDetailId == detail.ProductDetailId);
                    if (productDetail != null)
                    {
                        productDetail.Stock += detail.Amount ?? 0;
                        _context.ProductDetails.Update(productDetail);
                    }
                }

                // Cập nhật thông tin đơn hàng
                existingOrder.ReceiverName = order.ReceiverName;
                existingOrder.TransactStatusId = order.TransactStatusId;
                existingOrder.Paid = order.Paid;
                existingOrder.Note = order.Note;
                existingOrder.Address = order.Address;
                existingOrder.LocationId = order.LocationId;
                existingOrder.PromotionId = order.PromotionId;
                existingOrder.VoucherId = order.VoucherId;
                existingOrder.TotalDiscount = order.PromotionId.HasValue ? existingOrder.TotalDiscount : 0;
                existingOrder.PaymentDate = order.Paid ? (order.PaymentDate ?? DateTime.Now) : existingOrder.PaymentDate;
                existingOrder.OrderDate = existingOrder.OrderDate;
                existingOrder.ShipDate = existingOrder.ShipDate;
                existingOrder.Deleted = existingOrder.Deleted;
                existingOrder.CustomerId = existingOrder.CustomerId;

                // Cập nhật chi tiết đơn hàng và tính tổng tiền
                decimal newTotalMoney = 0;
                _context.OrderDetails.RemoveRange(existingOrder.OrderDetails);
                existingOrder.OrderDetails = new HashSet<OrderDetail>();
                for (int i = 0; i < ProductDetailIds.Count; i++)
                {
                    var productDetail = await _context.ProductDetails
                        .Include(pd => pd.Product)
                        .FirstOrDefaultAsync(pd => pd.ProductDetailId == ProductDetailIds[i]);
                    if (productDetail == null || !productDetail.Active || productDetail.Stock < Quantities[i])
                    {
                        await transaction.RollbackAsync();
                        var message = $"Sản phẩm {ProductDetailIds[i]} không tồn tại, không hoạt động hoặc không đủ hàng (còn {productDetail?.Stock ?? 0})!";
                        _logger.LogWarning("Edit: {Message}", message);
                        if (Request.IsAjaxRequest())
                            return Json(new { success = false, message });
                        _notifyService.Error(message);
                        return ViewWithData(order);
                    }

                    var orderDetail = new OrderDetail
                    {
                        ProductDetailId = ProductDetailIds[i],
                        Amount = Quantities[i],
                        Price = (int?)Prices[i],
                        Total = (int?)(Quantities[i] * Prices[i]),
                        Quantity = Quantities[i]
                    };
                    existingOrder.OrderDetails.Add(orderDetail);
                    newTotalMoney += Quantities[i] * Prices[i];

                    productDetail.Stock -= Quantities[i];
                    _context.ProductDetails.Update(productDetail);
                }

                if (newTotalMoney < 0)
                {
                    newTotalMoney = 0;
                }

                if (existingOrder.VoucherId.HasValue)
                {
                    var voucher = await _context.Vouchers
                        .FirstOrDefaultAsync(v => v.VoucherId == existingOrder.VoucherId);

                    if (voucher != null && newTotalMoney < voucher.MinOrderValue)
                    {
                        var userPromotion = await _context.UserPromotions
                            .FirstOrDefaultAsync(up => up.CustomerId == existingOrder.CustomerId &&
                                                      up.VoucherId == existingOrder.VoucherId &&
                                                      up.UsedDate == existingOrder.OrderDate);

                        if (userPromotion != null)
                        {
                            userPromotion.UsedDate = new DateTime(9999, 12, 31);
                            _context.UserPromotions.Update(userPromotion);
                        }

                        voucher.UsedCount = Math.Max(0, voucher.UsedCount - 1);
                        _context.Vouchers.Update(voucher);

                        existingOrder.VoucherId = null;
                        _notifyService.Warning("Đã gỡ voucher do tổng đơn hàng không còn đạt mức tối thiểu.");
                    }
                }

                existingOrder.TotalMoney = newTotalMoney;
                existingOrder.TotalMoney = Math.Max(0m, (existingOrder.TotalMoney ?? 0m) - (existingOrder.TotalDiscount));

                _context.Update(existingOrder);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Edit: Cập nhật đơn hàng thành công với OrderId: {OrderId}", id);
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true, redirectTo = Url.Action("Index", "AdminOrders", new { area = "Admin" }) });
                }
                _notifyService.Success("Cập nhật đơn hàng thành công!");
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Edit: Concurrency error for OrderId={OrderId}", id);
                if (!OrderExists(order.OrderId))
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "Đơn hàng không tồn tại!" });
                    _notifyService.Error("Đơn hàng không tồn tại!");
                    return NotFound();
                }
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Lỗi đồng bộ hóa dữ liệu. Vui lòng thử lại!" });
                _notifyService.Error("Lỗi đồng bộ hóa dữ liệu. Vui lòng thử lại!");
                return ViewWithData(order);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Edit: Lỗi khi cập nhật đơn hàng: {Message}", ex.Message);
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = $"Lỗi khi cập nhật đơn hàng: {ex.Message}" });
                _notifyService.Error($"Lỗi khi cập nhật đơn hàng: {ex.Message}");
                return ViewWithData(order);
            }
        }

        // GET: Admin/AdminOrders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Delete: OrderId is null");
                _notifyService.Error("Không tìm thấy đơn hàng!");
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.TransactStatus)
                .Include(o => o.Promotion)
                .Include(o => o.Voucher)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Size)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductDetail)
                    .ThenInclude(pd => pd.Color)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null)
            {
                _logger.LogWarning("Delete: Order not found for OrderId={OrderId}", id);
                _notifyService.Error("Đơn hàng không tồn tại!");
                return NotFound();
            }

            if (order.Deleted)
            {
                _logger.LogWarning("Delete: Order with OrderId={OrderId} is already deleted", id);
                _notifyService.Error("Đơn hàng đã bị xóa trước đó!");
                return RedirectToAction(nameof(Index));
            }

            if (order.TransactStatusId == 3 || order.TransactStatusId == 4)
            {
                _logger.LogWarning("Delete: Cannot delete order with OrderId={OrderId}. Status={TransactStatusId}", id, order.TransactStatusId);
                _notifyService.Error("Không thể xóa đơn hàng đang giao hoặc đã hoàn thành!");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.OrderDetails = order.OrderDetails.OrderBy(x => x.OrderDetailId).ToList();
            return View(order);
        }

        // POST: Admin/AdminOrders/DeleteConfirmed/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string search = null, int? status = null, int? page = 1)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (order == null)
                {
                    _logger.LogWarning("DeleteConfirmed: Order not found for OrderId={OrderId}", id);
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = $"Đơn hàng #{id} không tồn tại!" });
                    _notifyService.Error($"Đơn hàng #{id} không tồn tại!");
                    return RedirectToAction(nameof(Index));
                }

                if (order.Deleted)
                {
                    _logger.LogWarning("DeleteConfirmed: Order with OrderId={OrderId} is already deleted", id);
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = $"Đơn hàng #{id} đã được xóa trước đó!" });
                    _notifyService.Error($"Đơn hàng #{id} đã được xóa trước đó!");
                    return RedirectToAction(nameof(Index));
                }

                if (order.TransactStatusId == 3 || order.TransactStatusId == 4)
                {
                    _logger.LogWarning("DeleteConfirmed: Cannot delete order with OrderId={OrderId}. Status={TransactStatusId}", id, order.TransactStatusId);
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = $"Không thể xóa đơn hàng #{id} vì đang giao hoặc đã hoàn thành!" });
                    _notifyService.Error($"Không thể xóa đơn hàng #{id} vì đang giao hoặc đã hoàn thành!");
                    return RedirectToAction(nameof(Index));
                }

                // Đánh dấu đơn hàng đã bị xóa
                order.Deleted = true;
                order.TransactStatusId = 5; // Giả sử 5 là trạng thái hủy
                _logger.LogInformation("DeleteConfirmed: Marked OrderId={OrderId} as Deleted and set TransactStatusId=5", id);

                // Hoàn trả số lượng tồn kho
                foreach (var detail in order.OrderDetails)
                {
                    var productDetail = await _context.ProductDetails
                        .FirstOrDefaultAsync(pd => pd.ProductDetailId == detail.ProductDetailId);
                    if (productDetail == null)
                    {
                        _logger.LogWarning("DeleteConfirmed: ProductDetail not found for ProductDetailId={ProductDetailId} in OrderId={OrderId}", detail.ProductDetailId, id);
                        continue;
                    }
                    productDetail.Stock += detail.Amount ?? 0;
                    _context.ProductDetails.Update(productDetail);
                    _logger.LogInformation("DeleteConfirmed: Restored Stock={Amount} for ProductDetailId={ProductDetailId}", detail.Amount, detail.ProductDetailId);
                }

                // Gỡ Promotion nếu có
                if (order.PromotionId.HasValue)
                {
                    var promo = await _context.UserPromotions
                        .FirstOrDefaultAsync(up => up.CustomerId == order.CustomerId &&
                                                   up.PromotionId == order.PromotionId &&
                                                   up.UsedDate < new DateTime(9999, 12, 31));
                    if (promo != null)
                    {
                        promo.UsedDate = new DateTime(9999, 12, 31);
                        _context.UserPromotions.Update(promo);
                        _logger.LogInformation("DeleteConfirmed: Reset UsedDate for PromotionId={PromotionId}, CustomerId={CustomerId}", promo.PromotionId, promo.CustomerId);
                    }
                    order.PromotionId = null;
                }

                // Gỡ Voucher nếu có
                if (order.VoucherId.HasValue)
                {
                    var voucher = await _context.Vouchers.FindAsync(order.VoucherId);
                    if (voucher != null)
                    {
                        voucher.UsedCount = Math.Max(0, voucher.UsedCount - 1);
                        _context.Vouchers.Update(voucher);
                        _logger.LogInformation("DeleteConfirmed: Decreased UsedCount for VoucherId={VoucherId} to {UsedCount}", voucher.VoucherId, voucher.UsedCount);
                    }

                    var userVoucher = await _context.UserPromotions
                        .FirstOrDefaultAsync(up => up.CustomerId == order.CustomerId &&
                                                   up.VoucherId == order.VoucherId &&
                                                   up.UsedDate == order.OrderDate);
                    if (userVoucher != null)
                    {
                        userVoucher.UsedDate = new DateTime(9999, 12, 31);
                        _context.UserPromotions.Update(userVoucher);
                        _logger.LogInformation("DeleteConfirmed: Reset UsedDate for VoucherId={VoucherId}, CustomerId={CustomerId}", userVoucher.VoucherId, userVoucher.CustomerId);
                    }
                    order.VoucherId = null;
                }

                _context.Orders.Update(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("DeleteConfirmed: Successfully deleted order with OrderId={OrderId}", id);
                if (Request.IsAjaxRequest())
                {
                    var redirectUrl = Url.Action("Index", "AdminOrders", new { area = "Admin", search, status, page });
                    return Json(new { success = true, redirectTo = redirectUrl });
                }
                _notifyService.Success("Xóa đơn hàng thành công!");
                return RedirectToAction(nameof(Index), new { search, status, page });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "DeleteConfirmed: Concurrency error for OrderId={OrderId}", id);
                if (!OrderExists(id))
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = $"Đơn hàng #{id} không tồn tại!" });
                    _notifyService.Error($"Đơn hàng #{id} không tồn tại!");
                    return NotFound();
                }
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Lỗi đồng bộ hóa dữ liệu. Vui lòng thử lại!" });
                _notifyService.Error("Lỗi đồng bộ hóa dữ liệu. Vui lòng thử lại!");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "DeleteConfirmed: Error deleting order with OrderId={OrderId}: {Message}", id, ex.Message);
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = $"Lỗi khi xóa đơn hàng #{id}: {ex.Message}" });
                _notifyService.Error($"Lỗi khi xóa đơn hàng #{id}: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }
        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.OrderId == id);
        }

        private IActionResult ViewWithData(Order order)
        {
            var defaultCustomer = _context.Customers
                .FirstOrDefault(c => c.FullName == "Đơn hàng từ Admin");
            bool isGuest = order.CustomerId == defaultCustomer?.CustomerId;

            var customers = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Khách vãng lai (do admin tạo)", Selected = isGuest }
            };
            customers.AddRange(new SelectList(_context.Customers, "CustomerId", "FullName", order.CustomerId));
            ViewData["CustomerId"] = customers;

            ViewData["TransactStatusId"] = new SelectList(_context.TransactStatuses.Where(ts => ts.TransactStatusId == 1 || ts.TransactStatusId == 2), "TransactStatusId", "Status", order.TransactStatusId);

            if (isGuest)
            {
                ViewData["PromotionId"] = new SelectList(new List<SelectListItem>(), "Value", "Text");
                ViewData["VoucherId"] = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }
            else
            {
                ViewData["PromotionId"] = new SelectList(_context.Promotions.Where(p => p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now), "PromotionId", "PromotionName", order.PromotionId);
                ViewData["VoucherId"] = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }

            var products = _context.Products
                .Where(p => (p.Active ?? false))
                .Select(p => new { p.ProductId, p.ProductName })
                .ToList();
            ViewBag.Products = products.Any()
                ? new SelectList(products, "ProductId", "ProductName")
                : new SelectList(new List<SelectListItem>(), "Value", "Text");

            var variants = _context.ProductDetails
                .Where(pd => pd.Active && (pd.Product.Active ?? false))
                .Include(pd => pd.Size)
                .Include(pd => pd.Color)
                .Include(pd => pd.Product)
                .Select(pd => new
                {
                    ProductDetailId = pd.ProductDetailId,
                    ProductId = pd.ProductId,
                    SizeId = pd.SizeId ?? 0,
                    SizeName = pd.Size != null ? pd.Size.SizeName : "Không xác định",
                    ColorId = pd.ColorId ?? 0,
                    ColorName = pd.Color != null ? pd.Color.ColorName : "Không xác định",
                    Stock = pd.Stock ?? 0,
                    Price = pd.Product.Price ?? 0,
                    ProductName = pd.Product.ProductName
                })
                .Distinct()
                .ToList();

            try
            {
                ViewBag.Variants = JsonConvert.SerializeObject(variants, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Error = (sender, args) =>
                    {
                        _logger.LogError("ViewWithData: Error serializing variants: {Error}", args.ErrorContext.Error.Message);
                        args.ErrorContext.Handled = true;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ViewWithData: Failed to serialize variants: {Message}", ex.Message);
                ViewBag.Variants = JsonConvert.SerializeObject(new List<object>());
            }

            ViewBag.OrderDetails = order.OrderDetails?.ToList() ?? new List<OrderDetail>();
            return View("Edit", order);
        }
    }

    public static class HttpRequestExtensions
    {
        public static bool IsAjaxRequest(this HttpRequest request)
        {
            return request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }
    }
}