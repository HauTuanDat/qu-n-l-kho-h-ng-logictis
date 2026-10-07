using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ENUM: Các vai trò người dùng trong hệ sinh thái kho vận Logistics
    /// - Admin: Quản trị viên hệ thống (Toàn quyền quản trị, phân quyền, cấu hình)
    /// - Manager: Quản lý / Trưởng kho (Duyệt nhập xuất, kiểm kê, xem báo cáo)
    /// - Staff: Nhân viên kho vận (Tạo phiếu nhập xuất, kiểm đếm tồn bãi)
    /// </summary>
    public enum UserRole
    {
        Admin,      // Quản trị viên hệ thống (Toàn quyền)
        Manager,    // Quản lý / Trưởng kho (Duyệt nhập xuất, kiểm kê, báo cáo)
        Staff,      // Nhân viên kho vận (Tạo phiếu nhập xuất, kiểm đếm)
        Customer    // Khách hàng / Chủ Shop đối tác (Tạo đơn, theo dõi lộ trình & GPS)
    }

    /// <summary>
    /// DANH MỤC MÃ QUYỀN HẠN CHI TIẾT (Permissions)
    /// - Nhiệm vụ: Hỗ trợ phân quyền hạt nhân (Granular RBAC) cho các hành động cụ thể trong kho
    /// </summary>
    public static class Permissions
    {
        // Nhóm quyền Quản trị hệ thống
        public const string SystemAdmin = "SYSTEM_ADMIN";
        public const string ManageUsers = "MANAGE_USERS";
        public const string ViewAuditLogs = "VIEW_AUDIT_LOGS";

        // Nhóm quyền Quản lý & Phê duyệt
        public const string ApproveOrders = "APPROVE_ORDERS";
        public const string InventoryAudit = "INVENTORY_AUDIT";
        public const string ViewReports = "VIEW_REPORTS";
        public const string ManageWarehouse = "MANAGE_WAREHOUSE";

        // Nhóm quyền Vận hành trực tiếp tại kho
        public const string CreateImportOrder = "CREATE_IMPORT_ORDER";
        public const string CreateExportOrder = "CREATE_EXPORT_ORDER";
        public const string ViewStock = "VIEW_STOCK";
        public const string BarcodeScan = "BARCODE_SCAN";
    }

    /// <summary>
    /// THỰC THỂ TÀI KHOẢN NGƯỜI DÙNG (User Model)
    /// - Nhiệm vụ: Đại diện cho 1 tài khoản đăng nhập, tương thích với cấu trúc bảng _users trong SQL Server quanlykho.
    /// - Cách hoạt động: Lưu trữ định danh, mật khẩu đã băm SHA-256, họ tên, vai trò và trạng thái kích hoạt.
    /// - Tương tác dữ liệu: Bảng _users SQL Server, Login.cs, UserSession.cs, UserManagementView.xaml.cs.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? FullName { get; set; } = string.Empty;
        public string? Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Staff;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Danh sách mã quyền hạn cụ thể gắn riêng cho tài khoản
        /// </summary>
        public HashSet<string> CustomPermissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tên hiển thị tiếng Việt của vai trò
        /// </summary>
        public string RoleDisplayName => Role switch
        {
            UserRole.Admin => "Quản trị viên (Admin)",
            UserRole.Manager => "Quản lý kho (Manager)",
            UserRole.Staff => "Nhân viên kho (Staff)",
            UserRole.Customer => "Khách hàng / Chủ Shop",
            _ => "Người dùng"
        };

        /// <summary>
        /// Mã màu sắc nhận diện vai trò cho giao diện UI
        /// </summary>
        public string RoleBadgeColor => Role switch
        {
            UserRole.Admin => "#DC2626",    // Đỏ nổi bật
            UserRole.Manager => "#2563EB",  // Xanh dương doanh nghiệp
            UserRole.Staff => "#059669",    // Xanh lá vận hành
            UserRole.Customer => "#8B5CF6", // Tím sang trọng dành cho Chủ Shop
            _ => "#6B7280"
        };

        /// <summary>
        /// Mô tả chi tiết phạm vi trách nhiệm của vai trò
        /// </summary>
        public string RoleDescription => Role switch
        {
            UserRole.Admin => "Toàn quyền quản trị hệ thống, tài khoản, cấu hình và bảo mật.",
            UserRole.Manager => "Quản lý luồng hàng, duyệt phiếu xuất nhập, kiểm kê và xem báo cáo tài chính.",
            UserRole.Staff => "Thực hiện nhập kho, xuất kho, kiểm đếm tồn bãi và quét barcode.",
            UserRole.Customer => "Tạo bưu gửi trực tuyến, tra cứu tiến độ đơn hàng và theo dõi lộ trình GPS thời gian thực.",
            _ => "Quyền hạn cơ bản"
        };

        /// <summary>
        /// HÀM LOGIC: Kiểm tra quyền hạn theo vai trò mặc định kết hợp quyền mở rộng
        /// - Nhiệm vụ: Đảm bảo người dùng chỉ được thực hiện các tác vụ phù hợp với quyền hạn được cấp.
        /// </summary>
        public bool HasPermission(string maQuyenHan)
        {
            if (!IsActive) return false;

            // Admin có mọi quyền hạn tối cao
            if (Role == UserRole.Admin) return true;

            // Kiểm tra quyền gắn riêng
            if (CustomPermissions.Contains(maQuyenHan)) return true;

            // Kiểm tra phân quyền mặc định theo vai trò
            return Role switch
            {
                UserRole.Manager => maQuyenHan switch
                {
                    Permissions.ApproveOrders => true,
                    Permissions.InventoryAudit => true,
                    Permissions.ViewReports => true,
                    Permissions.ManageWarehouse => true,
                    Permissions.CreateImportOrder => true,
                    Permissions.CreateExportOrder => true,
                    Permissions.ViewStock => true,
                    Permissions.BarcodeScan => true,
                    _ => false
                },
                UserRole.Staff => maQuyenHan switch
                {
                    Permissions.CreateImportOrder => true,
                    Permissions.CreateExportOrder => true,
                    Permissions.ViewStock => true,
                    Permissions.BarcodeScan => true,
                    _ => false
                },
                UserRole.Customer => maQuyenHan switch
                {
                    Permissions.CreateExportOrder => true,
                    _ => false
                },
                _ => false
            };
        }

        /// <summary>
        /// HÀM LOGIC: Đối soát mật khẩu người dùng nhập vào với chuỗi lưu trữ
        /// - Nhiệm vụ: Hỗ trợ kiểm tra an toàn qua thuật toán băm SHA-256 đồng thời hỗ trợ mật khẩu văn bản thường.
        /// </summary>
        public bool VerifyPassword(string matKhauTho)
        {
            if (string.IsNullOrEmpty(matKhauTho)) return false;

            string chuoiBamDauVao = HashPassword(matKhauTho);
            return string.Equals(PasswordHash, chuoiBamDauVao, StringComparison.OrdinalIgnoreCase)
                || string.Equals(PasswordHash, matKhauTho, StringComparison.Ordinal); // Hỗ trợ fallback kiểm thử
        }

        /// <summary>
        /// HÀM TIỆN ÍCH: Băm mật khẩu theo thuật toán mật mã học chuẩn SHA-256
        /// - Nhiệm vụ: Mã hóa một chiều mật khẩu để không lộ mật khẩu gốc trong CSDL SQL Server.
        /// </summary>
        public static string HashPassword(string matKhauTho)
        {
            using var sha256 = SHA256.Create();
            byte[] mangByte = sha256.ComputeHash(Encoding.UTF8.GetBytes(matKhauTho));
            var boXayDungChuoi = new StringBuilder();
            foreach (var byteDon in mangByte)
            {
                boXayDungChuoi.Append(byteDon.ToString("x2"));
            }
            return boXayDungChuoi.ToString();
        }
    }
}
