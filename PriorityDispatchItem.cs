using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// THỰC THỂ: Mục Đơn Hàng Điều Phối Ưu Tiên (Priority Dispatch Item)
    /// - Nhiệm vụ: Đại diện cho đơn hàng sau khi được tính điểm ma trận ưu tiên (Priority Scoring)
    ///   và phân loại vào nhóm "Được duyệt giao ngay" hoặc "Lưu kho chờ ca sau".
    /// - Cách hoạt động:
    ///   + Nhận đối tượng ShippingOrder gốc.
    ///   + Tính điểm ưu tiên tổng hợp: Hỏa tốc Express (+1000đ) > Cận hạn SLA (+500đ) > Lưu kho FIFO (+50đ).
    ///   + Ghi nhận thứ hạng (Rank), quyết định phân bổ và lý do điều phối.
    /// - Tương tác dữ liệu: PriorityDispatchView.xaml, ShippingOrder.cs, WarehouseContext.cs.
    /// </summary>
    public class PriorityDispatchItem
    {
        /// <summary>
        /// Thứ hạng ưu tiên trong toàn bộ kho (1, 2, 3...)
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Đơn hàng vận chuyển gốc
        /// </summary>
        public ShippingOrder Order { get; set; } = new();

        /// <summary>
        /// Điểm số ưu tiên tổng hợp theo thuật toán đa tầng
        /// </summary>
        public double PriorityScore { get; set; }

        /// <summary>
        /// Nhãn cấp độ ưu tiên (Cấp 1: Hỏa Tốc ⚡, Cấp 2: Cận Hạn SLA ⏱️, Cấp 3: Tiêu Chuẩn 📦)
        /// </summary>
        public string PriorityLevel { get; set; } = string.Empty;

        /// <summary>
        /// Màu sắc nhận diện huy hiệu cấp độ ưu tiên
        /// </summary>
        public string PriorityBadgeColor { get; set; } = "#2563EB";

        /// <summary>
        /// Màu nền của huy hiệu
        /// </summary>
        public string PriorityBadgeBackground { get; set; } = "#EFF6FF";

        /// <summary>
        /// Quyết định phân bổ: true = Được duyệt giao ngay; false = Lưu kho ca sau
        /// </summary>
        public bool IsApprovedForDelivery { get; set; } = true;

        /// <summary>
        /// Nhãn quyết định phân bổ
        /// </summary>
        public string AllocationDecisionText => IsApprovedForDelivery ? "🟢 DUYỆT GIAO NGAY" : "🟡 LƯU KHO CA SAU";

        /// <summary>
        /// Màu quyết định phân bổ
        /// </summary>
        public string AllocationDecisionColor => IsApprovedForDelivery ? "#059669" : "#D97706";

        /// <summary>
        /// Lý do điều phối / hoãn giao
        /// </summary>
        public string AllocationReason { get; set; } = string.Empty;

        /// <summary>
        /// Thời gian cam kết SLA còn lại hiển thị trực quan (VD: Quá hạn 30p, Còn 1.5h, Còn 26h)
        /// </summary>
        public string SlaRemainingText
        {
            get
            {
                var thoiGianConLai = Order.EstimatedDeliveryDate - DateTime.Now;
                if (thoiGianConLai.TotalMinutes < 0)
                {
                    int phutTre = (int)Math.Abs(thoiGianConLai.TotalMinutes);
                    return phutTre >= 60 ? $"🚨 QUÁ HẠN {phutTre / 60}h{phutTre % 60}p!" : $"🚨 QUÁ HẠN {phutTre} phút!";
                }
                else if (thoiGianConLai.TotalHours < 2)
                {
                    return $"⚡ Giao gấp (Còn {(int)thoiGianConLai.TotalMinutes}p)";
                }
                else if (thoiGianConLai.TotalHours < 12)
                {
                    return $"⏱️ Cận hạn (Còn {thoiGianConLai.TotalHours:N1}h)";
                }
                else
                {
                    return $"🛡️ An toàn (Còn {thoiGianConLai.TotalHours:N0}h)";
                }
            }
        }

        /// <summary>
        /// Màu sắc của thời gian SLA còn lại
        /// </summary>
        public string SlaTextColor
        {
            get
            {
                var thoiGianConLai = Order.EstimatedDeliveryDate - DateTime.Now;
                if (thoiGianConLai.TotalMinutes < 0) return "#DC2626"; // Đỏ rực
                if (thoiGianConLai.TotalHours < 2) return "#EA580C";   // Cam đỏ
                if (thoiGianConLai.TotalHours < 12) return "#D97706";  // Vàng cam
                return "#16A34A";                                     // Xanh lá an toàn
            }
        }

        // --- CÁC THUỘC TÍNH CỦA ĐƠN HÀNG GỐC PHỤC VỤ BINDING NHANH ---
        public int OrderId => Order.Id;
        public string OrderCode => Order.OrderCode;
        public string ReceiverName => Order.ReceiverName;
        public string ReceiverPhone => Order.ReceiverPhone;
        public string ReceiverAddress => Order.ReceiverAddress;
        public string DestinationArea => Order.DestinationArea;
        public string ProductSummary => Order.ProductSummary;
        public double Weight => Order.Weight;
        public bool IsExpress => Order.IsExpress;
        public decimal CodAmount => Order.CodAmount;
        public decimal TotalPayment => Order.TotalCustomerPayment;
        public string ServiceTypeTag => Order.ServiceTypeTag;
        public DateTime EstimatedDeliveryDate => Order.EstimatedDeliveryDate;
        public DateTime CreatedDate => Order.CreatedDate;
    }
}
