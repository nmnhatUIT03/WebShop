-- ✅ Script để sửa lỗi "Invalid column name 'ProductDetailID'" trong bảng OrderDetails

USE webshop;
GO

PRINT '===== BẮT ĐẦU FIX ORDERDETAILS TABLE ====='
GO

-- Kiểm tra column hiện tại trong bảng OrderDetails
PRINT 'Đang kiểm tra cấu trúc bảng OrderDetails...'
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'OrderDetails'
ORDER BY ORDINAL_POSITION;
GO

-- Kiểm tra xem có column ProductID không
IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'OrderDetails' 
    AND COLUMN_NAME = 'ProductID'
)
BEGIN
    PRINT 'Tìm thấy column ProductID - cần đổi tên thành ProductDetailID'
    
    -- Kiểm tra xem có column ProductDetailID chưa
    IF NOT EXISTS (
        SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'OrderDetails' 
        AND COLUMN_NAME = 'ProductDetailID'
    )
    BEGIN
        -- Xóa foreign key constraint nếu có
        DECLARE @ConstraintName NVARCHAR(200)
        SELECT @ConstraintName = CONSTRAINT_NAME
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
        WHERE TABLE_NAME = 'OrderDetails' 
        AND COLUMN_NAME = 'ProductID'
        
        IF @ConstraintName IS NOT NULL
        BEGIN
            PRINT 'Đang xóa foreign key constraint: ' + @ConstraintName
            EXEC('ALTER TABLE OrderDetails DROP CONSTRAINT ' + @ConstraintName)
        END
        
        -- Đổi tên column
        PRINT 'Đang đổi tên column ProductID → ProductDetailID...'
        EXEC sp_rename 'OrderDetails.ProductID', 'ProductDetailID', 'COLUMN';
        
        -- Tạo lại foreign key nếu cần
        IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProductDetails')
        BEGIN
            PRINT 'Đang tạo lại foreign key constraint...'
            ALTER TABLE OrderDetails
            ADD CONSTRAINT FK_OrderDetails_ProductDetails 
            FOREIGN KEY (ProductDetailID) REFERENCES ProductDetails(ProductDetailID);
            
            PRINT 'Đã tạo lại foreign key constraint'
        END
        
        PRINT 'Hoàn thành đổi tên column!'
    END
    ELSE
    BEGIN
        PRINT 'Column ProductDetailID đã tồn tại - bỏ qua bước đổi tên'
    END
END
ELSE IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'OrderDetails' 
    AND COLUMN_NAME = 'ProductDetailID'
)
BEGIN
    PRINT 'Column ProductDetailID đã tồn tại - không cần thay đổi'
END
ELSE
BEGIN
    PRINT 'CẢNH BÁO: Không tìm thấy column ProductID hoặc ProductDetailID!'
    PRINT 'Đang tạo column ProductDetailID mới...'
    
    ALTER TABLE OrderDetails
    ADD ProductDetailID INT NULL;
    
    -- Tạo foreign key nếu có bảng ProductDetails
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProductDetails')
    BEGIN
        ALTER TABLE OrderDetails
        ADD CONSTRAINT FK_OrderDetails_ProductDetails 
        FOREIGN KEY (ProductDetailID) REFERENCES ProductDetails(ProductDetailID);
    END
    
    PRINT 'Đã tạo column ProductDetailID'
END
GO

-- Kiểm tra và thêm column SizeName, ColorName nếu chưa có
IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'OrderDetails' 
    AND COLUMN_NAME = 'SizeName'
)
BEGIN
    PRINT 'Đang thêm column SizeName...'
    ALTER TABLE OrderDetails ADD SizeName NVARCHAR(50) NULL;
    PRINT 'Đã thêm column SizeName'
END
ELSE
BEGIN
    PRINT 'Column SizeName đã tồn tại'
END
GO

IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'OrderDetails' 
    AND COLUMN_NAME = 'ColorName'
)
BEGIN
    PRINT 'Đang thêm column ColorName...'
    ALTER TABLE OrderDetails ADD ColorName NVARCHAR(50) NULL;
    PRINT 'Đã thêm column ColorName'
END
ELSE
BEGIN
    PRINT 'Column ColorName đã tồn tại'
END
GO

-- Kiểm tra kết quả cuối cùng
PRINT ''
PRINT '===== KẾT QUẢ CUỐI CÙNG ====='
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    ISNULL(CAST(CHARACTER_MAXIMUM_LENGTH AS VARCHAR(10)), 'N/A') AS MAX_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'OrderDetails'
ORDER BY ORDINAL_POSITION;
GO

PRINT ''
PRINT '✅ Script hoàn tất!'
PRINT 'Bảng OrderDetails đã được cập nhật thành công'
GO

