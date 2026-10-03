using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// PHÂN HỆ QUẢN LÝ KHO BÃI LOGISTICS (WAREHOUSE HUB)
    /// - Nhiệm vụ: Quản lý toàn bộ 7 nhánh chức năng kho bãi:
    ///   1. Tổng quan kho
    ///   2. Tiếp nhận hàng (Ghi nhận thời điểm vào kho)
    ///   3. Hàng đang lưu kho (Theo dõi tồn kho)
    ///   4. Phân loại hàng (Chặng cuối vs Trung chuyển)
    ///   5. Vị trí lưu trữ (Giám sát ô kệ)
    ///   6. Xuất hàng (Xác nhận xuất kho)
    ///   7. Lịch sử nhập / xuất (Nhật ký biến động)
    /// - Đối tượng sử dụng: Nhân viên kho, Quản lý kho.
    /// - Tương tác dữ liệu: WarehouseContext.cs, Inventory.cs, WarehouseLocation.cs, ImportOrder.cs, WarehouseMovement.cs.
    /// </summary>
    public partial class WarehouseManagementView : UserControl
    {
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
            var danhSachTonKho = khoDuLieu.GetAllInventories();
            var danhSachPhieuNhap = khoDuLieu.GetAllImportOrders();
            var danhSachBienDong = khoDuLieu.GetAllWarehouseMovements();
            var tatCaDonHang = khoDuLieu.GetAllShippingOrders();
            var danhSachTaiXe = khoDuLieu.GetAllShippers().Where(s => s.Status == ShipperStatus.Active).ToList();

            // 1. DỮ LIỆU NHÁNH 1: TỔNG QUAN KHO
            double tongTaiTrongToiDa = danhSachViTri.Sum(v => v.MaxWeightCapacity);
            double tongTaiTrongHienTai = danhSachViTri.Sum(v => v.CurrentWeight);
            double tyLeLapDay = tongTaiTrongToiDa > 0 ? (tongTaiTrongHienTai / tongTaiTrongToiDa) * 100.0 : 0;
            int tongSoSku = danhSachTonKho.Count;
            int tongSoLuongKien = danhSachTonKho.Sum(t => t.Quantity);
            double tongKhoiLuongTiepNhanHomNay = danhSachPhieuNhap.Sum(p => p.TotalWeight);
            int soChuyenXuatHomNay = danhSachBienDong.Count(m => m.MovementType == WarehouseMovementType.OutboundLastMile || m.MovementType == WarehouseMovementType.OutboundTransit);

            txtTyLeLapDayKho.Text = $"{tyLeLapDay:N1}%";
            txtTongMatHangLuuKho.Text = $"{tongSoSku} SKU ({tongSoLuongKien} kiện)";
            txtTiepNhanHomNay.Text = $"{danhSachPhieuNhap.Count} lô ({tongKhoiLuongTiepNhanHomNay:N0} kg)";
            txtXuatKhoHomNay.Text = $"{soChuyenXuatHomNay} chuyến đã xuất";

            // 2. DỮ LIỆU NHÁNH 2: TIẾP NHẬN HÀNG
            txtDongHoTiepNhan.Text = $"Thời điểm ghi nhận: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            dgDanhSachTiepNhan.ItemsSource = danhSachPhieuNhap;

            // 3. DỮ LIỆU NHÁNH 3: HÀNG ĐANG LƯU KHO
            dgHangDangLuuKho.ItemsSource = danhSachTonKho;

            // 4. DỮ LIỆU NHÁNH 4: PHÂN LOẠI HÀNG (CHẶNG CUỐI VS TRUNG CHUYỂN)
            // Lấy các bưu kiện mới tiếp nhận đang chờ phân loại
            var danhSachKienChoPhanLoai = tatCaDonHang
                .Where(d => d.Status == ShippingOrderStatus.NewReceived || d.Status == ShippingOrderStatus.PendingProcessing)
                .ToList();
            dgPhanLoaiHang.ItemsSource = danhSachKienChoPhanLoai;

            // 5. DỮ LIỆU NHÁNH 5: VỊ TRÍ LƯU TRỮ
            icViTriLuuTru.ItemsSource = danhSachViTri;

            // 6. DỮ LIỆU NHÁNH 6: XUẤT HÀNG
            cbDonHangXuat.Items.Clear();
            foreach (var donHang in tatCaDonHang.Take(10))
            {
                cbDonHangXuat.Items.Add(new ComboBoxItem
                {
                    Content = $"📦 [{donHang.OrderCode}] - {donHang.ProductSummary} ({donHang.Weight:N1} kg) -> {donHang.ReceiverName}",
                    Tag = donHang
                });
            }
            if (cbDonHangXuat.Items.Count > 0) cbDonHangXuat.SelectedIndex = 0;

            cbTaiXeTiepNhanXuat.Items.Clear();
            foreach (var taiXe in danhSachTaiXe)
            {
                cbTaiXeTiepNhanXuat.Items.Add(new ComboBoxItem
                {
                    Content = $"🛵 Shipper: {taiXe.FullName} ({taiXe.VehiclePlate} - {taiXe.DeliveryArea})",
                    Tag = taiXe
                });
            }
            cbTaiXeTiepNhanXuat.Items.Add(new ComboBoxItem
            {
                Content = "🚛 Xe tải trung chuyển liên tỉnh (29C-889.12 - Tuyến Bắc Nam)",
                Tag = null
            });
            if (cbTaiXeTiepNhanXuat.Items.Count > 0) cbTaiXeTiepNhanXuat.SelectedIndex = 0;

            var danhSachLenhXuat = danhSachBienDong
                .Where(m => m.MovementType == WarehouseMovementType.OutboundLastMile || m.MovementType == WarehouseMovementType.OutboundTransit)
                .ToList();
            dgDanhSachXuatKho.ItemsSource = danhSachLenhXuat;

            // 7. DỮ LIỆU NHÁNH 7: LỊCH SỬ NHẬP / XUẤT
            dgLichSuBienDongKho.ItemsSource = danhSachBienDong;
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
            panelHangLuuKho.Visibility = Visibility.Collapsed;
            panelPhanLoaiHang.Visibility = Visibility.Collapsed;
            panelViTriLuuTru.Visibility = Visibility.Collapsed;
            panelXuatHang.Visibility = Visibility.Collapsed;
            panelLichSuNhapXuat.Visibility = Visibility.Collapsed;

            // Hiện panel được chọn
            if (tabTongQuanKho.IsChecked == true) panelTongQuanKho.Visibility = Visibility.Visible;
            else if (tabTiepNhanHang.IsChecked == true)
            {
                panelTiepNhanHang.Visibility = Visibility.Visible;
                txtDongHoTiepNhan.Text = $"Thời điểm ghi nhận: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            }
            else if (tabHangLuuKho.IsChecked == true) panelHangLuuKho.Visibility = Visibility.Visible;
            else if (tabPhanLoaiHang.IsChecked == true) panelPhanLoaiHang.Visibility = Visibility.Visible;
            else if (tabViTriLuuTru.IsChecked == true) panelViTriLuuTru.Visibility = Visibility.Visible;
            else if (tabXuatHang.IsChecked == true) panelXuatHang.Visibility = Visibility.Visible;
            else if (tabLichSuNhapXuat.IsChecked == true) panelLichSuNhapXuat.Visibility = Visibility.Visible;
        }

        #region Phím Tắt Chuyển Tab Nhanh Từ Màn Hình Tổng Quan
        private void BtnChuyenSangTiepNhan_Click(object sender, RoutedEventArgs e) => tabTiepNhanHang.IsChecked = true;
        private void BtnChuyenSangPhanLoai_Click(object sender, RoutedEventArgs e) => tabPhanLoaiHang.IsChecked = true;
        private void BtnChuyenSangXuatHang_Click(object sender, RoutedEventArgs e) => tabXuatHang.IsChecked = true;
        private void BtnChuyenSangLichSu_Click(object sender, RoutedEventArgs e) => tabLichSuNhapXuat.IsChecked = true;
        private void BtnLamMoiKho_Click(object sender, RoutedEventArgs e) => NapDuLieuKho();
        #endregion

        #region Nghiệp Vụ Nhánh 2: Tiếp Nhận Hàng Vào Kho (Ghi Giờ)
        /// <summary>
        /// SỰ KIỆN: Xác nhận tiếp nhận đơn hàng vào kho
        /// - Nhiệm vụ: Tự động lưu Timestamp giờ phút giây hàng vào kho,
        ///             tạo phiếu nhập mới trong ImportOrders và ghi nhật ký WarehouseMovement.
        /// </summary>
        private void BtnXacNhanTiepNhan_Click(object sender, RoutedEventArgs e)
        {
            string tenNguoiGui = txtTenNguoiGuiHang.Text.Trim();
            string maVanDonGoc = txtMaVanDonGoc.Text.Trim();
            string ghiChu = txtGhiChuTiepNhan.Text.Trim();
            string viTri = (cbViTriTiepNhan.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "DOCK-INBOUND-01";
            string loaiNguonStr = (cbNguonGuiHang.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Hub Trung Chuyển";

            if (string.IsNullOrWhiteSpace(tenNguoiGui))
            {
                MessageBox.Show("Vui lòng nhập tên đơn vị hoặc người gửi hàng!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(txtKhoiLuongTiepNhan.Text, out double khoiLuong) || khoiLuong <= 0)
            {
                MessageBox.Show("Khối lượng tiếp nhận phải là số dương!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var thoiDiemVaoKho = DateTime.Now;
            var phieuNhapMoi = new ImportOrder
            {
                ImportCode = $"NK-{DateTime.Now:yyMMdd}-{new Random().Next(100, 999)}",
                SenderName = tenNguoiGui,
                WaybillNumber = maVanDonGoc,
                TotalWeight = khoiLuong,
                Status = ImportOrderStatus.Approved,
                CreatedDate = thoiDiemVaoKho,
                CreatedByName = UserSession.Current.CurrentUser?.FullName ?? "Thủ Kho Hệ Thống",
                Notes = ghiChu
            };

            WarehouseContext.Instance.AddImportOrder(phieuNhapMoi);
            WarehouseContext.Instance.XacNhanTiepNhanDonVaoKho(
                tenNguoiGui, 
                phieuNhapMoi.ImportCode, 
                khoiLuong, 
                viTri, 
                phieuNhapMoi.CreatedByName, 
                $"Tiếp nhận nguồn {loaiNguonStr} lúc {thoiDiemVaoKho:HH:mm:ss}");

            MessageBox.Show(
                $"ĐÃ TIẾP NHẬN HÀNG VÀO KHO THÀNH CÔNG!\n\n" +
                $"• Mã phiếu nhập: {phieuNhapMoi.ImportCode}\n" +
                $"• Thời điểm hàng vào kho: {thoiDiemVaoKho:dd/MM/yyyy HH:mm:ss}\n" +
                $"• Nguồn gửi: {tenNguoiGui}\n" +
                $"• Khối lượng: {khoiLuong:N1} kg\n" +
                $"• Vị trí đặt tiếp nhận: {viTri}\n\n" +
                $"Dữ liệu đã được ghi nhận vào Sổ Kho và Nhật Ký Biến Động.",
                "Tiếp Nhận Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

            NapDuLieuKho();
        }
        #endregion

        #region Nghiệp Vụ Nhánh 3: Hàng Đang Lưu Kho
        private void TxtTimKiemTonKho_TextChanged(object sender, TextChangedEventArgs e)
        {
            string tuKhoa = txtTimKiemTonKho.Text.Trim().ToLower();
            var danhSachTonKho = WarehouseContext.Instance.GetAllInventories();

            if (string.IsNullOrEmpty(tuKhoa))
            {
                dgHangDangLuuKho.ItemsSource = danhSachTonKho;
            }
            else
            {
                dgHangDangLuuKho.ItemsSource = danhSachTonKho.Where(t =>
                    t.ProductName.ToLower().Contains(tuKhoa) ||
                    t.ProductCode.ToLower().Contains(tuKhoa) ||
                    t.LocationCode.ToLower().Contains(tuKhoa) ||
                    t.BatchNumber.ToLower().Contains(tuKhoa)).ToList();
            }
        }
        #endregion

        #region Nghiệp Vụ Nhánh 4: Phân Loại Hàng (Chặng Cuối vs Trung Chuyển)
        /// <summary>
        /// SỰ KIỆN: Phân loại bưu kiện vào luồng GIAO CHẶNG CUỐI (Last-Mile)
        /// - Nhiệm vụ: Ghi nhận bưu kiện xếp vào máng gom tuyến giao cho bưu tá nội thành.
        /// </summary>
        private void BtnPhanLoaiChangCuoi_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is ShippingOrder donHang)
            {
                WarehouseContext.Instance.XacNhanPhanLoaiKienHang(
                    donHang.OrderCode,
                    donHang.ProductSummary,
                    WarehouseMovementType.SortingLastMile,
                    "KHO-EXP-01 (Máng Tuyến Bưu Tá)",
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
        /// - Nhiệm vụ: Ghi nhận bưu kiện chuyển ra cửa xuất xe tải đường dài.
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
                    $"Phân loại trung chuyển liên tỉnh chuyển bến tiếp theo");

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

        #region Nghiệp Vụ Nhánh 6: Xuất Hàng Khỏi Kho
        /// <summary>
        /// SỰ KIỆN: Xác nhận xuất hàng khỏi kho
        /// - Nhiệm vụ: Ghi nhận thời điểm xuất hàng, cập nhật biến động và chuyển trạng thái xuất kho.
        /// </summary>
        private void BtnXacNhanXuatKho_Click(object sender, RoutedEventArgs e)
        {
            if (cbDonHangXuat.SelectedItem is not ComboBoxItem mucDonHang || mucDonHang.Tag is not ShippingOrder donHang)
            {
                MessageBox.Show("Vui lòng chọn bưu kiện hoặc lô hàng cần xuất khỏi kho!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string hinhThucXuat = (cbHinhThucXuat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Giao chặng cuối";
            string taiXeTiepNhan = (cbTaiXeTiepNhanXuat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Shipper Tiếp Nhận";
            string ghiChu = txtGhiChuXuatKho.Text.Trim();

            var loaiXuat = hinhThucXuat.Contains("chặng cuối") 
                ? WarehouseMovementType.OutboundLastMile 
                : WarehouseMovementType.OutboundTransit;

            var thoiDiemXuat = DateTime.Now;

            WarehouseContext.Instance.XacNhanXuatKho(
                donHang.OrderCode,
                taiXeTiepNhan,
                loaiXuat,
                donHang.Weight,
                UserSession.Current.CurrentUser?.FullName ?? "Thủ Kho Hệ Thống",
                $"{hinhThucXuat} - {ghiChu}");

            // Cập nhật trạng thái đơn hàng sang Đang Giao
            WarehouseContext.Instance.UpdateShippingOrderStatus(donHang.Id, ShippingOrderStatus.Delivering);

            MessageBox.Show(
                $"XÁC NHẬN XUẤT HÀNG KHỎI KHO THÀNH CÔNG!\n\n" +
                $"• Mã bưu kiện: {donHang.OrderCode}\n" +
                $"• Thời điểm xuất kho: {thoiDiemXuat:dd/MM/yyyy HH:mm:ss}\n" +
                $"• Phương thức: {hinhThucXuat}\n" +
                $"• Bên tiếp nhận: {taiXeTiepNhan}\n" +
                $"• Khối lượng: {donHang.Weight:N1} kg\n\n" +
                $"Kiện hàng đã rời khỏi kho và được ghi nhận vào Nhật Ký Xuất Kho.",
                "Xuất Kho Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

            NapDuLieuKho();
        }
        #endregion

        #region Nghiệp Vụ Nhánh 7: Lịch Sử Nhập / Xuất (Xuất CSV)
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
