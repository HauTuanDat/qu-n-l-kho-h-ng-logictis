using System;
using System.Collections.Generic;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// THỰC THỂ: Biên Bản Bàn Giao Hàng Hoàn Trả Cho Shop (Return Handover Batch)
    /// - Nhiệm vụ: Gom danh sách các đơn hàng chuyển hoàn (RTO) thuộc cùng một Shop/Chủ hàng,
    ///   phục vụ in biên bản bàn giao, ký nhận và thu cước hoàn.
    /// - Tương tác dữ liệu: ReturnManagementView.xaml, WarehouseContext.cs, ShippingOrder.cs.
    /// </summary>
    public class ReturnHandoverBatch
    {
        public int Id { get; set; }

        /// <summary>
        /// Mã biên bản bàn giao hàng hoàn (VD: BBH-260925-001)
        /// </summary>
        public string BatchCode { get; set; } = string.Empty;

        /// <summary>
        /// Tên Shop / Đối tác gửi hàng nhận lại bưu kiện hoàn
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        public string? SenderPhone { get; set; } = string.Empty;

        public string? SenderAddress { get; set; } = string.Empty;

        public DateTime CreatedTime { get; set; } = DateTime.Now;

        public int TotalOrders { get; set; }

        /// <summary>
        /// Tổng giá trị tiền thu hộ COD của các bưu kiện hoàn
        /// </summary>
        public decimal TotalCodValue { get; set; }

        /// <summary>
        /// Tổng cước phí hoàn hàng Shop cần thanh toán
        /// </summary>
        public decimal TotalReturnFee { get; set; }

        public string? OperatorName { get; set; } = "Thủ Kho Hàng Hoàn";

        public string? Status { get; set; } = "Đang Lưu Kho"; // Đang Lưu Kho, Đã Trả Shop

        public string? Notes { get; set; } = string.Empty;

        public List<int> OrderIds { get; set; } = new();

        public string StatusColor
        {
            get => Status switch
            {
                "Đã Trả Shop" => "#059669",
                "Đang Lưu Kho" => "#D97706",
                _ => "#64748B"
            };
            set { }
        }
    }
}
