using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// THỰC THỂ: Quản Lý Tồn Kho & Vị Trí Kệ (Inventory Model)
    /// - Nhiệm vụ: Quản lý số lượng hàng hóa thực tế tại từng tọa độ kệ kho,
    ///             tính toán lượng hàng khả dụng xuất bán, phát hiện cảnh báo sắp hết hàng và theo dõi số lô (Lot/Batch).
    /// - Cách hoạt động: 
    ///   + AvailableQuantity = Quantity - ReservedQuantity - DamagedQuantity (Khả dụng = Tồn thực - Giữ chỗ - Hỏng).
    ///   + IsLowStock = AvailableQuantity <= MinStockLevel (Cảnh báo tồn dưới mức an toàn).
    /// - Tương tác dữ liệu: Product.cs, WarehouseLocation.cs, InventoryManagementView.xaml.cs, CSDL bảng product.
    /// </summary>
    public class Inventory
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = "Kiện";

        /// <summary>
        /// Mã định danh và mã vị trí kệ kho (VD: KE-A-01, KE-B-02)
        /// </summary>
        public int WarehouseLocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;

        /// <summary>
        /// Số lượng hàng thực tế hiện có tại vị trí kệ (Physical Count)
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Số lượng đang được giữ chỗ cho các đơn hàng đang chờ đóng gói xuất giao (Reserved)
        /// </summary>
        public int ReservedQuantity { get; set; } = 0;

        /// <summary>
        /// Số lượng hàng bị lỗi, móp méo, hư hỏng trong quá trình lưu bãi (Damaged)
        /// </summary>
        public int DamagedQuantity { get; set; } = 0;

        /// <summary>
        /// THUỘC TÍNH TỰ ĐỘNG TÍNH TOÁN: Số lượng hàng hóa khả dụng thực tế có thể xuất bán
        /// Công thức: Math.Max(0, Quantity - ReservedQuantity - DamagedQuantity)
        /// </summary>
        public int AvailableQuantity => Math.Max(0, Quantity - ReservedQuantity - DamagedQuantity);

        /// <summary>
        /// Định mức tồn kho an toàn tối thiểu (Nếu AvailableQuantity nhỏ hơn hoặc bằng số này -> Hệ thống cảnh báo cần nhập thêm)
        /// </summary>
        public int MinStockLevel { get; set; } = 10;

        /// <summary>
        /// Định mức tồn kho tối đa của ô kệ (Vượt mức này -> Cảnh báo quá tải kệ)
        /// </summary>
        public int MaxStockLevel { get; set; } = 500;

        /// <summary>
        /// Số lô sản xuất / kiểm định nhập kho (Batch / Lot Number)
        /// </summary>
        public string BatchNumber { get; set; } = "LOT-2026-01";

        /// <summary>
        /// Thời điểm nhập kho gần nhất
        /// </summary>
        public DateTime LastImportDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Thời điểm xuất kho gần nhất
        /// </summary>
        public DateTime? LastExportDate { get; set; }

        /// <summary>
        /// CẢNH BÁO TỒN THẤP: True khi số lượng khả dụng đã chạm hoặc dưới ngưỡng an toàn
        /// </summary>
        public bool IsLowStock => AvailableQuantity <= MinStockLevel;

        /// <summary>
        /// CẢNH BÁO TỒN CAO: True khi số lượng tồn vượt quá sức chứa tối đa của ô kệ
        /// </summary>
        public bool IsOverStock => Quantity >= MaxStockLevel;

        /// <summary>
        /// Chuỗi trạng thái tồn kho hiển thị trực quan trên DataGrid
        /// </summary>
        public string StockStatusText
        {
            get
            {
                if (AvailableQuantity == 0) return "Hết hàng khả dụng";
                if (IsLowStock) return "Cảnh báo: Tồn thấp";
                if (IsOverStock) return "Tồn cao";
                return "Bình thường";
            }
        }

        /// <summary>
        /// Mã màu sắc tương ứng với trạng thái tồn kho
        /// </summary>
        public string StockStatusColor
        {
            get
            {
                if (AvailableQuantity == 0) return "#EF4444"; // Đỏ hết hàng
                if (IsLowStock) return "#F59E0B";             // Cam cảnh báo
                if (IsOverStock) return "#8B5CF6";            // Tím tồn cao
                return "#10B981";                             // Xanh lá an toàn
            }
        }
    }
}
