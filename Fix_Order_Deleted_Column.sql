-- ✅ Script để sửa lỗi Order.Deleted không thể set null

USE webshop;
GO

-- Kiểm tra xem column Deleted có cho phép NULL không
IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Orders' 
    AND COLUMN_NAME = 'Deleted' 
    AND IS_NULLABLE = 'YES'
)
BEGIN
    PRINT 'Đang cập nhật column Deleted để không cho phép NULL...'
    
    -- Cập nhật tất cả giá trị NULL thành 0 (false)
    UPDATE Orders SET Deleted = 0 WHERE Deleted IS NULL;
    
    -- Thay đổi column để không cho phép NULL và set default value
    ALTER TABLE Orders 
    ALTER COLUMN Deleted BIT NOT NULL;
    
    -- Thêm default constraint
    IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_Orders_Deleted')
    BEGIN
        ALTER TABLE Orders 
        ADD CONSTRAINT DF_Orders_Deleted DEFAULT 0 FOR Deleted;
        PRINT 'Đã thêm default constraint cho column Deleted'
    END
    
    PRINT 'Hoàn thành! Column Deleted giờ không cho phép NULL và có giá trị mặc định là 0 (false)'
END
ELSE
BEGIN
    PRINT 'Column Deleted đã được cấu hình đúng (NOT NULL)'
    
    -- Vẫn đảm bảo có default constraint
    IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_Orders_Deleted')
    BEGIN
        ALTER TABLE Orders 
        ADD CONSTRAINT DF_Orders_Deleted DEFAULT 0 FOR Deleted;
        PRINT 'Đã thêm default constraint cho column Deleted'
    END
END
GO

-- Kiểm tra và làm tương tự cho column Paid
IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Orders' 
    AND COLUMN_NAME = 'Paid' 
    AND IS_NULLABLE = 'YES'
)
BEGIN
    PRINT 'Đang cập nhật column Paid để không cho phép NULL...'
    
    UPDATE Orders SET Paid = 0 WHERE Paid IS NULL;
    
    ALTER TABLE Orders 
    ALTER COLUMN Paid BIT NOT NULL;
    
    IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_Orders_Paid')
    BEGIN
        ALTER TABLE Orders 
        ADD CONSTRAINT DF_Orders_Paid DEFAULT 0 FOR Paid;
        PRINT 'Đã thêm default constraint cho column Paid'
    END
    
    PRINT 'Hoàn thành! Column Paid giờ không cho phép NULL và có giá trị mặc định là 0 (false)'
END
ELSE
BEGIN
    PRINT 'Column Paid đã được cấu hình đúng (NOT NULL)'
    
    IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_Orders_Paid')
    BEGIN
        ALTER TABLE Orders 
        ADD CONSTRAINT DF_Orders_Paid DEFAULT 0 FOR Paid;
        PRINT 'Đã thêm default constraint cho column Paid'
    END
END
GO

-- Kiểm tra kết quả
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Orders' 
AND COLUMN_NAME IN ('Deleted', 'Paid');
GO

PRINT 'Script hoàn tất!'

