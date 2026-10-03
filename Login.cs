using System;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ĐỐI TƯỢNG TRẢ VỀ: Kết quả xác thực đăng nhập
    /// - Nhiệm vụ: Đóng gói trạng thái thành công/thất bại, câu thông báo và đối tượng người dùng (nếu hợp lệ).
    /// - Tương tác: Trả về cho login.xaml.cs sau khi kiểm tra CSDL.
    /// </summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public User? User { get; set; }

        public static LoginResult Ok(User nguoiDung, string thongDiep = "Đăng nhập thành công!") =>
            new() { Success = true, Message = thongDiep, User = nguoiDung };

        public static LoginResult Fail(string thongDiep) =>
            new() { Success = false, Message = thongDiep, User = null };
    }

    /// <summary>
    /// LỚP NGHIỆP VỤ: Xử lý logic đăng nhập & kiểm tra bảo mật danh tính
    /// - Nhiệm vụ: Xác minh tính hợp lệ của tài khoản, kiểm tra trạng thái kích hoạt, băm và đối soát mật khẩu.
    /// - Cách hoạt động:
    ///   + Nhận chuỗi tenDangNhap và matKhau từ giao diện.
    ///   + Tìm bản ghi tài khoản trong WarehouseContext (bảng _users của CSDL quanlykho).
    ///   + Nếu tìm thấy -> kiểm tra thuộc tính IsActive và hàm VerifyPassword.
    ///   + Lưu thông tin vào phiên UserSession nếu hợp lệ.
    /// - Tương tác dữ liệu: WarehouseContext, User.cs, UserSession.cs.
    /// </summary>
    public class Login
    {
        private readonly WarehouseContext _khoDuLieu;

        public Login() : this(WarehouseContext.Instance)
        {
        }

        public Login(WarehouseContext khoDuLieu)
        {
            _khoDuLieu = khoDuLieu ?? throw new ArgumentNullException(nameof(khoDuLieu));
        }

        /// <summary>
        /// HÀM NGHIỆP VỤ: Thực hiện xác thực tài khoản và mật khẩu
        /// </summary>
        public LoginResult Authenticate(string? tenDangNhap, string? matKhau) => XacThuc(tenDangNhap, matKhau);

        public LoginResult XacThuc(string? tenDangNhap, string? matKhau)
        {
            // 1. Kiểm tra tính hợp lệ của dữ liệu người dùng nhập vào
            if (string.IsNullOrWhiteSpace(tenDangNhap))
            {
                return LoginResult.Fail("Vui lòng nhập tên đăng nhập!");
            }

            if (string.IsNullOrEmpty(matKhau))
            {
                return LoginResult.Fail("Vui lòng nhập mật khẩu truy cập!");
            }

            // 2. Tìm kiếm tài khoản trong kho dữ liệu (CSDL SQL Server / Bộ nhớ)
            var nguoiDung = _khoDuLieu.FindUserByUsername(tenDangNhap.Trim());
            if (nguoiDung == null)
            {
                return LoginResult.Fail("Tên đăng nhập không tồn tại trong hệ thống kho!");
            }

            // 3. Kiểm tra trạng thái hoạt động của tài khoản (IsActive)
            if (!nguoiDung.IsActive)
            {
                return LoginResult.Fail("Tài khoản này đã bị khóa hoặc tạm ngừng kích hoạt. Vui lòng liên hệ Quản trị viên (Admin)!");
            }

            // 4. Đối soát mật khẩu (hỗ trợ cả giải thuật băm SHA-256 lẫn mật khẩu văn bản chuẩn)
            if (!nguoiDung.VerifyPassword(matKhau))
            {
                return LoginResult.Fail("Mật khẩu không chính xác. Vui lòng kiểm tra lại!");
            }

            // 5. Khởi tạo phiên làm việc toàn cục UserSession cho tài khoản hợp lệ
            UserSession.Instance.SetUser(nguoiDung);

            return LoginResult.Ok(nguoiDung, $"Xin chào {nguoiDung.FullName}, bạn đã đăng nhập thành công với vai trò {nguoiDung.RoleDisplayName}!");
        }
    }

    /// <summary>
    /// Bí danh (Alias) dịch vụ xác thực tài khoản
    /// </summary>
    public class LoginService : Login
    {
        public LoginService() : base() { }
        public LoginService(WarehouseContext khoDuLieu) : base(khoDuLieu) { }
    }
}
