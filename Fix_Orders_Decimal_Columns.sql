-- ✅ Script để fix lỗi "Unable to cast object of type 'System.Int32' to type 'System.Decimal'"
-- Chuyển TotalMoney và TotalDiscount từ INT sang DECIMAL(10,2)

USE webshop;
GO

PRINT '========================================='
PRINT 'FIX ORDERS DECIMAL COLUMNS - BAT DAU'
PRINT '========================================='
PRINT ''
GO

-- Kiểm tra kiểu dữ liệu hiện tại
PRINT 'Kieu du lieu hien tai cua Orders:'
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Orders' 
AND COLUMN_NAME IN ('TotalMoney', 'TotalDiscount')
ORDER BY COLUMN_NAME;
GO

PRINT ''
PRINT '>>> Dang cap nhat columns...'
PRINT ''

-- Fix TotalMoney
DECLARE @TotalMoneyType VARCHAR(50)
SELECT @TotalMoneyType = DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'TotalMoney'

IF @TotalMoneyType = 'int'
BEGIN
    PRINT '  Dang chuyen TotalMoney tu INT -> DECIMAL(10,2)...'
    
    -- Xóa default constraint nếu có
    DECLARE @TotalMoneyConstraint NVARCHAR(200)
    SELECT @TotalMoneyConstraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'Orders' AND c.name = 'TotalMoney'
    
    IF @TotalMoneyConstraint IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE Orders DROP CONSTRAINT ' + @TotalMoneyConstraint)
        PRINT '  Da xoa constraint: ' + @TotalMoneyConstraint
    END
    
    -- Chuyển đổi column
    ALTER TABLE Orders 
    ALTER COLUMN TotalMoney DECIMAL(10,2) NULL;
    
    PRINT '  HOAN THANH: TotalMoney -> DECIMAL(10,2)'
END
ELSE IF @TotalMoneyType = 'decimal' OR @TotalMoneyType = 'numeric'
BEGIN
    PRINT '  TotalMoney da la DECIMAL - khong can thay doi'
END
ELSE
BEGIN
    PRINT '  CANH BAO: TotalMoney co kieu du lieu la nghia la: ' + @TotalMoneyType
END
GO

-- Fix TotalDiscount
DECLARE @TotalDiscountType VARCHAR(50)
SELECT @TotalDiscountType = DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'TotalDiscount'

IF @TotalDiscountType = 'int'
BEGIN
    PRINT '  Dang chuyen TotalDiscount tu INT -> DECIMAL(10,2)...'
    
    -- Cập nhật NULL thành 0 trước
    UPDATE Orders SET TotalDiscount = 0 WHERE TotalDiscount IS NULL;
    
    -- Xóa default constraint nếu có
    DECLARE @TotalDiscountConstraint NVARCHAR(200)
    SELECT @TotalDiscountConstraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'Orders' AND c.name = 'TotalDiscount'
    
    IF @TotalDiscountConstraint IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE Orders DROP CONSTRAINT ' + @TotalDiscountConstraint)
        PRINT '  Da xoa constraint: ' + @TotalDiscountConstraint
    END
    
    -- Chuyển đổi column
    ALTER TABLE Orders 
    ALTER COLUMN TotalDiscount DECIMAL(10,2) NOT NULL;
    
    -- Thêm lại default constraint
    ALTER TABLE Orders 
    ADD CONSTRAINT DF_Orders_TotalDiscount DEFAULT 0 FOR TotalDiscount;
    
    PRINT '  HOAN THANH: TotalDiscount -> DECIMAL(10,2) NOT NULL DEFAULT 0'
END
ELSE IF @TotalDiscountType = 'decimal' OR @TotalDiscountType = 'numeric'
BEGIN
    PRINT '  TotalDiscount da la DECIMAL - khong can thay doi'
    
    -- Đảm bảo có default constraint
    IF NOT EXISTS (
        SELECT * FROM sys.default_constraints 
        WHERE name = 'DF_Orders_TotalDiscount'
    )
    BEGIN
        ALTER TABLE Orders 
        ADD CONSTRAINT DF_Orders_TotalDiscount DEFAULT 0 FOR TotalDiscount;
        PRINT '  Da them default constraint cho TotalDiscount'
    END
END
ELSE
BEGIN
    PRINT '  CANH BAO: TotalDiscount co kieu du lieu la nghia la: ' + @TotalDiscountType
END
GO

PRINT ''
PRINT '>>> KIEM TRA KET QUA:'
PRINT ''

-- Kiểm tra lại kiểu dữ liệu sau khi fix
SELECT 
    COLUMN_NAME AS [Column],
    DATA_TYPE AS [Type],
    ISNULL(CAST(NUMERIC_PRECISION AS VARCHAR(10)), '-') AS [Precision],
    ISNULL(CAST(NUMERIC_SCALE AS VARCHAR(10)), '-') AS [Scale],
    IS_NULLABLE AS [Nullable],
    COLUMN_DEFAULT AS [Default]
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Orders' 
AND COLUMN_NAME IN ('TotalMoney', 'TotalDiscount')
ORDER BY COLUMN_NAME;
GO

PRINT ''
PRINT '========================================='
PRINT 'HOAN THANH!'
PRINT '========================================='
PRINT ''
PRINT 'Bay gio ban co the:'
PRINT '1. Restart lai ung dung ASP.NET Core'
PRINT '2. Thu dang nhap lai'
PRINT ''
GO

