using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// Ngữ cảnh dữ liệu kho hàng Logistics (Data Context).
    /// Kết nối trực tiếp vào cơ sở dữ liệu SQL Server (Database: quanlykho)
    /// và đồng bộ lưu trữ dữ liệu vĩnh viễn trong CSDL.
    /// </summary>
    public class WarehouseContext
    {
        private static readonly Lazy<WarehouseContext> _instance = new(() => new WarehouseContext());
        public static WarehouseContext Instance => _instance.Value;

        /// <summary>
        /// Chuỗi kết nối tới cơ sở dữ liệu SQL Server trên máy cục bộ
        /// </summary>
        public string ConnectionString { get; set; } = 
            "Server=localhost;Database=quanlykho;Trusted_Connection=True;TrustServerCertificate=True;";

        /// <summary>
        /// Trạng thái kết nối cơ sở dữ liệu SQL Server thực tế
        /// </summary>
        public bool IsDatabaseConnected { get; private set; } = false;

        public string ConnectedDatabaseName { get; private set; } = "quanlykho";
        public string ConnectionStatusMessage { get; private set; } = string.Empty;

        private readonly List<User> _users = new();
        private readonly List<Product> _products = new();
        private readonly List<WarehouseLocation> _locations = new();
        private readonly List<Inventory> _inventories = new();
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

        #region Khởi tạo & Đồng bộ CSDL SQL Server
        /// <summary>
        /// Kết nối SQL Server, đảm bảo các bảng cần thiết tồn tại và đồng bộ dữ liệu
        /// </summary>
        public void InitializeDatabase()
        {
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                conn.Open();

                IsDatabaseConnected = true;
                ConnectionStatusMessage = "Kết nối SQL Server thành công (Database: quanlykho)";

                // 1. Đảm bảo cấu trúc các bảng tồn tại (DDL)
                EnsureTablesExist(conn);

                // 2. Đồng bộ người dùng từ bảng _users
                SyncUsersFromDatabase(conn);

                // 3. Đồng bộ danh sách đơn vận chuyển từ bảng ShippingOrders
                SyncShippingOrdersFromDatabase(conn);

                // 4. Đồng bộ danh sách phiếu nhập kho từ bảng ImportOrders
                SyncImportOrdersFromDatabase(conn);

                // 5. Đồng bộ danh sách shipper từ bảng Shippers
                SyncShippersFromDatabase(conn);

                // 6. Đồng bộ nhật ký hoạt động từ bảng RecentActivities
                SyncActivitiesFromDatabase(conn);

                // 7. Đồng bộ danh mục sản phẩm từ bảng product
                SyncProductsFromDatabase(conn);
            }
            catch (Exception ex)
            {
                IsDatabaseConnected = false;
                ConnectionStatusMessage = $"Không thể kết nối SQL Server: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[WarehouseContext SQL Error] {ex.Message}");
            }
        }

        private void EnsureTablesExist(SqlConnection conn)
        {
            const string ddl = @"
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
                    Notes NVARCHAR(500) NULL
                );
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImportOrders')
            BEGIN
                CREATE TABLE ImportOrders (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ImportCode NVARCHAR(50) NOT NULL,
                    SourceType INT NOT NULL DEFAULT 0,
                    SourceTypeName NVARCHAR(100) NOT NULL,
                    SenderName NVARCHAR(100) NOT NULL,
                    WaybillNumber NVARCHAR(50) NULL,
                    VehicleNumber NVARCHAR(50) NULL,
                    TotalWeight FLOAT NOT NULL DEFAULT 0,
                    Status INT NOT NULL DEFAULT 0,
                    StatusDisplayName NVARCHAR(100) NOT NULL,
                    Notes NVARCHAR(500) NULL,
                    CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Shippers')
            BEGIN
                CREATE TABLE Shippers (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    FullName NVARCHAR(100) NOT NULL,
                    PhoneNumber NVARCHAR(20) NOT NULL,
                    VehicleType NVARCHAR(50) NOT NULL,
                    LicensePlate NVARCHAR(20) NOT NULL,
                    CurrentArea NVARCHAR(100) NOT NULL,
                    Status INT NOT NULL DEFAULT 0,
                    CompletedOrdersToday INT NOT NULL DEFAULT 0,
                    Rating FLOAT NOT NULL DEFAULT 5.0
                );
            END

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
            END";

            using var cmd = new SqlCommand(ddl, conn);
            cmd.ExecuteNonQuery();
        }

        private void SyncUsersFromDatabase(SqlConnection conn)
        {
            try
            {
                using var cmd = new SqlCommand("SELECT UserId, Username, Password, FullName, Role, IsActive, CreatedAt FROM _users", conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string username = reader["Username"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(username)) continue;

                    string roleStr = reader["Role"]?.ToString() ?? "Staff";
                    UserRole role = roleStr.ToLower() switch
                    {
                        "admin" => UserRole.Admin,
                        "manager" => UserRole.Manager,
                        _ => UserRole.Staff
                    };

                    lock (_lock)
                    {
                        var existing = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
                        if (existing == null)
                        {
                            _users.Add(new User
                            {
                                Id = reader["UserId"] != DBNull.Value ? Convert.ToInt32(reader["UserId"]) : _users.Count + 1,
                                Username = username,
                                PasswordHash = reader["Password"]?.ToString() ?? "",
                                FullName = reader["FullName"] != DBNull.Value && !string.IsNullOrWhiteSpace(reader["FullName"]?.ToString()) 
                                    ? reader["FullName"]!.ToString()! 
                                    : (username == "a" ? "Tài Khoản Quản Trị Hệ Thống" : username),
                                Role = role,
                                IsActive = reader["IsActive"] == DBNull.Value || Convert.ToBoolean(reader["IsActive"]),
                                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.Now
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncUsers Error] {ex.Message}");
            }
        }

        private void SyncShippingOrdersFromDatabase(SqlConnection conn)
        {
            try
            {
                // Kiểm tra số lượng đơn hiện có trong CSDL
                using var countCmd = new SqlCommand("SELECT COUNT(*) FROM ShippingOrders", conn);
                int count = Convert.ToInt32(countCmd.ExecuteScalar());

                if (count == 0)
                {
                    // Nạp các đơn mẫu ban đầu vào SQL Server
                    lock (_lock)
                    {
                        foreach (var o in _shippingOrders)
                        {
                            InsertShippingOrderToDb(o, conn);
                        }
                    }
                }
                else
                {
                    // Tải dữ liệu từ SQL Server về danh sách bộ nhớ
                    using var selectCmd = new SqlCommand("SELECT * FROM ShippingOrders ORDER BY CreatedDate DESC", conn);
                    using var reader = selectCmd.ExecuteReader();

                    var dbOrders = new List<ShippingOrder>();
                    while (reader.Read())
                    {
                        dbOrders.Add(new ShippingOrder
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            OrderCode = reader["OrderCode"].ToString() ?? "",
                            SenderName = reader["SenderName"].ToString() ?? "",
                            SenderPhone = reader["SenderPhone"]?.ToString() ?? "",
                            SenderAddress = reader["SenderAddress"]?.ToString() ?? "",
                            ReceiverName = reader["ReceiverName"].ToString() ?? "",
                            ReceiverPhone = reader["ReceiverPhone"]?.ToString() ?? "",
                            ReceiverAddress = reader["ReceiverAddress"].ToString() ?? "",
                            DestinationArea = reader["DestinationArea"].ToString() ?? "Hà Nội",
                            ProductSummary = reader["ProductSummary"].ToString() ?? "",
                            Weight = Convert.ToDouble(reader["Weight"]),
                            IsExpress = Convert.ToBoolean(reader["IsExpress"]),
                            CodAmount = Convert.ToDecimal(reader["CodAmount"]),
                            ShippingFee = Convert.ToDecimal(reader["ShippingFee"]),
                            ExpressSurcharge = reader["ExpressSurcharge"] != DBNull.Value ? Convert.ToDecimal(reader["ExpressSurcharge"]) : 0,
                            ReceiverPaysFee = reader["ReceiverPaysFee"] == DBNull.Value || Convert.ToBoolean(reader["ReceiverPaysFee"]),
                            Status = (ShippingOrderStatus)Convert.ToInt32(reader["Status"]),
                            AssignedShipperId = reader["AssignedShipperId"] != DBNull.Value ? Convert.ToInt32(reader["AssignedShipperId"]) : null,
                            AssignedShipperName = reader["AssignedShipperName"]?.ToString() ?? "Chưa phân phối",
                            ShipperPhone = reader["ShipperPhone"]?.ToString() ?? "",
                            CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                            EstimatedDeliveryDate = Convert.ToDateTime(reader["EstimatedDeliveryDate"]),
                            DeliveredDate = reader["DeliveredDate"] != DBNull.Value ? Convert.ToDateTime(reader["DeliveredDate"]) : null,
                            Notes = reader["Notes"]?.ToString() ?? ""
                        });
                    }

                    lock (_lock)
                    {
                        _shippingOrders.Clear();
                        _shippingOrders.AddRange(dbOrders);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncShippingOrders Error] {ex.Message}");
            }
        }

        private void SyncImportOrdersFromDatabase(SqlConnection conn)
        {
            try
            {
                using var countCmd = new SqlCommand("SELECT COUNT(*) FROM ImportOrders", conn);
                int count = Convert.ToInt32(countCmd.ExecuteScalar());

                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var o in _importOrders)
                        {
                            InsertImportOrderToDb(o, conn);
                        }
                    }
                }
                else
                {
                    using var selectCmd = new SqlCommand("SELECT * FROM ImportOrders ORDER BY CreatedDate DESC", conn);
                    using var reader = selectCmd.ExecuteReader();

                    var dbOrders = new List<ImportOrder>();
                    while (reader.Read())
                    {
                        dbOrders.Add(new ImportOrder
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            ImportCode = reader["ImportCode"].ToString() ?? "",
                            SourceType = (ImportSourceType)Convert.ToInt32(reader["SourceType"]),
                            SenderName = reader["SenderName"].ToString() ?? "",
                            WaybillNumber = reader["WaybillNumber"]?.ToString() ?? "",
                            VehiclePlate = reader["VehicleNumber"]?.ToString() ?? "",
                            TotalWeight = Convert.ToDouble(reader["TotalWeight"]),
                            Status = (ImportOrderStatus)Convert.ToInt32(reader["Status"]),
                            Notes = reader["Notes"]?.ToString() ?? "",
                            CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                        });
                    }

                    lock (_lock)
                    {
                        _importOrders.Clear();
                        _importOrders.AddRange(dbOrders);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncImportOrders Error] {ex.Message}");
            }
        }

        private void SyncShippersFromDatabase(SqlConnection conn)
        {
            try
            {
                using var countCmd = new SqlCommand("SELECT COUNT(*) FROM Shippers", conn);
                int count = Convert.ToInt32(countCmd.ExecuteScalar());

                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var s in _shippers)
                        {
                            using var cmd = new SqlCommand(@"
                                INSERT INTO Shippers (FullName, PhoneNumber, VehicleType, LicensePlate, CurrentArea, Status, CompletedOrdersToday, Rating)
                                VALUES (@FullName, @PhoneNumber, @VehicleType, @LicensePlate, @CurrentArea, @Status, @CompletedOrdersToday, @Rating)", conn);
                            cmd.Parameters.AddWithValue("@FullName", s.FullName);
                            cmd.Parameters.AddWithValue("@PhoneNumber", s.PhoneNumber);
                            cmd.Parameters.AddWithValue("@VehicleType", s.VehicleType);
                            cmd.Parameters.AddWithValue("@LicensePlate", s.LicensePlate);
                            cmd.Parameters.AddWithValue("@CurrentArea", s.CurrentArea);
                            cmd.Parameters.AddWithValue("@Status", (int)s.Status);
                            cmd.Parameters.AddWithValue("@CompletedOrdersToday", s.CompletedOrdersToday);
                            cmd.Parameters.AddWithValue("@Rating", s.Rating);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                // Tải danh sách Shipper thực tế từ database
                using var selectCmd = new SqlCommand("SELECT * FROM Shippers ORDER BY Id ASC", conn);
                using var reader = selectCmd.ExecuteReader();
                var dbShippers = new List<Shipper>();
                while (reader.Read())
                {
                    dbShippers.Add(new Shipper
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        FullName = reader["FullName"].ToString() ?? "",
                        Phone = reader["PhoneNumber"]?.ToString() ?? "",
                        VehicleType = reader["VehicleType"]?.ToString() ?? "Xe máy",
                        VehiclePlate = reader["LicensePlate"]?.ToString() ?? "",
                        DeliveryArea = reader["CurrentArea"]?.ToString() ?? "Quận Ba Đình",
                        Status = (ShipperStatus)Convert.ToInt32(reader["Status"]),
                        CompletedTodayCount = Convert.ToInt32(reader["CompletedOrdersToday"]),
                        Rating = Convert.ToDouble(reader["Rating"])
                    });
                }

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
                System.Diagnostics.Debug.WriteLine($"[SyncShippers Error] {ex.Message}");
            }
        }

        private void SyncActivitiesFromDatabase(SqlConnection conn)
        {
            try
            {
                using var countCmd = new SqlCommand("SELECT COUNT(*) FROM RecentActivities", conn);
                int count = Convert.ToInt32(countCmd.ExecuteScalar());

                if (count == 0)
                {
                    lock (_lock)
                    {
                        foreach (var a in _recentActivities)
                        {
                            using var cmd = new SqlCommand(@"
                                INSERT INTO RecentActivities (Title, Description, Timestamp, Icon, Category)
                                VALUES (@Title, @Description, @Timestamp, @Icon, @Category)", conn);
                            cmd.Parameters.AddWithValue("@Title", a.Title);
                            cmd.Parameters.AddWithValue("@Description", a.Description);
                            cmd.Parameters.AddWithValue("@Timestamp", a.Timestamp);
                            cmd.Parameters.AddWithValue("@Icon", (object?)a.Icon ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Category", (object?)a.Type.ToString() ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                // Tải lịch sử hoạt động từ database
                using var selectCmd = new SqlCommand("SELECT TOP 50 * FROM RecentActivities ORDER BY Timestamp DESC", conn);
                using var reader = selectCmd.ExecuteReader();
                var dbActivities = new List<RecentActivity>();
                while (reader.Read())
                {
                    string category = reader["Category"]?.ToString() ?? "System";
                    _ = Enum.TryParse<ActivityType>(category, true, out var actType);
                    dbActivities.Add(new RecentActivity
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Title = reader["Title"].ToString() ?? "",
                        Description = reader["Description"]?.ToString() ?? "",
                        Timestamp = Convert.ToDateTime(reader["Timestamp"]),
                        Type = actType
                    });
                }

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
                System.Diagnostics.Debug.WriteLine($"[SyncActivities Error] {ex.Message}");
            }
        }

        private void SyncProductsFromDatabase(SqlConnection conn)
        {
            try
            {
                using var cmd = new SqlCommand("SELECT ProductID, ProductName, SKU, SellingPrice, Stock, Unit, Category FROM product", conn);
                using var reader = cmd.ExecuteReader();

                var dbProducts = new List<Product>();
                var dbInventories = new List<Inventory>();
                int invId = 1;

                while (reader.Read())
                {
                    int prodId = Convert.ToInt32(reader["ProductID"]);
                    string prodName = reader["ProductName"]?.ToString() ?? "";
                    string sku = reader["SKU"]?.ToString() ?? $"SKU-{prodId}";
                    decimal price = reader["SellingPrice"] != DBNull.Value ? Convert.ToDecimal(reader["SellingPrice"]) : 0;
                    int stock = reader["Stock"] != DBNull.Value ? Convert.ToInt32(reader["Stock"]) : 0;
                    string unit = reader["Unit"]?.ToString() ?? "Cái";
                    string cat = reader["Category"]?.ToString() ?? "Linh kiện điện tử";

                    dbProducts.Add(new Product
                    {
                        Id = prodId,
                        ProductCode = sku,
                        ProductName = prodName,
                        Price = price,
                        Unit = unit,
                        Category = cat,
                        Weight = 0.5
                    });

                    dbInventories.Add(new Inventory
                    {
                        Id = invId++,
                        ProductId = prodId,
                        ProductCode = sku,
                        ProductName = prodName,
                        LocationCode = $"KỆ-A{prodId % 5 + 1}-0{prodId % 3 + 1}",
                        Quantity = stock,
                        ReservedQuantity = 0,
                        BatchNumber = $"LOT-2026-{prodId:D3}",
                        LastImportDate = DateTime.Now
                    });
                }

                if (dbProducts.Count > 0)
                {
                    lock (_lock)
                    {
                        _products.Clear();
                        _products.AddRange(dbProducts);

                        _inventories.Clear();
                        _inventories.AddRange(dbInventories);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncProducts Error] {ex.Message}");
            }
        }

        private static int InsertShippingOrderToDb(ShippingOrder o, SqlConnection conn)
        {
            const string sql = @"
                INSERT INTO ShippingOrders 
                (OrderCode, SenderName, SenderPhone, SenderAddress, ReceiverName, ReceiverPhone, ReceiverAddress, DestinationArea, ProductSummary, Weight, IsExpress, CodAmount, ShippingFee, ExpressSurcharge, ReceiverPaysFee, Status, AssignedShipperName, ShipperPhone, CreatedDate, EstimatedDeliveryDate, Notes)
                VALUES 
                (@OrderCode, @SenderName, @SenderPhone, @SenderAddress, @ReceiverName, @ReceiverPhone, @ReceiverAddress, @DestinationArea, @ProductSummary, @Weight, @IsExpress, @CodAmount, @ShippingFee, @ExpressSurcharge, @ReceiverPaysFee, @Status, @AssignedShipperName, @ShipperPhone, @CreatedDate, @EstimatedDeliveryDate, @Notes);
                SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OrderCode", o.OrderCode);
            cmd.Parameters.AddWithValue("@SenderName", o.SenderName);
            cmd.Parameters.AddWithValue("@SenderPhone", o.SenderPhone);
            cmd.Parameters.AddWithValue("@SenderAddress", (object?)o.SenderAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReceiverName", o.ReceiverName);
            cmd.Parameters.AddWithValue("@ReceiverPhone", o.ReceiverPhone);
            cmd.Parameters.AddWithValue("@ReceiverAddress", o.ReceiverAddress);
            cmd.Parameters.AddWithValue("@DestinationArea", o.DestinationArea);
            cmd.Parameters.AddWithValue("@ProductSummary", o.ProductSummary);
            cmd.Parameters.AddWithValue("@Weight", o.Weight);
            cmd.Parameters.AddWithValue("@IsExpress", o.IsExpress);
            cmd.Parameters.AddWithValue("@CodAmount", o.CodAmount);
            cmd.Parameters.AddWithValue("@ShippingFee", o.ShippingFee);
            cmd.Parameters.AddWithValue("@ExpressSurcharge", o.ExpressSurcharge);
            cmd.Parameters.AddWithValue("@ReceiverPaysFee", o.ReceiverPaysFee);
            cmd.Parameters.AddWithValue("@Status", (int)o.Status);
            cmd.Parameters.AddWithValue("@AssignedShipperName", (object?)o.AssignedShipperName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ShipperPhone", (object?)o.ShipperPhone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedDate", o.CreatedDate);
            cmd.Parameters.AddWithValue("@EstimatedDeliveryDate", o.EstimatedDeliveryDate);
            cmd.Parameters.AddWithValue("@Notes", (object?)o.Notes ?? DBNull.Value);

            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        private static int InsertImportOrderToDb(ImportOrder o, SqlConnection conn)
        {
            const string sql = @"
                INSERT INTO ImportOrders 
                (ImportCode, SourceType, SourceTypeName, SenderName, WaybillNumber, VehicleNumber, TotalWeight, Status, StatusDisplayName, Notes, CreatedDate)
                VALUES 
                (@ImportCode, @SourceType, @SourceTypeName, @SenderName, @WaybillNumber, @VehicleNumber, @TotalWeight, @Status, @StatusDisplayName, @Notes, @CreatedDate);
                SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ImportCode", o.ImportCode);
            cmd.Parameters.AddWithValue("@SourceType", (int)o.SourceType);
            cmd.Parameters.AddWithValue("@SourceTypeName", o.SourceTypeName);
            cmd.Parameters.AddWithValue("@SenderName", o.SenderName);
            cmd.Parameters.AddWithValue("@WaybillNumber", (object?)o.WaybillNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@VehicleNumber", (object?)o.VehiclePlate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TotalWeight", o.TotalWeight);
            cmd.Parameters.AddWithValue("@Status", (int)o.Status);
            cmd.Parameters.AddWithValue("@StatusDisplayName", o.StatusDisplayName);
            cmd.Parameters.AddWithValue("@Notes", (object?)o.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedDate", o.CreatedDate);

            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
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
                    Username = "admin",
                    PasswordHash = User.HashPassword("admin123"),
                    FullName = "Nguyễn Văn Quản Trị",
                    Email = "admin@logixwarehouse.vn",
                    PhoneNumber = "0901 234 567",
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.Now.AddMonths(-6)
                },
                new User
                {
                    Id = 2,
                    Username = "quanly",
                    PasswordHash = User.HashPassword("quanly123"),
                    FullName = "Trần Thị Trưởng Kho",
                    Email = "truongkho@logixwarehouse.vn",
                    PhoneNumber = "0912 345 678",
                    Role = UserRole.Manager,
                    IsActive = true,
                    CreatedAt = DateTime.Now.AddMonths(-3)
                },
                new User
                {
                    Id = 3,
                    Username = "nhanvien",
                    PasswordHash = User.HashPassword("nhanvien123"),
                    FullName = "Lê Văn Vận Hành",
                    Email = "nhanvien@logixwarehouse.vn",
                    PhoneNumber = "0987 654 321",
                    Role = UserRole.Staff,
                    IsActive = true,
                    CreatedAt = DateTime.Now.AddMonths(-1)
                },
                new User
                {
                    Id = 4,
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
            // 1. Sản phẩm hàng hóa
            _products.AddRange(new[]
            {
                new Product { Id = 1, ProductCode = "SP-DIENTU-01", ProductName = "Điện thoại thông minh Xiaomi Note 13", Unit = "Hộp", Category = "Điện Tử", Price = 4890000, Weight = 0.5, Length = 18, Width = 10, Height = 6, RequiresSpecialHandling = true, SpecialInstructions = "Hàng giá trị cao, tránh va đập", Barcode = "893850123001" },
                new Product { Id = 2, ProductCode = "SP-THUCPHAM-02", ProductName = "Thùng Sữa Tươi Vinamilk 100% 48 hộp", Unit = "Thùng", Category = "Tiêu Dùng Nhanh", Price = 385000, Weight = 9.2, Length = 40, Width = 25, Height = 20, RequiresSpecialHandling = false, Barcode = "893850123002" },
                new Product { Id = 3, ProductCode = "SP-MAYMAC-03", ProductName = "Kiện Áo Thun Polo Cotton Nam", Unit = "Kiện", Category = "Thời Trang", Price = 2500000, Weight = 4.5, Length = 50, Width = 35, Height = 15, RequiresSpecialHandling = false, Barcode = "893850123003" },
                new Product { Id = 4, ProductCode = "SP-MYPHAM-04", ProductName = "Bộ Mỹ Phẩm Skincare Dưỡng Trắng Serum", Unit = "Hộp", Category = "Mỹ Phẩm", Price = 1250000, Weight = 0.8, Length = 22, Width = 16, Height = 10, RequiresSpecialHandling = true, SpecialInstructions = "Hàng dễ vỡ chai thủy tinh", Barcode = "893850123004" },
                new Product { Id = 5, ProductCode = "SP-CONGKENH-05", ProductName = "Nồi Chiên Không Dầu Philips 6.2L", Unit = "Thùng", Category = "Gia Dụng", Price = 1890000, Weight = 6.8, Length = 42, Width = 38, Height = 36, RequiresSpecialHandling = false, Barcode = "893850123005" }
            });

            // 2. Vị trí kho bãi
            _locations.AddRange(new[]
            {
                new WarehouseLocation { Id = 1, LocationCode = "KHO-A-D01-K01", Zone = "Khu Lưu Kho Tiêu Chuẩn", Aisle = "A", Rack = "01", Shelf = "1", Bin = "01", CurrentWeight = 450, MaxWeightCapacity = 1000, Status = LocationStatus.PartiallyFull },
                new WarehouseLocation { Id = 2, LocationCode = "KHO-A-D01-K02", Zone = "Khu Lưu Kho Tiêu Chuẩn", Aisle = "A", Rack = "01", Shelf = "2", Bin = "01", CurrentWeight = 800, MaxWeightCapacity = 1000, Status = LocationStatus.PartiallyFull },
                new WarehouseLocation { Id = 3, LocationCode = "KHO-EXP-01", Zone = "Khu Hàng Hỏa Tốc (Express)", Aisle = "EXP", Rack = "01", Shelf = "1", Bin = "01", CurrentWeight = 120, MaxWeightCapacity = 500, Status = LocationStatus.PartiallyFull },
                new WarehouseLocation { Id = 4, LocationCode = "DOCK-INBOUND-01", Zone = "Khu Nhận Hàng Tiếp Nhận", Aisle = "IN", Rack = "01", Shelf = "1", Bin = "01", CurrentWeight = 950, MaxWeightCapacity = 2000, Status = LocationStatus.PartiallyFull }
            });

            // 3. Tồn kho
            _inventories.AddRange(new[]
            {
                new Inventory { Id = 1, ProductId = 1, ProductCode = "SP-DIENTU-01", ProductName = "Điện thoại thông minh Xiaomi Note 13", Unit = "Hộp", WarehouseLocationId = 3, LocationCode = "KHO-EXP-01", Quantity = 45, ReservedQuantity = 12, MinStockLevel = 10, MaxStockLevel = 100, BatchNumber = "LOT-XM-202609" },
                new Inventory { Id = 2, ProductId = 2, ProductCode = "SP-THUCPHAM-02", ProductName = "Thùng Sữa Tươi Vinamilk 100% 48 hộp", Unit = "Thùng", WarehouseLocationId = 1, LocationCode = "KHO-A-D01-K01", Quantity = 120, ReservedQuantity = 35, MinStockLevel = 30, MaxStockLevel = 300, BatchNumber = "LOT-VNM-202609" },
                new Inventory { Id = 3, ProductId = 3, ProductCode = "SP-MAYMAC-03", ProductName = "Kiện Áo Thun Polo Cotton Nam", Unit = "Kiện", WarehouseLocationId = 2, LocationCode = "KHO-A-D01-K02", Quantity = 8, ReservedQuantity = 2, MinStockLevel = 15, MaxStockLevel = 150, BatchNumber = "LOT-POLO-202608" },
                new Inventory { Id = 4, ProductId = 4, ProductCode = "SP-MYPHAM-04", ProductName = "Bộ Mỹ Phẩm Skincare Dưỡng Trắng Serum", Unit = "Hộp", WarehouseLocationId = 3, LocationCode = "KHO-EXP-01", Quantity = 78, ReservedQuantity = 20, MinStockLevel = 20, MaxStockLevel = 200, BatchNumber = "LOT-SERUM-202609" }
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
                new Shipper { Id = 4, FullName = "Vũ Đình Duy", PhoneNumber = "0984 444 555", VehicleType = "Xe máy Honda AirBlade", LicensePlate = "29B1-234.56", CurrentArea = "Hoàn Kiếm - Hai Bà Trưng", Status = ShipperStatus.OffDuty, CompletedOrdersToday = 0, Rating = 4.7 }
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

            // Nếu chưa có trong cache và có kết nối DB, truy vấn trực tiếp từ SQL Server
            if (IsDatabaseConnected)
            {
                try
                {
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand("SELECT TOP 1 UserId, Username, Password, FullName, Role, IsActive, CreatedAt FROM _users WHERE Username = @Username", ketNoi);
                    cauLenh.Parameters.AddWithValue("@Username", tenDangNhap.Trim());
                    using var boDoc = cauLenh.ExecuteReader();
                    if (boDoc.Read())
                    {
                        string chuoiVaiTro = boDoc["Role"]?.ToString() ?? "Staff";
                        UserRole vaiTro = chuoiVaiTro.ToLower() switch
                        {
                            "admin" => UserRole.Admin,
                            "manager" => UserRole.Manager,
                            _ => UserRole.Staff
                        };

                        var nguoiDung = new User
                        {
                            Id = boDoc["UserId"] != DBNull.Value ? Convert.ToInt32(boDoc["UserId"]) : 1,
                            Username = boDoc["Username"].ToString() ?? tenDangNhap,
                            PasswordHash = boDoc["Password"]?.ToString() ?? "",
                            FullName = boDoc["FullName"] != DBNull.Value ? boDoc["FullName"].ToString()! : tenDangNhap,
                            Role = vaiTro,
                            IsActive = boDoc["IsActive"] == DBNull.Value || Convert.ToBoolean(boDoc["IsActive"]),
                            CreatedAt = boDoc["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(boDoc["CreatedAt"]) : DateTime.Now
                        };

                        lock (_lock)
                        {
                            _users.Add(nguoiDung);
                        }
                        return nguoiDung;
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[FindUserByUsername SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand(@"
                        INSERT INTO _users (Username, Password, FullName, Role, IsActive, CreatedAt)
                        VALUES (@Username, @Password, @FullName, @Role, @IsActive, @CreatedAt)", ketNoi);
                    cauLenh.Parameters.AddWithValue("@Username", nguoiDung.Username);
                    cauLenh.Parameters.AddWithValue("@Password", nguoiDung.PasswordHash);
                    cauLenh.Parameters.AddWithValue("@FullName", nguoiDung.FullName);
                    cauLenh.Parameters.AddWithValue("@Role", nguoiDung.Role.ToString());
                    cauLenh.Parameters.AddWithValue("@IsActive", nguoiDung.IsActive);
                    cauLenh.Parameters.AddWithValue("@CreatedAt", nguoiDung.CreatedAt);
                    cauLenh.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddUser SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand("UPDATE _users SET IsActive = @IsActive WHERE UserId = @UserId", ketNoi);
                    cauLenh.Parameters.AddWithValue("@IsActive", trangThaiMoi);
                    cauLenh.Parameters.AddWithValue("@UserId", maNguoiDung);
                    cauLenh.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToggleUserActiveStatus SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand("UPDATE _users SET Password = @Password WHERE UserId = @UserId", ketNoi);
                    cauLenh.Parameters.AddWithValue("@Password", matKhauMoiTho);
                    cauLenh.Parameters.AddWithValue("@UserId", maNguoiDung);
                    cauLenh.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateUserPassword SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand(@"
                        UPDATE ShippingOrders 
                        SET AssignedShipperId = @ShipperId, AssignedShipperName = @ShipperName, ShipperPhone = @ShipperPhone, 
                            Status = CASE WHEN Status IN (0,1) THEN 2 ELSE Status END
                        WHERE Id = @Id", ketNoi);
                    cauLenh.Parameters.AddWithValue("@ShipperId", maTaiXe);
                    cauLenh.Parameters.AddWithValue("@ShipperName", tenTaiXe);
                    cauLenh.Parameters.AddWithValue("@ShipperPhone", soDienThoaiTaiXe);
                    cauLenh.Parameters.AddWithValue("@Id", maDonHang);
                    cauLenh.ExecuteNonQuery();

                    using var cauLenhLichSu = new SqlCommand(@"
                        INSERT INTO RecentActivities (Title, Description, Timestamp, Icon, Category)
                        VALUES (@Title, @Description, @Timestamp, @Icon, @Category)", ketNoi);
                    cauLenhLichSu.Parameters.AddWithValue("@Title", $"Điều phối Shipper {tenTaiXe}");
                    cauLenhLichSu.Parameters.AddWithValue("@Description", $"Đã phân công đơn hàng ID #{maDonHang}");
                    cauLenhLichSu.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                    cauLenhLichSu.Parameters.AddWithValue("@Icon", "🛵");
                    cauLenhLichSu.Parameters.AddWithValue("@Category", "Delivering");
                    cauLenhLichSu.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AssignShipper SQL Error] {ngoaiLe.Message}");
                }
            }
        }

        // =========================================================================
        // PHÂN HỆ SẢN PHẨM & TỒN KHO (PRODUCTS & INVENTORY)
        // =========================================================================

        /// <summary>
        /// Lấy toàn bộ danh mục sản phẩm đã đồng bộ từ bảng product trong CSDL
        /// </summary>
        public IReadOnlyList<Product> GetAllProducts() { lock (_lock) return _products.ToList().AsReadOnly(); }

        /// <summary>
        /// Tìm kiếm sản phẩm theo ID
        /// </summary>
        public Product? FindProductById(int maId) { lock (_lock) return _products.FirstOrDefault(p => p.Id == maId); }

        /// <summary>
        /// Lấy danh sách vị trí lưu trữ kệ kho (Khu A / Khu B / Khu C)
        /// </summary>
        public IReadOnlyList<WarehouseLocation> GetAllLocations() { lock (_lock) return _locations.ToList().AsReadOnly(); }

        /// <summary>
        /// Lấy toàn bộ số lượng tồn kho theo từng vị trí kệ
        /// </summary>
        public IReadOnlyList<Inventory> GetAllInventories() { lock (_lock) return _inventories.ToList().AsReadOnly(); }

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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    int maPhieuNhapMoi = InsertImportOrderToDb(phieuNhap, ketNoi);
                    phieuNhap.Id = maPhieuNhapMoi;

                    using var cauLenhLichSu = new SqlCommand(@"
                        INSERT INTO RecentActivities (Title, Description, Timestamp, Icon, Category)
                        VALUES (@Title, @Description, @Timestamp, @Icon, @Category)", ketNoi);
                    cauLenhLichSu.Parameters.AddWithValue("@Title", $"Tạo mới phiếu nhập {phieuNhap.ImportCode}");
                    cauLenhLichSu.Parameters.AddWithValue("@Description", $"Nguồn gửi: {phieuNhap.SenderName} ({phieuNhap.SourceTypeName})");
                    cauLenhLichSu.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                    cauLenhLichSu.Parameters.AddWithValue("@Icon", "📥");
                    cauLenhLichSu.Parameters.AddWithValue("@Category", "Import");
                    cauLenhLichSu.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddImportOrder SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    int maDonHangMoi = InsertShippingOrderToDb(donHang, ketNoi);
                    donHang.Id = maDonHangMoi;

                    using var cauLenhLichSu = new SqlCommand(@"
                        INSERT INTO RecentActivities (Title, Description, Timestamp, Icon, Category)
                        VALUES (@Title, @Description, @Timestamp, @Icon, @Category)", ketNoi);
                    cauLenhLichSu.Parameters.AddWithValue("@Title", donHang.IsExpress ? $"Tạo đơn Express {donHang.OrderCode}" : $"Tạo đơn hàng {donHang.OrderCode}");
                    cauLenhLichSu.Parameters.AddWithValue("@Description", $"Gửi tới: {donHang.ReceiverName} ({donHang.DestinationArea})");
                    cauLenhLichSu.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                    cauLenhLichSu.Parameters.AddWithValue("@Icon", donHang.IsExpress ? "⚡" : "📦");
                    cauLenhLichSu.Parameters.AddWithValue("@Category", donHang.IsExpress ? "Express" : "OrderNew");
                    cauLenhLichSu.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddShippingOrder SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand("UPDATE ShippingOrders SET Status = @Status, DeliveredDate = @DeliveredDate WHERE Id = @Id", ketNoi);
                    cauLenh.Parameters.AddWithValue("@Status", (int)trangThaiMoi);
                    cauLenh.Parameters.AddWithValue("@DeliveredDate", trangThaiMoi == ShippingOrderStatus.Delivered ? (object)DateTime.Now : DBNull.Value);
                    cauLenh.Parameters.AddWithValue("@Id", maDonHang);
                    cauLenh.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateShippingOrderStatus SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();

                    // Cập nhật hàng loạt bảng ShippingOrders
                    string chuoiDanhSachId = string.Join(",", danhSachMaDon);
                    string cauLenhSql = $@"
                        UPDATE ShippingOrders 
                        SET AssignedShipperId = @ShipperId, 
                            AssignedShipperName = @ShipperName, 
                            ShipperPhone = @ShipperPhone, 
                            Status = 2 
                        WHERE Id IN ({chuoiDanhSachId})";

                    using var cauLenh = new SqlCommand(cauLenhSql, ketNoi);
                    cauLenh.Parameters.AddWithValue("@ShipperId", maTaiXe);
                    cauLenh.Parameters.AddWithValue("@ShipperName", tenTaiXe);
                    cauLenh.Parameters.AddWithValue("@ShipperPhone", soDienThoaiTaiXe);
                    cauLenh.ExecuteNonQuery();

                    // Thêm bản ghi hoạt động
                    using var cauLenhLichSu = new SqlCommand(@"
                        INSERT INTO RecentActivities (Title, Description, Timestamp, Icon, Category)
                        VALUES (@Title, @Description, @Timestamp, @Icon, @Category)", ketNoi);
                    cauLenhLichSu.Parameters.AddWithValue("@Title", $"Phát hành chuyến gom {tenTuyenDuong}");
                    cauLenhLichSu.Parameters.AddWithValue("@Description", $"Shipper {tenTaiXe} nhận giao {danhSachMaDon.Count} bưu kiện");
                    cauLenhLichSu.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                    cauLenhLichSu.Parameters.AddWithValue("@Icon", "🗺️");
                    cauLenhLichSu.Parameters.AddWithValue("@Category", "Delivering");
                    cauLenhLichSu.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[DieuPhoiGomChuyenTuyen SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand(@"
                        SELECT TOP 1 * FROM ShippingOrders 
                        WHERE OrderCode LIKE @Keyword OR ReceiverPhone LIKE @Keyword OR SenderPhone LIKE @Keyword", ketNoi);
                    cauLenh.Parameters.AddWithValue("@Keyword", $"%{tuKhoaChuanHoa}%");
                    using var docDuLieu = cauLenh.ExecuteReader();
                    if (docDuLieu.Read())
                    {
                        var donHang = new ShippingOrder
                        {
                            Id = Convert.ToInt32(docDuLieu["Id"]),
                            OrderCode = docDuLieu["OrderCode"]?.ToString() ?? "",
                            SenderName = docDuLieu["SenderName"]?.ToString() ?? "",
                            SenderPhone = docDuLieu["SenderPhone"]?.ToString() ?? "",
                            SenderAddress = docDuLieu["SenderAddress"]?.ToString() ?? "",
                            ReceiverName = docDuLieu["ReceiverName"]?.ToString() ?? "",
                            ReceiverPhone = docDuLieu["ReceiverPhone"]?.ToString() ?? "",
                            ReceiverAddress = docDuLieu["ReceiverAddress"]?.ToString() ?? "",
                            DestinationArea = docDuLieu["DestinationArea"]?.ToString() ?? "",
                            ProductSummary = docDuLieu["ProductSummary"]?.ToString() ?? "",
                            Weight = Convert.ToDouble(docDuLieu["Weight"]),
                            IsExpress = Convert.ToBoolean(docDuLieu["IsExpress"]),
                            CodAmount = Convert.ToDecimal(docDuLieu["CodAmount"]),
                            ShippingFee = Convert.ToDecimal(docDuLieu["ShippingFee"]),
                            ExpressSurcharge = Convert.ToDecimal(docDuLieu["ExpressSurcharge"]),
                            ReceiverPaysFee = Convert.ToBoolean(docDuLieu["ReceiverPaysFee"]),
                            Status = (ShippingOrderStatus)Convert.ToInt32(docDuLieu["Status"]),
                            AssignedShipperName = docDuLieu["AssignedShipperName"]?.ToString() ?? "Chưa phân phối",
                            ShipperPhone = docDuLieu["ShipperPhone"]?.ToString() ?? "",
                            CreatedDate = Convert.ToDateTime(docDuLieu["CreatedDate"]),
                            EstimatedDeliveryDate = Convert.ToDateTime(docDuLieu["EstimatedDeliveryDate"]),
                            DeliveredDate = docDuLieu["DeliveredDate"] != DBNull.Value ? Convert.ToDateTime(docDuLieu["DeliveredDate"]) : null,
                            Notes = docDuLieu["Notes"]?.ToString() ?? ""
                        };
                        return donHang;
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[TimKiemDonHang SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand(@"
                        INSERT INTO Shippers (FullName, PhoneNumber, VehicleType, LicensePlate, CurrentArea, Status, CompletedOrdersToday, Rating)
                        VALUES (@FullName, @PhoneNumber, @VehicleType, @LicensePlate, @CurrentArea, @Status, @CompletedOrdersToday, @Rating);
                        SELECT SCOPE_IDENTITY();", ketNoi);
                    cauLenh.Parameters.AddWithValue("@FullName", taiXe.FullName);
                    cauLenh.Parameters.AddWithValue("@PhoneNumber", taiXe.Phone);
                    cauLenh.Parameters.AddWithValue("@VehicleType", taiXe.VehicleType);
                    cauLenh.Parameters.AddWithValue("@LicensePlate", taiXe.VehiclePlate);
                    cauLenh.Parameters.AddWithValue("@CurrentArea", taiXe.DeliveryArea);
                    cauLenh.Parameters.AddWithValue("@Status", (int)taiXe.Status);
                    cauLenh.Parameters.AddWithValue("@CompletedOrdersToday", taiXe.CompletedTodayCount);
                    cauLenh.Parameters.AddWithValue("@Rating", taiXe.Rating);
                    object result = cauLenh.ExecuteScalar();
                    if (result != null && int.TryParse(result.ToString(), out int newId))
                    {
                        taiXe.Id = newId;
                    }
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddShipper SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand("UPDATE Shippers SET Status = @Status WHERE Id = @Id", ketNoi);
                    cauLenh.Parameters.AddWithValue("@Status", (int)trangThaiMoi);
                    cauLenh.Parameters.AddWithValue("@Id", maTaiXe);
                    cauLenh.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateShipperShiftAndStatus SQL Error] {ngoaiLe.Message}");
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
                    using var ketNoi = new SqlConnection(ConnectionString);
                    ketNoi.Open();
                    using var cauLenh = new SqlCommand("UPDATE Shippers SET CurrentArea = @CurrentArea WHERE Id = @Id", ketNoi);
                    cauLenh.Parameters.AddWithValue("@CurrentArea", khuVucMoi);
                    cauLenh.Parameters.AddWithValue("@Id", maTaiXe);
                    cauLenh.ExecuteNonQuery();
                }
                catch (Exception ngoaiLe)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateShipperAreaAndLimit SQL Error] {ngoaiLe.Message}");
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
            lock (_lock)
            {
                var ngauNhien = new Random();
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

                int startId = _shippingOrders.Count > 0 ? _shippingOrders.Max(o => o.Id) + 1 : 1;

                for (int i = 0; i < count; i++)
                {
                    int currentId = startId + i;
                    string khuVuc = danhSachKhuVucTN[ngauNhien.Next(danhSachKhuVucTN.Length)];
                    string nguoiNhan = danhSachNguoiNhan[ngauNhien.Next(danhSachNguoiNhan.Length)];
                    string hangHoa = danhSachHangHoa[ngauNhien.Next(danhSachHangHoa.Length)];
                    
                    // Khoảng 30-35 đơn là đơn Hỏa Tốc Express
                    bool isExpress = (i < 30) || (ngauNhien.Next(100) < 32);
                    
                    // Giờ hẹn giao cam kết SLA
                    DateTime hanGiao;
                    if (isExpress)
                    {
                        // Hỏa tốc: Giao gấp trong vòng -30 phút (quá hạn) đến +2h
                        int deltaMinutes = ngauNhien.Next(-30, 120);
                        hanGiao = DateTime.Now.AddMinutes(deltaMinutes);
                    }
                    else
                    {
                        // Tiêu chuẩn: Giao trong 6h đến 48h
                        int deltaHours = ngauNhien.Next(6, 48);
                        hanGiao = DateTime.Now.AddHours(deltaHours);
                    }

                    decimal cod = ngauNhien.Next(0, 20) * 50000;
                    double weight = Math.Round(0.3 + ngauNhien.NextDouble() * 5.0, 1);

                    var donMoi = new ShippingOrder
                    {
                        Id = currentId,
                        OrderCode = $"LOGIX-TN-{(isExpress ? "EXP" : "STD")}-{DateTime.Now:yyMMdd}-{currentId:D4}",
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
                        CreatedDate = DateTime.Now.AddHours(-ngauNhien.Next(1, 24)),
                        EstimatedDeliveryDate = hanGiao,
                        Notes = isExpress ? "⚡ HỎA TỐC THÁI NGUYÊN (ƯU TIÊN XUẤT BẾN)" : "Giao tiêu chuẩn theo tuyến Thái Nguyên"
                    };

                    _shippingOrders.Add(donMoi);
                }

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"Khởi tạo kịch bản {count} đơn Thái Nguyên",
                    Description = $"Mô phỏng 100 đơn Thái Nguyên: Giao 40 đơn Hỏa tốc & SLA trước, 60 đơn thường lưu kho",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.OrderSuccess
                });
            }
        }

        public void GenerateSampleOrdersForSimulation(int count = 100)
        {
            lock (_lock)
            {
                var ngauNhien = new Random();
                string[] danhSachKhuVuc = { "Cầu Giấy", "Nam Từ Liêm", "Đống Đa", "Ba Đình", "Hoàn Kiếm", "Thanh Xuân", "Hà Đông", "Hai Bà Trưng" };
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

                int startId = _shippingOrders.Count > 0 ? _shippingOrders.Max(o => o.Id) + 1 : 1;

                for (int i = 0; i < count; i++)
                {
                    int currentId = startId + i;
                    string khuVuc = danhSachKhuVuc[ngauNhien.Next(danhSachKhuVuc.Length)];
                    string nguoiNhan = danhSachNguoiNhan[ngauNhien.Next(danhSachNguoiNhan.Length)];
                    string hangHoa = danhSachHangHoa[ngauNhien.Next(danhSachHangHoa.Length)];
                    
                    // Khoảng 35% là đơn Hỏa Tốc Express
                    bool isExpress = (i < 35) || (ngauNhien.Next(100) < 35);
                    
                    // Giờ hẹn giao cam kết SLA
                    DateTime hanGiao;
                    if (isExpress)
                    {
                        // Hỏa tốc: Giao gấp trong vòng -45 phút (quá hạn) đến +3h
                        int deltaMinutes = ngauNhien.Next(-45, 180);
                        hanGiao = DateTime.Now.AddMinutes(deltaMinutes);
                    }
                    else
                    {
                        // Tiêu chuẩn: Giao trong 4h đến 48h
                        int deltaHours = ngauNhien.Next(4, 48);
                        hanGiao = DateTime.Now.AddHours(deltaHours);
                    }

                    decimal cod = ngauNhien.Next(1, 30) * 50000;
                    double weight = Math.Round(0.3 + ngauNhien.NextDouble() * 5.0, 1);

                    var donMoi = new ShippingOrder
                    {
                        Id = currentId,
                        OrderCode = $"LOGIX-{(isExpress ? "EXP" : "STD")}-{DateTime.Now:yyMMdd}-{currentId:D4}",
                        SenderName = isExpress ? "Trung Tâm Phân Phối Hỏa Tốc" : "Kho Vận Tổng Hợp Hà Nội",
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
                        CreatedDate = DateTime.Now.AddHours(-ngauNhien.Next(1, 24)),
                        EstimatedDeliveryDate = hanGiao,
                        Notes = isExpress ? "⚡ ƯU TIÊN GIAO GẤP HỎA TỐC" : "Giao tiêu chuẩn theo tuyến"
                    };

                    _shippingOrders.Add(donMoi);
                }

                _recentActivities.Insert(0, new RecentActivity
                {
                    Id = _recentActivities.Count + 1,
                    Title = $"Khởi tạo kịch bản {count} đơn hàng mô phỏng",
                    Description = $"Tập dữ liệu kiểm thử năng lực điều phối ưu tiên ({count} đơn)",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.OrderSuccess
                });
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
        }
        #endregion
    }
}
