using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ENUM: Các loại giao dịch biến động hàng hóa trong kho logistics
    /// </summary>
    public enum WarehouseMovementType
    {
        InboundReceiving,    // Tiếp nhận hàng vào kho
        SortingLastMile,     // Phân loại hàng giao chặng cuối (nội thành)
        SortingTransit,      // Phân loại hàng trung chuyển (liên tỉnh)
        OutboundLastMile,    // Xác nhận xuất kho giao chặng cuối
        OutboundTransit,     // Xác nhận xuất kho xe trung chuyển
        StockRelocation,     // Điều chuyển vị trí ô kệ trong kho
        InventoryAdjustment  // Kiểm kê & Cân bằng tồn
    }

    /// <summary>
    /// THỰC THỂ: Nhật Ký Biến Động Nhập / Xuất Kho (Warehouse Movement Model)
    /// - Nhiệm vụ: Ghi nhận vết kiểm toán (Audit Trail) của từng lần di chuyển bưu kiện/hàng hóa:
    ///             thời điểm vào kho, phân loại chặng cuối/trung chuyển, vị trí lưu trữ và xuất kho.
    /// - Tương tác dữ liệu: WarehouseManagementView.xaml, WarehouseContext.cs.
    /// </summary>
    public class WarehouseMovement
    {
        public int Id { get; set; }

        /// <summary>
        /// Mã phiếu giao dịch biến động (VD: GD-NK-260901, GD-XK-260902)
        /// </summary>
        public string TransactionCode { get; set; } = string.Empty;

        /// <summary>
        /// Thời điểm chính xác ghi nhận biến động vào/ra kho
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Loại nghiệp vụ biến động kho
        /// </summary>
        public WarehouseMovementType MovementType { get; set; } = WarehouseMovementType.InboundReceiving;

        /// <summary>
        /// Tên mặt hàng / bưu kiện / kiện hàng
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Mã bưu kiện / Mã đơn liên quan (nếu có, VD: LOGIX-98001)
        /// </summary>
        public string ReferenceCode { get; set; } = string.Empty;

        /// <summary>
        /// Số lượng kiện
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Khối lượng hàng (kg)
        /// </summary>
        public double Weight { get; set; } = 0.0;

        /// <summary>
        /// Nguồn gửi đến hoặc Đích đến (VD: "Shop GenZ -> Dock Inbound", "Khu A -> Shipper Tuấn", "Khu B -> Xe Tải Long Biên")
        /// </summary>
        public string SourceOrDestination { get; set; } = string.Empty;

        /// <summary>
        /// Vị trí kệ kho liên quan (VD: KHO-A-D01-K01, DOCK-INBOUND-01)
        /// </summary>
        public string LocationCode { get; set; } = "DOCK-INBOUND-01";

        /// <summary>
        /// Nhân viên kho hoặc Quản lý kho thực hiện giao dịch
        /// </summary>
        public string OperatorName { get; set; } = "Thủ Kho Hệ Thống";

        /// <summary>
        /// Ghi chú nghiệp vụ
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Tên loại giao dịch hiển thị bằng tiếng Việt
        /// </summary>
        public string MovementTypeDisplayName => MovementType switch
        {
            WarehouseMovementType.InboundReceiving => "📥 Tiếp Nhận Hàng",
            WarehouseMovementType.SortingLastMile => "🏷️ Phân Loại Chặng Cuối",
            WarehouseMovementType.SortingTransit => "🚛 Phân Loại Trung Chuyển",
            WarehouseMovementType.OutboundLastMile => "📤 Xuất Hàng Chặng Cuối",
            WarehouseMovementType.OutboundTransit => "🚚 Xuất Hàng Trung Chuyển",
            WarehouseMovementType.StockRelocation => "🔄 Điều Chuyển Vị Trí Kệ",
            WarehouseMovementType.InventoryAdjustment => "📋 Kiểm Kê / Cân Bằng Tồn",
            _ => "Biến động khác"
        };

        /// <summary>
        /// Màu sắc nhận diện loại giao dịch trên bảng
        /// </summary>
        public string MovementTypeColor => MovementType switch
        {
            WarehouseMovementType.InboundReceiving => "#0284C7",    // Xanh da trời
            WarehouseMovementType.SortingLastMile => "#7C3AED",     // Tím chặng cuối
            WarehouseMovementType.SortingTransit => "#D97706",      // Vàng cam trung chuyển
            WarehouseMovementType.OutboundLastMile => "#059669",    // Xanh lá xuất phát
            WarehouseMovementType.OutboundTransit => "#2563EB",     // Xanh dương xuất xe tải
            WarehouseMovementType.StockRelocation => "#475569",     // Xám điều chuyển
            WarehouseMovementType.InventoryAdjustment => "#9333EA", // Tím kiểm kê
            _ => "#64748B"
        };
    }
}
