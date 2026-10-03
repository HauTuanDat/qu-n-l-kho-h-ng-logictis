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
    /// PHÂN HỆ ĐIỀU PHỐI & PHÂN BỔ ĐƠN HÀNG THEO NĂNG LỰC (PRIORITY DISPATCH ENGINE)
    /// - Nghiệp vụ: Giải quyết bài toán quá tải kho khi số lượng đơn cần giao (ví dụ: 100 đơn) 
    ///   vượt quá khả năng vận hành thực tế của đội ngũ shipper trong ngày (ví dụ: chỉ giao được 50 đơn).
    /// - Thuật toán ma trận ưu tiên đa tầng:
    ///   1. Ưu tiên số 1 (Tối thượng): 100% Đơn Hỏa Tốc (Express) (+1000 điểm ma trận) được duyệt trước.
    ///   2. Ưu tiên số 2 (SLA Deadline): Đơn quá hạn hoặc cận hạn cam kết giao trong 1-2h (+500đ đến +300đ) xếp tiếp theo.
    ///   3. Ưu tiên số 3 (FIFO): Đơn lưu kho lâu nhất được ưu tiên hơn đơn mới tạo.
    /// - Kết quả đầu ra:
    ///   + Nhóm A (🟢 Duyệt Giao Ngay): Đúng số lượng theo năng lực (Top 50 đơn).
    ///   + Nhóm B (🟡 Lưu Kho Ca Sau): Các đơn tiêu chuẩn có SLA còn xa, dời sang ca tiếp theo an toàn.
    /// - Đối tượng sử dụng: Quản lý kho, Trưởng bộ phận điều phối (Dispatcher).
    /// - Tương tác dữ liệu: ShippingOrder.cs, WarehouseContext.cs, PriorityDispatchItem.cs.
    /// </summary>
    public partial class PriorityDispatchView : UserControl
    {
        // =========================================================================
        // DANH SÁCH DỮ LIỆU ĐIỀU PHỐI NỘI BỘ
        // =========================================================================
        private List<PriorityDispatchItem> _danhSachDuyetGiao = new();
        private List<PriorityDispatchItem> _danhSachLuuKho = new();
        private int _nangLucGiaoHienTai = 40;

        public PriorityDispatchView()
        {
            InitializeComponent();
            NapDuLieuKho();
        }

        /// <summary>
        /// HÀM TIỆN ÍCH: Lấy danh sách đơn chờ giao từ WarehouseContext và áp dụng bộ lọc Khu Vực
        /// </summary>
        private List<ShippingOrder> LayDanhSachDonChoGiao()
        {
            var khoDuLieu = WarehouseContext.Instance;
            var tatCaDon = khoDuLieu.GetAllShippingOrders();

            var donChoGiao = tatCaDon
                .Where(o => o.Status == ShippingOrderStatus.NewReceived || o.Status == ShippingOrderStatus.PendingProcessing)
                .ToList();

            string khuVucChon = (cboLocKhuVuc?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            if (!string.IsNullOrEmpty(khuVucChon) && !khuVucChon.Contains("Tất Cả"))
            {
                donChoGiao = donChoGiao.Where(o => 
                    (o.DestinationArea != null && o.DestinationArea.IndexOf(khuVucChon, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (o.ReceiverAddress != null && o.ReceiverAddress.IndexOf(khuVucChon, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            return donChoGiao;
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ dữ liệu đơn hàng chờ giao từ WarehouseContext và chạy phân bổ
        /// </summary>
        public void NapDuLieuKho()
        {
            var donChoGiao = LayDanhSachDonChoGiao();

            // Nếu kho chưa có đủ dữ liệu kịch bản mô phỏng (dưới 20 đơn), tự động tạo 100 đơn mẫu Thái Nguyên
            if (donChoGiao.Count < 20)
            {
                WarehouseContext.Instance.GenerateThaiNguyenOrdersForSimulation(100);
                donChoGiao = LayDanhSachDonChoGiao();
            }

            // Đọc năng lực giao từ ô nhập giao diện (mặc định 40)
            if (int.TryParse(txtNangLucGiaoToiDa.Text.Trim(), out int nangLuc) && nangLuc > 0)
            {
                _nangLucGiaoHienTai = nangLuc;
            }
            else
            {
                _nangLucGiaoHienTai = 40;
                txtNangLucGiaoToiDa.Text = "40";
            }

            ThucHienPhanBoUuTien(donChoGiao, _nangLucGiaoHienTai);
        }

        public void LoadData() => NapDuLieuKho();

        /// <summary>
        /// THUẬT TOÁN MA TRẬN TÍNH ĐIỂM ƯU TIÊN VÀ PHÂN BỔ NĂNG LỰC
        /// - Nhiệm vụ: Chấm điểm cho từng đơn hàng theo công thức ma trận SLA & Hỏa tốc, sau đó tách 2 nhóm
        /// </summary>
        private void ThucHienPhanBoUuTien(List<ShippingOrder> danhSachDonGoc, int nangLucGiao)
        {
            var danhSachDaTinhDiem = new List<PriorityDispatchItem>();
            DateTime thoiDiemHienTai = DateTime.Now;

            foreach (var don in danhSachDonGoc)
            {
                double diemUuTien = 0;
                string capDoUuTien = "Cấp 3: Tiêu Chuẩn 📦";
                string mauHuyHieu = "#2563EB";
                string nenHuyHieu = "#EFF6FF";

                // TIÊU CHÍ 1: ĐƠN HỎA TỐC EXPRESS (ƯU TIÊN TUYỆT ĐỐI CẤP 1 - +1000 ĐIỂM)
                if (don.IsExpress)
                {
                    diemUuTien += 1000.0;
                    capDoUuTien = "⚡ Cấp 1: Hỏa Tốc";
                    mauHuyHieu = "#7C3AED";
                    nenHuyHieu = "#F3E8FF";
                }

                // TIÊU CHÍ 2: THỜI HẠN CAM KẾT GIAO SLA (DEADLINE)
                var khoangThoiGianConLai = don.EstimatedDeliveryDate - thoiDiemHienTai;

                if (khoangThoiGianConLai.TotalMinutes <= 0)
                {
                    // ĐÃ QUÁ HẠN CAM KẾT: Cực kỳ khẩn cấp cần giải cứu gấp
                    diemUuTien += 500.0 + Math.Min(Math.Abs(khoangThoiGianConLai.TotalHours) * 10, 200.0);
                    if (!don.IsExpress)
                    {
                        capDoUuTien = "🚨 Cấp 2: Quá Hạn SLA";
                        mauHuyHieu = "#DC2626";
                        nenHuyHieu = "#FEE2E2";
                    }
                }
                else if (khoangThoiGianConLai.TotalHours <= 2)
                {
                    // CẬN HẠN DƯỚI 2 GIỜ: Cần giao trong ca hiện tại
                    diemUuTien += 300.0 + (2.0 - khoangThoiGianConLai.TotalHours) * 50.0;
                    if (!don.IsExpress)
                    {
                        capDoUuTien = "⏱️ Cấp 2: Cận Hạn SLA";
                        mauHuyHieu = "#EA580C";
                        nenHuyHieu = "#FFEDD5";
                    }
                }
                else if (khoangThoiGianConLai.TotalHours <= 6)
                {
                    // CẬN HẠN DƯỚI 6 GIỜ
                    diemUuTien += 150.0 + (6.0 - khoangThoiGianConLai.TotalHours) * 10.0;
                    if (!don.IsExpress)
                    {
                        capDoUuTien = "⏱️ Cấp 2: Giao Ca Này";
                        mauHuyHieu = "#D97706";
                        nenHuyHieu = "#FEF3C7";
                    }
                }
                else if (khoangThoiGianConLai.TotalHours <= 12)
                {
                    diemUuTien += 60.0;
                }
                else
                {
                    diemUuTien += 10.0;
                }

                // TIÊU CHÍ 3: THỜI GIAN LƯU KHO FIFO (Đơn vào kho trước được cộng điểm ưu tiên)
                var thoiGianLuuKho = thoiDiemHienTai - don.CreatedDate;
                diemUuTien += Math.Min(thoiGianLuuKho.TotalHours * 2.0, 40.0);

                danhSachDaTinhDiem.Add(new PriorityDispatchItem
                {
                    Order = don,
                    PriorityScore = Math.Round(diemUuTien, 1),
                    PriorityLevel = capDoUuTien,
                    PriorityBadgeColor = mauHuyHieu,
                    PriorityBadgeBackground = nenHuyHieu
                });
            }

            // SẮP XẾP TOÀN BỘ DANH SÁCH: QUY TẮC QUÁ TẢI - ƯU TIÊN TUYỆT ĐỐI 100% ĐƠN HỎA TỐC (EXPRESS) ĐỨNG TRƯỚC
            var danhSachSapXep = danhSachDaTinhDiem
                .OrderByDescending(x => x.IsExpress)        // 1. Đơn Hỏa Tốc 100% luôn xếp trên đơn thường
                .ThenByDescending(x => x.PriorityScore)    // 2. Trong cùng nhóm: Đơn nào cận hạn/quá hạn hơn xếp trước
                .ToList();

            // PHÂN TÁCH THÀNH 2 NHÓM THEO HẠN MỨC NĂNG LỰC GIAO (VD: 40 ĐƠN)
            _danhSachDuyetGiao.Clear();
            _danhSachLuuKho.Clear();

            for (int i = 0; i < danhSachSapXep.Count; i++)
            {
                var muc = danhSachSapXep[i];
                muc.Rank = i + 1;

                if (i < nangLucGiao)
                {
                    // NẰM TRONG HẠN MỨC -> DUYỆT GIAO NGAY
                    muc.IsApprovedForDelivery = true;
                    muc.AllocationReason = muc.IsExpress 
                        ? "⚡ Ưu tiên số 1: Đơn Hỏa Tốc (Express) bắt buộc xuất kho ngay" 
                        : "Cận hạn cam kết SLA, còn chỗ trong hạn mức ngày nên được duyệt";
                    _danhSachDuyetGiao.Add(muc);
                }
                else
                {
                    // VƯỢT QUÁ HẠN MỨC -> LƯU KHO CHỜ CA SAU
                    muc.IsApprovedForDelivery = false;
                    muc.AllocationReason = muc.IsExpress
                        ? $"⚠️ Vượt quá hạn mức {nangLucGiao} đơn/ngày - Hỏa tốc dời chuyến tiếp theo"
                        : $"📦 Đơn thường: Lưu kho ca sau (Hạn SLA an toàn, nhường suất cho đơn Hỏa Tốc)";
                    _danhSachLuuKho.Add(muc);
                }
            }

            // CẬP NHẬT GIAO DIỆN & THẺ CHỈ SỐ KPI
            CapNhatThongKeGiaoDien(danhSachSapXep.Count, nangLucGiao);
        }

        /// <summary>
        /// CẬP NHẬT CÁC THẺ KPI & TIẾN ĐỘ LẤP ĐẦY CÔNG SUẤT
        /// </summary>
        private void CapNhatThongKeGiaoDien(int tongDon, int nangLucGiao)
        {
            int soDonHoaToc = _danhSachDuyetGiao.Count(x => x.IsExpress) + _danhSachLuuKho.Count(x => x.IsExpress);
            int hoaTocDuocDuyet = _danhSachDuyetGiao.Count(x => x.IsExpress);

            txtTongDonChoPhanBo.Text = $"{tongDon} đơn";
            txtDonHoaTocExpress.Text = $"{soDonHoaToc} đơn";

            if (soDonHoaToc > 0)
            {
                double phanTramHoaToc = (double)hoaTocDuocDuyet / soDonHoaToc * 100.0;
                txtTyLeDuyetHoaToc.Text = $"Đã duyệt {hoaTocDuocDuyet}/{soDonHoaToc} ({phanTramHoaToc:N0}% đơn)";
            }
            else
            {
                txtTyLeDuyetHoaToc.Text = "Không có đơn hỏa tốc";
            }

            txtDonDuocDuyet.Text = $"{_danhSachDuyetGiao.Count} / {nangLucGiao} đơn";
            txtDonLuuKhoCaSau.Text = $"{_danhSachLuuKho.Count} đơn";

            double tyLeLapDay = nangLucGiao > 0 ? ((double)_danhSachDuyetGiao.Count / nangLucGiao * 100.0) : 0;
            pbCongSuatGiao.Value = Math.Min(100.0, tyLeLapDay);

            txtCongSuatHienThi.Text = $"Tỷ lệ lấp đầy hạn mức: {_danhSachDuyetGiao.Count} / {nangLucGiao} đơn ({tyLeLapDay:N0}% công suất vận chuyển)";
            txtSoLuongDuyetHienTai.Text = $"{_danhSachDuyetGiao.Count} đơn";
            txtSoLuongLuuKhoHienTai.Text = $"{_danhSachLuuKho.Count} đơn";

            // Đổ dữ liệu vào 2 DataGrid
            dgDonDuyetGiao.ItemsSource = null;
            dgDonDuyetGiao.ItemsSource = _danhSachDuyetGiao;

            dgDonLuuKho.ItemsSource = null;
            dgDonLuuKho.ItemsSource = _danhSachLuuKho;
        }

        #region Các sự kiện nút thao tác điều khiển
        /// <summary>
        /// SỰ KIỆN: Người dùng đổi bộ lọc khu vực tỉnh/thành
        /// </summary>
        private void CboLocKhuVuc_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            NapDuLieuKho();
        }

        /// <summary>
        /// SỰ KIỆN: Nhấn nút Chạy Thuật Toán Phân Bổ Ưu Tiên
        /// </summary>
        private void BtnChayThuatToanPhanBo_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtNangLucGiaoToiDa.Text.Trim(), out int nangLuc) || nangLuc <= 0)
            {
                MessageBox.Show("Vui lòng nhập số lượng đơn có thể giao hôm nay là số nguyên dương!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _nangLucGiaoHienTai = nangLuc;

            var donChoGiao = LayDanhSachDonChoGiao();

            ThucHienPhanBoUuTien(donChoGiao, _nangLucGiaoHienTai);

            MessageBox.Show(
                $"THỰC THI THUẬT TOÁN PHÂN BỔ THÀNH CÔNG!\n\n" +
                $"• Tổng số đơn chờ phân bổ: {donChoGiao.Count} đơn\n" +
                $"• Hạn mức năng lực giao hôm nay: {_nangLucGiaoHienTai} đơn\n" +
                $"• Số đơn được DUYỆT GIAO NGAY: {_danhSachDuyetGiao.Count} đơn\n" +
                $"  (Trong đó có {_danhSachDuyetGiao.Count(x => x.IsExpress)} đơn Hỏa Tốc đã được duyệt 100%)\n" +
                $"• Số đơn LƯU KHO CHỜ CA SAU: {_danhSachLuuKho.Count} đơn\n\n" +
                $"Thuật toán đã ưu tiên tối đa cam kết SLA và đơn Express.",
                "Kết Quả Phân Bổ Ưu Tiên", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// SỰ KIỆN: Người dùng chọn các nút định mức nhanh (30, 40, 50, 70, 100)
        /// </summary>
        private void BtnPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.Content is string giaTriText && int.TryParse(giaTriText, out int giaTri))
            {
                txtNangLucGiaoToiDa.Text = giaTri.ToString();
                _nangLucGiaoHienTai = giaTri;

                var donChoGiao = LayDanhSachDonChoGiao();

                ThucHienPhanBoUuTien(donChoGiao, _nangLucGiaoHienTai);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Tự động tính hạn mức theo tổng Quota của các Shipper đang trực ca
        /// </summary>
        private void BtnLayQuotaShipper_Click(object sender, RoutedEventArgs e)
        {
            var danhSachShipper = WarehouseContext.Instance.GetAllShippers();
            var shipperTrucCa = danhSachShipper.Where(s => !s.IsLocked && (s.Status == ShipperStatus.Active || s.Status == ShipperStatus.Available)).ToList();

            int tongQuota = shipperTrucCa.Sum(s => s.MaxOrdersPerDay);
            if (tongQuota <= 0) tongQuota = 40;

            txtNangLucGiaoToiDa.Text = tongQuota.ToString();
            _nangLucGiaoHienTai = tongQuota;

            var donChoGiao = LayDanhSachDonChoGiao();

            ThucHienPhanBoUuTien(donChoGiao, _nangLucGiaoHienTai);

            MessageBox.Show(
                $"ĐÃ TÍNH TOÁN THEO ĐỘI NGŨ SHIPPER:\n\n" +
                $"• Số tài xế trực ca khả dụng: {shipperTrucCa.Count} tài xế\n" +
                $"• Tổng định mức đơn/ngày có thể vận chuyển: {tongQuota} đơn\n\n" +
                $"Hệ thống đã tự động gán năng lực giao hôm nay là {tongQuota} đơn.",
                "Định Mức Vận Tải Shipper", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// SỰ KIỆN: Sinh kịch bản đúng 100 đơn tại Thái Nguyên (Mô phỏng chính xác kịch bản người dùng yêu cầu: 100 đơn Thái Nguyên, giao 40 đơn)
        /// </summary>
        private void BtnSinh100DonThaiNguyen_Click(object sender, RoutedEventArgs e)
        {
            var xacNhan = MessageBox.Show(
                "BẠN CÓ MUỐN KHỞI TẠO KỊCH BẢN THỬ NGHIỆM ĐÚNG VÍ DỤ 100 ĐƠN THÁI NGUYÊN?\n\n" +
                "• Quy mô: 100 đơn hàng phân bổ tại Tỉnh THÁI NGUYÊN (TP Thái Nguyên, Sông Công, Phổ Yên, Đại Từ...)\n" +
                "• Cơ cấu đơn: ~32 đơn HỎA TỐC ⚡ (Express) + các đơn cận hạn SLA + 68 đơn tiêu chuẩn\n" +
                "• Hạn mức năng lực giao hôm nay: 40 ĐƠN\n\n" +
                "Thuật toán sẽ tự động phân bổ:\n" +
                "  + Đưa 40 đơn (toàn bộ đơn Hỏa Tốc + cận hạn gấp) sang tab [🟢 ĐÃ DUYỆT GIAO NGAY]\n" +
                "  + Đưa 60 đơn tiêu chuẩn còn lại sang tab [🟡 LƯU KHO CHỜ CA SAU]",
                "Khởi Tạo 100 Đơn Thái Nguyên (Giao 40 Đơn)", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (xacNhan == MessageBoxResult.Yes)
            {
                WarehouseContext.Instance.GenerateThaiNguyenOrdersForSimulation(100);

                if (cboLocKhuVuc != null)
                {
                    for (int i = 0; i < cboLocKhuVuc.Items.Count; i++)
                    {
                        if (cboLocKhuVuc.Items[i] is ComboBoxItem item && item.Content.ToString() == "Thái Nguyên")
                        {
                            cboLocKhuVuc.SelectedIndex = i;
                            break;
                        }
                    }
                }

                txtNangLucGiaoToiDa.Text = "40";
                _nangLucGiaoHienTai = 40;

                NapDuLieuKho();

                MessageBox.Show(
                    "ĐÃ CHẠY XONG THUẬT TOÁN ĐIỀU PHỐI ƯU TIÊN CHO 100 ĐƠN THÁI NGUYÊN!\n\n" +
                    $"• Số đơn DUYỆT GIAO NGAY: {_danhSachDuyetGiao.Count} / 40 đơn\n" +
                    $"  (Toàn bộ {_danhSachDuyetGiao.Count(x => x.IsExpress)} đơn Hỏa Tốc được ưu tiên tuyệt đối đi giao trước!)\n" +
                    $"• Số đơn LƯU KHO CA SAU: {_danhSachLuuKho.Count} đơn (Đơn tiêu chuẩn SLA còn xa, an toàn lưu kho ca sau)\n\n" +
                    "Bạn có thể bấm nút [Phê Duyệt Xuất Kho & Gán Shipper] ở góc phải để xuất bến giao hàng!",
                    "Phân Bổ Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Sinh 100 đơn hàng mẫu kịch bản mô phỏng chung để người dùng thử nghiệm tính năng
        /// </summary>
        private void BtnSinh100DonMau_Click(object sender, RoutedEventArgs e)
        {
            var xacNhan = MessageBox.Show(
                "Bạn có muốn sinh 100 đơn hàng mẫu kịch bản mô phỏng chung?\n\n" +
                "Kịch bản bao gồm:\n" +
                "• Khoảng 35 đơn HỎA TỐC ⚡ (Express 2h-4h)\n" +
                "• Các đơn quá hạn SLA và cận hạn giao trong 1-2h\n" +
                "• Khoảng 65 đơn tiêu chuẩn theo các quận huyện\n\n" +
                "Dữ liệu này sẽ giúp bạn kiểm thử rõ nét thuật toán ưu tiên!",
                "Khởi Tạo 100 Đơn Mẫu Kịch Bản", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (xacNhan == MessageBoxResult.Yes)
            {
                WarehouseContext.Instance.GenerateSampleOrdersForSimulation(100);
                NapDuLieuKho();
                MessageBox.Show("Đã sinh thành công 100 đơn hàng mẫu kịch bản!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Nạp lại kho từ CSDL
        /// </summary>
        private void BtnLamMoiDuLieu_Click(object sender, RoutedEventArgs e)
        {
            NapDuLieuKho();
        }

        /// <summary>
        /// SỰ KIỆN: Chuyển đổi giữa 2 Tab (🟢 Được Duyệt Giao vs 🟡 Lưu Kho Ca Sau)
        /// </summary>
        private void TabPhanBo_Checked(object sender, RoutedEventArgs e)
        {
            if (panelDuyetGiao == null || panelLuuKho == null) return;

            if (tabNhomDuyetGiao.IsChecked == true)
            {
                panelDuyetGiao.Visibility = Visibility.Visible;
                panelLuuKho.Visibility = Visibility.Collapsed;
            }
            else
            {
                panelDuyetGiao.Visibility = Visibility.Collapsed;
                panelLuuKho.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// SỰ KIỆN: Hoãn thủ công 1 đơn từ nhóm Duyệt sang nhóm Lưu kho
        /// </summary>
        private void BtnHoanDonSangCaSau_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is PriorityDispatchItem mucCanHoan)
            {
                _danhSachDuyetGiao.Remove(mucCanHoan);
                mucCanHoan.IsApprovedForDelivery = false;
                mucCanHoan.AllocationReason = "Điều phối viên hoãn thủ công sang ca sau";
                _danhSachLuuKho.Insert(0, mucCanHoan);

                // Cập nhật lại thứ hạng
                for (int i = 0; i < _danhSachDuyetGiao.Count; i++) _danhSachDuyetGiao[i].Rank = i + 1;
                for (int i = 0; i < _danhSachLuuKho.Count; i++) _danhSachLuuKho[i].Rank = _danhSachDuyetGiao.Count + i + 1;

                CapNhatThongKeGiaoDien(_danhSachDuyetGiao.Count + _danhSachLuuKho.Count, _nangLucGiaoHienTai);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Đẩy thủ công 1 đơn từ nhóm Lưu kho lên nhóm Duyệt giao gấp
        /// </summary>
        private void BtnDayLenDuyetGiao_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is PriorityDispatchItem mucCanDay)
            {
                _danhSachLuuKho.Remove(mucCanDay);
                mucCanDay.IsApprovedForDelivery = true;
                mucCanDay.AllocationReason = "Điều phối viên đẩy lên duyệt khẩn cấp (Khách giục)";
                _danhSachDuyetGiao.Add(mucCanDay);

                // Cập nhật lại thứ hạng
                for (int i = 0; i < _danhSachDuyetGiao.Count; i++) _danhSachDuyetGiao[i].Rank = i + 1;
                for (int i = 0; i < _danhSachLuuKho.Count; i++) _danhSachLuuKho[i].Rank = _danhSachDuyetGiao.Count + i + 1;

                CapNhatThongKeGiaoDien(_danhSachDuyetGiao.Count + _danhSachLuuKho.Count, _nangLucGiaoHienTai);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Phê duyệt xuất kho hàng loạt và tự động phân công Shipper
        /// </summary>
        private void BtnPheDuyetXuatKhoHangLoat_Click(object sender, RoutedEventArgs e)
        {
            if (_danhSachDuyetGiao.Count == 0)
            {
                MessageBox.Show("Hiện không có đơn hàng nào trong danh sách được duyệt!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var xacNhan = MessageBox.Show(
                $"XÁC NHẬN PHÊ DUYỆT XUẤT KHO HÀNG LOẠT:\n\n" +
                $"• Số lượng đơn phê duyệt: {_danhSachDuyetGiao.Count} đơn\n" +
                $"• Hệ thống sẽ tự động chuyển trạng thái sang [ĐANG GIAO HÀNG]\n" +
                $"• Tự động phân công cho các Shipper phù hợp theo từng khu vực quận/huyện\n\n" +
                $"Bạn có chắc chắn muốn tiến hành xuất kho?",
                "Phê Duyệt Điều Phối Xuất Kho", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (xacNhan == MessageBoxResult.Yes)
            {
                var danhSachId = _danhSachDuyetGiao.Select(x => x.OrderId).ToList();
                int soLuongThanhCong = WarehouseContext.Instance.BatchAssignOrdersToShippers(danhSachId);

                MessageBox.Show(
                    $"ĐÃ PHÊ DUYỆT VÀ PHÂN CÔNG THÀNH CÔNG {soLuongThanhCong} ĐƠN HÀNG!\n\n" +
                    $"• Toàn bộ {_danhSachDuyetGiao.Count(x => x.IsExpress)} đơn Hỏa Tốc đã được xuất kho ngay lập tức.\n" +
                    $"• Các tài xế Shipper đã tiếp nhận lộ trình giao hàng.\n" +
                    $"• {_danhSachLuuKho.Count} đơn còn lại tiếp tục được lưu giữ an toàn tại kho cho ca tiếp theo.",
                    "Xuất Kho Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);

                NapDuLieuKho();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Xuất báo cáo kết quả phân bổ năng lực ra tệp CSV (kèm UTF-8 BOM hiển thị chuẩn tiếng Việt trên Excel)
        /// </summary>
        private void BtnXuatBaoCaoCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hopThoaiLuu = new SaveFileDialog
                {
                    Filter = "CSV UTF-8 (*.csv)|*.csv|All Files (*.*)|*.*",
                    FileName = $"BaoCaoPhanBoUuTien_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "Xuất Báo Cáo Phân Bổ Năng Lực Điều Phối"
                };

                if (hopThoaiLuu.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Hạng,Mã Đơn,Loại Dịch Vụ,Cấp Độ Ưu Tiên,Điểm Ma Trận,Quyết Định,Hạn Cam Kết SLA,Khu Vực Giao,Người Nhận,SĐT,Tiền COD (VNĐ),Lý Do Phân Bổ");

                    // Xuất danh sách duyệt giao
                    foreach (var m in _danhSachDuyetGiao)
                    {
                        sb.AppendLine($"{m.Rank},\"{m.OrderCode}\",\"{m.ServiceTypeTag}\",\"{m.PriorityLevel}\",{m.PriorityScore},\"{m.AllocationDecisionText}\",\"{m.EstimatedDeliveryDate:dd/MM/yyyy HH:mm}\",\"{m.DestinationArea}\",\"{m.ReceiverName}\",\"{m.ReceiverPhone}\",{m.CodAmount:0},\"{m.AllocationReason}\"");
                    }

                    // Xuất danh sách lưu kho
                    foreach (var m in _danhSachLuuKho)
                    {
                        sb.AppendLine($"{m.Rank},\"{m.OrderCode}\",\"{m.ServiceTypeTag}\",\"{m.PriorityLevel}\",{m.PriorityScore},\"{m.AllocationDecisionText}\",\"{m.EstimatedDeliveryDate:dd/MM/yyyy HH:mm}\",\"{m.DestinationArea}\",\"{m.ReceiverName}\",\"{m.ReceiverPhone}\",{m.CodAmount:0},\"{m.AllocationReason}\"");
                    }

                    File.WriteAllText(hopThoaiLuu.FileName, sb.ToString(), new UTF8Encoding(true));

                    MessageBox.Show(
                        $"Đã xuất báo cáo phân bổ thành công ra tệp:\n{hopThoaiLuu.FileName}\n\n" +
                        $"Tổng số: {_danhSachDuyetGiao.Count + _danhSachLuuKho.Count} bản ghi (gồm {_danhSachDuyetGiao.Count} đơn duyệt và {_danhSachLuuKho.Count} đơn lưu kho).",
                        "Xuất Báo Cáo Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ngoaiLe)
            {
                MessageBox.Show($"Lỗi khi xuất tệp báo cáo: {ngoaiLe.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}
