// ShoppingCartController: Quản lý giỏ hàng, bao gồm thêm, xóa, cập nhật sản phẩm, đồng bộ giỏ hàng và xử lý mua ngay.
// Chứa 21 phương thức: CheckLogin, GetCartCount, AddToCart, BuyNow, ProceedToCheckout, RemoveFromCart, RemoveMultipleFromCart, UpdateCart, SyncCart, SyncLocalCart,
// GetDiscount, Index, AddToSessionCart, GetSessionCartItems, ClearSessionCart, RestoreCartFromCookie, RestoreBuyNowFromCookie, SaveCartToCookie, SaveBuyNowToCookie,
// EstimateMaxItems, UpdateCartTokenCookie.
using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

namespace WebShop.Controllers
{
    // Quản lý các chức năng liên quan đến giỏ hàng, hỗ trợ cả người dùng đã đăng nhập và khách vãng lai.
    public class ShoppingCartController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notyfService;
        private readonly IDataProtector _protector;
        private readonly ILogger<ShoppingCartController> _logger;
        private readonly object _cartLock = new object();
        private const int MaxCookieSize = 4000;

        // Khởi tạo controller: tiêm các dịch vụ như context, thông báo, bảo mật dữ liệu và logging.
        public ShoppingCartController(
            webshopContext context,
            INotyfService notyfService,
            IDataProtectionProvider provider,
            ILogger<ShoppingCartController> logger)
        {
            _context = context;
            _notyfService = notyfService;
            _protector = provider.CreateProtector("Cart");
            _logger = logger;
        }

        // Thuộc tính GioHang: Lấy hoặc cập nhật danh sách sản phẩm trong giỏ hàng.
        public List<CartItem> GioHang
        {
            // Khôi phục giỏ hàng từ cookie hoặc session, xác thực sản phẩm, lưu lại nếu có thay đổi.
            get
            {
                lock (_cartLock)
                {
                    var userId = HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                    var cartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString();
                    var simpleCart = RestoreCartFromCookie(userId, cartToken) ?? new SimpleCart
                    {
                        CustomerId = userId,
                        CartToken = cartToken,
                        Items = userId == "Anonymous" ? GetSessionCartItems() : new List<SimpleCartItem>()
                    };

                    var productDetailIds = simpleCart.Items.Select(i => i.ProductDetailId).ToList();
                    var productDetails = _context.ProductDetails
                        .Include(pd => pd.Product)
                        .Include(pd => pd.Size)
                        .Include(pd => pd.Color)
                        .Where(p => productDetailIds.Contains(p.ProductDetailId))
                        .Select(pd => new ProductDetailDTO
                        {
                            ProductDetailId = pd.ProductDetailId,
                            ProductId = pd.ProductId,
                            SizeId = pd.SizeId,
                            SizeName = pd.Size != null ? pd.Size.SizeName : null,
                            ColorId = pd.ColorId,
                            ColorName = pd.Color != null ? pd.Color.ColorName : null,
                            Stock = pd.Stock,
                            ProductActive = pd.Product.Active ?? false,
                            ProductDetailActive = pd.Active, // Thêm kiểm tra Active của ProductDetail
                            ProductName = pd.Product.ProductName,
                            Price = pd.Product.Price,
                            Thumb = pd.Product.Thumb
                        })
                        .ToListAsync()
                        .GetAwaiter()
                        .GetResult()
                        .ToDictionary(p => p.ProductDetailId);

                    var cartItems = new List<CartItem>();
                    var removedItems = new List<string>();

                    foreach (var item in simpleCart.Items)
                    {
                        if (!productDetails.TryGetValue(item.ProductDetailId, out var detail))
                        {
                            removedItems.Add($"Sản phẩm ID {item.ProductDetailId}");
                            _logger.LogInformation("Xóa sản phẩm không tồn tại: ID {ProductDetailId}", item.ProductDetailId);
                            continue;
                        }

                        if (!detail.ProductActive || detail.Stock < item.Amount || detail.SizeId <= 0 || detail.ColorId <= 0 || detail.Price == null)
                        {
                            removedItems.Add(detail.ProductName);
                            _logger.LogInformation("Xóa sản phẩm không hợp lệ: {ProductName}", detail.ProductName);
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
                                Price = (int?)detail.Price!.Value,
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
                        _logger.LogInformation("Xóa {Count} sản phẩm không hợp lệ: {Items}", removedItems.Count, string.Join(", ", removedItems));
                        simpleCart.Items = simpleCart.Items
                            .Where(i => productDetails.ContainsKey(i.ProductDetailId) &&
                                        productDetails[i.ProductDetailId].ProductActive &&
                                        productDetails[i.ProductDetailId].ProductDetailActive &&
                                        productDetails[i.ProductDetailId].Stock >= i.Amount &&
                                        productDetails[i.ProductDetailId].SizeId > 0 &&
                                        productDetails[i.ProductDetailId].ColorId > 0 &&
                                        productDetails[i.ProductDetailId].Price != null)
                            .ToList();
                        if (userId != "Anonymous")
                        {
                            SaveCartToCookie(simpleCart, userId);
                        }
                        else
                        {
                            ClearSessionCart();
                            foreach (var item in simpleCart.Items)
                            {
                                AddToSessionCart(item.ProductDetailId, item.Amount);
                            }
                        }
                    }

                    UpdateCartTokenCookie(cartToken);
                    return cartItems;
                }
            }
            // Cập nhật giỏ hàng: lưu danh sách sản phẩm mới vào cookie hoặc session.
            set
            {
                lock (_cartLock)
                {
                    var userId = HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                    var cartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString();
                    var simpleCart = new SimpleCart
                    {
                        CustomerId = userId,
                        CartToken = cartToken,
                        Items = value?.Select(item => new SimpleCartItem
                        {
                            ProductDetailId = item.productDetail.ProductDetailId,
                            Amount = item.amount
                        }).ToList() ?? new List<SimpleCartItem>()
                    };

                    if (userId != "Anonymous")
                    {
                        SaveCartToCookie(simpleCart, userId);
                    }
                    else
                    {
                        ClearSessionCart();
                        foreach (var item in simpleCart.Items)
                        {
                            AddToSessionCart(item.ProductDetailId, item.Amount);
                        }
                    }

                    UpdateCartTokenCookie(cartToken);
                }
            }
        }

        // Thêm sản phẩm vào giỏ hàng dựa trên session.
        private void AddToSessionCart(int productDetailId, int amount)
        {
            // Thêm hoặc cập nhật số lượng sản phẩm trong session của khách vãng lai.
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
                _logger.LogError(ex, "Lỗi khi thêm sản phẩm vào session: ProductDetailId={ProductDetailId}", productDetailId);
                _notyfService.Error("Lỗi khi thêm sản phẩm vào giỏ hàng.");
            }
        }

        // Lấy danh sách sản phẩm từ giỏ hàng trong session.
        private List<SimpleCartItem> GetSessionCartItems()
        {
            // Truy xuất danh sách sản phẩm từ session của khách vãng lai.
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
            _logger.LogInformation("Lấy giỏ session: {ItemCount} sản phẩm", cartItems.Count);
            return cartItems;
        }

        // Xóa tất cả sản phẩm khỏi giỏ hàng trong session.
        private void ClearSessionCart()
        {
            // Xóa toàn bộ dữ liệu giỏ hàng trong session của khách vãng lai.
            foreach (var key in HttpContext.Session.Keys.Where(k => k.StartsWith("CartItem_")))
            {
                HttpContext.Session.Remove(key);
            }
            _logger.LogInformation("Đã xóa giỏ session");
        }

        // Khôi phục giỏ hàng từ cookie.
        private SimpleCart RestoreCartFromCookie(string customerId, string cartToken)
        {
            // Giải mã cookie giỏ hàng, trả về giỏ nếu hợp lệ, xóa cookie nếu lỗi.
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
                        cart.CartToken = cartToken;
                        cart.Items = cart.Items ?? new List<SimpleCartItem>();
                        _logger.LogInformation("Khôi phục giỏ hàng từ cookie: CustomerId={CustomerId}, Items={ItemCount}", customerId, cart.Items.Count);
                        return cart;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi khôi phục giỏ hàng từ cookie: CustomerId={CustomerId}", customerId);
                    _notyfService.Error("Lỗi khôi phục giỏ hàng.");
                    Response.Cookies.Delete(cookieKey);
                }
            }
            return null;
        }

        // Khôi phục giỏ Mua ngay từ cookie.
        private SimpleCart RestoreBuyNowFromCookie(string customerId, string cartToken)
        {
            // Giải mã cookie giỏ Mua ngay, trả về giỏ nếu hợp lệ, xóa cookie nếu lỗi.
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

        // Lưu giỏ hàng vào cookie với mã hóa.
        private void SaveCartToCookie(SimpleCart cart, string customerId)
        {
            // Mã hóa giỏ hàng, lưu vào cookie với thời hạn 10 năm, kiểm tra kích thước và cập nhật local storage.
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

                if (base64Data.Length > MaxCookieSize)
                {
                    int maxItems = EstimateMaxItems(serializedCart, cart.Items.Count);
                    if (maxItems < cart.Items.Count)
                    {
                        cart.Items = cart.Items.Take(maxItems).ToList();
                        serializedCart = JsonSerializer.Serialize(cart, new JsonSerializerOptions { WriteIndented = false });
                        encryptedData = _protector.Protect(System.Text.Encoding.UTF8.GetBytes(serializedCart));
                        base64Data = Convert.ToBase64String(encryptedData);
                        _notyfService.Warning($"Giỏ hàng vượt quá dung lượng cookie: chỉ lưu {maxItems} sản phẩm.");
                        _logger.LogWarning("Giỏ hàng vượt quá dung lượng cookie: CustomerId={CustomerId}, MaxItems={MaxItems}", customerId, maxItems);
                    }
                }

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
                _logger.LogInformation("Lưu giỏ hàng vào cookie: CustomerId={CustomerId}, Items={ItemCount}", customerId, cart.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu giỏ hàng vào cookie: CustomerId={CustomerId}", customerId);
                _notyfService.Error("Lỗi khi lưu giỏ hàng.");
            }
        }

        // Lưu giỏ Mua ngay vào cookie với mã hóa.
        private void SaveBuyNowToCookie(SimpleCart cart, string customerId)
        {
            // Mã hóa giỏ Mua ngay, lưu vào cookie với thời hạn 1 giờ, cập nhật local storage.
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
                    Expires = DateTimeOffset.Now.AddHours(1), // Thời gian hết hạn ngắn cho Mua ngay
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                };

                Response.Cookies.Append(cookieKey, base64Data, cookieOptions);
                Response.Cookies.Append("GlobalCartToken", cart.CartToken, cookieOptions);

                HttpContext.Items["BuyNowCartForLocalStorage"] = new
                {
                    cartToken = cart.CartToken,
                    customerId = customerId,
                    items = cart.Items.Select(item => new
                    {
                        productDetailId = item.ProductDetailId,
                        amount = item.Amount
                    }).ToList()
                };
                _logger.LogInformation("Lưu giỏ Mua ngay vào cookie: CustomerId={CustomerId}, Items={ItemCount}", customerId, cart.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu giỏ Mua ngay vào cookie: CustomerId={CustomerId}", customerId);
                _notyfService.Error("Lỗi khi lưu giỏ Mua ngay.");
            }
        }

        // Ước tính số lượng sản phẩm tối đa có thể lưu trong cookie.
        private int EstimateMaxItems(string serializedCart, int currentItemCount)
        {
            // Tính toán số lượng sản phẩm tối đa dựa trên kích thước cookie cho phép.
            if (currentItemCount == 0) return 0;
            int approxSizePerItem = serializedCart.Length / currentItemCount;
            return Math.Max(1, (int)((MaxCookieSize * 0.8) / (approxSizePerItem + 100)));
        }

        // Cập nhật token giỏ hàng trong cookie.
        private void UpdateCartTokenCookie(string cartToken)
        {
            // Lưu GlobalCartToken vào cookie với thời hạn 10 năm.
            var cookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddYears(10),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            };
            Response.Cookies.Append("GlobalCartToken", cartToken, cookieOptions);
            _logger.LogInformation("Cập nhật GlobalCartToken: {CartToken}", cartToken);
        }

        // Kiểm tra trạng thái đăng nhập của người dùng.
        [HttpGet]
        [Route("api/account/check-login")]
        [AllowAnonymous]
        public IActionResult CheckLogin()
        {
            // Kiểm tra xem người dùng đã đăng nhập hay chưa, trả về trạng thái đăng nhập.
            var userId = HttpContext.Session.GetString("CustomerId");
            bool isLoggedIn = !string.IsNullOrEmpty(userId) && int.TryParse(userId, out _);
            _logger.LogInformation("Kiểm tra đăng nhập: CustomerId={CustomerId}, IsLoggedIn={IsLoggedIn}", userId, isLoggedIn);
            return Json(new { success = true, isLoggedIn });
        }

        // Lấy số lượng sản phẩm trong giỏ hàng.
        [HttpGet]
        [Route("api/cart/count")]
        [AllowAnonymous]
        public IActionResult GetCartCount()
        {
            // Tính tổng số lượng sản phẩm trong giỏ hàng, trả về kết quả.
            try
            {
                var cart = GioHang;
                int count = cart.Sum(x => x.amount);
                _logger.LogInformation("Lấy số lượng giỏ hàng: Count={Count}", count);
                return Json(new { success = true, count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy số lượng giỏ hàng");
                return Json(new { success = false, message = "Lỗi khi lấy số lượng giỏ hàng." });
            }
        }

        // Thêm sản phẩm vào giỏ hàng.
        [HttpPost]
        [Route("api/cart/add")]
        [AllowAnonymous]
        public async Task<IActionResult> AddToCart([FromBody] CartRequest request)
        {
            // Kiểm tra dữ liệu, xác thực sản phẩm, thêm vào giỏ hàng và lưu lại.
            try
            {
                if (request == null || request.ProductDetailId <= 0 || !request.Amount.HasValue || request.Amount <= 0)
                {
                    _notyfService.Error("Dữ liệu không hợp lệ");
                    _logger.LogWarning("Dữ liệu không hợp lệ: ProductDetailId={ProductDetailId}, Amount={Amount}", request?.ProductDetailId, request?.Amount);
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var customerId = HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                var cartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString();

                var productDetail = await _context.ProductDetails
                    .Include(pd => pd.Product)
                    .Include(pd => pd.Size)
                    .Include(pd => pd.Color)
                    .AsNoTracking()
                    .Select(pd => new ProductDetailDTO
                    {
                        ProductDetailId = pd.ProductDetailId,
                        ProductId = pd.ProductId,
                        SizeId = pd.SizeId,
                        SizeName = pd.Size != null ? pd.Size.SizeName : null,
                        ColorId = pd.ColorId,
                        ColorName = pd.Color != null ? pd.Color.ColorName : null,
                        Stock = pd.Stock,
                        ProductActive = pd.Product.Active ?? false,
                        ProductDetailActive = pd.Active, // Thêm kiểm tra Active của ProductDetail
                        ProductName = pd.Product.ProductName,
                        Price = pd.Product.Price,
                        Thumb = pd.Product.Thumb
                    })
                    .FirstOrDefaultAsync(x => x.ProductDetailId == request.ProductDetailId);

                if (productDetail == null || !(productDetail.ProductActive) || !(productDetail.ProductDetailActive) || productDetail.Stock <= 0 || productDetail.SizeId <= 0 || productDetail.ColorId <= 0 || productDetail.Price == null)
                {
                    var message = productDetail == null ? "Sản phẩm không tồn tại" : productDetail.Stock <= 0 ? "Sản phẩm đã hết hàng!" : "Sản phẩm không hợp lệ";
                    _notyfService.Error(message);
                    _logger.LogWarning("Sản phẩm không hợp lệ: ProductDetailId={ProductDetailId}, Message={Message}", request.ProductDetailId, message);
                    return Json(new { success = false, message });
                }

                int requestedAmount = request.Amount.Value;
                if (requestedAmount > productDetail.Stock)
                {
                    _notyfService.Error($"Số lượng vượt quá tồn kho (còn {productDetail.Stock} sản phẩm)");
                    _logger.LogWarning("Số lượng vượt quá tồn kho: ProductDetailId={ProductDetailId}, Requested={Requested}, Stock={Stock}",
                        request.ProductDetailId, requestedAmount, productDetail.Stock);
                    return Json(new { success = false, message = $"Số lượng vượt quá tồn kho (còn {productDetail.Stock} sản phẩm)" });
                }

                lock (_cartLock)
                {
                    SimpleCart cart;
                    if (customerId != "Anonymous")
                    {
                        cart = RestoreCartFromCookie(customerId, cartToken) ?? new SimpleCart
                        {
                            CustomerId = customerId,
                            CartToken = cartToken,
                            Items = new List<SimpleCartItem>()
                        };
                        var existingItem = cart.Items.FirstOrDefault(x => x.ProductDetailId == request.ProductDetailId);
                        if (existingItem != null)
                        {
                            int newAmount = existingItem.Amount + requestedAmount;
                            if (newAmount > productDetail.Stock)
                            {
                                _notyfService.Error($"Số lượng vượt quá tồn kho (còn {productDetail.Stock} sản phẩm)");
                                _logger.LogWarning("Tổng số lượng vượt quá tồn kho: ProductDetailId={ProductDetailId}, NewAmount={NewAmount}, Stock={Stock}",
                                    request.ProductDetailId, newAmount, productDetail.Stock);
                                return Json(new { success = false, message = $"Số lượng vượt quá tồn kho (còn {productDetail.Stock} sản phẩm)" });
                            }
                            existingItem.Amount = newAmount;
                        }
                        else
                        {
                            cart.Items.Add(new SimpleCartItem
                            {
                                ProductDetailId = request.ProductDetailId,
                                Amount = requestedAmount
                            });
                        }
                        SaveCartToCookie(cart, customerId);
                    }
                    else
                    {
                        AddToSessionCart(request.ProductDetailId, requestedAmount);
                        cart = new SimpleCart
                        {
                            CustomerId = customerId,
                            CartToken = cartToken,
                            Items = GetSessionCartItems()
                        };
                    }

                    _logger.LogInformation("Đã thêm sản phẩm vào giỏ: CustomerId={CustomerId}, ProductDetailId={ProductDetailId}, Amount={Amount}",
                        customerId, request.ProductDetailId, requestedAmount);
                    var localStorageCart = HttpContext.Items["CartForLocalStorage"];
                    _notyfService.Success("Thêm sản phẩm vào giỏ hàng thành công");
                    return Json(new { success = true, cartCount = cart.Items.Sum(x => x.Amount), cartToken, localStorageCart });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm sản phẩm vào giỏ: ProductDetailId={ProductDetailId}", request?.ProductDetailId);
                _notyfService.Error("Lỗi khi thêm sản phẩm vào giỏ hàng.");
                return Json(new { success = false, message = "Lỗi khi thêm sản phẩm vào giỏ hàng." });
            }
        }

        // Xử lý yêu cầu mua ngay.
        [HttpPost]
        [Route("api/cart/buy-now")]
        [AllowAnonymous]
        public async Task<IActionResult> BuyNow([FromBody] CartRequest request)
        {
            // Kiểm tra dữ liệu, xác thực sản phẩm, tạo giỏ Mua ngay và chuyển hướng đến trang thanh toán.
            try
            {
                if (request == null || request.ProductDetailId <= 0 || !request.Amount.HasValue || request.Amount <= 0)
                {
                    _notyfService.Error("Dữ liệu không hợp lệ");
                    _logger.LogWarning("Dữ liệu không hợp lệ: ProductDetailId={ProductDetailId}, Amount={Amount}", request?.ProductDetailId, request?.Amount);
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var customerId = HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                var cartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString();

                var productDetail = await _context.ProductDetails
                    .Include(pd => pd.Product)
                    .Include(pd => pd.Size)
                    .Include(pd => pd.Color)
                    .AsNoTracking()
                    .Select(pd => new ProductDetailDTO
                    {
                        ProductDetailId = pd.ProductDetailId,
                        ProductId = pd.ProductId,
                        SizeId = pd.SizeId,
                        SizeName = pd.Size != null ? pd.Size.SizeName : null,
                        ColorId = pd.ColorId,
                        ColorName = pd.Color != null ? pd.Color.ColorName : null,
                        Stock = pd.Stock,
                        ProductActive = pd.Product.Active ?? false,
                        ProductDetailActive = pd.Active, // Thêm kiểm tra Active của ProductDetail
                        ProductName = pd.Product.ProductName,
                        Price = pd.Product.Price,
                        Thumb = pd.Product.Thumb
                    })
                    .FirstOrDefaultAsync(x => x.ProductDetailId == request.ProductDetailId);

                if (productDetail == null || !(productDetail.ProductActive) || !(productDetail.ProductDetailActive) || productDetail.Stock <= 0 || productDetail.SizeId <= 0 || productDetail.ColorId <= 0 || productDetail.Price == null)
                {
                    var message = productDetail == null ? "Sản phẩm không tồn tại" : productDetail.Stock <= 0 ? "Sản phẩm đã hết hàng!" : "Sản phẩm không hợp lệ";
                    _notyfService.Error(message);
                    _logger.LogWarning("Sản phẩm không hợp lệ: ProductDetailId={ProductDetailId}, Message={Message}", request.ProductDetailId, message);
                    return Json(new { success = false, message });
                }

                int requestedAmount = request.Amount.Value;
                if (requestedAmount > productDetail.Stock)
                {
                    _notyfService.Error($"Số lượng vượt quá tồn kho (còn {productDetail.Stock} sản phẩm)");
                    _logger.LogWarning("Số lượng vượt quá tồn kho: ProductDetailId={ProductDetailId}, Requested={Requested}, Stock={Stock}",
                        request.ProductDetailId, requestedAmount, productDetail.Stock);
                    return Json(new { success = false, message = $"Số lượng vượt quá tồn kho (còn {productDetail.Stock} sản phẩm)" });
                }

                lock (_cartLock)
                {
                    var buyNowCart = new SimpleCart
                    {
                        CustomerId = customerId,
                        CartToken = cartToken,
                        Items = new List<SimpleCartItem>
                        {
                            new SimpleCartItem
                            {
                                ProductDetailId = request.ProductDetailId,
                                Amount = requestedAmount
                            }
                        }
                    };

                    SaveBuyNowToCookie(buyNowCart, customerId);
                    _logger.LogInformation("Xử lý mua ngay: CustomerId={CustomerId}, ProductDetailId={ProductDetailId}, Amount={Amount}",
                        customerId, request.ProductDetailId, requestedAmount);

                    return Json(new { success = true, redirectUrl = customerId == "Anonymous" ? "/dang-nhap.html?returnUrl=/checkout.html" : "/checkout.html" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý mua ngay: ProductDetailId={ProductDetailId}", request?.ProductDetailId);
                _notyfService.Error("Lỗi khi xử lý mua ngay.");
                return Json(new { success = false, message = "Lỗi khi xử lý mua ngay." });
            }
        }

        // Chuyển hướng đến trang thanh toán.
        [HttpPost]
        [Route("api/cart/proceed-to-checkout")]
        [AllowAnonymous]
        public IActionResult ProceedToCheckout()
        {
            // Kiểm tra giỏ hàng, lưu giỏ hàng và chuyển hướng đến trang thanh toán.
            try
            {
                var customerId = HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                var cart = GioHang;

                if (!cart.Any())
                {
                    _notyfService.Error("Giỏ hàng trống!");
                    _logger.LogWarning("Giỏ hàng trống: CustomerId={CustomerId}", customerId);
                    return Json(new { success = false, message = "Giỏ hàng trống!" });
                }

                var cartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString();
                var simpleCart = new SimpleCart
                {
                    CustomerId = customerId,
                    CartToken = cartToken,
                    Items = cart.Select(item => new SimpleCartItem
                    {
                        ProductDetailId = item.productDetail.ProductDetailId,
                        Amount = item.amount
                    }).ToList()
                };

                SaveCartToCookie(simpleCart, customerId);
                UpdateCartTokenCookie(cartToken);
                _logger.LogInformation("Chuyển đến thanh toán: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, cart.Count);

                return Json(new { success = true, redirectUrl = customerId == "Anonymous" ? "/dang-nhap.html?returnUrl=/checkout.html" : "/checkout.html" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chuyển đến thanh toán: CustomerId={CustomerId}", HttpContext.Session.GetString("CustomerId"));
                _notyfService.Error("Lỗi khi chuyển đến thanh toán.");
                return Json(new { success = false, message = "Lỗi khi chuyển đến thanh toán." });
            }
        }

        // Xóa một sản phẩm khỏi giỏ hàng.
        [HttpPost]
        [Route("api/cart/remove")]
        [AllowAnonymous]
        public IActionResult RemoveFromCart([FromBody] CartRequest request)
        {
            // Kiểm tra dữ liệu, xóa sản phẩm khỏi giỏ hàng và lưu lại.
            try
            {
                if (request == null || request.ProductDetailId <= 0)
                {
                    _notyfService.Error("Dữ liệu không hợp lệ");
                    _logger.LogWarning("Dữ liệu không hợp lệ: ProductDetailId={ProductDetailId}", request?.ProductDetailId);
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var customerId = HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                var cartToken = Request.Cookies["GlobalCartToken"];

                lock (_cartLock)
                {
                    var cart = RestoreCartFromCookie(customerId, cartToken);
                    if (cart == null)
                    {
                        _notyfService.Error("Giỏ hàng không tồn tại");
                        _logger.LogWarning("Giỏ hàng không tồn tại: CustomerId={CustomerId}", customerId);
                        return Json(new { success = false, message = "Giỏ hàng không tồn tại" });
                    }

                    var cartItem = cart.Items.FirstOrDefault(x => x.ProductDetailId == request.ProductDetailId);
                    if (cartItem == null)
                    {
                        _notyfService.Error("Sản phẩm không tồn tại trong giỏ hàng");
                        _logger.LogWarning("Sản phẩm không tồn tại trong giỏ: CustomerId={CustomerId}, ProductDetailId={ProductDetailId}",
                            customerId, request.ProductDetailId);
                        return Json(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng" });
                    }

                    cart.Items.Remove(cartItem);
                    if (customerId != "Anonymous")
                    {
                        SaveCartToCookie(cart, customerId);
                    }
                    else
                    {
                        ClearSessionCart();
                        foreach (var item in cart.Items)
                        {
                            AddToSessionCart(item.ProductDetailId, item.Amount);
                        }
                    }

                    _logger.LogInformation("Xóa sản phẩm khỏi giỏ: CustomerId={CustomerId}, ProductDetailId={ProductDetailId}", customerId, request.ProductDetailId);
                    var localStorageCart = HttpContext.Items["CartForLocalStorage"];
                    _notyfService.Success("Xóa sản phẩm thành công");
                    return Json(new
                    {
                        success = true,
                        cartCount = cart.Items.Sum(x => x.Amount),
                        cartToken,
                        localStorageCart
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa sản phẩm: CustomerId={CustomerId}, ProductDetailId={ProductDetailId}",
                    HttpContext.Session.GetString("CustomerId"), request?.ProductDetailId);
                _notyfService.Error("Lỗi khi xóa sản phẩm.");
                return Json(new { success = false, message = "Lỗi khi xóa sản phẩm." });
            }
        }

        // Xóa nhiều sản phẩm khỏi giỏ hàng.
        [HttpPost]
        [Route("api/cart/remove-multiple")]
        [AllowAnonymous]
        public IActionResult RemoveMultipleFromCart([FromBody] RemoveMultipleRequest request)
        {
            // Kiểm tra dữ liệu, xóa nhiều sản phẩm khỏi giỏ hàng và lưu lại.
            try
            {
                if (request == null || request.ProductDetailIds == null || !request.ProductDetailIds.Any())
                {
                    _notyfService.Error("Dữ liệu không hợp lệ");
                    _logger.LogWarning("Dữ liệu không hợp lệ: ProductDetailIds={ProductDetailIds}", request?.ProductDetailIds?.Count);
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var customerId = HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                var cartToken = Request.Cookies["GlobalCartToken"] ?? "";

                lock (_cartLock)
                {
                    var cart = RestoreCartFromCookie(customerId, cartToken) ?? new SimpleCart
                    {
                        CustomerId = customerId,
                        CartToken = cartToken,
                        Items = new List<SimpleCartItem>()
                    };

                    var itemsToRemove = cart.Items.Where(x => request.ProductDetailIds.Contains(x.ProductDetailId)).ToList();
                    if (!itemsToRemove.Any())
                    {
                        _notyfService.Error("Không tìm thấy sản phẩm nào trong giỏ hàng!");
                        _logger.LogWarning("Không tìm thấy sản phẩm để xóa: CustomerId={CustomerId}, ProductDetailIds={Ids}",
                            customerId, string.Join(",", request.ProductDetailIds));
                        return Json(new { success = false, message = "Không tìm thấy sản phẩm nào trong giỏ hàng!" });
                    }

                    foreach (var item in itemsToRemove)
                    {
                        cart.Items.Remove(item);
                    }

                    if (customerId != "Anonymous")
                    {
                        SaveCartToCookie(cart, customerId);
                    }
                    else
                    {
                        ClearSessionCart();
                        foreach (var item in cart.Items)
                        {
                            AddToSessionCart(item.ProductDetailId, item.Amount);
                        }
                    }

                    _logger.LogInformation("Xóa nhiều sản phẩm khỏi giỏ: CustomerId={CustomerId}, ProductDetailIds={Ids}",
                        customerId, string.Join(",", request.ProductDetailIds));
                    var localStorageCart = HttpContext.Items["CartForLocalStorage"];
                    _notyfService.Success("Xóa các sản phẩm thành công!");
                    return Json(new
                    {
                        success = true,
                        cartCount = cart.Items.Sum(x => x.Amount),
                        cartToken,
                        localStorageCart
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa nhiều sản phẩm: CustomerId={CustomerId}", HttpContext.Session.GetString("CustomerId"));
                _notyfService.Error("Lỗi khi xóa sản phẩm.");
                return Json(new { success = false, message = "Lỗi khi xóa sản phẩm." });
            }
        }

        // Cập nhật số lượng sản phẩm trong giỏ hàng.
        [HttpPost]
        [Route("api/cart/update")]
        [AllowAnonymous]
        public IActionResult UpdateCart([FromBody] CartRequest request)
        {
            // Kiểm tra dữ liệu, xác thực sản phẩm, cập nhật số lượng trong giỏ hàng và lưu lại.
            try
            {
                if (request == null || request.ProductDetailId <= 0 || !request.Amount.HasValue || request.Amount < 0)
                {
                    _notyfService.Error("Dữ liệu không hợp lệ");
                    _logger.LogWarning("Dữ liệu không hợp lệ: ProductDetailId={ProductDetailId}, Amount={Amount}", request?.ProductDetailId, request?.Amount);
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var customerId = HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                var cartToken = Request.Cookies["GlobalCartToken"] ?? "";

                var productDetail = _context.ProductDetails
                    .Include(pd => pd.Product)
                    .FirstOrDefault(x => x.ProductDetailId == request.ProductDetailId);

                if (productDetail == null || !(productDetail.Product.Active ?? false) || !productDetail.Active || productDetail.Stock <= 0)
                {
                    var message = productDetail == null ? "Sản phẩm không tồn tại" : productDetail.Stock <= 0 ? "Sản phẩm đã hết hàng!" : "Sản phẩm không hoạt động";
                    _notyfService.Error(message);
                    _logger.LogWarning("Sản phẩm không hợp lệ: ProductDetailId={ProductDetailId}, Message={Message}", request.ProductDetailId, message);
                    return Json(new { success = false, message });
                }

                if (request.Amount > productDetail.Stock)
                {
                    _notyfService.Error($"Số lượng vượt quá tồn kho (còn {productDetail.Stock} sản phẩm)");
                    _logger.LogWarning("Số lượng vượt quá tồn kho: ProductDetailId={ProductDetailId}, Requested={Requested}, Stock={Stock}",
                        request.ProductDetailId, request.Amount, productDetail.Stock);
                    return Json(new { success = false, message = $"Số lượng vượt quá tồn kho (còn {productDetail.Stock} sản phẩm)" });
                }

                lock (_cartLock)
                {
                    var cart = RestoreCartFromCookie(customerId, cartToken);
                    if (cart == null)
                    {
                        _notyfService.Error("Giỏ hàng không tồn tại");
                        _logger.LogWarning("Giỏ hàng không tồn tại: CustomerId={CustomerId}", customerId);
                        return Json(new { success = false, message = "Giỏ hàng không tồn tại" });
                    }

                    var cartItem = cart.Items.FirstOrDefault(x => x.ProductDetailId == request.ProductDetailId);
                    if (cartItem == null)
                    {
                        _notyfService.Error("Sản phẩm không tồn tại trong giỏ hàng");
                        _logger.LogWarning("Sản phẩm không tồn tại trong giỏ: CustomerId={CustomerId}, ProductDetailId={ProductDetailId}",
                            customerId, request.ProductDetailId);
                        return Json(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng" });
                    }

                    cartItem.Amount = request.Amount.Value;
                    if (customerId != "Anonymous")
                    {
                        SaveCartToCookie(cart, customerId);
                    }
                    else
                    {
                        ClearSessionCart();
                        foreach (var item in cart.Items)
                        {
                            AddToSessionCart(item.ProductDetailId, item.Amount);
                        }
                    }

                    _logger.LogInformation("Cập nhật giỏ hàng: CustomerId={CustomerId}, ProductDetailId={ProductDetailId}, NewAmount={NewAmount}",
                        customerId, request.ProductDetailId, request.Amount.Value);
                    var localStorageCart = HttpContext.Items["CartForLocalStorage"];
                    _notyfService.Success("Cập nhật giỏ hàng thành công");
                    return Json(new
                    {
                        success = true,
                        cartCount = cart.Items.Sum(x => x.Amount),
                        cartToken,
                        localStorageCart
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật giỏ hàng: CustomerId={CustomerId}, ProductDetailId={ProductDetailId}",
                    HttpContext.Session.GetString("CustomerId"), request?.ProductDetailId);
                _notyfService.Error("Lỗi khi cập nhật giỏ hàng.");
                return Json(new { success = false, message = "Lỗi khi cập nhật giỏ hàng." });
            }
        }

        // Đồng bộ giỏ hàng giữa server và client (đồng bộ giỏ hàng cho vãng lai với customerid).
        [HttpPost]
        [Route("api/cart/sync")]
        [AllowAnonymous]
        public IActionResult SyncCart([FromBody] SyncCartRequest request)
        {
            // Hợp nhất giỏ hàng ẩn danh và giỏ Mua ngay với giỏ hàng của người dùng đã đăng nhập, lưu lại.
            try
            {
                var customerId = request?.CustomerId.ToString() ?? HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                var cartToken = Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString();

                lock (_cartLock)
                {
                    var cart = RestoreCartFromCookie(customerId, cartToken) ?? new SimpleCart
                    {
                        CustomerId = customerId,
                        CartToken = cartToken,
                        Items = new List<SimpleCartItem>()
                    };

                    if (customerId != "Anonymous" && request?.CustomerId > 0)
                    {
                        var anonymousCart = RestoreCartFromCookie("Anonymous", cartToken);
                        if (anonymousCart != null && anonymousCart.Items.Any())
                        {
                            foreach (var item in anonymousCart.Items)
                            {
                                var existingItem = cart.Items.FirstOrDefault(x => x.ProductDetailId == item.ProductDetailId);
                                if (existingItem != null)
                                {
                                    existingItem.Amount += item.Amount;
                                }
                                else
                                {
                                    cart.Items.Add(item);
                                }
                            }
                            Response.Cookies.Delete("Cart_Anonymous");
                            _logger.LogInformation("Hợp nhất giỏ hàng ẩn danh: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, anonymousCart.Items.Count);
                        }

                        var anonymousBuyNowCart = RestoreBuyNowFromCookie("Anonymous", cartToken);
                        if (anonymousBuyNowCart != null && anonymousBuyNowCart.Items.Any())
                        {
                            var buyNowCart = RestoreBuyNowFromCookie(customerId, cartToken) ?? new SimpleCart
                            {
                                CustomerId = customerId,
                                CartToken = cartToken,
                                Items = new List<SimpleCartItem>()
                            };
                            foreach (var item in anonymousBuyNowCart.Items)
                            {
                                var existingItem = buyNowCart.Items.FirstOrDefault(x => x.ProductDetailId == item.ProductDetailId);
                                if (existingItem != null)
                                {
                                    existingItem.Amount = item.Amount; // Ghi đè cho Mua ngay
                                }
                                else
                                {
                                    buyNowCart.Items.Add(item);
                                }
                            }
                            SaveBuyNowToCookie(buyNowCart, customerId);
                            Response.Cookies.Delete("BuyNowCart_Anonymous");
                            _logger.LogInformation("Hợp nhất giỏ Mua ngay ẩn danh: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, anonymousBuyNowCart.Items.Count);
                        }
                    }

                    var fullCart = GioHang;
                    SaveCartToCookie(cart, customerId);
                    _logger.LogInformation("Đồng bộ giỏ hàng: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, fullCart.Count);
                    var localStorageCart = HttpContext.Items["CartForLocalStorage"];
                    _notyfService.Success("Đồng bộ giỏ hàng thành công");
                    return Json(new
                    {
                        success = true,
                        message = "Đồng bộ giỏ hàng thành công",
                        cartCount = fullCart.Sum(x => x.amount),
                        cartToken = cart.CartToken,
                        localStorageCart
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ giỏ hàng: CustomerId={CustomerId}", HttpContext.Session.GetString("CustomerId"));
                _notyfService.Error("Lỗi khi đồng bộ giỏ hàng.");
                return Json(new { success = false, message = "Lỗi khi đồng bộ giỏ hàng." });
            }
        }

        // Đồng bộ giỏ hàng từ localStorage.
        [HttpPost]
        [Route("api/cart/sync-local")]
        [AllowAnonymous]
        public IActionResult SyncLocalCart([FromBody] LocalCartSyncRequest request)
        {
            // Đồng bộ giỏ hàng từ localStorage với server, xác thực sản phẩm và lưu lại.
            try
            {
                var customerId = request?.CustomerId.ToString() ?? HttpContext.Session.GetString("CustomerId") ?? "Anonymous";
                var cartToken = request?.CartToken ?? Request.Cookies["GlobalCartToken"] ?? Guid.NewGuid().ToString();

                lock (_cartLock)
                {
                    var cart = RestoreCartFromCookie(customerId, cartToken) ?? new SimpleCart
                    {
                        CustomerId = customerId,
                        CartToken = cartToken,
                        Items = new List<SimpleCartItem>()
                    };

                    if (request?.Items != null && request.Items.Any())
                    {
                        foreach (var item in request.Items)
                        {
                            var productDetail = _context.ProductDetails
                                .Include(pd => pd.Product)
                                .FirstOrDefault(p => p.ProductDetailId == item.ProductDetailId);

                            if (productDetail == null || !(productDetail.Product.Active ?? false) || !productDetail.Active || productDetail.Stock < item.Amount)
                            {
                                _logger.LogWarning("Sản phẩm không hợp lệ khi đồng bộ local: ProductDetailId={ProductDetailId}", item.ProductDetailId);
                                continue;
                            }
                            var existingItem = cart.Items.FirstOrDefault(x => x.ProductDetailId == item.ProductDetailId);
                            if (existingItem != null)
                            {
                                existingItem.Amount += item.Amount;
                            }
                            else
                            {
                                cart.Items.Add(new SimpleCartItem
                                {
                                    ProductDetailId = item.ProductDetailId,
                                    Amount = item.Amount
                                });
                            }
                        }
                    }

                    if (customerId != "Anonymous")
                    {
                        Response.Cookies.Delete("Cart_Anonymous");
                        SaveCartToCookie(cart, customerId);
                    }
                    else
                    {
                        ClearSessionCart();
                        foreach (var item in cart.Items)
                        {
                            AddToSessionCart(item.ProductDetailId, item.Amount);
                        }
                    }

                    _logger.LogInformation("Đồng bộ giỏ hàng từ localStorage: CustomerId={CustomerId}, ItemCount={ItemCount}", customerId, cart.Items.Count);
                    var fullCart = GioHang;
                    var localStorageCart = HttpContext.Items["CartForLocalStorage"];
                    _notyfService.Success("Đồng bộ giỏ hàng từ localStorage thành công");
                    return Json(new
                    {
                        success = true,
                        message = "Đồng bộ giỏ hàng từ localStorage thành công",
                        cartCount = fullCart.Sum(x => x.amount),
                        cartToken = cart.CartToken,
                        localStorageCart
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ giỏ từ localStorage: CustomerId={CustomerId}", HttpContext.Session.GetString("CustomerId"));
                _notyfService.Error("Lỗi khi đồng bộ giỏ hàng từ localStorage.");
                return Json(new { success = false, message = "Lỗi khi đồng bộ giỏ hàng từ localStorage." });
            }
        }

        // Lấy thông tin giảm giá từ session.
        [HttpGet]
        [Route("api/cart/get-discount")]
        [AllowAnonymous]
        public IActionResult GetDiscount()
        {
            // Truy xuất phần trăm giảm giá từ session, trả về kết quả.
            try
            {
                var discountString = HttpContext.Session.GetString("CouponDiscount");
                double discountPercentage = 0.0;
                if (!string.IsNullOrEmpty(discountString))
                {
                    double.TryParse(discountString, out discountPercentage);
                }
                _logger.LogInformation("Lấy thông tin giảm giá: DiscountPercentage={DiscountPercentage}", discountPercentage);
                return Json(new { success = true, discountPercentage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin giảm giá");
                return Json(new { success = false, discountPercentage = 0.0, message = "Lỗi khi lấy thông tin giảm giá." });
            }
        }

        // Hiển thị trang giỏ hàng.
        [Route("cart.html")]
        public IActionResult Index()
        {
            // Lấy giỏ hàng, thông tin giảm giá và hiển thị trang giỏ hàng.
            var cartItems = GioHang;
            var discountPercentage = 0.0;
            var discountString = HttpContext.Session.GetString("CouponDiscount");
            if (!string.IsNullOrEmpty(discountString))
            {
                double.TryParse(discountString, out discountPercentage);
            }
            ViewBag.DiscountPercentage = discountPercentage;
            ViewBag.GlobalCartToken = Request.Cookies["GlobalCartToken"];
            _logger.LogInformation("Hiển thị giỏ hàng: ItemCount={ItemCount}, DiscountPercentage={DiscountPercentage}",
                cartItems.Count, discountPercentage);
            return View(cartItems);
        }

        // Lớp ProductDetailDTO: Đại diện cho thông tin chi tiết sản phẩm.
        public class ProductDetailDTO
        {
            public int ProductDetailId { get; set; }
            public int ProductId { get; set; }
            public int? SizeId { get; set; }
            public string SizeName { get; set; }
            public int? ColorId { get; set; }
            public string ColorName { get; set; }
            public int? Stock { get; set; }
            public bool ProductActive { get; set; }
            public bool ProductDetailActive { get; set; } // Thêm thuộc tính mới
            public string ProductName { get; set; }
            public decimal? Price { get; set; }
            public string Thumb { get; set; }
        }

        // Lớp CartRequest: Đại diện cho yêu cầu thêm hoặc cập nhật sản phẩm trong giỏ hàng.
        public class CartRequest
        {
            public int ProductDetailId { get; set; }
            public int? Amount { get; set; }
        }

        // Lớp RemoveMultipleRequest: Đại diện cho yêu cầu xóa nhiều sản phẩm khỏi giỏ hàng.
        public class RemoveMultipleRequest
        {
            public List<int> ProductDetailIds { get; set; }
        }

        // Lớp SyncCartRequest: Đại diện cho yêu cầu đồng bộ giỏ hàng với server.
        public class SyncCartRequest
        {
            public int CustomerId { get; set; }
        }

        // Lớp LocalCartSyncRequest: Đại diện cho yêu cầu đồng bộ giỏ hàng từ localStorage.
        public class LocalCartSyncRequest
        {
            public string CartToken { get; set; }
            public int CustomerId { get; set; }
            public List<CartItemRequest> Items { get; set; }
        }

        // Lớp CartItemRequest: Đại diện cho một mục sản phẩm trong yêu cầu đồng bộ localStorage.
        public class CartItemRequest
        {
            public int ProductDetailId { get; set; }
            public int Amount { get; set; }
        }

        // Lớp SimpleCart: Đại diện cho giỏ hàng đơn giản dùng để lưu trữ trong cookie.
        public class SimpleCart
        {
            public string CustomerId { get; set; }
            public string CartToken { get; set; }
            public List<SimpleCartItem> Items { get; set; }
        }

        // Lớp SimpleCartItem: Đại diện cho một mục sản phẩm trong giỏ hàng đơn giản.
        public class SimpleCartItem
        {
            public int ProductDetailId { get; set; }
            public int Amount { get; set; }
        }
    }
}