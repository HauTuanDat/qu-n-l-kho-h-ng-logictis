using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// PHÂN HỆ QUẢN LÝ XUẤT KHO & BÀN GIAO VẬN TẢI (OUTBOUND DISPATCH MANAGEMENT)
    /// - Nghiệp vụ: 
    ///   1. Quản lý toàn diện các lệnh xuất kho bưu kiện khỏi kho bãi.
    ///   2. Phân tách rõ ràng 2 luồng xuất:
    ///      + Xuất giao chặng cuối (Last-Mile Outbound): Bàn giao trực tiếp cho tài xế Shipper đi phát.
    ///      + Xuất trung chuyển liên tỉnh (Hub Transit Outbound): Bốc xếp lên xe tải trung chuyển đi các tỉnh (Thái Nguyên, Hải Phòng, v.v.).
    ///   3. Lập lệnh xuất kho mới, tự động cập nhật trạng thái đơn sang Đang Giao và giải phóng vị trí kệ kho.
    ///   4. In Phiếu Xuất Kho kiêm Biên Bản Bàn Giao Vận Tải hợp lệ có giá trị pháp lý và đối soát COD.
    ///   5. Xuất báo cáo lịch sử xuất kho ra Excel/CSV.
    /// - Đối tượng sử dụng: Thủ kho (Warehouse Keeper), Trưởng ca điều phối (Dispatcher).
    /// - Tương tác dữ liệu: WarehouseMovement.cs, ShippingOrder.cs, Shipper.cs, WarehouseContext.cs.
    /// </summary>
    public partial class ExportManagementView : UserControl
    {
        private List<WarehouseMovement> _danhSachLichSuXuat = new();
        private WarehouseMovement? _banGhiXuatDangChon;

        public ExportManagementView()
        {
            InitializeComponent();
            NapDuLieuXuatKho();
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ dữ liệu lịch sử xuất kho và cập nhật các thẻ KPI
        /// </summary>
        public void NapDuLieuXuatKho()
        {
            var khoDuLieu = WarehouseContext.Instance;
            var tatCaBienDong = khoDuLieu.GetAllWarehouseMovements();

            // 1. Lọc ra các biến động thuộc loại Xuất Kho (Chặng cuối hoặc Trung chuyển)
            _danhSachLichSuXuat = tatCaBienDong
                .Where(m => m.MovementType == WarehouseMovementType.OutboundLastMile || m.MovementType == WarehouseMovementType.OutboundTransit)
                .OrderByDescending(m => m.Timestamp)
                .ToList();

            // 2. Thống kê KPI xuất kho
            int tongXuat = _danhSachLichSuXuat.Count;
            int xuatShipper = _danhSachLichSuXuat.Count(m => m.MovementType == WarehouseMovementType.OutboundLastMile);
            int xuatTransit = _danhSachLichSuXuat.Count(m => m.MovementType == WarehouseMovementType.OutboundTransit);

            int soDonChoXuat = khoDuLieu.GetAllShippingOrders()
                .Count(o => o.Status == ShippingOrderStatus.NewReceived || o.Status == ShippingOrderStatus.PendingProcessing);

            txtTongLenhXuatHomNay.Text = $"{tongXuat} lệnh";
            txtXuatShipperCount.Text = $"{xuatShipper} đơn";
            txtXuatTransitCount.Text = $"{xuatTransit} lô";
            txtDonChoXuatKho.Text = $"{soDonChoXuat} đơn";

            ApDungBoLocXuatKho();
        }

        public void LoadData() => NapDuLieuXuatKho();

        /// <summary>
        /// BỘ LỌC TÌM KIẾM ĐA NĂNG
        /// </summary>
        private void ApDungBoLocXuatKho()
        {
            if (dgLichSuXuatKho == null) return;

            string tuKhoa = txtTimKiemXuat?.Text.Trim().ToLower() ?? "";
            int loaiXuatIndex = cbLocHinhThucXuat?.SelectedIndex ?? 0;
            string khuVucChon = (cbLocKhuVucXuat?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            var ketQua = _danhSachLichSuXuat.AsEnumerable();

            // Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(tuKhoa))
            {
                ketQua = ketQua.Where(m =>
                    (m.TransactionCode != null && m.TransactionCode.ToLower().Contains(tuKhoa)) ||
                    (m.ReferenceCode != null && m.ReferenceCode.ToLower().Contains(tuKhoa)) ||
                    (m.ItemName != null && m.ItemName.ToLower().Contains(tuKhoa)) ||
                    (m.SourceOrDestination != null && m.SourceOrDestination.ToLower().Contains(tuKhoa)) ||
                    (m.OperatorName != null && m.OperatorName.ToLower().Contains(tuKhoa))
                );
            }

            // Lọc theo hình thức xuất
            if (loaiXuatIndex == 1) // Xuất chặng cuối
            {
                ketQua = ketQua.Where(m => m.MovementType == WarehouseMovementType.OutboundLastMile);
            }
            else if (loaiXuatIndex == 2) // Xuất trung chuyển
            {
                ketQua = ketQua.Where(m => m.MovementType == WarehouseMovementType.OutboundTransit);
            }

            // Lọc theo điểm đến
            if (!string.IsNullOrEmpty(khuVucChon) && !khuVucChon.Contains("Tất Cả"))
            {
                ketQua = ketQua.Where(m => 
                    (m.SourceOrDestination != null && m.SourceOrDestination.IndexOf(khuVucChon, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (m.Notes != null && m.Notes.IndexOf(khuVucChon, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            var danhSachHienThi = ketQua.ToList();
            dgLichSuXuatKho.ItemsSource = danhSachHienThi;
            txtSoLuongHienThi.Text = $"Hiển thị: {danhSachHienThi.Count} lệnh";
        }

        private void TxtTimKiemXuat_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtTimKiemXuatPlaceholder != null)
            {
                txtTimKiemXuatPlaceholder.Visibility = string.IsNullOrEmpty(txtTimKiemXuat.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            ApDungBoLocXuatKho();
        }

        private void CbLocXuatKho_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApDungBoLocXuatKho();
        private void BtnLamMoiDuLieu_Click(object sender, RoutedEventArgs e) => NapDuLieuXuatKho();

        #region Xử lý Modal: Lập Lệnh Xuất Kho Mới
        /// <summary>
        /// SỰ KIỆN: Bấm nút mở Modal Lập Lệnh Xuất Kho Mới
        /// </summary>
        private void BtnMoModalLapLenhXuat_Click(object sender, RoutedEventArgs e)
        {
            var khoDuLieu = WarehouseContext.Instance;

            // 1. Nạp danh sách đơn hàng đang chờ xuất kho
            var donChoXuat = khoDuLieu.GetAllShippingOrders()
                .Where(o => o.Status == ShippingOrderStatus.NewReceived || o.Status == ShippingOrderStatus.PendingProcessing)
                .OrderByDescending(o => o.IsExpress)
                .ThenBy(o => o.EstimatedDeliveryDate)
                .ToList();

            cbModalDonHangXuat.Items.Clear();
            foreach (var d in donChoXuat)
            {
                string tagExpress = d.IsExpress ? "⚡ [HỎA TỐC] " : "[Tiêu Chuẩn] ";
                cbModalDonHangXuat.Items.Add(new ComboBoxItem
                {
                    Content = $"{tagExpress}{d.OrderCode} - {d.ProductSummary} ({d.Weight}kg) -> {d.DestinationArea}",
                    Tag = d
                });
            }

            if (cbModalDonHangXuat.Items.Count > 0)
            {
                cbModalDonHangXuat.SelectedIndex = 0;
            }
            else
            {
                txtChiTietNguoiNhan.Text = "Không có đơn chờ xuất";
                txtChiTietKhoiLuong.Text = "0 kg";
                txtChiTietTienCod.Text = "0 đ";
                txtModalDichDen.Text = "Kho hiện không có đơn chờ";
            }

            // 2. Nạp danh sách Shipper khả dụng
            var danhSachShipper = khoDuLieu.GetAllShippers().Where(s => !s.IsLocked).ToList();
            cbModalShipperTiepNhan.Items.Clear();
            foreach (var s in danhSachShipper)
            {
                cbModalShipperTiepNhan.Items.Add(new ComboBoxItem
                {
                    Content = $"🛵 {s.FullName} ({s.Phone}) - Tuyến: {s.DeliveryArea} - Xe: {s.VehiclePlate}",
                    Tag = s
                });
            }

            if (cbModalShipperTiepNhan.Items.Count > 0)
            {
                cbModalShipperTiepNhan.SelectedIndex = 0;
            }

            gridModalLapLenhXuat.Visibility = Visibility.Visible;
        }

        private void BtnDongModalLapLenh_Click(object sender, RoutedEventArgs e)
        {
            gridModalLapLenhXuat.Visibility = Visibility.Collapsed;
        }

        private void CbModalHinhThucXuat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelChonShipper == null || panelXeTaiTrungChuyen == null) return;

            bool isLastMile = cbModalHinhThucXuat.SelectedIndex == 0;
            panelChonShipper.Visibility = isLastMile ? Visibility.Visible : Visibility.Collapsed;
            panelXeTaiTrungChuyen.Visibility = isLastMile ? Visibility.Collapsed : Visibility.Visible;
        }

        private void CbModalDonHangXuat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbModalDonHangXuat.SelectedItem is ComboBoxItem item && item.Tag is ShippingOrder donHang)
            {
                txtChiTietNguoiNhan.Text = $"{donHang.ReceiverName} ({donHang.ReceiverPhone})";
                txtChiTietKhoiLuong.Text = $"{donHang.Weight:N1} kg";
                txtChiTietTienCod.Text = $"{donHang.CodAmount:N0} đ";
                txtModalDichDen.Text = $"{donHang.DestinationArea} - {donHang.ReceiverAddress}";
            }
        }

        /// <summary>
        /// SỰ KIỆN: Xác nhận tạo lệnh xuất kho và mở xem trước phiếu bàn giao
        /// </summary>
        private void BtnXacNhanLapLenhXuat_Click(object sender, RoutedEventArgs e)
        {
            if (cbModalDonHangXuat.SelectedItem is not ComboBoxItem itemDon || itemDon.Tag is not ShippingOrder donHang)
            {
                MessageBox.Show("Vui lòng chọn bưu kiện cần xuất khỏi kho!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isLastMile = cbModalHinhThucXuat.SelectedIndex == 0;
            string hinhThucXuat = isLastMile ? "Xuất giao chặng cuối (Shipper)" : "Xuất trung chuyển (Xe tải)";
            var loaiXuat = isLastMile ? WarehouseMovementType.OutboundLastMile : WarehouseMovementType.OutboundTransit;

            string benTiepNhan;
            if (isLastMile)
            {
                if (cbModalShipperTiepNhan.SelectedItem is ComboBoxItem itemShipper && itemShipper.Tag is Shipper s)
                {
                    benTiepNhan = $"Shipper: {s.FullName} ({s.Phone}) - Xe: {s.VehiclePlate}";
                    donHang.AssignedShipperName = s.FullName;
                    donHang.ShipperPhone = s.Phone;
                }
                else
                {
                    benTiepNhan = "Shipper Tiếp Nhận";
                }
            }
            else
            {
                benTiepNhan = txtModalXeTaiTrungChuyen.Text.Trim();
                if (string.IsNullOrEmpty(benTiepNhan)) benTiepNhan = "Xe tải trung chuyển liên tỉnh";
            }

            string ghiChu = txtModalGhiChu.Text.Trim();
            string nguoiThucHien = UserSession.Current.CurrentUser?.FullName ?? "Thủ Kho Hệ Thống";

            // 1. Ghi nhận giao dịch xuất kho vào WarehouseContext
            WarehouseContext.Instance.XacNhanXuatKho(
                donHang.OrderCode,
                benTiepNhan,
                loaiXuat,
                donHang.Weight,
                nguoiThucHien,
                $"{hinhThucXuat} - {ghiChu}");

            // 2. Chuyển trạng thái đơn sang Đang Giao Hàng
            WarehouseContext.Instance.UpdateShippingOrderStatus(donHang.Id, ShippingOrderStatus.Delivering);

            // 3. Đóng modal lập lệnh
            gridModalLapLenhXuat.Visibility = Visibility.Collapsed;

            // 4. Nạp lại bảng dữ liệu
            NapDuLieuXuatKho();

            // 5. Tìm bản ghi vừa xuất và hiển thị ngay phiếu in bàn giao
            var banGhiVuaXuat = _danhSachLichSuXuat.FirstOrDefault(m => m.ReferenceCode == donHang.OrderCode);
            HienThiPhieuInBaoCao(banGhiVuaXuat, donHang);

            MessageBox.Show(
                $"XUẤT KHO THÀNH CÔNG BƯU KIỆN [{donHang.OrderCode}]!\n\n" +
                $"• Thời điểm xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n" +
                $"• Bên nhận: {benTiepNhan}\n" +
                $"• Tuyến giao: {donHang.DestinationArea}\n\n" +
                $"Hệ thống đã tự động mở Phiếu Xuất Kho Kiêm Biên Bản Bàn Giao để bạn in hoặc lưu trữ.",
                "Xuất Kho Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region Xử lý Modal: Xem & In Phiếu Xuất Kho Bàn Giao
        /// <summary>
        /// SỰ KIỆN: Bấm nút in phiếu trên dòng của bảng
        /// </summary>
        private void BtnInPhieuRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is WarehouseMovement banGhi)
            {
                _banGhiXuatDangChon = banGhi;
                HienThiPhieuInBaoCao(banGhi, null);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Bấm nút in phiếu xuất gần nhất trên thanh Header
        /// </summary>
        private void BtnInPhieuGanNhat_Click(object sender, RoutedEventArgs e)
        {
            if (_danhSachLichSuXuat.Count == 0)
            {
                MessageBox.Show("Chưa có lệnh xuất kho nào được ghi nhận hôm nay!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var banGhiMoiNhat = _danhSachLichSuXuat.First();
            HienThiPhieuInBaoCao(banGhiMoiNhat, null);
        }

        /// <summary>
        /// HÀM ĐIỀN THÔNG TIN VÀO MẪU IN PHIẾU XUẤT KHO
        /// </summary>
        private void HienThiPhieuInBaoCao(WarehouseMovement? banGhi, ShippingOrder? donHangKemTheo)
        {
            if (banGhi == null) return;

            txtInMaPhieuXuat.Text = $"MÃ: {banGhi.TransactionCode}";
            txtInNgayXuat.Text = $"Ngày: {banGhi.Timestamp:dd/MM/yyyy HH:mm}";
            txtInNguoiGiao.Text = $"• Thủ kho xuất: {banGhi.OperatorName}";
            txtInNguoiNhan.Text = $"• Bên nhận: {banGhi.SourceOrDestination}";
            txtInTuyenDuong.Text = $"• Ghi chú tuyến: {banGhi.Notes}";

            txtInDongMaDon.Text = string.IsNullOrEmpty(banGhi.ReferenceCode) ? "---" : banGhi.ReferenceCode;
            txtInDongTenHang.Text = banGhi.ItemName;
            txtInDongKhoiLuong.Text = $"{banGhi.Weight:N1} kg";

            decimal tienCod = 0;
            if (donHangKemTheo != null)
            {
                tienCod = donHangKemTheo.CodAmount;
            }
            else
            {
                var don = WarehouseContext.Instance.GetAllShippingOrders().FirstOrDefault(o => o.OrderCode == banGhi.ReferenceCode);
                if (don != null) tienCod = don.CodAmount;
            }

            txtInDongTienCod.Text = $"{tienCod:N0} đ";
            txtInTongTienCod.Text = $"{tienCod:N0} đ";

            txtInChuKyThuKho.Text = banGhi.OperatorName;
            txtInChuKyTaiXe.Text = banGhi.SourceOrDestination.Split('-')[0].Trim();

            gridModalInPhieuXuat.Visibility = Visibility.Visible;
        }

        private void BtnDongModalInPhieu_Click(object sender, RoutedEventArgs e)
        {
            gridModalInPhieuXuat.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// SỰ KIỆN: Gọi lệnh in Windows PrintDialog để in phiếu bàn giao
        /// </summary>
        private void BtnThucHienIn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hopThoaiIn = new PrintDialog();
                if (hopThoaiIn.ShowDialog() == true)
                {
                    hopThoaiIn.PrintVisual(panelMauInPhieuXuat, "Phieu_Xuat_Kho_Logix");
                    MessageBox.Show("Đã gửi lệnh in Phiếu Xuất Kho thành công!", "In Ấn Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi in phiếu: {ex.Message}", "Lỗi In Ấn", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Xuất Excel / CSV Lịch Sử Xuất Kho
        /// <summary>
        /// SỰ KIỆN: Xuất danh sách lệnh xuất kho ra file CSV chuẩn UTF-8 BOM
        /// </summary>
        private void BtnXuatExcelLichSu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var danhSachHienTai = dgLichSuXuatKho.ItemsSource as IEnumerable<WarehouseMovement> ?? _danhSachLichSuXuat;
                var danhSach = danhSachHienTai.ToList();

                if (danhSach.Count == 0)
                {
                    MessageBox.Show("Không có dữ liệu xuất kho để kết xuất!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Mã Phiếu Xuất,Thời Điểm Xuất,Mã Đơn Hàng,Loại Hình Xuất,Mặt Hàng,Bên Tiếp Nhận,Khối Lượng (kg),Thủ Kho Xuất,Ghi Chú Nghiệp Vụ");

                foreach (var m in danhSach)
                {
                    sb.AppendLine($"\"{m.TransactionCode}\",\"{m.Timestamp:dd/MM/yyyy HH:mm:ss}\",\"{m.ReferenceCode}\",\"{m.MovementTypeDisplayName}\",\"{m.ItemName}\",\"{m.SourceOrDestination}\",{m.Weight},\"{m.OperatorName}\",\"{m.Notes}\"");
                }

                string duongDanDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string tenTep = $"BaoCao_XuatKho_Logistics_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string duongDan = Path.Combine(duongDanDesktop, tenTep);

                File.WriteAllText(duongDan, sb.ToString(), new UTF8Encoding(true));

                MessageBox.Show(
                    $"XUẤT BÁO CÁO XUẤT KHO THÀNH CÔNG!\n\n" +
                    $"• Tổng số lệnh xuất: {danhSach.Count} bản ghi\n" +
                    $"• File Excel/CSV đã được lưu tại Desktop:\n{duongDan}",
                    "Báo Cáo Xuất Kho", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}
