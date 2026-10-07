using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// Interaction logic for login.xaml
    /// Phân hệ xác thực đăng nhập, bảo mật và điều hướng phiên làm việc
    /// </summary>
    public partial class login : Window
    {
        // =========================================================================
        // CÁC TRƯỜNG DỮ LIỆU ĐIỀU KHIỂN LOGIC ĐĂNG NHẬP
        // Nhiệm vụ: Lưu trữ thể hiện của dịch vụ xác thực tài khoản và cờ trạng thái ẩn/hiện mật khẩu
        // Tương tác: Kết nối trực tiếp tới class Login.cs và các control TextBox trong login.xaml
        // =========================================================================
        private readonly Login _dichVuDangNhap;
        private bool _dangHienThiMatKhau = false;

        public login()
        {
            InitializeComponent();
            _dichVuDangNhap = new Login();
        }

        #region Kéo thả & Điều khiển cửa sổ giao diện
        /// <summary>
        /// SỰ KIỆN: Kéo thả di chuyển cửa sổ ứng dụng không viền (Borderless Window)
        /// - Nhiệm vụ: Cho phép người dùng nhấn giữ chuột trái bất kỳ đâu trên nền để di chuyển cửa sổ.
        /// - Cách hoạt động: Kiểm tra MouseButton == Left và kích hoạt this.DragMove().
        /// - Tương tác dữ liệu: Thao tác trực tiếp với Window OS API.
        /// </summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Thu nhỏ cửa sổ xuống Taskbar
        /// - Nhiệm vụ: Giảm kích thước cửa sổ về trạng thái Minimized.
        /// </summary>
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// SỰ KIỆN: Đóng ứng dụng
        /// - Nhiệm vụ: Thoát hoàn toàn tiến trình ứng dụng khỏi hệ điều hành.
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        #endregion


        #region Hiển thị / Ẩn mật khẩu & Placeholder văn bản gợi ý
        /// <summary>
        /// SỰ KIỆN: Chuyển đổi qua lại giữa xem mật khẩu rõ và che dấu bằng dấu chấm
        /// - Nhiệm vụ: Tăng trải nghiệm người dùng khi nhập mật khẩu phức tạp.
        /// - Cách hoạt động: Đồng bộ chuỗi giữa PasswordBox và TextBox văn bản thường, đảo ngược cờ _dangHienThiMatKhau, đổi icon 👁️ / 🙈.
        /// - Tương tác dữ liệu: txtPassword, txtPasswordUnmasked, txtEyeIcon.
        /// </summary>
        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _dangHienThiMatKhau = !_dangHienThiMatKhau;

            if (_dangHienThiMatKhau)
            {
                txtPasswordUnmasked.Text = txtPassword.Password;
                txtPassword.Visibility = Visibility.Collapsed;
                txtPasswordUnmasked.Visibility = Visibility.Visible;
                txtEyeIcon.Text = "🙈";
                txtPasswordUnmasked.Focus();
                txtPasswordUnmasked.CaretIndex = txtPasswordUnmasked.Text.Length;
            }
            else
            {
                txtPassword.Password = txtPasswordUnmasked.Text;
                txtPasswordUnmasked.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Visible;
                txtEyeIcon.Text = "👁️";
                txtPassword.Focus();
            }

            CapNhatGoiYMangMatKhau();
        }

        private void TxtUsername_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtUsernamePlaceholder.Visibility = string.IsNullOrEmpty(txtUsername.Text) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            AnThongBao();
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            CapNhatGoiYMangMatKhau();
            AnThongBao();
        }

        private void TxtPasswordUnmasked_TextChanged(object sender, TextChangedEventArgs e)
        {
            CapNhatGoiYMangMatKhau();
            AnThongBao();
        }

        /// <summary>
        /// HÀM TIỆN ÍCH: Quản lý hiển thị chữ mờ Placeholder của ô Mật khẩu
        /// - Nhiệm vụ: Hiển thị dòng chữ 'Nhập mật khẩu truy cập' khi ô rỗng và tự ẩn khi có ký tự.
        /// - Cách hoạt động: Kiểm tra độ dài mật khẩu của control đang kích hoạt.
        /// - Tương tác dữ liệu: txtPasswordPlaceholder.
        /// </summary>
        private void CapNhatGoiYMangMatKhau()
        {
            string matKhauHienTai = _dangHienThiMatKhau ? txtPasswordUnmasked.Text : txtPassword.Password;
            txtPasswordPlaceholder.Visibility = string.IsNullOrEmpty(matKhauHienTai) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        /// <summary>
        /// SỰ KIỆN: Cho phép nhấn phím Enter để thực hiện đăng nhập ngay
        /// </summary>
        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnLogin_Click(sender, e);
            }
        }
        #endregion

        #region Xử lý Nghiệp vụ Đăng Nhập
        /// <summary>
        /// SỰ KIỆN: Người dùng nhấn nút Đăng Nhập hoặc ấn phím Enter
        /// - Nhiệm vụ: Đọc thông tin tài khoản, gọi tầng dịch vụ nghiệp vụ LoginService để xác thực với CSDL.
        /// - Cách hoạt động:
        ///   + Lấy chuỗi username và password từ form.
        ///   + Gọi _dichVuDangNhap.Authenticate(tenDangNhap, matKhau).
        ///   + Nếu thất bại: Gọi HienThiThongBao(thongDiep, laLoi: true).
        ///   + Nếu thành công: Mở MainWindow, đóng cửa sổ đăng nhập hiện tại.
        /// - Tương tác dữ liệu: Login.cs, UserSession, MainWindow.xaml.
        /// </summary>
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string tenDangNhap = txtUsername.Text?.Trim() ?? string.Empty;
            string matKhau = _dangHienThiMatKhau ? txtPasswordUnmasked.Text : txtPassword.Password;

            // Gọi logic xác thực nghiệp vụ từ Login.cs
            var ketQuaDangNhap = _dichVuDangNhap.Authenticate(tenDangNhap, matKhau);

            if (!ketQuaDangNhap.Success)
            {
                HienThiThongBao(ketQuaDangNhap.Message, laLoi: true);
                return;
            }

            // Đăng nhập thành công!
            HienThiThongBao(ketQuaDangNhap.Message, laLoi: false);

            // Khởi chạy cửa sổ chính và giải phóng cửa sổ đăng nhập
            var cuaSoChinh = new MainWindow();
            cuaSoChinh.Show();
            this.Close();
        }

        /// <summary>
        /// HÀM HIỂN THỊ: Thông báo kết quả đăng nhập (Thành công hoặc Thất bại)
        /// - Nhiệm vụ: Định dạng hộp AlertBox với màu sắc thích hợp (Đỏ khi lỗi, Xanh khi thành công).
        /// - Cách hoạt động: Cập nhật Background, BorderBrush, Text và Icon.
        /// - Tương tác dữ liệu: AlertBox, txtAlertMessage, txtAlertIcon.
        /// </summary>
        private void HienThiThongBao(string thongDiep, bool laLoi)
        {
            AlertBox.Visibility = Visibility.Visible;
            txtAlertMessage.Text = thongDiep;

            if (laLoi)
            {
                AlertBox.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); // Đỏ nhạt Red-50
                AlertBox.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 165, 165));
                txtAlertMessage.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                txtAlertIcon.Text = "⚠️";
            }
            else
            {
                AlertBox.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)); // Xanh nhạt Green-50
                AlertBox.BorderBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172));
                txtAlertMessage.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61));
                txtAlertIcon.Text = "✅";
            }
        }

        /// <summary>
        /// HÀM TIỆN ÍCH: Ẩn hộp thông báo cảnh báo khi người dùng bắt đầu nhập lại
        /// </summary>
        private void AnThongBao()
        {
            if (AlertBox.Visibility == Visibility.Visible)
            {
                AlertBox.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// SỰ KIỆN: Người dùng bấm vào liên kết 'Quên mật khẩu?'
        /// - Nhiệm vụ: Hướng dẫn người dùng liên hệ quản trị viên IT để cấp lại mật khẩu.
        /// </summary>
        private void HyperlinkForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Để khôi phục mật khẩu truy cập hệ thống Logistics, vui lòng liên hệ Bộ phận Quản trị hệ thống (IT Admin):\n\n" +
                "• Email: it-admin@logixwarehouse.vn\n" +
                "• Hotline nội bộ: 1900 8888 (Nhánh 1)\n" +
                "• Hoặc liên hệ Trưởng ca kho để được cấp lại tài khoản.",
                "Quên Mật Khẩu - Hướng Dẫn Khôi Phục",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnQuickLoginAdmin_Click(object sender, RoutedEventArgs e)
        {
            txtUsername.Text = "admin";
            txtPassword.Password = "123456";
            if (_dangHienThiMatKhau) txtPasswordUnmasked.Text = "123456";
            CapNhatGoiYMangMatKhau();
            BtnLogin_Click(sender, e);
        }

        private void BtnQuickLoginStaff_Click(object sender, RoutedEventArgs e)
        {
            txtUsername.Text = "nhanvien";
            txtPassword.Password = "123456";
            if (_dangHienThiMatKhau) txtPasswordUnmasked.Text = "123456";
            CapNhatGoiYMangMatKhau();
            BtnLogin_Click(sender, e);
        }

        private void BtnQuickLoginShop_Click(object sender, RoutedEventArgs e)
        {
            txtUsername.Text = "shop";
            txtPassword.Password = "123";
            if (_dangHienThiMatKhau) txtPasswordUnmasked.Text = "123";
            CapNhatGoiYMangMatKhau();
            BtnLogin_Click(sender, e);
        }
        #endregion
    }
}
