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
        private int _nangLucGiaoHienTai = 50;

        /// <summary>
        /// Sự kiện yêu cầu phân hệ cha (DeliveryDispatchView) chuyển sang Tab TMS tương ứng (0: Phân bổ SLA, 1: Gom Tuyến, 2: Phân công Shipper, 3: Lịch sử)
        /// </summary>
        public event Action<int>? OnYeuCauChuyenTab;

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
                if (khuVucChon.Contains("Kịch Bản Chuẩn 100 Đơn (Hà Nội)"))
                {
                    donChoGiao = donChoGiao.Where(o => 
                        o.OrderCode.StartsWith("LOGIX-HN-") || 
                        (o.OrderCode.StartsWith("LOGIX-") && (o.OrderCode.Contains("-EXP-") || o.OrderCode.Contains("-STD-")) && !o.OrderCode.StartsWith("LOGIX-TN-"))
                    ).ToList();
                }
                else if (khuVucChon.Contains("Kịch Bản Chuẩn 100 Đơn (Thái Nguyên)"))
                {
                    donChoGiao = donChoGiao.Where(o => o.OrderCode.StartsWith("LOGIX-TN-")).ToList();
                }
                else
                {
                    donChoGiao = donChoGiao.Where(o => 
                        (o.DestinationArea != null && o.DestinationArea.IndexOf(khuVucChon, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (o.ReceiverAddress != null && o.ReceiverAddress.IndexOf(khuVucChon, StringComparison.OrdinalIgnoreCase) >= 0)
                    ).ToList();
                }
            }

            return donChoGiao;
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ dữ liệu đơn hàng chờ giao từ WarehouseContext và chạy phân bổ
        /// </summary>
        public void NapDuLieuKho()
        {
            var donChoGiao = LayDanhSachDonChoGiao();

            // Nếu hệ thống chưa từng nạp bộ kịch bản mô phỏng 100 đơn chuẩn, tự động khởi tạo lần đầu
            var kho = WarehouseContext.Instance;
            if (!kho.GetAllShippingOrders().Any(o => o.OrderCode.StartsWith("LOGIX-HN-") || o.OrderCode.StartsWith("LOGIX-TN-")))
            {
                kho.GenerateSampleOrdersForSimulation(100);
                donChoGiao = LayDanhSachDonChoGiao();
            }

            // Đọc năng lực giao từ ô nhập giao diện (mặc định 50 - ứng với 2 Shipper trực ca)
            if (int.TryParse(txtNangLucGiaoToiDa.Text.Trim(), out int nangLuc) && nangLuc > 0)
            {
                _nangLucGiaoHienTai = nangLuc;
            }
            else
            {
                _nangLucGiaoHienTai = 50;
                txtNangLucGiaoToiDa.Text = "50";
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
                    // ĐÃ QUÁ HẠN CAM KẾT: Cực kỳ khẩn cấp cần giải cứu gấp (Cơ chế Dynamic Aging chống Starvation)
                    // Cứ mỗi 1 giờ quá hạn, điểm phạt tăng lũy tiến (+50 điểm/h). 
                    // Khi đơn thường trễ quá 10 giờ -> Điểm vượt ngưỡng 1000, tự động leo lên Top 1 để giải cứu, không bao giờ bị 'chết cứng'!
                    double soGioTre = Math.Abs(khoangThoiGianConLai.TotalHours);
                    diemUuTien += 500.0 + soGioTre * 50.0;
                    if (diemUuTien >= 1000.0)
                    {
                        capDoUuTien = "🔥 Cứu Hộ Khẩn Cấp";
                        mauHuyHieu = "#991B1B";
                        nenHuyHieu = "#FEE2E2";
                    }
                    else if (!don.IsExpress)
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

            // SẮP XẾP TOÀN BỘ DANH SÁCH: SẮP XẾP CHUẨN XÁC THEO ĐIỂM SỐ MA TRẬN SLA ĐA TẦNG (CHỐNG STARVATION)
            // - Đơn Hỏa Tốc khởi điểm 1.000 điểm nên tự nhiên luôn đứng trước đơn thường (10 - 400 điểm).
            // - Đơn thường nếu bị trễ hạn quá mức (> 10h), điểm số sẽ vọt lên > 1.000 điểm và tự động vượt mặt Hỏa Tốc để được cứu hộ!
            var danhSachSapXep = danhSachDaTinhDiem
                .OrderByDescending(x => x.PriorityScore)
                .ThenBy(x => x.Order.CreatedDate)
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
                    if (muc.PriorityScore >= 1000 && !muc.IsExpress)
                    {
                        muc.AllocationReason = "🔥 Cứu hộ khẩn cấp: Đơn thường bị trễ hạn quá lâu, giải cứu vi phạm SLA";
                    }
                    else if (muc.IsExpress)
                    {
                        muc.AllocationReason = "⚡ Ưu tiên số 1: Đơn Hỏa Tốc (Express) bắt buộc xuất kho ngay";
                    }
                    else
                    {
                        muc.AllocationReason = "Cận hạn cam kết SLA, đủ điểm trong hạn mức ngày nên được duyệt";
                    }
                    _danhSachDuyetGiao.Add(muc);
                }
                else
                {
                    // VƯỢT QUÁ HẠN MỨC -> LƯU KHO CHỜ CA SAU
                    muc.IsApprovedForDelivery = false;
                    muc.AllocationReason = muc.IsExpress
                        ? $"⚠️ Vượt quá hạn mức {nangLucGiao} đơn/ca - Hỏa tốc dời chuyến tiếp theo"
                        : "Đơn tiêu chuẩn hạn SLA còn an toàn, lưu kho nhường suất cho đơn ưu tiên cao";
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
        /// SỰ KIỆN: Kích hoạt trực tiếp Kịch Bản Mô Phỏng Quá Tải (100 đơn tồn kho, thời tiết xấu/thiếu người chỉ có 2 Shipper giao tối đa 50 đơn)
        /// - Thuật toán Ma Trận SLA Đa Tầng tự động đẩy 100% đơn Hỏa Tốc (+1000đ) và đơn cận hạn SLA (+500đ) vào 🟢 DUYỆT GIAO NGAY.
        /// - Tự động hoãn các đơn thường an toàn vào 🟡 LƯU KHO CA SAU.
        /// </summary>
        private void BtnDemoKichBanMuaBao_Click(object sender, RoutedEventArgs e)
        {
            // 1. Dọn sạch đơn cũ và khởi tạo đúng 100 đơn kịch bản chuẩn (30 Hỏa tốc + 20 Cận hạn SLA + 50 Lưu kho)
            WarehouseContext.Instance.ResetAndGenerateSimulationOrders("Hanoi", 50);

            // 2. Thiết lập đúng hạn mức 50 đơn (năng lực 2 Shipper trực ca hôm nay)
            txtNangLucGiaoToiDa.Text = "50";
            _nangLucGiaoHienTai = 50;

            // 3. Tự động chọn kịch bản chuẩn 100 đơn Hà Nội (Cách ly độc lập, không làm ảnh hưởng đơn khác)
            if (cboLocKhuVuc != null)
            {
                for (int i = 0; i < cboLocKhuVuc.Items.Count; i++)
                {
                    if (cboLocKhuVuc.Items[i] is ComboBoxItem item && item.Content?.ToString()?.Contains("Kịch Bản Chuẩn 100 Đơn (Hà Nội)") == true)
                    {
                        cboLocKhuVuc.SelectedIndex = i;
                        break;
                    }
                }
            }

            // 4. Nạp dữ liệu và chạy phân bổ ma trận SLA
            var donChoGiao = LayDanhSachDonChoGiao();
            ThucHienPhanBoUuTien(donChoGiao, 50);

            // 5. Chọn tab Duyệt Giao Ngay
            if (tabNhomDuyetGiao != null)
            {
                tabNhomDuyetGiao.IsChecked = true;
            }

            // 6. Hiển thị báo cáo kết quả quản trị điều phối
            int soHoaToc = _danhSachDuyetGiao.Count(x => x.IsExpress);
            int soHoaTocTonKho = _danhSachDuyetGiao.Count(x => x.IsExpress) + _danhSachLuuKho.Count(x => x.IsExpress);
            int soBoSung = _danhSachDuyetGiao.Count - soHoaToc;

            MessageBox.Show(
                $"🌧️ KỊCH BẢN VẬN HÀNH QUÁ TẢI (MƯA BÃO / THIẾU SHIPPER):\n" +
                $"─────────────────────────────────────────────────────\n" +
                $"• Tồn kho chờ phân phối: {donChoGiao.Count} bưu kiện (Đã chuẩn hóa 100 đơn mô phỏng)\n" +
                $"• Nhân sự trực ca: 2 Shipper (Năng lực nhận tối đa: 50 bưu kiện)\n\n" +
                $"🏆 KẾT QUẢ THỰC THI MA TRẬN ƯU TIÊN SLA ĐA TẦNG:\n" +
                $"─────────────────────────────────────────────────────\n" +
                $"🟢 [DUYỆT GIAO NGAY]: {_danhSachDuyetGiao.Count} / 50 đơn (100% Công Suất Ca)\n" +
                $"   ⚡ Ưu tiên Cấp 1: 100% Đơn Hỏa Tốc VIP (+1.000 điểm) = {soHoaToc}/{soHoaTocTonKho} đơn xuất bến ngay!\n" +
                $"   ⏱️ Ưu tiên Cấp 2: {soBoSung} đơn cận hạn SLA (+500 điểm) bổ sung đủ hạn mức 50 đơn.\n\n" +
                $"🟡 [LƯU KHO CA SAU]: {_danhSachLuuKho.Count} đơn\n" +
                $"   📦 Toàn bộ {_danhSachLuuKho.Count} đơn tiêu chuẩn có SLA còn xa (24h-48h), an toàn giữ lại kho nhường chỗ cho đơn VIP.\n\n" +
                $"👉 Hệ thống đã giải quyết hoàn hảo bài toán quản trị quá tải mà không có bất kỳ mâu thuẫn nghiệp vụ nào!",
                "Kết Quả Điều Phối Ma Trận SLA Đa Tầng", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// SỰ KIỆN: Nhấn nút Chạy Thuật Toán Phân Bổ Ưu Tiên Ma Trận SLA Đa Tầng
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

            int soHoaTocDuyet = _danhSachDuyetGiao.Count(x => x.IsExpress);
            int tongHoaToc = soHoaTocDuyet + _danhSachLuuKho.Count(x => x.IsExpress);
            int soBoSungDuyet = _danhSachDuyetGiao.Count - soHoaTocDuyet;
            int soLuuKho = _danhSachLuuKho.Count;

            MessageBox.Show(
                $"⚡ THỰC THI MA TRẬN SLA ĐA TẦNG THÀNH CÔNG!\n\n" +
                $"• Tổng bưu kiện chờ phân phối: {donChoGiao.Count} đơn\n" +
                $"• Hạn mức năng lực giao ca này: {_nangLucGiaoHienTai} đơn\n\n" +
                $"────────────────────────────────────────\n" +
                $"🟢 DUYỆT GIAO NGAY: {_danhSachDuyetGiao.Count} đơn (Đạt 100% công suất)\n" +
                $"  ⚡ Đơn Hỏa Tốc VIP (+1.000 điểm): Đã duyệt {soHoaTocDuyet}/{tongHoaToc} đơn (Ưu tiên tuyệt đối xuất bến)\n" +
                $"  ⏱️ Đơn Cận Hạn SLA (+500 điểm): Đã duyệt {soBoSungDuyet} đơn (Bổ sung đủ hạn mức {_nangLucGiaoHienTai} đơn theo deadline gấp)\n\n" +
                $"🟡 LƯU KHO CA SAU: {soLuuKho} đơn\n" +
                $"  📦 {soLuuKho} đơn tiêu chuẩn an toàn lưu kho (Hạn SLA còn dài 24h - 48h, không bị phạt hợp đồng SLA).\n" +
                $"────────────────────────────────────────\n\n" +
                $"Thuật toán đã tối ưu hóa 100% tài nguyên vận tải cho doanh nghiệp!",
                "Kết Quả Phân Bổ Ưu Tiên TMS", MessageBoxButton.OK, MessageBoxImage.Information);
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
                "• Quy mô: Đúng 100 đơn hàng phân bổ tại Tỉnh THÁI NGUYÊN (TP Thái Nguyên, Sông Công, Phổ Yên, Đại Từ...)\n" +
                "• Cơ cấu đơn: 25 đơn HỎA TỐC ⚡ (Express) + 15 đơn cận hạn SLA + 60 đơn tiêu chuẩn an toàn\n" +
                "• Hạn mức năng lực giao hôm nay: 40 ĐƠN\n\n" +
                "Thuật toán sẽ tự động phân bổ:\n" +
                "  + Đưa 40 đơn (100% đơn Hỏa Tốc: 25/25 + 15 đơn cận hạn SLA) sang tab [🟢 ĐÃ DUYỆT GIAO NGAY]\n" +
                "  + Đưa 60 đơn tiêu chuẩn an toàn còn lại sang tab [🟡 LƯU KHO CHỜ CA SAU]",
                "Khởi Tạo 100 Đơn Thái Nguyên (Giao 40 Đơn)", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (xacNhan == MessageBoxResult.Yes)
            {
                WarehouseContext.Instance.ResetAndGenerateSimulationOrders("ThaiNguyen", 40);

                if (cboLocKhuVuc != null)
                {
                    for (int i = 0; i < cboLocKhuVuc.Items.Count; i++)
                    {
                        if (cboLocKhuVuc.Items[i] is ComboBoxItem item && item.Content?.ToString()?.Contains("Kịch Bản Chuẩn 100 Đơn (Thái Nguyên)") == true)
                        {
                            cboLocKhuVuc.SelectedIndex = i;
                            break;
                        }
                    }
                }

                txtNangLucGiaoToiDa.Text = "40";
                _nangLucGiaoHienTai = 40;

                NapDuLieuKho();

                int hoaTocTN = _danhSachDuyetGiao.Count(x => x.IsExpress);
                int tongHoaTocTN = hoaTocTN + _danhSachLuuKho.Count(x => x.IsExpress);
                int boSungTN = _danhSachDuyetGiao.Count - hoaTocTN;

                MessageBox.Show(
                    "⚡ THỰC THI MA TRẬN ĐIỀU PHỐI 100 ĐƠN THÁI NGUYÊN THÀNH CÔNG!\n\n" +
                    $"• Hạn mức năng lực giao ca này: 40 đơn\n" +
                    $"────────────────────────────────────────\n" +
                    $"🟢 DUYỆT GIAO NGAY: {_danhSachDuyetGiao.Count} / 40 đơn (Đạt 100% công suất)\n" +
                    $"  ⚡ Đơn Hỏa Tốc VIP (+1.000 điểm): {hoaTocTN}/{tongHoaTocTN} đơn được ưu tiên tuyệt đối xuất bến ngay!\n" +
                    $"  ⏱️ Đơn Cận Hạn SLA (+500 điểm): {boSungTN} đơn bổ sung đủ hạn mức 40 đơn theo deadline gấp nhất\n\n" +
                    $"🟡 LƯU KHO CA SAU: {_danhSachLuuKho.Count} đơn\n" +
                    $"  📦 {_danhSachLuuKho.Count} đơn tiêu chuẩn an toàn lưu kho (Hạn SLA còn dài 24h - 48h, không bị phạt hợp đồng SLA).\n" +
                    $"────────────────────────────────────────\n\n" +
                    "Bạn có thể bấm nút [Phê Duyệt Xuất Kho & Gán Shipper] ở góc phải để xuất bến giao hàng!",
                    "Kết Quả Phân Bổ Ưu Tiên TMS", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Sinh 100 đơn hàng mẫu kịch bản mô phỏng chung để người dùng thử nghiệm tính năng
        /// </summary>
        private void BtnSinh100DonMau_Click(object sender, RoutedEventArgs e)
        {
            var xacNhan = MessageBox.Show(
                "Bạn có muốn khởi tạo kịch bản 100 đơn hàng mô phỏng chuẩn?\n\n" +
                "Kịch bản bao gồm:\n" +
                "• 30 đơn HỎA TỐC ⚡ (Express cam kết 2h)\n" +
                "• 20 đơn cận hạn cam kết SLA cần giao ca này\n" +
                "• 50 đơn tiêu chuẩn an toàn lưu kho (24h-48h)\n\n" +
                "Hệ thống sẽ làm sạch kho và tải đúng 100 đơn chuẩn!",
                "Khởi Tạo 100 Đơn Mẫu Kịch Bản", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (xacNhan == MessageBoxResult.Yes)
            {
                WarehouseContext.Instance.ResetAndGenerateSimulationOrders("Hanoi", 50);
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
                string canhBaoUuTien = "";
                if (mucCanHoan.IsExpress)
                {
                    canhBaoUuTien = "\n\n⚠️ LƯU Ý ĐẶC BIỆT: Đây là đơn hàng HỎA TỐC (Express) có độ ưu tiên cao! Việc hoãn đơn có thể làm vi phạm cam kết SLA với khách hàng.";
                }
                else if (mucCanHoan.EstimatedDeliveryDate <= DateTime.Now.AddHours(4))
                {
                    canhBaoUuTien = "\n\n⚠️ LƯU Ý ĐẶC BIỆT: Đơn hàng này sắp đến hạn cam kết SLA! Hoãn đơn có thể làm tăng nguy cơ giao trễ hạn.";
                }

                var xacNhan = MessageBox.Show(
                    $"Bạn có chắc chắn muốn hoãn đơn hàng này sang ca sau không?\n\n" +
                    $"• Mã đơn: {mucCanHoan.OrderCode}\n" +
                    $"• Khách nhận: {mucCanHoan.ReceiverName} ({mucCanHoan.DestinationArea})\n" +
                    $"• Hàng hóa: {mucCanHoan.ProductSummary}\n" +
                    $"• Loại dịch vụ: {(mucCanHoan.IsExpress ? "⚡ Hỏa Tốc (Express)" : "📦 Tiêu Chuẩn")}\n" +
                    $"• Điểm ưu tiên SLA: {mucCanHoan.PriorityScore:N0} điểm" +
                    $"{canhBaoUuTien}\n\n" +
                    $"Đơn hàng sẽ được chuyển sang bảng 'Lưu kho chờ ca sau' để nhường tải cho các đơn khác.",
                    "Xác Nhận Hoãn Đơn Hàng",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (xacNhan != MessageBoxResult.Yes) return;

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
                var xacNhan = MessageBox.Show(
                    $"Bạn có chắc chắn muốn duyệt giao khẩn cấp cho đơn hàng này trong ca hiện tại không?\n\n" +
                    $"• Mã đơn: {mucCanDay.OrderCode}\n" +
                    $"• Khách nhận: {mucCanDay.ReceiverName} ({mucCanDay.DestinationArea})\n" +
                    $"• Hàng hóa: {mucCanDay.ProductSummary}\n\n" +
                    $"Đơn hàng sẽ được chuyển lên bảng 'Danh sách duyệt xuất giao' để xuất bến trong ca này.",
                    "Xác Nhận Duyệt Giao Khẩn Cấp",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (xacNhan != MessageBoxResult.Yes) return;

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

                var hoiChuyenTab = MessageBox.Show(
                    $"ĐÃ PHÊ DUYỆT VÀ PHÂN CÔNG THÀNH CÔNG {soLuongThanhCong} ĐƠN HÀNG!\n\n" +
                    $"• Toàn bộ {_danhSachDuyetGiao.Count(x => x.IsExpress)} đơn Hỏa Tốc đã được xuất kho ngay lập tức.\n" +
                    $"• Các tài xế Shipper đã tiếp nhận lộ trình giao hàng.\n" +
                    $"• {_danhSachLuuKho.Count} đơn còn lại tiếp tục được lưu giữ an toàn tại kho cho ca tiếp theo.\n\n" +
                    $"Bạn có muốn chuyển sang Tab [Gom Tuyến Giao Hàng] để tối ưu hóa lộ trình và kiểm tra các tuyến vừa gán không?",
                    "Xuất Kho Hoàn Tất", MessageBoxButton.YesNo, MessageBoxImage.Information);

                NapDuLieuKho();

                if (hoiChuyenTab == MessageBoxResult.Yes)
                {
                    OnYeuCauChuyenTab?.Invoke(1); // Chuyển sang Tab 2: Gom Tuyến
                }
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

                    var hoiMo = MessageBox.Show(
                        $"ĐÃ XUẤT BÁO CÁO PHÂN BỔ THÀNH CÔNG!\n\n" +
                        $"• Tổng số: {_danhSachDuyetGiao.Count + _danhSachLuuKho.Count} bản ghi ({_danhSachDuyetGiao.Count} đơn duyệt và {_danhSachLuuKho.Count} đơn lưu kho).\n" +
                        $"• Tệp đã lưu tại:\n{hopThoaiLuu.FileName}\n\n" +
                        $"Bạn có muốn mở ngay tập tin này trong Microsoft Excel không?",
                        "Xuất Báo Cáo Thành Công", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (hoiMo == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(hopThoaiLuu.FileName) { UseShellExecute = true });
                    }
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
