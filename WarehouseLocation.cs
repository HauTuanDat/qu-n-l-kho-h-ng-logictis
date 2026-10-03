using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ENUM: Trạng thái sử dụng của vị trí ô kệ kho bãi
    /// </summary>
    public enum LocationStatus
    {
        Empty,          // Vị trí đang trống (Sẵn sàng xếp hàng)
        PartiallyFull,  // Đang chứa hàng (Vẫn còn sức chứa)
        Full,           // Đã đầy công suất tối đa
        Maintenance     // Đang bảo trì / Khóa sửa chữa tạm thời
    }

    /// <summary>
    /// THỰC THỂ: Vị Trí Lưu Trữ Kho Bãi (Warehouse Location Model)
    /// - Nhiệm vụ: Quản lý tọa độ 5 cấp độ trong kho: Phân khu (Zone) -> Dãy (Aisle) -> Kệ (Rack) -> Tầng (Shelf) -> Ô (Bin),
    ///             giám sát tải trọng tối đa (Kg), thể tích tối đa (m3) và tỷ lệ lấp đầy kho.
    /// - Cách hoạt động: 
    ///   + OccupancyPercentage = (CurrentWeight / MaxWeightCapacity) * 100%.
    ///   + Tự động gắn nhãn trạng thái và màu sắc tương ứng.
    /// - Tương tác dữ liệu: Inventory.cs, InventoryManagementView.xaml.cs.
    /// </summary>
    public class WarehouseLocation
    {
        public int Id { get; set; }

        /// <summary>
        /// Mã vị trí kệ định danh (VD: KE-A-01, KE-B-02, KE-C-03)
        /// </summary>
        public string LocationCode { get; set; } = string.Empty;

        /// <summary>
        /// Tên kho bãi
        /// </summary>
        public string WarehouseName { get; set; } = "Kho Tổng Vận Hành Logistics";

        /// <summary>
        /// Phân khu kho: Khu A (Điện tử), Khu B (Tiêu dùng nhanh), Khu C (Hàng cồng kềnh)
        /// </summary>
        public string Zone { get; set; } = "Khu Lưu Trữ";

        /// <summary>
        /// Dãy hành lang (Aisle)
        /// </summary>
        public string Aisle { get; set; } = "A";

        /// <summary>
        /// Khung kệ (Rack)
        /// </summary>
        public string Rack { get; set; } = "01";

        /// <summary>
        /// Tầng kệ (Shelf)
        /// </summary>
        public string Shelf { get; set; } = "1";

        /// <summary>
        /// Ngăn chứa / Ô chứa (Bin)
        /// </summary>
        public string Bin { get; set; } = "01";

        /// <summary>
        /// Sức chứa tải trọng tối đa của ô kệ (Kg)
        /// </summary>
        public double MaxWeightCapacity { get; set; } = 1000.0;

        /// <summary>
        /// Khối lượng hàng hóa thực tế đang đặt trên kệ hiện tại (Kg)
        /// </summary>
        public double CurrentWeight { get; set; } = 0.0;

        /// <summary>
        /// Sức chứa thể tích tối đa của ô kệ (m3)
        /// </summary>
        public double MaxVolumeCapacity { get; set; } = 5.0;

        /// <summary>
        /// Trạng thái ô kệ kho
        /// </summary>
        public LocationStatus Status { get; set; } = LocationStatus.Empty;

        /// <summary>
        /// THUỘC TÍNH TỰ ĐỘNG TÍNH TOÁN: Tỷ lệ lấp đầy ô kệ theo tải trọng (%)
        /// Đã thêm setter rỗng để ngăn chặn lỗi TwoWay binding mặc định của WPF ProgressBar
        /// </summary>
        public double OccupancyPercentage
        {
            get => MaxWeightCapacity > 0 
                ? Math.Round(Math.Min(100.0, (CurrentWeight / MaxWeightCapacity) * 100.0), 1) 
                : 0;
            set { }
        }

        /// <summary>
        /// Tên hiển thị trạng thái vị trí bằng tiếng Việt
        /// </summary>
        public string StatusDisplayName
        {
            get => Status switch
            {
                LocationStatus.Empty => "Trống (Sẵn sàng)",
                LocationStatus.PartiallyFull => "Đang chứa hàng",
                LocationStatus.Full => "Đã đầy",
                LocationStatus.Maintenance => "Bảo trì",
                _ => "Không xác định"
            };
            set { }
        }

        /// <summary>
        /// Mã màu sắc hiển thị trạng thái
        /// </summary>
        public string StatusColor
        {
            get => Status switch
            {
                LocationStatus.Empty => "#10B981",         // Xanh lá sẵn sàng
                LocationStatus.PartiallyFull => "#3B82F6",  // Xanh dương đang chứa
                LocationStatus.Full => "#EF4444",           // Đỏ đầy
                LocationStatus.Maintenance => "#94A3B8",    // Xám bảo trì
                _ => "#6B7280"
            };
            set { }
        }
    }
}
