using System;
using System.Collections.Generic;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// THỰC THỂ: Bản Ghi Lịch Sử Phiên Điều Phối Giao Hàng (Dispatch Record)
    /// - Nhiệm vụ: Lưu trữ thông tin một chuyến/phiên điều phối bàn giao đơn hàng cho Shipper,
    ///   phục vụ lập phiếu giao hàng PDF, biên bản bàn giao và đối soát thu tiền COD.
    /// - Tương tác dữ liệu: DeliveryDispatchView.xaml, WarehouseContext.cs, Shipper.cs, ShippingOrder.cs.
    /// </summary>
    public class DispatchRecord
    {
        public int Id { get; set; }

        /// <summary>
        /// Mã phiên điều phối duy nhất (VD: DP-260925-001)
        /// </summary>
        public string DispatchCode { get; set; } = string.Empty;

        /// <summary>
        /// Ngày thực hiện giao hàng
        /// </summary>
        public DateTime DispatchDate { get; set; } = DateTime.Today;

        /// <summary>
        /// Thời điểm lập phiếu phân công
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        // --- THÔNG TIN SHIPPER PHỤ TRÁCH ---
        public int ShipperId { get; set; }
        public string ShipperName { get; set; } = string.Empty;
        public string? ShipperPhone { get; set; } = string.Empty;
        public string? VehiclePlate { get; set; } = string.Empty;
        public string? DeliveryArea { get; set; } = string.Empty;

        // --- CHỈ SỐ LÔ HÀNG BÀN GIAO ---
        public int TotalOrders { get; set; }
        public int ExpressOrdersCount { get; set; }
        public decimal TotalCodAmount { get; set; }
        public double TotalWeight { get; set; }

        /// <summary>
        /// Tên người thực hiện điều phối (Admin hoặc Quản lý kho)
        /// </summary>
        public string? DispatcherName { get; set; } = "Điều Phối Viên";

        /// <summary>
        /// Trạng thái chuyến giao: Đang Đi Giao, Hoàn Tất, Hủy
        /// </summary>
        public string? Status { get; set; } = "Đang Đi Giao";

        public string? Notes { get; set; } = string.Empty;

        /// <summary>
        /// Danh sách ID các đơn hàng nằm trong chuyến điều phối này
        /// </summary>
        public List<int> OrderIds { get; set; } = new();

        /// <summary>
        /// Màu sắc trạng thái chuyến điều phối
        /// </summary>
        public string StatusColor => Status switch
        {
            "Đang Đi Giao" => "#2563EB",
            "Hoàn Tất" => "#059669",
            "Hủy" => "#DC2626",
            _ => "#64748B"
        };
    }
}
