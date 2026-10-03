using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// Interaction logic for OverviewView.xaml
    /// Phân hệ Bảng điều khiển Tổng quan (Executive Dashboard)
    /// - Nhiệm vụ: Tổng hợp các chỉ số vận hành cốt lõi, cảnh báo đơn khẩn cấp và giám sát dòng sự kiện kho vận
    /// </summary>
    public partial class OverviewView : UserControl
    {
        public static readonly IValueConverter BoolToVisibilityConverter = new BooleanToVisibilityHelperConverter();

        public OverviewView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadOverviewData();
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ dữ liệu thống kê tổng quan của hệ thống
        /// - Nhiệm vụ:
        ///   1. Tổng hợp 7 chỉ số KPI thời gian thực.
        ///   2. Truy vấn danh sách các đơn hàng hỏa tốc (Express) hoặc quá hạn (Overdue) cần xử lý khẩn cấp.
        ///   3. Nạp danh sách dòng thời gian hoạt động gần đây từ bảng RecentActivities trong SQL Server.
        /// - Cách hoạt động:
        ///   + Gọi khoDuLieu.GetOverviewKpis() để lấy số liệu tổng hợp.
        ///   + Sử dụng LINQ lọc đơn IsExpress == true hoặc IsOverdue == true, sắp xếp theo EstimatedDeliveryDate.
        ///   + Đổ dữ liệu vào dgUrgentOrders và listRecentActivities.
        /// - Tương tác dữ liệu: WarehouseContext, ShippingOrder, RecentActivity, bảng điều khiển OverviewView.xaml.
        /// </summary>
        public void LoadOverviewData() => NapDuLieuTongQuan();

        public void NapDuLieuTongQuan()
        {
            var khoDuLieu = WarehouseContext.Instance;

            // 1. NẠP 7 CHỈ SỐ KPI VẬN HÀNH CỐT LÕI
            // Tương tác: Lấy kết quả từ hàm GetOverviewKpis() và gán lên các thẻ TextBlock KPI
            var (tongDon, choXuLy, hoaToc, dangGiao, giaoThanhCong, shipperHoatDong, quaHan) = khoDuLieu.GetOverviewKpis();

            txtTotalOrders.Text = tongDon.ToString("N0");
            txtPendingOrders.Text = choXuLy.ToString("N0");
            txtExpressOrders.Text = hoaToc.ToString("N0");
            txtDeliveringOrders.Text = dangGiao.ToString("N0");
            txtDeliveredOrders.Text = giaoThanhCong.ToString("N0");
            txtActiveShippers.Text = shipperHoatDong.ToString("N0");
            txtOverdueOrders.Text = quaHan.ToString("N0");

            // 2. NẠP DANH SÁCH ĐƠN HÀNG CẦN XỬ LÝ ƯU TIÊN (HỎA TỐC HOẶC QUÁ HẠN)
            // Nhiệm vụ: Giúp điều phối viên phát hiện ngay các đơn hàng có nguy cơ trễ hạn để ưu tiên xếp xe
            var danhSachDonUuTien = khoDuLieu.GetAllShippingOrders()
                .Where(donHang => donHang.IsExpress || donHang.IsOverdue)
                .OrderBy(donHang => donHang.EstimatedDeliveryDate)
                .Take(6)
                .ToList();  
            dgUrgentOrders.ItemsSource = danhSachDonUuTien;

            // 3. NẠP DANH SÁCH HOẠT ĐỘNG GẦN ĐÂY (AUDIT LOG / TIMELINE)
            // Nhiệm vụ: Ghi nhận minh bạch mọi thao tác xuất/nhập/tạo đơn/phân công tài xế
            var danhSachHoatDongGanDay = khoDuLieu.GetRecentActivities();
            listRecentActivities.ItemsSource = danhSachHoatDongGanDay;
        }

        /// <summary>
        /// SỰ KIỆN: Nhấn nút làm mới số liệu tổng quan
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            NapDuLieuTongQuan();
        }
    }

    /// <summary>
    /// BỘ CHUYỂN ĐỔI: Chuyển kiểu Boolean sang Visibility trong WPF XAML
    /// - Nhiệm vụ: Ẩn/Hiện phần tử giao diện dựa trên giá trị true/false của thuộc tính
    /// </summary>
    public class BooleanToVisibilityHelperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool giaTriDungSai)
            {
                return giaTriDungSai ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
