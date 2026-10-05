using System;
using Microsoft.EntityFrameworkCore;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ENTITY FRAMEWORK CORE DB CONTEXT (WarehouseDbContext)
    /// - Quản lý kết nối ORM trực tiếp tới cơ sở dữ liệu SQL Server (Database: quanlykho).
    /// - Thay thế hoàn toàn ADO.NET thuần túy bằng Entity Framework Core (EF Core 10).
    /// - Quản lý DbSet cho các thực thể bưu kiện, tài xế, phiếu nhập, người dùng và nhật ký.
    /// </summary>
    public class WarehouseDbContext : DbContext
    {
        public static string DefaultConnectionString { get; set; } = 
            "Server=localhost;Database=quanlykho;Trusted_Connection=True;TrustServerCertificate=True;";

        public DbSet<ShippingOrder> ShippingOrders { get; set; } = null!;
        public DbSet<ImportOrder> ImportOrders { get; set; } = null!;
        public DbSet<Shipper> Shippers { get; set; } = null!;
        public DbSet<RecentActivity> RecentActivities { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;

        public WarehouseDbContext() { }

        public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : base(options) { }

        public static WarehouseDbContext Create() => new();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(DefaultConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================================================================
            // 1. CẤU HÌNH BẢNG ShippingOrders (Bưu kiện vận chuyển)
            // =========================================================================
            modelBuilder.Entity<ShippingOrder>(entity =>
            {
                entity.ToTable("ShippingOrders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.SenderName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.SenderPhone).HasMaxLength(20).IsRequired();
                entity.Property(e => e.SenderAddress).HasMaxLength(255);
                entity.Property(e => e.ReceiverName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ReceiverPhone).HasMaxLength(20).IsRequired();
                entity.Property(e => e.ReceiverAddress).HasMaxLength(255).IsRequired();
                entity.Property(e => e.DestinationArea).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ProductSummary).HasMaxLength(255).IsRequired();
                entity.Property(e => e.CodAmount).HasPrecision(18, 2);
                entity.Property(e => e.ShippingFee).HasPrecision(18, 2);
                entity.Property(e => e.ExpressSurcharge).HasPrecision(18, 2);
                entity.Property(e => e.AssignedShipperName).HasMaxLength(100);
                entity.Property(e => e.ShipperPhone).HasMaxLength(20);
                entity.Property(e => e.Notes).HasMaxLength(500);

                // Bỏ qua các thuộc tính UI tính toán và trường mở rộng không có trong DB
                entity.Ignore(e => e.TotalCustomerPayment);
                entity.Ignore(e => e.IsOverdue);
                entity.Ignore(e => e.StatusDisplayName);
                entity.Ignore(e => e.StatusColor);
                entity.Ignore(e => e.ServiceTypeTag);
                entity.Ignore(e => e.IsLocalHubDelivery);
                entity.Ignore(e => e.RoutingCategoryName);
                entity.Ignore(e => e.RoutingCategoryTag);
                entity.Ignore(e => e.RoutingCategoryBadgeBackground);
                entity.Ignore(e => e.RoutingCategoryBorderColor);
                entity.Ignore(e => e.RoutingCategoryTextColor);
                entity.Ignore(e => e.RoutingActionRecommendation);
                entity.Ignore(e => e.CanQuickDeliverLocal);
                entity.Ignore(e => e.FailedDeliveryCount);
                entity.Ignore(e => e.FailureReason);
                entity.Ignore(e => e.FailureTimestamp);
                entity.Ignore(e => e.RtoTrackingCode);
                entity.Ignore(e => e.RtoLocationCode);
                entity.Ignore(e => e.RtoApprovedDate);
                entity.Ignore(e => e.ReturnShippingFee);
                entity.Ignore(e => e.ReturnHandoverBatchCode);
            });

            // =========================================================================
            // 2. CẤU HÌNH BẢNG ImportOrders (Phiếu nhập kho)
            // =========================================================================
            modelBuilder.Entity<ImportOrder>(entity =>
            {
                entity.ToTable("ImportOrders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ImportCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.SenderName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.WaybillNumber).HasMaxLength(50);
                entity.Property(e => e.VehiclePlate).HasColumnName("VehicleNumber").HasMaxLength(50);
                entity.Property(e => e.Notes).HasMaxLength(500);

                // Bỏ qua các trường mở rộng không có trong bảng SQL Server
                entity.Ignore(e => e.SenderPhone);
                entity.Ignore(e => e.SenderAddress);
                entity.Ignore(e => e.DriverName);
                entity.Ignore(e => e.TotalValue);
                entity.Ignore(e => e.CreatedByName);
                entity.Ignore(e => e.ApprovedDate);
                entity.Ignore(e => e.ApprovedByName);
                entity.Ignore(e => e.Details);
            });

            // =========================================================================
            // 3. CẤU HÌNH BẢNG Shippers (Đội ngũ Shipper)
            // =========================================================================
            modelBuilder.Entity<Shipper>(entity =>
            {
                entity.ToTable("Shippers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FullName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Phone).HasColumnName("PhoneNumber").HasMaxLength(20);
                entity.Property(e => e.VehicleType).HasMaxLength(50);
                entity.Property(e => e.VehiclePlate).HasColumnName("LicensePlate").HasMaxLength(20);
                entity.Property(e => e.DeliveryArea).HasColumnName("CurrentArea").HasMaxLength(100);
                entity.Property(e => e.CompletedTodayCount).HasColumnName("CompletedOrdersToday");

                // Bỏ qua alias và các trường mở rộng trong bộ nhớ
                entity.Ignore(e => e.PhoneNumber);
                entity.Ignore(e => e.LicensePlate);
                entity.Ignore(e => e.CurrentArea);
                entity.Ignore(e => e.CompletedOrdersToday);
                entity.Ignore(e => e.CitizenId);
                entity.Ignore(e => e.IsLocked);
                entity.Ignore(e => e.WorkShift);
                entity.Ignore(e => e.MaxOrdersPerDay);
                entity.Ignore(e => e.MaxWeightCapacity);
                entity.Ignore(e => e.ActiveDeliveringCount);
                entity.Ignore(e => e.CapacityUsagePercentage);
                entity.Ignore(e => e.StatusDisplayName);
                entity.Ignore(e => e.StatusColor);
            });

            // =========================================================================
            // 4. CẤU HÌNH BẢNG RecentActivities (Nhật ký hoạt động)
            // =========================================================================
            modelBuilder.Entity<RecentActivity>(entity =>
            {
                entity.ToTable("RecentActivities");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Timestamp);
                entity.Property(e => e.Type).HasColumnName("Category").HasConversion<string>();
            });

            // =========================================================================
            // 5. CẤU HÌNH BẢNG _users (Tài khoản người dùng)
            // =========================================================================
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("_users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("UserId");
                entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
                entity.Property(e => e.PasswordHash).HasColumnName("Password").HasMaxLength(255).IsRequired();
                entity.Property(e => e.FullName).HasMaxLength(100);
                entity.Property(e => e.Role).HasConversion<string>(); // Lưu enum dạng text trong SQL
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedAt);

                // Bỏ qua các thuộc tính mở rộng không có trong bảng _users
                entity.Ignore(e => e.Email);
                entity.Ignore(e => e.PhoneNumber);
                entity.Ignore(e => e.LastLoginAt);
                entity.Ignore(e => e.CustomPermissions);
            });
        }
    }
}
