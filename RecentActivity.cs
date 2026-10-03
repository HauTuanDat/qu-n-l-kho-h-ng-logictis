using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ENUM: Phân loại sự kiện hoạt động trong kho vận Logistics
    /// </summary>
    public enum ActivityType
    {
        Import,      // Nhập kho
        OrderNew,    // Đơn mới phát hành
        Express,     // Đơn hỏa tốc khẩn cấp
        Delivering,  // Xuất kho điều phối đi giao
        Delivered,   // Giao thành công
        Overdue,     // Cảnh báo quá hạn
        System,      // Hoạt động hệ thống
        OrderSuccess = Delivered,
        ShipperAssigned = Delivering,
        Warning = Overdue,
        OrderFailed = System
    }

    /// <summary>
    /// THỰC THỂ: Nhật Ký Hoạt Động Gần Đây (Recent Activity Model)
    /// - Nhiệm vụ: Lưu trữ dòng thời gian sự kiện kho vận theo thời gian thực (Audit Trail / Timeline),
    ///             tương thích trực tiếp với bảng RecentActivities trong SQL Server quanlykho.
    /// - Cách hoạt động: 
    ///   + Tự động tính toán chuỗi khoảng thời gian TimeAgo ("Vừa xong", "X phút trước", "X giờ trước").
    ///   + Tự động gắn Icon cảm xúc sinh động (📥, 📦, ⚡, 🛵, ✅, ⚠️).
    /// - Tương tác dữ liệu: OverviewView.xaml, WarehouseContext.cs, SQL Server bảng RecentActivities.
    /// </summary>
    public class RecentActivity
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public ActivityType Type { get; set; } = ActivityType.System;

        /// <summary>
        /// THUỘC TÍNH TỰ ĐỘNG TÍNH TOÁN: Khoảng thời gian tương đối so với thời điểm hiện tại
        /// </summary>
        public string TimeAgo
        {
            get
            {
                var khoangThoiGian = DateTime.Now - Timestamp;
                if (khoangThoiGian.TotalMinutes < 1) return "Vừa xong";
                if (khoangThoiGian.TotalMinutes < 60) return $"{(int)khoangThoiGian.TotalMinutes} phút trước";
                if (khoangThoiGian.TotalHours < 24) return $"{(int)khoangThoiGian.TotalHours} giờ trước";
                return Timestamp.ToString("dd/MM HH:mm");
            }
        }

        /// <summary>
        /// Biểu tượng cảm xúc trực quan theo từng loại hoạt động
        /// </summary>
        public string Icon => Type switch
        {
            ActivityType.Import => "📥",
            ActivityType.OrderNew => "📦",
            ActivityType.Express => "⚡",
            ActivityType.Delivering => "🛵",
            ActivityType.Delivered => "✅",
            ActivityType.Overdue => "⚠️",
            _ => "ℹ️"
        };

        /// <summary>
        /// Mã màu nhận diện loại hoạt động trên thanh dòng thời gian
        /// </summary>
        public string BadgeColor => Type switch
        {
            ActivityType.Import => "#3B82F6",    // Xanh dương nhập hàng
            ActivityType.OrderNew => "#0EA5E9",  // Xanh da trời đơn mới
            ActivityType.Express => "#8B5CF6",   // Tím hỏa tốc
            ActivityType.Delivering => "#2563EB",// Xanh dương đậm đang giao
            ActivityType.Delivered => "#10B981", // Xanh lá hoàn tất
            ActivityType.Overdue => "#EF4444",   // Đỏ cảnh báo
            _ => "#6B7280"
        };
    }
}
