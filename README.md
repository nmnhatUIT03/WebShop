# WebShop - Hệ thống Thương mại Điện tử Thời trang

## Giới thiệu

WebShop là ứng dụng thương mại điện tử chuyên về thời trang, được xây dựng trên nền tảng ASP.NET Core MVC. Hệ thống cung cấp đầy đủ các tính năng mua sắm trực tuyến, quản lý sản phẩm, đơn hàng, khuyến mãi và tích điểm khách hàng.

## Công nghệ sử dụng

### Backend
- **Framework**: ASP.NET Core MVC 6.0
- **Language**: C# 12.0
- **ORM**: Entity Framework Core 6.0.17
- **Database**: SQL Server
- **Authentication**: Cookie-based Authentication (2 schemes: Customer & Admin)
- **Session Management**: Distributed Memory Cache

### Frontend
- **View Engine**: Razor Pages (.cshtml)
- **CSS Framework**: Bootstrap 4.5.2
- **JavaScript**: jQuery 3.6.0
- **Icons**: Font Awesome 5.15.4
- **UI Components**: Owl Carousel, Nice Select, Magnific Popup

### Packages chính
- `AspNetCoreHero.ToastNotification` (1.1.0) - Thông báo
- `BCrypt.Net-Next` (4.0.3) - Mã hóa mật khẩu
- `PagedList.Core.Mvc` (3.0.0) - Phân trang
- `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` (6.0.17) - Hot reload views

## Cấu trúc dự án

```
webthoitrang/
├── WebShop/                          # Main application
│   ├── Areas/
│   │   └── Admin/                    # Admin area
│   │       ├── Controllers/          # 21 admin controllers
│   │       ├── Models/               # 10 admin view models
│   │       └── Views/                # 89 admin views
│   ├── Controllers/                  # Public controllers
│   │   ├── HomeController.cs         # Trang chủ, danh mục
│   │   ├── ProductController.cs      # Sản phẩm
│   │   ├── ShoppingCartController.cs # Giỏ hàng (21 methods)
│   │   ├── CheckoutController.cs     # Thanh toán
│   │   ├── AccountsController.cs     # Đăng ký/đăng nhập
│   │   ├── BlogController.cs         # Tin tức
│   │   └── DonHangController.cs      # Đơn hàng
│   ├── Models/                       # Entity models
│   │   ├── Product.cs                # Sản phẩm
│   │   ├── ProductDetail.cs          # Chi tiết SP (Size, Color, Stock)
│   │   ├── Category.cs               # Danh mục
│   │   ├── Customer.cs               # Khách hàng
│   │   ├── Order.cs                  # Đơn hàng
│   │   ├── OrderDetail.cs            # Chi tiết đơn hàng
│   │   ├── Promotion.cs              # Khuyến mãi
│   │   ├── Voucher.cs                # Mã giảm giá
│   │   ├── CheckInHistory.cs         # Lịch sử điểm danh
│   │   └── webshopContext.cs         # DbContext
│   ├── ModelViews/                   # View models & DTOs
│   │   ├── CartItem.cs               # Item giỏ hàng
│   │   ├── HomeViewVM.cs             # Trang chủ
│   │   ├── ProductHomeVM.cs          # Sản phẩm trang chủ
│   │   ├── MuaHangVM.cs              # Checkout
│   │   └── ...
│   ├── Views/                        # Razor views
│   │   ├── Shared/
│   │   │   ├── _Layout.cshtml        # Master layout
│   │   │   ├── _HeaderPartialView.cshtml
│   │   │   └── _FooterPartialView.cshtml
│   │   ├── Home/                     # Trang chủ views
│   │   ├── Product/                  # Sản phẩm views
│   │   ├── Checkout/                 # Thanh toán views
│   │   └── Accounts/                 # Tài khoản views
│   ├── wwwroot/                      # Static files
│   │   ├── assets/                   # Frontend assets
│   │   ├── Adminassets/              # Admin assets
│   │   ├── images/                   # Product images
│   │   └── products/                 # Product photos
│   ├── Migrations/                   # EF migrations
│   ├── Extension/                    # Extension methods
│   ├── Helpper/                      # Utility classes
│   ├── Program.cs                    # Entry point
│   ├── Startup.cs                    # Configuration
│   ├── appsettings.json              # App settings
│   └── WebShop.csproj                # Project file
├── scriptthoitrang.sql               # Database script
├── CreatePromotionProductsTable.sql  # Promotion tables script
└── WebShop.sln                       # Solution file
```

## Tính năng chính

### Khách hàng
- Xem danh sách sản phẩm theo danh mục (10 danh mục)
- Tìm kiếm, lọc, sắp xếp sản phẩm
- Xem chi tiết sản phẩm (Size, Color, Stock)
- Thêm vào giỏ hàng (hỗ trợ cả khách vãng lai)
- Mua ngay (Buy Now)
- Đặt hàng và thanh toán
- Áp dụng mã giảm giá (Voucher)
- Đăng ký/đăng nhập
- Quản lý đơn hàng cá nhân
- Điểm danh hằng ngày (Check-in) để tích điểm
- Đọc tin tức/blog

### Admin
- Quản lý sản phẩm (CRUD)
- Quản lý danh mục
- Quản lý đơn hàng
- Quản lý khách hàng
- Quản lý khuyến mãi (Promotion)
- Quản lý voucher
- Quản lý tin tức
- Quản lý nhà cung cấp
- Thống kê báo cáo

### Giỏ hàng nâng cao
- Lưu trữ giỏ hàng bằng Cookie mã hóa (DataProtection)
- Hỗ trợ khách vãng lai (Session-based)
- Đồng bộ giỏ hàng khi đăng nhập
- Kiểm tra tồn kho real-time
- Tự động xóa sản phẩm không hợp lệ
- API RESTful cho giỏ hàng

## Yêu cầu hệ thống

### Phần mềm cần thiết
- **.NET 6.0 SDK** (hoặc cao hơn)
  - Download: https://dotnet.microsoft.com/download/dotnet/6.0
- **SQL Server 2019** (hoặc cao hơn)
  - SQL Server Express: https://www.microsoft.com/sql-server/sql-server-downloads
  - SQL Server Management Studio (SSMS): Khuyến nghị
- **Visual Studio 2022** (khuyến nghị) hoặc Visual Studio Code
  - Visual Studio 2022 Community: https://visualstudio.microsoft.com/
- **Git** (tùy chọn)

### Cấu hình tối thiểu
- RAM: 4GB (khuyến nghị 8GB)
- Disk: 2GB trống
- OS: Windows 10/11, macOS, hoặc Linux

## Hướng dẫn cài đặt

### Bước 1: Kiểm tra phiên bản .NET

Mở Terminal/Command Prompt và chạy:

```bash
dotnet --version
```

Kết quả phải là `6.0.x` hoặc cao hơn. Nếu chưa có, tải và cài đặt .NET 6.0 SDK.

### Bước 2: Clone hoặc tải source code

```bash
# Nếu dùng Git
git clone <repository-url>
cd webfinal

# Hoặc giải nén file zip vào thư mục webfinal
```

### Bước 3: Cấu hình Database

#### 3.1. Tạo Database từ script SQL

1. Mở SQL Server Management Studio (SSMS)
2. Kết nối đến SQL Server instance của bạn
3. Mở file `webthoitrang/scriptthoitrang.sql`
4. **Quan trọng**: Sửa đường dẫn database trong script (dòng 6-9):

```sql
-- Thay đổi đường dẫn này phù hợp với máy bạn
( NAME = N'webshop', FILENAME = N'D:\DATALTWNC\webshop.mdf' , ...)
```

5. Chạy script để tạo database `webshop`
6. Chạy thêm script `webthoitrang/CreatePromotionProductsTable.sql` để tạo bảng Promotion (tùy chọn)

#### 3.2. Cập nhật Connection String

Mở file `webthoitrang/WebShop/appsettings.json` và sửa connection string:

```json
{
  "ConnectionStrings": {
    "DataContext": "Server=TEN_SERVER_CUA_BAN;Database=webshop;Trusted_Connection=True;"
  }
}
```

Thay `TEN_SERVER_CUA_BAN` bằng tên SQL Server instance của bạn. Ví dụ:
- `localhost` hoặc `.` (local default instance)
- `localhost\SQLEXPRESS` (SQL Server Express)
- `DESKTOP-ABC123\SQLEXPRESS`

Để tìm tên server, chạy trong SSMS:
```sql
SELECT @@SERVERNAME
```

### Bước 4: Restore NuGet Packages

Mở Terminal tại thư mục `webthoitrang/WebShop`:

```bash
cd webthoitrang/WebShop
dotnet restore
```

### Bước 5: Build Project

```bash
dotnet build
```

Nếu có lỗi, kiểm tra:
- Đã cài .NET 6.0 SDK chưa
- Connection string đã đúng chưa
- Database đã tạo chưa

## Hướng dẫn chạy ứng dụng

### Cách 1: Chạy bằng .NET CLI

```bash
cd webthoitrang/WebShop
dotnet run
```

Ứng dụng sẽ chạy tại:
- HTTPS: `https://localhost:5001` hoặc `https://localhost:44391`
- HTTP: `http://localhost:5000`

### Cách 2: Chạy bằng Visual Studio

1. Mở file `webthoitrang/WebShop.sln` bằng Visual Studio 2022
2. Đợi Visual Studio restore packages tự động
3. Chọn project `WebShop` làm Startup Project (nếu chưa)
4. Nhấn `F5` hoặc click nút `Start` (IIS Express)

### Cách 3: Chạy bằng Visual Studio Code

1. Mở thư mục `webthoitrang/WebShop` trong VS Code
2. Cài extension `C# Dev Kit` nếu chưa có
3. Nhấn `F5` hoặc chọn `Run > Start Debugging`

## Hướng dẫn Debug

### Debug trong Visual Studio 2022

1. Đặt breakpoint bằng cách click vào lề trái của dòng code (hoặc nhấn `F9`)
2. Nhấn `F5` để chạy ở chế độ Debug
3. Khi chương trình dừng tại breakpoint:
   - `F10`: Step Over (chạy qua dòng hiện tại)
   - `F11`: Step Into (đi vào hàm)
   - `Shift+F11`: Step Out (thoát khỏi hàm)
   - `F5`: Continue (chạy tiếp)
4. Xem giá trị biến trong cửa sổ:
   - `Locals`: Biến cục bộ
   - `Watch`: Biến theo dõi
   - `Immediate`: Console debug

### Debug trong Visual Studio Code

1. Đặt breakpoint bằng cách click vào lề trái
2. Nhấn `F5` hoặc chọn `Run > Start Debugging`
3. Sử dụng Debug toolbar:
   - Continue (F5)
   - Step Over (F10)
   - Step Into (F11)
   - Step Out (Shift+F11)
   - Restart (Ctrl+Shift+F5)
   - Stop (Shift+F5)

### Debug với Hot Reload

Project đã cấu hình `Razor Runtime Compilation`, cho phép chỉnh sửa `.cshtml` mà không cần restart:

1. Chạy ứng dụng ở chế độ Debug
2. Sửa file `.cshtml`
3. Lưu file (Ctrl+S)
4. Refresh trình duyệt để thấy thay đổi

Lưu ý: Hot Reload không áp dụng cho file `.cs` (C# code-behind)

## Cấu trúc Database

### Bảng chính

- **Products**: Sản phẩm (ProductID, ProductName, Price, Discount, Active, BestSellers, ...)
- **ProductDetails**: Chi tiết sản phẩm (Size, Color, Stock)
- **Categories**: Danh mục (CatID: 1009-1035)
- **Customers**: Khách hàng (CustomerID, Email, Password, Points, CheckInCount, ...)
- **Orders**: Đơn hàng (OrderID, CustomerID, TotalMoney, TransactStatusID, ...)
- **OrderDetails**: Chi tiết đơn hàng
- **Accounts**: Tài khoản admin
- **Roles**: Vai trò
- **Promotions**: Khuyến mãi
- **PromotionProducts**: Sản phẩm khuyến mãi (cần tạo thủ công)
- **Vouchers**: Mã giảm giá
- **UserPromotions**: Lịch sử sử dụng khuyến mãi
- **CheckInHistory**: Lịch sử điểm danh
- **RewardHistory**: Lịch sử đổi quà
- **TinTucs**: Tin tức/Blog
- **Suppliers**: Nhà cung cấp
- **Colors**: Màu sắc
- **Sizes**: Kích thước
- **Locations**: Địa điểm (tỉnh/thành phố)
- **TransactStatus**: Trạng thái giao dịch

## Tài khoản mặc định

### Admin
Cần tạo thủ công trong database:

```sql
-- Tạo role admin
INSERT INTO Roles (RoleName, Description) VALUES (N'Admin', N'Quản trị viên');

-- Tạo tài khoản admin (password: admin123)
INSERT INTO Accounts (FullName, Email, Password, Salt, Active, RoleID, CreateDate)
VALUES (
    N'Administrator',
    'admin@webshop.com',
    '$2a$11$hashed_password_here', -- Cần hash bằng BCrypt
    'SALT12',
    1,
    1, -- RoleID của Admin
    GETDATE()
);
```

### Customer
Đăng ký tại: `/dang-ky.html`

## API Endpoints

### Shopping Cart API

- `GET /api/cart/count` - Lấy số lượng sản phẩm trong giỏ
- `POST /api/cart/add` - Thêm sản phẩm vào giỏ
- `POST /api/cart/update` - Cập nhật số lượng
- `POST /api/cart/remove` - Xóa sản phẩm
- `POST /api/cart/remove-multiple` - Xóa nhiều sản phẩm
- `POST /api/cart/buy-now` - Mua ngay
- `POST /api/cart/proceed-to-checkout` - Chuyển đến thanh toán
- `POST /api/cart/sync` - Đồng bộ giỏ hàng
- `POST /api/cart/sync-local` - Đồng bộ từ localStorage
- `GET /api/cart/get-discount` - Lấy thông tin giảm giá

### Account API

- `GET /api/account/check-login` - Kiểm tra đăng nhập

## Routes chính

### Public Routes

- `/` hoặc `/Home/Index` - Trang chủ
- `/shop.html?categoryId={id}` - Danh sách sản phẩm theo danh mục
- `/cart.html` - Giỏ hàng
- `/checkout.html` - Thanh toán
- `/dang-nhap.html` - Đăng nhập
- `/dang-ky.html` - Đăng ký
- `/tai-khoan-cua-toi.html` - Dashboard khách hàng
- `/dat-hang-thanh-cong.html` - Đặt hàng thành công
- `/Blog/Index` - Tin tức

### Admin Routes

- `/Admin/AdminLogin` - Đăng nhập admin
- `/Admin/AdminProducts` - Quản lý sản phẩm
- `/Admin/AdminCategories` - Quản lý danh mục
- `/Admin/AdminOrders` - Quản lý đơn hàng
- `/Admin/AdminCustomers` - Quản lý khách hàng
- `/Admin/AdminPromotions` - Quản lý khuyến mãi

## Troubleshooting

### Lỗi: "Invalid object name 'PromotionProducts'"

**Nguyên nhân**: Bảng PromotionProducts chưa được tạo trong database.

**Giải pháp**:
1. Chạy script `CreatePromotionProductsTable.sql` trong SSMS
2. Hoặc chạy migration: `dotnet ef database update`

### Lỗi: "SqlNullValueException: Data is Null"

**Nguyên nhân**: Dữ liệu trong database có giá trị NULL không khớp với model.

**Giải pháp**: Đã được fix trong code, đảm bảo các trường `BestSellers`, `Active`, `Published` là `bool?` (nullable)

### Lỗi: "A connection was successfully established with the server, but..."

**Nguyên nhân**: Connection string không đúng hoặc SQL Server không chạy.

**Giải pháp**:
1. Kiểm tra SQL Server đang chạy (SQL Server Configuration Manager)
2. Kiểm tra tên server trong connection string
3. Thử kết nối bằng SSMS trước

### Lỗi: "The certificate chain was issued by an authority that is not trusted"

**Giải pháp**: Thêm `TrustServerCertificate=True` vào connection string:

```json
"DataContext": "Server=.;Database=webshop;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Port đã được sử dụng

**Giải pháp**: Sửa port trong `Properties/launchSettings.json`:

```json
"applicationUrl": "https://localhost:5001;http://localhost:5000"
```

## Ghi chú quan trọng

1. **Bảo mật**: Đây là project demo, không nên deploy production mà không review lại security
2. **Connection String**: Không commit connection string thật vào Git
3. **Images**: Thư mục `wwwroot/images` và `wwwroot/products` cần quyền ghi
4. **Session**: Session timeout mặc định là 30 phút
5. **Cookie**: Cookie giỏ hàng có thời hạn 10 năm (có thể điều chỉnh)

## Tác giả

Project được phát triển cho mục đích học tập và demo.

## License

Chưa có license cụ thể.

