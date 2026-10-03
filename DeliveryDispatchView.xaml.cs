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
    /// PHÂN HỆ 5: ĐIỀU PHỐI GIAO HÀNG (DELIVERY DISPATCH MANAGEMENT)
    /// - Nhiệm vụ: Quản lý toàn diện quy trình điều phối bưu kiện cho Shipper với 6 nhánh điều hướng:
    ///   1. Đơn chờ phân công (Lọc đơn, tìm kiếm, KPI hàng tồn)
    ///   2. Điều phối theo ngày (Chọn ngày giao, dự báo sản lượng theo ngày)
    ///   3. Phân công Shipper (Quy trình chuẩn 7 bước: Chọn ngày -> Chọn Shipper -> Lọc đơn -> Ưu tiên Express/SLA -> Kiểm tra năng lực -> Xác nhận -> Tạo phiếu PDF)
    ///   4. Danh sách giao dự kiến (Tổng hợp theo từng tài xế)
    ///   5. Phân công đã xác nhận (Theo dõi đơn đang trên đường giao)
    ///   6. Lịch sử điều phối (Nhật ký các chuyến điều động và in lại phiếu)
    /// - Đối tượng sử dụng: Điều phối viên kho, Quản lý logistics.
    /// - Tương tác dữ liệu: WarehouseContext.cs, ShippingOrder.cs, Shipper.cs, DispatchRecord.cs.
    /// </summary>
    public partial class DeliveryDispatchView : UserControl
    {
        // =========================================================================
        // DANH SÁCH DỮ LIỆU & BIẾN TRẠNG THÁI NỘI BỘ
        // =========================================================================
        private List<DispatchCandidateItem> _danhSachDonChoGiao = new();
        private List<Shipper> _danhSachShipperKhaDung = new();
        private Shipper? _shipperDangChon;
        private DispatchRecord? _banGhiInPhieuHienTai;
        private List<ShippingOrder> _danhSachDonChiTietPhieuIn = new();

        public static readonly IValueConverter ExpressToBgConverter = new FuncValueConverter(val => (bool)val ? "#FDF4FF" : "#F8FAFC");
        public static readonly IValueConverter ExpressToFgConverter = new FuncValueConverter(val => (bool)val ? "#7C3AED" : "#64748B");

        public DeliveryDispatchView()
        {
            InitializeComponent();
            dpNgayPhanCong.SelectedDate = DateTime.Today;
            dpChonNgayGiao.SelectedDate = DateTime.Today;
            NapDuLieu();
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ dữ liệu đơn chờ, đội ngũ Shipper và lịch sử điều phối
        /// </summary>
        public void NapDuLieu()
        {
            var khoDuLieu = WarehouseContext.Instance;
            var tatCaDon = khoDuLieu.GetAllShippingOrders();
            _danhSachShipperKhaDung = khoDuLieu.GetAllShippers().Where(s => !s.IsLocked).ToList();

            // 1. NẠP DỮ LIỆU ĐƠN CHỜ PHÂN CÔNG (NewReceived & PendingProcessing)
            var donChuaGan = tatCaDon
                .Where(o => o.Status == ShippingOrderStatus.NewReceived || o.Status == ShippingOrderStatus.PendingProcessing)
                .OrderByDescending(o => o.IsExpress)
                .ThenBy(o => o.EstimatedDeliveryDate)
                .ToList();

            _danhSachDonChoGiao = donChuaGan.Select(d => new DispatchCandidateItem { Order = d }).ToList();

            // 2. CẬP NHẬT 4 THẺ KPI NHÁNH 1
            int tongCho = _danhSachDonChoGiao.Count;
            int hoaToc = _danhSachDonChoGiao.Count(d => d.IsExpress);
            int canHan = _danhSachDonChoGiao.Count(d => (d.EstimatedDeliveryDate - DateTime.Now).TotalHours <= 2);
            decimal tongCod = _danhSachDonChoGiao.Sum(d => d.CodAmount);

            txtTongDonChoPhanCong.Text = $"{tongCho} đơn";
            txtDonHoaTocChoGiao.Text = $"{hoaToc} đơn";
            txtDonCanHanSla.Text = $"{canHan} đơn";
            txtTongTienCodChoGiao.Text = $"{tongCod:N0} đ";

            ApDungBoLocDonCho();

            // 3. NẠP DỮ LIỆU COMBOBOX CHỌN SHIPPER (NHÁNH 3)
            cbChonShipper.Items.Clear();
            foreach (var s in _danhSachShipperKhaDung)
            {
                int donDangGiao = s.ActiveDeliveringCount;
                int conLai = Math.Max(0, s.MaxOrdersPerDay - donDangGiao);
                cbChonShipper.Items.Add(new ComboBoxItem
                {
                    Content = $"🛵 {s.FullName} ({s.Phone}) - Tuyến: {s.DeliveryArea} [Còn nhận: {conLai} đơn]",
                    Tag = s
                });
            }

            if (cbChonShipper.Items.Count > 0)
            {
                cbChonShipper.SelectedIndex = 0;
            }

            // 4. NẠP DỮ LIỆU NHÁNH 2 (ĐIỀU PHỐI THEO NGÀY)
            CapNhatDuLieuTheoNgay(dpChonNgayGiao.SelectedDate ?? DateTime.Today);

            // 5. NẠP DỮ LIỆU NHÁNH 4 (DANH SÁCH GIAO DỰ KIẾN THEO SHIPPER)
            CapNhatKeHoachGiaoDuKien();

            // 6. NẠP DỮ LIỆU NHÁNH 5 (PHÂN CÔNG ĐÃ XÁC NHẬN - ĐƠN ĐANG GIAO)
            var danhSachDonDangGiao = tatCaDon.Where(o => o.Status == ShippingOrderStatus.Delivering).ToList();
            dgDonDaXacNhan.ItemsSource = danhSachDonDangGiao;

            // 7. NẠP DỮ LIỆU NHÁNH 6 (LỊCH SỬ ĐIỀU PHỐI)
            dgLichSuDieuPhoi.ItemsSource = khoDuLieu.GetAllDispatchRecords();
        }

        public void LoadData() => NapDuLieu();

        private void BtnLamMoiDuLieu_Click(object sender, RoutedEventArgs e) => NapDuLieu();

        #region Xử lý chuyển đổi giữa 6 nhánh điều hướng phụ
        /// <summary>
        /// SỰ KIỆN: Chuyển đổi giữa 6 nhánh thanh điều hướng qua RadioButton
        /// </summary>
        private void TabDieuHuong_Checked(object sender, RoutedEventArgs e)
        {
            if (panelDonChoPhanCong == null) return;

            // Ẩn tất cả các panel
            panelDonChoPhanCong.Visibility = Visibility.Collapsed;
            panelDieuPhoiTheoNgay.Visibility = Visibility.Collapsed;
            panelPhanCongShipper.Visibility = Visibility.Collapsed;
            panelDanhSachGiaoDuKien.Visibility = Visibility.Collapsed;
            panelPhanCongDaXacNhan.Visibility = Visibility.Collapsed;
            panelLichSuDieuPhoi.Visibility = Visibility.Collapsed;

            // Hiển thị panel được chọn
            if (tabDonChoPhanCong.IsChecked == true) panelDonChoPhanCong.Visibility = Visibility.Visible;
            else if (tabDieuPhoiTheoNgay.IsChecked == true) panelDieuPhoiTheoNgay.Visibility = Visibility.Visible;
            else if (tabPhanCongShipper.IsChecked == true) panelPhanCongShipper.Visibility = Visibility.Visible;
            else if (tabDanhSachGiaoDuKien.IsChecked == true) panelDanhSachGiaoDuKien.Visibility = Visibility.Visible;
            else if (tabPhanCongDaXacNhan.IsChecked == true) panelPhanCongDaXacNhan.Visibility = Visibility.Visible;
            else if (tabLichSuDieuPhoi.IsChecked == true) panelLichSuDieuPhoi.Visibility = Visibility.Visible;
        }
        #endregion

        #region Nhánh 1: Đơn Chờ Phân Công
        private void TxtTimKiemDonCho_TextChanged(object sender, TextChangedEventArgs e) => ApDungBoLocDonCho();
        private void CbLocDonCho_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApDungBoLocDonCho();

        private void ApDungBoLocDonCho()
        {
            if (dgDonChoPhanCong == null) return;

            string tuKhoa = txtTimKiemDonCho?.Text.Trim().ToLower() ?? "";
            string khuVuc = (cbLocKhuVucDonCho?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string loaiDichVu = (cbLocDichVuDonCho?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            var ketQua = _danhSachDonChoGiao.Where(item =>
            {
                bool khopTuKhoa = string.IsNullOrEmpty(tuKhoa) ||
                                  item.OrderCode.ToLower().Contains(tuKhoa) ||
                                  item.ReceiverName.ToLower().Contains(tuKhoa) ||
                                  item.ReceiverPhone.Contains(tuKhoa) ||
                                  item.ReceiverAddress.ToLower().Contains(tuKhoa);

                bool khopKhuVuc = string.IsNullOrEmpty(khuVuc) || khuVuc.Contains("Tất Cả") ||
                                  item.DestinationArea.Contains(khuVuc);

                bool khopDichVu = true;
                if (loaiDichVu.Contains("Hỏa Tốc")) khopDichVu = item.IsExpress;
                else if (loaiDichVu.Contains("Tiêu Chuẩn")) khopDichVu = !item.IsExpress;

                return khopTuKhoa && khopKhuVuc && khopDichVu;
            }).ToList();

            dgDonChoPhanCong.ItemsSource = ketQua;
        }

        private void BtnChuyenSangPhanCong_Click(object sender, RoutedEventArgs e)
        {
            tabPhanCongShipper.IsChecked = true;
        }
        #endregion

        #region Nhánh 2: Điều Phối Theo Ngày
        private void DpChonNgayGiao_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpChonNgayGiao.SelectedDate.HasValue)
            {
                CapNhatDuLieuTheoNgay(dpChonNgayGiao.SelectedDate.Value);
            }
        }

        private void BtnChonNgayNhanh_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut)
            {
                if (nut == btnNgayHomNay) dpChonNgayGiao.SelectedDate = DateTime.Today;
                else if (nut == btnNgayMai) dpChonNgayGiao.SelectedDate = DateTime.Today.AddDays(1);
                else if (nut == btnTatCaCacNgay) CapNhatDuLieuTheoNgay(null);
            }
        }

        private void CapNhatDuLieuTheoNgay(DateTime? ngayChon)
        {
            if (dgDonTheoNgay == null) return;

            var khoDuLieu = WarehouseContext.Instance;
            var tatCaDon = khoDuLieu.GetAllShippingOrders();

            List<ShippingOrder> danhSach;
            if (ngayChon.HasValue)
            {
                danhSach = tatCaDon.Where(o => o.EstimatedDeliveryDate.Date == ngayChon.Value.Date).ToList();
            }
            else
            {
                danhSach = tatCaDon.ToList();
            }

            txtTongDonHenGiaoNgay.Text = $"{danhSach.Count} đơn";
            txtDonHoaTocTheoNgay.Text = $"{danhSach.Count(d => d.IsExpress)} đơn";
            txtDonTieuChuanTheoNgay.Text = $"{danhSach.Count(d => !d.IsExpress)} đơn";

            dgDonTheoNgay.ItemsSource = danhSach;
        }

        private void BtnDieuPhoiNgayNay_Click(object sender, RoutedEventArgs e)
        {
            dpNgayPhanCong.SelectedDate = dpChonNgayGiao.SelectedDate ?? DateTime.Today;
            tabPhanCongShipper.IsChecked = true;
        }
        #endregion

        #region Nhánh 3: Quy Trình Phân Công Shipper 7 Bước
        private void CbChonShipper_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbChonShipper.SelectedItem is ComboBoxItem muc && muc.Tag is Shipper s)
            {
                _shipperDangChon = s;
                int donDangGiao = s.ActiveDeliveringCount;
                int conLai = Math.Max(0, s.MaxOrdersPerDay - donDangGiao);

                txtThongTinShipperChon.Text = $"🛵 Tài xế: {s.FullName} ({s.Phone})";
                txtTuyenVaXeShipper.Text = $"Phương tiện: {s.VehiclePlate} ({s.VehicleType}) • Tuyến chính: {s.DeliveryArea}";
                txtNangLucConLai.Text = $"Đã nhận: {donDangGiao}/{s.MaxOrdersPerDay} đơn (Còn nhận: {conLai} đơn)";

                ApDungBoLocPhanCong();
            }
        }

        private void ChkChiLocKhuVucShipper_Click(object sender, RoutedEventArgs e)
        {
            ApDungBoLocPhanCong();
        }

        private void ApDungBoLocPhanCong()
        {
            if (dgDonPhanCongUngVien == null || _shipperDangChon == null) return;

            bool chiLocTuyen = chkChiLocKhuVucShipper.IsChecked == true;
            string tuyenShipper = _shipperDangChon.DeliveryArea.ToLower();

            var danhSachHienThi = _danhSachDonChoGiao.Where(d =>
            {
                if (!chiLocTuyen) return true;
                // Kiểm tra quận/huyện
                string khuVucDon = d.DestinationArea.ToLower();
                return tuyenShipper.Contains(khuVucDon) || khuVucDon.Contains(tuyenShipper);
            }).ToList();

            dgDonPhanCongUngVien.ItemsSource = null;
            dgDonPhanCongUngVien.ItemsSource = danhSachHienThi;

            CapNhatKiemTraNangLuc();
        }

        /// <summary>
        /// BƯỚC 4: Tự động sắp xếp ưu tiên Express ⚡ và đơn gần hạn giao
        /// </summary>
        private void BtnSapXepUuTien_Click(object sender, RoutedEventArgs e)
        {
            if (dgDonPhanCongUngVien.ItemsSource is List<DispatchCandidateItem> danhSachHienTai)
            {
                var danhSachUuTien = danhSachHienTai
                    .OrderByDescending(d => d.IsExpress)
                    .ThenBy(d => d.EstimatedDeliveryDate)
                    .ThenBy(d => d.CreatedDate)
                    .ToList();

                dgDonPhanCongUngVien.ItemsSource = null;
                dgDonPhanCongUngVien.ItemsSource = danhSachUuTien;

                MessageBox.Show("ĐÃ SẮP XẾP ƯU TIÊN THÀNH CÔNG!\n\n" +
                                "1. Toàn bộ đơn HỎA TỐC ⚡ (Express) đã được đẩy lên đầu bảng.\n" +
                                "2. Tiếp theo là các đơn sắp đến hạn cam kết giao SLA.\n" +
                                "3. Các đơn tiêu chuẩn xếp kế tiếp theo thời gian tiếp nhận.",
                                "Sắp Xếp Ưu Tiên", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ChkDonHang_Click(object sender, RoutedEventArgs e)
        {
            CapNhatKiemTraNangLuc();
        }

        private void BtnChonTatCaDuTai_Click(object sender, RoutedEventArgs e)
        {
            if (_shipperDangChon == null) return;

            int donDangGiao = _shipperDangChon.ActiveDeliveringCount;
            int conLai = Math.Max(0, _shipperDangChon.MaxOrdersPerDay - donDangGiao);

            if (dgDonPhanCongUngVien.ItemsSource is List<DispatchCandidateItem> danhSach)
            {
                int dem = 0;
                foreach (var d in danhSach)
                {
                    if (dem < conLai)
                    {
                        d.IsSelected = true;
                        dem++;
                    }
                    else
                    {
                        d.IsSelected = false;
                    }
                }

                dgDonPhanCongUngVien.Items.Refresh();
                CapNhatKiemTraNangLuc();
            }
        }

        private void BtnBoChonTatCa_Click(object sender, RoutedEventArgs e)
        {
            if (dgDonPhanCongUngVien.ItemsSource is List<DispatchCandidateItem> danhSach)
            {
                foreach (var d in danhSach) d.IsSelected = false;
                dgDonPhanCongUngVien.Items.Refresh();
                CapNhatKiemTraNangLuc();
            }
        }

        /// <summary>
        /// BƯỚC 5: Kiểm tra khu vực và năng lực thời gian thực
        /// </summary>
        private void CapNhatKiemTraNangLuc()
        {
            if (dgDonPhanCongUngVien.ItemsSource is not List<DispatchCandidateItem> danhSach || _shipperDangChon == null) return;

            var donDuocChon = danhSach.Where(d => d.IsSelected).ToList();
            int soDonChon = donDuocChon.Count;
            double tongKg = Math.Round(donDuocChon.Sum(d => d.Weight), 1);
            int conLai = Math.Max(0, _shipperDangChon.MaxOrdersPerDay - _shipperDangChon.ActiveDeliveringCount);

            txtKiemTraNangLucThoiGianThuc.Text = $"Đã chọn: {soDonChon} đơn (Tổng khối lượng: {tongKg} kg)";

            if (soDonChon > conLai)
            {
                txtCanhBaoQuaTai.Text = $"⚠️ CẢNH BÁO QUÁ TẢI: Vượt quá {soDonChon - conLai} đơn so với hạn mức còn lại ({conLai} đơn)!";
            }
            else
            {
                txtCanhBaoQuaTai.Text = $"✓ Hợp lệ: Nằm trong định mức an toàn ({soDonChon}/{conLai} đơn)";
            }
        }

        /// <summary>
        /// BƯỚC 6 & BƯỚC 7: Xác nhận phân công và tạo phiếu giao hàng PDF
        /// </summary>
        private void BtnXacNhanPhanCongVaIn_Click(object sender, RoutedEventArgs e)
        {
            if (_shipperDangChon == null)
            {
                MessageBox.Show("Vui lòng chọn Shipper tiếp nhận đơn hàng!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgDonPhanCongUngVien.ItemsSource is not List<DispatchCandidateItem> danhSach) return;

            var donDuocChon = danhSach.Where(d => d.IsSelected).ToList();
            if (donDuocChon.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 đơn hàng để bàn giao cho Shipper!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int conLai = Math.Max(0, _shipperDangChon.MaxOrdersPerDay - _shipperDangChon.ActiveDeliveringCount);
            if (donDuocChon.Count > conLai)
            {
                var xacNhanVuotTai = MessageBox.Show(
                    $"Số đơn đã chọn ({donDuocChon.Count} đơn) vượt quá hạn mức còn lại ({conLai} đơn) của tài xế {_shipperDangChon.FullName}.\n\n" +
                    "Bạn có chắc chắn muốn phân công vượt định mức ngày?",
                    "Cảnh Báo Quá Tải Shipper", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (xacNhanVuotTai != MessageBoxResult.Yes) return;
            }

            // TIẾN HÀNH TẠO PHIÊN ĐIỀU PHỐI VÀ CẬP NHẬT ĐƠN HÀNG
            DateTime ngayGiao = dpNgayPhanCong.SelectedDate ?? DateTime.Today;
            var danhSachMaDon = donDuocChon.Select(d => d.OrderId).ToList();

            var banGhiMoi = WarehouseContext.Instance.CreateDispatchRecord(
                _shipperDangChon.Id,
                _shipperDangChon.FullName,
                _shipperDangChon.Phone,
                _shipperDangChon.VehiclePlate,
                _shipperDangChon.DeliveryArea,
                ngayGiao,
                danhSachMaDon,
                UserSession.Current.CurrentUser?.FullName ?? "Điều Phối Viên Kho",
                "Lập lệnh giao hàng theo tuyến điều phối");

            MessageBox.Show(
                $"ĐÃ PHÂN CÔNG ĐIỀU PHỐI THÀNH CÔNG!\n\n" +
                $"• Mã chuyến: {banGhiMoi.DispatchCode}\n" +
                $"• Tài xế nhận: {banGhiMoi.ShipperName} ({banGhiMoi.ShipperPhone})\n" +
                $"• Tổng số đơn: {banGhiMoi.TotalOrders} đơn (Có {banGhiMoi.ExpressOrdersCount} Hỏa Tốc)\n" +
                $"• Tổng tiền COD cần thu: {banGhiMoi.TotalCodAmount:N0} đ\n\n" +
                $"Hệ thống sẽ mở Phiếu Giao Hàng để bạn in ấn hoặc xuất PDF.",
                "Phân Công Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);

            // Nạp lại toàn bộ dữ liệu hệ thống
            NapDuLieu();

            // Mở Modal xem trước và in phiếu PDF
            MoModalInPhieu(banGhiMoi);
        }
        #endregion

        #region Nhánh 4 & Nhánh 5: Danh Sách Dự Kiến & Đã Xác Nhận
        private void CapNhatKeHoachGiaoDuKien()
        {
            var khoDuLieu = WarehouseContext.Instance;
            var danhSachBanGhi = khoDuLieu.GetAllDispatchRecords().Where(r => r.DispatchDate.Date == DateTime.Today).ToList();
            dgKeHoachGiaoDuKien.ItemsSource = danhSachBanGhi;
        }

        private void BtnXemVaInPhieuChuyen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is DispatchRecord banGhi)
            {
                MoModalInPhieu(banGhi);
            }
        }

        private void BtnInDanhSachDangGiao_Click(object sender, RoutedEventArgs e)
        {
            var hopThoaiIn = new PrintDialog();
            if (hopThoaiIn.ShowDialog() == true)
            {
                hopThoaiIn.PrintVisual(dgDonDaXacNhan, "DanhSachDonDangGiao");
                MessageBox.Show("Đã gửi danh sách đơn đang giao tới máy in!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region Nhánh 6: Lịch Sử Điều Phối
        private void BtnXemLaiPhieuLichSu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is DispatchRecord banGhi)
            {
                MoModalInPhieu(banGhi);
            }
        }
        #endregion

        #region Chức Năng In Phiếu Giao Hàng & Xuất PDF
        /// <summary>
        /// MỞ MODAL XEM TRƯỚC VÀ IN PHIẾU BÀN GIAO GIAO HÀNG
        /// </summary>
        private void MoModalInPhieu(DispatchRecord banGhi)
        {
            _banGhiInPhieuHienTai = banGhi;

            txtMaChuyenIn.Text = $"MÃ CHUYẾN: {banGhi.DispatchCode}";
            txtNgayLapPhieuIn.Text = $"Ngày lập: {banGhi.CreatedTime:dd/MM/yyyy HH:mm}";
            txtTenShipperIn.Text = $"• Họ và tên Shipper: {banGhi.ShipperName}";
            txtSdtShipperIn.Text = $"• Số điện thoại: {banGhi.ShipperPhone}";
            txtBienSoShipperIn.Text = $"• Phương tiện / Biển số: {banGhi.VehiclePlate}";
            txtTuyenGiaoIn.Text = $"• Tuyến đường phụ trách: {banGhi.DeliveryArea}";
            txtNguoiLapLenhIn.Text = $"• Người lập lệnh: {banGhi.DispatcherName}";

            txtTongKetPhieuIn.Text = $"TỔNG CỘNG: {banGhi.TotalOrders} Bưu kiện ({banGhi.ExpressOrdersCount} Express) | TỔNG COD PHẢI THU: {banGhi.TotalCodAmount:N0} đ";

            // Lấy danh sách các đơn hàng chi tiết
            _danhSachDonChiTietPhieuIn = WarehouseContext.Instance.GetOrdersByIds(banGhi.OrderIds).ToList();
            dgChiTietPhieuIn.ItemsSource = _danhSachDonChiTietPhieuIn;

            modalInPhieuGiaoHang.Visibility = Visibility.Visible;
        }

        private void BtnDongModalPhieu_Click(object sender, RoutedEventArgs e)
        {
            modalInPhieuGiaoHang.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// SỰ KIỆN: Bấm nút "In Phiếu / Xuất File PDF"
        /// - Nhiệm vụ: Kích hoạt hộp thoại Windows PrintDialog. Người dùng có thể chọn máy in vật lý
        ///             hoặc chọn 'Microsoft Print to PDF' để lưu thành file PDF hoàn chỉnh.
        /// </summary>
        private void BtnThucHienInPhieu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hopThoaiIn = new PrintDialog();
                if (hopThoaiIn.ShowDialog() == true)
                {
                    hopThoaiIn.PrintVisual(vungInPhieuGiaoHang, $"PhieuGiaoHang_{_banGhiInPhieuHienTai?.DispatchCode}");
                    MessageBox.Show("Đã gửi lệnh in phiếu giao hàng thành công!\n(Nếu bạn chọn Microsoft Print to PDF, tệp PDF đã được lưu lại)",
                                    "In Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thực hiện in: {ex.Message}", "Lỗi In Ấn", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        /// <summary>
        /// Lớp phụ trợ chuyển đổi giá trị màu hiển thị (Value Converter)
        /// </summary>
        private class FuncValueConverter : IValueConverter
        {
            private readonly Func<object, object> _chuyenDoi;
            public FuncValueConverter(Func<object, object> chuyenDoi) => _chuyenDoi = chuyenDoi;
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => _chuyenDoi(value);
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw exoticException();
            private static NotImplementedException exoticException() => new();
        }
    }
}
