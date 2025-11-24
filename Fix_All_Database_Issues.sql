-- ✅ Script tổng hợp để fix tất cả vấn đề database
-- Chạy script này để sửa lỗi:
-- 1. Order.Deleted cannot be null
-- 2. Invalid column name 'ProductDetailID'

USE webshop;
GO

PRINT '========================================='
PRINT 'FIX ALL DATABASE ISSUES - BẮT ĐẦU'
PRINT '========================================='
PRINT ''
GO

-- ==========================================
-- PHẦN 1: FIX ORDERS TABLE (Deleted, Paid)
-- ==========================================
PRINT '>>> PHẦN 1: FIX ORDERS TABLE'
PRINT ''

IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Orders' 
    AND COLUMN_NAME = 'Deleted' 
    AND IS_NULLABLE = 'YES'
)
BEGIN
    PRINT '  ✓ Đang cập nhật column Deleted...'
    UPDATE Orders SET Deleted = 0 WHERE Deleted IS NULL;
    ALTER TABLE Orders ALTER COLUMN Deleted BIT NOT NULL;
    
    IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_Orders_Deleted')
    BEGIN
        ALTER TABLE Orders ADD CONSTRAINT DF_Orders_Deleted DEFAULT 0 FOR Deleted;
    END
    PRINT '  ✓ Hoàn thành column Deleted'
END
ELSE
BEGIN
    PRINT '  ✓ Column Deleted đã OK'
END
GO

IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Orders' 
    AND COLUMN_NAME = 'Paid' 
    AND IS_NULLABLE = 'YES'
)
BEGIN
    PRINT '  ✓ Đang cập nhật column Paid...'
    UPDATE Orders SET Paid = 0 WHERE Paid IS NULL;
    ALTER TABLE Orders ALTER COLUMN Paid BIT NOT NULL;
    
    IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_Orders_Paid')
    BEGIN
        ALTER TABLE Orders ADD CONSTRAINT DF_Orders_Paid DEFAULT 0 FOR Paid;
    END
    PRINT '  ✓ Hoàn thành column Paid'
END
ELSE
BEGIN
    PRINT '  ✓ Column Paid đã OK'
END
GO

PRINT ''
PRINT '>>> PHẦN 1: HOÀN THÀNH ✅'
PRINT ''

-- ==========================================
-- PHẦN 2: FIX ORDERDETAILS TABLE (ProductDetailID)
-- ==========================================
PRINT '>>> PHẦN 2: FIX ORDERDETAILS TABLE'
PRINT ''

-- Kiểm tra và đổi tên ProductID → ProductDetailID nếu cần
IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'OrderDetails' 
    AND COLUMN_NAME = 'ProductID'
)
AND NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'OrderDetails' 
    AND COLUMN_NAME = 'ProductDetailID'
)
BEGIN
    PRINT '  ✓ Tìm thấy column ProductID - cần đổi tên'
    
    -- Xóa foreign key constraint cũ nếu có
    DECLARE @OldFK NVARCHAR(200)
    SELECT @OldFK = fk.name
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns c ON fkc.parent_column_id = c.column_id AND fkc.parent_object_id = c.object_id
    WHERE fk.parent_object_id = OBJECT_ID('OrderDetails')
    AND c.name = 'ProductID'
    
    IF @OldFK IS NOT NULL
    BEGIN
        PRINT '  ✓ Đang xóa foreign key cũ: ' + @OldFK
        EXEC('ALTER TABLE OrderDetails DROP CONSTRAINT ' + @OldFK)
    END
    
    -- Đổi tên column
    PRINT '  ✓ Đang đổi tên ProductID → ProductDetailID...'
    EXEC sp_rename 'OrderDetails.ProductID', 'ProductDetailID', 'COLUMN';
    
    -- Tạo lại foreign key mới
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProductDetails')
    BEGIN
        PRINT '  ✓ Đang tạo lại foreign key...'
        
        -- Xóa constraint cũ nếu tồn tại
        IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_OrderDetails_ProductDetails')
        BEGIN
            ALTER TABLE OrderDetails DROP CONSTRAINT FK_OrderDetails_ProductDetails;
        END
        
        -- Tạo mới
        ALTER TABLE OrderDetails
        ADD CONSTRAINT FK_OrderDetails_ProductDetails 
        FOREIGN KEY (ProductDetailID) REFERENCES ProductDetails(ProductDetailID)
        ON DELETE NO ACTION;
        
        PRINT '  ✓ Đã tạo lại foreign key'
    END
    
    PRINT '  ✓ Hoàn thành đổi tên column'
END
ELSE IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'OrderDetails' 
    AND COLUMN_NAME = 'ProductDetailID'
)
BEGIN
    PRINT '  ✓ Column ProductDetailID đã tồn tại - không cần thay đổi'
END
ELSE
BEGIN
    PRINT '  ⚠ CẢNH BÁO: Không tìm thấy ProductID hoặc ProductDetailID'
    PRINT '  ✓ Đang tạo column ProductDetailID mới...'
    
    ALTER TABLE OrderDetails ADD ProductDetailID INT NULL;
    
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProductDetails')
    BEGIN
        ALTER TABLE OrderDetails
        ADD CONSTRAINT FK_OrderDetails_ProductDetails 
        FOREIGN KEY (ProductDetailID) REFERENCES ProductDetails(ProductDetailID);
    END
    
    PRINT '  ✓ Đã tạo column ProductDetailID'
END
GO

-- Thêm SizeName và ColorName nếu chưa có
IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'OrderDetails' AND COLUMN_NAME = 'SizeName'
)
BEGIN
    PRINT '  ✓ Đang thêm column SizeName...'
    ALTER TABLE OrderDetails ADD SizeName NVARCHAR(50) NULL;
    PRINT '  ✓ Đã thêm SizeName'
END
ELSE
BEGIN
    PRINT '  ✓ Column SizeName đã tồn tại'
END
GO

IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'OrderDetails' AND COLUMN_NAME = 'ColorName'
)
BEGIN
    PRINT '  ✓ Đang thêm column ColorName...'
    ALTER TABLE OrderDetails ADD ColorName NVARCHAR(50) NULL;
    PRINT '  ✓ Đã thêm ColorName'
END
ELSE
BEGIN
    PRINT '  ✓ Column ColorName đã tồn tại'
END
GO

PRINT ''
PRINT '>>> PHẦN 2: HOÀN THÀNH ✅'
PRINT ''

-- ==========================================
-- PHẦN 3: FIX ORDERS DECIMAL COLUMNS
-- ==========================================
PRINT '>>> PHẦN 3: FIX DECIMAL COLUMNS (TotalMoney, TotalDiscount)'
PRINT ''

-- Fix TotalMoney INT -> DECIMAL
DECLARE @TotalMoneyType VARCHAR(50)
SELECT @TotalMoneyType = DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'TotalMoney'

IF @TotalMoneyType = 'int'
BEGIN
    PRINT '  ✓ Chuyển TotalMoney: INT -> DECIMAL(10,2)...'
    
    DECLARE @TMConstraint NVARCHAR(200)
    SELECT @TMConstraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'Orders' AND c.name = 'TotalMoney'
    
    IF @TMConstraint IS NOT NULL
        EXEC('ALTER TABLE Orders DROP CONSTRAINT ' + @TMConstraint)
    
    ALTER TABLE Orders ALTER COLUMN TotalMoney DECIMAL(10,2) NULL;
    PRINT '  ✓ TotalMoney OK'
END
ELSE
    PRINT '  ✓ TotalMoney đã là DECIMAL'
GO

-- Fix TotalDiscount INT -> DECIMAL
DECLARE @TotalDiscountType VARCHAR(50)
SELECT @TotalDiscountType = DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'TotalDiscount'

IF @TotalDiscountType = 'int'
BEGIN
    PRINT '  ✓ Chuyển TotalDiscount: INT -> DECIMAL(10,2)...'
    
    UPDATE Orders SET TotalDiscount = 0 WHERE TotalDiscount IS NULL;
    
    DECLARE @TDConstraint NVARCHAR(200)
    SELECT @TDConstraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'Orders' AND c.name = 'TotalDiscount'
    
    IF @TDConstraint IS NOT NULL
        EXEC('ALTER TABLE Orders DROP CONSTRAINT ' + @TDConstraint)
    
    ALTER TABLE Orders ALTER COLUMN TotalDiscount DECIMAL(10,2) NOT NULL;
    
    IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_Orders_TotalDiscount')
        ALTER TABLE Orders ADD CONSTRAINT DF_Orders_TotalDiscount DEFAULT 0 FOR TotalDiscount;
    
    PRINT '  ✓ TotalDiscount OK'
END
ELSE
BEGIN
    PRINT '  ✓ TotalDiscount đã là DECIMAL'
    
    IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_Orders_TotalDiscount')
    BEGIN
        ALTER TABLE Orders ADD CONSTRAINT DF_Orders_TotalDiscount DEFAULT 0 FOR TotalDiscount;
        PRINT '  ✓ Đã thêm default constraint'
    END
END
GO

PRINT ''
PRINT '>>> PHẦN 3: HOÀN THÀNH ✅'
PRINT ''

-- ==========================================
-- PHẦN 4: KIỂM TRA KẾT QUẢ
-- ==========================================
PRINT '>>> PHẦN 4: KIỂM TRA KẾT QUẢ'
PRINT ''

PRINT 'Cấu trúc bảng ORDERS:'
SELECT 
    COLUMN_NAME AS [Column],
    DATA_TYPE AS [Type],
    ISNULL(CAST(NUMERIC_PRECISION AS VARCHAR(10)), '-') AS [Precision],
    ISNULL(CAST(NUMERIC_SCALE AS VARCHAR(10)), '-') AS [Scale],
    IS_NULLABLE AS [Nullable],
    COLUMN_DEFAULT AS [Default]
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Orders' 
AND COLUMN_NAME IN ('Deleted', 'Paid', 'LocationID', 'TotalMoney', 'TotalDiscount')
ORDER BY ORDINAL_POSITION;
GO

PRINT ''
PRINT 'Cấu trúc bảng ORDERDETAILS:'
SELECT 
    COLUMN_NAME AS [Column],
    DATA_TYPE AS [Type],
    IS_NULLABLE AS [Nullable],
    ISNULL(CAST(CHARACTER_MAXIMUM_LENGTH AS VARCHAR(10)), '-') AS [MaxLength]
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'OrderDetails'
AND COLUMN_NAME IN ('ProductDetailID', 'SizeName', 'ColorName')
ORDER BY ORDINAL_POSITION;
GO

PRINT ''
PRINT '========================================='
PRINT 'TAT CA DA HOAN THANH!'
PRINT '========================================='
PRINT ''
PRINT 'CAC PHAN DA FIX:'
PRINT '  1. Orders.Deleted & Orders.Paid: BIT NOT NULL DEFAULT 0'
PRINT '  2. OrderDetails.ProductDetailID: Doi ten tu ProductID'
PRINT '  3. OrderDetails.SizeName & ColorName: NVARCHAR(50)'
PRINT '  4. Orders.TotalMoney: DECIMAL(10,2) NULL'
PRINT '  5. Orders.TotalDiscount: DECIMAL(10,2) NOT NULL DEFAULT 0'
PRINT ''
PRINT 'BAY GIO BAN CO THE:'
PRINT '  1. Restart lai ung dung ASP.NET Core'
PRINT '  2. Thu dang nhap va dat hang'
PRINT '  3. Kiem tra xem con loi nao khong'
PRINT ''
PRINT 'DONE!'
PRINT '========================================='
GO

