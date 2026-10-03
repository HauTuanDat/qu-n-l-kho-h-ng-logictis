using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// THỰC THỂ PHỤ TRỢ: Mục Đơn Hàng Ứng Viên Điều Phối (Dispatch Candidate Item)
    /// - Nhiệm vụ: Hỗ trợ chọn CheckBox (IsSelected) khi điều phối viên lọc và gán đơn hàng cho Shipper.
    /// - Tương tác dữ liệu: DeliveryDispatchView.xaml, ShippingOrder.cs, WarehouseContext.cs.
    /// </summary>
    public class DispatchCandidateItem
    {
        public bool IsSelected { get; set; } = false;

        public ShippingOrder Order { get; set; } = new();

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
        public DateTime EstimatedDeliveryDate => Order.EstimatedDeliveryDate;
        public DateTime CreatedDate => Order.CreatedDate;
        public string ServiceTypeTag => Order.ServiceTypeTag;
        public string StatusDisplayName => Order.StatusDisplayName;

        public string SlaRemainingText
        {
            get
            {
                var thoiGianConLai = Order.EstimatedDeliveryDate - DateTime.Now;
                if (thoiGianConLai.TotalMinutes < 0)
                {
                    int phutTre = (int)Math.Abs(thoiGianConLai.TotalMinutes);
                    return phutTre >= 60 ? $"🚨 Quá hạn {phutTre / 60}h{phutTre % 60}p!" : $"🚨 Quá hạn {phutTre}p!";
                }
                else if (thoiGianConLai.TotalHours < 2)
                {
                    return $"⚡ Còn {(int)thoiGianConLai.TotalMinutes}p";
                }
                else if (thoiGianConLai.TotalHours < 12)
                {
                    return $"⏱️ Còn {thoiGianConLai.TotalHours:N1}h";
                }
                else
                {
                    return $"🛡️ Còn {thoiGianConLai.TotalHours:N0}h";
                }
            }
        }

        public string SlaTextColor
        {
            get
            {
                var thoiGianConLai = Order.EstimatedDeliveryDate - DateTime.Now;
                if (thoiGianConLai.TotalMinutes < 0) return "#DC2626";
                if (thoiGianConLai.TotalHours < 2) return "#EA580C";
                if (thoiGianConLai.TotalHours < 12) return "#D97706";
                return "#16A34A";
            }
        }
    }
}
