using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PagedList.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Helpper;
using WebShop.Models;

namespace WebShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuthentication")]
    public class AdminProductsController : Controller
    {
        private readonly webshopContext _context;
        private readonly INotyfService _notifyService;

        public AdminProductsController(webshopContext context, INotyfService notifyService)
        {
            _context = context;
            _notifyService = notifyService;
        }

        public IActionResult Index(int page = 1, int CatID = 0)
        {
            var accountId = HttpContext.Session.GetString("AccountId");
            if (string.IsNullOrEmpty(accountId))
            {
                _notifyService.Error("Phiên đăng nhập đã hết hạn");
                return RedirectToAction("AdminLogin", "AdminLogin");
            }

            var pageNumber = page;
            var pageSize = 8;
            List<Product> lsProducts = CatID != 0
                ? _context.Products.AsNoTracking()
                    .Where(x => x.CatId == CatID)
                    .Include(x => x.Cat)
                    .Include(x => x.Supplier)
                    .Include(x => x.ProductDetails).ThenInclude(pd => pd.Size)
                    .Include(x => x.ProductDetails).ThenInclude(pd => pd.Color)
                    .OrderByDescending(x => x.ProductId)
                    .ToList()
                : _context.Products.AsNoTracking()
                    .Include(x => x.Cat)
                    .Include(x => x.Supplier)
                    .Include(x => x.ProductDetails).ThenInclude(pd => pd.Size)
                    .Include(x => x.ProductDetails).ThenInclude(pd => pd.Color)
                    .OrderByDescending(x => x.ProductId)
                    .ToList();

            var models = new PagedList<Product>(lsProducts.AsQueryable(), pageNumber, pageSize);
            ViewBag.CurrentCateID = CatID;
            ViewBag.CurrentPage = pageNumber;

            var categories = _context.Categories?.ToList() ?? new List<Category>();
            var suppliers = _context.Suppliers?.Select(s => new SelectListItem
            {
                Value = s.SupplierId.ToString(),
                Text = s.Name
            }).ToList() ?? new List<SelectListItem>();

            ViewData["DanhMuc"] = new SelectList(categories, "CatId", "CatName", CatID);
            ViewData["Supplier"] = new SelectList(suppliers, "Value", "Text");
            return View(models);
        }

        public IActionResult Filtter(int CatID = 0)
        {
            var url = CatID == 0 ? "/Admin/AdminProducts" : $"/Admin/AdminProducts?CatID={CatID}";
            return Json(new { status = "success", redirectUrl = url });
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Cat)
                .Include(p => p.Supplier)
                .Include(p => p.ProductDetails).ThenInclude(pd => pd.Size)
                .Include(p => p.ProductDetails).ThenInclude(pd => pd.Color)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            if (product == null) return NotFound();

            return View(product);
        }

        public IActionResult Create()
        {
            var sizes = _context.Sizes?.Select(s => new SelectListItem { Value = s.SizeId.ToString(), Text = s.SizeName }).ToList() ?? new List<SelectListItem>();
            var colors = _context.Colors?.Select(c => new SelectListItem { Value = c.ColorId.ToString(), Text = c.ColorName }).ToList() ?? new List<SelectListItem>();
            var suppliers = _context.Suppliers?.Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.Name }).ToList() ?? new List<SelectListItem>();
            var categories = _context.Categories?.ToList() ?? new List<Category>();

            Console.WriteLine($"Debug - Create View: Sizes count = {sizes.Count}, Colors count = {colors.Count}, Suppliers count = {suppliers.Count}, Categories count = {categories.Count}");
            if (sizes.Count == 0) _notifyService.Warning("Không có dữ liệu kích thước. Vui lòng kiểm tra bảng Sizes.");
            if (colors.Count == 0) _notifyService.Warning("Không có dữ liệu màu sắc. Vui lòng kiểm tra bảng Colors.");
            if (suppliers.Count == 0 || categories.Count == 0) _notifyService.Warning("Không có dữ liệu nhà cung cấp hoặc danh mục. Vui lòng kiểm tra bảng Suppliers hoặc Categories.");

            ViewBag.DanhMuc = new SelectList(categories, "CatId", "CatName");
            ViewBag.Supplier = new SelectList(suppliers, "Value", "Text");
            ViewBag.Sizes = new SelectList(sizes, "Value", "Text");
            ViewBag.Colors = new SelectList(colors, "Value", "Text");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductName,ShortDesc,Description,CatId,Price,Discount,UnitsInStock,Title,MetaDesc,MetaKey,Tags,Chatlieu,Songan,SupplierId,Active,BestSellers,SizeIds,ColorIds,DefaultStock")] Product product, IFormFile fThumb)
        {
            Console.WriteLine($"Received data: ProductName={product.ProductName}, SupplierId={product.SupplierId}, CatId={product.CatId}, BestSellers={product.BestSellers}, SizeIds={string.Join(",", product.SizeIds ?? new int[0])}, ColorIds={string.Join(",", product.ColorIds ?? new int[0])}, DefaultStock={product.DefaultStock}");

            try
            {
                product.ProductName = Utilities.ToTitleCase(product.ProductName);
                product.Alias = Utilities.SEOUrl(product.ProductName);
                product.DateCreated = DateTime.Now;
                product.DateModified = DateTime.Now;
                product.Active = true;
                product.BestSellers = product.BestSellers;

                if (fThumb != null)
                {
                    if (fThumb.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("fThumb", "Kích thước ảnh không được vượt quá 5MB.");
                        _notifyService.Error("Kích thước ảnh không được vượt quá 5MB.");
                    }
                    else if (!fThumb.ContentType.StartsWith("image/"))
                    {
                        ModelState.AddModelError("fThumb", "Vui lòng chọn file ảnh hợp lệ.");
                        _notifyService.Error("Vui lòng chọn file ảnh hợp lệ.");
                    }
                    else
                    {
                        string extension = Path.GetExtension(fThumb.FileName);
                        string imageName = Utilities.SEOUrl(product.ProductName) + extension;
                        string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/products");
                        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
                        string imagePath = Path.Combine(directoryPath, imageName.ToLower());
                        using (var stream = new FileStream(imagePath, FileMode.Create))
                        {
                            await fThumb.CopyToAsync(stream);
                        }
                        product.Thumb = "/products/" + imageName.ToLower();
                    }
                }
                if (string.IsNullOrEmpty(product.Thumb)) product.Thumb = "/products/default.jpg";

                if (product.SupplierId == null || !_context.Suppliers.Any(s => s.SupplierId == product.SupplierId))
                {
                    ModelState.AddModelError("SupplierId", "Vui lòng chọn nhà cung cấp hợp lệ.");
                    _notifyService.Error("Vui lòng chọn nhà cung cấp hợp lệ.");
                }
                if (product.CatId == null || !_context.Categories.Any(c => c.CatId == product.CatId))
                {
                    ModelState.AddModelError("CatId", "Vui lòng chọn danh mục hợp lệ.");
                    _notifyService.Error("Vui lòng chọn danh mục hợp lệ.");
                }

                int totalStock = 0;
                if (product.SizeIds != null && product.ColorIds != null && product.SizeIds.Any() && product.ColorIds.Any())
                {
                    if (product.DefaultStock < 0)
                    {
                        ModelState.AddModelError("DefaultStock", "Số lượng tồn kho không được nhỏ hơn 0.");
                        _notifyService.Error("Số lượng tồn kho không được nhỏ hơn 0.");
                    }
                    else
                    {
                        int variantCount = product.SizeIds.Length * product.ColorIds.Length;
                        totalStock = variantCount * (product.DefaultStock ?? 0);
                        Console.WriteLine($"Calculated total stock: {totalStock} (Variants: {variantCount} x DefaultStock: {product.DefaultStock ?? 0})");
                    }
                }
                product.UnitsInStock = totalStock;

                if (ModelState.IsValid)
                {
                    _context.Add(product);
                    await _context.SaveChangesAsync();

                    if (product.SizeIds != null && product.ColorIds != null && product.SizeIds.Any() && product.ColorIds.Any())
                    {
                        foreach (var sizeId in product.SizeIds)
                        {
                            foreach (var colorId in product.ColorIds)
                            {
                                var productDetail = new ProductDetail
                                {
                                    ProductId = product.ProductId,
                                    SizeId = sizeId,
                                    ColorId = colorId,
                                    Stock = product.DefaultStock ?? 0,
                                    Active = (product.DefaultStock ?? 0) > 0
                                };
                                _context.ProductDetails.Add(productDetail);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    _notifyService.Success("Thêm sản phẩm thành công");
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            Console.WriteLine($"ModelState Error in {state.Key}: {error.ErrorMessage}");
                            _notifyService.Error($"Lỗi: {state.Key} - {error.ErrorMessage}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi thêm sản phẩm: {ex.Message}\nStackTrace: {ex.StackTrace}");
                _notifyService.Error($"Lỗi khi thêm sản phẩm: {ex.Message}");
            }

            var sizes = _context.Sizes?.Select(s => new SelectListItem { Value = s.SizeId.ToString(), Text = s.SizeName }).ToList() ?? new List<SelectListItem>();
            var colors = _context.Colors?.Select(c => new SelectListItem { Value = c.ColorId.ToString(), Text = c.ColorName }).ToList() ?? new List<SelectListItem>();
            var suppliers = _context.Suppliers?.Select(s => new SelectListItem { Value = s.SupplierId.ToString(), Text = s.Name }).ToList() ?? new List<SelectListItem>();
            var categories = _context.Categories?.ToList() ?? new List<Category>();

            ViewBag.DanhMuc = new SelectList(categories, "CatId", "CatName", product.CatId);
            ViewBag.Supplier = new SelectList(suppliers, "Value", "Text", product.SupplierId);
            ViewBag.Sizes = new SelectList(sizes, "Value", "Text");
            ViewBag.Colors = new SelectList(colors, "Value", "Text");
            return View(product);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || id <= 0)
            {
                _notifyService.Error("ID sản phẩm không hợp lệ.");
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Cat)
                .Include(p => p.Supplier)
                .Include(p => p.ProductDetails).ThenInclude(pd => pd.Size)
                .Include(p => p.ProductDetails).ThenInclude(pd => pd.Color)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            if (product == null)
            {
                _notifyService.Error("Sản phẩm không tồn tại.");
                return NotFound();
            }

            Console.WriteLine($"Edit GET: ProductId={id}, ProductName={product.ProductName}, BestSellers={product.BestSellers}, ProductDetails count={(product.ProductDetails != null ? product.ProductDetails.Count : 0)}");
            foreach (var detail in product.ProductDetails ?? new List<ProductDetail>())
            {
                Console.WriteLine($"ProductDetail: Id={detail.ProductDetailId}, ColorId={detail.ColorId}, SizeId={detail.SizeId}, Stock={detail.Stock}, Active={detail.Active}");
            }

            var sizes = _context.Sizes?.Select(s => new SelectListItem
            {
                Value = s.SizeId.ToString(),
                Text = string.IsNullOrEmpty(s.SizeName) ? "" : s.SizeName
            }).ToList() ?? new List<SelectListItem>();
            var colors = _context.Colors?.Select(c => new SelectListItem
            {
                Value = c.ColorId.ToString(),
                Text = string.IsNullOrEmpty(c.ColorName) ? "" : c.ColorName
            }).ToList() ?? new List<SelectListItem>();
            var suppliers = _context.Suppliers?.Select(s => new SelectListItem
            {
                Value = s.SupplierId.ToString(),
                Text = string.IsNullOrEmpty(s.Name) ? "" : s.Name
            }).ToList() ?? new List<SelectListItem>();
            var categories = _context.Categories?.ToList() ?? new List<Category>();

            Console.WriteLine($"Số lượng Sizes: {sizes.Count}, Colors: {colors.Count}, Suppliers: {suppliers.Count}, Categories: {categories.Count}");
            if (sizes.Count == 0 || colors.Count == 0 || suppliers.Count == 0 || categories.Count == 0)
            {
                _notifyService.Warning("Dữ liệu kích thước, màu sắc, nhà cung cấp hoặc danh mục trống. Vui lòng kiểm tra database.");
            }

            var colorOptions = colors.Select(c => new { id = c.Value, name = c.Text }).ToList();
            var sizeOptions = sizes.Select(s => new { id = s.Value, name = s.Text }).ToList();
            ViewBag.ColorOptionsJson = JsonConvert.SerializeObject(colorOptions, Formatting.None, new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeHtml
            });
            ViewBag.SizeOptionsJson = JsonConvert.SerializeObject(sizeOptions, Formatting.None, new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeHtml
            });

            product.SizeIds = product.ProductDetails?.Select(pd => pd.SizeId).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToArray() ?? new int[] { };
            product.ColorIds = product.ProductDetails?.Select(pd => pd.ColorId).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToArray() ?? new int[] { };

            ViewBag.DanhMuc = new SelectList(categories, "CatId", "CatName", product.CatId);
            ViewBag.Supplier = new SelectList(suppliers, "Value", "Text", product.SupplierId);
            ViewBag.Sizes = new SelectList(sizes, "Value", "Text");
            ViewBag.Colors = new SelectList(colors, "Value", "Text");

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductId,ProductName,ShortDesc,Description,CatId,Price,Discount,Thumb,Title,MetaDesc,MetaKey,Tags,DateCreated,Active,BestSellers,Chatlieu,Songan,SupplierId,SizeIds,ColorIds,DefaultStock")] Product model, IFormFile fThumb)
        {
            if (id != model.ProductId)
            {
                Console.WriteLine($"Error: ID mismatch. Route id={id}, Model ProductId={model.ProductId}");
                _notifyService.Error("ID sản phẩm không khớp.");
                return BadRequest();
            }

            try
            {
                var existingProduct = await _context.Products
                    .Include(p => p.ProductDetails)
                    .FirstOrDefaultAsync(m => m.ProductId == id);
                if (existingProduct == null)
                {
                    Console.WriteLine($"Error: Product with ID {id} not found in database.");
                    _notifyService.Error("Sản phẩm không tồn tại.");
                    return NotFound();
                }

                Console.WriteLine($"Edit POST: id={id}, ProductId={model.ProductId}, ProductName={model.ProductName}, BestSellers={model.BestSellers}");

                // Validate SupplierId and CatId
                if (model.SupplierId == null || !_context.Suppliers.Any(s => s.SupplierId == model.SupplierId))
                {
                    ModelState.AddModelError("SupplierId", "Vui lòng chọn nhà cung cấp hợp lệ.");
                    _notifyService.Error("Vui lòng chọn nhà cung cấp hợp lệ.");
                }
                if (model.CatId == null || !_context.Categories.Any(c => c.CatId == model.CatId))
                {
                    ModelState.AddModelError("CatId", "Vui lòng chọn danh mục hợp lệ.");
                    _notifyService.Error("Vui lòng chọn danh mục hợp lệ.");
                }

                // Update product fields
                existingProduct.ProductName = !string.IsNullOrEmpty(model.ProductName) ? Utilities.ToTitleCase(model.ProductName) : existingProduct.ProductName;
                existingProduct.Alias = !string.IsNullOrEmpty(model.ProductName) ? Utilities.SEOUrl(model.ProductName) : existingProduct.Alias;
                existingProduct.ShortDesc = !string.IsNullOrEmpty(model.ShortDesc) ? model.ShortDesc : existingProduct.ShortDesc;
                existingProduct.Description = !string.IsNullOrEmpty(model.Description) ? model.Description : existingProduct.Description;
                existingProduct.CatId = model.CatId ?? existingProduct.CatId;
                existingProduct.Price = model.Price ?? existingProduct.Price;
                existingProduct.Discount = model.Discount ?? existingProduct.Discount;
                existingProduct.Title = !string.IsNullOrEmpty(model.Title) ? model.Title : existingProduct.Title;
                existingProduct.MetaDesc = !string.IsNullOrEmpty(model.MetaDesc) ? model.MetaDesc : existingProduct.MetaDesc;
                existingProduct.MetaKey = !string.IsNullOrEmpty(model.MetaKey) ? model.MetaKey : existingProduct.MetaKey;
                existingProduct.Tags = !string.IsNullOrEmpty(model.Tags) ? model.Tags : existingProduct.Tags;
                existingProduct.Chatlieu = !string.IsNullOrEmpty(model.Chatlieu) ? model.Chatlieu : existingProduct.Chatlieu;
                existingProduct.Songan = !string.IsNullOrEmpty(model.Songan) ? model.Songan : existingProduct.Songan;
                existingProduct.SupplierId = model.SupplierId ?? existingProduct.SupplierId;
                existingProduct.Active = model.Active;
                existingProduct.BestSellers = model.BestSellers;
                existingProduct.DateModified = DateTime.Now;

                // Handle image upload
                if (fThumb != null)
                {
                    if (fThumb.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("fThumb", "Kích thước ảnh không được vượt quá 5MB.");
                        _notifyService.Error("Kích thước ảnh không được vượt quá 5MB.");
                    }
                    else if (!fThumb.ContentType.StartsWith("image/"))
                    {
                        ModelState.AddModelError("fThumb", "Vui lòng chọn file ảnh hợp lệ.");
                        _notifyService.Error("Vui lòng chọn file ảnh hợp lệ.");
                    }
                    else
                    {
                        string extension = Path.GetExtension(fThumb.FileName);
                        string imageName = Utilities.SEOUrl(existingProduct.ProductName) + extension;
                        string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/products");
                        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
                        string imagePath = Path.Combine(directoryPath, imageName.ToLower());
                        using (var stream = new FileStream(imagePath, FileMode.Create))
                        {
                            await fThumb.CopyToAsync(stream);
                        }
                        existingProduct.Thumb = "/products/" + imageName.ToLower();
                    }
                }
                if (string.IsNullOrEmpty(existingProduct.Thumb))
                    existingProduct.Thumb = "/products/default.jpg";

                // Handle product variants
                if (model.SizeIds != null && model.ColorIds != null && model.SizeIds.Any() && model.ColorIds.Any())
                {
                    if (model.DefaultStock < 0)
                    {
                        ModelState.AddModelError("DefaultStock", "Số lượng tồn kho không được nhỏ hơn 0.");
                        _notifyService.Error("Số lượng tồn kho không được nhỏ hơn 0.");
                    }
                    else
                    {
                        // Validate SizeIds and ColorIds
                        var validSizes = await _context.Sizes.Where(s => model.SizeIds.Contains(s.SizeId)).Select(s => s.SizeId).ToListAsync();
                        var validColors = await _context.Colors.Where(c => model.ColorIds.Contains(c.ColorId)).Select(c => c.ColorId).ToListAsync();
                        if (validSizes.Count != model.SizeIds.Length || validColors.Count != model.ColorIds.Length)
                        {
                            ModelState.AddModelError("", "Một số kích thước hoặc màu sắc không hợp lệ.");
                            _notifyService.Error("Một số kích thước hoặc màu sắc không hợp lệ.");
                        }
                        else
                        {
                            // Remove existing product details
                            _context.ProductDetails.RemoveRange(existingProduct.ProductDetails);
                            await _context.SaveChangesAsync();

                            // Add new product details
                            foreach (var sizeId in model.SizeIds)
                            {
                                foreach (var colorId in model.ColorIds)
                                {
                                    var productDetail = new ProductDetail
                                    {
                                        ProductId = existingProduct.ProductId,
                                        SizeId = sizeId,
                                        ColorId = colorId,
                                        Stock = model.DefaultStock ?? 0,
                                        Active = (model.DefaultStock ?? 0) > 0
                                    };
                                    _context.ProductDetails.Add(productDetail);
                                }
                            }
                        }
                    }
                }

                // Update total stock
                existingProduct.UnitsInStock = existingProduct.ProductDetails
                    ?.Where(d => d.Active && (d.Stock ?? 0) > 0)
                    .Sum(d => d.Stock ?? 0) ?? 0;

                if (!ModelState.IsValid)
                {
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            Console.WriteLine($"ModelState Error in {state.Key}: {error.ErrorMessage}");
                            _notifyService.Error($"Lỗi: {state.Key} - {error.ErrorMessage}");
                        }
                    }

                    // Reload dropdown data for the view
                    var sizes = _context.Sizes?.Select(s => new SelectListItem
                    {
                        Value = s.SizeId.ToString(),
                        Text = string.IsNullOrEmpty(s.SizeName) ? "" : s.SizeName
                    }).ToList() ?? new List<SelectListItem>();
                    var colors = _context.Colors?.Select(c => new SelectListItem
                    {
                        Value = c.ColorId.ToString(),
                        Text = string.IsNullOrEmpty(c.ColorName) ? "" : c.ColorName
                    }).ToList() ?? new List<SelectListItem>();
                    var suppliers = _context.Suppliers?.Select(s => new SelectListItem
                    {
                        Value = s.SupplierId.ToString(),
                        Text = string.IsNullOrEmpty(s.Name) ? "" : s.Name
                    }).ToList() ?? new List<SelectListItem>();
                    var categories = _context.Categories?.ToList() ?? new List<Category>();

                    ViewBag.ColorOptionsJson = JsonConvert.SerializeObject(colors.Select(c => new { id = c.Value, name = c.Text }).ToList());
                    ViewBag.SizeOptionsJson = JsonConvert.SerializeObject(sizes.Select(s => new { id = s.Value, name = s.Text }).ToList());
                    ViewBag.DanhMuc = new SelectList(categories, "CatId", "CatName", model.CatId);
                    ViewBag.Supplier = new SelectList(suppliers, "Value", "Text", model.SupplierId);
                    ViewBag.Sizes = new SelectList(sizes, "Value", "Text");
                    ViewBag.Colors = new SelectList(colors, "Value", "Text");
                    return View(model);
                }

                await _context.SaveChangesAsync();
                _notifyService.Success("Cập nhật sản phẩm thành công");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi cập nhật sản phẩm: {ex.Message}\nStackTrace: {ex.StackTrace}");
                _notifyService.Error($"Lỗi khi cập nhật sản phẩm: {ex.Message}");

                // Reload dropdown data for the view
                var sizes = _context.Sizes?.Select(s => new SelectListItem
                {
                    Value = s.SizeId.ToString(),
                    Text = string.IsNullOrEmpty(s.SizeName) ? "" : s.SizeName
                }).ToList() ?? new List<SelectListItem>();
                var colors = _context.Colors?.Select(c => new SelectListItem
                {
                    Value = c.ColorId.ToString(),
                    Text = string.IsNullOrEmpty(c.ColorName) ? "" : c.ColorName
                }).ToList() ?? new List<SelectListItem>();
                var suppliers = _context.Suppliers?.Select(s => new SelectListItem
                {
                    Value = s.SupplierId.ToString(),
                    Text = string.IsNullOrEmpty(s.Name) ? "" : s.Name
                }).ToList() ?? new List<SelectListItem>();
                var categories = _context.Categories?.ToList() ?? new List<Category>();

                ViewBag.ColorOptionsJson = JsonConvert.SerializeObject(colors.Select(c => new { id = c.Value, name = c.Text }).ToList());
                ViewBag.SizeOptionsJson = JsonConvert.SerializeObject(sizes.Select(s => new { id = s.Value, name = s.Text }).ToList());
                ViewBag.DanhMuc = new SelectList(categories, "CatId", "CatName", model.CatId);
                ViewBag.Supplier = new SelectList(suppliers, "Value", "Text", model.SupplierId);
                ViewBag.Sizes = new SelectList(sizes, "Value", "Text");
                ViewBag.Colors = new SelectList(colors, "Value", "Text");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVariants(int productId, int[] SizeIds, int[] ColorIds, int? DefaultStock)
        {
            Console.WriteLine($"AddVariants POST: productId={productId}, SizeIds={string.Join(",", SizeIds ?? new int[0])}, ColorIds={string.Join(",", ColorIds ?? new int[0])}, DefaultStock={DefaultStock}");

            try
            {
                if (productId <= 0)
                {
                    Console.WriteLine("Lỗi: productId không hợp lệ.");
                    return Json(new { success = false, message = "ID sản phẩm không hợp lệ." });
                }
                if (SizeIds == null || ColorIds == null || !SizeIds.Any() || !ColorIds.Any())
                {
                    Console.WriteLine("Lỗi: SizeIds hoặc ColorIds không hợp lệ.");
                    return Json(new { success = false, message = "Vui lòng chọn ít nhất một kích thước và một màu sắc." });
                }
                if (DefaultStock == null || DefaultStock < 0)
                {
                    Console.WriteLine("Lỗi: DefaultStock không hợp lệ hoặc nhỏ hơn 0.");
                    return Json(new { success = false, message = "Số lượng tồn kho không hợp lệ hoặc nhỏ hơn 0." });
                }

                var product = await _context.Products
                    .Include(p => p.ProductDetails)
                    .ThenInclude(pd => pd.Size)
                    .Include(p => p.ProductDetails)
                    .ThenInclude(pd => pd.Color)
                    .FirstOrDefaultAsync(p => p.ProductId == productId);
                if (product == null)
                {
                    Console.WriteLine("Lỗi: Sản phẩm không tồn tại.");
                    return Json(new { success = false, message = "Sản phẩm không tồn tại." });
                }

                var validSizes = await _context.Sizes.Where(s => SizeIds.Contains(s.SizeId)).Select(s => s.SizeId).ToListAsync();
                var validColors = await _context.Colors.Where(c => ColorIds.Contains(c.ColorId)).Select(c => c.ColorId).ToListAsync();
                if (validSizes.Count != SizeIds.Length || validColors.Count != ColorIds.Length)
                {
                    Console.WriteLine($"Lỗi: Một số SizeIds hoặc ColorIds không tồn tại. Valid Sizes: {string.Join(",", validSizes)}, Valid Colors: {string.Join(",", validColors)}");
                    return Json(new { success = false, message = "Một số kích thước hoặc màu sắc không hợp lệ." });
                }

                var initialVariantCount = product.ProductDetails?.Count ?? 0;
                Console.WriteLine($"Số biến thể trước khi thêm: {initialVariantCount}");

                var updatedDetails = new List<ProductDetail>();
                var newVariantsCount = 0;

                foreach (var sizeId in SizeIds)
                {
                    foreach (var colorId in ColorIds)
                    {
                        var existingDetail = product.ProductDetails
                            ?.FirstOrDefault(d => d.SizeId == sizeId && d.ColorId == colorId);
                        if (existingDetail != null)
                        {
                            Console.WriteLine($"Cập nhật biến thể: SizeId={sizeId}, ColorId={colorId}, Old Stock={existingDetail.Stock}, New Stock={(existingDetail.Stock ?? 0) + (DefaultStock ?? 0)}");
                            existingDetail.Stock = (existingDetail.Stock ?? 0) + (DefaultStock ?? 0);
                            existingDetail.Active = (existingDetail.Stock ?? 0) > 0;
                            updatedDetails.Add(existingDetail);
                        }
                        else
                        {
                            Console.WriteLine($"Thêm biến thể mới: SizeId={sizeId}, ColorId={colorId}, Stock={DefaultStock}");
                            var newDetail = new ProductDetail
                            {
                                ProductId = product.ProductId,
                                SizeId = sizeId,
                                ColorId = colorId,
                                Stock = DefaultStock ?? 0,
                                Active = (DefaultStock ?? 0) > 0
                            };
                            product.ProductDetails.Add(newDetail);
                            updatedDetails.Add(newDetail);
                            newVariantsCount++;
                        }
                    }
                }

                product.UnitsInStock = product.ProductDetails
                    ?.Where(d => d.Active && (d.Stock ?? 0) > 0)
                    .Sum(d => d.Stock ?? 0) ?? 0;

                await _context.SaveChangesAsync();

                var finalVariantCount = product.ProductDetails?.Count ?? 0;
                Console.WriteLine($"Số biến thể sau khi thêm: {finalVariantCount}, Số biến thể mới thêm: {newVariantsCount}");

                var variantRows = "";
                if (product.ProductDetails != null && product.ProductDetails.Any())
                {
                    for (int i = 0; i < product.ProductDetails.Count; i++)
                    {
                        var detail = product.ProductDetails.ElementAt(i);
                        variantRows += $@"<tr>
                            <td>{(detail.Color?.ColorName ?? "Không xác định")}<input type=""hidden"" name=""ProductDetails[{i}].ColorId"" value=""{detail.ColorId}"" /><input type=""hidden"" name=""ProductDetails[{i}].ProductDetailId"" value=""{detail.ProductDetailId}"" /></td>
                            <td>{(detail.Size?.SizeName ?? "Không xác định")}<input type=""hidden"" name=""ProductDetails[{i}].SizeId"" value=""{detail.SizeId}"" /></td>
                            <td><input type=""number"" name=""ProductDetails[{i}].Stock"" class=""form-control stock-input"" min=""0"" value=""{detail.Stock}"" /></td>
                            <td><input type=""checkbox"" name=""ProductDetails[{i}].Active"" {(detail.Active ? "checked" : "")} /></td>
                            <td><button type=""button"" class=""btn btn-primary btn-sm update-variant"" data-product-detail-id=""{detail.ProductDetailId}"" data-product-id=""{product.ProductId}"">Cập nhật tổng</button></td>
                        </tr>";
                    }
                }
                else
                {
                    variantRows = "<tr><td colspan=\"5\">Không có biến thể nào.</td></tr>";
                }

                _notifyService.Success($"Thêm {newVariantsCount} biến thể thành công.");
                return Json(new
                {
                    success = true,
                    variantRows = variantRows,
                    newVariantsCount = newVariantsCount,
                    totalVariants = finalVariantCount,
                    totalStock = product.UnitsInStock
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi thêm biến thể: {ex.Message}\nStackTrace: {ex.StackTrace}");
                _notifyService.Error($"Lỗi khi thêm biến thể: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(int productDetailId, int stock)
        {
            try
            {
                if (stock < 0)
                {
                    return Json(new { success = false, message = "Số lượng tồn kho không được nhỏ hơn 0." });
                }

                var productDetail = await _context.ProductDetails.FindAsync(productDetailId);
                if (productDetail == null)
                {
                    return Json(new { success = false, message = "Biến thể không tồn tại." });
                }

                productDetail.Stock = stock;
                productDetail.Active = stock > 0;
                await _context.SaveChangesAsync();

                var product = await _context.Products
                    .Include(p => p.ProductDetails)
                    .FirstOrDefaultAsync(p => p.ProductId == productDetail.ProductId);
                if (product != null)
                {
                    product.UnitsInStock = product.ProductDetails
                        ?.Where(d => d.Active && (d.Stock ?? 0) > 0)
                        .Sum(d => d.Stock ?? 0) ?? 0;
                    await _context.SaveChangesAsync();
                }

                _notifyService.Success("Cập nhật số lượng tồn kho thành công.");
                return Json(new { success = true, newStock = productDetail.Stock, totalStock = product?.UnitsInStock ?? 0 });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi cập nhật số lượng: {ex.Message}\nStackTrace: {ex.StackTrace}");
                _notifyService.Error($"Lỗi khi cập nhật số lượng: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult Delete(int? id)
        {
            if (id == null)
            {
                Console.WriteLine("Lỗi: ID sản phẩm không hợp lệ.");
                _notifyService.Error("ID sản phẩm không hợp lệ.");
                return NotFound();
            }
            var product = _context.Products
                .Include(p => p.Cat)
                .Include(p => p.ProductDetails)
                .Include(p => p.PromotionProducts)
                .Include(p => p.Comments)
                .FirstOrDefault(m => m.ProductId == id);
            if (product == null)
            {
                Console.WriteLine($"Lỗi: Sản phẩm ID={id} không tồn tại.");
                _notifyService.Error("Sản phẩm không tồn tại.");
                return NotFound();
            }

            Console.WriteLine($"Delete GET: Sản phẩm ID={id}, Tên={product.ProductName}, Biến thể={product.ProductDetails?.Count ?? 0}, Khuyến mãi={product.PromotionProducts?.Count ?? 0}, Bình luận={product.Comments?.Count ?? 0}");

            // Truyền thông tin ràng buộc vào ViewBag
            var orderDetails = _context.OrderDetails
                .Include(od => od.Order)
                .Where(od => od.ProductDetail.ProductId == id)
                .ToList();
            var pendingOrders = orderDetails
                .Select(od => od.Order)
                .Where(o => o.TransactStatusId != 4 && o.TransactStatusId != 5 && !o.Deleted) // Loại trừ "Đã giao" và "Hủy"
                .Distinct()
                .ToList();
            ViewBag.PendingOrderCount = pendingOrders.Count;
            ViewBag.PendingOrderIds = string.Join(", ", pendingOrders.Select(o => o.OrderId));
            ViewBag.PromotionCount = product.PromotionProducts?.Count ?? 0;
            ViewBag.CommentCount = product.Comments?.Count ?? 0;
            ViewBag.VariantCount = product.ProductDetails?.Count ?? 0;
            ViewBag.CanDelete = pendingOrders.Count == 0; // Cho phép xóa nếu không có đơn hàng chưa thanh toán

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                Console.WriteLine($"Bắt đầu xóa sản phẩm ID={id}");

                var product = await _context.Products
                    .Include(p => p.ProductDetails)
                    .Include(p => p.PromotionProducts)
                    .Include(p => p.Comments)
                    .FirstOrDefaultAsync(m => m.ProductId == id);
                if (product == null)
                {
                    Console.WriteLine($"Lỗi: Sản phẩm ID={id} không tồn tại.");
                    _notifyService.Error("Sản phẩm không tồn tại.");
                    return NotFound();
                }

                Console.WriteLine($"Sản phẩm: {product.ProductName}, Số biến thể: {product.ProductDetails?.Count ?? 0}, Số khuyến mãi: {product.PromotionProducts?.Count ?? 0}, Số bình luận: {product.Comments?.Count ?? 0}");

                // Kiểm tra OrderDetail
                var orderDetails = await _context.OrderDetails
                    .Include(od => od.Order)
                    .Where(od => od.ProductDetail.ProductId == id)
                    .ToListAsync();

                if (orderDetails.Any())
                {
                    var pendingOrders = orderDetails
                        .Select(od => od.Order)
                        .Where(o => o.TransactStatusId != 4 && o.TransactStatusId != 5 && !o.Deleted)
                        .Distinct()
                        .ToList();

                    if (pendingOrders.Any())
                    {
                        var orderIds = string.Join(", ", pendingOrders.Select(o => o.OrderId));
                        Console.WriteLine($"Lỗi: Sản phẩm '{product.ProductName}' liên quan đến đơn hàng chưa hoàn tất: {orderIds}");
                        _notifyService.Error($"Không thể xóa sản phẩm '{product.ProductName}' vì nó liên quan đến các đơn hàng chưa hoàn tất: {orderIds}.");
                        return RedirectToAction(nameof(Index));
                    }
                }

                // Kiểm tra PromotionProducts
                if (product.PromotionProducts.Any())
                {
                    var promotionIds = string.Join(", ", product.PromotionProducts.Select(pp => pp.PromotionId));
                    Console.WriteLine($"Lỗi: Sản phẩm '{product.ProductName}' thuộc các chương trình khuyến mãi: {promotionIds}");
                    _notifyService.Error($"Không thể xóa sản phẩm '{product.ProductName}' vì nó thuộc các chương trình khuyến mãi: {promotionIds}.");
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra Comments
                if (product.Comments.Any())
                {
                    Console.WriteLine($"Cảnh báo: Sản phẩm '{product.ProductName}' có {product.Comments.Count} bình luận, sẽ xóa tất cả.");
                    _notifyService.Warning($"Sản phẩm '{product.ProductName}' có {product.Comments.Count} bình luận, tất cả sẽ bị xóa.");
                    _context.Comments.RemoveRange(product.Comments);
                }

                // Xóa ProductDetails và Product
                Console.WriteLine($"Xóa {product.ProductDetails?.Count ?? 0} biến thể của sản phẩm '{product.ProductName}'");
                _context.ProductDetails.RemoveRange(product.ProductDetails);
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Xóa sản phẩm '{product.ProductName}' thành công");
                _notifyService.Success($"Xóa sản phẩm '{product.ProductName}' và {product.ProductDetails?.Count ?? 0} biến thể thành công");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xóa sản phẩm ID={id}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                _notifyService.Error($"Lỗi khi xóa sản phẩm: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
        }

        [HttpPost]
        public IActionResult FindProduct(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return Json(new { success = false, message = "Vui lòng nhập từ khóa tìm kiếm." });
            }

            var accountId = HttpContext.Session.GetString("AccountId");
            if (string.IsNullOrEmpty(accountId))
            {
                _notifyService.Error("Phiên đăng nhập đã hết hạn");
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
            }

            var products = _context.Products.AsNoTracking()
                .Include(x => x.Cat)
                .Include(x => x.Supplier)
                .Include(x => x.ProductDetails).ThenInclude(pd => pd.Size)
                .Include(x => x.ProductDetails).ThenInclude(pd => pd.Color)
                .Where(x => x.ProductName.Contains(keyword) || x.Cat.CatName.Contains(keyword))
                .OrderByDescending(x => x.ProductId)
                .ToList();

            if (!products.Any())
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm nào." });
            }

            var html = GenerateProductTableHtml(products);
            return Json(new { success = true, html = html });
        }

        private string GenerateProductTableHtml(List<Product> products)
        {
            int index = 1;
            var htmlBuilder = new System.Text.StringBuilder();
            foreach (var item in products)
            {
                htmlBuilder.AppendLine("<tr>");
                htmlBuilder.AppendLine($"<td>{index}</td>");
                htmlBuilder.AppendLine($"<td>{item.ProductName}</td>");
                htmlBuilder.AppendLine($"<td>{item.Cat?.CatName}</td>");
                htmlBuilder.AppendLine($"<td>{item.Price?.ToString("#,##0") ?? "0"} VND</td>");
                htmlBuilder.AppendLine($"<td>");
                if (item.ProductDetails != null && item.ProductDetails.Any())
                {
                    htmlBuilder.AppendLine($"<span>Tổng: {item.ProductDetails.Count()} biến thể</span>");
                    htmlBuilder.AppendLine($"<a asp-area=\"Admin\" asp-controller=\"AdminProducts\" asp-action=\"Details\" asp-route-id=\"{item.ProductId}\" class=\"btn btn-info btn-sm ml-2\">Xem chi tiết</a>");
                }
                else
                {
                    htmlBuilder.AppendLine("<span>Chưa có biến thể</span>");
                }
                htmlBuilder.AppendLine("</td>");
                htmlBuilder.AppendLine($"<td>{(item.ProductDetails?.Sum(pd => pd.Stock ?? 0) ?? 0)}</td>");
                htmlBuilder.AppendLine("<td>");
                if (item.Active ?? false)
                {
                    htmlBuilder.AppendLine("<div class=\"d-flex align-items-center\">");
                    htmlBuilder.AppendLine("<div class=\"badge badge-success badge-dot m-r-10\"></div>");
                    htmlBuilder.AppendLine("<div>Đang Hoạt Động</div>");
                    htmlBuilder.AppendLine("</div>");
                }
                else
                {
                    htmlBuilder.AppendLine("<div class=\"d-flex align-items-center\">");
                    htmlBuilder.AppendLine("<div class=\"badge badge-danger badge-dot m-r-10></ div> ");
                    htmlBuilder.AppendLine("<div>Không Hoạt Động</div>");
                    htmlBuilder.AppendLine("</div>");
                }
                htmlBuilder.AppendLine("</td>");
                htmlBuilder.AppendLine("<td>");
                htmlBuilder.AppendLine($"<a class=\"btn btn-primary btn-tone m-r-5\" asp-area=\"Admin\" asp-controller=\"AdminProducts\" asp-action=\"Details\" asp-route-id=\"{item.ProductId}\"><i class=\"far fa-eye m-r-5 fa-lg\"></i>Xem</a>");
                htmlBuilder.AppendLine($"<a class=\"btn btn-secondary btn-tone m-r-5\" asp-area=\"Admin\" asp-controller=\"AdminProducts\" asp-action=\"Edit\" asp-route-id=\"{item.ProductId}\"><i class=\"far fa-edit m-r-5 fa-lg\"></i>Sửa</a>");
                htmlBuilder.AppendLine($"<a class=\"btn btn-danger btn-tone m-r-5\" asp-area=\"Admin\" asp-controller=\"AdminProducts\" asp-action=\"Delete\" asp-route-id=\"{item.ProductId}\"><i class=\"far fa-trash-alt m-r-5 fa-lg\"></i>Xóa</a>");
                htmlBuilder.AppendLine("</td>");
                htmlBuilder.AppendLine("</tr>");
                index++;
            }
            return htmlBuilder.ToString();
        }
    }
}