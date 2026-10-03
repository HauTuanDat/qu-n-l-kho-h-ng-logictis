using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// PHÂN HỆ NGHIỆP VỤ (TÍNH NĂNG 7): Cổng Tra Cứu Vận Đơn Trực Tuyến & Timeline Hành Trình (Tracking Portal)
    /// - Nhiệm vụ: Cho phép khách hàng hoặc nhân viên CSKH tra cứu trạng thái đơn hàng thời gian thực,
    ///             hiển thị dòng thời gian chuyển phát 5 mốc trực quan (Visual Stepper),
    ///             và tính toán phần trăm hoàn thành hành trình.
    /// - Tương tác dữ liệu: WarehouseContext.cs, ShippingOrder.cs.
    /// </summary>
    public partial class TrackingPortalView : UserControl
    {
        private ShippingOrder? _donHangHienTai = null;

        public TrackingPortalView()
        {
            InitializeComponent();
            // Mặc định nạp thử một đơn hàng mẫu để giao diện luôn sinh động
            ThucHienTraCuu("LOGIX-EXP-001");
        }

        /// <summary>
        /// HÀM LOGIC: Thực hiện tìm kiếm và hiển thị toàn bộ hành trình đơn hàng
        /// - Nhiệm vụ:
        ///   1. Tìm kiếm đơn hàng theo Mã vận đơn hoặc Số điện thoại qua WarehouseContext.
        ///   2. Cập nhật thẻ Hero (Mã đơn, loại hình Express/Tiêu chuẩn, hạn SLA, thanh tiến trình %).
        ///   3. Nạp thông tin 3 thẻ: Bên gửi, Bên nhận, Tài xế & Cước COD.
        ///   4. Cập nhật trạng thái màu sắc và thời gian cho 5 mốc hành trình.
        /// </summary>
        public void ThucHienTraCuu(string? maHoacSoDienThoai = null)
        {
            string tuKhoa = (maHoacSoDienThoai ?? txtMaVanDonTraCuu.Text).Trim();
            if (string.IsNullOrWhiteSpace(tuKhoa))
            {
                MessageBox.Show("Vui lòng nhập Mã vận đơn hoặc Số điện thoại để tra cứu!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            txtMaVanDonTraCuu.Text = tuKhoa;
            var donHang = WarehouseContext.Instance.TimKiemDonHangTheoMaHoacSoDienThoai(tuKhoa);

            if (donHang == null)
            {
                _donHangHienTai = null;
                khungKhongTimThay.Visibility = Visibility.Visible;
                khungKetQuaTraCuu.Visibility = Visibility.Collapsed;
                txtLyDoKhongTimThay.Text = $"Không tìm thấy đơn hàng nào có mã hoặc số điện thoại '{tuKhoa}'. Vui lòng thử lại với các mã gợi ý bên dưới.";
                return;
            }

            _donHangHienTai = donHang;
            khungKhongTimThay.Visibility = Visibility.Collapsed;
            khungKetQuaTraCuu.Visibility = Visibility.Visible;

            // 1. CẬP NHẬT THẺ HERO BANNER
            txtKetQuaMaVanDon.Text = donHang.OrderCode;
            txtBadgeLoaiDichVu.Text = donHang.ServiceTypeTag;
            badgeLoaiDichVu.Background = donHang.IsExpress 
                ? new SolidColorBrush(Color.FromRgb(245, 243, 255)) 
                : new SolidColorBrush(Color.FromRgb(239, 246, 255));
            txtBadgeLoaiDichVu.Foreground = donHang.IsExpress 
                ? new SolidColorBrush(Color.FromRgb(124, 58, 237)) 
                : new SolidColorBrush(Color.FromRgb(37, 99, 235));

            txtBadgeTrangThaiDon.Text = donHang.StatusDisplayName;
            txtBadgeTrangThaiDon.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(donHang.StatusColor)!;

            // Tiến trình phần trăm & thanh ProgressBar
            int phanTram = donHang.Status switch
            {
                ShippingOrderStatus.NewReceived => 20,
                ShippingOrderStatus.PendingProcessing => 40,
                ShippingOrderStatus.Delivering => 70,
                ShippingOrderStatus.Delivered => 100,
                ShippingOrderStatus.Failed => 70,
                ShippingOrderStatus.Returned => 50,
                _ => 10
            };

            pbTienTrinhDonHang.Value = phanTram;
            txtPhanTramTienTrinh.Text = $"{phanTram}% Hoàn Thành";

            txtHanGiaoSla.Text = donHang.EstimatedDeliveryDate.ToString("dd/MM/yyyy HH:mm");
            badgeCanhBaoQuaHan.Visibility = donHang.IsOverdue ? Visibility.Visible : Visibility.Collapsed;

            // 2. CẬP NHẬT THÔNG TIN BÊN GỬI
            txtNguoiGuiTen.Text = string.IsNullOrWhiteSpace(donHang.SenderName) ? "Kho Tổng Tiếp Nhận" : donHang.SenderName;
            txtNguoiGuiSdt.Text = $"📞 {donHang.SenderPhone}";
            txtNguoiGuiDiaChi.Text = $"📍 {(string.IsNullOrWhiteSpace(donHang.SenderAddress) ? "Hub Tiếp Nhận Trung Tâm Hà Nội" : donHang.SenderAddress)}";
            txtHangHoaTomTat.Text = $"Kiện hàng: {donHang.ProductSummary} ({donHang.Weight:N1} kg)";

            // 3. CẬP NHẬT THÔNG TIN BÊN NHẬN
            txtNguoiNhanTen.Text = donHang.ReceiverName;
            txtNguoiNhanSdt.Text = $"📞 {donHang.ReceiverPhone}";
            txtNguoiNhanDiaChi.Text = $"📍 {donHang.ReceiverAddress}";
            txtKhuVucGiaoHang.Text = $"Khu vực: {donHang.DestinationArea}";

            // 4. CẬP NHẬT THÔNG TIN SHIPPER & THANH TOÁN
            txtTaiXePhuTrach.Text = donHang.AssignedShipperName;
            txtTaiXeSdt.Text = string.IsNullOrWhiteSpace(donHang.ShipperPhone) ? "Chưa có thông tin số điện thoại" : $"📞 {donHang.ShipperPhone}";
            txtTienCodHienThi.Text = $"{donHang.CodAmount:N0} đ";
            txtPhiVanChuyenHienThi.Text = $"{donHang.ShippingFee + donHang.ExpressSurcharge:N0} đ";
            txtTongTienKhachTra.Text = $"{donHang.TotalCustomerPayment:N0} đ";

            // 5. CẬP NHẬT DÒNG THỜI GIAN 5 MỐC HÀNH TRÌNH (TIMELINE STEPPER)
            CapNhatDongThoiGian(donHang);
        }

        /// <summary>
        /// HÀM LOGIC: Đồng bộ màu sắc, icon và thời gian cho 5 mốc hành trình
        /// </summary>
        private void CapNhatDongThoiGian(ShippingOrder donHang)
        {
            var mauXanhLa = new SolidColorBrush(Color.FromRgb(16, 185, 129));   // Đã xong (#10B981)
            var mauXanhDuong = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // Đang diễn ra (#2563EB)
            var mauXamMo = new SolidColorBrush(Color.FromRgb(226, 232, 240));   // Chưa đến (#E2E8F0)
            var mauChuXam = new SolidColorBrush(Color.FromRgb(100, 116, 139));  // #64748B

            // Mốc 1: Tiếp nhận (Luôn luôn hoàn thành)
            iconMoc1.Background = mauXanhLa;
            txtThoiGianMoc1.Text = donHang.CreatedDate.ToString("dd/MM/yyyy HH:mm");

            // Mốc 2: Phân loại kho
            if (donHang.Status >= ShippingOrderStatus.PendingProcessing)
            {
                iconMoc2.Background = mauXanhLa;
                txtIconMoc2.Text = "✓";
                txtIconMoc2.Foreground = Brushes.White;
                txtThoiGianMoc2.Text = donHang.CreatedDate.AddMinutes(25).ToString("dd/MM/yyyy HH:mm");
            }
            else
            {
                iconMoc2.Background = mauXanhDuong;
                txtIconMoc2.Text = "2";
                txtIconMoc2.Foreground = Brushes.White;
                txtThoiGianMoc2.Text = "Đang xử lý phân loại...";
            }

            // Mốc 3: Xuất kho bàn giao Shipper
            if (donHang.Status >= ShippingOrderStatus.Delivering)
            {
                iconMoc3.Background = mauXanhLa;
                txtIconMoc3.Text = "✓";
                txtIconMoc3.Foreground = Brushes.White;
                txtThoiGianMoc3.Text = donHang.CreatedDate.AddMinutes(50).ToString("dd/MM/yyyy HH:mm");
                txtMoTaMoc3.Text = $"Bưu tá {donHang.AssignedShipperName} ({donHang.ShipperPhone}) đã nhận bàn giao kiện hàng xuất bến.";
            }
            else if (donHang.Status == ShippingOrderStatus.PendingProcessing)
            {
                iconMoc3.Background = mauXanhDuong;
                txtIconMoc3.Text = "3";
                txtIconMoc3.Foreground = Brushes.White;
                txtThoiGianMoc3.Text = "Chuẩn bị bàn giao tài xế";
                txtMoTaMoc3.Text = "Đang xếp vào chuyến giao của tuyến khu vực.";
            }
            else
            {
                iconMoc3.Background = mauXamMo;
                txtIconMoc3.Text = "3";
                txtIconMoc3.Foreground = mauChuXam;
                txtThoiGianMoc3.Text = "Chưa xuất kho";
                txtMoTaMoc3.Text = "Kiện hàng chưa bàn giao cho Shipper.";
            }

            // Mốc 4: Đang trên tuyến giao hàng
            if (donHang.Status == ShippingOrderStatus.Delivering)
            {
                iconMoc4.Background = mauXanhDuong;
                txtIconMoc4.Text = "4";
                txtIconMoc4.Foreground = Brushes.White;
                txtThoiGianMoc4.Text = "Đang giao hàng trực tiếp";
                txtMoTaMoc4.Text = $"Shipper {donHang.AssignedShipperName} đang trên đường phát bưu kiện tới địa chỉ {donHang.ReceiverAddress}.";
            }
            else if (donHang.Status == ShippingOrderStatus.Delivered)
            {
                iconMoc4.Background = mauXanhLa;
                txtIconMoc4.Text = "✓";
                txtIconMoc4.Foreground = Brushes.White;
                txtThoiGianMoc4.Text = donHang.DeliveredDate?.AddMinutes(-20).ToString("dd/MM/yyyy HH:mm") ?? "Đã giao đến khu vực";
                txtMoTaMoc4.Text = "Đã phát tới địa chỉ khách nhận thành công.";
            }
            else
            {
                iconMoc4.Background = mauXamMo;
                txtIconMoc4.Text = "4";
                txtIconMoc4.Foreground = mauChuXam;
                txtThoiGianMoc4.Text = "Chưa phát hàng";
                txtMoTaMoc4.Text = "Shipper chưa bắt đầu lộ trình phát đơn này.";
            }

            // Mốc 5: Giao thành công / Kết thúc
            if (donHang.Status == ShippingOrderStatus.Delivered)
            {
                iconMoc5.Background = mauXanhLa;
                txtIconMoc5.Text = "✓";
                txtIconMoc5.Foreground = Brushes.White;
                txtThoiGianMoc5.Text = (donHang.DeliveredDate ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm");
                txtMoTaMoc5.Text = $"Khách hàng {donHang.ReceiverName} đã nhận hàng nguyên vẹn và hoàn tất thanh toán COD {donHang.TotalCustomerPayment:N0} đ.";
            }
            else if (donHang.Status == ShippingOrderStatus.Failed)
            {
                iconMoc5.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Đỏ
                txtIconMoc5.Text = "✕";
                txtIconMoc5.Foreground = Brushes.White;
                txtThoiGianMoc5.Text = "Giao hàng thất bại";
                txtMoTaMoc5.Text = "Khách hàng không nghe máy hoặc hẹn giao lại vào ca làm việc tiếp theo.";
            }
            else if (donHang.Status == ShippingOrderStatus.Returned)
            {
                iconMoc5.Background = new SolidColorBrush(Color.FromRgb(139, 92, 246)); // Tím
                txtIconMoc5.Text = "↺";
                txtIconMoc5.Foreground = Brushes.White;
                txtThoiGianMoc5.Text = "Đơn chuyển hoàn";
                txtMoTaMoc5.Text = "Khách từ chối nhận hàng, bưu kiện được hoàn trả về lại kho gửi.";
            }
            else
            {
                iconMoc5.Background = mauXamMo;
                txtIconMoc5.Text = "5";
                txtIconMoc5.Foreground = mauChuXam;
                txtThoiGianMoc5.Text = "Chưa hoàn tất";
                txtMoTaMoc5.Text = "Chờ hoàn thành giao hàng và thu tiền COD.";
            }
        }

        /// <summary>
        /// SỰ KIỆN: Nhấn nút "Tra Cứu Ngay"
        /// </summary>
        private void BtnTraCuu_Click(object sender, RoutedEventArgs e)
        {
            ThucHienTraCuu();
        }

        /// <summary>
        /// SỰ KIỆN: Nhấn Enter trong ô tìm kiếm
        /// </summary>
        private void TxtMaVanDonTraCuu_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ThucHienTraCuu();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Nhấn các nút chip gợi ý nhanh mã mẫu
        /// </summary>
        private void BtnChipGoiY_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.Tag is string maGoiY)
            {
                ThucHienTraCuu(maGoiY);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Bấm nút "Cập Nhật Tiến Trình Tiếp Theo ➔" (Mô phỏng chu trình vòng đời)
        /// </summary>
        private void BtnMoPhongTienTrinh_Click(object sender, RoutedEventArgs e)
        {
            if (_donHangHienTai == null) return;

            WarehouseContext.Instance.ChuyenBuocTienTrinhDonHang(_donHangHienTai.Id);
            ThucHienTraCuu(_donHangHienTai.OrderCode);

            MessageBox.Show($"Đã cập nhật đơn {_donHangHienTai.OrderCode} sang bước tiếp theo: {_donHangHienTai.StatusDisplayName}!", "Mô Phỏng Tiến Trình", MessageBoxButton.OK, MessageBoxImage.Information);

            if (Application.Current.MainWindow is MainWindow cuaSoChinh)
            {
                cuaSoChinh.CapNhatHuyHieuDonChoXuLy();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Sao chép mã vận đơn vào Clipboard
        /// </summary>
        private void BtnSaoChepMa_Click(object sender, RoutedEventArgs e)
        {
            if (_donHangHienTai != null)
            {
                Clipboard.SetText(_donHangHienTai.OrderCode);
                MessageBox.Show($"Đã sao chép mã vận đơn '{_donHangHienTai.OrderCode}' vào bộ nhớ tạm!", "Đã Sao Chép", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Sao chép link tra cứu công khai
        /// </summary>
        private void BtnSaoChepLink_Click(object sender, RoutedEventArgs e)
        {
            if (_donHangHienTai != null)
            {
                string linkTraCuu = $"https://tracking.logixware.vn/shipment/{_donHangHienTai.OrderCode}";
                Clipboard.SetText(linkTraCuu);
                MessageBox.Show($"Đã sao chép liên kết tra cứu trực tuyến:\n{linkTraCuu}", "Đã Sao Chép Link", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// SỰ KIỆN: In phiếu tra cứu hành trình vận đơn
        /// </summary>
        private void BtnInPhieuTraCuu_Click(object sender, RoutedEventArgs e)
        {
            if (_donHangHienTai == null) return;

            var hopThoaiIn = new PrintDialog();
            if (hopThoaiIn.ShowDialog() == true)
            {
                hopThoaiIn.PrintVisual(khungKetQuaTraCuu, $"PhieuHanhTrinh_{_donHangHienTai.OrderCode}");
                MessageBox.Show("Đã gửi thông tin hành trình vận đơn tới máy in!", "In Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Quản lý hiển thị placeholder và nút xóa trắng
        /// </summary>
        private void TxtMaVanDonTraCuu_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool coChu = !string.IsNullOrEmpty(txtMaVanDonTraCuu.Text);
            txtPlaceholderTraCuu.Visibility = coChu ? Visibility.Collapsed : Visibility.Visible;
            btnXoaTrang.Visibility = coChu ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnXoaTrang_Click(object sender, RoutedEventArgs e)
        {
            txtMaVanDonTraCuu.Text = string.Empty;
            txtMaVanDonTraCuu.Focus();
        }
    }
}
