using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebShop.Extension;
using WebShop.Models;
using WebShop.ModelViews;
using static WebShop.Controllers.ShoppingCartController;

namespace WebShop.Controllers
{
    [Authorize(AuthenticationSchemes = "CustomerAuthentication")]
    public class CheckoutController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notyfService;
        private readonly ILogger<CheckoutController> _logger;
        private readonly IDataProtector _protector;
        private readonly object _cartLock = new object();

        public CheckoutController(webshopContext context, INotyfService notyfService, ILogger<CheckoutController> logger,
            IDataProtectionProvider provider)
        {
            _context = context;
            _notyfService = notyfService;
            _logger = logger;
            _protector = provider.CreateProtector("Cart");
        }

        private List<CartItem> GetBuyNowCartItems()
        {
            var customerId = HttpContext.Session.GetString("CustomerId");
            var cartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString();
            var buyNowCart = RestoreBuyNowFromCookie(customerId, cartToken);

            if (buyNowCart == null || !buyNowCart.Items.Any())
            {
                return new List<CartItem>();
            }

            var productDetailIds = buyNowCart.Items.Select(i => i.ProductDetailId).ToList();
            var productDetails = _context.ProductDetails
                .Include(pd => pd.Product)
                .Include(pd => pd.Size)
                .Include(pd => pd.Color)
                .AsNoTracking()
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
                .ToList()
                .ToDictionary(p => p.ProductDetailId);

            var cartItems = new List<CartItem>();
            var removedItems = new List<string>();

            foreach (var item in buyNowCart.Items)
            {
                if (!productDetails.TryGetValue(item.ProductDetailId, out var detail))
                {
                    removedItems.Add($"Sản phẩm ID {item.ProductDetailId}");
                    _logger.LogInformation("Xóa sản phẩm không tồn tại khỏi giỏ Mua ngay: ID {ProductDetailId}", item.ProductDetailId);
                    continue;
                }

                if (!detail.ProductActive || !detail.ProductDetailActive || detail.Stock < item.Amount || !detail.SizeId.HasValue || !detail.ColorId.HasValue || detail.Price == null)
                {
                    removedItems.Add(detail.ProductName);
                    _logger.LogInformation("Xóa sản phẩm không hợp lệ khỏi giỏ Mua ngay: {ProductName}, ProductActive={ProductActive}, ProductDetailActive={ProductDetailActive}, Stock={Stock}",
                        detail.ProductName, detail.ProductActive, detail.ProductDetailActive, detail.Stock);
                    continue;
                }

                cartItems.Add(new CartItem
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
                _notyfService.Warning($"Đã xóa {removedItems.Count} sản phẩm không hợp lệ khỏi giỏ Mua ngay: {string.Join(", ", removedItems)}");
                buyNowCart.Items = buyNowCart.Items
                    .Where(i => productDetails.ContainsKey(i.ProductDetailId) &&
                                productDetails[i.ProductDetailId].ProductActive &&
                                productDetails[i.ProductDetailId].ProductDetailActive &&
                                productDetails[i.ProductDetailId].Stock >= i.Amount &&
                                productDetails[i.ProductDetailId].SizeId.HasValue &&
                                productDetails[i.ProductDetailId].ColorId.HasValue &&
                                productDetails[i.ProductDetailId].Price != null)
                    .ToList();
                SaveBuyNowToCookie(buyNowCart, customerId);
            }

            return cartItems;
        }

        private SimpleCart RestoreBuyNowFromCookie(string customerId, string cartToken)
        {
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
                        cart.CartToken = cartToken;
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
                _notyfService.Error("Lỗi khi lưu giỏ Mua ngay.");
            }
        }

        private SimpleCart RestoreCartFromCookie(string customerId, string cartToken)
        {
            var cookieKey = customerId == "Anonymous" ? "Cart_Anonymous" : $"Cart_{customerId}";
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
                        cart.CartToken = cartToken;
                        cart.Items = cart.Items ?? new List<SimpleCartItem>();
                        _logger.LogInformation("Khôi phục giỏ hàng từ cookie: CustomerId={CustomerId}, ItemCount={Count}", customerId, cart.Items.Count);
                        return cart;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi giải mã cookie: CookieKey={CookieKey}", cookieKey);
                    Response.Cookies.Delete(cookieKey);
                }
            }
            return null;
        }

        private void SaveCartToCookie(SimpleCart cart, string customerId)
        {
            var cookieKey = customerId == "Anonymous" ? "Cart_Anonymous" : $"Cart_{customerId}";
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

                HttpContext.Items["CartForLocalStorage"] = new
                {
                    cartToken = cart.CartToken,
                    customerId = customerId,
                    items = cart.Items.Select(item => new
                    {
                        productDetailId = item.ProductDetailId,
                        amount = item.Amount
                    }).ToList()
                };
                _logger.LogInformation("Lưu giỏ hàng vào cookie: CustomerId={CustomerId}, ItemCount={Count}, Items={Items}",
                    customerId, cart.Items.Count, JsonSerializer.Serialize(cart.Items));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu giỏ hàng vào cookie: CustomerId={CustomerId}", customerId);
                _notyfService.Error("Lỗi khi lưu giỏ hàng.");
            }
        }

        public List<CartItem> GioHang
        {
            get
            {
                lock (_cartLock)
                {
                    var customerId = HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                    var cartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString();
                    var cart = RestoreCartFromCookie(customerId, cartToken) ?? new SimpleCart
                    {
                        CustomerId = customerId,
                        CartToken = cartToken,
                        Items = new List<SimpleCartItem>()
                    };

                    if (customerId == "Anonymous")
                    {
                        var sessionCartItems = HttpContext.Session.Keys
                            .Where(k => k.StartsWith("CartItem_"))
                            .Select(k =>
                            {
                                if (int.TryParse(k.Replace("CartItem_", ""), out int productDetailId))
                                {
                                    var amount = HttpContext.Session.GetInt32(k) ?? 0;
                                    return new SimpleCartItem { ProductDetailId = productDetailId, Amount = amount };
                                }
                                return null;
                            })
                            .Where(i => i != null && i.Amount > 0)
                            .ToList();

                        foreach (var sessionItem in sessionCartItems)
                        {
                            var existingItem = cart.Items.FirstOrDefault(x => x.ProductDetailId == sessionItem.ProductDetailId);
                            if (existingItem != null)
                            {
                                existingItem.Amount += sessionItem.Amount;
                            }
                            else
                            {
                                cart.Items.Add(sessionItem);
                            }
                        }
                    }

                    var productDetailIds = cart.Items
                        .Where(i => i.ProductDetailId > 0)
                        .Select(i => i.ProductDetailId)
                        .Distinct()
                        .ToList();

                    var productDetails = _context.ProductDetails
                        .Include(pd => pd.Product)
                        .Include(pd => pd.Size)
                        .Include(pd => pd.Color)
                        .AsNoTracking()
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
                        .ToList()
                        .ToDictionary(p => p.ProductDetailId);

                    var cartItems = new List<CartItem>();
                    var removedItems = new List<string>();

                    foreach (var item in cart.Items.Where(i => i.ProductDetailId > 0))
                    {
                        if (productDetails.TryGetValue(item.ProductDetailId, out var detail) &&
                            detail.ProductActive &&
                            detail.ProductDetailActive &&
                            detail.Stock >= item.Amount &&
                            detail.SizeId.HasValue &&
                            detail.ColorId.HasValue &&
                            detail.Price != null)
                        {
                            cartItems.Add(new CartItem
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
                        else
                        {
                            removedItems.Add(detail?.ProductName ?? $"Sản phẩm ID {item.ProductDetailId}");
                            _logger.LogInformation("Xóa sản phẩm không hợp lệ khỏi giỏ hàng: ProductDetailId={ProductDetailId}, ProductActive={ProductActive}, ProductDetailActive={ProductDetailActive}, Stock={Stock}",
                                item.ProductDetailId, detail?.ProductActive, detail?.ProductDetailActive, detail?.Stock);
                        }
                    }

                    if (removedItems.Any())
                    {
                        _notyfService.Warning($"Đã xóa {removedItems.Count} sản phẩm không hợp lệ khỏi giỏ hàng: {string.Join(", ", removedItems)}");
                        cart.Items = cartItems
                            .Where(x => x?.productDetail?.ProductDetailId > 0)
                            .Select(x => new SimpleCartItem
                            {
                                ProductDetailId = x.productDetail.ProductDetailId,
                                Amount = x.amount
                            })
                            .ToList();
                        SaveCartToCookie(cart, customerId);
                    }

                    return cartItems;
                }
            }
        }

        private List<SelectListItem> GetTinhThanhList()
        {
            return _context.Locations
                .Where(x => x.Levels == 1)
                .OrderBy(x => x.Name)
                .AsNoTracking()
                .Select(x => new SelectListItem
                {
                    Value = x.LocationId.ToString(),
                    Text = x.NameWithType
                }).ToList();
        }

        private async Task<List<VoucherViewModel>> GetVoucherViewModels(string customerId, decimal totalOrderValue)
        {
            try
            {
                if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int customerIdInt))
                {
                    _logger.LogWarning("CustomerId không hợp lệ: CustomerId={CustomerId}", customerId);
                    return new List<VoucherViewModel>();
                }

                var vouchers = await _context.Vouchers
                    .AsNoTracking()
                    .Where(v => v.EndDate >= DateTime.Now &&
                                v.UsedCount < v.MaxUsage &&
                                (!v.MinOrderValue.HasValue || v.MinOrderValue <= totalOrderValue))
                    .ToListAsync();

                if (!vouchers.Any())
                {
                    _logger.LogWarning("Không tìm thấy voucher nào hợp lệ trong bảng Vouchers: CustomerId={CustomerId}, TotalOrderValue={TotalOrderValue}",
                        customerId, totalOrderValue);
                    return new List<VoucherViewModel>();
                }

                var userPromotions = await _context.UserPromotions
                    .AsNoTracking()
                    .Where(up => up.CustomerId == customerIdInt && up.VoucherId != null)
                    .GroupBy(up => up.VoucherId)
                    .Select(g => new
                    {
                        VoucherId = g.Key,
                        UsedCountByUser = g.Count(up => up.UsedDate < DateTime.Now)
                    })
                    .ToListAsync();

                var voucherViewModels = new List<VoucherViewModel>();
                var assignedVouchers = await _context.UserPromotions
                    .Where(up => up.CustomerId == customerIdInt && up.VoucherId != null)
                    .Select(up => up.VoucherId)
                    .Distinct()
                    .ToListAsync();

                foreach (var voucher in vouchers)
                {
                    if (assignedVouchers.Contains(voucher.VoucherId))
                    {
                        var usedCountByUser = userPromotions
                            .FirstOrDefault(up => up.VoucherId == voucher.VoucherId)?.UsedCountByUser ?? 0;

                        if (usedCountByUser < voucher.DefaultUserMaxUsage)
                        {
                            voucherViewModels.Add(new VoucherViewModel
                            {
                                VoucherId = voucher.VoucherId,
                                VoucherCode = voucher.VoucherCode,
                                DiscountValue = voucher.DiscountValue,
                                DiscountType = voucher.DiscountType,
                                MinOrderValue = voucher.MinOrderValue,
                                MaxUsage = voucher.MaxUsage,
                                UsedCount = voucher.UsedCount,
                                EndDate = voucher.EndDate,
                                UsedCountByUser = usedCountByUser,
                                DefaultUserMaxUsage = voucher.DefaultUserMaxUsage,
                                IsApplicable = true
                            });
                        }
                    }
                }

                return voucherViewModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách voucher: CustomerId={CustomerId}, TotalOrderValue={TotalOrderValue}", customerId, totalOrderValue);
                _notyfService.Error("Lỗi khi tải danh sách voucher. Vui lòng kiểm tra log để biết chi tiết.");
                return new List<VoucherViewModel>();
            }
        }

        private async Task<List<PromotionViewModel>> GetPromotionViewModels(string customerId, List<int> productIds)
        {
            try
            {
                if (string.IsNullOrEmpty(customerId) || !int.TryParse(customerId, out int customerIdInt) || productIds == null || !productIds.Any())
                {
                    return new List<PromotionViewModel>();
                }

                var validProductIds = await _context.PromotionProducts
                    .AsNoTracking()
                    .Where(pp => productIds.Contains(pp.ProductId))
                    .Select(pp => pp.ProductId)
                    .Distinct()
                    .ToListAsync();

                if (!validProductIds.Any())
                {
                    return new List<PromotionViewModel>();
                }

                var usedPromotions = await _context.UserPromotions
                    .AsNoTracking()
                    .Where(up => up.CustomerId == customerIdInt && up.PromotionId != null && up.UsedDate < DateTime.Now)
                    .Select(up => up.PromotionId.Value)
                    .ToListAsync();

                var promotions = await _context.Promotions
                    .AsNoTracking()
                    .Where(p => p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
                    .Join(_context.PromotionProducts,
                        p => p.PromotionId,
                        pp => pp.PromotionId,
                        (p, pp) => new { Promotion = p, ProductId = pp.ProductId })
                    .Where(x => validProductIds.Contains(x.ProductId) && !usedPromotions.Contains(x.Promotion.PromotionId))
                    .Select(x => x.Promotion)
                    .Distinct()
                    .ToListAsync();

                if (promotions == null || !promotions.Any())
                {
                    return new List<PromotionViewModel>();
                }

                var promotionViewModels = new List<PromotionViewModel>();
                foreach (var promotion in promotions)
                {
                    var usedCount = await _context.UserPromotions
                        .AsNoTracking()
                        .CountAsync(up => up.PromotionId == promotion.PromotionId && up.CustomerId == customerIdInt && up.UsedDate < DateTime.Now);

                    if (usedCount < promotion.DefaultUserMaxUsage)
                    {
                        promotionViewModels.Add(new PromotionViewModel
                        {
                            PromotionId = promotion.PromotionId,
                            PromotionName = promotion.PromotionName,
                            Discount = promotion.Discount,
                            StartDate = promotion.StartDate,
                            EndDate = promotion.EndDate,
                            IsActive = promotion.IsActive,
                            UsedCountByUser = usedCount,
                            DefaultUserMaxUsage = promotion.DefaultUserMaxUsage,
                            IsApplicable = true
                        });
                    }
                }

                return promotionViewModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách promotion: CustomerId={CustomerId}", customerId);
                _notyfService.Error("Lỗi khi tải danh sách promotion. Vui lòng kiểm tra cấu hình hoặc dữ liệu.");
                return new List<PromotionViewModel>();
            }
        }

        [HttpGet]
        [Route("checkout.html", Name = "Checkout")]
        public async Task<IActionResult> Index(string returnUrl = null)
        {
            try
            {
                var customerId = HttpContext.Session.GetString("CustomerId");
                if (string.IsNullOrEmpty(customerId) || customerId == "Anonymous")
                {
                    _logger.LogInformation("Chưa đăng nhập, chuyển hướng đến trang đăng nhập với returnUrl=/checkout.html");
                    return RedirectToAction("Login", "Accounts", new { returnUrl = "/checkout.html" });
                }

                var customer = await _context.Customers
                    .Include(c => c.Location)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CustomerId == int.Parse(customerId));

                if (customer == null)
                {
                    _logger.LogWarning("Không tìm thấy khách hàng với CustomerId: {CustomerId}", customerId);
                    return RedirectToAction("Login", "Accounts", new { returnUrl = "/checkout.html" });
                }

                var cartItems = GetBuyNowCartItems();
                bool isBuyNow = cartItems.Any();
                List<int> selectedProductDetailIds;

                // Kiểm tra và chuyển sản phẩm từ giỏ Mua ngay về giỏ hàng chính nếu không hoàn tất thanh toán
                var buyNowCart = RestoreBuyNowFromCookie(customerId, Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString());
                if (buyNowCart != null && buyNowCart.Items.Any())
                {
                    lock (_cartLock)
                    {
                        var mainCart = RestoreCartFromCookie(customerId, Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString()) ?? new SimpleCart
                        {
                            CustomerId = customerId,
                            CartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString(),
                            Items = new List<SimpleCartItem>()
                        };

                        foreach (var buyNowItem in buyNowCart.Items)
                        {
                            // Kiểm tra sản phẩm có hợp lệ không
                            var productDetail = _context.ProductDetails
                                .Include(pd => pd.Product)
                                .AsNoTracking()
                                .FirstOrDefault(p => p.ProductDetailId == buyNowItem.ProductDetailId);

                            if (productDetail == null || !(productDetail.Product.Active ?? false) || !productDetail.Active || productDetail.Stock < buyNowItem.Amount)
                            {
                                _logger.LogWarning("Sản phẩm không hợp lệ trong giỏ Mua ngay, bỏ qua: ProductDetailId={ProductDetailId}, ProductActive={ProductActive}, ProductDetailActive={ProductDetailActive}, Stock={Stock}",
                                    buyNowItem.ProductDetailId, productDetail?.Product.Active, productDetail?.Active, productDetail?.Stock);
                                continue;
                            }

                            var existingItem = mainCart.Items.FirstOrDefault(x => x.ProductDetailId == buyNowItem.ProductDetailId);
                            if (existingItem != null)
                            {
                                existingItem.Amount += buyNowItem.Amount;
                                _logger.LogInformation("Cộng dồn sản phẩm từ giỏ Mua ngay vào giỏ chính: ProductDetailId={ProductDetailId}, NewAmount={NewAmount}", buyNowItem.ProductDetailId, existingItem.Amount);
                            }
                            else
                            {
                                mainCart.Items.Add(new SimpleCartItem
                                {
                                    ProductDetailId = buyNowItem.ProductDetailId,
                                    Amount = buyNowItem.Amount
                                });
                                _logger.LogInformation("Thêm sản phẩm mới từ giỏ Mua ngay vào giỏ chính: ProductDetailId={ProductDetailId}, Amount={Amount}", buyNowItem.ProductDetailId, buyNowItem.Amount);
                            }
                        }

                        try
                        {
                            SaveCartToCookie(mainCart, customerId);
                            _logger.LogInformation("Đã lưu giỏ hàng chính sau khi chuyển sản phẩm từ giỏ Mua ngay: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, mainCart.Items.Count);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Lỗi khi lưu giỏ hàng chính sau khi chuyển từ giỏ Mua ngay: CustomerId={CustomerId}", customerId);
                            _notyfService.Error("Lỗi khi lưu giỏ hàng chính.");
                        }

                        try
                        {
                            Response.Cookies.Delete($"BuyNowCart_{customerId}");
                            _logger.LogInformation("Xóa giỏ Mua ngay sau khi chuyển sản phẩm: CustomerId={CustomerId}", customerId);
                            _notyfService.Information("Sản phẩm từ giỏ Mua ngay đã được chuyển về giỏ hàng chính do gián đoạn thanh toán.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Lỗi khi xóa giỏ Mua ngay: CustomerId={CustomerId}", customerId);
                            _notyfService.Error("Lỗi khi xử lý giỏ Mua ngay.");
                        }
                    }
                }

                if (isBuyNow)
                {
                    _logger.LogInformation("Luồng Mua ngay: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, cartItems.Count);
                    HttpContext.Session.SetString("BuyNow", "true");
                    selectedProductDetailIds = cartItems.Select(i => i.productDetail.ProductDetailId).ToList();
                    HttpContext.Session.SetString("BuyNowProductDetailIds", JsonSerializer.Serialize(selectedProductDetailIds));
                }
                else
                {
                    cartItems = GioHang;
                    var buyNowProductDetailIdsJson = HttpContext.Session.GetString("BuyNowProductDetailIds");
                    if (HttpContext.Session.GetString("BuyNow") == "true" && !string.IsNullOrEmpty(buyNowProductDetailIdsJson))
                    {
                        _logger.LogInformation("Phát hiện trạng thái BuyNow, chuyển sản phẩm từ BuyNowCart sang giỏ hàng chính: CustomerId={CustomerId}", customerId);
                        selectedProductDetailIds = JsonSerializer.Deserialize<List<int>>(buyNowProductDetailIdsJson) ?? new List<int>();
                        var buyNowCartExisting = RestoreBuyNowFromCookie(customerId, Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString());
                        if (buyNowCartExisting != null && buyNowCartExisting.Items.Any())
                        {
                            var mainCart = RestoreCartFromCookie(customerId, Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString()) ?? new SimpleCart
                            {
                                CustomerId = customerId,
                                CartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString(),
                                Items = new List<SimpleCartItem>()
                            };

                            foreach (var buyNowItem in buyNowCartExisting.Items)
                            {
                                var existingItem = mainCart.Items.FirstOrDefault(x => x.ProductDetailId == buyNowItem.ProductDetailId);
                                if (existingItem != null)
                                {
                                    existingItem.Amount += buyNowItem.Amount;
                                    _logger.LogInformation("Cộng dồn sản phẩm trùng: ProductDetailId={ProductDetailId}, NewAmount={NewAmount}", buyNowItem.ProductDetailId, existingItem.Amount);
                                }
                                else
                                {
                                    mainCart.Items.Add(new SimpleCartItem
                                    {
                                        ProductDetailId = buyNowItem.ProductDetailId,
                                        Amount = buyNowItem.Amount
                                    });
                                    _logger.LogInformation("Thêm sản phẩm mới vào giỏ hàng chính: ProductDetailId={ProductDetailId}, Amount={Amount}", buyNowItem.ProductDetailId, buyNowItem.Amount);
                                }
                            }

                            try
                            {
                                SaveCartToCookie(mainCart, customerId);
                                _logger.LogInformation("Đã lưu giỏ hàng chính vào cookie: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, mainCart.Items.Count);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Lỗi khi lưu giỏ hàng chính vào cookie: CustomerId={CustomerId}", customerId);
                                _notyfService.Error("Lỗi khi lưu giỏ hàng chính.");
                            }

                            try
                            {
                                Response.Cookies.Delete($"BuyNowCart_{customerId}");
                                HttpContext.Session.Remove("BuyNow");
                                HttpContext.Session.Remove("BuyNowProductDetailIds");
                                _logger.LogInformation("Xóa BuyNowCart và session: CustomerId={CustomerId}", customerId);
                                _notyfService.Information("Sản phẩm từ giỏ Mua ngay đã được chuyển về giỏ hàng chính.");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Lỗi khi xóa BuyNowCart hoặc session: CustomerId={CustomerId}", customerId);
                                _notyfService.Error("Lỗi khi xử lý giỏ Mua ngay.");
                            }

                            cartItems = GioHang;
                            selectedProductDetailIds = cartItems
                                .Where(i => i?.productDetail?.ProductDetailId > 0)
                                .Select(i => i.productDetail.ProductDetailId)
                                .ToList();
                        }
                    }
                    else
                    {
                        selectedProductDetailIds = cartItems
                            .Where(i => i?.productDetail?.ProductDetailId > 0)
                            .Select(i => i.productDetail.ProductDetailId)
                            .ToList();
                    }
                }

                if (!cartItems.Any())
                {
                    _notyfService.Error("Giỏ hàng của bạn trống!");
                    _logger.LogWarning("Giỏ hàng trống: CustomerId={CustomerId}", customerId);
                    HttpContext.Session.Remove("BuyNow");
                    HttpContext.Session.Remove("BuyNowProductDetailIds");
                    return RedirectToAction("Index", "ShoppingCart");
                }

                var selectedItems = cartItems
                    .Where(item => item?.productDetail?.ProductDetailId > 0 && selectedProductDetailIds.Contains(item.productDetail.ProductDetailId))
                    .ToList();

                if (!selectedItems.Any())
                {
                    _notyfService.Error(isBuyNow ? "Không tìm thấy sản phẩm cho Mua ngay!" : "Không có sản phẩm nào được chọn để thanh toán!");
                    _logger.LogWarning("{Message}, CustomerId={CustomerId}", isBuyNow ? "BuyNowProductDetailIds trống" : "Không có sản phẩm được chọn", customerId);
                    HttpContext.Session.Remove("BuyNow");
                    HttpContext.Session.Remove("BuyNowProductDetailIds");
                    return RedirectToAction("Index", "ShoppingCart");
                }

                decimal totalOrderValue = selectedItems.Sum(x => (decimal)x.product.Price * x.amount);
                var voucherViewModels = await GetVoucherViewModels(customerId, totalOrderValue);
                var productIds = selectedItems.Select(x => x.productDetail.ProductId).ToList();
                var promotionViewModels = await GetPromotionViewModels(customerId, productIds);

                var muaHangVM = new MuaHangVM
                {
                    CustomerId = customer.CustomerId,
                    FullName = customer.FullName,
                    Email = customer.Email,
                    Phone = customer.Phone,
                    Address = customer.Address,
                    TinhThanh = customer.LocationId ?? 0,
                    QuanHuyen = customer.Address?.Split(',').Length > 1 ? customer.Address.Split(',')[1].Trim() : "",
                    PhuongXa = customer.Address?.Split(',').Length > 2 ? customer.Address.Split(',')[2].Trim() : "",
                    SelectedProductDetailIds = JsonSerializer.Serialize(selectedProductDetailIds),
                    PaymentID = 1 // Giá trị mặc định
                };

                ViewBag.GioHang = selectedItems;
                ViewBag.SelectedProductDetailIds = muaHangVM.SelectedProductDetailIds;
                ViewBag.lsTinhThanh = GetTinhThanhList();
                ViewBag.AvailableVouchers = voucherViewModels;
                ViewBag.AvailablePromotions = promotionViewModels;
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.GlobalCartToken = Request.Cookies["GlobalCartToken"];
                _logger.LogInformation("Hiển thị trang thanh toán: CustomerId={CustomerId}, ItemCount={ItemCount}, VoucherCount={VoucherCount}, PromotionCount={PromotionCount}",
                    customerId, selectedItems.Count, voucherViewModels.Count, promotionViewModels.Count);

                return View(muaHangVM);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi hiển thị trang thanh toán: CustomerId={CustomerId}", HttpContext.Session.GetString("CustomerId"));
                _notyfService.Error("Lỗi khi tải trang thanh toán.");
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [Route("checkout.html")]
        public async Task<IActionResult> Index(MuaHangVM model)
        {
            try
            {
                var customerIdStr = HttpContext.Session.GetString("CustomerId");
                if (string.IsNullOrEmpty(customerIdStr))
                {
                    _logger.LogWarning("CustomerId không hợp lệ trong session, chuyển hướng đến trang đăng nhập");
                    return RedirectToAction("Login", "Accounts", new { returnUrl = "/checkout.html" });
                }

                if (!int.TryParse(customerIdStr, out int customerId))
                {
                    _logger.LogWarning("CustomerId trong session không phải số: {CustomerId}", customerIdStr);
                    return RedirectToAction("Login", "Accounts", new { returnUrl = "/checkout.html" });
                }

                _logger.LogInformation("Received PaymentID from client: {PaymentID}", model.PaymentID);

                model.CustomerId = customerId;

                var customer = await _context.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId);
                if (customer == null)
                {
                    _logger.LogWarning("Không tìm thấy khách hàng với CustomerId: {CustomerId}", customerId);
                    return RedirectToAction("Login", "Accounts", new { returnUrl = "/checkout.html" });
                }

                var cartItems = GetBuyNowCartItems();
                bool isBuyNow = cartItems.Any();
                if (!isBuyNow)
                {
                    cartItems = GioHang;
                }

                if (!cartItems.Any())
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Giỏ hàng trống!" });

                    _notyfService.Error("Giỏ hàng của bạn trống!");
                    _logger.LogWarning("Giỏ hàng trống trong quá trình thanh toán: CustomerId={CustomerId}", customerId);
                    HttpContext.Session.Remove("BuyNow");
                    HttpContext.Session.Remove("BuyNowProductDetailIds");
                    return RedirectToAction("Index", "ShoppingCart");
                }

                var selectedProductDetailIds = !string.IsNullOrEmpty(model.SelectedProductDetailIds)
                    ? JsonSerializer.Deserialize<List<int>>(model.SelectedProductDetailIds) ?? new List<int>()
                    : new List<int>();

                var selectedItems = cartItems
                    .Where(item => item?.productDetail?.ProductDetailId > 0 && selectedProductDetailIds.Contains(item.productDetail.ProductDetailId))
                    .ToList();

                if (!selectedItems.Any())
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Không có sản phẩm nào được chọn để thanh toán!" });

                    _notyfService.Error("Vui lòng chọn ít nhất một sản phẩm để thanh toán!");
                    _logger.LogWarning("Không có sản phẩm nào được chọn để thanh toán: CustomerId={CustomerId}", customerId);
                    ViewBag.GioHang = selectedItems;
                    ViewBag.SelectedProductDetailIds = model.SelectedProductDetailIds ?? JsonSerializer.Serialize(new List<int>());
                    ViewBag.lsTinhThanh = GetTinhThanhList();
                    ViewBag.AvailableVouchers = await GetVoucherViewModels(customerIdStr, selectedItems.Sum(x => (decimal)x.product.Price * x.amount));
                    ViewBag.AvailablePromotions = await GetPromotionViewModels(customerIdStr, selectedItems.Select(x => x.productDetail.ProductId).ToList());
                    return View(model);
                }

                if (string.IsNullOrEmpty(model.FullName) || string.IsNullOrEmpty(model.Phone) || model.TinhThanh <= 0 || string.IsNullOrEmpty(model.QuanHuyen) || string.IsNullOrEmpty(model.PhuongXa))
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin giao hàng!" });

                    _notyfService.Error("Vui lòng điền đầy đủ thông tin giao hàng (Họ tên, Số điện thoại, Tỉnh/Thành, Quận/Huyện, Phường/Xã).");
                    _logger.LogWarning("Thông tin giao hàng không hợp lệ: CustomerId={CustomerId}", customerId);
                    ViewBag.GioHang = selectedItems;
                    ViewBag.SelectedProductDetailIds = model.SelectedProductDetailIds;
                    ViewBag.lsTinhThanh = GetTinhThanhList();
                    ViewBag.AvailableVouchers = await GetVoucherViewModels(customerIdStr, selectedItems.Sum(x => (decimal)x.product.Price * x.amount));
                    ViewBag.AvailablePromotions = await GetPromotionViewModels(customerIdStr, selectedItems.Select(x => x.productDetail.ProductId).ToList());
                    return View(model);
                }

                if (!model.PaymentID.HasValue || model.PaymentID <= 0)
                {
                    _logger.LogWarning("PaymentID không hợp lệ hoặc không được cung cấp: {PaymentID}, CustomerId={CustomerId}", model.PaymentID, customerId);
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Vui lòng chọn phương thức thanh toán!" });

                    _notyfService.Error("Vui lòng chọn phương thức thanh toán!");
                    ViewBag.GioHang = selectedItems;
                    ViewBag.SelectedProductDetailIds = model.SelectedProductDetailIds;
                    ViewBag.lsTinhThanh = GetTinhThanhList();
                    ViewBag.AvailableVouchers = await GetVoucherViewModels(customerIdStr, selectedItems.Sum(x => (decimal)x.product.Price * x.amount));
                    ViewBag.AvailablePromotions = await GetPromotionViewModels(customerIdStr, selectedItems.Select(x => x.productDetail.ProductId).ToList());
                    return View(model);
                }

                var productDetailIds = selectedItems.Select(x => x.productDetail.ProductDetailId).ToList();
                var productDetails = await _context.ProductDetails
                    .Include(pd => pd.Product)
                    .Where(p => productDetailIds.Contains(p.ProductDetailId))
                    .ToListAsync();

                foreach (var item in selectedItems)
                {
                    var productDetail = productDetails.FirstOrDefault(p => p.ProductDetailId == item.productDetail.ProductDetailId);
                    if (productDetail == null || !(productDetail.Product.Active ?? false) || !productDetail.Active || productDetail.Stock < item.amount)
                    {
                        string msg = productDetail == null ? $"Sản phẩm không tồn tại (ID: {item.productDetail.ProductDetailId})" :
                                     !(productDetail.Product.Active ?? false) ? $"Sản phẩm không hoạt động (ID: {item.productDetail.ProductDetailId})" :
                                     !productDetail.Active ? $"Sản phẩm chi tiết không hoạt động (ID: {item.productDetail.ProductDetailId})" :
                                     $"Số lượng vượt quá tồn kho (ID: {item.productDetail.ProductDetailId})";
                        _logger.LogWarning(msg);

                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                            return Json(new { success = false, message = msg });

                        _notyfService.Error("Sản phẩm không hợp lệ hoặc hết hàng!");
                        ViewBag.GioHang = selectedItems;
                        ViewBag.SelectedProductDetailIds = model.SelectedProductDetailIds;
                        ViewBag.lsTinhThanh = GetTinhThanhList();
                        ViewBag.AvailableVouchers = await GetVoucherViewModels(customerIdStr, selectedItems.Sum(x => (decimal)x.product.Price * x.amount));
                        ViewBag.AvailablePromotions = await GetPromotionViewModels(customerIdStr, selectedItems.Select(x => x.productDetail.ProductId).ToList());
                        return View(model);
                    }
                }

                decimal totalOrderValue = selectedItems.Sum(x => (decimal)x.product.Price * x.amount);
                decimal totalDiscount = 0;

                if (model.VoucherId.HasValue)
                {
                    var voucher = await _context.Vouchers
                        .FirstOrDefaultAsync(v => v.VoucherId == model.VoucherId && v.EndDate >= DateTime.Now && v.UsedCount < v.MaxUsage);
                    if (voucher != null && (!voucher.MinOrderValue.HasValue || voucher.MinOrderValue <= totalOrderValue))
                    {
                        var usedCountByUser = await _context.UserPromotions
                            .CountAsync(up => up.CustomerId == customerId && up.VoucherId == model.VoucherId && up.UsedDate < DateTime.Now);
                        if (usedCountByUser >= voucher.DefaultUserMaxUsage)
                        {
                            _notyfService.Error("Bạn đã sử dụng voucher này quá số lần cho phép!");
                            ViewBag.GioHang = selectedItems;
                            ViewBag.SelectedProductDetailIds = model.SelectedProductDetailIds;
                            ViewBag.lsTinhThanh = GetTinhThanhList();
                            ViewBag.AvailableVouchers = await GetVoucherViewModels(customerIdStr, totalOrderValue);
                            ViewBag.AvailablePromotions = await GetPromotionViewModels(customerIdStr, selectedItems.Select(x => x.productDetail.ProductId).ToList());
                            return View(model);
                        }

                        totalDiscount += voucher.DiscountType == "Percentage"
                            ? totalOrderValue * (voucher.DiscountValue / 100)
                            : voucher.DiscountValue;
                    }
                    else
                    {
                        _logger.LogWarning("Voucher không hợp lệ hoặc đã hết lượt sử dụng: VoucherId={VoucherId}, CustomerId={CustomerId}", model.VoucherId, customerId);
                    }
                }

                if (model.PromotionId.HasValue)
                {
                    var promotion = await _context.Promotions
                        .FirstOrDefaultAsync(p => p.PromotionId == model.PromotionId && p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now);
                    if (promotion != null)
                    {
                        var usedCountByUser = await _context.UserPromotions
                            .CountAsync(up => up.CustomerId == customerId && up.PromotionId == model.PromotionId && up.UsedDate < DateTime.Now);
                        if (usedCountByUser >= promotion.DefaultUserMaxUsage)
                        {
                            _notyfService.Error("Bạn đã sử dụng khuyến mãi này quá số lần cho phép!");
                            ViewBag.GioHang = selectedItems;
                            ViewBag.SelectedProductDetailIds = model.SelectedProductDetailIds;
                            ViewBag.lsTinhThanh = GetTinhThanhList();
                            ViewBag.AvailableVouchers = await GetVoucherViewModels(customerIdStr, totalOrderValue);
                            ViewBag.AvailablePromotions = await GetPromotionViewModels(customerIdStr, selectedItems.Select(x => x.productDetail.ProductId).ToList());
                            return View(model);
                        }

                        totalDiscount += totalOrderValue * (promotion.Discount / 100);
                    }
                    else
                    {
                        _logger.LogWarning("Promotion không hợp lệ hoặc không còn hiệu lực: PromotionId={PromotionId}, CustomerId={CustomerId}", model.PromotionId, customerId);
                    }
                }

                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var order = new Order
                        {
                            CustomerId = model.CustomerId,
                            ReceiverName = model.FullName,
                            Address = $"{model.Address}, {model.QuanHuyen}, {model.PhuongXa}",
                            LocationId = model.TinhThanh,
                            OrderDate = DateTime.Now,
                            TransactStatusId = 1,
                            Deleted = false,
                            Paid = model.PaymentID == 1,
                            PaymentDate = model.PaymentID == 1 ? null : DateTime.Now,
                            TotalMoney = (int)(totalOrderValue - totalDiscount),
                            TotalDiscount = totalDiscount,
                            PaymentId = model.PaymentID,
                            Note = model.Note?.Trim(),
                            PromotionId = model.PromotionId,
                            VoucherId = model.VoucherId
                        };
                        _logger.LogInformation("Order created with Paid = {Paid}, PaymentID = {PaymentID}", order.Paid, model.PaymentID);

                        _context.Orders.Add(order);
                        await _context.SaveChangesAsync();

                        foreach (var item in selectedItems)
                        {
                            var productDetail = productDetails.First(p => p.ProductDetailId == item.productDetail.ProductDetailId);

                            var orderDetail = new OrderDetail
                            {
                                OrderId = order.OrderId,
                                ProductDetailId = item.productDetail.ProductDetailId,
                                Amount = item.amount,
                                Quantity = item.amount,
                                ShipDate = null,
                                Price = item.product.Price,
                                Total = (int)(item.product.Price * item.amount)
                            };
                            _context.OrderDetails.Add(orderDetail);

                            productDetail.Stock -= item.amount;
                            if (productDetail.Stock < 0)
                            {
                                throw new InvalidOperationException($"Số lượng tồn kho không đủ cho sản phẩm ID {productDetail.ProductDetailId}");
                            }
                            _context.ProductDetails.Update(productDetail);
                        }

                        if (model.VoucherId.HasValue)
                        {
                            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == model.VoucherId);
                            if (voucher != null)
                            {
                                voucher.UsedCount += 1;
                                _context.Vouchers.Update(voucher);

                                _context.UserPromotions.Add(new UserPromotion
                                {
                                    CustomerId = customerId,
                                    VoucherId = model.VoucherId,
                                    UsedDate = DateTime.Now
                                });
                                _logger.LogInformation("Cập nhật voucher: VoucherId={VoucherId}, UsedCount={UsedCount}, CustomerId={CustomerId}", model.VoucherId, voucher.UsedCount, customerId);
                            }
                        }

                        if (model.PromotionId.HasValue)
                        {
                            _context.UserPromotions.Add(new UserPromotion
                            {
                                CustomerId = customerId,
                                PromotionId = model.PromotionId,
                                UsedDate = DateTime.Now
                            });
                            _logger.LogInformation("Cập nhật promotion: PromotionId={PromotionId}, CustomerId={CustomerId}", model.PromotionId, customerId);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        if (isBuyNow)
                        {
                            Response.Cookies.Delete($"BuyNowCart_{customerId}");
                            _logger.LogInformation("Xóa BuyNowCart sau khi đặt hàng thành công: CustomerId={CustomerId}", customerId);
                        }
                        else
                        {
                            var updatedCartItems = cartItems
                                .Where(item => item?.productDetail?.ProductDetailId > 0 && !selectedProductDetailIds.Contains(item.productDetail.ProductDetailId))
                                .ToList();
                            var simpleCart = new SimpleCart
                            {
                                CustomerId = customerId.ToString(),
                                CartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString(),
                                Items = updatedCartItems.Select(x => new SimpleCartItem
                                {
                                    ProductDetailId = x.productDetail.ProductDetailId,
                                    Amount = x.amount
                                }).ToList()
                            };
                            SaveCartToCookie(simpleCart, customerId.ToString());
                            _logger.LogInformation("Cập nhật giỏ hàng chính sau khi đặt hàng: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, simpleCart.Items.Count);
                        }

                        HttpContext.Session.Remove("BuyNow");
                        HttpContext.Session.Remove("BuyNowProductDetailIds");

                        _notyfService.Success("Đặt hàng thành công!");
                        return Json(new { success = true, redirectUrl = Url.Action("Success", new { orderId = order.OrderId }) });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Lỗi khi lưu đơn hàng: CustomerId={CustomerId}, Message={Message}, InnerException={InnerException}", customerId, ex.Message, ex.InnerException?.Message);
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            return Json(new { success = false, message = "Lỗi khi xử lý đơn hàng: " + (ex.InnerException?.Message ?? ex.Message) });
                        }

                        _notyfService.Error($"Lỗi khi xử lý đơn hàng: {ex.InnerException?.Message ?? ex.Message}");
                        ViewBag.GioHang = GetBuyNowCartItems().Any() ? GetBuyNowCartItems() : GioHang;
                        ViewBag.SelectedProductDetailIds = model?.SelectedProductDetailIds ?? JsonSerializer.Serialize(new List<int>());
                        ViewBag.lsTinhThanh = GetTinhThanhList();
                        ViewBag.AvailableVouchers = customerIdStr != null ? await GetVoucherViewModels(customerIdStr, (GetBuyNowCartItems().Any() ? GetBuyNowCartItems() : GioHang).Sum(x => (decimal)x.product.Price * x.amount)) : new List<VoucherViewModel>();
                        ViewBag.AvailablePromotions = customerIdStr != null ? await GetPromotionViewModels(customerIdStr, (GetBuyNowCartItems().Any() ? GetBuyNowCartItems() : GioHang).Select(x => x.productDetail.ProductId).ToList()) : new List<PromotionViewModel>();
                        return View(model ?? new MuaHangVM());
                    }
                }
            }
            catch (Exception ex)
            {
                var customerId = HttpContext.Session.GetString("CustomerId");
                _logger.LogError(ex, "Lỗi khi xử lý thanh toán: CustomerId={CustomerId}", customerId);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Lỗi khi xử lý đơn hàng: " + ex.Message });
                }

                _notyfService.Error($"Lỗi khi xử lý đơn hàng: {ex.Message}");
                ViewBag.GioHang = GetBuyNowCartItems().Any() ? GetBuyNowCartItems() : GioHang;
                ViewBag.SelectedProductDetailIds = model?.SelectedProductDetailIds ?? JsonSerializer.Serialize(new List<int>());
                ViewBag.lsTinhThanh = GetTinhThanhList();
                ViewBag.AvailableVouchers = customerId != null ? await GetVoucherViewModels(customerId, (GetBuyNowCartItems().Any() ? GetBuyNowCartItems() : GioHang).Sum(x => (decimal)x.product.Price * x.amount)) : new List<VoucherViewModel>();
                ViewBag.AvailablePromotions = customerId != null ? await GetPromotionViewModels(customerId, (GetBuyNowCartItems().Any() ? GetBuyNowCartItems() : GioHang).Select(x => x.productDetail.ProductId).ToList()) : new List<PromotionViewModel>();
                return View(model ?? new MuaHangVM());
            }
        }

        [HttpPost]
        [Route("checkout/calculate-discount")]
        public async Task<IActionResult> CalculateDiscount([FromBody] DiscountRequest request)
        {
            try
            {
                var customerId = HttpContext.Session.GetString("CustomerId");
                if (string.IsNullOrEmpty(customerId)) return Json(new { success = false, error = "Customer not logged in" });

                decimal total = request.TotalAmount;
                decimal voucherDiscount = 0;
                decimal promotionDiscount = 0;

                if (request.VoucherId.HasValue)
                {
                    var voucher = await _context.Vouchers
                        .FirstOrDefaultAsync(v => v.VoucherId == request.VoucherId && v.EndDate >= DateTime.Now && v.UsedCount < v.MaxUsage);
                    if (voucher != null && (!voucher.MinOrderValue.HasValue || voucher.MinOrderValue <= total))
                    {
                        voucherDiscount = voucher.DiscountType == "Percentage"
                            ? total * (voucher.DiscountValue / 100)
                            : voucher.DiscountValue;
                        voucherDiscount = Math.Min(voucherDiscount, total);
                    }
                }

                if (request.PromotionId.HasValue)
                {
                    var promotion = await _context.Promotions
                        .FirstOrDefaultAsync(p => p.PromotionId == request.PromotionId && p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now);
                    if (promotion != null)
                    {
                        promotionDiscount = total * (promotion.Discount / 100);
                    }
                }

                decimal totalDiscount = voucherDiscount + promotionDiscount;
                return Json(new { success = true, totalDiscount = totalDiscount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tính giảm giá: CustomerId={CustomerId}", HttpContext.Session.GetString("CustomerId"));
                return Json(new { success = false, error = "Error calculating discount" });
            }
        }

        public class DiscountRequest
        {
            public decimal TotalAmount { get; set; }
            public int? VoucherId { get; set; }
            public int? PromotionId { get; set; }
        }

        [HttpGet]
        [Route("dat-hang-thanh-cong.html", Name = "Success")]
        public async Task<IActionResult> Success(int orderId)
        {
            try
            {
                var customerId = HttpContext.Session.GetString("CustomerId");
                if (string.IsNullOrEmpty(customerId))
                {
                    _logger.LogInformation("Chưa đăng nhập, chuyển hướng đến trang đăng nhập với returnUrl=/dat-hang-thanh-cong.html");
                    return RedirectToAction("Login", "Accounts", new { returnUrl = $"/dat-hang-thanh-cong.html?orderId={orderId}" });
                }

                var order = await _context.Orders
                    .Include(x => x.OrderDetails)
                        .ThenInclude(od => od.ProductDetail)
                        .ThenInclude(pd => pd.Product)
                    .Include(x => x.OrderDetails)
                        .ThenInclude(od => od.ProductDetail)
                        .ThenInclude(pd => pd.Size)
                    .Include(x => x.OrderDetails)
                        .ThenInclude(od => od.ProductDetail)
                        .ThenInclude(pd => pd.Color)
                    .Include(x => x.TransactStatus)
                    .Include(x => x.Location)
                    .Include(x => x.Voucher)
                    .Include(x => x.Promotion)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.OrderId == orderId && x.CustomerId == int.Parse(customerId) && x.Deleted == false);

                if (order == null)
                {
                    _logger.LogWarning("Không tìm thấy đơn hàng: OrderId={OrderId}, CustomerId={CustomerId}", orderId, customerId);
                    _notyfService.Error("Đơn hàng không tồn tại hoặc bạn không có quyền xem!");
                    return RedirectToAction("Index", "Home");
                }

                var tinhThanh = order.Location?.NameWithType ?? "Không xác định";
                var addressParts = order.Address?.Split(',') ?? new string[] { };
                var quanHuyen = addressParts.Length > 1 ? addressParts[1].Trim() : "Không xác định";
                var phuongXa = addressParts.Length > 2 ? addressParts[2].Trim() : "Không xác định";

                ViewBag.TinhThanh = tinhThanh;
                ViewBag.QuanHuyen = quanHuyen;
                ViewBag.PhuongXa = phuongXa;
                ViewBag.OrderDetails = order.OrderDetails.ToList();
                ViewBag.Voucher = order.Voucher;
                ViewBag.Promotion = order.Promotion;

                _logger.LogInformation("Hiển thị trang xác nhận đơn hàng: OrderId={OrderId}, CustomerId={CustomerId}", orderId, customerId);
                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi hiển thị trang xác nhận đơn hàng: OrderId={OrderId}, CustomerId={CustomerId}", orderId, HttpContext.Session.GetString("CustomerId"));
                _notyfService.Error("Lỗi khi tải trang xác nhận đơn hàng.");
                return RedirectToAction("Index", "Home");
            }
        }
    }
}