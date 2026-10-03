using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// BỘ CHUYỂN ĐỔI: Màu nền Badge số lần giao thất bại (Lần 1, 2, 3)
    /// </summary>
    public class LanThatBaiToBgConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int soLan)
            {
                return soLan switch
                {
                    <= 1 => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // #FEF3C7 vàng nhạt (Lần 1)
                    2 => new SolidColorBrush(Color.FromRgb(255, 237, 213)),    // #FFEDD5 cam nhạt (Lần 2)
                    _ => new SolidColorBrush(Color.FromRgb(254, 226, 226))     // #FEE2E2 đỏ nhạt (Lần 3+)
                };
            }
            return new SolidColorBrush(Color.FromRgb(241, 245, 249));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// BỘ CHUYỂN ĐỔI: Màu chữ Badge số lần giao thất bại (Lần 1, 2, 3)
    /// </summary>
    public class LanThatBaiToFgConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int soLan)
            {
                return soLan switch
                {
                    <= 1 => new SolidColorBrush(Color.FromRgb(180, 83, 9)),   // #B45309 vàng sẫm (Lần 1)
                    2 => new SolidColorBrush(Color.FromRgb(234, 88, 12)),     // #EA580C cam đậm (Lần 2)
                    _ => new SolidColorBrush(Color.FromRgb(220, 38, 38))      // #DC2626 đỏ đậm (Lần 3+)
                };
            }
            return new SolidColorBrush(Color.FromRgb(71, 85, 105));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// PHÂN HỆ: Quản Lý Hàng Hoàn & Giao Thất Bại (Reverse Logistics & RTO)
    /// Gồm 6 nhánh nghiệp vụ theo chuẩn quy trình Logistics ngược:
    ///   1. Đơn giao thất bại trong ngày
    ///   2. Phân loại lý do không giao được
    ///   3. Lên lịch phát lại lần 2 / lần 3
    ///   4. Duyệt chuyển hoàn (Return to Origin)
    ///   5. Khu vực kho lưu hàng hoàn
    ///   6. Biên bản bàn giao trả lại Shop
    /// </summary>
    public partial class ReturnManagementView : UserControl
    {
        // =========================================================================
        // CÁC CONVERTER TĨNH DÙNG CHO DATATEMPLATE TRONG XAML
        // =========================================================================
        public static IValueConverter LanThatBaiToBgConverter { get; } = new LanThatBaiToBgConverterImpl();
        public static IValueConverter LanThatBaiToFgConverter { get; } = new LanThatBaiToFgConverterImpl();

        // Biến lưu trữ trạng thái đơn hàng đang được thao tác trên Modal
        private ShippingOrder? _donDangThaoTac;
        private List<ShippingOrder> _danhSachDonThatBaiGoc = new();

        public ReturnManagementView()
        {
            InitializeComponent();
            Loaded += ReturnManagementView_Loaded;
        }

        private void ReturnManagementView_Loaded(object sender, RoutedEventArgs e)
        {
            NapDuLieu();
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ dữ liệu 6 nhánh từ WarehouseContext
        /// - Tương tác dữ liệu: WarehouseContext.Instance.GetAllShippingOrders() và GetAllReturnBatches()
        /// - Cập nhật các chỉ số KPI, các bảng DataGrid và danh sách phân loại.
        /// </summary>
        public void NapDuLieu()
        {
            var tatCaDon = WarehouseContext.Instance.GetAllShippingOrders();

            // Nếu chưa có đơn giao thất bại hoặc hoàn trả nào, tự động kích hoạt sinh dữ liệu mẫu
            if (!tatCaDon.Any(d => d.Status == ShippingOrderStatus.Failed || d.Status == ShippingOrderStatus.Returned))
            {
                WarehouseContext.Instance.SeedSampleRtoData();
                tatCaDon = WarehouseContext.Instance.GetAllShippingOrders();
            }

            // ================= 1. NHÁNH 1: ĐƠN GIAO THẤT BẠI TRONG NGÀY =================
            _danhSachDonThatBaiGoc = tatCaDon
                .Where(d => d.Status == ShippingOrderStatus.Failed)
                .OrderByDescending(d => d.FailureTimestamp ?? d.CreatedDate)
                .ToList();

            // Cập nhật 4 thẻ KPI
            txtTongDonThatBai.Text = $"{_danhSachDonThatBaiGoc.Count} đơn";
            txtChoPhatLai.Text = $"{_danhSachDonThatBaiGoc.Count(d => d.FailedDeliveryCount < 3)} đơn";
            txtDuDieuKienHoan.Text = $"{_danhSachDonThatBaiGoc.Count(d => d.FailedDeliveryCount >= 3 || d.FailureReason.Contains("Boom", StringComparison.OrdinalIgnoreCase) || d.FailureReason.Contains("từ chối", StringComparison.OrdinalIgnoreCase))} đơn";
            txtTongTienCodKet.Text = $"{_danhSachDonThatBaiGoc.Sum(d => d.CodAmount):N0} đ";

            // Áp dụng bộ lọc tìm kiếm
            ApDungBoLocDonThatBai();

            // ================= 3. NHÁNH 3: LÊN LỊCH PHÁT LẠI LẦN 2 / 3 =================
            var danhSachPhatLai = tatCaDon
                .Where(d => (d.Status == ShippingOrderStatus.PendingProcessing && d.FailedDeliveryCount > 0) ||
                            (d.Notes != null && d.Notes.Contains("Hẹn phát lại", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(d => d.EstimatedDeliveryDate)
                .ToList();
            dgDonPhatLai.ItemsSource = danhSachPhatLai;

            // ================= 4. NHÁNH 4: DUYỆT CHUYỂN HOÀN (RTO) =================
            var danhSachDuDieuKienHoan = _danhSachDonThatBaiGoc
                .Where(d => d.FailedDeliveryCount >= 3 ||
                            d.FailureReason.Contains("Boom", StringComparison.OrdinalIgnoreCase) ||
                            d.FailureReason.Contains("từ chối", StringComparison.OrdinalIgnoreCase))
                .ToList();
            dgDonDuDieuKienHoan.ItemsSource = danhSachDuDieuKienHoan;

            // ================= 5. NHÁNH 5: KHU VỰC KHO LƯU HÀNG HOÀN =================
            var danhSachLuuKhoHoan = tatCaDon
                .Where(d => d.Status == ShippingOrderStatus.Returned)
                .OrderByDescending(d => d.RtoApprovedDate ?? d.CreatedDate)
                .ToList();
            dgHangLuuKhoHoan.ItemsSource = danhSachLuuKhoHoan;

            // ================= 6. NHÁNH 6: BIÊN BẢN BÀN GIAO TRẢ SHOP =================
            var danhSachBienBan = WarehouseContext.Instance.GetAllReturnBatches();
            dgBienBanTraShop.ItemsSource = danhSachBienBan;
        }

        /// <summary>
        /// Áp dụng bộ lọc từ khóa và số lần giao hỏng cho DataGrid Đơn thất bại
        /// </summary>
        private void ApDungBoLocDonThatBai()
        {
            if (dgDonThatBai == null) return;

            string tuKhoa = txtTimKiemDonThatBai?.Text?.Trim().ToLower() ?? string.Empty;
            int mucLocSoLan = cbLocSoLanThatBai?.SelectedIndex ?? 0;

            var ketQua = _danhSachDonThatBaiGoc.AsEnumerable();

            if (!string.IsNullOrEmpty(tuKhoa))
            {
                ketQua = ketQua.Where(d =>
                    (d.OrderCode != null && d.OrderCode.ToLower().Contains(tuKhoa)) ||
                    (d.ReceiverName != null && d.ReceiverName.ToLower().Contains(tuKhoa)) ||
                    (d.ReceiverPhone != null && d.ReceiverPhone.Contains(tuKhoa)) ||
                    (d.FailureReason != null && d.FailureReason.ToLower().Contains(tuKhoa)) ||
                    (d.AssignedShipperName != null && d.AssignedShipperName.ToLower().Contains(tuKhoa)));
            }

            if (mucLocSoLan == 1) // Hỏng lần 1
            {
                ketQua = ketQua.Where(d => d.FailedDeliveryCount == 1);
            }
            else if (mucLocSoLan == 2) // Hỏng lần 2
            {
                ketQua = ketQua.Where(d => d.FailedDeliveryCount == 2);
            }
            else if (mucLocSoLan == 3) // Hỏng lần 3
            {
                ketQua = ketQua.Where(d => d.FailedDeliveryCount >= 3);
            }

            dgDonThatBai.ItemsSource = ketQua.ToList();
        }

        #region Xử Lý Điều Hướng 6 Nhánh (Tabs)
        /// <summary>
        /// SỰ KIỆN: Người dùng click chọn RadioButton tab điều hướng phụ
        /// - Nhiệm vụ: Ẩn/Hiện tương ứng các Panel nghiệp vụ theo 6 nhánh
        /// </summary>
        private void TabDieuHuong_Checked(object sender, RoutedEventArgs e)
        {
            if (panelDonThatBai == null || panelPhanLoaiLyDo == null || panelLenLichPhatLai == null ||
                panelDuyetChuyenHoan == null || panelKhoLuuHangHoan == null || panelBienBanTraShop == null)
            {
                return;
            }

            // Ẩn tất cả panel
            panelDonThatBai.Visibility = Visibility.Collapsed;
            panelPhanLoaiLyDo.Visibility = Visibility.Collapsed;
            panelLenLichPhatLai.Visibility = Visibility.Collapsed;
            panelDuyetChuyenHoan.Visibility = Visibility.Collapsed;
            panelKhoLuuHangHoan.Visibility = Visibility.Collapsed;
            panelBienBanTraShop.Visibility = Visibility.Collapsed;

            // Bật panel tương ứng
            if (tabDonThatBai.IsChecked == true)
            {
                panelDonThatBai.Visibility = Visibility.Visible;
            }
            else if (tabPhanLoaiLyDo.IsChecked == true)
            {
                panelPhanLoaiLyDo.Visibility = Visibility.Visible;
            }
            else if (tabLenLichPhatLai.IsChecked == true)
            {
                panelLenLichPhatLai.Visibility = Visibility.Visible;
            }
            else if (tabDuyetChuyenHoan.IsChecked == true)
            {
                panelDuyetChuyenHoan.Visibility = Visibility.Visible;
            }
            else if (tabKhoLuuHangHoan.IsChecked == true)
            {
                panelKhoLuuHangHoan.Visibility = Visibility.Visible;
            }
            else if (tabBienBanTraShop.IsChecked == true)
            {
                panelBienBanTraShop.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region Các Thao Tác Header & Tìm Kiếm
        private void BtnSinhDuLieuMauRto_Click(object sender, RoutedEventArgs e)
        {
            WarehouseContext.Instance.SeedSampleRtoData();
            NapDuLieu();
            MessageBox.Show(
                "ĐÃ SINH THÀNH CÔNG DỮ LIỆU KIỂM THỬ CHO CẢ 6 NHÁNH HÀNG HOÀN (RTO)!\n\n" +
                "• Bổ sung các đơn giao hỏng lần 1, lần 2, lần 3 do khách thuê bao / chuyển trọ.\n" +
                "• Bổ sung đơn đã duyệt chuyển hoàn và cấp mã vị trí KHO-RTO-01.\n" +
                "• Khởi tạo sẵn biên bản bàn giao trả hàng hoàn cho Shop.",
                "Sinh Dữ Liệu Kiểm Thử Thành Công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnLamMoiDuLieu_Click(object sender, RoutedEventArgs e)
        {
            NapDuLieu();
        }

        private void TxtTimKiemDonThatBai_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApDungBoLocDonThatBai();
        }

        private void CbLocSoLanThatBai_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApDungBoLocDonThatBai();
        }

        private void BtnChuyenSangLenLich_Click(object sender, RoutedEventArgs e)
        {
            tabLenLichPhatLai.IsChecked = true;
        }

        private void BtnChuyenSangDieuPhoiGiaoHang_Click(object sender, RoutedEventArgs e)
        {
            var cuaSoChinh = Window.GetWindow(this) as MainWindow;
            cuaSoChinh?.ChuyenSangTrangDieuPhoiGiaoHang();
        }
        #endregion

        #region Modal 1: Lên Lịch Phát Lại Lần 2 / 3
        private void BtnMoModalPhatLai_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is ShippingOrder don)
            {
                _donDangThaoTac = don;
                txtThongTinDonPhatLai.Text = $"Mã đơn: {don.OrderCode} • Người nhận: {don.ReceiverName} ({don.ReceiverPhone}) - Đã hỏng {don.FailedDeliveryCount} lần";
                dpNgayPhatLaiMoi.SelectedDate = DateTime.Today.AddDays(1);
                txtGhiChuPhatLai.Text = $"Khách hẹn phát lại: {don.FailureReason}";
                modalPhatLai.Visibility = Visibility.Visible;
            }
        }

        private void BtnDongModalPhatLai_Click(object sender, RoutedEventArgs e)
        {
            modalPhatLai.Visibility = Visibility.Collapsed;
        }

        private void BtnXacNhanPhatLai_Click(object sender, RoutedEventArgs e)
        {
            if (_donDangThaoTac == null) return;

            DateTime ngayHen = dpNgayPhatLaiMoi.SelectedDate ?? DateTime.Today.AddDays(1);
            string ghiChu = txtGhiChuPhatLai.Text.Trim();

            WarehouseContext.Instance.RescheduleOrder(_donDangThaoTac.Id, ngayHen, ghiChu);

            modalPhatLai.Visibility = Visibility.Collapsed;
            NapDuLieu();

            MessageBox.Show(
                $"ĐÃ LÊN LỊCH PHÁT LẠI THÀNH CÔNG!\n\n" +
                $"• Mã vận đơn: {_donDangThaoTac.OrderCode}\n" +
                $"• Ngày hẹn phát mới: {ngayHen:dd/MM/yyyy}\n" +
                $"• Ghi chú điều phối: {ghiChu}\n\n" +
                $"Đơn hàng đã được đưa lại vào hàng đợi Điều phối giao hàng (Mục 5).",
                "Xác Nhận Hẹn Lại Thành Công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        #endregion

        #region Modal 2: Duyệt Chuyển Hoàn Về Shop (RTO)
        private void BtnMoModalDuyetHoan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is ShippingOrder don)
            {
                _donDangThaoTac = don;
                txtThongTinDonDuyetHoan.Text = $"Mã đơn: {don.OrderCode} • Shop gửi: {don.SenderName} (Thất bại {don.FailedDeliveryCount} lần)";
                cbViTriKeHoan.SelectedIndex = 0;
                txtCuocPhiHoan.Text = "15000";
                txtLyDoDuyetHoan.Text = $"Giao hỏng {don.FailedDeliveryCount} lần: {don.FailureReason}. Duyệt chuyển hoàn về Shop.";
                modalDuyetHoan.Visibility = Visibility.Visible;
            }
        }

        private void BtnDongModalDuyetHoan_Click(object sender, RoutedEventArgs e)
        {
            modalDuyetHoan.Visibility = Visibility.Collapsed;
        }

        private void BtnXacNhanDuyetHoan_Click(object sender, RoutedEventArgs e)
        {
            if (_donDangThaoTac == null) return;

            string viTriKe = (cbViTriKeHoan.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "KHO-RTO-01";
            if (viTriKe.Contains('('))
            {
                viTriKe = viTriKe.Split('(')[0].Trim();
            }

            if (!decimal.TryParse(txtCuocPhiHoan.Text.Trim(), out decimal cuocHoan))
            {
                cuocHoan = 15000;
            }

            string lyDo = txtLyDoDuyetHoan.Text.Trim();

            WarehouseContext.Instance.ApproveRto(_donDangThaoTac.Id, viTriKe, cuocHoan, lyDo);

            modalDuyetHoan.Visibility = Visibility.Collapsed;
            NapDuLieu();

            MessageBox.Show(
                $"ĐÃ DUYỆT CHUYỂN HOÀN (RTO) THÀNH CÔNG!\n\n" +
                $"• Mã đơn gốc: {_donDangThaoTac.OrderCode}\n" +
                $"• Mã vận đơn RTO: {_donDangThaoTac.RtoTrackingCode}\n" +
                $"• Vị trí lưu kho RTO: {viTriKe}\n" +
                $"• Cước phí hoàn thu của Shop: {cuocHoan:N0} đ\n\n" +
                $"Kiện hàng đã được chuyển sang Khu vực kho lưu hàng hoàn (Nhánh 5) để chờ trả Shop.",
                "Duyệt Chuyển Hoàn Hoàn Tất",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        #endregion

        #region Modal 3: Lập & In Biên Bản Bàn Giao Trả Shop
        private void BtnMoModalLapBienBanTraShop_Click(object sender, RoutedEventArgs e)
        {
            var tatCaDon = WarehouseContext.Instance.GetAllShippingOrders();
            var cacDonChoTra = tatCaDon
                .Where(d => d.Status == ShippingOrderStatus.Returned && string.IsNullOrEmpty(d.ReturnHandoverBatchCode))
                .ToList();

            if (!cacDonChoTra.Any())
            {
                // Nếu tất cả đơn returned đã có biên bản, lấy các đơn returned mới nhất để minh họa
                cacDonChoTra = tatCaDon.Where(d => d.Status == ShippingOrderStatus.Returned).Take(5).ToList();
            }

            if (!cacDonChoTra.Any())
            {
                MessageBox.Show(
                    "Hiện tại chưa có bưu kiện nào được duyệt chuyển hoàn trong kho!\n\n" +
                    "Vui lòng thực hiện 'Duyệt chuyển hoàn (RTO)' tại Nhánh 1 hoặc Nhánh 4 trước khi lập biên bản.",
                    "Chưa Có Hàng Chuyển Hoàn",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Gom theo Shop đầu tiên
            var donDau = cacDonChoTra.First();
            string tenShop = donDau.SenderName;
            string sdtShop = string.IsNullOrEmpty(donDau.SenderPhone) ? "0919 223 344" : donDau.SenderPhone;
            string diaChiShop = string.IsNullOrEmpty(donDau.SenderAddress) ? "Số 120 Đường Cầu Giấy, Hà Nội" : donDau.SenderAddress;

            var cacDonCungShop = cacDonChoTra.Where(d => d.SenderName == tenShop).ToList();
            var danhSachId = cacDonCungShop.Select(d => d.Id).ToList();

            var nguoiLap = UserSession.Current.CurrentUser?.FullName ?? "Thủ Kho Hàng Hoàn";

            var bienBanMoi = WarehouseContext.Instance.CreateReturnHandoverBatch(
                tenShop,
                sdtShop,
                diaChiShop,
                danhSachId,
                nguoiLap,
                $"Bàn giao hàng hoàn đợt {DateTime.Now:dd/MM/yyyy}");

            NapDuLieu();

            // Hiển thị trực tiếp lên Modal in biên bản
            HienThiModalInBienBan(bienBanMoi, cacDonCungShop);
        }

        private void BtnInBienBanTraShop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is ReturnHandoverBatch bienBan)
            {
                var tatCaDon = WarehouseContext.Instance.GetAllShippingOrders();
                var cacDonTrongBienBan = tatCaDon
                    .Where(d => d.ReturnHandoverBatchCode == bienBan.BatchCode || bienBan.OrderIds.Contains(d.Id))
                    .ToList();

                HienThiModalInBienBan(bienBan, cacDonTrongBienBan);
            }
        }

        private void HienThiModalInBienBan(ReturnHandoverBatch bienBan, List<ShippingOrder> cacDon)
        {
            txtMaBienBanIn.Text = $"MÃ BIÊN BẢN: {bienBan.BatchCode}";
            txtNgayLapBienBanIn.Text = $"Ngày lập: {bienBan.CreatedTime:dd/MM/yyyy HH:mm}";
            txtTenShopIn.Text = $"• Shop đối tác: {bienBan.SenderName}";
            txtSdtShopIn.Text = $"• Số điện thoại: {bienBan.SenderPhone}";
            txtDiaChiShopIn.Text = $"• Địa chỉ kho Shop: {bienBan.SenderAddress}";
            txtNguoiLapBienBanIn.Text = $"• Đại diện kho bàn giao: {bienBan.OperatorName}";

            dgChiTietBienBanIn.ItemsSource = cacDon;

            decimal tongCuoc = cacDon.Sum(d => d.ReturnShippingFee);
            if (tongCuoc == 0) tongCuoc = bienBan.TotalReturnFee;
            txtTongKetBienBanIn.Text = $"TỔNG SỐ KIỆN HOÀN: {cacDon.Count} Kiện | TỔNG CƯỚC HOÀN SHOP PHẢI TRẢ: {tongCuoc:N0} đ";

            modalInBienBan.Visibility = Visibility.Visible;
        }

        private void BtnDongModalInBienBan_Click(object sender, RoutedEventArgs e)
        {
            modalInBienBan.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// THỰC HIỆN IN BIÊN BẢN BÀN GIAO HOẶC XUẤT PDF
        /// Sử dụng hộp thoại in PrintDialog tiêu chuẩn của WPF
        /// </summary>
        private void BtnThucHienInBienBan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hopThoaiIn = new PrintDialog();
                if (hopThoaiIn.ShowDialog() == true)
                {
                    hopThoaiIn.PrintVisual(vungInBienBanTraShop, "BienBanBanGiaoHangHoanTraShop");
                    MessageBox.Show(
                        "LỆNH IN BIÊN BẢN BÀN GIAO HÀNG HOÀN ĐÃ HOÀN TẤT THÀNH CÔNG!\n\n" +
                        "Biên bản đã được chuyển đến máy in (hoặc xuất file PDF tương ứng).",
                        "In Biên Bản Thành Công",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Không thể thực hiện in biên bản: {ex.Message}",
                    "Lỗi In Ấn",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        #endregion
    }
}
