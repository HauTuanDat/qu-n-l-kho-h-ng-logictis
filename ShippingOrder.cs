using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ENUM: Các trạng thái trong vòng đời chu trình giao nhận đơn hàng Logistics
    /// - NewReceived: Đơn mới tiếp nhận vào hệ thống
    /// - PendingProcessing: Đơn chờ đóng gói, dán tem nhãn vận đơn
    /// - Delivering: Đã phân công tài xế Shipper và đang trên đường giao
    /// - Delivered: Giao hàng thành công, đã thu COD (nếu có)
    /// - Failed: Giao thất bại (khách không nghe máy, sai địa chỉ)
    /// - Returned: Khách từ chối nhận, đơn hoàn trả về kho
    /// </summary>
    public enum ShippingOrderStatus
    {
        NewReceived,       // Đơn mới tiếp nhận
        PendingProcessing, // Đơn chờ xử lý (chờ phân loại / đóng gói)
        Delivering,        // Đơn đang giao
        Delivered,         // Đơn giao thành công
        Failed,            // Đơn giao thất bại
        Returned           // Đơn hoàn trả
    }

    /// <summary>
    /// THỰC THỂ: Đơn Hàng Vận Chuyển (Shipping Order)
    /// - Nhiệm vụ: Đại diện cho 1 bưu kiện vận chuyển từ người gửi đến người nhận,
    ///             tương thích hoàn toàn với bảng ShippingOrders trong SQL Server quanlykho.
    /// - Cách hoạt động: 
    ///   + Quản lý mã vận đơn, thông tin bên gửi, bên nhận, trọng lượng, biểu phí cước và tiền thu hộ COD.
    ///   + Tự động tính toán tổng số tiền khách cần thanh toán qua công thức TotalCustomerPayment.
    ///   + Tự động kiểm tra cảnh báo quá hạn cam kết giao hàng (SLA) qua IsOverdue.
    /// - Tương tác dữ liệu: Bảng ShippingOrders trong CSDL, OrderManagementView.xaml, OverviewView.xaml.
    /// </summary>
    public class ShippingOrder
    {
        public int Id { get; set; }

        /// <summary>
        /// Mã vận đơn duy nhất (VD: LOGIX-260925-102)
        /// </summary>
        public string OrderCode { get; set; } = string.Empty;

        // --- THÔNG TIN BÊN GỬI HÀNG ---
        public string SenderName { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string? SenderAddress { get; set; } = string.Empty;

        // --- THÔNG TIN BÊN NHẬN HÀNG ---
        public string ReceiverName { get; set; } = string.Empty;
        public string ReceiverPhone { get; set; } = string.Empty;
        public string ReceiverAddress { get; set; } = string.Empty;
        public string DestinationArea { get; set; } = "Hà Nội";

        // --- THÔNG TIN HÀNG HÓA & DỊCH VỤ ---
        public string ProductSummary { get; set; } = "Hàng bưu kiện tiêu chuẩn";
        public double Weight { get; set; } = 1.0; // Đơn vị: kg

        /// <summary>
        /// Có phải đơn giao HỎA TỐC (Express) trong 2h-4h hay không
        /// </summary>
        public bool IsExpress { get; set; } = false;

        /// <summary>
        /// Số tiền thu hộ COD (Cash on Delivery)
        /// </summary>
        public decimal CodAmount { get; set; } = 0;

        /// <summary>
        /// Cước phí vận chuyển tiêu chuẩn (theo khối lượng hàng)
        /// </summary>
        public decimal ShippingFee { get; set; } = 20000;

        /// <summary>
        /// Phụ phí giao hỏa tốc Express (20.000đ khi chọn Express)
        /// </summary>
        public decimal ExpressSurcharge { get; set; } = 0;

        /// <summary>
        /// Xác định bên chi trả cước phí: true = Khách nhận trả; false = Người gửi trả (Freeship)
        /// </summary>
        public bool ReceiverPaysFee { get; set; } = true;

        /// <summary>
        /// THUỘC TÍNH TỰ ĐỘNG TÍNH TOÁN: Tổng tiền khách cần thanh toán khi nhận hàng
        /// Công thức: Tiền COD + (Nếu khách chịu ship thì cộng Phí vận chuyển + Phụ phí hỏa tốc)
        /// </summary>
        public decimal TotalCustomerPayment
        {
            get => CodAmount + (ReceiverPaysFee ? (ShippingFee + ExpressSurcharge) : 0);
            set { }
        }

        /// <summary>
        /// Trạng thái đơn hàng hiện tại trong chu trình giao nhận
        /// </summary>
        public ShippingOrderStatus Status { get; set; } = ShippingOrderStatus.NewReceived;

        // --- SHIPPER PHỤ TRÁCH GIAO HÀNG ---
        public int? AssignedShipperId { get; set; }
        public string? AssignedShipperName { get; set; } = "Chưa phân phối";
        public string? ShipperPhone { get; set; } = string.Empty;

        // --- MỐC THỜI GIAN & CAM KẾT SLA ---
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Hạn cam kết giao hàng thành công (SLA Deadline)
        /// </summary>
        public DateTime EstimatedDeliveryDate { get; set; } = DateTime.Now.AddHours(24);

        public DateTime? DeliveredDate { get; set; }

        public string? Notes { get; set; } = string.Empty;

        // --- CÁC TRƯỜNG PHỤC VỤ LOGISTICS NGƯỢC (REVERSE LOGISTICS & RTO) ---
        /// <summary>
        /// Số lần giao hàng thất bại (Tối đa 3 lần trước khi duyệt chuyển hoàn)
        /// </summary>
        public int FailedDeliveryCount { get; set; } = 0;

        /// <summary>
        /// Lý do giao thất bại gần nhất
        /// </summary>
        public string? FailureReason { get; set; } = string.Empty;

        /// <summary>
        /// Thời điểm ghi nhận giao hàng thất bại gần nhất
        /// </summary>
        public DateTime? FailureTimestamp { get; set; }

        /// <summary>
        /// Mã vận đơn chuyển hoàn (VD: RTO-260925-001)
        /// </summary>
        public string? RtoTrackingCode { get; set; } = string.Empty;

        /// <summary>
        /// Vị trí kệ lưu trữ hàng hoàn trong kho (VD: KHO-RTO-01, KHO-RTO-02)
        /// </summary>
        public string? RtoLocationCode { get; set; } = "KHO-RTO-01";

        /// <summary>
        /// Ngày phê duyệt chuyển hoàn (RTO Approved Date)
        /// </summary>
        public DateTime? RtoApprovedDate { get; set; }

        /// <summary>
        /// Cước phí hoàn hàng trả về cho bên gửi
        /// </summary>
        public decimal ReturnShippingFee { get; set; } = 10000;

        /// <summary>
        /// Mã biên bản bàn giao trả lại cho Shop
        /// </summary>
        public string? ReturnHandoverBatchCode { get; set; } = string.Empty;

        /// <summary>
        /// THUỘC TÍNH KIỂM TRA: Cảnh báo đơn quá hạn cam kết giao hàng
        /// Điều kiện: Chưa hoàn tất giao hàng và thời điểm hiện tại đã vượt quá EstimatedDeliveryDate
        /// </summary>
        public bool IsOverdue
        {
            get => (Status == ShippingOrderStatus.NewReceived || 
                    Status == ShippingOrderStatus.PendingProcessing || 
                    Status == ShippingOrderStatus.Delivering)
                   && DateTime.Now > EstimatedDeliveryDate;
            set { }
        }

        /// <summary>
        /// Tên trạng thái hiển thị bằng tiếng Việt có dấu trên giao diện
        /// </summary>
        public string StatusDisplayName
        {
            get => Status switch
            {
                ShippingOrderStatus.NewReceived => "Mới tiếp nhận",
                ShippingOrderStatus.PendingProcessing => "Chờ xử lý",
                ShippingOrderStatus.Delivering => "Đang giao hàng",
                ShippingOrderStatus.Delivered => "Giao thành công",
                ShippingOrderStatus.Failed => "Giao thất bại",
                ShippingOrderStatus.Returned => "Đơn hoàn trả",
                _ => "Không xác định"
            };
            set { }
        }

        /// <summary>
        /// Mã màu sắc nhận diện Badge trạng thái trên giao diện
        /// </summary>
        public string StatusColor
        {
            get => Status switch
            {
                ShippingOrderStatus.NewReceived => "#0EA5E9",       // Xanh da trời
                ShippingOrderStatus.PendingProcessing => "#F59E0B", // Cam cảnh báo
                ShippingOrderStatus.Delivering => "#2563EB",        // Xanh dương đang đi
                ShippingOrderStatus.Delivered => "#10B981",         // Xanh lá hoàn tất
                ShippingOrderStatus.Failed => "#EF4444",            // Đỏ thất bại
                ShippingOrderStatus.Returned => "#8B5CF6",          // Tím hoàn trả
                _ => "#6B7280"
            };
            set { }
        }

        /// <summary>
        /// Huy hiệu loại dịch vụ Express / Tiêu chuẩn
        /// </summary>
        public string ServiceTypeTag
        {
            get => IsExpress ? "⚡ EXPRESS" : "📦 TIÊU CHUẨN";
            set { }
        }

        // =========================================================================
        // TÍNH NĂNG THÔNG MINH: TỰ ĐỘNG PHÂN LUỒNG TUYẾN KHO (LOCAL HUB ROUTING)
        // - Nghiệp vụ: So khớp vị trí người nhận với vị trí Kho hiện tại (Hub Thái Nguyên).
        // - Nếu trùng tuyến kho: Hàng giao chặng cuối (Last-Mile) -> Lấy hàng giao luôn trong ca.
        // - Nếu khác tỉnh: Hàng trung chuyển (Linehaul Transit) -> Chờ xe tải chuyển tiếp.
        // =========================================================================

        /// <summary>
        /// Tên kho / bưu cục vận hành hiện tại của trạm (Mặc định: Thái Nguyên)
        /// </summary>
        public static string CurrentOperatingHub { get; set; } = "Thái Nguyên";

        /// <summary>
        /// KIỂM TRA TỰ ĐỘNG: Đơn hàng có địa chỉ người nhận thuộc cùng tuyến/địa bàn với Kho hiện tại hay không
        /// </summary>
        public bool IsLocalHubDelivery
        {
            get
            {
                string diaChi = ((DestinationArea ?? "") + " " + (ReceiverAddress ?? "")).ToLower();
                string khoHienTai = (CurrentOperatingHub ?? "thái nguyên").ToLower();

                if (khoHienTai.Contains("thái nguyên") || khoHienTai.Contains("thai nguyen"))
                {
                    return diaChi.Contains("thái nguyên") || diaChi.Contains("thai nguyen") ||
                           diaChi.Contains("thịnh đán") || diaChi.Contains("thinh dan") ||
                           diaChi.Contains("phan đình phùng") || diaChi.Contains("phan dinh phung") ||
                           diaChi.Contains("sông công") || diaChi.Contains("song cong") ||
                           diaChi.Contains("phổ yên") || diaChi.Contains("pho yen") ||
                           diaChi.Contains("lương ngọc quyến") || diaChi.Contains("luong ngoc quyen") ||
                           diaChi.Contains("hoàng văn thụ") || diaChi.Contains("hoang van thu") ||
                           diaChi.Contains("quang trung") || diaChi.Contains("tân cương") ||
                           diaChi.Contains("đồng hỷ") || diaChi.Contains("đại từ") ||
                           diaChi.Contains("phú bình") || diaChi.Contains("định hóa") ||
                           diaChi.Contains("võ nhai");
                }

                return diaChi.Contains(khoHienTai);
            }
            set { }
        }

        /// <summary>
        /// Tên phân luồng tuyến vận chuyển
        /// </summary>
        public string RoutingCategoryName
        {
            get => IsLocalHubDelivery ? "Giao Ngay (Nội Vùng)" : "Trung Chuyển (Liên Tỉnh)";
            set { }
        }

        /// <summary>
        /// Huy hiệu ngắn gọn hiển thị trên bảng vận đơn
        /// </summary>
        public string RoutingCategoryTag
        {
            get => IsLocalHubDelivery ? "🛵 GIAO NGAY" : "🚛 TRUNG CHUYỂN";
            set { }
        }

        /// <summary>
        /// Màu nền của Badge phân luồng
        /// </summary>
        public string RoutingCategoryBadgeBackground
        {
            get => IsLocalHubDelivery ? "#ECFDF5" : "#F1F5F9"; // Xanh ngọc nhạt vs Xám nhạt
            set { }
        }

        /// <summary>
        /// Màu viền của Badge phân luồng
        /// </summary>
        public string RoutingCategoryBorderColor
        {
            get => IsLocalHubDelivery ? "#10B981" : "#CBD5E1"; // Xanh lá vs Viền xám
            set { }
        }

        /// <summary>
        /// Màu chữ của Badge phân luồng
        /// </summary>
        public string RoutingCategoryTextColor
        {
            get => IsLocalHubDelivery ? "#047857" : "#475569"; // Xanh đậm nổi bật vs Xám đậm
            set { }
        }

        /// <summary>
        /// Hướng dẫn khuyến nghị thao tác nghiệp vụ cho thủ kho & điều phối viên
        /// </summary>
        public string RoutingActionRecommendation
        {
            get => IsLocalHubDelivery
                ? "⚡ Đơn nội vùng Thái Nguyên: Cùng tuyến kho, lấy hàng giao ngay cho Shipper phát trong ca, KHÔNG lưu kho!"
                : "📦 Đơn ngoại tỉnh: Chờ gom chuyến xe tải trung chuyển (Linehaul Transit) về kho đích";
            set { }
        }

        /// <summary>
        /// Điều kiện để hiển thị nút thao tác nhanh "⚡ Giao Luôn" (chỉ hiện khi là đơn nội vùng và chưa phân giao)
        /// </summary>
        public bool CanQuickDeliverLocal
        {
            get => IsLocalHubDelivery && (Status == ShippingOrderStatus.NewReceived || Status == ShippingOrderStatus.PendingProcessing);
            set { }
        }
    }
}
