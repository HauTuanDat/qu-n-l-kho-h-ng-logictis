using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// LỚP QUẢN LÝ PHIÊN LÀM VIỆC TOÀN CỤC (User Session - Singleton Pattern)
    /// - Nhiệm vụ: Lưu trữ trạng thái tài khoản đang đăng nhập hiện tại, thời điểm đăng nhập,
    ///             và cung cấp các phương thức kiểm tra phân quyền (RBAC) cho toàn bộ ứng dụng.
    /// - Cách hoạt động: 
    ///   + Khởi tạo duy nhất 1 lần (Lazy Singleton).
    ///   + Khi đăng nhập thành công: Gọi SetUser(nguoiDung).
    ///   + Khi đăng xuất: Gọi Logout() để xóa thông tin phiên.
    /// - Tương tác dữ liệu: User.cs, Login.cs, MainWindow.xaml.cs, UserManagementView.xaml.cs.
    /// </summary>
    public class UserSession
    {
        private static readonly Lazy<UserSession> _theHienDuyNhat = new(() => new UserSession());
        public static UserSession Instance => _theHienDuyNhat.Value;
        public static UserSession Current => _theHienDuyNhat.Value;

        /// <summary>
        /// Thông tin người dùng đang đăng nhập trong phiên
        /// </summary>
        public User? CurrentUser { get; private set; }

        /// <summary>
        /// Thời điểm bắt đầu phiên đăng nhập
        /// </summary>
        public DateTime? LoginTime { get; private set; }

        /// <summary>
        /// Kiểm tra đã có người dùng đăng nhập hợp lệ chưa
        /// </summary>
        public bool IsLoggedIn => CurrentUser != null;

        /// <summary>
        /// Tên vai trò hiển thị bằng tiếng Việt (Quản Trị Viên / Quản Lý Kho / Nhân Viên Kho)
        /// </summary>
        public string RoleDisplayName => CurrentUser?.RoleDisplayName ?? "Chưa đăng nhập";

        /// <summary>
        /// Họ và tên đầy đủ của người dùng
        /// </summary>
        public string FullName => CurrentUser?.FullName ?? "Khách";

        /// <summary>
        /// Sự kiện phát sinh khi phiên thay đổi (Đăng nhập / Đăng xuất)
        /// </summary>
        public event Action<User?>? SessionChanged;

        private UserSession() { }

        /// <summary>
        /// HÀM THIẾT LẬP: Gán thông tin người dùng vào phiên làm việc sau khi xác thực thành công
        /// - Nhiệm vụ: Cập nhật tài khoản hiện hành, thời gian đăng nhập và phát sự kiện SessionChanged.
        /// - Tương tác: Được gọi từ Login.cs khi thông tin tài khoản và mật khẩu trùng khớp.
        /// </summary>
        public void SetUser(User nguoiDung)
        {
            CurrentUser = nguoiDung ?? throw new ArgumentNullException(nameof(nguoiDung));
            CurrentUser.LastLoginAt = DateTime.Now;
            LoginTime = DateTime.Now;
            SessionChanged?.Invoke(CurrentUser);
        }

        /// <summary>
        /// HÀM ĐĂNG XUẤT: Hủy phiên làm việc hiện tại
        /// - Nhiệm vụ: Xóa sạch thông tin tài khoản trong bộ nhớ và phát sự kiện thông báo.
        /// - Tương tác: Được gọi từ nút Đăng Xuất trên MainWindow.
        /// </summary>
        public void Logout()
        {
            CurrentUser = null;
            LoginTime = null;
            SessionChanged?.Invoke(null);
        }

        /// <summary>
        /// HÀM KIỂM TRA PHÂN QUYỀN: Xác định người dùng có đúng vai trò chỉ định hay không
        /// </summary>
        public bool HasRole(UserRole vaiTro)
        {
            return CurrentUser != null && CurrentUser.IsActive && CurrentUser.Role == vaiTro;
        }

        /// <summary>
        /// HÀM KIỂM TRA PHÂN QUYỀN: Xác định người dùng có thuộc một trong các vai trò được cấp phép hay không
        /// </summary>
        public bool HasAnyRole(params UserRole[] danhSachVaiTro)
        {
            if (CurrentUser == null || !CurrentUser.IsActive) return false;
            foreach (var vaiTro in danhSachVaiTro)
            {
                if (CurrentUser.Role == vaiTro) return true;
            }
            return false;
        }

        /// <summary>
        /// HÀM KIỂM TRA QUYỀN HẠN CỤ THỂ (Permission Code)
        /// </summary>
        public bool HasPermission(string maQuyenHan)
        {
            if (CurrentUser == null || !CurrentUser.IsActive) return false;
            return CurrentUser.HasPermission(maQuyenHan);
        }

        public bool IsAdmin => HasRole(UserRole.Admin);
        public bool IsManager => HasRole(UserRole.Manager);
        public bool IsStaff => HasRole(UserRole.Staff);
    }
}
