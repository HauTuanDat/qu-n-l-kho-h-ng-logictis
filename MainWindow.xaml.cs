using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// Điều phối chuyển trang qua ListView và ContentControl
    /// </summary>
    public partial class MainWindow : Window
    {
        // =========================================================================
        // CÁC TRƯỜNG DỮ LIỆU ĐẠI DIỆN CHO 7 PHÂN HỆ CHÍNH (LƯU TRỮ ĐƠN THỂ CACHE)
        // Nhiệm vụ: Tối ưu hiệu năng, chỉ khởi tạo View 1 lần duy nhất khi người dùng mở
        // Tương tác: Nhúng trực tiếp vào MainContentArea.Content của giao diện chính
        // =========================================================================
        private OverviewView? _trangTongQuan;
        private OrderManagementView? _trangQuanLyDonHang;
        private WarehouseManagementView? _trangQuanLyKho;
        private DeliveryDispatchView? _trangDieuPhoiGiaoHang;
        private ShipperManagementView? _trangQuanLyShipper;
        private ReturnManagementView? _trangQuanLyHangHoan;
        private UserManagementView? _trangQuanLyNguoiDung;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// SỰ KIỆN: Khi cửa sổ MainWindow được tải hoàn tất (Window_Loaded)
        /// - Nhiệm vụ: Kiểm tra quyền phiên làm 
        /// 
        /// 
        /// việc, thiết lập thông tin người dùng lên Header,
        ///             cập nhật số lượng đơn chờ lên Badge và kiểm tra tình trạng kết nối CSDL SQL Server.
        /// - Cách hoạt động: 
        ///   + Lấy đối tượng tài khoản từ UserSession.Current.CurrentUser.
        ///   + Nếu chưa đăng nhập (null) -> chuyển hướng ngay về cửa sổ đăng nhập login.xaml.
        ///   + Nếu đã đăng nhập -> hiển thị Họ tên, Huy hiệu vai trò (Admin/Manager/Staff), Avatar chữ cái đầu.
        ///   + Khóa hoặc mở biểu tượng quyền Admin tương ứng.
        /// - Tương tác dữ liệu: UserSession, WarehouseContext, bảng điều khiển Header và Sidebar.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var nguoiDungHienTai = UserSession.Current.CurrentUser;

            // 1. Kiểm tra trạng thái phiên đăng nhập
            // Nếu người dùng chưa đăng nhập hợp lệ, lập tức trả về màn hình đăng nhập
            if (nguoiDungHienTai == null)
            {
                var cuaSoDangNhap = new login();
                cuaSoDangNhap.Show();
                this.Close();
                return;
            }

            // 2. Cập nhật thông tin tài khoản đang đăng nhập lên thanh Header
            // Tương tác: Đổ dữ liệu từ nguoiDungHienTai vào các TextBlock txtCurrentUserName, txtCurrentUserRole
            txtCurrentUserName.Text = nguoiDungHienTai.FullName;
            txtCurrentUserRole.Text = nguoiDungHienTai.RoleDisplayName;
            txtAvatarInitials.Text = LayKyTuDau(nguoiDungHienTai.FullName);

            try
            {
                var mauSacVaiTro = (Color)ColorConverter.ConvertFromString(nguoiDungHienTai.RoleBadgeColor);
                badgeRoleContainer.Background = new SolidColorBrush(mauSacVaiTro);
                avatarBorder.Background = new SolidColorBrush(mauSacVaiTro);
            }
            catch
            {
                badgeRoleContainer.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            }

            var thoiGianDangNhap = UserSession.Current.LoginTime ?? DateTime.Now;
            txtLoginTimestamp.Text = $"Đăng nhập lúc: {thoiGianDangNhap:HH:mm:ss}";

            // 3. Cập nhật biểu tượng khóa nếu tài khoản không mang quyền Quản trị viên (Admin)
            // Nhiệm vụ: Báo hiệu trực quan cho nhân viên biết mục Quản trị người dùng có bị khóa hay không
            badgeAdminLock.Visibility = nguoiDungHienTai.Role == UserRole.Admin ? Visibility.Collapsed : Visibility.Visible;

            // 4. Cập nhật số lượng đơn chờ xử lý lên Badge của danh mục Điều hướng
            CapNhatHuyHieuDonChoXuLy();

            // 5. Cập nhật trạng thái kết nối máy chủ SQL Server quanlykho
            CapNhatTrangThaiKetNoiCoSoDuLieu();

            // 6. Hiện đầy đủ các nghiệp vụ quản lý kho bãi cho nhân viên điều hành kho / Quản lý / Admin
            navItemOverview.Visibility = Visibility.Visible;
            navItemOrders.Visibility = Visibility.Visible;
            navItemWarehouse.Visibility = Visibility.Visible;
            navItemDeliveryDispatch.Visibility = Visibility.Visible;
            navItemShipper.Visibility = Visibility.Visible;
            navItemReturnManagement.Visibility = Visibility.Visible;
            navItemUsers.Visibility = Visibility.Visible;

            txtNavCategoryTitle.Text = "PHÂN HỆ NGHIỆP VỤ KHO VẬN";

            lvSidebarNavigation.SelectedIndex = 0;
        }

        /// <summary>
        /// HÀM LOGIC: Cập nhật trạng thái kết nối SQL Server lên giao diện
        /// - Nhiệm vụ: Hiển thị chấm trạng thái màu xanh lá (Đã kết nối) hoặc màu vàng cam (Ngoại tuyến/Cache).
        /// - Cách hoạt động: Truy vấn thuộc tính IsDatabaseConnected của WarehouseContext.Instance.
        /// - Tương tác dữ liệu: borderDbStatus, dotDbStatus, txtDbStatus và ConnectionStatusMessage.
        /// </summary>
        public void CapNhatTrangThaiKetNoiCoSoDuLieu()
        {
            var khungTrangThaiKetNoi = FindName("borderDbStatus") as Border;
            var chamBieuThiKetNoi = FindName("dotDbStatus") as System.Windows.Shapes.Shape;
            var nhanTrangThaiKetNoi = FindName("txtDbStatus") as TextBlock;

            if (khungTrangThaiKetNoi == null || chamBieuThiKetNoi == null || nhanTrangThaiKetNoi == null) return;

            if (WarehouseContext.Instance.IsDatabaseConnected)
            {
                khungTrangThaiKetNoi.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // Nền xanh nhạt
                chamBieuThiKetNoi.Fill = new SolidColorBrush(Color.FromRgb(22, 163, 74));            // Chấm xanh lá đậm
                nhanTrangThaiKetNoi.Text = "SQL Server: quanlykho (Đã kết nối)";
                nhanTrangThaiKetNoi.Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52));
                khungTrangThaiKetNoi.ToolTip = WarehouseContext.Instance.ConnectionStatusMessage;
            }
            else
            {
                khungTrangThaiKetNoi.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // Nền vàng cảnh báo
                chamBieuThiKetNoi.Fill = new SolidColorBrush(Color.FromRgb(217, 119, 6));            // Chấm cam đậm
                nhanTrangThaiKetNoi.Text = "SQL Server: Ngoại tuyến (Đang dùng cache)";
                nhanTrangThaiKetNoi.Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14));
                khungTrangThaiKetNoi.ToolTip = WarehouseContext.Instance.ConnectionStatusMessage;
            }
        }

        // Tương thích ngược gọi hàm cũ nếu có
        public void UpdateDatabaseConnectionStatus() => CapNhatTrangThaiKetNoiCoSoDuLieu();

        /// <summary>
        /// HÀM LOGIC: Tính toán và hiển thị số lượng đơn hàng cần xử lý gấp
        /// - Nhiệm vụ: Đếm số lượng đơn ở trạng thái Mới Tiếp Nhận (NewReceived) hoặc Chờ Xử Lý (PendingProcessing).
        /// - Cách hoạt động: Gọi WarehouseContext.Instance.GetAllShippingOrders() và lọc bằng LINQ.
        /// - Tương tác dữ liệu: TextBlock badgePendingOrders trên thanh Sidebar.
        /// </summary>
        public void CapNhatHuyHieuDonChoXuLy()
        {
            int soLuongDonChoXuLy = WarehouseContext.Instance.GetAllShippingOrders()
                .Count(donHang => donHang.Status == ShippingOrderStatus.PendingProcessing || donHang.Status == ShippingOrderStatus.NewReceived);
            badgePendingOrders.Text = soLuongDonChoXuLy.ToString();
        }

        // Tương thích ngược gọi hàm cũ nếu có
        public void UpdatePendingOrdersBadge() => CapNhatHuyHieuDonChoXuLy();

        /// <summary>
        /// HÀM TIỆN ÍCH: Trích xuất 2 chữ cái đầu của Họ và Tên để làm hình đại diện (Avatar)
        /// - Nhiệm vụ: Tạo biểu tượng chữ viết tắt trang nhã khi người dùng chưa có file ảnh.
        /// - Cách hoạt động: Tách chuỗi Họ tên theo khoảng trắng, lấy ký tự đầu của từ đầu và từ cuối.
        /// - Tương tác dữ liệu: Chuỗi FullName từ đối tượng User.
        /// </summary>
        private string LayKyTuDau(string hoVaTen)
        {
            if (string.IsNullOrWhiteSpace(hoVaTen)) return "U";
            var cacPhanTen = hoVaTen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (cacPhanTen.Length == 1) return cacPhanTen[0].Substring(0, Math.Min(2, cacPhanTen[0].Length)).ToUpper();
            return $"{cacPhanTen[0][0]}{cacPhanTen[^1][0]}".ToUpper();
        }

        #region Xử lý Chuyển Trang qua ListView & ContentControl
        /// <summary>
        /// SỰ KIỆN: Điều hướng khi người dùng nhấn chọn mục trên thanh Sidebar
        /// - Nhiệm vụ: Hoán đổi nội dung hiển thị trong MainContentArea tương ứng với chức năng được chọn.
        /// - Cách hoạt động:
        ///   + So sánh mục được chọn (selectedItem) với các ListViewItem (navItemOverview, navItemOrders, v.v.).
        ///   + Kiểm tra bảo mật RBAC: Nếu chọn mục Quản trị người dùng mà Role != Admin -> Hiển thị cảnh báo từ chối truy cập.
        ///   + Khởi tạo lười (Lazy initialization) các UserControl, gọi hàm nạp dữ liệu và gán vào MainContentArea.Content.
        /// - Tương tác dữ liệu: MainContentArea, txtCurrentPageTitle, UserSession, và các UserControl chức năng.
        /// </summary>
        private void LvSidebarNavigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtCurrentPageTitle == null || MainContentArea == null) return;
            if (lvSidebarNavigation?.SelectedItem is not ListViewItem mucDuocChon) return;

            var nguoiDungHienTai = UserSession.Current.CurrentUser;

            if (mucDuocChon == navItemOverview)
            {
                // 1. PHÂN HỆ TỔNG QUAN HỆ THỐNG
                // Nhiệm vụ: Hiển thị 7 chỉ số KPI, danh sách đơn cần ưu tiên và dòng thời gian hoạt động gần đây
                txtCurrentPageTitle.Text = "BẢNG TỔNG QUAN HỆ THỐNG KHO & ĐIỀU PHỐI (DASHBOARD & SLA)";
                _trangTongQuan ??= new OverviewView();
                _trangTongQuan.LoadOverviewData();
                MainContentArea.Content = _trangTongQuan;
            }
            else if (mucDuocChon == navItemOrders)
            {
                // 2. PHÂN HỆ QUẢN LÝ ĐƠN HÀNG & VẬN ĐƠN (KÈM TRA CỨU HÀNH TRÌNH)
                // Nhiệm vụ: Lọc đơn theo vòng đời, tạo đơn mới, tính cước/Freeship, xuất CSV và tra cứu bưu kiện thời gian thực
                txtCurrentPageTitle.Text = "QUẢN LÝ ĐƠN HÀNG & VẬN ĐƠN (TẠO ĐƠN, VÒNG ĐỜI & TRA CỨU HÀNH TRÌNH)";
                _trangQuanLyDonHang ??= new OrderManagementView();
                _trangQuanLyDonHang.LoadData();
                MainContentArea.Content = _trangQuanLyDonHang;
            }
            else if (mucDuocChon == navItemWarehouse)
            {
                // 3. PHÂN HỆ QUẢN LÝ KHO BÃI TOÀN DIỆN (WMS HUB - 6 TABS ĐIỀU HÀNH)
                // Nhiệm vụ: Tổng quan sức chứa, Tiếp nhận Inbound (Xe tải/Khách lẻ), Tồn kho & Safe stock, Vị trí ô kệ, Xuất kho Outbound, Sổ nhật ký kho
                txtCurrentPageTitle.Text = "TRUNG TÂM PHÂN LOẠI & TRUNG CHUYỂN BƯU KIỆN (HUB WMS)";
                _trangQuanLyKho ??= new WarehouseManagementView();
                _trangQuanLyKho.NapDuLieuKho();
                MainContentArea.Content = _trangQuanLyKho;
            }
            else if (mucDuocChon == navItemDeliveryDispatch)
            {
                // 4. PHÂN HỆ TRUNG TÂM ĐIỀU PHỐI VẬN TẢI (DISPATCH & ROUTING TMS - 4 TABS)
                // Nhiệm vụ: Phân bổ năng lực SLA & Hỏa tốc, Gom tuyến & cụm địa bàn, Phân công Shipper 7 bước & Quota, Lịch sử & In phiếu PDF
                txtCurrentPageTitle.Text = "TRUNG TÂM ĐIỀU PHỐI VẬN TẢI (DISPATCH & ROUTING TMS - 4 TABS)";
                _trangDieuPhoiGiaoHang ??= new DeliveryDispatchView();
                _trangDieuPhoiGiaoHang.NapDuLieu();
                MainContentArea.Content = _trangDieuPhoiGiaoHang;
            }
            else if (mucDuocChon == navItemShipper)
            {
                // 5. PHÂN HỆ QUẢN LÝ ĐỘI XE & SHIPPER (7 NHÁNH)
                // Nhiệm vụ: Quản lý thông tin tài xế, phương tiện, năng lực giao hàng, định mức đơn/ngày, lịch trực và lịch sử giao hàng
                txtCurrentPageTitle.Text = "QUẢN LÝ ĐỘI XE & SHIPPER (HỒ SƠ, NĂNG LỰC, LỊCH TRỰC, ĐỊA BÀN)";
                _trangQuanLyShipper ??= new ShipperManagementView();
                _trangQuanLyShipper.NapDuLieuShipper();
                MainContentArea.Content = _trangQuanLyShipper;
            }
            else if (mucDuocChon == navItemReturnManagement)
            {
                // 6. PHÂN HỆ QUẢN LÝ HÀNG HOÀN & GIAO THẤT BẠI (REVERSE LOGISTICS & RTO - 6 NHÁNH)
                // Nhiệm vụ: Xử lý đơn giao thất bại, hẹn lịch phát lại, duyệt chuyển hoàn, lưu kho RTO và lập biên bản bàn giao trả Shop
                txtCurrentPageTitle.Text = "QUẢN LÝ HÀNG HOÀN & GIAO THẤT BẠI (REVERSE LOGISTICS & RTO)";
                _trangQuanLyHangHoan ??= new ReturnManagementView();
                _trangQuanLyHangHoan.NapDuLieu();
                MainContentArea.Content = _trangQuanLyHangHoan;
            }
            else if (mucDuocChon == navItemUsers)
            {
                // 7. PHÂN HỆ QUẢN TRỊ HỆ THỐNG & PHÂN QUYỀN (CHỈ DÀNH CHO ADMIN)
                // Nhiệm vụ: Kiểm soát quyền bảo mật Admin/Manager/Staff, thêm tài khoản, khóa/mở khóa tài khoản trong SQL Server
                if (nguoiDungHienTai?.Role != UserRole.Admin)
                {
                    MessageBox.Show(
                        "TRUY CẬP BỊ TỪ CHỐI!\n\n" +
                        "Chức năng Quản trị hệ thống & Phân quyền chỉ dành riêng cho tài khoản QUẢN TRỊ VIÊN (Admin).\n" +
                        $"Vai trò hiện tại của bạn: {nguoiDungHienTai?.RoleDisplayName}",
                        "Không Có Quyền Truy Cập",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Quay lại trang Tổng quan hệ thống
                    lvSidebarNavigation.SelectedItem = navItemOverview;
                    return;
                }

                txtCurrentPageTitle.Text = "QUẢN TRỊ HỆ THỐNG & PHÂN QUYỀN (ADMIN / MANAGER / STAFF)";
                _trangQuanLyNguoiDung ??= new UserManagementView();
                _trangQuanLyNguoiDung.LoadData();
                MainContentArea.Content = _trangQuanLyNguoiDung;
            }
        }
        #endregion

        #region Đăng Xuất
        /// <summary>
        /// SỰ KIỆN: Người dùng nhấn nút Đăng Xuất
        /// - Nhiệm vụ: Xác nhận mong muốn đăng xuất, xóa phiên làm việc UserSession và mở lại màn hình login.xaml.
        /// - Cách hoạt động: Hiện hộp thoại MessageBox xác nhận (Yes/No); nếu Yes -> gọi UserSession.Current.Logout().
        /// - Tương tác dữ liệu: UserSession.Instance, đóng MainWindow và mở login.
        /// </summary>
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var xacNhanDangXuat = MessageBox.Show(
                "Bạn có chắc chắn muốn đăng xuất khỏi hệ thống?",
                "Xác Nhận Đăng Xuất",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (xacNhanDangXuat == MessageBoxResult.Yes)
            {
                UserSession.Current.Logout();
                var cuaSoDangNhap = new login();
                cuaSoDangNhap.Show();
                this.Close();
            }
        }
        #endregion

        /// <summary>
        /// HÀM TIỆN ÍCH CÔNG KHAI: Chuyển hướng trực tiếp sang Phân hệ Quản Lý Đơn Hàng Vận Chuyển (Mục 2)
        /// - Nhiệm vụ: Kích hoạt điều hướng sidebar sang navItemOrders, làm mới danh sách đơn và có thể lọc theo mã đơn
        /// </summary>
        public void ChuyenSangTrangDonHang(string? maDonCanTim = null)
        {
            if (lvSidebarNavigation != null && navItemOrders != null)
            {
                txtCurrentPageTitle.Text = "QUẢN LÝ ĐƠN HÀNG & VẬN ĐƠN (TẠO ĐƠN, VÒNG ĐỜI & TRA CỨU HÀNH TRÌNH)";
                _trangQuanLyDonHang ??= new OrderManagementView();
                _trangQuanLyDonHang.LoadData();
                if (!string.IsNullOrEmpty(maDonCanTim))
                {
                    _trangQuanLyDonHang.TimKiemDonHang(maDonCanTim);
                }
                MainContentArea.Content = _trangQuanLyDonHang;
                lvSidebarNavigation.SelectedItem = navItemOrders;
            }
        }

        /// <summary>
        /// HÀM TIỆN ÍCH CÔNG KHAI: Chuyển hướng trực tiếp sang Cổng Tra Cứu Vận Đơn (Tab 2 trong Quản Lý Đơn Hàng)
        /// - Nhiệm vụ: Chuyển sang View Đơn Hàng và tự động kích hoạt Tab Tra Cứu
        /// </summary>
        public void ChuyenSangTrangTraCuu(string? maVanDon = null)
        {
            if (lvSidebarNavigation != null && navItemOrders != null)
            {
                txtCurrentPageTitle.Text = "QUẢN LÝ ĐƠN HÀNG & VẬN ĐƠN (TẠO ĐƠN, VÒNG ĐỜI & TRA CỨU HÀNH TRÌNH)";
                _trangQuanLyDonHang ??= new OrderManagementView();
                _trangQuanLyDonHang.LoadData();
                MainContentArea.Content = _trangQuanLyDonHang;
                lvSidebarNavigation.SelectedItem = navItemOrders;
                _trangQuanLyDonHang.ChuyenSangTabTraCuu(maVanDon);
            }
        }

        /// <summary>
        /// HÀM TIỆN ÍCH CÔNG KHAI: Chuyển hướng trực tiếp sang Tiếp Nhận &amp; Nhập Kho (Tab 2 trong Quản Lý Kho Bãi)
        /// </summary>
        public void ChuyenSangTrangNhapKho()
        {
            if (lvSidebarNavigation != null && navItemWarehouse != null)
            {
                txtCurrentPageTitle.Text = "TRUNG TÂM PHÂN LOẠI & TRUNG CHUYỂN BƯU KIỆN (HUB WMS)";
                _trangQuanLyKho ??= new WarehouseManagementView();
                _trangQuanLyKho.NapDuLieuKho();
                MainContentArea.Content = _trangQuanLyKho;
                lvSidebarNavigation.SelectedItem = navItemWarehouse;
                _trangQuanLyKho.ChuyenSangTabTiepNhan();
            }
        }

        /// <summary>
        /// HÀM TIỆN ÍCH CÔNG KHAI: Chuyển hướng trực tiếp sang Nghiệp Vụ Xuất Kho (Tab 5 trong Quản Lý Kho Bãi)
        /// </summary>
        public void ChuyenSangTrangXuatKho()
        {
            if (lvSidebarNavigation != null && navItemWarehouse != null)
            {
                txtCurrentPageTitle.Text = "TRUNG TÂM PHÂN LOẠI & TRUNG CHUYỂN BƯU KIỆN (HUB WMS)";
                _trangQuanLyKho ??= new WarehouseManagementView();
                _trangQuanLyKho.NapDuLieuKho();
                MainContentArea.Content = _trangQuanLyKho;
                lvSidebarNavigation.SelectedItem = navItemWarehouse;
                _trangQuanLyKho.ChuyenSangTabXuatKho();
            }
        }

        /// <summary>
        /// HÀM TIỆN ÍCH CÔNG KHAI: Chuyển hướng sang Phân Bổ Năng Lực &amp; Ma Trận SLA (Tab 1 trong Trung Tâm Điều Phối TMS)
        /// </summary>
        public void ChuyenSangTrangDieuPhoiUuTien()
        {
            if (lvSidebarNavigation != null && navItemDeliveryDispatch != null)
            {
                txtCurrentPageTitle.Text = "TRUNG TÂM ĐIỀU PHỐI VẬN TẢI (DISPATCH & ROUTING TMS - 4 TABS)";
                _trangDieuPhoiGiaoHang ??= new DeliveryDispatchView();
                _trangDieuPhoiGiaoHang.NapDuLieu();
                MainContentArea.Content = _trangDieuPhoiGiaoHang;
                lvSidebarNavigation.SelectedItem = navItemDeliveryDispatch;
                _trangDieuPhoiGiaoHang.ChuyenSangTabPhanBoSla();
            }
        }

        /// <summary>
        /// HÀM TIỆN ÍCH CÔNG KHAI: Chuyển hướng sang Gom Tuyến &amp; Cụm Địa Bàn (Tab 2 trong Trung Tâm Điều Phối TMS)
        /// </summary>
        public void ChuyenSangTrangGomTuyen(string? maTuyenHoacKhuVuc = null)
        {
            if (lvSidebarNavigation != null && navItemDeliveryDispatch != null)
            {
                txtCurrentPageTitle.Text = "TRUNG TÂM ĐIỀU PHỐI VẬN TẢI (DISPATCH & ROUTING TMS - 4 TABS)";
                _trangDieuPhoiGiaoHang ??= new DeliveryDispatchView();
                _trangDieuPhoiGiaoHang.NapDuLieu();
                MainContentArea.Content = _trangDieuPhoiGiaoHang;
                lvSidebarNavigation.SelectedItem = navItemDeliveryDispatch;
                _trangDieuPhoiGiaoHang.ChuyenSangTabGomTuyen(maTuyenHoacKhuVuc);
            }
        }

        /// <summary>
        /// HÀM TIỆN ÍCH CÔNG KHAI: Chuyển hướng sang Phân Công Shipper 7 Bước (Tab 3 trong Trung Tâm Điều Phối TMS)
        /// </summary>
        public void ChuyenSangTrangDieuPhoiGiaoHang()
        {
            if (lvSidebarNavigation != null && navItemDeliveryDispatch != null)
            {
                txtCurrentPageTitle.Text = "TRUNG TÂM ĐIỀU PHỐI VẬN TẢI (DISPATCH & ROUTING TMS - 4 TABS)";
                _trangDieuPhoiGiaoHang ??= new DeliveryDispatchView();
                _trangDieuPhoiGiaoHang.NapDuLieu();
                MainContentArea.Content = _trangDieuPhoiGiaoHang;
                lvSidebarNavigation.SelectedItem = navItemDeliveryDispatch;
                _trangDieuPhoiGiaoHang.ChuyenSangTabPhanCong();
            }
        }

        /// <summary>
        /// HÀM TIỆN ÍCH CÔNG KHAI: Chuyển hướng trực tiếp sang Phân hệ Quản Lý Shipper (Mục 5: Đội Xe &amp; Shipper)
        /// </summary>
        public void ChuyenSangTrangShipper()
        {
            if (lvSidebarNavigation != null && navItemShipper != null)
            {
                lvSidebarNavigation.SelectedItem = navItemShipper;
            }
        }

        /// <summary>
        /// HÀM TIỆN ÍCH CÔNG KHAI: Chuyển hướng trực tiếp sang Phân hệ Quản Lý Hàng Hoàn &amp; Giao Thất Bại (Mục 6: Hàng Hoàn)
        /// </summary>
        public void ChuyenSangTrangQuanLyHangHoan()
        {
            if (lvSidebarNavigation != null && navItemReturnManagement != null)
            {
                lvSidebarNavigation.SelectedItem = navItemReturnManagement;
            }
        }
    }
}
