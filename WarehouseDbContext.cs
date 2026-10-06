using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ENTITY FRAMEWORK CORE DB CONTEXT (WarehouseDbContext)
    /// - Quản lý kết nối ORM trực tiếp tới cơ sở dữ liệu SQL Server (Database: quanlykho).
    /// - Thay thế hoàn toàn ADO.NET thuần túy bằng Entity Framework Core (EF Core 10).
    /// - Quản lý DbSet cho toàn bộ thực thể hệ thống: đơn hàng, phiếu nhập, shipper, user,
    ///   vị trí kho bãi, biến động kho, phiên điều phối và biên bản hoàn trả.
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
        public DbSet<WarehouseLocation> WarehouseLocations { get; set; } = null!;
        public DbSet<WarehouseMovement> WarehouseMovements { get; set; } = null!;
        public DbSet<DispatchRecord> DispatchRecords { get; set; } = null!;
        public DbSet<ReturnHandoverBatch> ReturnHandoverBatches { get; set; } = null!;

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

                // Cấu hình các trường nghiệp vụ Hàng Hoàn & Giao thất bại (RTO)
                entity.Property(e => e.FailedDeliveryCount).HasDefaultValue(0);
                entity.Property(e => e.FailureReason).HasMaxLength(255);
                entity.Property(e => e.FailureTimestamp);
                entity.Property(e => e.RtoTrackingCode).HasMaxLength(50);
                entity.Property(e => e.RtoLocationCode).HasMaxLength(50);
                entity.Property(e => e.RtoApprovedDate);
                entity.Property(e => e.ReturnShippingFee).HasPrecision(18, 2).HasDefaultValue(10000);
                entity.Property(e => e.ReturnHandoverBatchCode).HasMaxLength(50);

                // Bỏ qua các thuộc tính UI tính toán và trường hiển thị không cần lưu DB
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
                entity.Property(e => e.SenderPhone).HasMaxLength(20);
                entity.Property(e => e.SenderAddress).HasMaxLength(255);
                entity.Property(e => e.WaybillNumber).HasMaxLength(50);
                entity.Property(e => e.VehiclePlate).HasColumnName("VehicleNumber").HasMaxLength(50);
                entity.Property(e => e.DriverName).HasMaxLength(100);
                entity.Property(e => e.TotalWeight);
                entity.Property(e => e.TotalValue).HasPrecision(18, 2);
                entity.Property(e => e.CreatedByName).HasMaxLength(100);
                entity.Property(e => e.ApprovedDate);
                entity.Property(e => e.ApprovedByName).HasMaxLength(100);
                entity.Property(e => e.Notes).HasMaxLength(500);

                // Bỏ qua các trường UI tính toán
                entity.Ignore(e => e.Details);
                entity.Ignore(e => e.TotalExpectedQuantity);
                entity.Ignore(e => e.TotalActualQuantity);
                entity.Ignore(e => e.SourceTypeName);
                entity.Ignore(e => e.SourceTypeBadgeColor);
                entity.Ignore(e => e.StatusDisplayName);
                entity.Ignore(e => e.StatusColor);
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
                entity.Property(e => e.CitizenId).HasMaxLength(20);
                entity.Property(e => e.VehicleType).HasMaxLength(50);
                entity.Property(e => e.VehiclePlate).HasColumnName("LicensePlate").HasMaxLength(20);
                entity.Property(e => e.DeliveryArea).HasColumnName("CurrentArea").HasMaxLength(100);
                entity.Property(e => e.CompletedTodayCount).HasColumnName("CompletedOrdersToday");
                entity.Property(e => e.IsLocked).HasDefaultValue(false);
                entity.Property(e => e.WorkShift).HasMaxLength(50);
                entity.Property(e => e.MaxOrdersPerDay).HasDefaultValue(25);
                entity.Property(e => e.MaxWeightCapacity).HasDefaultValue(50.0);
                entity.Property(e => e.Rating);

                // Bỏ qua alias và các trường mở rộng trong bộ nhớ
                entity.Ignore(e => e.PhoneNumber);
                entity.Ignore(e => e.LicensePlate);
                entity.Ignore(e => e.CurrentArea);
                entity.Ignore(e => e.CompletedOrdersToday);
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
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
                entity.Property(e => e.Role).HasConversion<string>(); // Lưu enum dạng text trong SQL
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedAt);
                entity.Property(e => e.LastLoginAt);

                // Bỏ qua các thuộc tính UI và quyền mở rộng
                entity.Ignore(e => e.CustomPermissions);
                entity.Ignore(e => e.RoleDisplayName);
                entity.Ignore(e => e.RoleBadgeColor);
                entity.Ignore(e => e.RoleDescription);
            });

            // =========================================================================
            // 6. CẤU HÌNH BẢNG WarehouseLocations (Vị trí ô kệ kho bãi)
            // =========================================================================
            modelBuilder.Entity<WarehouseLocation>(entity =>
            {
                entity.ToTable("WarehouseLocations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LocationCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.WarehouseName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Zone).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Aisle).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Rack).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Shelf).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Bin).HasMaxLength(20).IsRequired();
                entity.Property(e => e.MaxWeightCapacity).HasDefaultValue(1000.0);
                entity.Property(e => e.CurrentWeight).HasDefaultValue(0.0);
                entity.Property(e => e.MaxVolumeCapacity).HasDefaultValue(5.0);
                entity.Property(e => e.Status).HasDefaultValue(LocationStatus.Empty);

                // Bỏ qua các trường UI tính toán
                entity.Ignore(e => e.OccupancyPercentage);
                entity.Ignore(e => e.StatusDisplayName);
                entity.Ignore(e => e.StatusColor);
            });

            // =========================================================================
            // 7. CẤU HÌNH BẢNG WarehouseMovements (Biến động nhập/xuất/điều chuyển kho)
            // =========================================================================
            modelBuilder.Entity<WarehouseMovement>(entity =>
            {
                entity.ToTable("WarehouseMovements");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TransactionCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Timestamp);
                entity.Property(e => e.MovementType);
                entity.Property(e => e.ItemName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.ReferenceCode).HasMaxLength(100);
                entity.Property(e => e.Quantity).HasDefaultValue(1);
                entity.Property(e => e.Weight).HasDefaultValue(0.0);
                entity.Property(e => e.SourceOrDestination).HasMaxLength(255);
                entity.Property(e => e.LocationCode).HasMaxLength(50);
                entity.Property(e => e.OperatorName).HasMaxLength(100);
                entity.Property(e => e.Notes).HasMaxLength(500);

                // Bỏ qua các trường UI
                entity.Ignore(e => e.MovementTypeDisplayName);
                entity.Ignore(e => e.MovementTypeColor);
            });

            // =========================================================================
            // 8. CẤU HÌNH BẢNG DispatchRecords (Phiên điều phối giao hàng)
            // =========================================================================
            modelBuilder.Entity<DispatchRecord>(entity =>
            {
                entity.ToTable("DispatchRecords");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DispatchCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.DispatchDate);
                entity.Property(e => e.CreatedTime);
                entity.Property(e => e.ShipperId);
                entity.Property(e => e.ShipperName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ShipperPhone).HasMaxLength(20);
                entity.Property(e => e.VehiclePlate).HasMaxLength(20);
                entity.Property(e => e.DeliveryArea).HasMaxLength(100);
                entity.Property(e => e.TotalOrders);
                entity.Property(e => e.ExpressOrdersCount);
                entity.Property(e => e.TotalCodAmount).HasPrecision(18, 2);
                entity.Property(e => e.TotalWeight);
                entity.Property(e => e.DispatcherName).HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.Notes).HasMaxLength(500);

                // Chuyển đổi danh sách OrderIds thành chuỗi lưu trong SQL Server
                entity.Property(e => e.OrderIds)
                    .HasConversion(
                        v => string.Join(",", v),
                        v => string.IsNullOrWhiteSpace(v) 
                            ? new List<int>() 
                            : v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                    );

                entity.Ignore(e => e.StatusColor);
            });

            // =========================================================================
            // 9. CẤU HÌNH BẢNG ReturnHandoverBatches (Biên bản bàn giao hàng hoàn trả Shop)
            // =========================================================================
            modelBuilder.Entity<ReturnHandoverBatch>(entity =>
            {
                entity.ToTable("ReturnHandoverBatches");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BatchCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.SenderName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.SenderPhone).HasMaxLength(20);
                entity.Property(e => e.SenderAddress).HasMaxLength(255);
                entity.Property(e => e.CreatedTime);
                entity.Property(e => e.TotalOrders);
                entity.Property(e => e.TotalCodValue).HasPrecision(18, 2);
                entity.Property(e => e.TotalReturnFee).HasPrecision(18, 2);
                entity.Property(e => e.OperatorName).HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.Notes).HasMaxLength(500);

                // Chuyển đổi danh sách OrderIds thành chuỗi lưu trong SQL Server
                entity.Property(e => e.OrderIds)
                    .HasConversion(
                        v => string.Join(",", v),
                        v => string.IsNullOrWhiteSpace(v) 
                            ? new List<int>() 
                            : v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                    );

                entity.Ignore(e => e.StatusColor);
            });
        }
    }
}
