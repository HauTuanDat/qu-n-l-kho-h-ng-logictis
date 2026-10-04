using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// Interaction logic for OrderManagementView.xaml
    /// Phân hệ Quản Lý Đơn Hàng Vận Chuyển, Tính Cước, Điều Phối Giao Nhận & Đối Soát COD
    /// - Nhiệm vụ: Kiểm soát toàn bộ vòng đời đơn hàng (Mới nhận, Chờ xử lý, Đang giao, Đã giao, Thất bại, Hoàn trả),
    ///             tự động tính cước & chính sách Freeship, phân công tài xế Shipper và xuất báo cáo Excel đối soát COD.
    /// </summary>
    public partial class OrderManagementView : UserControl
    {
        // =========================================================================
        // TRƯỜNG DỮ LIỆU BỘ NHỚ ĐỆM ĐƠN HÀNG
        // Nhiệm vụ: Giữ danh sách đơn hàng nạp từ CSDL để phục vụ tìm kiếm & lọc tức thời trên giao diện
        // =========================================================================
        private List<ShippingOrder> _danhSachTatCaDonHang = new();

        // Biến lưu trữ ID của đơn hàng đang được mở hộp thoại phân công tài xế
        private int _idDonHangDuocChonDieuPhoi = 0;

        /// <summary>
        /// Hạn mức tiền hàng để được áp dụng chính sách Miễn phí vận chuyển (Freeship)
        /// Giá trị: 500.000 VNĐ
        /// </summary>
        public const decimal HAN_MUC_FREESHIP = 500000m;

        public OrderManagementView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ danh sách đơn hàng từ CSDL
        /// - Nhiệm vụ: Truy vấn bảng ShippingOrders từ SQL Server quanlykho và kích hoạt bộ lọc hiển thị.
        /// - Tương tác dữ liệu: WarehouseContext.GetAllShippingOrders(), FilterOrders().
        /// </summary>
        public void LoadData() => NapDuLieuDonHang();

        public void NapDuLieuDonHang()
        {
            _danhSachTatCaDonHang = WarehouseContext.Instance.GetAllShippingOrders().ToList();
            LocDanhSachDonHang();
        }

        #region Bộ Lọc Trạng Thái & Tìm Kiếm Theo Thời Gian Thực
        private void TabFilter_Checked(object sender, RoutedEventArgs e)
        {
            LocDanhSachDonHang();
        }

        private void TxtSearchKeyword_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtSearchKeyword.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
            LocDanhSachDonHang();
        }

        private void CbServiceTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LocDanhSachDonHang();
        }

        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            txtSearchKeyword.Text = string.Empty;
            cbServiceTypeFilter.SelectedIndex = 0;
            tabAll.IsChecked = true;
            LocDanhSachDonHang();
        }

        /// <summary>
        /// TIỆN ÍCH CÔNG KHAI: Tìm kiếm hoặc lọc trực tiếp đơn hàng theo mã vận đơn hoặc từ khóa
        /// </summary>
        public void TimKiemDonHang(string tuKhoa)
        {
            if (tabAll != null) tabAll.IsChecked = true;
            if (cbServiceTypeFilter != null) cbServiceTypeFilter.SelectedIndex = 0;
            if (txtSearchKeyword != null)
            {
                txtSearchKeyword.Text = tuKhoa ?? "";
            }
            else
            {
                LocDanhSachDonHang();
            }
        }

        /// <summary>
        /// HÀM LOGIC: Lọc đơn hàng kết hợp nhiều điều kiện (Tab trạng thái + Dropdown dịch vụ + Từ khóa tìm kiếm)
        /// - Nhiệm vụ: Sàng lọc tập dữ liệu đơn hàng và cập nhật trực tiếp lên DataGrid dgShippingOrders.
        /// - Cách hoạt động:
        ///   1. Lọc theo Tab trạng thái: Mới tiếp nhận, Chờ xử lý, Hỏa tốc, Đang giao, Đã giao, Thất bại, Hoàn trả.
        ///   2. Lọc theo Loại hình dịch vụ: Tất cả, Chỉ Express hỏa tốc, Chỉ Tiêu chuẩn.
        ///   3. Tìm kiếm toàn văn: Mã đơn, Tên người nhận, SĐT nhận, Địa chỉ nhận, Tên người gửi, Tóm tắt hàng hóa.
        /// - Tương tác dữ liệu: _danhSachTatCaDonHang, dgShippingOrders, txtResultCount.
        /// </summary>
        public void FilterOrders() => LocDanhSachDonHang();

        public void LocDanhSachDonHang()
        {
            if (dgShippingOrders == null || _danhSachTatCaDonHang == null) return;

            IEnumerable<ShippingOrder> truyVan = _danhSachTatCaDonHang;

            // 1. Lọc theo Tab trạng thái vòng đời đơn hàng
            if (tabLocalDispatch?.IsChecked == true)
            {
                // Lọc các đơn nội vùng cùng tuyến kho Thái Nguyên cần lấy ra đi gửi luôn trong ca
                truyVan = truyVan.Where(d => d.IsLocalHubDelivery && (d.Status == ShippingOrderStatus.NewReceived || d.Status == ShippingOrderStatus.PendingProcessing || d.Status == ShippingOrderStatus.Delivering));
            }
            else if (tabNew?.IsChecked == true)
            {
                truyVan = truyVan.Where(d => d.Status == ShippingOrderStatus.NewReceived);
            }
            else if (tabPending?.IsChecked == true)
            {
                truyVan = truyVan.Where(d => d.Status == ShippingOrderStatus.PendingProcessing);
            }
            else if (tabExpress?.IsChecked == true)
            {
                truyVan = truyVan.Where(d => d.IsExpress);
            }
            else if (tabDelivering?.IsChecked == true)
            {
                truyVan = truyVan.Where(d => d.Status == ShippingOrderStatus.Delivering);
            }
            else if (tabDelivered?.IsChecked == true)
            {
                truyVan = truyVan.Where(d => d.Status == ShippingOrderStatus.Delivered);
            }
            else if (tabFailed?.IsChecked == true)
            {
                truyVan = truyVan.Where(d => d.Status == ShippingOrderStatus.Failed);
            }
            else if (tabReturned?.IsChecked == true)
            {
                truyVan = truyVan.Where(d => d.Status == ShippingOrderStatus.Returned);
            }

            // 2. Lọc theo Loại hình dịch vụ (Express / Tiêu chuẩn)
            if (cbServiceTypeFilter?.SelectedIndex == 1) // Chỉ Express
            {
                truyVan = truyVan.Where(d => d.IsExpress);
            }
            else if (cbServiceTypeFilter?.SelectedIndex == 2) // Chỉ Tiêu chuẩn
            {
                truyVan = truyVan.Where(d => !d.IsExpress);
            }

            // 3. Lọc theo Từ khóa tìm kiếm
            string tuKhoaTimKiem = txtSearchKeyword?.Text?.Trim().ToLower() ?? string.Empty;
            if (!string.IsNullOrEmpty(tuKhoaTimKiem))
            {
                truyVan = truyVan.Where(d =>
                    d.OrderCode.ToLower().Contains(tuKhoaTimKiem) ||
                    d.ReceiverName.ToLower().Contains(tuKhoaTimKiem) ||
                    d.ReceiverPhone.Contains(tuKhoaTimKiem) ||
                    d.ReceiverAddress.ToLower().Contains(tuKhoaTimKiem) ||
                    d.SenderName.ToLower().Contains(tuKhoaTimKiem) ||
                    d.ProductSummary.ToLower().Contains(tuKhoaTimKiem));
            }

            var danhSachKetQua = truyVan.ToList();
            dgShippingOrders.ItemsSource = danhSachKetQua;
            if (txtResultCount != null)
            {
                txtResultCount.Text = $" Đang hiển thị {danhSachKetQua.Count} đơn hàng";
            }
        }
        #endregion

        #region Tiến Độ & Chuyển Nhanh Trạng Thái Vòng Đời Đơn Hàng
        /// <summary>
        /// SỰ KIỆN: Chuyển nhanh trạng thái đơn hàng (Quick Advance Status)
        /// - Nhiệm vụ: Giúp nhân viên kho chuyển nhanh tiến độ: Mới tiếp nhận -> Chờ xử lý -> Đang giao -> Đã giao.
        /// </summary>
        private void BtnQuickAdvanceStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nutBam && nutBam.DataContext is ShippingOrder donHang)
            {
                switch (donHang.Status)
                {
                    case ShippingOrderStatus.NewReceived:
                        donHang.Status = ShippingOrderStatus.PendingProcessing;
                        break;
                    case ShippingOrderStatus.PendingProcessing:
                        donHang.Status = ShippingOrderStatus.Delivering;
                        donHang.AssignedShipperName = "Nguyễn Văn Tuấn";
                        break;
                    case ShippingOrderStatus.Delivering:
                        donHang.Status = ShippingOrderStatus.Delivered;
                        donHang.DeliveredDate = DateTime.Now;
                        break;
                    case ShippingOrderStatus.Delivered:
                        MessageBox.Show("Đơn hàng này đã hoàn tất giao thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    default:
                        donHang.Status = ShippingOrderStatus.NewReceived;
                        break;
                }

                LocDanhSachDonHang();
            }
        }
        #endregion

        #region Tạo Đơn Hàng Mới & Tự Động Tính Biểu Phí / Freeship
        private void BtnOpenCreateOrder_Click(object sender, RoutedEventArgs e)
        {
            // Reset dữ liệu mẫu theo yêu cầu
            txtNewSenderName.Text = "Shop Thời Trang H&M";
            txtNewSenderPhone.Text = "0944 888 999";
            txtNewReceiverName.Text = string.Empty;
            txtNewReceiverPhone.Text = string.Empty;
            txtNewReceiverAddress.Text = string.Empty;
            txtNewProductSummary.Text = "Quần áo thời trang";
            txtNewWeight.Text = "1.0";
            txtNewCodAmount.Text = "0";
            chkNewIsExpress.IsChecked = false;
            rbReceiverPays.IsChecked = true;

            TinhToanBieuPhiTuDong();

            modalCreateOrder.Visibility = Visibility.Visible;
            txtNewReceiverName.Focus();
        }

        private void BtnCloseCreateModal_Click(object sender, RoutedEventArgs e)
        {
            modalCreateOrder.Visibility = Visibility.Collapsed;
        }

        private void TxtNewFeeInputs_TextChanged(object sender, TextChangedEventArgs e)
        {
            TinhToanBieuPhiTuDong();
        }

        private void FeeOption_Changed(object sender, RoutedEventArgs e)
        {
            TinhToanBieuPhiTuDong();
        }

        /// <summary>
        /// HÀM LOGIC CỐT LÕI: Tự động tính toán biểu phí cước vận chuyển và tổng tiền khách cần thanh toán
        /// - Nhiệm vụ:
        ///   1. Lấy số tiền thu hộ COD từ ô nhập liệu.
        ///   2. Tính phí vận chuyển tiêu chuẩn theo cân nặng (<= 2kg: 20.000đ; thêm mỗi kg + 5.000đ).
        ///   3. Kiểm tra điều kiện chính sách FREESHIP (khi tiền COD >= 500.000đ -> Miễn 100% cước vận chuyển).
        ///   4. Phụ phí hỏa tốc 20.000đ khi tick chọn Express.
        ///   5. Tính tổng tiền khách cần trả: COD + (nếu khách chịu cước thì cộng cước + phụ phí).
        /// - Tương tác dữ liệu: txtFeeCod, txtFeeShipping, txtFeeExpress, txtFeeTotal, borderFreeshipPolicy.
        /// </summary>
        public void RecalculateFees() => TinhToanBieuPhiTuDong();

        public void TinhToanBieuPhiTuDong()
        {
            if (txtFeeCod == null || txtFeeShipping == null || txtFeeExpress == null || txtFeeTotal == null)
                return;

            // 1. Số tiền thu hộ COD
            decimal tienThuHoCod = 0;
            if (txtNewCodAmount != null && !string.IsNullOrWhiteSpace(txtNewCodAmount.Text))
            {
                decimal.TryParse(txtNewCodAmount.Text.Replace(".", "").Replace(",", "").Trim(), out tienThuHoCod);
            }

            // 2. Phí vận chuyển tiêu chuẩn theo cân nặng
            double trongLuongKg = 1.0;
            if (txtNewWeight != null && !string.IsNullOrWhiteSpace(txtNewWeight.Text))
            {
                double.TryParse(txtNewWeight.Text.Replace(",", ".").Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out trongLuongKg);
            }
            decimal cuocVanChuyenTieuChuan = 20000;
            if (trongLuongKg > 2.0)
            {
                int soKgVuot = (int)Math.Ceiling(trongLuongKg - 2.0);
                cuocVanChuyenTieuChuan += soKgVuot * 5000;
            }

            // 3. Kiểm tra điều kiện chính sách FREESHIP (COD >= 500.000đ)
            bool duDieuKienMienPhiVanChuyen = tienThuHoCod >= HAN_MUC_FREESHIP;
            decimal phiVanChuyenThucTe = duDieuKienMienPhiVanChuyen ? 0 : cuocVanChuyenTieuChuan;

            if (borderFreeshipPolicy != null && txtFreeshipPolicyMessage != null && txtFreeshipIcon != null && badgeFreeshipTag != null)
            {
                if (duDieuKienMienPhiVanChuyen)
                {
                    borderFreeshipPolicy.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ECFDF5"));
                    borderFreeshipPolicy.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A7F3D0"));
                    txtFreeshipIcon.Text = "🎉";
                    txtFreeshipPolicyMessage.Text = $"Đơn hàng đủ điều kiện FREESHIP (COD ≥ {HAN_MUC_FREESHIP:N0}đ) - Miễn 100% cước vận chuyển!";
                    txtFreeshipPolicyMessage.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#059669"));
                    badgeFreeshipTag.Visibility = Visibility.Visible;
                }
                else
                {
                    decimal soTienConThieuDeDuocFreeship = HAN_MUC_FREESHIP - tienThuHoCod;
                    borderFreeshipPolicy.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EFF6FF"));
                    borderFreeshipPolicy.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BFDBFE"));
                    txtFreeshipIcon.Text = "💡";
                    txtFreeshipPolicyMessage.Text = $"Chính sách Freeship: Mua thêm {soTienConThieuDeDuocFreeship:N0}đ để được miễn phí vận chuyển (Đơn từ 500k)";
                    txtFreeshipPolicyMessage.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1D4ED8"));
                    badgeFreeshipTag.Visibility = Visibility.Collapsed;
                }
            }

            // 4. Phụ phí dịch vụ Hỏa tốc (Express)
            bool laGiaoHoaToc = chkNewIsExpress?.IsChecked == true;
            decimal phuPhiHoaToc = laGiaoHoaToc ? 20000 : 0;

            // 5. Xác định người chi trả cước phí
            bool khachNhanTraCuoc = rbReceiverPays?.IsChecked != false;

            // 6. Tổng số tiền khách nhận cần thanh toán tại thời điểm giao hàng
            decimal tongTienKhachCanThanhToan = tienThuHoCod + (khachNhanTraCuoc ? (phiVanChuyenThucTe + phuPhiHoaToc) : 0);

            // Cập nhật giá trị hiển thị lên giao diện
            txtFeeCod.Text = $"{tienThuHoCod:N0}đ";
            txtFeeShipping.Text = duDieuKienMienPhiVanChuyen ? "0đ" : $"{phiVanChuyenThucTe:N0}đ";
            txtFeeExpress.Text = $"{phuPhiHoaToc:N0}đ";
            txtFeeTotal.Text = $"{tongTienKhachCanThanhToan:N0}đ";

            if (txtFeePayerNote != null)
            {
                if (khachNhanTraCuoc)
                {
                    if (duDieuKienMienPhiVanChuyen)
                    {
                        txtFeePayerNote.Text = laGiaoHoaToc 
                            ? "(Đã áp dụng FREESHIP 0đ cước tiêu chuẩn + Phụ phí hỏa tốc 20.000đ)" 
                            : "(Đã áp dụng FREESHIP 0đ cước vận chuyển - Khách chỉ trả tiền COD)";
                    }
                    else
                    {
                        txtFeePayerNote.Text = laGiaoHoaToc 
                            ? "(Bao gồm COD + Phí vận chuyển + Phụ phí hỏa tốc)" 
                            : "(Bao gồm COD + Phí vận chuyển)";
                    }
                }
                else
                {
                    txtFeePayerNote.Text = "(Người gửi trả cước phí - Khách nhận chỉ trả tiền COD)";
                }
            }
        }

        /// <summary>
        /// SỰ KIỆN: Lưu đơn hàng vận chuyển mới vào cơ sở dữ liệu
        /// - Nhiệm vụ: Xác thực thông tin, tạo mã đơn hàng, lưu vào SQL Server và cập nhật lịch sử.
        /// - Tương tác dữ liệu: ShippingOrder.cs, WarehouseContext.cs.
        /// </summary>
        private void BtnSaveNewOrder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewReceiverName.Text) || 
                string.IsNullOrWhiteSpace(txtNewReceiverPhone.Text) ||
                string.IsNullOrWhiteSpace(txtNewReceiverAddress.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Tên người nhận, SĐT và Địa chỉ giao hàng!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double.TryParse(txtNewWeight.Text.Replace(",", ".").Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double trongLuongKg);
            decimal.TryParse(txtNewCodAmount.Text.Replace(".", "").Replace(",", "").Trim(), out decimal tienThuHoCod);
            bool laGiaoHoaToc = chkNewIsExpress.IsChecked == true;
            bool khachNhanTraCuoc = rbReceiverPays.IsChecked == true;

            decimal cuocVanChuyenTieuChuan = 20000;
            if (trongLuongKg > 2.0)
            {
                int soKgVuot = (int)Math.Ceiling(trongLuongKg - 2.0);
                cuocVanChuyenTieuChuan += soKgVuot * 5000;
            }
            bool duDieuKienMienPhi = tienThuHoCod >= HAN_MUC_FREESHIP;
            decimal phiVanChuyenThucTe = duDieuKienMienPhi ? 0 : cuocVanChuyenTieuChuan;
            decimal phuPhiHoaToc = laGiaoHoaToc ? 20000 : 0;

            var donHangMoi = new ShippingOrder
            {
                SenderName = txtNewSenderName.Text.Trim(),
                SenderPhone = txtNewSenderPhone.Text.Trim(),
                SenderAddress = "Kho Cầu Giấy, Hà Nội",
                ReceiverName = txtNewReceiverName.Text.Trim(),
                ReceiverPhone = txtNewReceiverPhone.Text.Trim(),
                ReceiverAddress = txtNewReceiverAddress.Text.Trim(),
                DestinationArea = "Hà Nội",
                ProductSummary = txtNewProductSummary.Text.Trim(),
                Weight = trongLuongKg > 0 ? trongLuongKg : 1.0,
                CodAmount = tienThuHoCod,
                IsExpress = laGiaoHoaToc,
                ShippingFee = phiVanChuyenThucTe,
                ExpressSurcharge = phuPhiHoaToc,
                ReceiverPaysFee = khachNhanTraCuoc,
                Status = ShippingOrderStatus.NewReceived,
                CreatedDate = DateTime.Now,
                EstimatedDeliveryDate = laGiaoHoaToc ? DateTime.Now.AddHours(4) : DateTime.Now.AddHours(24)
            };

            WarehouseContext.Instance.AddShippingOrder(donHangMoi);

            modalCreateOrder.Visibility = Visibility.Collapsed;
            NapDuLieuDonHang();

            string thongBaoMienPhiVanChuyen = duDieuKienMienPhi ? " (🎉 Đạt chính sách FREESHIP 0đ cước)" : "";
            MessageBox.Show($"Tạo thành công đơn hàng [{donHangMoi.OrderCode}]!\n" +
                            $"• Dịch vụ: {(laGiaoHoaToc ? "⚡ EXPRESS HỎA TỐC" : "📦 TIÊU CHUẨN")}\n" +
                            $"• Tiền thu hộ COD: {tienThuHoCod:N0}đ\n" +
                            $"• Phí vận chuyển: {phiVanChuyenThucTe:N0}đ{thongBaoMienPhiVanChuyen}\n" +
                            $"• Phụ phí hỏa tốc: {phuPhiHoaToc:N0}đ\n" +
                            $"• Tổng tiền khách thanh toán: {donHangMoi.TotalCustomerPayment:N0}đ",
                "Phát Hành Đơn Hàng Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region Xuất Báo Cáo Đối Soát COD (Excel / CSV Chuẩn UTF-8 BOM)
        /// <summary>
        /// SỰ KIỆN: Xuất danh sách đơn hàng và số liệu đối soát COD ra tệp Excel/CSV
        /// - Nhiệm vụ: Tạo tệp CSV chuẩn UTF-8 có BOM (Byte Order Mark) để Microsoft Excel hiển thị đúng 100% tiếng Việt có dấu.
        /// - Cách hoạt động:
        ///   1. Lấy danh sách các đơn hàng hiện có trên DataGrid.
        ///   2. Dùng StringBuilder nối từng dòng dữ liệu theo chuẩn định dạng CSV.
        ///   3. Lưu tệp trực tiếp ra thư mục Desktop của máy tính với timestamp độc nhất.
        ///   4. Tính tổng tiền COD và tổng tiền thu hộ để thông báo cho người dùng.
        /// - Tương tác dữ liệu: dgShippingOrders, System.IO.File, Desktop folder.
        /// </summary>
        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var danhSachDonHangXuat = dgShippingOrders.ItemsSource as IEnumerable<ShippingOrder> ?? _danhSachTatCaDonHang;
                var danhSachDon = danhSachDonHangXuat.ToList();

                if (danhSachDon.Count == 0)
                {
                    MessageBox.Show("Không có dữ liệu đơn hàng để xuất báo cáo!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var chuoiNoiDungCsv = new System.Text.StringBuilder();
                // Dòng tiêu đề cột tiếng Việt
                chuoiNoiDungCsv.AppendLine("Mã Đơn,Người Gửi,SĐT Gửi,Địa Chỉ Gửi,Người Nhận,SĐT Nhận,Địa Chỉ Nhận,Khu Vực,Hàng Hóa,Trọng Lượng (kg),Loại Dịch Vụ,Tiền Thu Hộ COD (VNĐ),Phí Vận Chuyển (VNĐ),Phụ Phí Hỏa Tốc (VNĐ),Tổng Tiền Khách Trả (VNĐ),Người Trả Cước,Trạng Thái,Shipper Giao,SĐT Shipper,Ngày Tạo,Hạn Giao Dự Kiến,Ghi Chú");

                foreach (var don in danhSachDon)
                {
                    chuoiNoiDungCsv.AppendLine($"\"{don.OrderCode}\",\"{don.SenderName}\",\"{don.SenderPhone}\",\"{don.SenderAddress}\",\"{don.ReceiverName}\",\"{don.ReceiverPhone}\",\"{don.ReceiverAddress}\",\"{don.DestinationArea}\",\"{don.ProductSummary}\",{don.Weight},\"{(don.IsExpress ? "Express Hỏa Tốc" : "Tiêu Chuẩn")}\",{don.CodAmount},{don.ShippingFee},{don.ExpressSurcharge},{don.TotalCustomerPayment},\"{(don.ReceiverPaysFee ? "Khách Nhận Trả" : "Shop Trả Freeship")}\",\"{don.StatusDisplayName}\",\"{don.AssignedShipperName}\",\"{don.ShipperPhone}\",\"{don.CreatedDate:yyyy-MM-dd HH:mm}\",\"{don.EstimatedDeliveryDate:yyyy-MM-dd HH:mm}\",\"{don.Notes}\"");
                }

                string duongDanDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string tenTepTinBaoCao = $"BaoCao_DonHang_Logistics_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string duongDanTepTinHoanChinh = System.IO.Path.Combine(duongDanDesktop, tenTepTinBaoCao);

                // Ghi tệp với Encoding UTF-8 BOM để Excel hiển thị tiếng Việt chính xác tuyệt đối
                System.IO.File.WriteAllText(duongDanTepTinHoanChinh, chuoiNoiDungCsv.ToString(), new System.Text.UTF8Encoding(true));

                decimal tongTienThuHoCod = danhSachDon.Sum(x => x.CodAmount);
                decimal tongTienThanhToan = danhSachDon.Sum(x => x.TotalCustomerPayment);

                MessageBox.Show($"XUẤT BÁO CÁO THÀNH CÔNG!\n\n" +
                                $"• Tổng số đơn hàng: {danhSachDon.Count} đơn\n" +
                                $"• Tổng tiền thu hộ COD: {tongTienThuHoCod:N0} đ\n" +
                                $"• Tổng tiền cần thanh toán: {tongTienThanhToan:N0} đ\n\n" +
                                $"File Excel/CSV đã được lưu tại Desktop của bạn:\n{duongDanTepTinHoanChinh}",
                    "Báo Cáo Đối Soát COD", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Điều Phối Nhân Viên Giao Hàng (Shipper Dispatch)
        /// <summary>
        /// SỰ KIỆN: Mở hộp thoại (Modal) điều phối Shipper cho đơn hàng
        /// - Nhiệm vụ: Nạp danh sách tài xế giao hàng đang hoạt động từ bảng Shippers CSDL và hiển thị Modal.
        /// - Tương tác dữ liệu: WarehouseContext.GetAllShippers(), cbShipperList, modalAssignShipper.
        /// </summary>
        private void BtnOpenAssignShipper_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nutBam && nutBam.DataContext is ShippingOrder donHang)
            {
                _idDonHangDuocChonDieuPhoi = donHang.Id;
                txtAssignModalTargetOrder.Text = $"Đơn hàng: {donHang.OrderCode} | Người nhận: {donHang.ReceiverName} ({donHang.DestinationArea})";

                var danhSachTaiXe = WarehouseContext.Instance.GetAllShippers();
                cbShipperList.Items.Clear();
                foreach (var taiXe in danhSachTaiXe)
                {
                    cbShipperList.Items.Add(new ComboBoxItem
                    {
                        Content = $"🛵 {taiXe.FullName} | SĐT: {taiXe.Phone} | Biển: {taiXe.VehiclePlate} | Khu vực: {taiXe.DeliveryArea} (Đã giao {taiXe.CompletedTodayCount} đơn)",
                        Tag = taiXe
                    });
                }

                if (cbShipperList.Items.Count > 0)
                {
                    cbShipperList.SelectedIndex = 0;
                }

                modalAssignShipper.Visibility = Visibility.Visible;
            }
        }

        private void BtnCloseAssignModal_Click(object sender, RoutedEventArgs e)
        {
            modalAssignShipper.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// SỰ KIỆN: Xác nhận phân công Shipper giao đơn hàng
        /// - Nhiệm vụ: Gán tên và SĐT Shipper vào đơn hàng, tự động chuyển trạng thái đơn sang 'Đang Giao',
        ///             lưu thay đổi vào CSDL SQL Server và cập nhật dòng thời gian hoạt động gần đây.
        /// - Tương tác dữ liệu: WarehouseContext.AssignShipperToOrder, ShippingOrder.cs, SQL Server.
        /// </summary>
        private void BtnConfirmAssignShipper_Click(object sender, RoutedEventArgs e)
        {
            if (cbShipperList.SelectedItem is ComboBoxItem mucDuocChon && mucDuocChon.Tag is Shipper taiXe)
            {
                WarehouseContext.Instance.AssignShipperToOrder(_idDonHangDuocChonDieuPhoi, taiXe.Id, taiXe.FullName, taiXe.Phone);
                modalAssignShipper.Visibility = Visibility.Collapsed;
                NapDuLieuDonHang();

                MessageBox.Show($"Đã phân công Shipper [{taiXe.FullName}] phụ trách giao đơn hàng thành công!\nTrạng thái đơn hàng tự động chuyển sang 'Đang Giao'.",
                    "Điều Phối Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region Điều Hướng Tích Hợp (Tính Năng 5 & Tính Năng 7)
        /// <summary>
        /// SỰ KIỆN: Bấm nút "Phân Bổ SLA"
        /// - Nhiệm vụ: Chuyển hướng trực tiếp sang phân hệ Điều Phối & Phân Bổ Ưu Tiên Năng Lực (Hỏa Tốc & SLA).
        /// </summary>
        private void BtnOpenPriorityDispatch_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow cuaSoChinh)
            {
                cuaSoChinh.ChuyenSangTrangDieuPhoiUuTien();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Bấm nút "Gom Tuyến (TN5)"
        /// - Nhiệm vụ: Chuyển hướng trực tiếp sang phân hệ Gom Đơn Theo Tuyến Đường & Lộ Trình (Tính năng 5).
        /// </summary>
        private void BtnOpenRouteBatching_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow cuaSoChinh)
            {
                cuaSoChinh.ChuyenSangTrangGomTuyen();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Bấm nút "Bến Nhập Kho (2 Kiểu)"
        /// - Nhiệm vụ: Chuyển hướng trực tiếp sang phân hệ Tiếp Nhận &amp; Nhập Kho (Xe Tải &amp; Khách Lẻ).
        /// </summary>
        private void BtnOpenInboundReceiving_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow cuaSoChinh)
            {
                cuaSoChinh.ChuyenSangTrangNhapKho();
            }
        }

        #region Điều Hướng Tab Chính: Đơn Hàng vs Tra Cứu Hành Trình
        private void MainTab_Checked(object sender, RoutedEventArgs e)
        {
            if (panelDanhSachDonHang == null || panelTraCuuVanDon == null) return;

            if (tabDanhSachDonHang.IsChecked == true)
            {
                panelDanhSachDonHang.Visibility = Visibility.Visible;
                panelTraCuuVanDon.Visibility = Visibility.Collapsed;
            }
            else if (tabTraCuuHanhTrinh.IsChecked == true)
            {
                panelDanhSachDonHang.Visibility = Visibility.Collapsed;
                panelTraCuuVanDon.Visibility = Visibility.Visible;
            }
        }

        public void ChuyenSangTabTraCuu(string? maVanDon = null)
        {
            if (tabTraCuuHanhTrinh != null)
            {
                tabTraCuuHanhTrinh.IsChecked = true;
            }
            if (!string.IsNullOrEmpty(maVanDon) && ucTrackingPortal != null)
            {
                ucTrackingPortal.ThucHienTraCuu(maVanDon);
            }
        }

        public void ChuyenSangTabDanhSach()
        {
            if (tabDanhSachDonHang != null)
            {
                tabDanhSachDonHang.IsChecked = true;
            }
        }
        #endregion

        /// <summary>
        /// SỰ KIỆN: Bấm nút "🔍 Tra Cứu" trên từng dòng đơn hàng
        /// - Nhiệm vụ: Chuyển hướng trực tiếp sang Tab Cổng Tra Cứu Vận Đơn và mở chi tiết hành trình của đơn này.
        /// </summary>
        private void BtnQuickTrackOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nutBam && nutBam.DataContext is ShippingOrder donHang)
            {
                ChuyenSangTabTraCuu(donHang.OrderCode);
            }
        }
        #endregion

        #region Nghiệp Vụ Giao Ngay Nội Vùng (Last-Mile Fast Dispatch)
        /// <summary>
        /// SỰ KIỆN: Bấm nút "⚡ Giao Luôn" trên từng đơn hàng nội vùng (Thái Nguyên)
        /// - Nghiệp vụ: Đơn có địa chỉ nhận cùng tuyến kho Thái Nguyên -> Xuất kho bàn giao ngay cho Shipper đi phát trong ca.
        /// </summary>
        private void BtnQuickLocalDeliver_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nutBam && nutBam.DataContext is ShippingOrder donHang)
            {
                // 1. Tìm kiếm Shipper phụ trách địa bàn Thái Nguyên
                var danhSachShipper = WarehouseContext.Instance.GetAllShippers().ToList();
                var shipperPhuTrach = danhSachShipper.FirstOrDefault(s => (s.DeliveryArea.Contains("Thái Nguyên") || s.CurrentArea.Contains("Thái Nguyên")) && s.Status == ShipperStatus.Available)
                                   ?? danhSachShipper.FirstOrDefault(s => s.DeliveryArea.Contains("Thái Nguyên") || s.CurrentArea.Contains("Thái Nguyên"))
                                   ?? danhSachShipper.FirstOrDefault(s => s.Status == ShipperStatus.Active || s.Status == ShipperStatus.Available)
                                   ?? new Shipper { Id = 5, FullName = "Bùi Văn Đạt", Phone = "0985 667 889" };

                // 2. Cập nhật trạng thái đơn sang Đang Giao
                donHang.Status = ShippingOrderStatus.Delivering;
                donHang.AssignedShipperId = shipperPhuTrach.Id;
                donHang.AssignedShipperName = shipperPhuTrach.FullName;
                donHang.ShipperPhone = shipperPhuTrach.Phone;
                donHang.Notes = (donHang.Notes ?? "") + $" | [GIAO NGAY NỘI VÙNG] Bàn giao Shipper {shipperPhuTrach.FullName} xuất bến {DateTime.Now:HH:mm dd/MM}";

                // 3. Cập nhật CSDL
                WarehouseContext.Instance.UpdateShippingOrder(donHang);

                // 4. Ghi nhận biến động xuất kho chặng cuối (OutboundLastMile)
                WarehouseContext.Instance.AddWarehouseMovement(new WarehouseMovement
                {
                    TransactionCode = $"GD-XK-LM-{DateTime.Now:yyMMddHHmmss}",
                    Timestamp = DateTime.Now,
                    MovementType = WarehouseMovementType.OutboundLastMile,
                    ItemName = $"{donHang.ProductSummary} ({donHang.Weight:N1} kg)",
                    ReferenceCode = donHang.OrderCode,
                    Quantity = 1,
                    Weight = donHang.Weight,
                    SourceOrDestination = $"Hub Thái Nguyên ➔ Shipper: {shipperPhuTrach.FullName} (Giao: {donHang.ReceiverName})",
                    LocationCode = "DOCK-LAST-MILE-01",
                    OperatorName = UserSession.Current.CurrentUser?.FullName ?? "Điều phối viên",
                    Notes = $"Xuất kho giao ngay chặng cuối (Nội vùng Thái Nguyên). Địa chỉ: {donHang.ReceiverAddress}"
                });

                // 5. Ghi nhận RecentActivity
                WarehouseContext.Instance.AddRecentActivity(new RecentActivity
                {
                    Title = $"Xuất giao ngay đơn nội vùng {donHang.OrderCode}",
                    Description = $"Bàn giao cho Shipper {shipperPhuTrach.FullName} đi giao tại {donHang.ReceiverAddress}",
                    Timestamp = DateTime.Now,
                    Type = ActivityType.OrderSuccess
                });

                // 6. Thông báo trực quan và nạp lại dữ liệu
                MessageBox.Show(
                    $"⚡ XUẤT KHO VÀ ĐI GIAO NGAY THÀNH CÔNG!\n\n" +
                    $"• Mã vận đơn: {donHang.OrderCode}\n" +
                    $"• Người nhận: {donHang.ReceiverName}\n" +
                    $"• Địa chỉ giao: {donHang.ReceiverAddress}\n" +
                    $"• Shipper bàn giao: {shipperPhuTrach.FullName} ({shipperPhuTrach.Phone})\n\n" +
                    $"Hàng đã được xuất khỏi kho và bàn giao cho tài xế đi phát luôn cho khách trong ca!",
                    "Xuất Giao Ngay Đơn Nội Vùng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                NapDuLieuDonHang();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Bấm nút "⚡ Xuất Giao Đơn Nội Vùng" (Hàng loạt)
        /// - Nghiệp vụ: Gom toàn bộ đơn nội vùng Thái Nguyên mới tiếp nhận / chờ xử lý để xuất bến giao luôn trong 1 click.
        /// </summary>
        private void BtnBatchDispatchLocalOrders_Click(object sender, RoutedEventArgs e)
        {
            var donNoiVungChoGiao = _danhSachTatCaDonHang
                .Where(d => d.IsLocalHubDelivery && (d.Status == ShippingOrderStatus.NewReceived || d.Status == ShippingOrderStatus.PendingProcessing))
                .ToList();

            if (donNoiVungChoGiao.Count == 0)
            {
                MessageBox.Show(
                    "Hiện tại không có đơn hàng nội vùng Thái Nguyên nào đang ở trạng thái 'Mới tiếp nhận' hoặc 'Chờ xử lý' cần xuất giao!",
                    "Thông Báo Phân Phối",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var hoiXacNhan = MessageBox.Show(
                $"Hệ thống phát hiện {donNoiVungChoGiao.Count} đơn hàng có người nhận tại Thái Nguyên (cùng tuyến kho) đang chờ xuất bến.\n\n" +
                $"Bạn có muốn XUẤT KHO HÀNG LOẠT và BÀN GIAO NGAY cho đội ngũ Shipper Thái Nguyên đi phát luôn trong ca không?",
                "Xác Nhận Xuất Giao Toàn Bộ Đơn Nội Vùng",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (hoiXacNhan != MessageBoxResult.Yes) return;

            var danhSachShipper = WarehouseContext.Instance.GetAllShippers()
                .Where(s => s.DeliveryArea.Contains("Thái Nguyên") || s.CurrentArea.Contains("Thái Nguyên"))
                .ToList();

            if (danhSachShipper.Count == 0)
            {
                danhSachShipper = WarehouseContext.Instance.GetAllShippers().ToList();
            }

            int indexShipper = 0;
            foreach (var don in donNoiVungChoGiao)
            {
                var shipper = danhSachShipper[indexShipper % danhSachShipper.Count];
                indexShipper++;

                don.Status = ShippingOrderStatus.Delivering;
                don.AssignedShipperId = shipper.Id;
                don.AssignedShipperName = shipper.FullName;
                don.ShipperPhone = shipper.Phone;
                don.Notes = (don.Notes ?? "") + $" | [XUẤT HÀNG LOẠT] Bàn giao {shipper.FullName} lúc {DateTime.Now:HH:mm dd/MM}";

                WarehouseContext.Instance.UpdateShippingOrder(don);

                WarehouseContext.Instance.AddWarehouseMovement(new WarehouseMovement
                {
                    TransactionCode = $"GD-XK-LM-{DateTime.Now:yyMMddHHmmss}-{don.Id}",
                    Timestamp = DateTime.Now,
                    MovementType = WarehouseMovementType.OutboundLastMile,
                    ItemName = $"{don.ProductSummary}",
                    ReferenceCode = don.OrderCode,
                    Quantity = 1,
                    Weight = don.Weight,
                    SourceOrDestination = $"Hub Thái Nguyên ➔ Shipper: {shipper.FullName}",
                    LocationCode = "DOCK-LAST-MILE-01",
                    OperatorName = UserSession.Current.CurrentUser?.FullName ?? "Điều phối viên",
                    Notes = $"Gom xuất giao nhanh đơn nội vùng {don.OrderCode}"
                });
            }

            MessageBox.Show(
                $"✅ ĐÃ XUẤT KHO VÀ ĐI GIAO THÀNH CÔNG {donNoiVungChoGiao.Count} ĐƠN HÀNG NỘI VÙNG!\n\n" +
                $"Tất cả các đơn đã được phân bổ đều cho đội ngũ Shipper Thái Nguyên và chuyển sang trạng thái 'Đang Giao'.",
                "Hoàn Tất Xuất Giao Hàng Loạt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            NapDuLieuDonHang();
        }
        #endregion
    }
}
