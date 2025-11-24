
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace WebShop.Models
{
    public partial class webshopContext : DbContext
    {
        public webshopContext()
        {
        }

        public webshopContext(DbContextOptions<webshopContext> options)
            : base(options)
        {
            ChangeTracker.LazyLoadingEnabled = false;
        }

        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Comment> Comments { get; set; }
        public virtual DbSet<Customer> Customers { get; set; }
        public virtual DbSet<Location> Locations { get; set; }
        public virtual DbSet<Order> Orders { get; set; }
        public virtual DbSet<OrderDetail> OrderDetails { get; set; }
        public virtual DbSet<Page> Pages { get; set; }
        public virtual DbSet<Product> Products { get; set; }
        public virtual DbSet<ProductDetail> ProductDetails { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<Supplier> Suppliers { get; set; }
        public virtual DbSet<TinTuc> TinTucs { get; set; }
        public virtual DbSet<TransactStatus> TransactStatuses { get; set; }
        public virtual DbSet<Color> Colors { get; set; }
        public virtual DbSet<Size> Sizes { get; set; }
        public virtual DbSet<Promotion> Promotions { get; set; }
        public virtual DbSet<PromotionProduct> PromotionProducts { get; set; }
        public virtual DbSet<Voucher> Vouchers { get; set; }
        public virtual DbSet<UserPromotion> UserPromotions { get; set; }
        public virtual DbSet<CheckInHistory> CheckInHistory { get; set; } // Thêm DbSet cho CheckInHistory
        public virtual DbSet<RewardHistory> RewardHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("Relational:Collation", "SQL_Latin1_General_CP1_CI_AS");

            modelBuilder.Entity<Account>(entity =>
            {
                entity.Property(e => e.AccountId).HasColumnName("AccountID");
                entity.Property(e => e.CreateDate).HasColumnType("datetime");
                entity.Property(e => e.Email).HasMaxLength(50);
                entity.Property(e => e.FullName).HasMaxLength(150);
                entity.Property(e => e.LastLogin).HasColumnType("datetime");
                entity.Property(e => e.Password).HasMaxLength(50);
                entity.Property(e => e.Phone).HasMaxLength(12).IsUnicode(false);
                entity.Property(e => e.RoleId).HasColumnName("RoleID");
                entity.Property(e => e.Salt).HasMaxLength(6).IsFixedLength(true);
                entity.HasOne(d => d.Role).WithMany(p => p.Accounts).HasForeignKey(d => d.RoleId).HasConstraintName("FK_Accounts_Roles");
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.CatId);
                entity.Property(e => e.CatId).HasColumnName("CatID");
                entity.Property(e => e.Alias).HasMaxLength(250);
                entity.Property(e => e.CatName).HasMaxLength(250);
                entity.Property(e => e.Cover).HasMaxLength(255);
                entity.Property(e => e.MetaDesc).HasMaxLength(250);
                entity.Property(e => e.MetaKey).HasMaxLength(250);
                entity.Property(e => e.ParentId).HasColumnName("ParentID");
                entity.Property(e => e.Thumb).HasMaxLength(250);
                entity.Property(e => e.Title).HasMaxLength(250);
            });

            modelBuilder.Entity<Comment>(entity =>
            {
                entity.ToTable("Comment");
                entity.Property(e => e.CommentId).HasColumnName("CommentID");
                entity.Property(e => e.Created).HasColumnType("datetime");
                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
                entity.Property(e => e.Message).HasMaxLength(500);
                entity.Property(e => e.ProductId).HasColumnName("ProductID");
                entity.HasOne(d => d.Customer).WithMany(p => p.Comments).HasForeignKey(d => d.CustomerId).HasConstraintName("FK_Comment_Customers");
                entity.HasOne(d => d.Product).WithMany(p => p.Comments).HasForeignKey(d => d.ProductId).HasConstraintName("FK_Comment_Products");
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
                entity.Property(e => e.Address).HasMaxLength(255);
                entity.Property(e => e.Avatar).HasMaxLength(255);
                entity.Property(e => e.Birthday).HasColumnType("datetime");
                entity.Property(e => e.CreateDate).HasColumnType("datetime");
                entity.Property(e => e.Email).HasMaxLength(150).IsFixedLength(true);
                entity.Property(e => e.FullName).HasMaxLength(255);
                entity.Property(e => e.LastLogin).HasColumnType("datetime");
                entity.Property(e => e.LocationId).HasColumnName("LocationID");
                entity.Property(e => e.Password).HasMaxLength(50);
                entity.Property(e => e.Phone).HasMaxLength(12).IsUnicode(false);
                entity.Property(e => e.Salt).HasMaxLength(8).IsFixedLength(true);
                entity.HasOne(d => d.Location).WithMany(p => p.Customers).HasForeignKey(d => d.LocationId).HasConstraintName("FK_Customers_Locations");
            });

            modelBuilder.Entity<Location>(entity =>
            {
                entity.Property(e => e.LocationId).HasColumnName("LocationID");
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.NameWithType).HasMaxLength(255);
                entity.Property(e => e.PathWithType).HasMaxLength(255);
                entity.Property(e => e.Slug).HasMaxLength(100);
                entity.Property(e => e.Type).HasMaxLength(20);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.OrderId).HasColumnName("OrderID");
                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
                entity.Property(e => e.ReceiverName).HasMaxLength(255);
                entity.Property(e => e.Address).HasMaxLength(255);
                entity.Property(e => e.LocationId).HasColumnName("LocationID");
                entity.Property(e => e.OrderDate).HasColumnType("datetime");
                entity.Property(e => e.ShipDate).HasColumnType("datetime");
                entity.Property(e => e.TransactStatusId).HasColumnName("TransactStatusID");
                entity.Property(e => e.Deleted).HasDefaultValue(false);
                entity.Property(e => e.Paid).HasDefaultValue(false);
                entity.Property(e => e.PaymentDate).HasColumnType("datetime");
                entity.Property(e => e.TotalMoney).HasColumnType("decimal(10,2)");
                entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
                entity.Property(e => e.Note).HasMaxLength(500);
                entity.Property(e => e.PromotionId).HasColumnName("PromotionID");
                entity.Property(e => e.VoucherId).HasColumnName("VoucherID");
                entity.Property(e => e.TotalDiscount).HasColumnType("decimal(10,2)").HasDefaultValue(0m).IsRequired();
                entity.HasOne(d => d.Customer).WithMany(p => p.Orders).HasForeignKey(d => d.CustomerId).HasConstraintName("FK_Orders_Customers");
                entity.HasOne(d => d.TransactStatus).WithMany(p => p.Orders).HasForeignKey(d => d.TransactStatusId).HasConstraintName("FK_Orders_TransactStatus");
                entity.HasOne(d => d.Promotion).WithMany(p => p.Orders).HasForeignKey(d => d.PromotionId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(d => d.Voucher).WithMany(p => p.Orders).HasForeignKey(d => d.VoucherId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.Property(e => e.OrderDetailId).HasColumnName("OrderDetailID");
                entity.Property(e => e.OrderId).HasColumnName("OrderID");
                entity.Property(e => e.ProductDetailId).HasColumnName("ProductDetailID");
                entity.Property(e => e.ShipDate).HasColumnType("datetime");
                entity.Property(e => e.SizeName).HasMaxLength(50);
                entity.Property(e => e.ColorName).HasMaxLength(50);
                entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails).HasForeignKey(d => d.OrderId).HasConstraintName("FK_OrderDetails_Orders");
                entity.HasOne(d => d.ProductDetail).WithMany(p => p.OrderDetails).HasForeignKey(d => d.ProductDetailId).HasConstraintName("FK_OrderDetails_ProductDetails");
            });

            modelBuilder.Entity<Page>(entity =>
            {
                entity.Property(e => e.PageId).HasColumnName("PageID");
                entity.Property(e => e.Alias).HasMaxLength(250);
                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.MetaDesc).HasMaxLength(250);
                entity.Property(e => e.MetaKey).HasMaxLength(250);
                entity.Property(e => e.PageName).HasMaxLength(250);
                entity.Property(e => e.Thumb).HasMaxLength(250);
                entity.Property(e => e.Title).HasMaxLength(250);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(e => e.ProductId).HasColumnName("ProductID");
                entity.Property(e => e.Alias).HasMaxLength(255);
                entity.Property(e => e.CatId).HasColumnName("CatID");
                entity.Property(e => e.Chatlieu).HasMaxLength(255).HasColumnName("chatlieu");
                entity.Property(e => e.DateCreated).HasColumnType("datetime");
                entity.Property(e => e.DateModified).HasColumnType("datetime");
                entity.Property(e => e.Image).HasMaxLength(50).HasColumnName("image");
                entity.Property(e => e.MetaDesc).HasMaxLength(255);
                entity.Property(e => e.MetaKey).HasMaxLength(255);
                entity.Property(e => e.ProductName).HasMaxLength(255);
                entity.Property(e => e.ShortDesc).HasMaxLength(255);
                entity.Property(e => e.Songan).HasMaxLength(255).HasColumnName("songan");
                entity.Property(e => e.Thumb).HasMaxLength(255);
                entity.Property(e => e.Title).HasMaxLength(255);
                entity.Property(e => e.Video).HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.HasOne(d => d.Cat).WithMany(p => p.Products).HasForeignKey(d => d.CatId).HasConstraintName("FK_Products_Categories");
                entity.HasOne(d => d.Supplier).WithMany(p => p.Products).HasForeignKey(d => d.SupplierId).HasConstraintName("FK__Products__Suppli__04E4BC85");
            });

            modelBuilder.Entity<ProductDetail>(entity =>
            {
                entity.Property(e => e.ProductDetailId).HasColumnName("ProductDetailID");
                entity.Property(e => e.ProductId).HasColumnName("ProductID");
                entity.Property(e => e.SizeId).HasColumnName("SizeID");
                entity.Property(e => e.ColorId).HasColumnName("ColorID");
                entity.HasOne(d => d.Product).WithMany(p => p.ProductDetails).HasForeignKey(d => d.ProductId).HasConstraintName("FK_ProductDetails_Products");
                entity.HasOne(d => d.Size).WithMany(p => p.ProductDetails).HasForeignKey(d => d.SizeId).HasConstraintName("FK_ProductDetails_Sizes");
                entity.HasOne(d => d.Color).WithMany(p => p.ProductDetails).HasForeignKey(d => d.ColorId).HasConstraintName("FK_ProductDetails_Colors");
            });

            modelBuilder.Entity<Promotion>(entity =>
            {
                entity.Property(e => e.PromotionId).HasColumnName("PromotionID");
                entity.Property(e => e.PromotionName).HasMaxLength(100);
                entity.Property(e => e.Discount).HasColumnType("decimal(5,2)");
                entity.Property(e => e.StartDate).HasColumnType("date");
                entity.Property(e => e.EndDate).HasColumnType("date");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.DefaultUserMaxUsage).HasDefaultValue(1); // Thêm thuộc tính mới
            });

            modelBuilder.Entity<PromotionProduct>(entity =>
            {
                entity.Property(e => e.PromotionProductId).HasColumnName("PromotionProductID");
                entity.Property(e => e.PromotionId).HasColumnName("PromotionID");
                entity.Property(e => e.ProductId).HasColumnName("ProductID");
                entity.HasOne(d => d.Promotion).WithMany(p => p.PromotionProducts).HasForeignKey(d => d.PromotionId).HasConstraintName("FK_PromotionProducts_Promotions");
                entity.HasOne(d => d.Product).WithMany(p => p.PromotionProducts).HasForeignKey(d => d.ProductId).HasConstraintName("FK_PromotionProducts_Products");
            });

            modelBuilder.Entity<Voucher>(entity =>
            {
                entity.Property(e => e.VoucherId).HasColumnName("VoucherID");
                entity.Property(e => e.VoucherCode).HasMaxLength(50).IsUnicode(true);
                entity.Property(e => e.DiscountType).HasMaxLength(20).HasDefaultValue("Percentage");
                entity.Property(e => e.DiscountValue).HasColumnType("decimal(10,2)");
                entity.Property(e => e.MaxUsage).HasDefaultValue(9999);
                entity.Property(e => e.UsedCount).HasDefaultValue(0);
                entity.Property(e => e.MinOrderValue).HasColumnType("decimal(10,2)").IsRequired(false);
                entity.Property(e => e.DefaultUserMaxUsage).HasDefaultValue(1);
                entity.Property(e => e.EndDate).HasColumnType("datetime");
                entity.HasIndex(e => e.VoucherCode).IsUnique();
            });

            modelBuilder.Entity<UserPromotion>(entity =>
            {
                entity.Property(e => e.UserPromotionId).HasColumnName("UserPromotionID");
                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
                entity.Property(e => e.PromotionId).HasColumnName("PromotionID");
                entity.Property(e => e.VoucherId).HasColumnName("VoucherID");
                entity.Property(e => e.UsedDate).HasColumnType("datetime").IsRequired();
                entity.HasOne(d => d.Customer).WithMany(p => p.UserPromotions).HasForeignKey(d => d.CustomerId).HasConstraintName("FK_UserPromotions_Customers");
                entity.HasOne(d => d.Promotion).WithMany(p => p.UserPromotions).HasForeignKey(d => d.PromotionId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_UserPromotions_Promotions");
                entity.HasOne(d => d.Voucher).WithMany(p => p.UserPromotions).HasForeignKey(d => d.VoucherId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_UserPromotions_Vouchers");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.Property(e => e.RoleId).HasColumnName("RoleID");
                entity.Property(e => e.Description).HasMaxLength(50);
                entity.Property(e => e.RoleName).HasMaxLength(50);
            });

            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.ToTable("Supplier");
                entity.Property(e => e.SupplierId).HasColumnName("SupplierID");
                entity.Property(e => e.Address).HasMaxLength(300);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
            });

            modelBuilder.Entity<TinTuc>(entity =>
            {
                entity.HasKey(e => e.PostId);
                entity.Property(e => e.PostId).HasColumnName("PostID");
                entity.Property(e => e.AccountId).HasColumnName("AccountID");
                entity.Property(e => e.Alias).HasMaxLength(255);
                entity.Property(e => e.Author).HasMaxLength(255);
                entity.Property(e => e.CatId).HasColumnName("CatID");
                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.IsHot).HasColumnName("isHot");
                entity.Property(e => e.IsNewfeed).HasColumnName("isNewfeed");
                entity.Property(e => e.MetaDesc).HasMaxLength(255);
                entity.Property(e => e.MetaKey).HasMaxLength(255);
                entity.Property(e => e.Scontents).HasMaxLength(255).HasColumnName("SContents");
                entity.Property(e => e.Thumb).HasMaxLength(255);
                entity.Property(e => e.Title).HasMaxLength(255);
            });

            modelBuilder.Entity<TransactStatus>(entity =>
            {
                entity.ToTable("TransactStatus");
                entity.Property(e => e.TransactStatusId).HasColumnName("TransactStatusID");
                entity.Property(e => e.Status).HasMaxLength(50);
            });

            modelBuilder.Entity<Color>(entity =>
            {
                entity.Property(e => e.ColorId).HasColumnName("ColorID");
                entity.Property(e => e.ColorName).HasMaxLength(50);
            });

            modelBuilder.Entity<Size>(entity =>
            {
                entity.Property(e => e.SizeId).HasColumnName("SizeID");
                entity.Property(e => e.SizeName).HasMaxLength(50);
            });

            // Cấu hình cho CheckInHistory
            modelBuilder.Entity<CheckInHistory>(entity =>
            {
                entity.HasKey(e => e.CheckInHistoryId);
                entity.Property(e => e.CheckInHistoryId).ValueGeneratedOnAdd(); // Tự động tăng
                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
                entity.Property(e => e.CheckInDate).HasColumnType("datetime");
                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.CheckInHistory) // Liên kết với Customer
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_CheckInHistory_Customers");
            });
            modelBuilder.Entity<RewardHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
                entity.Property(e => e.RewardName).HasMaxLength(255);
                entity.Property(e => e.RedeemedAt).HasColumnType("datetime");
                entity.Property(e => e.PointsUsed);
                entity.Property(e => e.IsConfirmed); // Thêm khai báo cho IsConfirmed
                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.RewardHistories)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_RewardHistory_Customers");
            });
            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}