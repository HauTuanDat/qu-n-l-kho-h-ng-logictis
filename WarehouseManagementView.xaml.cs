using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// PHÂN HỆ QUẢN LÝ KHO BÃI TOÀN DIỆN (WAREHOUSE MANAGEMENT HUB - WMS)
    /// - Quản lý 7 phân hệ cốt lõi:
    ///   1. Tổng quan kho & Sức chứa thời gian thực
    ///   2. Tiếp nhận & Nhập kho (Xe tải Linehaul / Khách lẻ bưu cục)
    ///   3. Phân luồng bưu kiện tại Dock (Giao chặng cuối vs Trung chuyển)
    ///   4. Tồn kho & Phiếu kiểm kê cân bằng tồn (Stock Count & Adjustment)
    ///   5. Bản đồ ô kệ & Lệnh điều chuyển vị trí kệ (Put-away / Relocation)
    ///   6. Nghiệp vụ xuất kho & Bàn giao vận tải
    ///   7. Sổ nhật ký biến động kho & Thẻ kho (Audit Trail Log)
    /// </summary>
    public partial class WarehouseManagementView : UserControl
    {
        private string _currentZoneFilter = "ALL";
        private List<ShippingOrder> _danhSachDonChoPhanLoai = new();
        private List<Inventory> _danhSachTonKhoGoc = new();
        private List<WarehouseMovement> _danhSachBienDongGoc = new();

        public WarehouseManagementView()
        {
            InitializeComponent();
            NapDuLieuKho();
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ dữ liệu cho 7 phân nhánh quản lý kho
        /// </summary>
        public void NapDuLieuKho()
        {
            var khoDuLieu = WarehouseContext.Instance;
            var danhSachViTri = khoDuLieu.GetAllLocations();
            _danhSachTonKhoGoc = khoDuLieu.GetAllInventories().ToList();
            var danhSachPhieuNhap = khoDuLieu.GetAllImportOrders();
            _danhSachBienDongGoc = khoDuLieu.GetAllWarehouseMovements().ToList();
            var tatCaDonHang = khoDuLieu.GetAllShippingOrders();

            // =========================================================================
            // 1. DỮ LIỆU NHÁNH 1: TỔNG QUAN KHO & SỨC CHỨA ĐỘNG
            // =========================================================================
            double tongTaiTrongToiDa = danhSachViTri.Sum(v => v.MaxWeightCapacity);
            double tongTaiTrongHienTai = danhSachViTri.Sum(v => v.CurrentWeight);
            double tyLeLapDay = tongTaiTrongToiDa > 0 ? (tongTaiTrongHienTai / tongTaiTrongToiDa) * 100.0 : 0;
            int tongSoSku = _danhSachTonKhoGoc.Count;
            int tongSoLuongKien = _danhSachTonKhoGoc.Sum(t => t.Quantity);
            double tongKhoiLuongTiepNhanHomNay = danhSachPhieuNhap.Sum(p => p.TotalWeight);
            int soChuyenXuatHomNay = _danhSachBienDongGoc.Count(m => m.MovementType == WarehouseMovementType.OutboundLastMile || m.MovementType == WarehouseMovementType.OutboundTransit);

            txtTyLeLapDayKho.Text = $"{tyLeLapDay:N1}%";
            txtTrangThaiTongThe.Text = tyLeLapDay > 85 ? "⚠️ Tải trọng kho đang ở mức cao!" : "Đang ở mức tải trọng an toàn";
            txtTrangThaiTongThe.Foreground = tyLeLapDay > 85 ? new SolidColorBrush(Color.FromRgb(220, 38, 38)) : new SolidColorBrush(Color.FromRgb(5, 150, 105));

            txtTongMatHangLuuKho.Text = $"{tongSoSku} SKU ({tongSoLuongKien} kiện)";
            txtTiepNhanHomNay.Text = $"{danhSachPhieuNhap.Count} lô ({tongKhoiLuongTiepNhanHomNay:N0} kg)";
            txtXuatKhoHomNay.Text = $"{soChuyenXuatHomNay} chuyến đã xuất";

            // Tính toán động sức chứa theo 3 phân khu:
            // Phân khu A: Tiêu chuẩn
            var cacViTriKhuA = danhSachViTri.Where(v => v.Zone.Contains("Tiêu Chuẩn") || v.Aisle == "A").ToList();
            double taiKhuA = cacViTriKhuA.Sum(v => v.CurrentWeight);
            double maxKhuA = cacViTriKhuA.Sum(v => v.MaxWeightCapacity);
            double pctKhuA = maxKhuA > 0 ? Math.Round((taiKhuA / maxKhuA) * 100.0, 1) : 0;
            txtKhuAInfo.Text = $"{taiKhuA:N0} kg / {maxKhuA:N0} kg ({pctKhuA:N1}%)";
            pbKhuA.Value = pctKhuA;

            // Phân khu EXP: Hỏa tốc
            var cacViTriKhuEXP = danhSachViTri.Where(v => v.Zone.Contains("Hỏa Tốc") || v.Aisle == "EXP").ToList();
            double taiKhuEXP = cacViTriKhuEXP.Sum(v => v.CurrentWeight);
            double maxKhuEXP = cacViTriKhuEXP.Sum(v => v.MaxWeightCapacity);
            double pctKhuEXP = maxKhuEXP > 0 ? Math.Round((taiKhuEXP / maxKhuEXP) * 100.0, 1) : 0;
            txtKhuEXPInfo.Text = $"{taiKhuEXP:N0} kg / {maxKhuEXP:N0} kg ({pctKhuEXP:N1}%)";
            pbKhuEXP.Value = pctKhuEXP;

            // Phân khu Dock Inbound: Tiếp nhận
            var cacViTriDock = danhSachViTri.Where(v => v.Zone.Contains("Tiếp Nhận") || v.Aisle == "IN" || v.LocationCode.StartsWith("DOCK")).ToList();
            double taiKhuDock = cacViTriDock.Sum(v => v.CurrentWeight);
            double maxKhuDock = cacViTriDock.Sum(v => v.MaxWeightCapacity);
            double pctKhuDock = maxKhuDock > 0 ? Math.Round((taiKhuDock / maxKhuDock) * 100.0, 1) : 0;
            txtKhuDockInfo.Text = $"{taiKhuDock:N0} kg / {maxKhuDock:N0} kg ({pctKhuDock:N1}%)";
            pbKhuDock.Value = pctKhuDock;

            // Kiểm tra cảnh báo phân khu quá tải (> 80%)
            if (pctKhuA >= 80 || pctKhuEXP >= 80 || pctKhuDock >= 80)
            {
                brdCanhBaoSucChua.Visibility = Visibility.Visible;
                var dsCanhBao = new List<string>();
                if (pctKhuA >= 80) dsCanhBao.Add($"Khu A ({pctKhuA}%)");
                if (pctKhuEXP >= 80) dsCanhBao.Add($"Khu EXP ({pctKhuEXP}%)");
                if (pctKhuDock >= 80) dsCanhBao.Add($"Dock Inbound ({pctKhuDock}%)");
                txtCanhBaoSucChua.Text = $"Cảnh báo tải trọng cao tại: {string.Join(", ", dsCanhBao)}! Khuyến nghị di dời hoặc tạm dừng xếp hàng thêm.";
            }
            else
            {
                brdCanhBaoSucChua.Visibility = Visibility.Collapsed;
            }

            // =========================================================================
            // 2. DỮ LIỆU NHÁNH 2: TIẾP NHẬN & NHẬP KHO
            // =========================================================================
            ucImportManagement?.NapDuLieuNhapKho();

            // =========================================================================
            // 3. DỮ LIỆU NHÁNH 3: PHÂN LUỒNG BƯU KIỆN TẠI DOCK
            // =========================================================================
            _danhSachDonChoPhanLoai = tatCaDonHang
                .Where(d => d.Status == ShippingOrderStatus.PendingProcessing || d.Status == ShippingOrderStatus.NewReceived)
                .ToList();
            ApDungLocPhanLoai();

            // =========================================================================
            // 4. DỮ LIỆU NHÁNH 4: HÀNG ĐANG LƯU KHO & KIỂM KÊ
            // =========================================================================
            ApDungLocTonKho();
            NapDanhSachSanPhamKiemKe();

            // =========================================================================
            // 5. DỮ LIỆU NHÁNH 5: VỊ TRÍ LƯU TRỮ & Ô KỆ
            // =========================================================================
            ApDungLocZone();
            NapDanhSachViTriDieuChuyen();

            // =========================================================================
            // 6. DỮ LIỆU NHÁNH 6: NGHIỆP VỤ XUẤT KHO
            // =========================================================================
            ucExportManagement?.NapDuLieuXuatKho();

            // =========================================================================
            // 7. DỮ LIỆU NHÁNH 7: LỊCH SỬ NHẬP / XUẤT & AUDIT LOG
            // =========================================================================
            ApDungLocLichSu();
        }

        // Tương thích gọi hàm cũ
        public void LoadData() => NapDuLieuKho();

        /// <summary>
        /// SỰ KIỆN: Chuyển đổi giữa 7 nhánh chức năng kho bãi qua RadioButton
        /// </summary>
        private void TabNghiepVu_Checked(object sender, RoutedEventArgs e)
        {
            if (panelTongQuanKho == null) return;

            // Ẩn tất cả các panel
            panelTongQuanKho.Visibility = Visibility.Collapsed;
            panelTiepNhanHang.Visibility = Visibility.Collapsed;
            panelPhanLoaiHang.Visibility = Visibility.Collapsed;
            panelHangLuuKho.Visibility = Visibility.Collapsed;
            panelViTriLuuTru.Visibility = Visibility.Collapsed;
            panelXuatHang.Visibility = Visibility.Collapsed;
            panelLichSuNhapXuat.Visibility = Visibility.Collapsed;

            // Hiện panel được chọn
            if (tabTongQuanKho.IsChecked == true)
            {
                panelTongQuanKho.Visibility = Visibility.Visible;
            }
            else if (tabTiepNhanHang.IsChecked == true)
            {
                panelTiepNhanHang.Visibility = Visibility.Visible;
                ucImportManagement?.NapDuLieuNhapKho();
            }
            else if (tabPhanLoaiHang.IsChecked == true)
            {
                panelPhanLoaiHang.Visibility = Visibility.Visible;
                ApDungLocPhanLoai();
            }
            else if (tabHangLuuKho.IsChecked == true)
            {
                panelHangLuuKho.Visibility = Visibility.Visible;
                ApDungLocTonKho();
            }
            else if (tabViTriLuuTru.IsChecked == true)
            {
                panelViTriLuuTru.Visibility = Visibility.Visible;
                ApDungLocZone();
            }
            else if (tabXuatHang.IsChecked == true)
            {
                panelXuatHang.Visibility = Visibility.Visible;
                ucExportManagement?.NapDuLieuXuatKho();
            }
            else if (tabLichSuNhapXuat.IsChecked == true)
            {
                panelLichSuNhapXuat.Visibility = Visibility.Visible;
                ApDungLocLichSu();
            }
        }

        #region Phím Tắt & Điều Hướng Công Khai
        public void ChuyenSangTabTiepNhan()
        {
            if (tabTiepNhanHang != null) tabTiepNhanHang.IsChecked = true;
            ucImportManagement?.NapDuLieuNhapKho();
        }

        public void ChuyenSangTabPhanLoai()
        {
            if (tabPhanLoaiHang != null) tabPhanLoaiHang.IsChecked = true;
            ApDungLocPhanLoai();
        }

        public void ChuyenSangTabXuatKho()
        {
            if (tabXuatHang != null) tabXuatHang.IsChecked = true;
            ucExportManagement?.NapDuLieuXuatKho();
        }

        public void ChuyenSangTabTonKho()
        {
            if (tabHangLuuKho != null) tabHangLuuKho.IsChecked = true;
            ApDungLocTonKho();
        }

        public void ChuyenSangTabViTri()
        {
            if (tabViTriLuuTru != null) tabViTriLuuTru.IsChecked = true;
            ApDungLocZone();
        }

        public void ChuyenSangTabLichSu()
        {
            if (tabLichSuNhapXuat != null) tabLichSuNhapXuat.IsChecked = true;
            ApDungLocLichSu();
        }

        private void BtnChuyenSangTiepNhan_Click(object sender, RoutedEventArgs e) => ChuyenSangTabTiepNhan();
        private void BtnChuyenSangPhanLoai_Click(object sender, RoutedEventArgs e) => ChuyenSangTabPhanLoai();
        private void BtnChuyenSangXuatHang_Click(object sender, RoutedEventArgs e) => ChuyenSangTabXuatKho();
        private void BtnChuyenSangLichSu_Click(object sender, RoutedEventArgs e) => ChuyenSangTabLichSu();
        private void BtnLamMoiKho_Click(object sender, RoutedEventArgs e) => NapDuLieuKho();
        #endregion

        #region Nghiệp Vụ Nhánh 3: Phân Luồng Bưu Kiện Tại Dock
        private void ApDungLocPhanLoai()
        {
            string tuKhoa = txtTimKiemPhanLoai?.Text.Trim().ToLower() ?? "";
            var danhSach = _danhSachDonChoPhanLoai;

            if (!string.IsNullOrEmpty(tuKhoa))
            {
                danhSach = danhSach.Where(d =>
                    d.OrderCode.ToLower().Contains(tuKhoa) ||
                    d.ProductSummary.ToLower().Contains(tuKhoa) ||
                    d.ReceiverAddress.ToLower().Contains(tuKhoa) ||
                    d.DestinationArea.ToLower().Contains(tuKhoa)).ToList();
            }

            if (dgPhanLoaiHang != null)
            {
                dgPhanLoaiHang.ItemsSource = danhSach;
            }

            if (txtDemKienChoPhanLoai != null)
            {
                txtDemKienChoPhanLoai.Text = $"{danhSach.Count} bưu kiện đang chờ phân luồng tại Dock tiếp nhận";
            }
        }

        private void TxtTimKiemPhanLoai_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApDungLocPhanLoai();
        }

        /// <summary>
        /// SỰ KIỆN: Phân loại bưu kiện vào luồng GIAO CHẶNG CUỐI (Last-Mile)
        /// </summary>
        private void BtnPhanLoaiChangCuoi_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is ShippingOrder donHang)
            {
                WarehouseContext.Instance.XacNhanPhanLoaiKienHang(
                    donHang.OrderCode,
                    donHang.ProductSummary,
                    WarehouseMovementType.SortingLastMile,
                    $"Máng Bưu Tá ({donHang.DestinationArea})",
                    UserSession.Current.CurrentUser?.FullName ?? "Thủ Kho Hệ Thống",
                    $"Phân loại giao chặng cuối tới địa bàn {donHang.DestinationArea}");

                MessageBox.Show(
                    $"ĐÃ PHÂN LOẠI THÀNH CÔNG HÀNG CHẶNG CUỐI!\n\n" +
                    $"• Mã bưu kiện: {donHang.OrderCode}\n" +
                    $"• Hàng hóa: {donHang.ProductSummary}\n" +
                    $"• Phân luồng: 🛵 Giao Chặng Cuối Cho Shipper Nội Thành\n" +
                    $"• Vị trí xếp kiện: Máng Tuyến Bưu Tá ({donHang.DestinationArea})",
                    "Phân Loại Chặng Cuối", MessageBoxButton.OK, MessageBoxImage.Information);

                NapDuLieuKho();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Phân loại bưu kiện vào luồng TRUNG CHUYỂN LIÊN TỈNH (Transit)
        /// </summary>
        private void BtnPhanLoaiTrungChuyen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is ShippingOrder donHang)
            {
                WarehouseContext.Instance.XacNhanPhanLoaiKienHang(
                    donHang.OrderCode,
                    donHang.ProductSummary,
                    WarehouseMovementType.SortingTransit,
                    "DOCK-OUTBOUND (Cửa Xuất Xe Tải)",
                    UserSession.Current.CurrentUser?.FullName ?? "Thủ Kho Hệ Thống",
                    $"Phân loại trung chuyển liên tỉnh chuyển tiếp bến tiếp theo");

                MessageBox.Show(
                    $"ĐÃ PHÂN LOẠI THÀNH CÔNG HÀNG TRUNG CHUYỂN!\n\n" +
                    $"• Mã bưu kiện: {donHang.OrderCode}\n" +
                    $"• Hàng hóa: {donHang.ProductSummary}\n" +
                    $"• Phân luồng: 🚛 Xe Tải Trung Chuyển Liên Tỉnh\n" +
                    $"• Vị trí xếp kiện: Cửa Xuất Xe Tải (Dock Outbound)",
                    "Phân Loại Trung Chuyển", MessageBoxButton.OK, MessageBoxImage.Information);

                NapDuLieuKho();
            }
        }
        #endregion

        #region Nghiệp Vụ Nhánh 4: Tồn Kho & Kiểm Kê (Stock Count & Adjustment)
        private void ApDungLocTonKho()
        {
            string tuKhoa = txtTimKiemTonKho?.Text.Trim().ToLower() ?? "";
            bool chiLocTonThap = chkChiHienTonThap?.IsChecked == true;

            var danhSach = _danhSachTonKhoGoc.AsEnumerable();

            if (chiLocTonThap)
            {
                danhSach = danhSach.Where(t => t.IsLowStock);
            }

            if (!string.IsNullOrEmpty(tuKhoa))
            {
                danhSach = danhSach.Where(t =>
                    t.ProductName.ToLower().Contains(tuKhoa) ||
                    t.ProductCode.ToLower().Contains(tuKhoa) ||
                    t.LocationCode.ToLower().Contains(tuKhoa) ||
                    t.BatchNumber.ToLower().Contains(tuKhoa));
            }

            if (dgHangDangLuuKho != null)
            {
                dgHangDangLuuKho.ItemsSource = danhSach.ToList();
            }
        }

        private void TxtTimKiemTonKho_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApDungLocTonKho();
        }

        private void ChkChiHienTonThap_Changed(object sender, RoutedEventArgs e)
        {
            ApDungLocTonKho();
        }

        private void NapDanhSachSanPhamKiemKe()
        {
            if (cboChonSanPhamKiemKe == null) return;

            cboChonSanPhamKiemKe.ItemsSource = _danhSachTonKhoGoc.Select(t => new
            {
                t.Id,
                DisplayText = $"[{t.ProductCode}] {t.ProductName} (Kệ: {t.LocationCode})",
                Item = t
            }).ToList();
            cboChonSanPhamKiemKe.DisplayMemberPath = "DisplayText";
            cboChonSanPhamKiemKe.SelectedValuePath = "Id";

            if (cboChonSanPhamKiemKe.Items.Count > 0 && cboChonSanPhamKiemKe.SelectedIndex < 0)
            {
                cboChonSanPhamKiemKe.SelectedIndex = 0;
            }
        }

        private void CboChonSanPhamKiemKe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboChonSanPhamKiemKe?.SelectedItem is { } selectedObj)
            {
                dynamic obj = selectedObj;
                Inventory t = obj.Item;
                txtThongTinTonHienTai.Text = $"Tồn hệ thống: {t.Quantity} kiện | Đang giữ chỗ: {t.ReservedQuantity} | Khả dụng: {t.AvailableQuantity} | Kệ: {t.LocationCode}";
                txtSoLuongThucTe.Text = t.Quantity.ToString();
                txtSoLuongHuHong.Text = t.DamagedQuantity.ToString();
            }
        }

        private void BtnMoKiemKeKho_Click(object sender, RoutedEventArgs e)
        {
            NapDanhSachSanPhamKiemKe();
            modalKiemKeTonKho.Visibility = Visibility.Visible;
        }

        private void BtnDongKiemKe_Click(object sender, RoutedEventArgs e)
        {
            modalKiemKeTonKho.Visibility = Visibility.Collapsed;
        }

        private void BtnXacNhanKiemKe_Click(object sender, RoutedEventArgs e)
        {
            if (cboChonSanPhamKiemKe.SelectedValue == null)
            {
                MessageBox.Show("Vui lòng chọn sản phẩm cần kiểm kê!", "Thiếu Dữ Liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtSoLuongThucTe.Text.Trim(), out int soLuongThucTe) || soLuongThucTe < 0)
            {
                MessageBox.Show("Vui lòng nhập số lượng kiểm đếm thực tế hợp lệ (số nguyên >= 0)!", "Sai Định Dạng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtSoLuongHuHong.Text.Trim(), out int soLuongHuHong) || soLuongHuHong < 0)
            {
                soLuongHuHong = 0;
            }

            int inventoryId = (int)cboChonSanPhamKiemKe.SelectedValue;
            string lyDo = (cboLyDoDieuChinh.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Cân bằng tồn kho";
            string ghiChu = txtGhiChuKiemKe.Text.Trim();
            string nguoiThucHien = UserSession.Current.CurrentUser?.FullName ?? "Thủ Kho Hệ Thống";

            bool thanhCong = WarehouseContext.Instance.DieuChinhTonKho(inventoryId, soLuongThucTe, soLuongHuHong, lyDo, nguoiThucHien, ghiChu);

            if (thanhCong)
            {
                MessageBox.Show(
                    "ĐÃ CẬP NHẬT PHIẾU KIỂM KÊ & CÂN BẰNG TỒN THÀNH CÔNG!\n\n" +
                    $"• Số lượng thực tế mới: {soLuongThucTe} kiện\n" +
                    $"• Hàng hỏng ghi nhận: {soLuongHuHong} kiện\n" +
                    $"• Lý do: {lyDo}\n" +
                    $"• Đã tự động ghi nhật ký kiểm toán kho bãi.",
                    "Kiểm Kê Kho", MessageBoxButton.OK, MessageBoxImage.Information);

                modalKiemKeTonKho.Visibility = Visibility.Collapsed;
                NapDuLieuKho();
            }
        }
        #endregion

        #region Nghiệp Vụ Nhánh 5: Vị Trí Ô Kệ & Điều Chuyển (Put-away / Relocation)
        private void ApDungLocZone()
        {
            var tatCaViTri = WarehouseContext.Instance.GetAllLocations();
            var danhSach = tatCaViTri.AsEnumerable();

            if (_currentZoneFilter == "A")
            {
                danhSach = danhSach.Where(v => v.Zone.Contains("Tiêu Chuẩn") || v.Aisle == "A");
            }
            else if (_currentZoneFilter == "EXP")
            {
                danhSach = danhSach.Where(v => v.Zone.Contains("Hỏa Tốc") || v.Aisle == "EXP");
            }
            else if (_currentZoneFilter == "IN")
            {
                danhSach = danhSach.Where(v => v.Zone.Contains("Tiếp Nhận") || v.Aisle == "IN" || v.LocationCode.StartsWith("DOCK"));
            }

            if (icViTriLuuTru != null)
            {
                icViTriLuuTru.ItemsSource = danhSach.ToList();
            }
        }

        private void BtnLocZone_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                _currentZoneFilter = tag;

                // Cập nhật giao diện nút bấm phân khu
                CapNhatGiaoDienNutLocZone(tag);
                ApDungLocZone();
            }
        }

        private void CapNhatGiaoDienNutLocZone(string activeTag)
        {
            // Reset tất cả các nút về dạng nhạt
            var nutList = new[] { "ALL", "A", "EXP", "IN" };
            // Có thể duyệt tìm các nút trong giao diện
        }

        private void NapDanhSachViTriDieuChuyen()
        {
            var tatCaViTri = WarehouseContext.Instance.GetAllLocations();

            // Vị trí nguồn: các vị trí đang có hàng (tải trọng > 0)
            var dsNguon = tatCaViTri.Where(v => v.CurrentWeight > 0).Select(v => new
            {
                v.Id,
                DisplayText = $"{v.LocationCode} (Đang chứa: {v.CurrentWeight:N0} kg)",
                Item = v
            }).ToList();

            // Vị trí đích: các vị trí còn sức chứa
            var dsDich = tatCaViTri.Where(v => v.CurrentWeight < v.MaxWeightCapacity).Select(v => new
            {
                v.Id,
                DisplayText = $"{v.LocationCode} (Còn trống: {v.MaxWeightCapacity - v.CurrentWeight:N0} kg)",
                Item = v
            }).ToList();

            if (cboViTriNguon != null)
            {
                cboViTriNguon.ItemsSource = dsNguon;
                cboViTriNguon.DisplayMemberPath = "DisplayText";
                cboViTriNguon.SelectedValuePath = "Id";
                if (dsNguon.Count > 0 && cboViTriNguon.SelectedIndex < 0) cboViTriNguon.SelectedIndex = 0;
            }

            if (cboViTriDich != null)
            {
                cboViTriDich.ItemsSource = dsDich;
                cboViTriDich.DisplayMemberPath = "DisplayText";
                cboViTriDich.SelectedValuePath = "Id";
                if (dsDich.Count > 0 && cboViTriDich.SelectedIndex < 0) cboViTriDich.SelectedIndex = 0;
            }
        }

        private void BtnMoDieuChuyenKe_Click(object sender, RoutedEventArgs e)
        {
            NapDanhSachViTriDieuChuyen();
            modalDieuChuyenKe.Visibility = Visibility.Visible;
        }

        private void BtnDongDieuChuyen_Click(object sender, RoutedEventArgs e)
        {
            modalDieuChuyenKe.Visibility = Visibility.Collapsed;
        }

        private void BtnXacNhanDieuChuyen_Click(object sender, RoutedEventArgs e)
        {
            if (cboViTriNguon.SelectedValue == null || cboViTriDich.SelectedValue == null)
            {
                MessageBox.Show("Vui lòng chọn cả vị trí nguồn và vị trí đích!", "Thiếu Dữ Liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int idNguon = (int)cboViTriNguon.SelectedValue;
            int idDich = (int)cboViTriDich.SelectedValue;

            if (idNguon == idDich)
            {
                MessageBox.Show("Vị trí đích không được trùng với vị trí nguồn!", "Lỗi Chọn Vị Trí", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(txtKhoiLuongDieuChuyen.Text.Trim(), out double khoiLuong) || khoiLuong <= 0)
            {
                MessageBox.Show("Vui lòng nhập khối lượng cần điều chuyển hợp lệ (> 0 kg)!", "Sai Định Dạng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string moTaHang = txtMoTaHangDieuChuyen.Text.Trim();
            string ghiChu = txtGhiChuDieuChuyen.Text.Trim();
            string nguoiThucHien = UserSession.Current.CurrentUser?.FullName ?? "Thủ Kho Hệ Thống";

            bool thanhCong = WarehouseContext.Instance.DieuChuyenViTriKe(idNguon, idDich, khoiLuong, moTaHang, nguoiThucHien, ghiChu);

            if (thanhCong)
            {
                MessageBox.Show(
                    "ĐÃ THỰC HIỆN LỆNH ĐIỀU CHUYỂN Ô KỆ THÀNH CÔNG!\n\n" +
                    $"• Khối lượng di dời: {khoiLuong:N1} kg\n" +
                    $"• Mặt hàng: {moTaHang}\n" +
                    $"• Tải trọng của 2 ô kệ đã được tự động cân bằng lại.",
                    "Điều Chuyển Ô Kệ", MessageBoxButton.OK, MessageBoxImage.Information);

                modalDieuChuyenKe.Visibility = Visibility.Collapsed;
                NapDuLieuKho();
            }
            else
            {
                MessageBox.Show("Không thể thực hiện điều chuyển (kiểm tra lại tải trọng nguồn hoặc đích)!", "Lỗi Thao Tác", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Nghiệp Vụ Nhánh 7: Lịch Sử Nhập / Xuất & Nhật Ký Biến Động
        private void ApDungLocLichSu()
        {
            string tuKhoa = txtTimKiemLichSu?.Text.Trim().ToLower() ?? "";
            int loaiIndex = cboLocLoaiBienDong?.SelectedIndex ?? 0;

            var danhSach = _danhSachBienDongGoc.AsEnumerable();

            // Lọc theo loại biến động
            if (loaiIndex > 0)
            {
                danhSach = loaiIndex switch
                {
                    1 => danhSach.Where(m => m.MovementType == WarehouseMovementType.InboundReceiving),
                    2 => danhSach.Where(m => m.MovementType == WarehouseMovementType.SortingLastMile),
                    3 => danhSach.Where(m => m.MovementType == WarehouseMovementType.SortingTransit),
                    4 => danhSach.Where(m => m.MovementType == WarehouseMovementType.OutboundLastMile),
                    5 => danhSach.Where(m => m.MovementType == WarehouseMovementType.OutboundTransit),
                    6 => danhSach.Where(m => m.MovementType == WarehouseMovementType.StockRelocation),
                    7 => danhSach.Where(m => m.MovementType == WarehouseMovementType.InventoryAdjustment),
                    _ => danhSach
                };
            }

            // Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(tuKhoa))
            {
                danhSach = danhSach.Where(m =>
                    m.TransactionCode.ToLower().Contains(tuKhoa) ||
                    m.ItemName.ToLower().Contains(tuKhoa) ||
                    m.LocationCode.ToLower().Contains(tuKhoa) ||
                    m.OperatorName.ToLower().Contains(tuKhoa) ||
                    m.Notes.ToLower().Contains(tuKhoa));
            }

            if (dgLichSuBienDongKho != null)
            {
                dgLichSuBienDongKho.ItemsSource = danhSach.ToList();
            }
        }

        private void CboLocLoaiBienDong_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApDungLocLichSu();
        }

        private void TxtTimKiemLichSu_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApDungLocLichSu();
        }

        /// <summary>
        /// SỰ KIỆN: Xuất toàn bộ nhật ký biến động kho ra tệp CSV (UTF-8 BOM)
        /// </summary>
        private void BtnXuatLichSuCsv_Click(object sender, RoutedEventArgs e)
        {
            var danhSachBienDong = WarehouseContext.Instance.GetAllWarehouseMovements();
            if (danhSachBienDong.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu nhật ký biến động để xuất!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hopThoaiLuu = new SaveFileDialog
            {
                Filter = "Tệp CSV Báo Cáo (*.csv)|*.csv",
                FileName = $"NhatKyKho_Logistics_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Lưu Nhật Ký Nhập / Xuất Kho"
            };

            if (hopThoaiLuu.ShowDialog() == true)
            {
                try
                {
                    var noiDungCsv = new StringBuilder();
                    noiDungCsv.AppendLine("Mã Giao Dịch;Thời Gian;Loại Nghiệp Vụ;Tên Hàng / Kiện;Mã Tham Chiếu;Khối Lượng (kg);Luồng Di Chuyển;Vị Trí Kệ;Người Thực Hiện;Ghi Chú");

                    foreach (var m in danhSachBienDong)
                    {
                        noiDungCsv.AppendLine($"{m.TransactionCode};{m.Timestamp:dd/MM/yyyy HH:mm:ss};{m.MovementTypeDisplayName};{m.ItemName};{m.ReferenceCode};{m.Weight:N1};{m.SourceOrDestination};{m.LocationCode};{m.OperatorName};{m.Notes}");
                    }

                    File.WriteAllText(hopThoaiLuu.FileName, noiDungCsv.ToString(), new UTF8Encoding(true));
                    MessageBox.Show($"Đã xuất nhật ký kho thành công ra file:\n{hopThoaiLuu.FileName}", "Xuất Báo Cáo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ngoaiLe)
                {
                    MessageBox.Show($"Lỗi khi ghi file: {ngoaiLe.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion
    }
}
