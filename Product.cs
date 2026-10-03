using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// THỰC THỂ: Hàng Hóa / Sản Phẩm Logistics (Product Model)
    /// - Nhiệm vụ: Quản lý thuộc tính vật lý của hàng hóa: Mã SKU, Tên, Đơn vị tính, Ngành hàng, Đơn giá, Cân nặng và Thể tích khối,
    ///             đồng bộ trực tiếp với bảng product trong SQL Server quanlykho.
    /// - Cách hoạt động: 
    ///   + VolumeCbm = Dài x Rộng x Cao / 1.000.000 (m3 - CBM tính dung tích kệ và thể tích xe tải).
    ///   + VolumetricWeight = Dài x Rộng x Cao / 5000 (Trọng lượng thể tích quy đổi trong ngành vận chuyển hàng không/đường bộ).
    ///   + ChargeableWeight = Max(Cân nặng thực tế, Trọng lượng thể tích) để tính cước vận chuyển chính xác nhất.
    /// - Tương tác dữ liệu: Bảng product CSDL quanlykho, Inventory.cs, InventoryManagementView.xaml.cs.
    /// </summary>
    public class Product
    {
        public int Id { get; set; }

        /// <summary>
        /// Mã định danh sản phẩm / SKU / Mã vạch Barcode (Ánh xạ từ cột SKU / ProductID trong CSDL)
        /// </summary>
        public string ProductCode { get; set; } = string.Empty;

        /// <summary>
        /// Tên sản phẩm / Kiện hàng (Ánh xạ từ cột ProductName trong CSDL)
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Đơn vị tính: Thùng, Kiện, Chiếc, Hộp, Bao, Kg...
        /// </summary>
        public string Unit { get; set; } = "Kiện";

        /// <summary>
        /// Danh mục ngành hàng: Linh kiện điện tử, Thời trang, Tiêu dùng nhanh (FMCG), Hóa mỹ phẩm, Hàng cồng kềnh...
        /// </summary>
        public string Category { get; set; } = "Tổng hợp";

        /// <summary>
        /// Đơn giá niêm yết / Giá trị hàng hóa (VNĐ)
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Cân nặng thực tế (Kg) - Phục vụ tính tải trọng xe tải & cước phí
        /// </summary>
        public double Weight { get; set; }

        // ================= CÁC TRƯỜNG PHỤC VỤ NGHIỆP VỤ LOGISTICS NÂNG CAO =================

        /// <summary>
        /// Kích thước chiều dài (cm)
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// Kích thước chiều rộng (cm)
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Kích thước chiều cao (cm)
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// THỂ TÍCH KHỐI LOGISTICS (m3 - CBM): Dài x Rộng x Cao / 1.000.000
        /// Rất quan trọng để tính dung lượng chứa của kho bãi và khoang xe tải
        /// </summary>
        public double VolumeCbm => Math.Round((Length * Width * Height) / 1_000_000.0, 4);

        /// <summary>
        /// TRỌNG LƯỢNG THỂ TÍCH QUY ĐỔI: Dài x Rộng x Cao / 5000 (chuẩn IATA/Logistics)
        /// </summary>
        public double VolumetricWeight => Math.Round((Length * Width * Height) / 5000.0, 2);

        /// <summary>
        /// TRỌNG LƯỢNG TÍNH CƯỚC: Lấy giá trị lớn nhất giữa cân nặng thực tế và trọng lượng quy đổi
        /// </summary>
        public double ChargeableWeight => Math.Max(Weight, VolumetricWeight);

        /// <summary>
        /// Đánh dấu hàng yêu cầu lưu ý đặc biệt: Hàng dễ vỡ, bảo quản lạnh, pin, chất lỏng...
        /// </summary>
        public bool RequiresSpecialHandling { get; set; }

        /// <summary>
        /// Hướng dẫn xử lý kho: "Không xếp chồng quá 3 lớp", "Tránh nước", "Hàng giá trị cao"...
        /// </summary>
        public string SpecialInstructions { get; set; } = string.Empty;

        /// <summary>
        /// Mã vạch Barcode quét nhanh bằng súng quét mã khi bốc dỡ tại bến
        /// </summary>
        public string Barcode { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
