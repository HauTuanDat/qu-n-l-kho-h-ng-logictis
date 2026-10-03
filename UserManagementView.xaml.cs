using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// BỘ CHUYỂN ĐỔI: Trạng thái kích hoạt tài khoản sang chuỗi tiếng Việt
    /// - Nhiệm vụ: Hiển thị "Đang hoạt động" khi IsActive == true và "Đã khóa" khi false
    /// </summary>
    public class UserActiveStatusConverter : IValueConverter
    {
        public object Convert(object giaTri, Type kieuDich, object thamSo, CultureInfo ngonNgu)
        {
            return giaTri is true ? "Đang hoạt động" : "Đã khóa";
        }

        public object ConvertBack(object giaTri, Type kieuDich, object thamSo, CultureInfo ngonNgu) => throw new NotImplementedException();
    }

    /// <summary>
    /// BỘ CHUYỂN ĐỔI: Màu sắc huy hiệu trạng thái tài khoản
    /// - Nhiệm vụ: Trả về mã màu xanh lá (#059669) nếu kích hoạt, màu đỏ (#EF4444) nếu bị khóa
    /// </summary>
    public class UserActiveColorConverter : IValueConverter
    {
        public object Convert(object giaTri, Type kieuDich, object thamSo, CultureInfo ngonNgu)
        {
            return giaTri is true ? "#059669" : "#EF4444";
        }

        public object ConvertBack(object giaTri, Type kieuDich, object thamSo, CultureInfo ngonNgu) => throw new NotImplementedException();
    }

    /// <summary>
    /// Interaction logic for UserManagementView.xaml
    /// Phân hệ Quản trị Tài khoản & Phân Quyền Hệ Thống (RBAC - Admin Only)
    /// - Nhiệm vụ: Cấp phát tài khoản, phân quyền Admin / Manager / Staff, khóa tài khoản vi phạm và đổi mật khẩu trong SQL Server
    /// </summary>
    public partial class UserManagementView : UserControl
    {
        public static readonly IValueConverter ActiveStatusConverter = new UserActiveStatusConverter();
        public static readonly IValueConverter ActiveColorConverter = new UserActiveColorConverter();

        // Biến lưu ID tài khoản đang được chọn để thực hiện đổi mật khẩu
        private int _idNguoiDungDuocChonDoiMatKhau = 0;

        public UserManagementView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp danh sách người dùng từ SQL Server và thống kê theo từng vai trò
        /// - Nhiệm vụ: Đọc toàn bộ tài khoản từ bảng _users CSDL, hiển thị lên DataGrid dgUsers và tính toán 4 thẻ KPI.
        /// - Cách hoạt động: Gọi WarehouseContext.Instance.GetAllUsers(), dùng LINQ đếm số lượng Admin, Manager, Staff.
        /// - Tương tác dữ liệu: User.cs, WarehouseContext.cs, dgUsers, các TextBlock thống kê.
        /// </summary>
        public void LoadData() => NapDuLieuNguoiDung();

        public void NapDuLieuNguoiDung()
        {
            var danhSachNguoiDung = WarehouseContext.Instance.GetAllUsers();
            dgUsers.ItemsSource = danhSachNguoiDung;

            txtTotalUsers.Text = danhSachNguoiDung.Count.ToString();
            txtAdminCount.Text = danhSachNguoiDung.Count(u => u.Role == UserRole.Admin).ToString();
            txtManagerCount.Text = danhSachNguoiDung.Count(u => u.Role == UserRole.Manager).ToString();
            txtStaffCount.Text = danhSachNguoiDung.Count(u => u.Role == UserRole.Staff).ToString();
            txtUserCount.Text = $" Đang hiển thị {danhSachNguoiDung.Count} tài khoản";
        }

        /// <summary>
        /// SỰ KIỆN: Mở hộp thoại (Modal) thêm tài khoản mới
        /// </summary>
        private void BtnOpenAddUser_Click(object sender, RoutedEventArgs e)
        {
            modalAddUser.Visibility = Visibility.Visible;
            txtNewUsername.Focus();
        }

        /// <summary>
        /// SỰ KIỆN: Đóng hộp thoại thêm tài khoản
        /// </summary>
        private void BtnCloseAddUserModal_Click(object sender, RoutedEventArgs e)
        {
            modalAddUser.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// SỰ KIỆN: Lưu tài khoản mới vào CSDL SQL Server
        /// - Nhiệm vụ: Kiểm tra tính hợp lệ dữ liệu, kiểm tra trùng lặp Username, lưu thông tin vào bảng _users trong CSDL.
        /// - Cách hoạt động:
        ///   1. Xác thực: Username, Mật khẩu, Họ tên không được để trống.
        ///   2. Kiểm tra tên đăng nhập đã tồn tại trong CSDL qua hàm FindUserByUsername.
        ///   3. Ánh xạ vai trò (Admin, Manager, Staff).
        ///   4. Tạo đối tượng User và gọi WarehouseContext.Instance.AddUser.
        /// - Tương tác dữ liệu: User.cs, WarehouseContext.cs, bảng _users SQL Server.
        /// </summary>
        private void BtnSaveNewUser_Click(object sender, RoutedEventArgs e)
        {
            string tenDangNhap = txtNewUsername.Text?.Trim() ?? string.Empty;
            string matKhau = txtNewPassword.Text?.Trim() ?? string.Empty;
            string hoVaTen = txtNewFullName.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tenDangNhap))
            {
                MessageBox.Show("Vui lòng nhập tên đăng nhập!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewUsername.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(matKhau))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu khởi tạo!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewPassword.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(hoVaTen))
            {
                MessageBox.Show("Vui lòng nhập họ và tên đầy đủ của người dùng!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewFullName.Focus();
                return;
            }

            var nguoiDungDaTonTai = WarehouseContext.Instance.FindUserByUsername(tenDangNhap);
            if (nguoiDungDaTonTai != null)
            {
                MessageBox.Show($"Tên đăng nhập [{tenDangNhap}] đã tồn tại trong hệ thống! Vui lòng chọn tên khác.",
                    "Trùng tài khoản", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewUsername.Focus();
                return;
            }

            UserRole vaiTro = cbNewUserRole.SelectedIndex switch
            {
                0 => UserRole.Admin,
                1 => UserRole.Manager,
                _ => UserRole.Staff
            };

            var nguoiDungMoi = new User
            {
                Username = tenDangNhap,
                PasswordHash = matKhau,
                FullName = hoVaTen,
                Role = vaiTro,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            WarehouseContext.Instance.AddUser(nguoiDungMoi);
            modalAddUser.Visibility = Visibility.Collapsed;

            txtNewUsername.Text = "";
            txtNewPassword.Text = "123456";
            txtNewFullName.Text = "";

            NapDuLieuNguoiDung();

            MessageBox.Show($"Tạo mới tài khoản [{nguoiDungMoi.Username}] ({nguoiDungMoi.RoleDisplayName}) thành công!",
                "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// SỰ KIỆN: Khóa hoặc Mở khóa tài khoản người dùng (Toggle Active)
        /// - Nhiệm vụ: Đổi trạng thái IsActive của người dùng trong SQL Server bảng _users.
        /// - Cách hoạt động: 
        ///   + Kiểm tra an toàn: Không cho phép quản trị viên tự khóa tài khoản của chính mình đang đăng nhập.
        ///   + Bật MessageBox xác nhận.
        ///   + Gọi WarehouseContext.Instance.ToggleUserActiveStatus(u.Id) để UPDATE cột IsActive trong CSDL.
        /// - Tương tác dữ liệu: UserSession, WarehouseContext.cs, SQL Server bảng _users.
        /// </summary>
        private void BtnToggleActive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is User nguoiDung)
            {
                // Cơ chế bảo vệ: Không cho phép tự khóa tài khoản đang thực hiện thao tác
                if (nguoiDung.Username.Equals(UserSession.Current.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Không thể tự khóa tài khoản bạn đang đăng nhập!", "Cảnh báo bảo mật", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string hanhDong = nguoiDung.IsActive ? "khóa" : "mở khóa";
                var xacNhan = MessageBox.Show($"Bạn có chắc chắn muốn {hanhDong} tài khoản [{nguoiDung.Username}] không?",
                    "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (xacNhan == MessageBoxResult.Yes)
                {
                    WarehouseContext.Instance.ToggleUserActiveStatus(nguoiDung.Id);
                    NapDuLieuNguoiDung();
                }
            }
        }

        /// <summary>
        /// SỰ KIỆN: Mở hộp thoại (Modal) đổi mật khẩu cho tài khoản
        /// </summary>
        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is User nguoiDung)
            {
                _idNguoiDungDuocChonDoiMatKhau = nguoiDung.Id;
                txtChangePassTarget.Text = $"Đổi mật khẩu cho: {nguoiDung.FullName} ({nguoiDung.Username})";
                txtNewPasswordInput.Text = "";
                modalChangePassword.Visibility = Visibility.Visible;
                txtNewPasswordInput.Focus();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Đóng hộp thoại đổi mật khẩu
        /// </summary>
        private void BtnCloseChangePassModal_Click(object sender, RoutedEventArgs e)
        {
            modalChangePassword.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// SỰ KIỆN: Xác nhận cập nhật mật khẩu mới vào CSDL SQL Server
        /// - Nhiệm vụ: Băm mật khẩu mới và gọi UPDATE bảng _users trong SQL Server.
        /// - Cách hoạt động: Đọc chuỗi mật khẩu mới, gọi WarehouseContext.Instance.UpdateUserPassword.
        /// - Tương tác dữ liệu: WarehouseContext.cs, SQL Server bảng _users.
        /// </summary>
        private void BtnConfirmChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string matKhauMoi = txtNewPasswordInput.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(matKhauMoi))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu mới!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewPasswordInput.Focus();
                return;
            }

            WarehouseContext.Instance.UpdateUserPassword(_idNguoiDungDuocChonDoiMatKhau, matKhauMoi);
            modalChangePassword.Visibility = Visibility.Collapsed;
            NapDuLieuNguoiDung();

            MessageBox.Show("Đã cập nhật mật khẩu mới thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
