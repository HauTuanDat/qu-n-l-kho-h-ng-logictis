using System;
using System.Collections.Generic;
using System.Linq;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ENUM: Nguồn gốc luồng tiếp nhận đơn nhập kho (Inbound Sources)
    /// - TransitHub: Tiếp nhận từ các kho trung chuyển bưu cục liên tỉnh gửi về
    /// - Company: Tiếp nhận từ Doanh nghiệp sản xuất / Đối tác B2B phân phối
    /// - Shop: Tiếp nhận từ Shop bán lẻ thương mại điện tử (Dịch vụ Fulfillment)
    /// - Individual: Tiếp nhận đơn ký gửi trực tiếp từ khách hàng cá nhân tại quầy
    /// </summary>
    public enum ImportSourceType
    {
        TransitHub,   // Từ kho / Hub trung chuyển tỉnh gửi về
        Company,      // Từ Doanh nghiệp / Đối tác B2B
        Shop,         // Từ Cửa hàng / Shop bán lẻ (Fulfillment)
        Individual    // Từ Người gửi lẻ tại quầy
    }

    /// <summary>
    /// ENUM: Trạng thái quy trình kiểm đếm và lên kệ phiếu nhập kho
    /// - PendingInspection: Mới hạ tải từ xe - Chờ nhân viên kiểm đếm
    /// - Inspecting: Đang cân đo, kiểm đếm số lượng và quét mã barcode
    /// - Approved: Đã duyệt phiếu nhập kho, số lượng hợp lệ
    /// - PutAwayCompleted: Đã phân bổ và xếp gọn vào các vị trí kệ kho hoàn tất
    /// - Rejected: Từ chối tiếp nhận (Hàng sai quy cách, rách vỡ hoặc thất thoát)
    /// </summary>
    public enum ImportOrderStatus
    {
        PendingInspection, // Mới tiếp nhận - Chờ kiểm đếm
        Inspecting,        // Đang kiểm đếm & phân loại
        Approved,          // Đã duyệt nhập kho
        PutAwayCompleted,  // Đã phân bổ vào vị trí kệ hoàn tất
        Rejected,          // Từ chối nhận hàng (Sai quy cách / Rách vỡ)
        PendingCount = PendingInspection
    }

    /// <summary>
    /// THỰC THỂ: Phiếu Nhập Kho Đa Nguồn (Import Order)
    /// - Nhiệm vụ: Quản lý thông tin chuyến hàng nhập, xe vận tải, tài xế, mã vận đơn, tổng trọng lượng và giá trị khai báo,
    ///             tương thích hoàn toàn với bảng ImportOrders trong SQL Server quanlykho.
    /// - Cách hoạt động: 
    ///   + Lưu giữ thông tin đối tác gửi hàng, số điện thoại, biển số xe tải.
    ///   + Chứa danh sách mặt hàng kiểm đếm chi tiết (Details).
    ///   + Tự động tính tổng số lượng dự kiến và thực nhận qua LINQ.
    /// - Tương tác dữ liệu: Bảng ImportOrders CSDL, ImportManagementView.xaml.cs, RecentActivities.
    /// </summary>
    public class ImportOrder
    {
        public int Id { get; set; }

        /// <summary>
        /// Mã phiếu nhập kho quy chuẩn (VD: NK-HUB-260925-01)
        /// </summary>
        public string ImportCode { get; set; } = string.Empty;

        /// <summary>
        /// Kênh nguồn gốc nhập hàng
        /// </summary>
        public ImportSourceType SourceType { get; set; } = ImportSourceType.TransitHub;

        /// <summary>
        /// Tên đơn vị / người gửi hàng
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        public string SenderPhone { get; set; } = string.Empty;
        public string SenderAddress { get; set; } = string.Empty;

        /// <summary>
        /// Mã bảng kê vận chuyển / Mã chuyến xe tải giao tới (Manifest Number)
        /// </summary>
        public string WaybillNumber { get; set; } = string.Empty;

        /// <summary>
        /// Biển số xe tải giao hàng (nếu là xe container / xe tải trung chuyển)
        /// </summary>
        public string VehiclePlate { get; set; } = string.Empty;

        /// <summary>
        /// Tên tài xế giao hàng
        /// </summary>
        public string DriverName { get; set; } = string.Empty;

        /// <summary>
        /// Tổng khối lượng lô hàng nhập (Đơn vị: kg)
        /// </summary>
        public double TotalWeight { get; set; }

        /// <summary>
        /// Tổng giá trị hàng hóa khai báo (VNĐ)
        /// </summary>
        public decimal TotalValue { get; set; }

        /// <summary>
        /// Trạng thái kiểm đếm của phiếu nhập
        /// </summary>
        public ImportOrderStatus Status { get; set; } = ImportOrderStatus.PendingInspection;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string CreatedByName { get; set; } = "Nhân viên bến nhập";

        public DateTime? ApprovedDate { get; set; }
        public string? ApprovedByName { get; set; }

        /// <summary>
        /// Ghi chú kiểm đếm / Tình trạng seal niêm phong xe
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Danh sách chi tiết các mặt hàng trong lô nhập
        /// </summary>
        public List<ImportOrderDetail> Details { get; set; } = new();

        /// <summary>
        /// TỔNG SỐ LƯỢNG DỰ KIẾN: Tính tổng số lượng khai báo trên phiếu gửi
        /// </summary>
        public int TotalExpectedQuantity => Details.Sum(ct => ct.ExpectedQuantity);

        /// <summary>
        /// TỔNG SỐ LƯỢNG THỰC TẾ: Tính tổng số lượng nhân viên kiểm đếm thực nhận tại bến
        /// </summary>
        public int TotalActualQuantity => Details.Sum(ct => ct.ActualQuantity);

        /// <summary>
        /// Tên nguồn gốc nhập hiển thị tiếng Việt có dấu
        /// </summary>
        public string SourceTypeName => SourceType switch
        {
            ImportSourceType.TransitHub => "Hub Trung Chuyển",
            ImportSourceType.Company => "Doanh Nghiệp / B2B",
            ImportSourceType.Shop => "Cửa Hàng / Shop",
            ImportSourceType.Individual => "Khách Gửi Lẻ",
            _ => "Khác"
        };

        /// <summary>
        /// Mã màu nhận diện nguồn nhập trên giao diện
        /// </summary>
        public string SourceTypeBadgeColor => SourceType switch
        {
            ImportSourceType.TransitHub => "#3B82F6",  // Xanh dương
            ImportSourceType.Company => "#8B5CF6",     // Tím
            ImportSourceType.Shop => "#F59E0B",        // Cam
            ImportSourceType.Individual => "#10B981",  // Xanh lá
            _ => "#6B7280"
        };

        /// <summary>
        /// Tên trạng thái kiểm đếm hiển thị tiếng Việt
        /// </summary>
        public string StatusDisplayName => Status switch
        {
            ImportOrderStatus.PendingInspection => "Chờ kiểm đếm",
            ImportOrderStatus.Inspecting => "Đang kiểm đếm",
            ImportOrderStatus.Approved => "Đã duyệt nhập",
            ImportOrderStatus.PutAwayCompleted => "Đã lên kệ",
            ImportOrderStatus.Rejected => "Từ chối nhận",
            _ => "Mới tiếp nhận"
        };

        /// <summary>
        /// Mã màu nhận diện trạng thái kiểm đếm
        /// </summary>
        public string StatusColor => Status switch
        {
            ImportOrderStatus.PendingInspection => "#F59E0B", // Vàng cam
            ImportOrderStatus.Inspecting => "#3B82F6",        // Xanh dương
            ImportOrderStatus.Approved => "#10B981",          // Xanh lá
            ImportOrderStatus.PutAwayCompleted => "#059669",  // Xanh đậm
            ImportOrderStatus.Rejected => "#EF4444",          // Đỏ
            _ => "#6B7280"
        };
    }

    /// <summary>
    /// THỰC THỂ: Chi Tiết Mặt Hàng Trong Phiếu Nhập Kho
    /// - Nhiệm vụ: Lưu trữ số lượng dự kiến, số lượng thực nhận, số lượng hỏng vỡ và vị trí kệ phân bổ.
    /// </summary>
    public class ImportOrderDetail
    {
        public int Id { get; set; }
        public int ImportOrderId { get; set; }

        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = "Kiện";

        /// <summary>
        /// Số lượng khai báo trên vận đơn gửi
        /// </summary>
        public int ExpectedQuantity { get; set; }

        /// <summary>
        /// Số lượng thực tế kiểm đếm tại cửa kho
        /// </summary>
        public int ActualQuantity { get; set; }

        /// <summary>
        /// Hàng hư hỏng / móp méo phát hiện lúc dỡ hàng
        /// </summary>
        public int DamagedQuantity { get; set; } = 0;

        /// <summary>
        /// Chênh lệch thừa / thiếu (Actual - Expected)
        /// </summary>
        public int Discrepancy => ActualQuantity - ExpectedQuantity;

        /// <summary>
        /// Vị trí kệ phân bổ sau khi kiểm đếm hoàn tất
        /// </summary>
        public string AssignedLocationCode { get; set; } = "KE-A-01";
    }
}
