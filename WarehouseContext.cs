using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// Ngữ cảnh dữ liệu kho hàng Logistics (Data Context).
    /// Quản lý dữ liệu tập trung, kết nối ORM trực tiếp qua Entity Framework Core 10 (WarehouseDbContext)
    /// tới SQL Server (Database: quanlykho), kết hợp bộ nhớ đệm Thread-Safe.
    /// </summary>
    public class WarehouseContext
    {
        private static readonly Lazy<WarehouseContext> _instance = new(() => new WarehouseContext());
        public static WarehouseContext Instance => _instance.Value;

        /// <summary>
        /// Chuỗi kết nối tới cơ sở dữ liệu SQL Server trên máy cục bộ
        /// </summary>
        public string ConnectionString
        {
            get => WarehouseDbContext.DefaultConnectionString;
            set => WarehouseDbContext.DefaultConnectionString = value;
        }

        /// <summary>
        /// Trạng thái kết nối cơ sở dữ liệu SQL Server thực tế
        /// </summary>
        public bool IsDatabaseConnected { get; private set; } = false;

        public string ConnectedDatabaseName { get; private set; } = "quanlykho";
        public string ConnectionStatusMessage { get; private set; } = string.Empty;

        private readonly List<User> _users = new();
        private readonly List<WarehouseLocation> _locations = new();
        private readonly List<ImportOrder> _importOrders = new();
        private readonly List<ShippingOrder> _shippingOrders = new();
        private readonly List<Shipper> _shippers = new();
        private readonly List<RecentActivity> _recentActivities = new();
        private readonly List<WarehouseMovement> _warehouseMovements = new();
        private readonly List<DispatchRecord> _dispatchRecords = new();
        private readonly List<ReturnHandoverBatch> _returnBatches = new();

        private readonly object _lock = new();

        public WarehouseContext()
        {
            SeedInitialUsers();
            SeedLogisticsData();

            // Khởi tạo và đồng bộ với SQL Server database
            InitializeDatabase();
        }

        #region Khởi tạo & Đồng bộ CSDL SQL Server qua Entity Framework Core (EF Core)
        /// <summary>
        /// Kết nối SQL Server qua Entity Framework Core (WarehouseDbContext),
        /// đảm bảo các bảng cần thiết tồn tại và đồng bộ dữ liệu vào bộ nhớ.
        /// </summary>
        public void InitializeDatabase()
        {
            try
            {
                using var db = new WarehouseDbContext();
                
                // Kiểm tra khả năng kết nối tới SQL Server
                if (!db.Database.CanConnect())
                {
                    throw new Exception("Không thể kết nối đến máy chủ SQL Server quanlykho.");
                }

                IsDatabaseConnected = true;
                ConnectionStatusMessage = "Kết nối SQL Server thành công qua EF Core (Database: quanlykho)";

                // 1. Đảm bảo cấu trúc các bảng tồn tại (DDL)
                EnsureTablesExist(db);

                // 2. Đồng bộ người dùng từ DbSet<User>
                SyncUsersFromDatabase(db);

                // 3. Đồng bộ danh sách đơn vận chuyển từ DbSet<ShippingOrder>
                SyncShippingOrdersFromDatabase(db);

                // 4. Đồng bộ danh sách phiếu nhập kho từ DbSet<ImportOrder>
                SyncImportOrdersFromDatabase(db);

                // 5. Đồng bộ danh sách shipper từ DbSet<Shipper>
                SyncShippersFromDatabase(db);

                // 6. Đồng bộ nhật ký hoạt động từ DbSet<RecentActivity>
                SyncActivitiesFromDatabase(db);

                // 7. Đồng bộ danh sách vị trí kho bãi từ DbSet<WarehouseLocation>
                SyncLocationsFromDatabase(db);

                // 8. Đồng bộ nhật ký biến động kho từ DbSet<WarehouseMovement>
                SyncMovementsFromDatabase(db);

                // 9. Đồng bộ danh sách phiên điều phối từ DbSet<DispatchRecord>
                SyncDispatchRecordsFromDatabase(db);

                // 10. Đồng bộ danh sách biên bản bàn giao hoàn trả từ DbSet<ReturnHandoverBatch>
                SyncReturnBatchesFromDatabase(db);
            }
            catch (Exception ex)
            {
                IsDatabaseConnected = false;
                ConnectionStatusMessage = $"Không thể kết nối SQL Server (EF Core): {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[WarehouseContext EF Core Error] {ex.Message}");
            }
        }

        private void EnsureTablesExist(WarehouseDbContext db)
        {
            const string ddl = @"
            -- 1. Bảng ShippingOrders
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ShippingOrders')
            BEGIN
                CREATE TABLE ShippingOrders (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    OrderCode NVARCHAR(50) NOT NULL,
                    SenderName NVARCHAR(100) NOT NULL,
                    SenderPhone NVARCHAR(20) NOT NULL,
                    SenderAddress NVARCHAR(255) NULL,
                    ReceiverName NVARCHAR(100) NOT NULL,
                    ReceiverPhone NVARCHAR(20) NOT NULL,
                    ReceiverAddress NVARCHAR(255) NOT NULL,
                    DestinationArea NVARCHAR(100) NOT NULL,
                    ProductSummary NVARCHAR(255) NOT NULL,
                    Weight FLOAT NOT NULL DEFAULT 1.0,
                    IsExpress BIT NOT NULL DEFAULT 0,
                    CodAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    ShippingFee DECIMAL(18,2) NOT NULL DEFAULT 20000,
                    ExpressSurcharge DECIMAL(18,2) NOT NULL DEFAULT 0,
                    ReceiverPaysFee BIT NOT NULL DEFAULT 1,
                    Status INT NOT NULL DEFAULT 0,
                    AssignedShipperId INT NULL,
                    AssignedShipperName NVARCHAR(100) NULL,
                    ShipperPhone NVARCHAR(20) NULL,
                    CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
                    EstimatedDeliveryDate DATETIME2 NOT NULL DEFAULT GETDATE(),
                    DeliveredDate DATETIME2 NULL,
                    Notes NVARCHAR(500) NULL,
                    FailedDeliveryCount INT NOT NULL DEFAULT 0,
                    FailureReason NVARCHAR(255) NULL,
                    FailureTimestamp DATETIME2 NULL,
                    RtoTrackingCode NVARCHAR(50) NULL,
                    RtoLocationCode NVARCHAR(50) NULL,
                    RtoApprovedDate DATETIME2 NULL,
                    ReturnShippingFee DECIMAL(18,2) NOT NULL DEFAULT 10000,
                    ReturnHandoverBatchCode NVARCHAR(50) NULL
                );
            END
            ELSE
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ShippingOrders') AND name = 'FailedDeliveryCount')
                    ALTER TABLE ShippingOrders ADD FailedDeliveryCount INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ShippingOrders') AND name = 'FailureReason')
                    ALTER TABLE ShippingOrders ADD FailureReason NVARCHAR(255) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ShippingOrders') AND name = 'FailureTimestamp')
                    ALTER TABLE ShippingOrders ADD FailureTimestamp DATETIME2 NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ShippingOrders') AND name = 'RtoTrackingCode')
                    ALTER TABLE ShippingOrders ADD RtoTrackingCode NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ShippingOrders') AND name = 'RtoLocationCode')
                    ALTER TABLE ShippingOrders ADD RtoLocationCode NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ShippingOrders') AND name = 'RtoApprovedDate')
                    ALTER TABLE ShippingOrders ADD RtoApprovedDate DATETIME2 NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ShippingOrders') AND name = 'ReturnShippingFee')
                    ALTER TABLE ShippingOrders ADD ReturnShippingFee DECIMAL(18,2) NOT NULL DEFAULT 10000;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ShippingOrders') AND name = 'ReturnHandoverBatchCode')
                    ALTER TABLE ShippingOrders ADD ReturnHandoverBatchCode NVARCHAR(50) NULL;
            END

            -- 2. Bảng ImportOrders
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImportOrders')
            BEGIN
                CREATE TABLE ImportOrders (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ImportCode NVARCHAR(50) NOT NULL,
                    SourceType INT NOT NULL DEFAULT 0,
                    SourceTypeName NVARCHAR(100) NULL,
                    SenderName NVARCHAR(100) NOT NULL,
                    SenderPhone NVARCHAR(20) NULL,
                    SenderAddress NVARCHAR(255) NULL,
                    WaybillNumber NVARCHAR(50) NULL,
                    VehicleNumber NVARCHAR(50) NULL,
                    DriverName NVARCHAR(100) NULL,
                    TotalWeight FLOAT NOT NULL DEFAULT 0,
                    TotalValue DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Status INT NOT NULL DEFAULT 0,
                    StatusDisplayName NVARCHAR(100) NULL,
                    CreatedByName NVARCHAR(100) NULL,
                    ApprovedDate DATETIME2 NULL,
                    ApprovedByName NVARCHAR(100) NULL,
                    Notes NVARCHAR(500) NULL,
                    CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END
            ELSE
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ImportOrders') AND name = 'SenderPhone')
                    ALTER TABLE ImportOrders ADD SenderPhone NVARCHAR(20) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ImportOrders') AND name = 'SenderAddress')
                    ALTER TABLE ImportOrders ADD SenderAddress NVARCHAR(255) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ImportOrders') AND name = 'DriverName')
                    ALTER TABLE ImportOrders ADD DriverName NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ImportOrders') AND name = 'TotalValue')
                    ALTER TABLE ImportOrders ADD TotalValue DECIMAL(18,2) NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ImportOrders') AND name = 'CreatedByName')
                    ALTER TABLE ImportOrders ADD CreatedByName NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ImportOrders') AND name = 'ApprovedDate')
                    ALTER TABLE ImportOrders ADD ApprovedDate DATETIME2 NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ImportOrders') AND name = 'ApprovedByName')
                    ALTER TABLE ImportOrders ADD ApprovedByName NVARCHAR(100) NULL;
            END

            -- 3. Bảng Shippers
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Shippers')
            BEGIN
                CREATE TABLE Shippers (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    FullName NVARCHAR(100) NOT NULL,
                    PhoneNumber NVARCHAR(20) NOT NULL,
                    CitizenId NVARCHAR(20) NULL,
                    VehicleType NVARCHAR(50) NOT NULL,
                    LicensePlate NVARCHAR(20) NOT NULL,
                    CurrentArea NVARCHAR(100) NOT NULL,
                    Status INT NOT NULL DEFAULT 0,
                    IsLocked BIT NOT NULL DEFAULT 0,
                    WorkShift NVARCHAR(50) NULL,
                    MaxOrdersPerDay INT NOT NULL DEFAULT 25,
                    MaxWeightCapacity FLOAT NOT NULL DEFAULT 50.0,
                    CompletedOrdersToday INT NOT NULL DEFAULT 0,
                    Rating FLOAT NOT NULL DEFAULT 5.0
                );
            END
            ELSE
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Shippers') AND name = 'CitizenId')
                    ALTER TABLE Shippers ADD CitizenId NVARCHAR(20) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Shippers') AND name = 'IsLocked')
                    ALTER TABLE Shippers ADD IsLocked BIT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Shippers') AND name = 'WorkShift')
                    ALTER TABLE Shippers ADD WorkShift NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Shippers') AND name = 'MaxOrdersPerDay')
                    ALTER TABLE Shippers ADD MaxOrdersPerDay INT NOT NULL DEFAULT 25;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Shippers') AND name = 'MaxWeightCapacity')
                    ALTER TABLE Shippers ADD MaxWeightCapacity FLOAT NOT NULL DEFAULT 50.0;
            END

            -- 4. Bảng RecentActivities
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecentActivities')
            BEGIN
                CREATE TABLE RecentActivities (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Title NVARCHAR(255) NOT NULL,
                    Description NVARCHAR(500) NOT NULL,
                    Timestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
                    Icon NVARCHAR(50) NULL,
                    Category NVARCHAR(50) NULL
                );
            END

            -- 5. Bảng _users
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '_users')
            BEGIN
                CREATE TABLE _users (
                    UserId INT IDENTITY(1,1) PRIMARY KEY,
                    Username NVARCHAR(50) NOT NULL,
                    Password NVARCHAR(255) NOT NULL,
                    FullName NVARCHAR(100) NULL,
                    Email NVARCHAR(100) NULL,
                    PhoneNumber NVARCHAR(20) NULL,
                    Role NVARCHAR(50) NOT NULL DEFAULT 'Staff',
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                    LastLoginAt DATETIME2 NULL
                );
            END
            ELSE
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('_users') AND name = 'Email')
                    ALTER TABLE _users ADD Email NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('_users') AND name = 'PhoneNumber')
                    ALTER TABLE _users ADD PhoneNumber NVARCHAR(20) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('_users') AND name = 'LastLoginAt')
                    ALTER TABLE _users ADD LastLoginAt DATETIME2 NULL;
            END

            -- 6. Bảng WarehouseLocations
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WarehouseLocations')
            BEGIN
                CREATE TABLE WarehouseLocations (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    LocationCode NVARCHAR(50) NOT NULL,
                    WarehouseName NVARCHAR(100) NOT NULL,
                    Zone NVARCHAR(100) NOT NULL,
                    Aisle NVARCHAR(20) NOT NULL,
                    Rack NVARCHAR(20) NOT NULL,
                    Shelf NVARCHAR(20) NOT NULL,
                    Bin NVARCHAR(20) NOT NULL,
                    MaxWeightCapacity FLOAT NOT NULL DEFAULT 1000.0,
                    CurrentWeight FLOAT NOT NULL DEFAULT 0.0,
                    MaxVolumeCapacity FLOAT NOT NULL DEFAULT 5.0,
                    Status INT NOT NULL DEFAULT 0
                );
            END

            -- 7. Bảng WarehouseMovements
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WarehouseMovements')
            BEGIN
                CREATE TABLE WarehouseMovements (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TransactionCode NVARCHAR(50) NOT NULL,
                    Timestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
                    MovementType INT NOT NULL DEFAULT 0,
                    ItemName NVARCHAR(255) NOT NULL,
                    ReferenceCode NVARCHAR(100) NULL,
                    Quantity INT NOT NULL DEFAULT 1,
                    Weight FLOAT NOT NULL DEFAULT 0.0,
                    SourceOrDestination NVARCHAR(255) NULL,
                    LocationCode NVARCHAR(50) NULL,
                    OperatorName NVARCHAR(100) NULL,
                    Notes NVARCHAR(500) NULL
                );
            END

            -- 8. Bảng DispatchRecords
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DispatchRecords')
            BEGIN
                CREATE TABLE DispatchRecords (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    DispatchCode NVARCHAR(50) NOT NULL,
                    DispatchDate DATETIME2 NOT NULL DEFAULT GETDATE(),
                    CreatedTime DATETIME2 NOT NULL DEFAULT GETDATE(),
                    ShipperId INT NOT NULL,
                    ShipperName NVARCHAR(100) NOT NULL,
                    ShipperPhone NVARCHAR(20) NULL,
                    VehiclePlate NVARCHAR(20) NULL,
                    DeliveryArea NVARCHAR(100) NULL,
                    TotalOrders INT NOT NULL DEFAULT 0,
                    ExpressOrdersCount INT NOT NULL DEFAULT 0,
                    TotalCodAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalWeight FLOAT NOT NULL DEFAULT 0.0,
                    DispatcherName NVARCHAR(100) NULL,
                    Status NVARCHAR(50) NOT NULL DEFAULT N'Đang Đi Giao',
                    Notes NVARCHAR(500) NULL,
                    OrderIds NVARCHAR(MAX) NULL
                );
            END

            -- 9. Bảng ReturnHandoverBatches
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ReturnHandoverBatches')
            BEGIN
                CREATE TABLE ReturnHandoverBatches (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    BatchCode NVARCHAR(50) NOT NULL,
                    SenderName NVARCHAR(100) NOT NULL,
                    SenderPhone NVARCHAR(20) NULL,
                    SenderAddress NVARCHAR(255) NULL,
                    CreatedTime DATETIME2 NOT NULL DEFAULT GETDATE(),
                    TotalOrders INT NOT NULL DEFAULT 0,
                    TotalCodValue DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalReturnFee DECIMAL(18,2) NOT NULL DEFAULT 0,
                    OperatorName NVARCHAR(100) NULL,
                    Status NVARCHAR(50) NOT NULL DEFAULT N'Đang Lưu Kho',
                    Notes NVARCHAR(500) NULL,
                    OrderIds NVARCHAR(MAX) NULL
                );
            END";

            db.Database.ExecuteSqlRaw(ddl);
        }

        public static string ChuanHoaHoTenNguoiDung(string? hoTen)
        {
            if (string.IsNullOrWhiteSpace(hoTen)) return string.Empty;

            // Khắc phục triệt để lỗi hiển thị font tiếng Việt (Mojibake UTF-8)
            if (hoTen.Contains("Quáº£n") || hoTen.Contains("Trá»‹") || hoTen.Contains("ViÃªn") || hoTen.Contains("Há»‡"))
                return "Quản Trị Viên Hệ Thống";
            if (hoTen.Contains("Tráº§n") || hoTen.Contains("VÄƒn") || hoTen.Contains("Quáº£n LÃ½") || hoTen.Contains("LÃ½ Kho"))
                return "Trần Văn Quản Lý Kho";
            if (hoTen.Contains("LÃª") || hoTen.Contains("Thá»‹") || hoTen.Contains("Váº­n") || hoTen.Contains("HÃ nh Kho") || hoTen.Contains("Vâºn"))
                return "Lê Thị Vận Hành Kho";

            return hoTen;
        }

        private void SyncUsersFromDatabase(WarehouseDbContext db)
        {
            try
            {
                var dbUsers = db.Users.ToList();
                bool coCapNhatDb = false;

                lock (_lock)
                {
                    foreach (var user in dbUsers)
                    {
                        if (string.IsNullOrWhiteSpace(user.Username)) continue;

                        // Chuẩn hóa họ tên tránh lỗi font
                        string tenChuanHoa = ChuanHoaHoTenNguoiDung(user.FullName);
                        if (!string.Equals(user.FullName, tenChuanHoa, StringComparison.Ordinal))
                        {
                            user.FullName = tenChuanHoa;
                            coCapNhatDb = true;
                        }

                        var existing = _users.FirstOrDefault(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase));
                        if (existing == null)
                        {
                            _users.Add(user);
                        }
                        else
                        {
                            existing.Id = user.Id;
                            existing.PasswordHash = user.PasswordHash;
                            existing.FullName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : existing.FullName;
                            existing.Email = !string.IsNullOrWhiteSpace(user.Email) ? user.Email : existing.Email;
                            existing.PhoneNumber = !string.IsNullOrWhiteSpace(user.PhoneNumber) ? user.PhoneNumber : existing.PhoneNumber;
                            existing.Role = user.Role;
                            existing.IsActive = user.IsActive;
                            existing.CreatedAt = user.CreatedAt;
                            existing.LastLoginAt = user.LastLoginAt;
                        }
                    }
                }

                if (coCapNhatDb)
                {
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncUsers EF Core Error] {ex.Message}");
            }
        }

        private void SyncShippingOrdersFromDatabase(WarehouseDbContext db)
        {
            try
            {
                int count = db.ShippingOrders.Count();
                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var o in _shippingOrders)
                        {
                            db.ShippingOrders.Add(new ShippingOrder
                            {
                                OrderCode = o.OrderCode,
                                SenderName = o.SenderName,
                                SenderPhone = o.SenderPhone,
                                SenderAddress = o.SenderAddress,
                                ReceiverName = o.ReceiverName,
                                ReceiverPhone = o.ReceiverPhone,
                                ReceiverAddress = o.ReceiverAddress,
                                DestinationArea = o.DestinationArea,
                                ProductSummary = o.ProductSummary,
                                Weight = o.Weight,
                                IsExpress = o.IsExpress,
                                CodAmount = o.CodAmount,
                                ShippingFee = o.ShippingFee,
                                ExpressSurcharge = o.ExpressSurcharge,
                                ReceiverPaysFee = o.ReceiverPaysFee,
                                Status = o.Status,
                                AssignedShipperId = o.AssignedShipperId,
                                AssignedShipperName = o.AssignedShipperName,
                                ShipperPhone = o.ShipperPhone,
                                CreatedDate = o.CreatedDate,
                                EstimatedDeliveryDate = o.EstimatedDeliveryDate,
                                DeliveredDate = o.DeliveredDate,
                                Notes = o.Notes,
                                FailedDeliveryCount = o.FailedDeliveryCount,
                                FailureReason = o.FailureReason,
                                FailureTimestamp = o.FailureTimestamp,
                                RtoTrackingCode = o.RtoTrackingCode,
                                RtoLocationCode = o.RtoLocationCode,
                                RtoApprovedDate = o.RtoApprovedDate,
                                ReturnShippingFee = o.ReturnShippingFee,
                                ReturnHandoverBatchCode = o.ReturnHandoverBatchCode
                            });
                        }
                        db.SaveChanges();
                    }
                }
                else
                {
                    var dbOrders = db.ShippingOrders.AsNoTracking()
                        .OrderByDescending(o => o.CreatedDate)
                        .ToList();

                    lock (_lock)
                    {
                        _shippingOrders.Clear();
                        _shippingOrders.AddRange(dbOrders);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncShippingOrders EF Core Error] {ex.Message}");
            }
        }

        private void SyncImportOrdersFromDatabase(WarehouseDbContext db)
        {
            try
            {
                int count = db.ImportOrders.Count();
                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var o in _importOrders)
                        {
                            db.ImportOrders.Add(new ImportOrder
                            {
                                ImportCode = o.ImportCode,
                                SourceType = o.SourceType,
                                SenderName = o.SenderName,
                                SenderPhone = o.SenderPhone,
                                SenderAddress = o.SenderAddress,
                                WaybillNumber = o.WaybillNumber,
                                VehiclePlate = o.VehiclePlate,
                                DriverName = o.DriverName,
                                TotalWeight = o.TotalWeight,
                                TotalValue = o.TotalValue,
                                Status = o.Status,
                                CreatedByName = o.CreatedByName,
                                ApprovedDate = o.ApprovedDate,
                                ApprovedByName = o.ApprovedByName,
                                Notes = o.Notes,
                                CreatedDate = o.CreatedDate
                            });
                        }
                        db.SaveChanges();
                    }
                }
                else
                {
                    var dbOrders = db.ImportOrders.AsNoTracking()
                        .OrderByDescending(o => o.CreatedDate)
                        .ToList();

                    lock (_lock)
                    {
                        _importOrders.Clear();
                        _importOrders.AddRange(dbOrders);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncImportOrders EF Core Error] {ex.Message}");
            }
        }

        private void SyncShippersFromDatabase(WarehouseDbContext db)
        {
            try
            {
                int count = db.Shippers.Count();
                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var s in _shippers)
                        {
                            db.Shippers.Add(new Shipper
                            {
                                FullName = s.FullName,
                                Phone = s.Phone,
                                CitizenId = s.CitizenId,
                                VehicleType = s.VehicleType,
                                VehiclePlate = s.VehiclePlate,
                                DeliveryArea = s.DeliveryArea,
                                Status = s.Status,
                                IsLocked = s.IsLocked,
                                WorkShift = s.WorkShift,
                                MaxOrdersPerDay = s.MaxOrdersPerDay,
                                MaxWeightCapacity = s.MaxWeightCapacity,
                                CompletedTodayCount = s.CompletedTodayCount,
                                Rating = s.Rating
                            });
                        }
                        db.SaveChanges();
                    }
                }

                var dbShippers = db.Shippers.AsNoTracking()
                    .OrderBy(s => s.Id)
                    .ToList();

                if (dbShippers.Count > 0)
                {
                    lock (_lock)
                    {
                        _shippers.Clear();
                        _shippers.AddRange(dbShippers);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncShippers EF Core Error] {ex.Message}");
            }
        }

        private void SyncActivitiesFromDatabase(WarehouseDbContext db)
        {
            try
            {
                int count = db.RecentActivities.Count();
                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var a in _recentActivities)
                        {
                            db.RecentActivities.Add(new RecentActivity
                            {
                                Title = a.Title,
                                Description = a.Description,
                                Timestamp = a.Timestamp,
                                Type = a.Type
                            });
                        }
                        db.SaveChanges();
                    }
                }

                var dbActivities = db.RecentActivities.AsNoTracking()
                    .OrderByDescending(a => a.Timestamp)
                    .Take(50)
                    .ToList();

                if (dbActivities.Count > 0)
                {
                    lock (_lock)
                    {
                        _recentActivities.Clear();
                        _recentActivities.AddRange(dbActivities);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncActivities EF Core Error] {ex.Message}");
            }
        }

        private void SyncLocationsFromDatabase(WarehouseDbContext db)
        {
            try
            {
                int count = db.WarehouseLocations.Count();
                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var loc in _locations)
                        {
                            db.WarehouseLocations.Add(new WarehouseLocation
                            {
                                LocationCode = loc.LocationCode,
                                WarehouseName = loc.WarehouseName,
                                Zone = loc.Zone,
                                Aisle = loc.Aisle,
                                Rack = loc.Rack,
                                Shelf = loc.Shelf,
                                Bin = loc.Bin,
                                MaxWeightCapacity = loc.MaxWeightCapacity,
                                CurrentWeight = loc.CurrentWeight,
                                MaxVolumeCapacity = loc.MaxVolumeCapacity,
                                Status = loc.Status
                            });
                        }
                        db.SaveChanges();
                    }
                }
                else
                {
                    var dbLocations = db.WarehouseLocations.AsNoTracking().OrderBy(l => l.Id).ToList();
                    lock (_lock)
                    {
                        _locations.Clear();
                        _locations.AddRange(dbLocations);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncLocations EF Core Error] {ex.Message}");
            }
        }

        private void SyncMovementsFromDatabase(WarehouseDbContext db)
        {
            try
            {
                int count = db.WarehouseMovements.Count();
                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var m in _warehouseMovements)
                        {
                            db.WarehouseMovements.Add(new WarehouseMovement
                            {
                                TransactionCode = m.TransactionCode,
                                Timestamp = m.Timestamp,
                                MovementType = m.MovementType,
                                ItemName = m.ItemName,
                                ReferenceCode = m.ReferenceCode,
                                Quantity = m.Quantity,
                                Weight = m.Weight,
                                SourceOrDestination = m.SourceOrDestination,
                                LocationCode = m.LocationCode,
                                OperatorName = m.OperatorName,
                                Notes = m.Notes
                            });
                        }
                        db.SaveChanges();
                    }
                }
                else
                {
                    var dbMovements = db.WarehouseMovements.AsNoTracking().OrderByDescending(m => m.Timestamp).ToList();
                    lock (_lock)
                    {
                        _warehouseMovements.Clear();
                        _warehouseMovements.AddRange(dbMovements);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncMovements EF Core Error] {ex.Message}");
            }
        }

        private void SyncDispatchRecordsFromDatabase(WarehouseDbContext db)
        {
            try
            {
                int count = db.DispatchRecords.Count();
                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var d in _dispatchRecords)
                        {
                            db.DispatchRecords.Add(new DispatchRecord
                            {
                                DispatchCode = d.DispatchCode,
                                DispatchDate = d.DispatchDate,
                                CreatedTime = d.CreatedTime,
                                ShipperId = d.ShipperId,
                                ShipperName = d.ShipperName,
                                ShipperPhone = d.ShipperPhone,
                                VehiclePlate = d.VehiclePlate,
                                DeliveryArea = d.DeliveryArea,
                                TotalOrders = d.TotalOrders,
                                ExpressOrdersCount = d.ExpressOrdersCount,
                                TotalCodAmount = d.TotalCodAmount,
                                TotalWeight = d.TotalWeight,
                                DispatcherName = d.DispatcherName,
                                Status = d.Status,
                                Notes = d.Notes,
                                OrderIds = d.OrderIds.ToList()
                            });
                        }
                        db.SaveChanges();
                    }
                }
                else
                {
                    var dbDispatches = db.DispatchRecords.AsNoTracking().OrderByDescending(d => d.CreatedTime).ToList();
                    lock (_lock)
                    {
                        _dispatchRecords.Clear();
                        _dispatchRecords.AddRange(dbDispatches);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncDispatchRecords EF Core Error] {ex.Message}");
            }
        }

        private void SyncReturnBatchesFromDatabase(WarehouseDbContext db)
        {
            try
            {
                int count = db.ReturnHandoverBatches.Count();
                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var b in _returnBatches)
                        {
                            db.ReturnHandoverBatches.Add(new ReturnHandoverBatch
                            {
                                BatchCode = b.BatchCode,
                                SenderName = b.SenderName,
                                SenderPhone = b.SenderPhone,
                                SenderAddress = b.SenderAddress,
                                CreatedTime = b.CreatedTime,
                                TotalOrders = b.TotalOrders,
                                TotalCodValue = b.TotalCodValue,
                                TotalReturnFee = b.TotalReturnFee,
                                OperatorName = b.OperatorName,
                                Status = b.Status,
                                Notes = b.Notes,
                                OrderIds = b.OrderIds.ToList()
                            });
                        }
                        db.SaveChanges();
                    }
                }
                else
                {
                    var dbBatches = db.ReturnHandoverBatches.AsNoTracking().OrderByDescending(b => b.CreatedTime).ToList();
                    lock (_lock)
                    {
                        _returnBatches.Clear();
                        _returnBatches.AddRange(dbBatches);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncReturnBatches EF Core Error] {ex.Message}");
            }
        }
        #endregion

        #region Khởi tạo dữ liệu mẫu ban đầu
        private void SeedInitialUsers()
        {
            _users.AddRange(new[]
            {
                new User
                {
                    Id = 1,
                    Username = "a",
                    PasswordHash = User.HashPassword("1"),
                    FullName = "Hầu Tuấn Đạt",
                    Email = "hautuandat@logixwarehouse.vn",
                    PhoneNumber = "0988 888 888",
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.Now.AddMonths(-6)
                },
                new User
                {
                    Id = 2,
                    Username = "admin",
                    PasswordHash = User.HashPassword("admin123"),
                    FullName = "Quản Trị Viên Hệ Thống",
                    Email = "admin@logixwarehouse.vn",
                    PhoneNumber = "0901 234 567",
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.Now.AddMonths(-6)
                },
                new User
                {
                    Id = 3,
                    Username = "quanly",
                    PasswordHash = User.HashPassword("quanly123"),
                    FullName = "Trần Văn Quản Lý Kho",
                    Email = "truongkho@logixwarehouse.vn",
                    PhoneNumber = "0912 345 678",
                    Role = UserRole.Manager,
                    IsActive = true,
                    CreatedAt = DateTime.Now.AddMonths(-3)
                },
                new User
                {
                    Id = 4,
                    Username = "nhanvien",
                    PasswordHash = User.HashPassword("nhanvien123"),
                    FullName = "Lê Thị Vận Hành Kho",
                    Email = "nhanvien@logixwarehouse.vn",
                    PhoneNumber = "0987 654 321",
                    Role = UserRole.Staff,
                    IsActive = true,
                    CreatedAt = DateTime.Now.AddMonths(-1)
                },
                new User
                {
                    Id = 5,
                    Username = "khoakhoan",
                    PasswordHash = User.HashPassword("123456"),
                    FullName = "Tài Khoản Đã Khóa",
                    Email = "locked@logixwarehouse.vn",
                    PhoneNumber = "0900 000 000",
                    Role = UserRole.Staff,
                    IsActive = false,
                    CreatedAt = DateTime.Now.AddYears(-1)
                }
            });
        }

        private void SeedLogisticsData()
        {
            // 1. Vị trí kho bãi & Máng phân loại bưu kiện
            _locations.AddRange(new[]
            {
                new WarehouseLocation { Id = 1, LocationCode = "KHO-A-D01-K01", Zone = "Khu Lưu Bưu Kiện Tiêu Chuẩn", Aisle = "A", Rack = "01", Shelf = "1", Bin = "01", CurrentWeight = 450, MaxWeightCapacity = 1000, Status = LocationStatus.PartiallyFull },
                new WarehouseLocation { Id = 2, LocationCode = "KHO-A-D01-K02", Zone = "Khu Lưu Bưu Kiện Tiêu Chuẩn", Aisle = "A", Rack = "01", Shelf = "2", Bin = "01", CurrentWeight = 800, MaxWeightCapacity = 1000, Status = LocationStatus.PartiallyFull },
                new WarehouseLocation { Id = 3, LocationCode = "KHO-EXP-01", Zone = "Khu Bưu Kiện Hỏa Tốc (Express)", Aisle = "EXP", Rack = "01", Shelf = "1", Bin = "01", CurrentWeight = 120, MaxWeightCapacity = 500, Status = LocationStatus.PartiallyFull },
                new WarehouseLocation { Id = 4, LocationCode = "DOCK-INBOUND-01", Zone = "Khu Nhận Hàng Tiếp Nhận", Aisle = "IN", Rack = "01", Shelf = "1", Bin = "01", CurrentWeight = 950, MaxWeightCapacity = 2000, Status = LocationStatus.PartiallyFull }
            });

            // 4. Phiếu nhập kho ban đầu
            _importOrders.AddRange(new[]
            {
                new ImportOrder
                {
                    Id = 1,
                    ImportCode = "NK-HUB-260901",
                    SourceType = ImportSourceType.TransitHub,
                    SenderName = "Hub Trung Chuyển Liên Tỉnh Long Biên",
                    SenderPhone = "024 3829 1111",
                    SenderAddress = "Km 5, Đường Nguyễn Văn Linh, Long Biên, Hà Nội",
                    WaybillNumber = "TRANSIT-XE-29C-88912",
                    VehiclePlate = "29C-889.12",
                    DriverName = "Nguyễn Văn Bắc",
                    TotalWeight = 450.0,
                    TotalValue = 125000000,
                    Status = ImportOrderStatus.Approved,
                    CreatedDate = DateTime.Now.AddHours(-6),
                    CreatedByName = "Lê Văn Vận Hành",
                    Notes = "Xe tải 5 tấn, chì niêm phong nguyên vẹn (Seal #8812)"
                },
                new ImportOrder
                {
                    Id = 2,
                    ImportCode = "NK-B2B-260902",
                    SourceType = ImportSourceType.Company,
                    SenderName = "Công ty TNHH Phân Phối Gia Dụng Toàn Cầu",
                    SenderPhone = "028 3999 8888",
                    SenderAddress = "Khu Công Nghiệp Tân Bình, TP.HCM",
                    WaybillNumber = "PO-B2B-99812",
                    VehiclePlate = "51D-432.10",
                    DriverName = "Phạm Quốc Tuấn",
                    TotalWeight = 280.0,
                    TotalValue = 89000000,
                    Status = ImportOrderStatus.Inspecting,
                    CreatedDate = DateTime.Now.AddHours(-2),
                    CreatedByName = "Lê Văn Vận Hành",
                    Notes = "Lô hàng gia dụng theo hợp đồng quý 3"
                },
                new ImportOrder
                {
                    Id = 3,
                    ImportCode = "NK-SHOP-260903",
                    SourceType = ImportSourceType.Shop,
                    SenderName = "Shop Thời Trang GenZ Official (Shopee/TikTok)",
                    SenderPhone = "0944 555 666",
                    SenderAddress = "Số 12 Chùa Bộc, Đống Đa, Hà Nội",
                    WaybillNumber = "SHOP-FULFILL-019",
                    VehiclePlate = "29D-654.32",
                    DriverName = "Lê Hoàng Long",
                    TotalWeight = 35.0,
                    TotalValue = 18500000,
                    Status = ImportOrderStatus.PendingInspection,
                    CreatedDate = DateTime.Now.AddMinutes(-45),
                    CreatedByName = "Lê Văn Vận Hành",
                    Notes = "Gửi 150 sản phẩm lưu kho đóng gói hoàn tất đơn Shopee"
                },
                new ImportOrder
                {
                    Id = 4,
                    ImportCode = "NK-LE-260904",
                    SourceType = ImportSourceType.Individual,
                    SenderName = "Hoàng Thị Mai (Khách lẻ tại quầy)",
                    SenderPhone = "0977 112 233",
                    SenderAddress = "Số 56 Nguyễn Trãi, Thanh Xuân, Hà Nội",
                    WaybillNumber = "RETAIL-00412",
                    VehiclePlate = "Xe máy",
                    DriverName = "Tự mang đến",
                    TotalWeight = 1.2,
                    TotalValue = 850000,
                    Status = ImportOrderStatus.Approved,
                    CreatedDate = DateTime.Now.AddMinutes(-15),
                    CreatedByName = "Lê Văn Vận Hành",
                    Notes = "Gửi bưu kiện quà biếu chuyển phát nhanh vào Đà Nẵng"
                }
            });

            // 5. Shipper giao nhận
            _shippers.AddRange(new[]
            {
                new Shipper { Id = 1, FullName = "Nguyễn Văn Tuấn", PhoneNumber = "0981 111 222", VehicleType = "Xe máy Honda Wave", LicensePlate = "29H1-892.11", CurrentArea = "Đống Đa - Ba Đình", Status = ShipperStatus.Active, CompletedOrdersToday = 8, Rating = 4.9 },
                new Shipper { Id = 2, FullName = "Trần Đình Trọng", PhoneNumber = "0982 222 333", VehicleType = "Xe máy Yamaha Sirius", LicensePlate = "29F2-445.89", CurrentArea = "Cầu Giấy - Nam Từ Liêm", Status = ShipperStatus.Active, CompletedOrdersToday = 6, Rating = 4.8 },
                new Shipper { Id = 3, FullName = "Lê Hoàng Long", PhoneNumber = "0983 333 444", VehicleType = "Xe tải 1.25 tấn", LicensePlate = "29C-778.90", CurrentArea = "Hà Đông - Thanh Xuân", Status = ShipperStatus.Active, CompletedOrdersToday = 12, Rating = 5.0 },
                new Shipper { Id = 4, FullName = "Vũ Đình Duy", PhoneNumber = "0984 444 555", VehicleType = "Xe máy Honda AirBlade", LicensePlate = "29B1-234.56", CurrentArea = "Hoàn Kiếm - Hai Bà Trưng", Status = ShipperStatus.OffDuty, CompletedOrdersToday = 0, Rating = 4.7 },
                new Shipper { Id = 5, FullName = "Bùi Văn Đạt", PhoneNumber = "0985 667 889", VehicleType = "Xe máy Honda Wave Alpha", LicensePlate = "20B1-567.89", CurrentArea = "Thái Nguyên (Thịnh Đán - Phan Đình Phùng)", Status = ShipperStatus.Available, CompletedOrdersToday = 15, Rating = 4.9 },
                new Shipper { Id = 6, FullName = "Hoàng Thái Bảo", PhoneNumber = "0986 778 990", VehicleType = "Xe máy Yamaha Sirius", LicensePlate = "20B2-123.45", CurrentArea = "Thái Nguyên (Sông Công - Phổ Yên)", Status = ShipperStatus.Available, CompletedOrdersToday = 11, Rating = 4.8 }
            });

            // 6. Đơn hàng giao nhận mẫu
            _shippingOrders.AddRange(new[]
            {
                new ShippingOrder { Id = 1, OrderCode = "LOGIX-98001", SenderName = "Shop Thời Trang GenZ", SenderPhone = "0944555666", ReceiverName = "Hoàng Thị Mai", ReceiverPhone = "0977112233", ReceiverAddress = "Số 45 Chùa Láng, Đống Đa, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Áo khoác gió unisex", Weight = 0.8, IsExpress = false, CodAmount = 450000, ShippingFee = 25000, Status = ShippingOrderStatus.NewReceived, CreatedDate = DateTime.Now.AddHours(-1) },
                new ShippingOrder { Id = 2, OrderCode = "LOGIX-98002", SenderName = "Điện Máy Xanh Cầu Giấy", SenderPhone = "0243888999", ReceiverName = "Nguyễn Văn Hùng", ReceiverPhone = "0912001122", ReceiverAddress = "Tòa FPT Cầu Giấy, Duy Tân, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Bàn phím cơ không dây", Weight = 1.2, IsExpress = false, CodAmount = 1200000, ShippingFee = 25000, Status = ShippingOrderStatus.PendingProcessing, CreatedDate = DateTime.Now.AddHours(-2) },
                new ShippingOrder { Id = 3, OrderCode = "LOGIX-EXP-001", SenderName = "Trung Tâm Laptop 24h", SenderPhone = "0909112233", ReceiverName = "Vũ Đình Duy", ReceiverPhone = "0988776655", ReceiverAddress = "Tòa Keangnam Landmark 72, Nam Từ Liêm, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Điện thoại Xiaomi Note 13", Weight = 0.6, IsExpress = true, CodAmount = 4890000, ShippingFee = 20000, ExpressSurcharge = 20000, Status = ShippingOrderStatus.Delivering, AssignedShipperName = "Trần Đình Trọng", CreatedDate = DateTime.Now.AddHours(-1), EstimatedDeliveryDate = DateTime.Now.AddHours(1) },
                new ShippingOrder { Id = 4, OrderCode = "LOGIX-98004", SenderName = "Shop Mỹ Phẩm Korea", SenderPhone = "0934889900", ReceiverName = "Đặng Thu Thảo", ReceiverPhone = "0966443322", ReceiverAddress = "Số 18 Lý Thường Kiệt, Hoàn Kiếm, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Set mỹ phẩm dưỡng trắng", Weight = 1.0, IsExpress = true, CodAmount = 1250000, ShippingFee = 20000, ExpressSurcharge = 20000, Status = ShippingOrderStatus.PendingProcessing, CreatedDate = DateTime.Now.AddHours(-1), EstimatedDeliveryDate = DateTime.Now.AddHours(3) },
                new ShippingOrder { Id = 5, OrderCode = "LOGIX-OVERDUE-01", SenderName = "Công ty TNHH Bách Hóa Xanh", SenderPhone = "0901223344", ReceiverName = "Trịnh Kim Ngân", ReceiverPhone = "0945678901", ReceiverAddress = "Số 99 Giảng Võ, Ba Đình, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Lô mỹ phẩm nhập khẩu", Weight = 2.5, IsExpress = true, CodAmount = 2100000, ShippingFee = 20000, ExpressSurcharge = 20000, Status = ShippingOrderStatus.Delivering, AssignedShipperName = "Nguyễn Văn Tuấn", CreatedDate = DateTime.Now.AddHours(-5), EstimatedDeliveryDate = DateTime.Now.AddHours(-1) },
                new ShippingOrder { Id = 6, OrderCode = "LOGIX-98006", SenderName = "Vinamilk Megastore", SenderPhone = "0243888999", ReceiverName = "Phạm Thị Lan", ReceiverPhone = "0915667788", ReceiverAddress = "Số 88 Láng Hạ, Đống Đa, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "2 Thùng sữa tươi Vinamilk", Weight = 18.4, IsExpress = false, CodAmount = 770000, ShippingFee = 35000, Status = ShippingOrderStatus.Delivering, AssignedShipperName = "Nguyễn Văn Tuấn", CreatedDate = DateTime.Now.AddHours(-3) },
                new ShippingOrder { Id = 7, OrderCode = "LOGIX-98007", SenderName = "Shop Phụ Kiện Zin", SenderPhone = "0988776655", ReceiverName = "Bùi Văn Nam", ReceiverPhone = "0922334455", ReceiverAddress = "Số 234 Đội Cấn, Ba Đình, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Tai nghe không dây Bluetooth", Weight = 0.3, IsExpress = false, CodAmount = 320000, ShippingFee = 25000, Status = ShippingOrderStatus.Delivered, AssignedShipperName = "Nguyễn Văn Tuấn", CreatedDate = DateTime.Now.AddHours(-7), DeliveredDate = DateTime.Now.AddHours(-1) },
                new ShippingOrder { Id = 8, OrderCode = "LOGIX-98008", SenderName = "Thời Trang Nam Merriman", SenderPhone = "0911223344", ReceiverName = "Đinh Quang Hiếu", ReceiverPhone = "0933221100", ReceiverAddress = "Tòa Lotte Center, Ba Đình, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Áo sơ mi công sở", Weight = 0.4, IsExpress = true, CodAmount = 650000, ShippingFee = 20000, ExpressSurcharge = 20000, Status = ShippingOrderStatus.Delivered, AssignedShipperName = "Lê Hoàng Long", CreatedDate = DateTime.Now.AddHours(-4), DeliveredDate = DateTime.Now.AddMinutes(-30) },
                new ShippingOrder { Id = 9, OrderCode = "LOGIX-98009", SenderName = "Shop Giày Sneaker", SenderPhone = "0977889900", ReceiverName = "Mai Thế Vinh", ReceiverPhone = "0904112233", ReceiverAddress = "Ngõ 105 Xuân Thủy, Cầu Giấy, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Giày thể thao nam size 42", Weight = 1.1, IsExpress = false, CodAmount = 850000, ShippingFee = 25000, Status = ShippingOrderStatus.Failed, AssignedShipperName = "Trần Đình Trọng", CreatedDate = DateTime.Now.AddHours(-8) },
                new ShippingOrder { Id = 10, OrderCode = "LOGIX-98010", SenderName = "Gia Dụng Thông Minh", SenderPhone = "0919223344", ReceiverName = "Trương Văn Bách", ReceiverPhone = "0967889900", ReceiverAddress = "Khu Đô Thị Linh Đàm, Hoàng Mai, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Bộ nồi inox 5 món", Weight = 5.2, IsExpress = false, CodAmount = 1450000, ShippingFee = 40000, Status = ShippingOrderStatus.Returned, CreatedDate = DateTime.Now.AddDays(-1) },
                new ShippingOrder { Id = 11, OrderCode = "LOGIX-98003", SenderName = "Kho Tổng Tân Bình", SenderPhone = "02839998888", ReceiverName = "Lê Thị Cúc", ReceiverPhone = "0933445566", ReceiverAddress = "Khu Đô Thị Ciputra, Tây Hồ, Hà Nội", DestinationArea = "Hà Nội", ProductSummary = "Nồi chiên không dầu Philips", Weight = 6.8, IsExpress = false, CodAmount = 1890000, ShippingFee = 45000, Status = ShippingOrderStatus.PendingProcessing, CreatedDate = DateTime.Now.AddHours(-3) }
            });

            // 7. Hoạt động gần đây
            _recentActivities.AddRange(new[]
            {
                new RecentActivity { Id = 1, Title = "Đơn Express LOGIX-98008 giao thành công", Description = "Shipper Lê Hoàng Long đã thu COD 650.000đ", Timestamp = DateTime.Now.AddMinutes(-30), Type = ActivityType.OrderSuccess },
                new RecentActivity { Id = 2, Title = "Tiếp nhận lô hàng Hub Long Biên (450 kg)", Description = "Phiếu nhập NK-HUB-260901 đã được duyệt nhập kho", Timestamp = DateTime.Now.AddHours(-1), Type = ActivityType.Import },
                new RecentActivity { Id = 3, Title = "Shipper Trần Đình Trọng nhận điều phối", Description = "Bắt đầu giao đơn hỏa tốc LOGIX-EXP-001 tới Keangnam", Timestamp = DateTime.Now.AddHours(-1), Type = ActivityType.ShipperAssigned },
                new RecentActivity { Id = 4, Title = "Cảnh báo đơn hàng LOGIX-OVERDUE-01 quá hạn", Description = "Đơn hỏa tốc đã quá hạn cam kết giao 45 phút!", Timestamp = DateTime.Now.AddHours(-2), Type = ActivityType.Warning },
                new RecentActivity { Id = 5, Title = "Đơn hàng LOGIX-98009 giao thất bại", Description = "Khách hàng hẹn lại giao sau 18h tối nay", Timestamp = DateTime.Now.AddHours(-3), Type = ActivityType.OrderFailed }
            });

            // 8. Nhật ký biến động nhập / xuất kho ban đầu
            _warehouseMovements.AddRange(new[]
            {
                new WarehouseMovement { Id = 1, TransactionCode = "GD-NK-260901", Timestamp = DateTime.Now.AddHours(-6), MovementType = WarehouseMovementType.InboundReceiving, ItemName = "Lô hàng trung chuyển 450kg", ReferenceCode = "NK-HUB-260901", Quantity = 45, Weight = 450.0, SourceOrDestination = "Hub Long Biên -> Dock Inbound", LocationCode = "DOCK-INBOUND-01", OperatorName = "Lê Văn Vận Hành", Notes = "Tiếp nhận xe tải 29C-889.12" },
                new WarehouseMovement { Id = 2, TransactionCode = "GD-PL-260902", Timestamp = DateTime.Now.AddHours(-5), MovementType = WarehouseMovementType.SortingLastMile, ItemName = "Bưu kiện Áo khoác gió", ReferenceCode = "LOGIX-98001", Quantity = 1, Weight = 0.8, SourceOrDestination = "Dock Inbound -> Tuyến Đống Đa", LocationCode = "KHO-A-D01-K01", OperatorName = "Trần Thị Trưởng Kho", Notes = "Phân loại giao chặng cuối cho Shipper nội thành" },
                new WarehouseMovement { Id = 3, TransactionCode = "GD-PL-260903", Timestamp = DateTime.Now.AddHours(-4), MovementType = WarehouseMovementType.SortingTransit, ItemName = "Kiện gia dụng phân phối liên tỉnh", ReferenceCode = "NK-B2B-260902", Quantity = 20, Weight = 280.0, SourceOrDestination = "Dock Inbound -> Cửa xuất xe tải", LocationCode = "DOCK-INBOUND-01", OperatorName = "Lê Văn Vận Hành", Notes = "Phân loại hàng trung chuyển chuyển tiếp Hub Đà Nẵng" },
                new WarehouseMovement { Id = 4, TransactionCode = "GD-XK-260904", Timestamp = DateTime.Now.AddHours(-3), MovementType = WarehouseMovementType.OutboundLastMile, ItemName = "2 Thùng sữa tươi Vinamilk", ReferenceCode = "LOGIX-98006", Quantity = 2, Weight = 18.4, SourceOrDestination = "Khu B -> Shipper Nguyễn Văn Tuấn", LocationCode = "KHO-A-D01-K01", OperatorName = "Trần Thị Trưởng Kho", Notes = "Xác nhận xuất kho cho tài xế giao chặng cuối" },
                new WarehouseMovement { Id = 5, TransactionCode = "GD-XK-260905", Timestamp = DateTime.Now.AddHours(-2), MovementType = WarehouseMovementType.OutboundTransit, ItemName = "Lô hàng bưu kiện liên tỉnh Tây Bắc", ReferenceCode = "TRANSIT-TB-01", Quantity = 15, Weight = 120.0, SourceOrDestination = "Dock Inbound -> Xe tải 29C-778.90", LocationCode = "DOCK-INBOUND-01", OperatorName = "Lê Văn Vận Hành", Notes = "Xác nhận xuất bến xe tải trung chuyển" },
                new WarehouseMovement { Id = 6, TransactionCode = "GD-DC-260906", Timestamp = DateTime.Now.AddHours(-1), MovementType = WarehouseMovementType.StockRelocation, ItemName = "Điện thoại Xiaomi Note 13", ReferenceCode = "SP-DIENTU-01", Quantity = 10, Weight = 5.0, SourceOrDestination = "Dock Inbound -> Kệ Hỏa Tốc", LocationCode = "KHO-EXP-01", OperatorName = "Lê Văn Vận Hành", Notes = "Chuyển kệ lưu trữ hàng giá trị cao" }
            });

            // 9. Dữ liệu chuyến điều phối ban đầu
            _dispatchRecords.AddRange(new[]
            {
                new DispatchRecord
                {
                    Id = 1,
                    DispatchCode = "DP-260925-001",
                    DispatchDate = DateTime.Today,
                    CreatedTime = DateTime.Now.AddHours(-3),
                    ShipperId = 1,
                    ShipperName = "Nguyễn Văn Tuấn",
                    ShipperPhone = "0981 111 222",
                    VehiclePlate = "29H1-892.11",
                    DeliveryArea = "Đống Đa - Ba Đình",
                    TotalOrders = 6,
                    ExpressOrdersCount = 2,
                    TotalCodAmount = 3540000,
                    TotalWeight = 21.7,
                    DispatcherName = "Trần Thị Trưởng Kho",
                    Status = "Đang Đi Giao",
                    Notes = "Tuyến giao buổi sáng, ưu tiên 2 đơn hỏa tốc trước 11h",
                    OrderIds = new List<int> { 5, 6 }
                },
                new DispatchRecord
                {
                    Id = 2,
                    DispatchCode = "DP-260925-002",
                    DispatchDate = DateTime.Today,
                    CreatedTime = DateTime.Now.AddHours(-1),
                    ShipperId = 2,
                    ShipperName = "Trần Đình Trọng",
                    ShipperPhone = "0982 222 333",
                    VehiclePlate = "29F2-445.89",
                    DeliveryArea = "Cầu Giấy - Nam Từ Liêm",
                    TotalOrders = 4,
                    ExpressOrdersCount = 1,
                    TotalCodAmount = 6090000,
                    TotalWeight = 5.2,
                    DispatcherName = "Lê Văn Vận Hành",
                    Status = "Đang Đi Giao",
                    Notes = "Giao khu công nghệ Duy Tân và Keangnam",
                    OrderIds = new List<int> { 3 }
                }
            });

            // 10. Dữ liệu biên bản bàn giao hàng hoàn trả Shop ban đầu
            _returnBatches.AddRange(new[]
            {
                new ReturnHandoverBatch
                {
                    Id = 1,
                    BatchCode = "BBH-260925-001",
                    SenderName = "Gia Dụng Thông Minh",
                    SenderPhone = "0919223344",
                    SenderAddress = "Số 120 Đường Cầu Giấy, Hà Nội",
                    CreatedTime = DateTime.Now.AddHours(-2),
                    TotalOrders = 1,
                    TotalCodValue = 1450000,
                    TotalReturnFee = 15000,
                    OperatorName = "Trần Thị Trưởng Kho",
                    Status = "Đang Lưu Kho",
                    Notes = "Khách từ chối nhận mẫu nồi inox, bàn giao trả Shop tại quầy",
                    OrderIds = new List<int> { 10 }
                }
            });
        }
        #endregion

        #region Các hàm truy vấn và thao tác dữ liệu (CRUD)
        
        // =========================================================================
        // PHÂN HỆ QUẢN TRỊ NGƯỜI DÙNG & TÀI KHOẢN (USERS)
        // =========================================================================

        /// <summary>
        /// HÀM TRUY VẤN: Tìm kiếm tài khoản theo Tên đăng nhập (Username)
        /// - Nhiệm vụ: Xác định tài khoản có tồn tại trong hệ thống hay không để phục vụ đăng nhập và kiểm tra trùng lặp.
        /// - Cách hoạt động: 
        ///   1. Kiểm tra trong bộ nhớ đệm _users trước (nhanh chóng).
        ///   2. Nếu chưa có và CSDL SQL Server đang kết nối -> thực hiện truy vấn SELECT TOP 1 từ bảng _users.
        ///   3. Ánh xạ dữ liệu cột CSDL (UserId, Username, Password, FullName, Role, IsActive, CreatedAt) vào User model và nạp vào cache.
        /// - Tương tác dữ liệu: Bảng _users trong SQL Server quanlykho, danh sách _users bộ nhớ, lớp User.cs.
        /// </summary>
        public User? FindUserByUsername(string tenDangNhap)
        {
            if (string.IsNullOrWhiteSpace(tenDangNhap)) return null;

            lock (_lock)
            {
                var nguoiDungTrongCache = _users.FirstOrDefault(u => u.Username.Equals(tenDangNhap.Trim(), StringComparison.OrdinalIgnoreCase));
                if (nguoiDungTrongCache != null) return nguoiDungTrongCache;
            }

            // Nếu chưa có trong cache và có kết nối DB, truy vấn trực tiếp từ SQL Server qua EF Core
            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var nguoiDungDb = db.Users.AsNoTracking().FirstOrDefault(u => u.Username == tenDangNhap.Trim());
                    if (nguoiDungDb != null)
                    {
                        lock (_lock)
                        {
                            _users.Add(nguoiDungDb);
                        }
                        return nguoiDungDb;
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[FindUserByUsername EF Core Error] {ngoaiLe.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// HÀM TRUY VẤN: Tìm kiếm tài khoản theo mã ID định danh
        /// </summary>
        public User? FindUserById(int maId)
        {
            lock (_lock) return _users.FirstOrDefault(u => u.Id == maId);
        }

        /// <summary>
        /// HÀM TRUY VẤN: Lấy toàn bộ danh sách tài khoản người dùng
        /// - Tương tác: Đổ dữ liệu vào DataGrid của UserManagementView.xaml.
        /// </summary>
        public IReadOnlyList<User> GetAllUsers()
        {
            lock (_lock) return _users.ToList().AsReadOnly();
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Thêm mới tài khoản người dùng vào hệ thống
        /// - Nhiệm vụ: Ghi nhận thông tin tài khoản mới vào bộ nhớ đệm và lưu vĩnh viễn vào bảng _users trong SQL Server.
        /// - Cách hoạt động: Tự động gán ID tăng dần, thực thi lệnh INSERT INTO _users với các tham số tương ứng.
        /// - Tương tác dữ liệu: UserManagementView.xaml.cs, SQL Server bảng _users.
        /// </summary>
        public void AddUser(User nguoiDung)
        {
            lock (_lock)
            {
                if (nguoiDung.Id <= 0)
                {
                    nguoiDung.Id = _users.Count > 0 ? _users.Max(u => u.Id) + 1 : 1;
                }
                _users.Add(nguoiDung);
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var entity = new User
                    {
                        Username = nguoiDung.Username,
                        PasswordHash = nguoiDung.PasswordHash,
                        FullName = nguoiDung.FullName,
                        Email = nguoiDung.Email,
                        PhoneNumber = nguoiDung.PhoneNumber,
                        Role = nguoiDung.Role,
                        IsActive = nguoiDung.IsActive,
                        CreatedAt = nguoiDung.CreatedAt,
                        LastLoginAt = nguoiDung.LastLoginAt
                    };
                    db.Users.Add(entity);
                    db.SaveChanges();
                    nguoiDung.Id = entity.Id;
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddUser EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Đổi trạng thái kích hoạt (Khóa / Mở khóa) của tài khoản
        /// - Nhiệm vụ: Thay đổi giá trị IsActive trong bộ nhớ và thực thi UPDATE _users SET IsActive trong SQL Server.
        /// - Tương tác dữ liệu: Nút btnToggleActive trong UserManagementView.xaml.
        /// </summary>
        public void ToggleUserActiveStatus(int maNguoiDung)
        {
            bool trangThaiMoi = true;
            lock (_lock)
            {
                var nguoiDung = _users.FirstOrDefault(x => x.Id == maNguoiDung);
                if (nguoiDung != null)
                {
                    nguoiDung.IsActive = !nguoiDung.IsActive;
                    trangThaiMoi = nguoiDung.IsActive;
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var user = db.Users.Find(maNguoiDung);
                    if (user != null)
                    {
                        user.IsActive = trangThaiMoi;
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToggleUserActiveStatus EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Đổi mật khẩu tài khoản người dùng
        /// - Nhiệm vụ: Cập nhật PasswordHash trong bộ nhớ và thực thi UPDATE _users SET Password trong SQL Server.
        /// - Tương tác dữ liệu: Modal đổi mật khẩu trong UserManagementView.xaml.
        /// </summary>
        public void UpdateUserPassword(int maNguoiDung, string matKhauMoiTho)
        {
            lock (_lock)
            {
                var nguoiDung = _users.FirstOrDefault(x => x.Id == maNguoiDung);
                if (nguoiDung != null)
                {
                    nguoiDung.PasswordHash = matKhauMoiTho;
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var user = db.Users.Find(maNguoiDung);
                    if (user != null)
                    {
                        user.PasswordHash = matKhauMoiTho;
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateUserPassword EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Điều phối & phân công tài xế Shipper cho đơn hàng vận chuyển
        /// - Nhiệm vụ: Gán thông tin Shipper (Id, Tên, SĐT), tự động chuyển trạng thái đơn sang 'Đang Giao',
        ///             ghi nhận UPDATE bảng ShippingOrders và INSERT vào bảng RecentActivities trong SQL Server.
        /// - Cách hoạt động: 
        ///   1. Tìm đơn hàng theo maDonHang.
        ///   2. Cập nhật AssignedShipperId, AssignedShipperName, ShipperPhone.
        ///   3. Nếu trạng thái đang là Mới nhận hoặc Chờ xử lý -> nâng lên Đang Giao (Delivering).
        ///   4. Thực thi câu lệnh SQL UPDATE và thêm dòng lịch sử hoạt động.
        /// - Tương tác dữ liệu: OrderManagementView.xaml, Shipper.cs, ShippingOrder.cs, SQL Server.
        /// </summary>
        public void AssignShipperToOrder(int maDonHang, int maTaiXe, string tenTaiXe, string soDienThoaiTaiXe)
        {
            lock (_lock)
            {
                var donHang = _shippingOrders.FirstOrDefault(o => o.Id == maDonHang);
                if (donHang != null)
                {
                    donHang.AssignedShipperId = maTaiXe;
                    donHang.AssignedShipperName = tenTaiXe;
                    donHang.ShipperPhone = soDienThoaiTaiXe;
                    if (donHang.Status == ShippingOrderStatus.NewReceived || donHang.Status == ShippingOrderStatus.PendingProcessing)
                    {
                        donHang.Status = ShippingOrderStatus.Delivering;
                    }
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var don = db.ShippingOrders.Find(maDonHang);
                    if (don != null)
                    {
                        don.AssignedShipperId = maTaiXe;
                        don.AssignedShipperName = tenTaiXe;
                        don.ShipperPhone = soDienThoaiTaiXe;
                        if (don.Status == ShippingOrderStatus.NewReceived || don.Status == ShippingOrderStatus.PendingProcessing)
                        {
                            don.Status = ShippingOrderStatus.Delivering;
                        }
                    }

                    db.RecentActivities.Add(new RecentActivity
                    {
                        Title = $"Điều phối Shipper {tenTaiXe}",
                        Description = $"Đã phân công đơn hàng ID #{maDonHang}",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.Delivering
                    });
                    db.SaveChanges();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AssignShipper EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        // =========================================================================
        // PHÂN HỆ VỊ TRÍ KHO BÃI & MÁNG PHÂN LOẠI (WAREHOUSE LOCATIONS)
        // =========================================================================

        /// <summary>
        /// Lấy danh sách vị trí lưu trữ kệ kho & máng phân loại bưu kiện (Khu A / Khu B / Khu C / Express)
        /// </summary>
        public IReadOnlyList<WarehouseLocation> GetAllLocations() { lock (_lock) return _locations.ToList().AsReadOnly(); }

        // =========================================================================
        // PHÂN HỆ NGHIỆP VỤ NHẬP KHO (IMPORT ORDERS)
        // =========================================================================

        /// <summary>
        /// Lấy toàn bộ danh sách phiếu nhập kho, sắp xếp mới nhất lên đầu
        /// </summary>
        public IReadOnlyList<ImportOrder> GetAllImportOrders() { lock (_lock) return _importOrders.OrderByDescending(o => o.CreatedDate).ToList().AsReadOnly(); }
        
        /// <summary>
        /// HÀM NGHIỆP VỤ: Tiếp nhận và lưu phiếu nhập kho mới
        /// - Nhiệm vụ: Thêm phiếu nhập vào bảng ImportOrders trong SQL Server và ghi log vào RecentActivities.
        /// - Tương tác dữ liệu: ImportManagementView.xaml.cs, SQL Server.
        /// </summary>
        public void AddImportOrder(ImportOrder phieuNhap)
        {
            phieuNhap.CreatedDate = DateTime.Now;

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var entity = new ImportOrder
                    {
                        ImportCode = phieuNhap.ImportCode,
                        SourceType = phieuNhap.SourceType,
                        SenderName = phieuNhap.SenderName,
                        SenderPhone = phieuNhap.SenderPhone,
                        SenderAddress = phieuNhap.SenderAddress,
                        WaybillNumber = phieuNhap.WaybillNumber,
                        VehiclePlate = phieuNhap.VehiclePlate,
                        DriverName = phieuNhap.DriverName,
                        TotalWeight = phieuNhap.TotalWeight,
                        TotalValue = phieuNhap.TotalValue,
                        Status = phieuNhap.Status,
                        CreatedByName = phieuNhap.CreatedByName,
                        ApprovedDate = phieuNhap.ApprovedDate,
                        ApprovedByName = phieuNhap.ApprovedByName,
                        Notes = phieuNhap.Notes,
                        CreatedDate = phieuNhap.CreatedDate
                    };
                    db.ImportOrders.Add(entity);

                    db.RecentActivities.Add(new RecentActivity
                    {
                        Title = $"Tạo mới phiếu nhập {phieuNhap.ImportCode}",
                        Description = $"Nguồn gửi: {phieuNhap.SenderName} ({phieuNhap.SourceTypeName})",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.Import
                    });
                    db.SaveChanges();
                    phieuNhap.Id = entity.Id;
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddImportOrder EF Core Error] {ngoaiLe.Message}");
                }
            }

            lock (_lock)
            {
                if (phieuNhap.Id <= 0)
                {
                    phieuNhap.Id = _importOrders.Count > 0 ? _importOrders.Max(o => o.Id) + 1 : 1;
                }
                _importOrders.Insert(0, phieuNhap);
                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"Tạo mới phiếu nhập {phieuNhap.ImportCode}",
                    Description = $"Nguồn gửi: {phieuNhap.SenderName} ({phieuNhap.SourceTypeName})",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.Import
                });
            }
        }

        // =========================================================================
        // PHÂN HỆ ĐƠN HÀNG VẬN CHUYỂN (SHIPPING ORDERS)
        // =========================================================================

        /// <summary>
        /// Lấy toàn bộ danh sách đơn hàng vận chuyển, sắp xếp theo thời gian mới nhất
        /// </summary>
        public IReadOnlyList<ShippingOrder> GetAllShippingOrders() { lock (_lock) return _shippingOrders.OrderByDescending(o => o.CreatedDate).ToList().AsReadOnly(); }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Phát hành đơn hàng vận chuyển mới
        /// - Nhiệm vụ: Tự động tạo mã vận đơn (LOGIX-...), lưu vào bảng ShippingOrders và ghi nhận hoạt động vào RecentActivities.
        /// - Tương tác dữ liệu: OrderManagementView.xaml.cs, SQL Server.
        /// </summary>
        public void AddShippingOrder(ShippingOrder donHang)
        {
            donHang.CreatedDate = DateTime.Now;
            if (string.IsNullOrEmpty(donHang.OrderCode))
            {
                donHang.OrderCode = $"LOGIX-{DateTime.Now:yyMMdd}-{new Random().Next(100, 999)}";
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var entity = new ShippingOrder
                    {
                        OrderCode = donHang.OrderCode,
                        SenderName = donHang.SenderName,
                        SenderPhone = donHang.SenderPhone,
                        SenderAddress = donHang.SenderAddress,
                        ReceiverName = donHang.ReceiverName,
                        ReceiverPhone = donHang.ReceiverPhone,
                        ReceiverAddress = donHang.ReceiverAddress,
                        DestinationArea = donHang.DestinationArea,
                        ProductSummary = donHang.ProductSummary,
                        Weight = donHang.Weight,
                        IsExpress = donHang.IsExpress,
                        CodAmount = donHang.CodAmount,
                        ShippingFee = donHang.ShippingFee,
                        ExpressSurcharge = donHang.ExpressSurcharge,
                        ReceiverPaysFee = donHang.ReceiverPaysFee,
                        Status = donHang.Status,
                        AssignedShipperId = donHang.AssignedShipperId,
                        AssignedShipperName = donHang.AssignedShipperName,
                        ShipperPhone = donHang.ShipperPhone,
                        CreatedDate = donHang.CreatedDate,
                        EstimatedDeliveryDate = donHang.EstimatedDeliveryDate,
                        DeliveredDate = donHang.DeliveredDate,
                        Notes = donHang.Notes,
                        FailedDeliveryCount = donHang.FailedDeliveryCount,
                        FailureReason = donHang.FailureReason,
                        FailureTimestamp = donHang.FailureTimestamp,
                        RtoTrackingCode = donHang.RtoTrackingCode,
                        RtoLocationCode = donHang.RtoLocationCode,
                        RtoApprovedDate = donHang.RtoApprovedDate,
                        ReturnShippingFee = donHang.ReturnShippingFee,
                        ReturnHandoverBatchCode = donHang.ReturnHandoverBatchCode
                    };
                    db.ShippingOrders.Add(entity);

                    db.RecentActivities.Add(new RecentActivity
                    {
                        Title = donHang.IsExpress ? $"Tạo đơn Express {donHang.OrderCode}" : $"Tạo đơn hàng {donHang.OrderCode}",
                        Description = $"Gửi tới: {donHang.ReceiverName} ({donHang.DestinationArea})",
                        Timestamp = DateTime.Now,
                        Type = donHang.IsExpress ? ActivityType.Express : ActivityType.OrderNew
                    });
                    db.SaveChanges();
                    donHang.Id = entity.Id;
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddShippingOrder EF Core Error] {ngoaiLe.Message}");
                }
            }

            lock (_lock)
            {
                if (donHang.Id <= 0)
                {
                    donHang.Id = _shippingOrders.Count > 0 ? _shippingOrders.Max(o => o.Id) + 1 : 1;
                }
                _shippingOrders.Insert(0, donHang);

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = donHang.IsExpress ? $"Tạo đơn Express {donHang.OrderCode}" : $"Tạo đơn hàng {donHang.OrderCode}",
                    Description = $"Gửi tới: {donHang.ReceiverName} ({donHang.DestinationArea})",
                    Timestamp = DateTime.Now,
                    Type = donHang.IsExpress ? ActivityType.Express : ActivityType.OrderNew
                });
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Cập nhật trạng thái vòng đời đơn hàng
        /// - Nhiệm vụ: Cập nhật thuộc tính Status trong bộ nhớ và thực thi UPDATE bảng ShippingOrders trong SQL Server.
        /// </summary>
        public void UpdateShippingOrderStatus(int maDonHang, ShippingOrderStatus trangThaiMoi)
        {
            lock (_lock)
            {
                var donHang = _shippingOrders.FirstOrDefault(o => o.Id == maDonHang);
                if (donHang != null)
                {
                    donHang.Status = trangThaiMoi;
                    if (trangThaiMoi == ShippingOrderStatus.Delivered)
                    {
                        donHang.DeliveredDate = DateTime.Now;
                    }
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var don = db.ShippingOrders.Find(maDonHang);
                    if (don != null)
                    {
                        don.Status = trangThaiMoi;
                        if (trangThaiMoi == ShippingOrderStatus.Delivered)
                        {
                            don.DeliveredDate = DateTime.Now;
                        }
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateShippingOrderStatus EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Cập nhật toàn diện thông tin đơn hàng (Shipper, trạng thái, ghi chú)
        /// </summary>
        public void UpdateShippingOrder(ShippingOrder donHang)
        {
            if (donHang == null) return;

            lock (_lock)
            {
                var target = _shippingOrders.FirstOrDefault(o => o.Id == donHang.Id || (!string.IsNullOrEmpty(donHang.OrderCode) && o.OrderCode == donHang.OrderCode));
                if (target != null)
                {
                    target.Status = donHang.Status;
                    target.AssignedShipperId = donHang.AssignedShipperId;
                    target.AssignedShipperName = donHang.AssignedShipperName;
                    target.ShipperPhone = donHang.ShipperPhone;
                    target.Notes = donHang.Notes;
                    target.DeliveredDate = donHang.DeliveredDate;
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var targetDb = db.ShippingOrders.FirstOrDefault(o => o.Id == donHang.Id || (!string.IsNullOrEmpty(donHang.OrderCode) && o.OrderCode == donHang.OrderCode));
                    if (targetDb != null)
                    {
                        targetDb.Status = donHang.Status;
                        targetDb.AssignedShipperId = donHang.AssignedShipperId;
                        targetDb.AssignedShipperName = donHang.AssignedShipperName;
                        targetDb.ShipperPhone = donHang.ShipperPhone;
                        targetDb.Notes = donHang.Notes;
                        targetDb.DeliveredDate = donHang.DeliveredDate;
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateShippingOrder EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Ghi nhận hoạt động hệ thống mới vào RecentActivities
        /// </summary>
        public void AddRecentActivity(RecentActivity hoatDong)
        {
            if (hoatDong == null) return;
            lock (_lock)
            {
                hoatDong.Id = _recentActivities.Count + 1;
                _recentActivities.Insert(0, hoatDong);
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    db.RecentActivities.Add(new RecentActivity
                    {
                        Title = hoatDong.Title,
                        Description = hoatDong.Description,
                        Timestamp = hoatDong.Timestamp,
                        Type = hoatDong.Type
                    });
                    db.SaveChanges();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddRecentActivity EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ (TÍNH NĂNG 5): Điều phối hàng loạt đơn hàng theo tuyến đường gom (Route Batch Dispatch)
        /// - Nhiệm vụ: Gán một Shipper cho toàn bộ danh sách đơn hàng thuộc tuyến đường gom,
        ///             chuyển trạng thái các đơn sang 'Đang Giao' (Delivering),
        ///             đồng bộ cập nhật hàng loạt trong SQL Server và ghi log vào RecentActivities.
        /// - Cách hoạt động:
        ///   1. Duyệt qua bộ nhớ đệm _shippingOrders, tìm các đơn có ID nằm trong danhSachMaDon.
        ///   2. Cập nhật đồng loạt AssignedShipperId, AssignedShipperName, ShipperPhone và Status = Delivering.
        ///   3. Nếu có kết nối CSDL, sinh câu lệnh SQL UPDATE IN (...) để đồng bộ ngay lập tức.
        ///   4. Thêm bản ghi hoạt động vào RecentActivities thông báo phát hành chuyến gom.
        /// - Tương tác dữ liệu: RouteBatchingView.xaml, ShippingOrders, RecentActivities.
        /// </summary>
        public void DieuPhoiGomChuyenTuyen(List<int> danhSachMaDon, int maTaiXe, string tenTaiXe, string soDienThoaiTaiXe, string tenTuyenDuong)
        {
            if (danhSachMaDon == null || danhSachMaDon.Count == 0) return;

            lock (_lock)
            {
                foreach (var maDon in danhSachMaDon)
                {
                    var donHang = _shippingOrders.FirstOrDefault(o => o.Id == maDon);
                    if (donHang != null)
                    {
                        donHang.AssignedShipperId = maTaiXe;
                        donHang.AssignedShipperName = tenTaiXe;
                        donHang.ShipperPhone = soDienThoaiTaiXe;
                        donHang.Status = ShippingOrderStatus.Delivering;
                    }
                }

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"Phát hành chuyến gom {tenTuyenDuong}",
                    Description = $"Shipper {tenTaiXe} nhận giao {danhSachMaDon.Count} bưu kiện",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.ShipperAssigned
                });
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var dsDon = db.ShippingOrders.Where(o => danhSachMaDon.Contains(o.Id)).ToList();
                    foreach (var don in dsDon)
                    {
                        don.AssignedShipperId = maTaiXe;
                        don.AssignedShipperName = tenTaiXe;
                        don.ShipperPhone = soDienThoaiTaiXe;
                        don.Status = ShippingOrderStatus.Delivering;
                    }

                    db.RecentActivities.Add(new RecentActivity
                    {
                        Title = $"Phát hành chuyến gom {tenTuyenDuong}",
                        Description = $"Shipper {tenTaiXe} nhận giao {danhSachMaDon.Count} bưu kiện",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.ShipperAssigned
                    });
                    db.SaveChanges();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[DieuPhoiGomChuyenTuyen EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM TRUY VẤN (TÍNH NĂNG 7): Tìm kiếm đơn hàng phục vụ Cổng Tra Cứu Hành Trình Vận Đơn
        /// - Nhiệm vụ: Tìm kiếm chính xác hoặc tương đối theo Mã vận đơn (OrderCode) hoặc Số điện thoại người nhận.
        /// - Cách hoạt động:
        ///   1. Làm sạch từ khóa tìm kiếm (cắt khoảng trắng, chữ hoa/thường).
        ///   2. Ưu tiên tìm khớp chính xác mã vận đơn trong cache _shippingOrders.
        ///   3. Nếu không thấy, tìm theo số điện thoại người nhận hoặc người gửi.
        ///   4. Nếu có kết nối DB và chưa thấy trong cache, tìm trực tiếp trong SQL Server.
        /// - Tương tác dữ liệu: TrackingPortalView.xaml.cs, ShippingOrder.cs.
        /// </summary>
        public ShippingOrder? TimKiemDonHangTheoMaHoacSoDienThoai(string tuKhoa)
        {
            if (string.IsNullOrWhiteSpace(tuKhoa)) return null;
            string tuKhoaChuanHoa = tuKhoa.Trim();

            lock (_lock)
            {
                // Ưu tiên tìm theo Mã vận đơn
                var donTheoMa = _shippingOrders.FirstOrDefault(o => o.OrderCode.Equals(tuKhoaChuanHoa, StringComparison.OrdinalIgnoreCase)
                                                                 || o.OrderCode.Contains(tuKhoaChuanHoa, StringComparison.OrdinalIgnoreCase));
                if (donTheoMa != null) return donTheoMa;

                // Tìm theo Số điện thoại người nhận hoặc người gửi
                var donTheoSoDienThoai = _shippingOrders.FirstOrDefault(o => o.ReceiverPhone.Contains(tuKhoaChuanHoa)
                                                                         || o.SenderPhone.Contains(tuKhoaChuanHoa));
                if (donTheoSoDienThoai != null) return donTheoSoDienThoai;
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var donHang = db.ShippingOrders.AsNoTracking().FirstOrDefault(o =>
                        o.OrderCode.Contains(tuKhoaChuanHoa) ||
                        o.ReceiverPhone.Contains(tuKhoaChuanHoa) ||
                        o.SenderPhone.Contains(tuKhoaChuanHoa));
                    if (donHang != null) return donHang;
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[TimKiemDonHang EF Core Error] {ngoaiLe.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Đẩy nhanh tiến trình vòng đời đơn hàng sang nấc tiếp theo
        /// - Nhiệm vụ: Tự động chuyển đơn hàng theo chu trình:
        ///   Mới tiếp nhận (0) -> Chờ xử lý (1) -> Đang giao (2) -> Giao thành công (3).
        /// - Tương tác dữ liệu: TrackingPortalView.xaml.cs, OrderManagementView.xaml.cs.
        /// </summary>
        public void ChuyenBuocTienTrinhDonHang(int maDonHang)
        {
            lock (_lock)
            {
                var donHang = _shippingOrders.FirstOrDefault(o => o.Id == maDonHang);
                if (donHang != null)
                {
                    var trangThaiKeTiep = donHang.Status switch
                    {
                        ShippingOrderStatus.NewReceived => ShippingOrderStatus.PendingProcessing,
                        ShippingOrderStatus.PendingProcessing => ShippingOrderStatus.Delivering,
                        ShippingOrderStatus.Delivering => ShippingOrderStatus.Delivered,
                        _ => donHang.Status
                    };

                    if (trangThaiKeTiep != donHang.Status)
                    {
                        UpdateShippingOrderStatus(maDonHang, trangThaiKeTiep);
                    }
                }
            }
        }

        /// <summary>
        /// Lấy toàn bộ danh sách Shipper giao hàng (tự động cập nhật số đơn đang đi giao)
        /// </summary>
        public IReadOnlyList<Shipper> GetAllShippers()
        {
            lock (_lock)
            {
                foreach (var s in _shippers)
                {
                    s.ActiveDeliveringCount = _shippingOrders.Count(o => o.AssignedShipperId == s.Id && o.Status == ShippingOrderStatus.Delivering);
                }
                return _shippers.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Đăng ký thêm nhân viên giao hàng (Shipper) mới
        /// </summary>
        public void AddShipper(Shipper taiXe)
        {
            lock (_lock)
            {
                if (taiXe.Id <= 0)
                {
                    taiXe.Id = _shippers.Count > 0 ? _shippers.Max(s => s.Id) + 1 : 1;
                }
                _shippers.Add(taiXe);

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"Đăng ký tài xế mới: {taiXe.FullName}",
                    Description = $"SĐT: {taiXe.Phone} - Tuyến: {taiXe.DeliveryArea} ({taiXe.VehiclePlate})",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.ShipperAssigned
                });
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var entity = new Shipper
                    {
                        FullName = taiXe.FullName,
                        Phone = taiXe.Phone,
                        CitizenId = taiXe.CitizenId,
                        VehicleType = taiXe.VehicleType,
                        VehiclePlate = taiXe.VehiclePlate,
                        DeliveryArea = taiXe.DeliveryArea,
                        Status = taiXe.Status,
                        IsLocked = taiXe.IsLocked,
                        WorkShift = taiXe.WorkShift,
                        MaxOrdersPerDay = taiXe.MaxOrdersPerDay,
                        MaxWeightCapacity = taiXe.MaxWeightCapacity,
                        CompletedTodayCount = taiXe.CompletedTodayCount,
                        Rating = taiXe.Rating
                    };
                    db.Shippers.Add(entity);
                    db.SaveChanges();
                    taiXe.Id = entity.Id;
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddShipper EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Khóa hoặc Mở khóa tài khoản Shipper
        /// </summary>
        public void ToggleShipperLock(int maTaiXe)
        {
            lock (_lock)
            {
                var taiXe = _shippers.FirstOrDefault(s => s.Id == maTaiXe);
                if (taiXe != null)
                {
                    taiXe.IsLocked = !taiXe.IsLocked;
                    if (taiXe.IsLocked)
                    {
                        taiXe.Status = ShipperStatus.Offline;
                    }
                    else
                    {
                        taiXe.Status = ShipperStatus.Available;
                    }

                    _recentActivities.Insert(0, new RecentActivity
                    {
                        Id = _recentActivities.Count + 1,
                        Title = $"{(taiXe.IsLocked ? "Khóa tài khoản" : "Mở khóa")} Shipper: {taiXe.FullName}",
                        Description = $"Trạng thái mới: {(taiXe.IsLocked ? "Tạm khóa nhận đơn" : "Sẵn sàng hoạt động")}",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.Warning
                    });
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var taiXeDb = db.Shippers.Find(maTaiXe);
                    if (taiXeDb != null)
                    {
                        var taiXeMem = _shippers.FirstOrDefault(s => s.Id == maTaiXe);
                        if (taiXeMem != null)
                        {
                            taiXeDb.IsLocked = taiXeMem.IsLocked;
                            taiXeDb.Status = taiXeMem.Status;
                        }
                        db.RecentActivities.Add(new RecentActivity
                        {
                            Title = $"{(taiXeDb.IsLocked ? "Khóa tài khoản" : "Mở khóa")} Shipper: {taiXeDb.FullName}",
                            Description = $"Trạng thái mới: {(taiXeDb.IsLocked ? "Tạm khóa nhận đơn" : "Sẵn sàng hoạt động")}",
                            Timestamp = DateTime.Now,
                            Type = ActivityType.Warning
                        });
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToggleShipperLock EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Cập nhật ca làm việc và trạng thái trực ban của Shipper
        /// </summary>
        public void UpdateShipperShiftAndStatus(int maTaiXe, string caLamViec, ShipperStatus trangThaiMoi)
        {
            lock (_lock)
            {
                var taiXe = _shippers.FirstOrDefault(s => s.Id == maTaiXe);
                if (taiXe != null)
                {
                    taiXe.WorkShift = caLamViec;
                    taiXe.Status = trangThaiMoi;
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var taiXe = db.Shippers.Find(maTaiXe);
                    if (taiXe != null)
                    {
                        taiXe.WorkShift = caLamViec;
                        taiXe.Status = trangThaiMoi;
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateShipperShiftAndStatus EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Thiết lập khu vực phụ trách và giới hạn số đơn/ngày cho Shipper
        /// </summary>
        public void UpdateShipperAreaAndLimit(int maTaiXe, string khuVucMoi, int gioiHanDonMoi)
        {
            lock (_lock)
            {
                var taiXe = _shippers.FirstOrDefault(s => s.Id == maTaiXe);
                if (taiXe != null)
                {
                    taiXe.DeliveryArea = khuVucMoi;
                    taiXe.MaxOrdersPerDay = gioiHanDonMoi;
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var taiXe = db.Shippers.Find(maTaiXe);
                    if (taiXe != null)
                    {
                        taiXe.DeliveryArea = khuVucMoi;
                        taiXe.MaxOrdersPerDay = gioiHanDonMoi;
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateShipperAreaAndLimit EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// Lấy danh sách 15 sự kiện/hoạt động vận hành gần nhất
        /// </summary>
        public IReadOnlyList<RecentActivity> GetRecentActivities() { lock (_lock) return _recentActivities.Take(15).ToList().AsReadOnly(); }

        // =========================================================================
        // PHÂN HỆ QUẢN LÝ KHO (WAREHOUSE MOVEMENTS & INVENTORY CONTROL)
        // =========================================================================

        /// <summary>
        /// Lấy toàn bộ lịch sử biến động nhập / xuất kho sắp xếp mới nhất lên đầu
        /// </summary>
        public IReadOnlyList<WarehouseMovement> GetAllWarehouseMovements()
        {
            lock (_lock) return _warehouseMovements.OrderByDescending(m => m.Timestamp).ToList().AsReadOnly();
        }

        /// <summary>
        /// Thêm bản ghi biến động kho mới và đồng bộ log
        /// </summary>
        public void AddWarehouseMovement(WarehouseMovement bienDong)
        {
            bienDong.Timestamp = DateTime.Now;
            if (string.IsNullOrEmpty(bienDong.TransactionCode))
            {
                bienDong.TransactionCode = $"GD-{DateTime.Now:yyMMdd}-{new Random().Next(100, 999)}";
            }

            lock (_lock)
            {
                if (bienDong.Id <= 0)
                {
                    bienDong.Id = _warehouseMovements.Count > 0 ? _warehouseMovements.Max(m => m.Id) + 1 : 1;
                }
                _warehouseMovements.Insert(0, bienDong);

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"{bienDong.MovementTypeDisplayName}: {bienDong.ItemName}",
                    Description = $"{bienDong.SourceOrDestination} ({bienDong.Weight:N1} kg)",
                    Timestamp = DateTime.Now,
                    Type = bienDong.MovementType == WarehouseMovementType.InboundReceiving ? ActivityType.Import : ActivityType.ShipperAssigned
                });
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var entity = new WarehouseMovement
                    {
                        TransactionCode = bienDong.TransactionCode,
                        Timestamp = bienDong.Timestamp,
                        MovementType = bienDong.MovementType,
                        ItemName = bienDong.ItemName,
                        ReferenceCode = bienDong.ReferenceCode,
                        Quantity = bienDong.Quantity,
                        Weight = bienDong.Weight,
                        SourceOrDestination = bienDong.SourceOrDestination,
                        LocationCode = bienDong.LocationCode,
                        OperatorName = bienDong.OperatorName,
                        Notes = bienDong.Notes
                    };
                    db.WarehouseMovements.Add(entity);

                    db.RecentActivities.Add(new RecentActivity
                    {
                        Title = $"{bienDong.MovementTypeDisplayName}: {bienDong.ItemName}",
                        Description = $"{bienDong.SourceOrDestination} ({bienDong.Weight:N1} kg)",
                        Timestamp = DateTime.Now,
                        Type = bienDong.MovementType == WarehouseMovementType.InboundReceiving ? ActivityType.Import : ActivityType.ShipperAssigned
                    });
                    db.SaveChanges();
                    bienDong.Id = entity.Id;
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddWarehouseMovement EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Xác nhận tiếp nhận đơn hàng vào kho (ghi nhận thời điểm timestamp)
        /// </summary>
        public void XacNhanTiepNhanDonVaoKho(string nguonGui, string maChungTu, double khoiLuong, string viTri, string nguoiTiepNhan, string ghiChu)
        {
            var bienDong = new WarehouseMovement
            {
                MovementType = WarehouseMovementType.InboundReceiving,
                ItemName = $"Tiếp nhận đơn từ {nguonGui}",
                ReferenceCode = maChungTu,
                Weight = khoiLuong,
                SourceOrDestination = $"{nguonGui} -> {viTri}",
                LocationCode = viTri,
                OperatorName = nguoiTiepNhan,
                Notes = ghiChu
            };
            AddWarehouseMovement(bienDong);
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Phân loại hàng giao chặng cuối hoặc hàng trung chuyển
        /// </summary>
        public void XacNhanPhanLoaiKienHang(string maKien, string tenHang, WarehouseMovementType loaiPhanLuong, string viTriMoi, string nguoiPhanLoai, string ghiChu)
        {
            var bienDong = new WarehouseMovement
            {
                MovementType = loaiPhanLuong,
                ItemName = tenHang,
                ReferenceCode = maKien,
                SourceOrDestination = $"Khu phân loại -> {viTriMoi}",
                LocationCode = viTriMoi,
                OperatorName = nguoiPhanLoai,
                Notes = ghiChu
            };
            AddWarehouseMovement(bienDong);
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Phân loại tự động hàng loạt bưu kiện tại sàn Dock (Automated Sorting System)
        /// Dựa trên thuật toán định tuyến địa chỉ (Destination Routing) để tự động phân luồng vào Máng Tuyến hoặc Xe Trung Chuyển.
        /// </summary>
        public (int tongSo, int changCuoi, int trungChuyen) XacNhanPhanLoaiHangLoat(IEnumerable<ShippingOrder> danhSachDon, string nguoiPhanLoai)
        {
            var dsList = danhSachDon?.ToList() ?? new List<ShippingOrder>();
            if (dsList.Count == 0) return (0, 0, 0);

            int soChangCuoi = 0;
            int soTrungChuyen = 0;
            var listMovements = new List<WarehouseMovement>();
            DateTime now = DateTime.Now;

            foreach (var don in dsList)
            {
                bool isLocal = don.IsLocalHubDelivery;
                var loaiPhanLuong = isLocal ? WarehouseMovementType.SortingLastMile : WarehouseMovementType.SortingTransit;
                string viTriMoi = isLocal ? $"Máng Bưu Tá ({don.DestinationArea})" : "DOCK-OUTBOUND (Cửa Xuất Xe Tải)";
                string ghiChu = isLocal 
                    ? $"Hệ thống tự động phân loại chặng cuối nội tỉnh ({don.DestinationArea})" 
                    : $"Hệ thống tự động phân loại trung chuyển liên tỉnh ({don.DestinationArea})";

                if (isLocal) soChangCuoi++;
                else soTrungChuyen++;

                var bienDong = new WarehouseMovement
                {
                    TransactionCode = $"GD-PL-{now:yyMMdd}-{new Random().Next(1000, 9999)}",
                    Timestamp = now,
                    MovementType = loaiPhanLuong,
                    ItemName = don.ProductSummary,
                    ReferenceCode = don.OrderCode,
                    Quantity = 1,
                    Weight = don.Weight,
                    SourceOrDestination = $"Sàn Dock Inbound -> {viTriMoi}",
                    LocationCode = viTriMoi,
                    OperatorName = nguoiPhanLoai,
                    Notes = ghiChu
                };
                listMovements.Add(bienDong);
            }

            lock (_lock)
            {
                int maxId = _warehouseMovements.Count > 0 ? _warehouseMovements.Max(m => m.Id) : 0;
                foreach (var m in listMovements)
                {
                    m.Id = ++maxId;
                    _warehouseMovements.Insert(0, m);
                }

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"⚡ Tự động phân luồng: {dsList.Count} bưu kiện tại Dock",
                    Description = $"{soChangCuoi} chặng cuối bưu tá, {soTrungChuyen} trung chuyển xe tải",
                    Timestamp = now,
                    Type = ActivityType.ShipperAssigned
                });
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    db.WarehouseMovements.AddRange(listMovements);
                    db.RecentActivities.Add(new RecentActivity
                    {
                        Title = $"⚡ Tự động phân luồng: {dsList.Count} bưu kiện tại Dock",
                        Description = $"{soChangCuoi} chặng cuối bưu tá, {soTrungChuyen} trung chuyển xe tải",
                        Timestamp = now,
                        Type = ActivityType.ShipperAssigned
                    });
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[XacNhanPhanLoaiHangLoat EF Core Error] {ex.Message}");
                }
            }

            return (dsList.Count, soChangCuoi, soTrungChuyen);
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Xác nhận xuất kho hàng hóa
        /// </summary>
        public void XacNhanXuatKho(string maDonXuat, string tenNguoiNhan, WarehouseMovementType loaiXuat, double khoiLuong, string nguoiXuat, string ghiChu)
        {
            var bienDong = new WarehouseMovement
            {
                MovementType = loaiXuat,
                ItemName = $"Xuất bưu kiện {maDonXuat}",
                ReferenceCode = maDonXuat,
                Weight = khoiLuong,
                SourceOrDestination = $"Kho -> {tenNguoiNhan}",
                OperatorName = nguoiXuat,
                Notes = ghiChu
            };
            AddWarehouseMovement(bienDong);
        }


        /// <summary>
        /// HÀM NGHIỆP VỤ: Điều chuyển hàng hóa giữa các vị trí ô kệ (Put-away / Internal Relocation)
        /// - Nhiệm vụ: Di dời khối lượng từ kệ nguồn sang kệ đích, cập nhật tải trọng và ghi nhật ký.
        /// </summary>
        public bool DieuChuyenViTriKe(int viTriNguonId, int viTriDichId, double khoiLuongChuyen, string moTaHang, string nguoiThucHien, string ghiChu)
        {
            lock (_lock)
            {
                var nguon = _locations.FirstOrDefault(l => l.Id == viTriNguonId);
                var dich = _locations.FirstOrDefault(l => l.Id == viTriDichId);

                if (nguon == null || dich == null || khoiLuongChuyen <= 0) return false;

                // Trừ tải trọng nguồn
                nguon.CurrentWeight = Math.Max(0, nguon.CurrentWeight - khoiLuongChuyen);
                nguon.Status = nguon.CurrentWeight <= 0 ? LocationStatus.Empty : LocationStatus.PartiallyFull;

                // Cộng tải trọng đích
                dich.CurrentWeight = dich.CurrentWeight + khoiLuongChuyen;
                dich.Status = dich.CurrentWeight >= dich.MaxWeightCapacity ? LocationStatus.Full : LocationStatus.PartiallyFull;

                var maGD = $"GD-DC-{DateTime.Now:yyMMdd}-{new Random().Next(100, 999)}";
                var bienDong = new WarehouseMovement
                {
                    TransactionCode = maGD,
                    Timestamp = DateTime.Now,
                    MovementType = WarehouseMovementType.StockRelocation,
                    ItemName = string.IsNullOrWhiteSpace(moTaHang) ? "Hàng hóa điều chuyển" : moTaHang,
                    ReferenceCode = $"{nguon.LocationCode} -> {dich.LocationCode}",
                    Weight = khoiLuongChuyen,
                    SourceOrDestination = $"{nguon.LocationCode} -> {dich.LocationCode}",
                    LocationCode = dich.LocationCode,
                    OperatorName = string.IsNullOrWhiteSpace(nguoiThucHien) ? "Thủ Kho Hệ Thống" : nguoiThucHien,
                    Notes = $"Điều chuyển {khoiLuongChuyen:N1} kg từ {nguon.LocationCode} sang {dich.LocationCode}. {ghiChu}"
                };

                AddWarehouseMovement(bienDong);

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"Điều chuyển ô kệ: {nguon.LocationCode} ➜ {dich.LocationCode}",
                    Description = $"Khối lượng: {khoiLuongChuyen:N1} kg ({moTaHang})",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.System
                });

                if (IsDatabaseConnected)
                {
                    try
                    {
                        using var db = new WarehouseDbContext();
                        var locNguon = db.WarehouseLocations.Find(viTriNguonId);
                        var locDich = db.WarehouseLocations.Find(viTriDichId);
                        if (locNguon != null)
                        {
                            locNguon.CurrentWeight = nguon.CurrentWeight;
                            locNguon.Status = nguon.Status;
                        }
                        if (locDich != null)
                        {
                            locDich.CurrentWeight = dich.CurrentWeight;
                            locDich.Status = dich.Status;
                        }
                        db.SaveChanges();
                    }
                    catch (Exception ngoaiLe)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DieuChuyenViTriKe EF Core Error] {ngoaiLe.Message}");
                    }
                }

                return true;
            }
        }

        #endregion

        #region Thống kê 7 chỉ số KPI phục vụ Trang Tổng Quan
        /// <summary>
        /// HÀM TỔNG HỢP: Tính toán 7 chỉ số KPI vận hành thời gian thực
        /// - Nhiệm vụ: Trả về một Tuple chứa 7 số liệu thống kê:
        ///   1. Tổng số đơn hàng (Total).
        ///   2. Đơn chờ xử lý (Pending: Chờ xử lý + Mới tiếp nhận).
        ///   3. Đơn hỏa tốc (Express).
        ///   4. Đơn đang giao (Delivering).
        ///   5. Đơn giao thành công (Delivered).
        ///   6. Shipper đang hoạt động (ActiveShippers).
        ///   7. Đơn quá hạn giao dự kiến (Overdue).
        /// - Tương tác dữ liệu: OverviewView.xaml.cs, các TextBlock thẻ KPI.
        /// </summary>
        public (int Total, int Pending, int Express, int Delivering, int Delivered, int ActiveShippers, int Overdue) GetOverviewKpis()
        {
            lock (_lock)
            {
                int tongSoDon = _shippingOrders.Count;
                int donChoXuLy = _shippingOrders.Count(o => o.Status == ShippingOrderStatus.PendingProcessing || o.Status == ShippingOrderStatus.NewReceived);
                int donHoaToc = _shippingOrders.Count(o => o.IsExpress);
                int donDangGiao = _shippingOrders.Count(o => o.Status == ShippingOrderStatus.Delivering);
                int donGiaoThanhCong = _shippingOrders.Count(o => o.Status == ShippingOrderStatus.Delivered);
                int shipperDangHoatDong = _shippers.Count(s => s.Status == ShipperStatus.Active);
                int donQuaHan = _shippingOrders.Count(o => o.IsOverdue);

                return (tongSoDon, donChoXuLy, donHoaToc, donDangGiao, donGiaoThanhCong, shipperDangHoatDong, donQuaHan);
            }
        }
        #endregion

        #region Nghiệp vụ Phân Bổ & Điều Phối Ưu Tiên Năng Lực (Priority Dispatching)
        /// <summary>
        /// HÀM NGHIỆP VỤ: Sinh danh sách đơn hàng mô phỏng kịch bản quá tải kho (ví dụ: 100 đơn với ~35 đơn hỏa tốc)
        /// - Nhiệm vụ: Tạo ra tập dữ liệu chân thực với các đơn Hỏa tốc Express, đơn quá hạn SLA, đơn cận hạn 1-2h và đơn tiêu chuẩn
        /// - Phục vụ: Kiểm thử thuật toán xếp hạng ưu tiên và phân bổ năng lực
        /// </summary>
        /// <summary>
        /// HÀM NGHIỆP VỤ: Sinh 100 đơn hàng mô phỏng kịch bản quá tải tại Thái Nguyên
        /// - Phục vụ kịch bản người dùng: 100 đơn ở Thái Nguyên, năng lực giao 40 đơn, ưu tiên Hỏa Tốc ship trước, đơn thường lưu kho ca sau.
        /// </summary>
        public void GenerateThaiNguyenOrdersForSimulation(int count = 100)
        {
            ResetAndGenerateSimulationOrders("ThaiNguyen", 40);
        }

        public void GenerateSampleOrdersForSimulation(int count = 100)
        {
            ResetAndGenerateSimulationOrders("Hanoi", 50);
        }

        /// <summary>
        /// NGHIỆP VỤ ĐIỀU PHỐI ĐẶC BIỆT: Tự động dọn sạch các đơn thử nghiệm cũ và sinh đúng 100 đơn hàng mô phỏng kịch bản chuẩn
        /// - scenario == "Hanoi": 30 đơn Hỏa Tốc (Express) + 20 đơn Cận hạn/Quá hạn SLA + 50 đơn Tiêu chuẩn an toàn
        /// - scenario == "ThaiNguyen": 25 đơn Hỏa Tốc + 15 đơn Cận hạn/Quá hạn SLA + 60 đơn Tiêu chuẩn an toàn
        /// - Đảm bảo tính toán chính xác 100%: Khi Quota = 50 (Hà Nội) hoặc 40 (Thái Nguyên), 100% đơn Hỏa Tốc được duyệt, không bị đẩy sang lưu kho!
        /// - Đồng bộ cả In-memory và Database (SQL Server).
        /// </summary>
        public void ResetAndGenerateSimulationOrders(string scenario = "Hanoi", int quota = 50)
        {
            var createdOrders = new List<ShippingOrder>();
            lock (_lock)
            {
                // 1. Chỉ dọn dẹp các đơn mô phỏng cũ trong In-memory (TUYỆT ĐỐI BẢO TOÀN toàn bộ đơn hàng thực tế của hệ thống)
                _shippingOrders.RemoveAll(o => 
                    o.OrderCode.StartsWith("LOGIX-EXP-") || 
                    o.OrderCode.StartsWith("LOGIX-STD-") || 
                    o.OrderCode.StartsWith("LOGIX-TN-") || 
                    o.OrderCode.StartsWith("LOGIX-HN-"));

                _warehouseMovements.RemoveAll(m => 
                    m.ReferenceCode != null && (
                        m.ReferenceCode.StartsWith("LOGIX-EXP-") || 
                        m.ReferenceCode.StartsWith("LOGIX-STD-") || 
                        m.ReferenceCode.StartsWith("LOGIX-TN-") || 
                        m.ReferenceCode.StartsWith("LOGIX-HN-")));

                var ngauNhien = new Random();
                int startId = _shippingOrders.Count > 0 ? _shippingOrders.Max(o => o.Id) + 1 : 1;

                if (scenario.Equals("ThaiNguyen", StringComparison.OrdinalIgnoreCase))
                {
                    string[] danhSachKhuVucTN = { "TP. Thái Nguyên", "Sông Công", "Phổ Yên", "Đại Từ", "Phú Bình", "Đồng Hỷ", "Định Hóa", "Võ Nhai" };
                    string[] danhSachNguoiNhan = { 
                        "Nguyễn Hoàng Anh", "Trần Mai Phương", "Lê Văn Tuấn", "Phạm Thu Hương", 
                        "Vũ Quốc Bảo", "Đặng Thị Lan", "Bùi Minh Đức", "Hoàng Kim Ngân", 
                        "Trịnh Xuân Tùng", "Đỗ Quỳnh Chi", "Ngô Thành Đạt", "Lý Gia Hân" 
                    };
                    string[] danhSachHangHoa = {
                        "Chè Thái Nguyên Tân Cương Thượng Hạng", "Điện thoại Samsung Galaxy (KCN Yên Bình, Phổ Yên)", 
                        "Set mỹ phẩm dưỡng da", "Thùng sữa chua Ba Vì", "Áo thun polo thể thao nam", 
                        "Bộ nồi chiên không dầu", "Giày sneaker thời trang", "Tai nghe bluetooth chống ồn", 
                        "Tài liệu hợp đồng khẩn cấp", "Lô dược phẩm y tế", "Chuột gaming không dây"
                    };

                    // Tổng 100 đơn: 25 Hỏa Tốc + 15 Cận hạn SLA + 60 An toàn lưu kho
                    for (int i = 0; i < 100; i++)
                    {
                        int currentId = startId + i;
                        string khuVuc = danhSachKhuVucTN[i % danhSachKhuVucTN.Length];
                        string nguoiNhan = danhSachNguoiNhan[ngauNhien.Next(danhSachNguoiNhan.Length)];
                        string hangHoa = danhSachHangHoa[ngauNhien.Next(danhSachHangHoa.Length)];
                        decimal cod = ngauNhien.Next(1, 20) * 50000;
                        double weight = Math.Round(0.4 + ngauNhien.NextDouble() * 3.5, 1);

                        bool isExpress = i < 25;
                        DateTime hanGiao;
                        string notes;

                        if (isExpress)
                        {
                            // 25 đơn Hỏa tốc: Giao gấp trong 25 phút đến 2.5 giờ
                            int deltaMinutes = 25 + (i * 4);
                            hanGiao = DateTime.Now.AddMinutes(deltaMinutes);
                            notes = "⚡ HỎA TỐC THÁI NGUYÊN (ƯU TIÊN TUYỆT ĐỐI XUẤT BẾN)";
                        }
                        else if (i < 40) // 15 đơn cận hạn SLA (i từ 25 đến 39)
                        {
                            if (i == 25)
                            {
                                // 1 đơn quá hạn nhẹ 10 phút cần cứu nguy khẩn cấp
                                hanGiao = DateTime.Now.AddMinutes(-10);
                                notes = "🚨 Đơn Thái Nguyên trễ SLA 10 phút - Cứu nguy giao ngay";
                            }
                            else
                            {
                                int deltaMinutes = 35 + ((i - 26) * 5);
                                hanGiao = DateTime.Now.AddMinutes(deltaMinutes);
                                notes = "⏱️ Đơn Thái Nguyên cận hạn cam kết SLA - Cần giao ca này";
                            }
                        }
                        else // 60 đơn an toàn lưu kho (i từ 40 đến 99)
                        {
                            int deltaHours = 20 + ((i - 40) % 28);
                            hanGiao = DateTime.Now.AddHours(deltaHours);
                            notes = "📦 Đơn tiêu chuẩn an toàn lưu kho Thái Nguyên (Hạn SLA còn dài)";
                        }

                        var donMoi = new ShippingOrder
                        {
                            Id = currentId,
                            OrderCode = $"LOGIX-TN-{(isExpress ? "EXP" : "STD")}-{DateTime.Now:yyMMdd}-{(2000 + i)}",
                            SenderName = isExpress ? "Kho Hỏa Tốc Hub Thái Nguyên" : "Tổng Kho Vận Thái Nguyên Logistics",
                            SenderPhone = "0280 3855 888",
                            SenderAddress = "Số 168 Đường Hoàng Văn Thụ, TP. Thái Nguyên",
                            ReceiverName = nguoiNhan,
                            ReceiverPhone = $"09{ngauNhien.Next(10000000, 99999999)}",
                            ReceiverAddress = $"Số {ngauNhien.Next(1, 150)} Đường Lương Ngọc Quyến, {khuVuc}, Thái Nguyên",
                            DestinationArea = $"Thái Nguyên - {khuVuc}",
                            ProductSummary = hangHoa,
                            Weight = weight,
                            IsExpress = isExpress,
                            CodAmount = cod,
                            ShippingFee = isExpress ? 25000 : 20000,
                            ExpressSurcharge = isExpress ? 20000 : 0,
                            ReceiverPaysFee = true,
                            Status = ShippingOrderStatus.PendingProcessing,
                            AssignedShipperName = "Chưa phân phối",
                            CreatedDate = DateTime.Now.AddHours(-1 - (i % 8)),
                            EstimatedDeliveryDate = hanGiao,
                            Notes = notes
                        };

                        _shippingOrders.Add(donMoi);
                        createdOrders.Add(donMoi);
                    }
                }
                else
                {
                    // Kịch bản Hà Nội (Chuẩn 100 đơn -> Năng lực 50 đơn)
                    string[] danhSachKhuVucHN = { "Cầu Giấy", "Nam Từ Liêm", "Đống Đa", "Ba Đình", "Hoàn Kiếm", "Thanh Xuân", "Hà Đông", "Hai Bà Trưng" };
                    string[] danhSachNguoiNhan = { 
                        "Nguyễn Hoàng Anh", "Trần Mai Phương", "Lê Văn Tuấn", "Phạm Thu Hương", 
                        "Vũ Quốc Bảo", "Đặng Thị Lan", "Bùi Minh Đức", "Hoàng Kim Ngân", 
                        "Trịnh Xuân Tùng", "Đỗ Quỳnh Chi", "Ngô Thành Đạt", "Lý Gia Hân" 
                    };
                    string[] danhSachHangHoa = {
                        "Điện thoại thông minh Xiaomi Note 13", "Set mỹ phẩm dưỡng trắng da", "Thùng sữa tươi tiệt trùng",
                        "Áo thun polo thể thao nam", "Bộ nồi chiên không dầu", "Giày sneaker thời trang",
                        "Tai nghe bluetooth chống ồn", "Tài liệu hợp đồng khẩn cấp", "Lô dược phẩm y tế", "Chuột không dây gaming"
                    };

                    // Tổng 100 đơn: 30 Hỏa Tốc + 20 Cận hạn SLA + 50 An toàn lưu kho
                    for (int i = 0; i < 100; i++)
                    {
                        int currentId = startId + i;
                        string khuVuc = danhSachKhuVucHN[i % danhSachKhuVucHN.Length];
                        string nguoiNhan = danhSachNguoiNhan[ngauNhien.Next(danhSachNguoiNhan.Length)];
                        string hangHoa = danhSachHangHoa[ngauNhien.Next(danhSachHangHoa.Length)];
                        decimal cod = ngauNhien.Next(1, 30) * 50000;
                        double weight = Math.Round(0.3 + ngauNhien.NextDouble() * 4.5, 1);

                        bool isExpress = i < 30;
                        DateTime hanGiao;
                        string notes;

                        if (isExpress)
                        {
                            // 30 đơn Hỏa tốc: Giao gấp trong 25 phút đến 2.5 giờ
                            int deltaMinutes = 25 + (i * 4);
                            hanGiao = DateTime.Now.AddMinutes(deltaMinutes);
                            notes = "⚡ ƯU TIÊN GIAO GẤP HỎA TỐC (CAM KẾT 2H)";
                        }
                        else if (i < 50) // 20 đơn cận hạn SLA (i từ 30 đến 49)
                        {
                            if (i == 30)
                            {
                                // 1 đơn quá hạn nhẹ 12 phút để cứu nguy
                                hanGiao = DateTime.Now.AddMinutes(-12);
                                notes = "🚨 Đơn trễ SLA 12 phút - Giải cứu khẩn cấp";
                            }
                            else
                            {
                                int deltaMinutes = 35 + ((i - 31) * 4);
                                hanGiao = DateTime.Now.AddMinutes(deltaMinutes);
                                notes = "⏱️ Đơn cận hạn cam kết SLA - Cần giao ca này";
                            }
                        }
                        else // 50 đơn an toàn lưu kho (i từ 50 đến 99)
                        {
                            int deltaHours = 20 + ((i - 50) % 28);
                            hanGiao = DateTime.Now.AddHours(deltaHours);
                            notes = "📦 Đơn tiêu chuẩn an toàn lưu kho (Hạn SLA còn 24h-48h)";
                        }

                        var donMoi = new ShippingOrder
                        {
                            Id = currentId,
                            OrderCode = $"LOGIX-HN-{(isExpress ? "EXP" : "STD")}-{DateTime.Now:yyMMdd}-{(1000 + i)}",
                            SenderName = isExpress ? "Trung Tâm Phân Phối Hỏa Tốc Hà Nội" : "Kho Vận Tổng Hợp Hà Nội",
                            SenderPhone = "024 3888 9999",
                            SenderAddress = "Kho Tổng WMS Logistics, Hà Nội",
                            ReceiverName = nguoiNhan,
                            ReceiverPhone = $"09{ngauNhien.Next(10000000, 99999999)}",
                            ReceiverAddress = $"Số {ngauNhien.Next(1, 150)} Phố {khuVuc}, Hà Nội",
                            DestinationArea = khuVuc,
                            ProductSummary = hangHoa,
                            Weight = weight,
                            IsExpress = isExpress,
                            CodAmount = cod,
                            ShippingFee = isExpress ? 25000 : 20000,
                            ExpressSurcharge = isExpress ? 20000 : 0,
                            ReceiverPaysFee = true,
                            Status = ShippingOrderStatus.PendingProcessing,
                            AssignedShipperName = "Chưa phân phối",
                            CreatedDate = DateTime.Now.AddHours(-1 - (i % 6)),
                            EstimatedDeliveryDate = hanGiao,
                            Notes = notes
                        };

                        _shippingOrders.Add(donMoi);
                        createdOrders.Add(donMoi);
                    }
                }

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"Khởi tạo 100 đơn mô phỏng kịch bản {scenario}",
                    Description = $"Chuẩn hóa 100 đơn hàng: {scenario} (Đủ hạn mức phân bổ theo ma trận SLA)",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.OrderSuccess
                });
            }

            // Đồng bộ xuống CSDL SQL Server qua EF Core
            if (IsDatabaseConnected && createdOrders.Count > 0)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    // Xóa các đơn mô phỏng cũ
                    var oldDbSimOrders = db.ShippingOrders.Where(o => 
                        o.OrderCode.StartsWith("LOGIX-EXP-") || 
                        o.OrderCode.StartsWith("LOGIX-STD-") || 
                        o.OrderCode.StartsWith("LOGIX-TN-") || 
                        o.OrderCode.StartsWith("LOGIX-HN-")).ToList();
                    if (oldDbSimOrders.Count > 0)
                    {
                        db.ShippingOrders.RemoveRange(oldDbSimOrders);
                    }

                    var oldMovements = db.WarehouseMovements.Where(m => 
                        m.ReferenceCode != null && (
                            m.ReferenceCode.StartsWith("LOGIX-EXP-") || 
                            m.ReferenceCode.StartsWith("LOGIX-STD-") || 
                            m.ReferenceCode.StartsWith("LOGIX-TN-") || 
                            m.ReferenceCode.StartsWith("LOGIX-HN-"))).ToList();
                    if (oldMovements.Count > 0)
                    {
                        db.WarehouseMovements.RemoveRange(oldMovements);
                    }

                    // 2. Thêm 100 đơn mới của kịch bản mô phỏng (TUYỆT ĐỐI KHÔNG ẢNH HƯỞNG đến các đơn hàng thực tế khác)

                    // Thêm 100 đơn mới
                    foreach (var don in createdOrders)
                    {
                        db.ShippingOrders.Add(new ShippingOrder
                        {
                            OrderCode = don.OrderCode,
                            SenderName = don.SenderName,
                            SenderPhone = don.SenderPhone,
                            SenderAddress = don.SenderAddress,
                            ReceiverName = don.ReceiverName,
                            ReceiverPhone = don.ReceiverPhone,
                            ReceiverAddress = don.ReceiverAddress,
                            DestinationArea = don.DestinationArea,
                            ProductSummary = don.ProductSummary,
                            Weight = don.Weight,
                            IsExpress = don.IsExpress,
                            CodAmount = don.CodAmount,
                            ShippingFee = don.ShippingFee,
                            ExpressSurcharge = don.ExpressSurcharge,
                            ReceiverPaysFee = don.ReceiverPaysFee,
                            Status = don.Status,
                            AssignedShipperName = don.AssignedShipperName,
                            CreatedDate = don.CreatedDate,
                            EstimatedDeliveryDate = don.EstimatedDeliveryDate,
                            Notes = don.Notes
                        });
                    }

                    db.RecentActivities.Add(new RecentActivity
                    {
                        Title = $"Khởi tạo 100 đơn mô phỏng kịch bản {scenario}",
                        Description = $"Chuẩn hóa 100 đơn hàng: {scenario} (Đủ hạn mức phân bổ theo ma trận SLA)",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.OrderSuccess
                    });

                    db.SaveChanges();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[ResetAndGenerateSimulationOrders EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Phê duyệt xuất kho hàng loạt và tự động phân công cho các Shipper phù hợp theo khu vực
        /// </summary>
        public int BatchAssignOrdersToShippers(List<int> danhSachMaDon)
        {
            int demThanhCong = 0;
            lock (_lock)
            {
                var danhSachShipperKhaDung = _shippers.Where(s => !s.IsLocked && (s.Status == ShipperStatus.Active || s.Status == ShipperStatus.Available)).ToList();
                if (danhSachShipperKhaDung.Count == 0)
                {
                    danhSachShipperKhaDung = _shippers.Where(s => !s.IsLocked).ToList();
                }

                int chiSoShipper = 0;

                foreach (var id in danhSachMaDon)
                {
                    var don = _shippingOrders.FirstOrDefault(o => o.Id == id);
                    if (don != null)
                    {
                        don.Status = ShippingOrderStatus.Delivering;

                        // Tìm shipper cùng khu vực nếu có
                        var shipperPhuHop = danhSachShipperKhaDung.FirstOrDefault(s => s.DeliveryArea.Contains(don.DestinationArea, StringComparison.OrdinalIgnoreCase));
                        if (shipperPhuHop == null && danhSachShipperKhaDung.Count > 0)
                        {
                            shipperPhuHop = danhSachShipperKhaDung[chiSoShipper % danhSachShipperKhaDung.Count];
                            chiSoShipper++;
                        }

                        if (shipperPhuHop != null)
                        {
                            don.AssignedShipperId = shipperPhuHop.Id;
                            don.AssignedShipperName = shipperPhuHop.FullName;
                            don.ShipperPhone = shipperPhuHop.Phone;
                            shipperPhuHop.Status = ShipperStatus.Active;
                        }

                        demThanhCong++;
                    }
                }

                if (demThanhCong > 0)
                {
                    _recentActivities.Insert(0, new RecentActivity
                    {
                        Id = _recentActivities.Count + 1,
                        Title = $"Phê duyệt điều phối {demThanhCong} đơn hàng ưu tiên",
                        Description = "Đã xuất kho và phân công tự động cho đội ngũ Shipper",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.ShipperAssigned
                    });
                }
            }

            if (IsDatabaseConnected && demThanhCong > 0)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var dsDonDb = db.ShippingOrders.Where(o => danhSachMaDon.Contains(o.Id)).ToList();
                    foreach (var donDb in dsDonDb)
                    {
                        var donMem = _shippingOrders.FirstOrDefault(o => o.Id == donDb.Id);
                        if (donMem != null)
                        {
                            donDb.Status = ShippingOrderStatus.Delivering;
                            donDb.AssignedShipperId = donMem.AssignedShipperId;
                            donDb.AssignedShipperName = donMem.AssignedShipperName;
                            donDb.ShipperPhone = donMem.ShipperPhone;
                        }
                    }

                    db.RecentActivities.Add(new RecentActivity
                    {
                        Title = $"Phê duyệt điều phối {demThanhCong} đơn hàng ưu tiên",
                        Description = "Đã xuất kho và phân công tự động cho đội ngũ Shipper",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.ShipperAssigned
                    });
                    db.SaveChanges();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[BatchAssignOrdersToShippers EF Core Error] {ngoaiLe.Message}");
                }
            }

            return demThanhCong;
        }
        #endregion

        #region Phân Hệ 5: Điều Phối Giao Hàng (Dispatch Management)
        /// <summary>
        /// Lấy toàn bộ danh sách các phiên/chuyến điều phối giao hàng
        /// </summary>
        public IReadOnlyList<DispatchRecord> GetAllDispatchRecords()
        {
            lock (_lock)
            {
                return _dispatchRecords.OrderByDescending(r => r.CreatedTime).ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Lấy danh sách các đơn hàng theo danh sách mã ID chỉ định
        /// </summary>
        public IReadOnlyList<ShippingOrder> GetOrdersByIds(IEnumerable<int> danhSachId)
        {
            lock (_lock)
            {
                var tapHopId = new HashSet<int>(danhSachId);
                return _shippingOrders.Where(o => tapHopId.Contains(o.Id)).ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Tạo mới phiên điều phối giao hàng, gán Shipper cho các đơn và chuyển trạng thái sang Delivering
        /// </summary>
        public DispatchRecord CreateDispatchRecord(int shipperId, string shipperName, string shipperPhone, string vehiclePlate, string deliveryArea, DateTime ngayGiao, List<int> maDonHangs, string nguoiDieuPhoi, string ghiChu)
        {
            lock (_lock)
            {
                var donHangs = _shippingOrders.Where(o => maDonHangs.Contains(o.Id)).ToList();
                int soDonHoaToc = donHangs.Count(o => o.IsExpress);
                decimal tongCod = donHangs.Sum(o => o.CodAmount);
                double tongKhoiLuong = donHangs.Sum(o => o.Weight);

                string maChuyen = $"DP-{ngayGiao:yyMMdd}-{(_dispatchRecords.Count + 1):D3}";

                var banGhi = new DispatchRecord
                {
                    Id = _dispatchRecords.Count + 1,
                    DispatchCode = maChuyen,
                    DispatchDate = ngayGiao,
                    CreatedTime = DateTime.Now,
                    ShipperId = shipperId,
                    ShipperName = shipperName,
                    ShipperPhone = shipperPhone,
                    VehiclePlate = vehiclePlate,
                    DeliveryArea = deliveryArea,
                    TotalOrders = donHangs.Count,
                    ExpressOrdersCount = soDonHoaToc,
                    TotalCodAmount = tongCod,
                    TotalWeight = Math.Round(tongKhoiLuong, 1),
                    DispatcherName = nguoiDieuPhoi,
                    Status = "Đang Đi Giao",
                    Notes = ghiChu,
                    OrderIds = maDonHangs.ToList()
                };

                // Cập nhật từng đơn hàng được gán
                foreach (var don in donHangs)
                {
                    don.Status = ShippingOrderStatus.Delivering;
                    don.AssignedShipperId = shipperId;
                    don.AssignedShipperName = shipperName;
                    don.ShipperPhone = shipperPhone;
                }

                // Cập nhật trạng thái shipper sang Active và tăng số đơn
                var shipper = _shippers.FirstOrDefault(s => s.Id == shipperId);
                if (shipper != null)
                {
                    shipper.Status = ShipperStatus.Active;
                }

                _dispatchRecords.Insert(0, banGhi);

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"Lập lệnh điều phối {maChuyen} cho {shipperName}",
                    Description = $"Số lượng: {donHangs.Count} đơn (Có {soDonHoaToc} Express) - COD: {tongCod:N0}đ",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.ShipperAssigned
                });

                if (IsDatabaseConnected)
                {
                    try
                    {
                        using var db = new WarehouseDbContext();
                        var entity = new DispatchRecord
                        {
                            DispatchCode = maChuyen,
                            DispatchDate = ngayGiao,
                            CreatedTime = DateTime.Now,
                            ShipperId = shipperId,
                            ShipperName = shipperName,
                            ShipperPhone = shipperPhone,
                            VehiclePlate = vehiclePlate,
                            DeliveryArea = deliveryArea,
                            TotalOrders = donHangs.Count,
                            ExpressOrdersCount = soDonHoaToc,
                            TotalCodAmount = tongCod,
                            TotalWeight = Math.Round(tongKhoiLuong, 1),
                            DispatcherName = nguoiDieuPhoi,
                            Status = "Đang Đi Giao",
                            Notes = ghiChu,
                            OrderIds = maDonHangs.ToList()
                        };
                        db.DispatchRecords.Add(entity);

                        var dsDonDb = db.ShippingOrders.Where(o => maDonHangs.Contains(o.Id)).ToList();
                        foreach (var don in dsDonDb)
                        {
                            don.Status = ShippingOrderStatus.Delivering;
                            don.AssignedShipperId = shipperId;
                            don.AssignedShipperName = shipperName;
                            don.ShipperPhone = shipperPhone;
                        }

                        var dbShipper = db.Shippers.Find(shipperId);
                        if (dbShipper != null)
                        {
                            dbShipper.Status = ShipperStatus.Active;
                        }

                        db.RecentActivities.Add(new RecentActivity
                        {
                            Title = $"Lập lệnh điều phối {maChuyen} cho {shipperName}",
                            Description = $"Số lượng: {donHangs.Count} đơn (Có {soDonHoaToc} Express) - COD: {tongCod:N0}đ",
                            Timestamp = DateTime.Now,
                            Type = ActivityType.ShipperAssigned
                        });

                        db.SaveChanges();
                        banGhi.Id = entity.Id;
                    }
                    catch (Exception ngoaiLe)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CreateDispatchRecord EF Core Error] {ngoaiLe.Message}");
                    }
                }

                return banGhi;
            }
        }
        #endregion

        #region Phân Hệ Quản Lý Hàng Hoàn & Xử Lý Giao Thất Bại (Reverse Logistics & RTO)
        /// <summary>
        /// Lấy toàn bộ danh sách các biên bản bàn giao hàng hoàn trả cho Shop
        /// </summary>
        public IReadOnlyList<ReturnHandoverBatch> GetAllReturnBatches()
        {
            lock (_lock) return _returnBatches.OrderByDescending(b => b.CreatedTime).ToList().AsReadOnly();
        }

        /// <summary>
        /// Ghi nhận giao hàng thất bại (Tự động tăng số lần thất bại, cập nhật lý do và mốc thời gian)
        /// </summary>
        public void MarkOrderAsFailed(int orderId, string reason, string notes)
        {
            lock (_lock)
            {
                var don = _shippingOrders.FirstOrDefault(o => o.Id == orderId);
                if (don != null)
                {
                    don.FailedDeliveryCount++;
                    don.Status = ShippingOrderStatus.Failed;
                    don.FailureReason = reason;
                    don.FailureTimestamp = DateTime.Now;
                    if (!string.IsNullOrWhiteSpace(notes))
                    {
                        don.Notes = string.IsNullOrWhiteSpace(don.Notes) ? notes : $"{don.Notes} | {notes}";
                    }

                    _recentActivities.Insert(0, new RecentActivity
                    {
                        Id = _recentActivities.Count + 1,
                        Title = $"Đơn {don.OrderCode} giao thất bại (Lần {don.FailedDeliveryCount})",
                        Description = $"Lý do: {reason}",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.OrderFailed
                    });
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var don = db.ShippingOrders.Find(orderId);
                    if (don != null)
                    {
                        don.FailedDeliveryCount++;
                        don.Status = ShippingOrderStatus.Failed;
                        don.FailureReason = reason;
                        don.FailureTimestamp = DateTime.Now;
                        if (!string.IsNullOrWhiteSpace(notes))
                        {
                            don.Notes = string.IsNullOrWhiteSpace(don.Notes) ? notes : $"{don.Notes} | {notes}";
                        }

                        db.RecentActivities.Add(new RecentActivity
                        {
                            Title = $"Đơn {don.OrderCode} giao thất bại (Lần {don.FailedDeliveryCount})",
                            Description = $"Lý do: {reason}",
                            Timestamp = DateTime.Now,
                            Type = ActivityType.OrderFailed
                        });
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[MarkOrderAsFailed EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// Lên lịch phát lại đơn hàng (Re-delivery) vào ngày giờ chỉ định
        /// </summary>
        public void RescheduleOrder(int orderId, DateTime newDeliveryDate, string notes)
        {
            lock (_lock)
            {
                var don = _shippingOrders.FirstOrDefault(o => o.Id == orderId);
                if (don != null)
                {
                    don.Status = ShippingOrderStatus.PendingProcessing;
                    don.EstimatedDeliveryDate = newDeliveryDate;
                    don.Notes = string.IsNullOrWhiteSpace(don.Notes) 
                        ? $"Hẹn phát lại: {newDeliveryDate:dd/MM/yyyy HH:mm} ({notes})" 
                        : $"{don.Notes} | Hẹn phát lại: {newDeliveryDate:dd/MM/yyyy HH:mm} ({notes})";

                    _recentActivities.Insert(0, new RecentActivity
                    {
                        Id = _recentActivities.Count + 1,
                        Title = $"Hẹn lịch phát lại đơn {don.OrderCode}",
                        Description = $"Ngày hẹn mới: {newDeliveryDate:dd/MM/yyyy HH:mm} - {notes}",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.Warning
                    });
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var don = db.ShippingOrders.Find(orderId);
                    if (don != null)
                    {
                        don.Status = ShippingOrderStatus.PendingProcessing;
                        don.EstimatedDeliveryDate = newDeliveryDate;
                        don.Notes = string.IsNullOrWhiteSpace(don.Notes) 
                            ? $"Hẹn phát lại: {newDeliveryDate:dd/MM/yyyy HH:mm} ({notes})" 
                            : $"{don.Notes} | Hẹn phát lại: {newDeliveryDate:dd/MM/yyyy HH:mm} ({notes})";

                        db.RecentActivities.Add(new RecentActivity
                        {
                            Title = $"Hẹn lịch phát lại đơn {don.OrderCode}",
                            Description = $"Ngày hẹn mới: {newDeliveryDate:dd/MM/yyyy HH:mm} - {notes}",
                            Timestamp = DateTime.Now,
                            Type = ActivityType.Warning
                        });
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[RescheduleOrder EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// Phê duyệt chuyển hoàn hàng về cho Shop (RTO Approved)
        /// </summary>
        public void ApproveRto(int orderId, string rtoLocation, decimal returnFee, string notes)
        {
            lock (_lock)
            {
                var don = _shippingOrders.FirstOrDefault(o => o.Id == orderId);
                if (don != null)
                {
                    don.Status = ShippingOrderStatus.Returned;
                    don.RtoApprovedDate = DateTime.Now;
                    don.RtoLocationCode = string.IsNullOrWhiteSpace(rtoLocation) ? "KHO-RTO-01" : rtoLocation;
                    don.ReturnShippingFee = returnFee;
                    don.RtoTrackingCode = $"RTO-{DateTime.Now:yyMMdd}-{don.Id:D4}";
                    if (!string.IsNullOrWhiteSpace(notes))
                    {
                        don.Notes = string.IsNullOrWhiteSpace(don.Notes) ? $"Duyệt hoàn: {notes}" : $"{don.Notes} | Duyệt hoàn: {notes}";
                    }

                    _recentActivities.Insert(0, new RecentActivity
                    {
                        Id = _recentActivities.Count + 1,
                        Title = $"Duyệt chuyển hoàn đơn {don.OrderCode}",
                        Description = $"Mã vận đơn hoàn: {don.RtoTrackingCode} - Lưu tại: {don.RtoLocationCode}",
                        Timestamp = DateTime.Now,
                        Type = ActivityType.OrderFailed
                    });
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var don = db.ShippingOrders.Find(orderId);
                    if (don != null)
                    {
                        don.Status = ShippingOrderStatus.Returned;
                        don.RtoApprovedDate = DateTime.Now;
                        don.RtoLocationCode = string.IsNullOrWhiteSpace(rtoLocation) ? "KHO-RTO-01" : rtoLocation;
                        don.ReturnShippingFee = returnFee;
                        don.RtoTrackingCode = $"RTO-{DateTime.Now:yyMMdd}-{don.Id:D4}";
                        if (!string.IsNullOrWhiteSpace(notes))
                        {
                            don.Notes = string.IsNullOrWhiteSpace(don.Notes) ? $"Duyệt hoàn: {notes}" : $"{don.Notes} | Duyệt hoàn: {notes}";
                        }

                        db.RecentActivities.Add(new RecentActivity
                        {
                            Title = $"Duyệt chuyển hoàn đơn {don.OrderCode}",
                            Description = $"Mã vận đơn hoàn: {don.RtoTrackingCode} - Lưu tại: {don.RtoLocationCode}",
                            Timestamp = DateTime.Now,
                            Type = ActivityType.OrderFailed
                        });
                        db.SaveChanges();
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[ApproveRto EF Core Error] {ngoaiLe.Message}");
                }
            }
        }

        /// <summary>
        /// Tạo biên bản bàn giao hàng hoàn trả cho Shop
        /// </summary>
        public ReturnHandoverBatch CreateReturnHandoverBatch(string senderName, string senderPhone, string senderAddress, List<int> orderIds, string operatorName, string notes)
        {
            lock (_lock)
            {
                var donHoans = _shippingOrders.Where(o => orderIds.Contains(o.Id)).ToList();
                decimal tongCod = donHoans.Sum(o => o.CodAmount);
                decimal tongCuocHoan = donHoans.Sum(o => o.ReturnShippingFee);

                string maBienBan = $"BBH-{DateTime.Now:yyMMdd}-{(_returnBatches.Count + 1):D3}";

                var bienBan = new ReturnHandoverBatch
                {
                    Id = _returnBatches.Count + 1,
                    BatchCode = maBienBan,
                    SenderName = senderName,
                    SenderPhone = senderPhone,
                    SenderAddress = senderAddress,
                    CreatedTime = DateTime.Now,
                    TotalOrders = donHoans.Count,
                    TotalCodValue = tongCod,
                    TotalReturnFee = tongCuocHoan,
                    OperatorName = operatorName,
                    Status = "Đã Trả Shop",
                    Notes = notes,
                    OrderIds = orderIds.ToList()
                };

                foreach (var don in donHoans)
                {
                    don.ReturnHandoverBatchCode = maBienBan;
                }

                _returnBatches.Insert(0, bienBan);

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"Lập biên bản hoàn trả {maBienBan} cho {senderName}",
                    Description = $"Số lượng: {donHoans.Count} đơn - Phí hoàn: {tongCuocHoan:N0}đ",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.OrderSuccess
                });

                if (IsDatabaseConnected)
                {
                    try
                    {
                        using var db = new WarehouseDbContext();
                        var entity = new ReturnHandoverBatch
                        {
                            BatchCode = maBienBan,
                            SenderName = senderName,
                            SenderPhone = senderPhone,
                            SenderAddress = senderAddress,
                            CreatedTime = DateTime.Now,
                            TotalOrders = donHoans.Count,
                            TotalCodValue = tongCod,
                            TotalReturnFee = tongCuocHoan,
                            OperatorName = operatorName,
                            Status = "Đã Trả Shop",
                            Notes = notes,
                            OrderIds = orderIds.ToList()
                        };
                        db.ReturnHandoverBatches.Add(entity);

                        var dbOrders = db.ShippingOrders.Where(o => orderIds.Contains(o.Id)).ToList();
                        foreach (var don in dbOrders)
                        {
                            don.ReturnHandoverBatchCode = maBienBan;
                        }

                        db.RecentActivities.Add(new RecentActivity
                        {
                            Title = $"Lập biên bản hoàn trả {maBienBan} cho {senderName}",
                            Description = $"Số lượng: {donHoans.Count} đơn - Phí hoàn: {tongCuocHoan:N0}đ",
                            Timestamp = DateTime.Now,
                            Type = ActivityType.OrderSuccess
                        });

                        db.SaveChanges();
                        bienBan.Id = entity.Id;
                    }
                    catch (Exception ngoaiLe)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CreateReturnHandoverBatch EF Core Error] {ngoaiLe.Message}");
                    }
                }

                return bienBan;
            }
        }

        /// <summary>
        /// Khởi tạo dữ liệu kiểm thử thực tế cho phân hệ Hàng Hoàn & Giao Thất Bại
        /// </summary>
        public void SeedSampleRtoData()
        {
            lock (_lock)
            {
                var don9 = _shippingOrders.FirstOrDefault(o => o.Id == 9);
                if (don9 != null)
                {
                    don9.Status = ShippingOrderStatus.Failed;
                    don9.FailedDeliveryCount = 1;
                    don9.FailureReason = "Khách hẹn lại sau 18h tối";
                    don9.FailureTimestamp = DateTime.Now.AddHours(-3);
                }

                var don10 = _shippingOrders.FirstOrDefault(o => o.Id == 10);
                if (don10 != null)
                {
                    don10.Status = ShippingOrderStatus.Returned;
                    don10.FailedDeliveryCount = 3;
                    don10.FailureReason = "Khách từ chối nhận (Boom hàng / Không đúng màu)";
                    don10.FailureTimestamp = DateTime.Now.AddDays(-1);
                    don10.RtoTrackingCode = "RTO-260924-0010";
                    don10.RtoLocationCode = "KHO-RTO-01";
                    don10.RtoApprovedDate = DateTime.Now.AddHours(-5);
                    don10.ReturnShippingFee = 15000;
                    don10.ReturnHandoverBatchCode = "BBH-260925-001";
                }

                if (!_shippingOrders.Any(o => o.OrderCode == "LOGIX-FAIL-01"))
                {
                    _shippingOrders.Add(new ShippingOrder
                    {
                        Id = _shippingOrders.Max(o => o.Id) + 1,
                        OrderCode = "LOGIX-FAIL-01",
                        SenderName = "Shop Thời Trang GenZ Official",
                        SenderPhone = "0944555666",
                        SenderAddress = "Số 88 Cầu Giấy, Hà Nội",
                        ReceiverName = "Vũ Đình Duy",
                        ReceiverPhone = "0988776655",
                        ReceiverAddress = "Tòa Keangnam Landmark 72, Nam Từ Liêm, Hà Nội",
                        DestinationArea = "Nam Từ Liêm",
                        ProductSummary = "Áo bomber nỉ lót lông",
                        Weight = 0.9,
                        IsExpress = true,
                        CodAmount = 650000,
                        Status = ShippingOrderStatus.Failed,
                        FailedDeliveryCount = 2,
                        FailureReason = "Khách không nghe máy (Gọi 3 cuộc không liên lạc được)",
                        FailureTimestamp = DateTime.Now.AddHours(-1),
                        AssignedShipperName = "Trần Đình Trọng",
                        EstimatedDeliveryDate = DateTime.Now.AddHours(-2)
                    });
                }

                if (!_shippingOrders.Any(o => o.OrderCode == "LOGIX-FAIL-02"))
                {
                    _shippingOrders.Add(new ShippingOrder
                    {
                        Id = _shippingOrders.Max(o => o.Id) + 1,
                        OrderCode = "LOGIX-FAIL-02",
                        SenderName = "Trung Tâm Phân Phối Mỹ Phẩm Korea",
                        SenderPhone = "0934889900",
                        SenderAddress = "Số 18 Lý Thường Kiệt, Hoàn Kiếm, Hà Nội",
                        ReceiverName = "Hoàng Kim Ngân",
                        ReceiverPhone = "0977443322",
                        ReceiverAddress = "Ngõ 45 Chùa Láng, Đống Đa, Hà Nội",
                        DestinationArea = "Đống Đa",
                        ProductSummary = "Set mỹ phẩm dưỡng trắng da",
                        Weight = 0.7,
                        IsExpress = false,
                        CodAmount = 1250000,
                        Status = ShippingOrderStatus.Failed,
                        FailedDeliveryCount = 3,
                        FailureReason = "Sai địa chỉ / Không tìm thấy số nhà (Khách đã chuyển trọ)",
                        FailureTimestamp = DateTime.Now.AddHours(-4),
                        AssignedShipperName = "Nguyễn Văn Tuấn",
                        EstimatedDeliveryDate = DateTime.Now.AddHours(-6)
                    });
                }
            }

            if (IsDatabaseConnected)
            {
                try
                {
                    using var db = new WarehouseDbContext();
                    var dbDon9 = db.ShippingOrders.Find(9);
                    if (dbDon9 != null)
                    {
                        dbDon9.Status = ShippingOrderStatus.Failed;
                        dbDon9.FailedDeliveryCount = 1;
                        dbDon9.FailureReason = "Khách hẹn lại sau 18h tối";
                        dbDon9.FailureTimestamp = DateTime.Now.AddHours(-3);
                    }

                    var dbDon10 = db.ShippingOrders.Find(10);
                    if (dbDon10 != null)
                    {
                        dbDon10.Status = ShippingOrderStatus.Returned;
                        dbDon10.FailedDeliveryCount = 3;
                        dbDon10.FailureReason = "Khách từ chối nhận (Boom hàng / Không đúng màu)";
                        dbDon10.FailureTimestamp = DateTime.Now.AddDays(-1);
                        dbDon10.RtoTrackingCode = "RTO-260924-0010";
                        dbDon10.RtoLocationCode = "KHO-RTO-01";
                        dbDon10.RtoApprovedDate = DateTime.Now.AddHours(-5);
                        dbDon10.ReturnShippingFee = 15000;
                        dbDon10.ReturnHandoverBatchCode = "BBH-260925-001";
                    }

                    if (!db.ShippingOrders.Any(o => o.OrderCode == "LOGIX-FAIL-01"))
                    {
                        db.ShippingOrders.Add(new ShippingOrder
                        {
                            OrderCode = "LOGIX-FAIL-01",
                            SenderName = "Shop Thời Trang GenZ Official",
                            SenderPhone = "0944555666",
                            SenderAddress = "Số 88 Cầu Giấy, Hà Nội",
                            ReceiverName = "Vũ Đình Duy",
                            ReceiverPhone = "0988776655",
                            ReceiverAddress = "Tòa Keangnam Landmark 72, Nam Từ Liêm, Hà Nội",
                            DestinationArea = "Nam Từ Liêm",
                            ProductSummary = "Áo bomber nỉ lót lông",
                            Weight = 0.9,
                            IsExpress = true,
                            CodAmount = 650000,
                            Status = ShippingOrderStatus.Failed,
                            FailedDeliveryCount = 2,
                            FailureReason = "Khách không nghe máy (Gọi 3 cuộc không liên lạc được)",
                            FailureTimestamp = DateTime.Now.AddHours(-1),
                            AssignedShipperName = "Trần Đình Trọng",
                            EstimatedDeliveryDate = DateTime.Now.AddHours(-2)
                        });
                    }

                    if (!db.ShippingOrders.Any(o => o.OrderCode == "LOGIX-FAIL-02"))
                    {
                        db.ShippingOrders.Add(new ShippingOrder
                        {
                            OrderCode = "LOGIX-FAIL-02",
                            SenderName = "Trung Tâm Phân Phối Mỹ Phẩm Korea",
                            SenderPhone = "0934889900",
                            SenderAddress = "Số 18 Lý Thường Kiệt, Hoàn Kiếm, Hà Nội",
                            ReceiverName = "Hoàng Kim Ngân",
                            ReceiverPhone = "0977443322",
                            ReceiverAddress = "Ngõ 45 Chùa Láng, Đống Đa, Hà Nội",
                            DestinationArea = "Đống Đa",
                            ProductSummary = "Set mỹ phẩm dưỡng trắng da",
                            Weight = 0.7,
                            IsExpress = false,
                            CodAmount = 1250000,
                            Status = ShippingOrderStatus.Failed,
                            FailedDeliveryCount = 3,
                            FailureReason = "Sai địa chỉ / Không tìm thấy số nhà (Khách đã chuyển trọ)",
                            FailureTimestamp = DateTime.Now.AddHours(-4),
                            AssignedShipperName = "Nguyễn Văn Tuấn",
                            EstimatedDeliveryDate = DateTime.Now.AddHours(-6)
                        });
                    }

                    db.SaveChanges();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[SeedSampleRtoData EF Core Error] {ngoaiLe.Message}");
                }
            }
        }
        #endregion
    }
}
