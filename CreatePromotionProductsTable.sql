-- Script tạo bảng PromotionProducts và Promotions nếu chưa có
USE [webshop]
GO

-- Tạo bảng Promotions nếu chưa tồn tại
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Promotions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Promotions](
        [PromotionID] [int] IDENTITY(1,1) NOT NULL,
        [PromotionName] [nvarchar](100) NULL,
        [Discount] [decimal](5, 2) NOT NULL,
        [StartDate] [date] NOT NULL,
        [EndDate] [date] NOT NULL,
        [IsActive] [bit] NOT NULL DEFAULT 1,
        [DefaultUserMaxUsage] [int] NOT NULL DEFAULT 1,
        CONSTRAINT [PK_Promotions] PRIMARY KEY CLUSTERED ([PromotionID] ASC)
    ) ON [PRIMARY]
    
    PRINT 'Bảng Promotions đã được tạo.'
END
ELSE
BEGIN
    PRINT 'Bảng Promotions đã tồn tại.'
END
GO

-- Tạo bảng PromotionProducts nếu chưa tồn tại
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PromotionProducts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PromotionProducts](
        [PromotionProductID] [int] IDENTITY(1,1) NOT NULL,
        [PromotionID] [int] NOT NULL,
        [ProductID] [int] NOT NULL,
        CONSTRAINT [PK_PromotionProducts] PRIMARY KEY CLUSTERED ([PromotionProductID] ASC),
        CONSTRAINT [FK_PromotionProducts_Promotions] FOREIGN KEY([PromotionID])
            REFERENCES [dbo].[Promotions] ([PromotionID])
            ON DELETE CASCADE,
        CONSTRAINT [FK_PromotionProducts_Products] FOREIGN KEY([ProductID])
            REFERENCES [dbo].[Products] ([ProductID])
            ON DELETE CASCADE
    ) ON [PRIMARY]
    
    PRINT 'Bảng PromotionProducts đã được tạo.'
END
ELSE
BEGIN
    PRINT 'Bảng PromotionProducts đã tồn tại.'
END
GO

-- Tạo bảng UserPromotions nếu chưa tồn tại
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserPromotions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UserPromotions](
        [UserPromotionID] [int] IDENTITY(1,1) NOT NULL,
        [CustomerID] [int] NOT NULL,
        [PromotionID] [int] NULL,
        [VoucherID] [int] NULL,
        [UsedDate] [datetime] NOT NULL,
        CONSTRAINT [PK_UserPromotions] PRIMARY KEY CLUSTERED ([UserPromotionID] ASC),
        CONSTRAINT [FK_UserPromotions_Customers] FOREIGN KEY([CustomerID])
            REFERENCES [dbo].[Customers] ([CustomerID])
            ON DELETE CASCADE,
        CONSTRAINT [FK_UserPromotions_Promotions] FOREIGN KEY([PromotionID])
            REFERENCES [dbo].[Promotions] ([PromotionID])
            ON DELETE SET NULL,
        CONSTRAINT [FK_UserPromotions_Vouchers] FOREIGN KEY([VoucherID])
            REFERENCES [dbo].[Vouchers] ([VoucherID])
            ON DELETE SET NULL
    ) ON [PRIMARY]
    
    PRINT 'Bảng UserPromotions đã được tạo.'
END
ELSE
BEGIN
    PRINT 'Bảng UserPromotions đã tồn tại.'
END
GO

PRINT 'Hoàn tất tạo các bảng liên quan đến Promotion.'

