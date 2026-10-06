using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ENUM: Trạng thái trực ban của nhân viên giao hàng (Shipper)
    /// - Active: Đang đi giao các đơn hàng trên tuyến đường
    /// - Available: Đang có mặt tại bưu cục/kho, sẵn sàng nhận thêm đơn mới
    /// - Offline: Đang nghỉ ca / ngừng nhận đơn
    /// </summary>
    public enum ShipperStatus
    {
        Active,      // Đang hoạt động / đang đi giao
        Available,   // Sẵn sàng nhận đơn mới tại bưu cục
        Offline,     // Đang nghỉ ca
        OffDuty = Offline
    }

    /// <summary>
    /// THỰC THỂ: Nhân Viên Giao Hàng (Shipper Model)
    /// - Nhiệm vụ: Quản lý hồ sơ tài xế giao nhận, phương tiện xe cộ, khu vực địa bàn phụ trách,
    ///             theo dõi số đơn đang giao, giới hạn số đơn, ca làm việc và lịch sử hoàn thành đơn hàng.
    /// - Cách hoạt động: 
    ///   + Tích hợp trực tiếp vào phân hệ Quản lý Shipper (ShipperManagementView.xaml).
    ///   + Khi điều phối viên gán đơn cho tài xế, thông tin tài xế sẽ được lưu vào đơn hàng tương ứng.
    /// - Tương tác dữ liệu: Bảng Shippers trong SQL Server quanlykho, ShipperManagementView.xaml.cs, OrderManagementView.xaml.cs.
    /// </summary>
    public class Shipper
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PhoneNumber { get => Phone; set => Phone = value; }

        /// <summary>
        /// Số Căn cước công dân / CMND của Shipper
        /// </summary>
        public string? CitizenId { get; set; } = "001095012345";

        /// <summary>
        /// Loại phương tiện: Xe máy Honda Wave / Xe máy Yamaha Sirius / Xe tải van 1.25 tấn
        /// </summary>
        public string? VehicleType { get; set; } = "Xe máy";

        /// <summary>
        /// Biển kiểm soát phương tiện (VD: 29B1-888.99)
        /// </summary>
        public string? VehiclePlate { get; set; } = string.Empty;
        public string? LicensePlate { get => VehiclePlate; set => VehiclePlate = value; }

        /// <summary>
        /// Địa bàn / Tuyến quận phụ trách giao hàng chính
        /// </summary>
        public string? DeliveryArea { get; set; } = "Quận Ba Đình";
        public string? CurrentArea { get => DeliveryArea; set => DeliveryArea = value; }

        /// <summary>
        /// Trạng thái hoạt động hiện tại
        /// </summary>
        public ShipperStatus Status { get; set; } = ShipperStatus.Active;

        /// <summary>
        /// Trạng thái khóa tài khoản Shipper (true = Bị khóa, không nhận đơn)
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Ca làm việc: Ca Sáng (07:00 - 15:00) / Ca Chiều (13:00 - 21:00) / Ca Hành Chính (08:00 - 17:30)
        /// </summary>
        public string? WorkShift { get; set; } = "Ca Sáng (07:00 - 15:00)";

        /// <summary>
        /// Giới hạn số lượng đơn hàng tối đa có thể nhận trong 1 ngày (mặc định: 25 đơn)
        /// </summary>
        public int MaxOrdersPerDay { get; set; } = 25;

        /// <summary>
        /// Tải trọng tối đa phương tiện có thể chuyên chở (kg)
        /// </summary>
        public double MaxWeightCapacity { get; set; } = 50.0;

        /// <summary>
        /// Số lượng đơn hàng Shipper đang cầm giao trên đường
        /// </summary>
        public int ActiveDeliveringCount { get; set; } = 0;

        /// <summary>
        /// Tổng số đơn hàng đã giao thành công trong ngày hôm nay
        /// </summary>
        public int CompletedTodayCount { get; set; } = 0;
        public int CompletedOrdersToday { get => CompletedTodayCount; set => CompletedTodayCount = value; }

        /// <summary>
        /// Điểm số đánh giá chất lượng dịch vụ từ khách nhận hàng (thang điểm 5 sao)
        /// </summary>
        public double Rating { get; set; } = 5.0;

        /// <summary>
        /// THUỘC TÍNH TỰ ĐỘNG TÍNH TOÁN: Tỷ lệ sử dụng năng lực giao hàng (%)
        /// Đã thêm setter rỗng để ngăn chặn lỗi TwoWay binding mặc định của WPF ProgressBar
        /// </summary>
        public double CapacityUsagePercentage
        {
            get => MaxOrdersPerDay > 0 
                ? Math.Round(Math.Min(100.0, ((double)ActiveDeliveringCount / MaxOrdersPerDay) * 100.0), 1) 
                : 0;
            set { }
        }

        /// <summary>
        /// Tên trạng thái hiển thị bằng tiếng Việt
        /// </summary>
        public string StatusDisplayName
        {
            get
            {
                if (IsLocked) return "🔒 Đã khóa";
                return Status switch
                {
                    ShipperStatus.Active => "Đang đi giao",
                    ShipperStatus.Available => "Sẵn sàng nhận đơn",
                    ShipperStatus.Offline => "Nghỉ ca",
                    _ => "Offline"
                };
            }
            set { }
        }

        /// <summary>
        /// Màu sắc Badge đại diện trạng thái Shipper
        /// </summary>
        public string StatusColor
        {
            get
            {
                if (IsLocked) return "#EF4444"; // Đỏ
                return Status switch
                {
                    ShipperStatus.Active => "#2563EB",     // Xanh dương đang đi
                    ShipperStatus.Available => "#10B981",  // Xanh lá sẵn sàng
                    ShipperStatus.Offline => "#94A3B8",    // Xám nghỉ ca
                    _ => "#6B7280"
                };
            }
            set { }
        }
    }
}
