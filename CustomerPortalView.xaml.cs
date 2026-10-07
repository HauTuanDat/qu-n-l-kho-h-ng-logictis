using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ĐỐI TƯỢNG: Điểm mốc GPS thời gian thực (Waypoint & Turn-by-Turn Instruction)
    /// </summary>
    public class GpsWaypoint
    {
        public Point CanvasCoord { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string RoadName { get; set; } = string.Empty;
        public string TurnInstruction { get; set; } = string.Empty;
        public string TurnIcon { get; set; } = "⬆️";
        public string DistToTurn { get; set; } = "200 m";
        public double SpeedLimitKmH { get; set; } = 40.0;
    }

    /// <summary>
    /// Interaction logic for CustomerPortalView.xaml
    /// Phân hệ dành riêng cho Khách hàng & Chủ Shop đối tác (Logix Smart Logistics)
    /// </summary>
    public partial class CustomerPortalView : UserControl
    {
        // =========================================================================
        // CÁC TRƯỜNG DỮ LIỆU ĐIỀU HÀNH & MÔ PHỎNG GPS
        // =========================================================================
        private readonly WarehouseContext _kho = WarehouseContext.Instance;
        private List<ShippingOrder> _danhSachDonHangShop = new();
        private ShippingOrder? _donHangDangTiepNhan = null;
        private ShippingOrder? _donHangDangTheoDoiGps = null;

        // Bộ hẹn giờ mô phỏng GPS lăn bánh và sóng Radar
        private readonly DispatcherTimer _gpsTimer;
        private readonly DispatcherTimer _radarTimer;
        private int _gpsStepIndex = 0;
        private double _radarScale = 1.0;
        private bool _isVehicleShipperSelected = true;

        // Trạng thái thu phóng & kéo rê bản đồ (Zoom & Pan)
        private double _zoomScale = 1.0;
        private bool _isPanning = false;
        private Point _panStartPoint;

        // Danh sách mốc tọa độ tuyến đường đầy đủ (Turn-by-turn Waypoints)
        private List<GpsWaypoint> _danhSachWaypoint = new();

        public CustomerPortalView()
        {
            InitializeComponent();

            // Khởi tạo danh sách mốc định vị GPS chuẩn đô thị Thái Nguyên
            KhoiTaoDanhSachWaypointThaiNguyen();

            // Khởi tạo DispatcherTimer mô phỏng GPS di chuyển (mặc định 1.5s / bước)
            _gpsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _gpsTimer.Tick += GpsTimer_Tick;

            // Khởi tạo DispatcherTimer mô phỏng sóng Radar quét vệ tinh (mỗi 80ms)
            _radarTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            _radarTimer.Tick += RadarTimer_Tick;
            _radarTimer.Start();

            // Tải dữ liệu ban đầu và cấu hình giao diện theo vai trò (Customer vs Warehouse Staff)
            NapDanhSachShipper();
            CauHinhGiaoDienKhachHang(UserSession.Current.CurrentUser?.Role == UserRole.Customer);
            KhoiTaoGoiYMaDon();
        }

        #region Phân Quyền Vai Trò & Điều Khiển Giao Diện (Customer vs Warehouse Staff)
        public void CauHinhGiaoDienKhachHang(bool laKhachHang)
        {
            var currentUser = UserSession.Current.CurrentUser;

            if (laKhachHang)
            {
                // 1. Khách hàng: Ẩn hoàn toàn nghiệp vụ kho (Tab 2: Quét tiếp nhận & Phân xe)
                tabBtnWarehouseScan.Visibility = Visibility.Collapsed;
                btnMarkDeliveredGps.Visibility = Visibility.Collapsed;

                // 2. Đánh số lại các Tab dành riêng cho khách hàng
                tabBtnCreateOrder.Content = "📝 1. Lên Đơn Giao Hàng";
                tabBtnGpsTracking.Content = "🛰️ 2. Bản Đồ GPS Đơn Của Tôi";
                tabBtnOrderList.Content = "📊 3. Sổ Bưu Gửi & Ví COD";

                // 3. Cập nhật thông tin nhận diện cửa hàng / chủ shop
                txtCustomerBannerTitle.Text = "CỔNG DỊCH VỤ DÀNH CHO KHÁCH HÀNG & CHỦ SHOP";
                txtCustomerHeaderRoleText.Text = "ĐỐI TÁC THƯƠNG MẠI";

                if (currentUser != null)
                {
                    runShopName.Text = currentUser.FullName;
                    runShopPhone.Text = currentUser.PhoneNumber ?? "0978 999 888";

                    // Điền mặc định thông tin người gửi là tên cửa hàng
                    txtSenderName.Text = currentUser.FullName;
                    txtSenderPhone.Text = currentUser.PhoneNumber ?? "0978 999 888";
                    txtSenderName.IsReadOnly = true;
                }
            }
            else
            {
                // Nhân viên điều hành kho / Quản lý / Admin
                tabBtnWarehouseScan.Visibility = Visibility.Visible;
                btnMarkDeliveredGps.Visibility = Visibility.Visible;

                tabBtnCreateOrder.Content = "📝 1. Chủ Shop Lên Đơn";
                tabBtnWarehouseScan.Content = "⚡ 2. Quét Tiếp Nhận Kho & Phân Xe";
                tabBtnGpsTracking.Content = "🛰️ 3. Bản Đồ GPS Định Vị";
                tabBtnOrderList.Content = "📊 4. Sổ Bưu Gửi & Ví COD";

                txtCustomerBannerTitle.Text = "TRUNG TÂM TIẾP NHẬN BƯU KIỆN & PHÂN PHỐI HUB";
                txtCustomerHeaderRoleText.Text = "NHÂN VIÊN ĐIỀU HÀNH KHO";
                txtSenderName.IsReadOnly = false;
            }

            NapDanhSachDonHang();
            NapDanhSachDonDangGiaoGps();
            CapNhatThongKeShop();
        }

        public void ChuyenSangTabTaoDon()
        {
            tabBtnCreateOrder.IsChecked = true;
        }

        public void ChuyenSangTabTiepNhanKho()
        {
            tabBtnWarehouseScan.IsChecked = true;
        }

        public void ChuyenSangTabGps()
        {
            tabBtnGpsTracking.IsChecked = true;
        }

        public void ChuyenSangTabSoDon()
        {
            tabBtnOrderList.IsChecked = true;
        }
        #endregion

        #region Khởi Tạo Lộ Trình & Waypoints Thực Tế
        private void KhoiTaoDanhSachWaypointThaiNguyen()
        {
            _danhSachWaypoint = new List<GpsWaypoint>
            {
                new GpsWaypoint
                {
                    CanvasCoord = new Point(100, 490),
                    Latitude = 21.584200,
                    Longitude = 105.843100,
                    RoadName = "Kho Hub Tuấn Đạt (Phan Đình Phùng)",
                    TurnInstruction = "Xuất phát từ Kho Hub Tuấn Đạt, rẽ phải vào đường Phan Đình Phùng",
                    TurnIcon = "⬆️",
                    DistToTurn = "300 m",
                    SpeedLimitKmH = 35.0
                },
                new GpsWaypoint
                {
                    CanvasCoord = new Point(150, 440),
                    Latitude = 21.586100,
                    Longitude = 105.844500,
                    RoadName = "Phố Phan Đình Phùng (Khu Đền Đô)",
                    TurnInstruction = "Đi thẳng trên phố Phan Đình Phùng hướng về Quảng Trường",
                    TurnIcon = "⬆️",
                    DistToTurn = "400 m",
                    SpeedLimitKmH = 38.0
                },
                new GpsWaypoint
                {
                    CanvasCoord = new Point(210, 380),
                    Latitude = 21.588200,
                    Longitude = 105.846000,
                    RoadName = "Phố Phan Đình Phùng (Bên hông Quảng Trường Võ Nguyên Giáp)",
                    TurnInstruction = "Tiếp tục đi thẳng 400m nữa tới Vòng Xuyến Đảo Tròn",
                    TurnIcon = "⬆️",
                    DistToTurn = "400 m",
                    SpeedLimitKmH = 40.0
                },
                new GpsWaypoint
                {
                    CanvasCoord = new Point(270, 315),
                    Latitude = 21.590100,
                    Longitude = 105.847500,
                    RoadName = "Tiếp cận Vòng Xuyến Đảo Tròn TP Thái Nguyên",
                    TurnInstruction = "Giảm tốc độ, chuẩn bị vào Vòng Xuyến Đảo Tròn",
                    TurnIcon = "🔄",
                    DistToTurn = "150 m",
                    SpeedLimitKmH = 30.0
                },
                new GpsWaypoint
                {
                    CanvasCoord = new Point(320, 260),
                    Latitude = 21.591800,
                    Longitude = 105.848800,
                    RoadName = "Đảo Tròn Trung Tâm TP Thái Nguyên",
                    TurnInstruction = "Tại Vòng Xuyến Đảo Tròn, rẽ phải vào đường Hoàng Văn Thụ",
                    TurnIcon = "↪️",
                    DistToTurn = "200 m",
                    SpeedLimitKmH = 25.0
                },
                new GpsWaypoint
                {
                    CanvasCoord = new Point(410, 200),
                    Latitude = 21.593600,
                    Longitude = 105.850200,
                    RoadName = "Đường Hoàng Văn Thụ (Trước TTTM Vincom Plaza)",
                    TurnInstruction = "Đi thẳng qua ngã tư Vincom Plaza hướng Phường Phan Đình Phùng",
                    TurnIcon = "⬆️",
                    DistToTurn = "350 m",
                    SpeedLimitKmH = 38.0
                },
                new GpsWaypoint
                {
                    CanvasCoord = new Point(500, 165),
                    Latitude = 21.595200,
                    Longitude = 105.851600,
                    RoadName = "Đường Hoàng Văn Thụ (Giao lộ Phố Minh Cầu)",
                    TurnInstruction = "Tiếp tục đi thẳng 300m trên đường Hoàng Văn Thụ",
                    TurnIcon = "⬆️",
                    DistToTurn = "300 m",
                    SpeedLimitKmH = 35.0
                },
                new GpsWaypoint
                {
                    CanvasCoord = new Point(590, 135),
                    Latitude = 21.596800,
                    Longitude = 105.852800,
                    RoadName = "Số 25 Đường Hoàng Văn Thụ",
                    TurnInstruction = "Sau 200m nữa chuẩn bị dừng xe, tiếp cận địa chỉ người nhận",
                    TurnIcon = "⚠️",
                    DistToTurn = "200 m",
                    SpeedLimitKmH = 30.0
                },
                new GpsWaypoint
                {
                    CanvasCoord = new Point(660, 115),
                    Latitude = 21.597800,
                    Longitude = 105.853600,
                    RoadName = "Số 38 Hoàng Văn Thụ (VÀO VÙNG GEOFENCE 300M)",
                    TurnInstruction = "Giảm tốc độ, chuẩn bị dừng trước số 45 Hoàng Văn Thụ",
                    TurnIcon = "🛑",
                    DistToTurn = "60 m",
                    SpeedLimitKmH = 18.0
                },
                new GpsWaypoint
                {
                    CanvasCoord = new Point(720, 100),
                    Latitude = 21.598500,
                    Longitude = 105.854300,
                    RoadName = "Đích đến: Số 45 Đường Hoàng Văn Thụ (Hoàng Thu Hà)",
                    TurnInstruction = "ĐÃ ĐẾN NƠI: Số 45 Hoàng Văn Thụ - Khách Hoàng Thu Hà",
                    TurnIcon = "🎯",
                    DistToTurn = "0 m",
                    SpeedLimitKmH = 0.0
                }
            };
        }
        #endregion

        #region Điều Hướng Tab Giao Diện
        private void TabButton_Checked(object sender, RoutedEventArgs e)
        {
            if (gridTabCreateOrder == null || gridTabWarehouseScan == null ||
                gridTabGpsTracking == null || gridTabOrderList == null) return;

            // Ẩn tất cả tab
            gridTabCreateOrder.Visibility = Visibility.Collapsed;
            gridTabWarehouseScan.Visibility = Visibility.Collapsed;
            gridTabGpsTracking.Visibility = Visibility.Collapsed;
            gridTabOrderList.Visibility = Visibility.Collapsed;

            // Bật tab được chọn
            if (tabBtnCreateOrder.IsChecked == true)
            {
                gridTabCreateOrder.Visibility = Visibility.Visible;
            }
            else if (tabBtnWarehouseScan.IsChecked == true)
            {
                gridTabWarehouseScan.Visibility = Visibility.Visible;
                txtScanOrderCode.Focus();
            }
            else if (tabBtnGpsTracking.IsChecked == true)
            {
                gridTabGpsTracking.Visibility = Visibility.Visible;
                NapDanhSachDonDangGiaoGps();
            }
            else if (tabBtnOrderList.IsChecked == true)
            {
                gridTabOrderList.Visibility = Visibility.Visible;
                NapDanhSachDonHang();
            }
        }
        #endregion

        #region TAB 1: Chủ Shop Lên Đơn Online
        private void BtnSampleLocal_Click(object sender, RoutedEventArgs e)
        {
            txtReceiverName.Text = "Hoàng Thu Hà";
            txtReceiverPhone.Text = "0915 222 333";
            txtReceiverAddress.Text = "Số 45 Đường Hoàng Văn Thụ, P. Phan Đình Phùng, TP Thái Nguyên";
            cboDestinationArea.SelectedIndex = 0; // Thái Nguyên
            chkIsExpress.IsChecked = false;
            txtProductSummary.Text = "Hạt SmartHeart Gold vị cá hồi cho chó Poodle (Túi 2kg)";
            txtWeight.Text = "2.0";
            txtCodAmount.Text = "350000";
            cboPayer.SelectedIndex = 0;
            CapNhatDuBaoPhanLuong();
        }

        private void BtnSampleInterProvincial_Click(object sender, RoutedEventArgs e)
        {
            txtReceiverName.Text = "Trần Quốc Bảo";
            txtReceiverPhone.Text = "0982 777 888";
            txtReceiverAddress.Text = "Số 88 Đường Cầu Giấy, P. Dịch Vọng, Quận Cầu Giấy, Hà Nội";
            cboDestinationArea.SelectedIndex = 1; // Hà Nội
            chkIsExpress.IsChecked = true;
            txtProductSummary.Text = "Pate tươi mèo Royal Canin Mother & Babycat (Lốc 12 lon)";
            txtWeight.Text = "1.5";
            txtCodAmount.Text = "480000";
            cboPayer.SelectedIndex = 0;
            CapNhatDuBaoPhanLuong();
        }

        private void TxtReceiverAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            CapNhatDuBaoPhanLuong();
        }

        private void CboDestinationArea_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CapNhatDuBaoPhanLuong();
        }

        private void ChkIsExpress_Changed(object sender, RoutedEventArgs e)
        {
            CapNhatDuBaoPhanLuong();
        }

        private void CapNhatDuBaoPhanLuong()
        {
            if (borderRoutePrediction == null || txtPredictTag == null || txtPredictDesc == null ||
                txtPredictVehicle == null || txtPredictSla == null) return;

            string diaChi = (txtReceiverAddress?.Text ?? string.Empty).ToLowerInvariant();
            string khuVuc = (cboDestinationArea?.SelectedItem is ComboBoxItem item ? item.Content.ToString() : string.Empty) ?? string.Empty;

            bool laNoiVungThaiNguyen = khuVuc.Contains("Thái Nguyên", StringComparison.OrdinalIgnoreCase) ||
                                       diaChi.Contains("thái nguyên") ||
                                       diaChi.Contains("phan đình phùng") ||
                                       diaChi.Contains("học liệu") ||
                                       diaChi.Contains("hoàng văn thụ") ||
                                       diaChi.Contains("lương ngọc quyến") ||
                                       diaChi.Contains("thịnh đán") ||
                                       diaChi.Contains("sông công") ||
                                       diaChi.Contains("phổ yên");

            if (laNoiVungThaiNguyen)
            {
                borderRoutePrediction.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                borderRoutePrediction.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                txtPredictTag.Text = "🛵 TUYẾN NỘI VÙNG (GIAO NGAY TRONG NGÀY)";
                txtPredictTag.Foreground = new SolidColorBrush(Color.FromRgb(4, 120, 87));
                txtPredictDesc.Text = "Người nhận cùng địa bàn với kho Hub Tuấn Đạt (Thái Nguyên). Khi chủ Shop bàn giao tới kho, thủ kho sẽ xuất ngay cho Shipper xe máy đi phát trong ca, KHÔNG lưu kho bãi!";
                txtPredictVehicle.Text = "🛵 Xe máy Shipper (Nội thành)";
                txtPredictVehicle.Foreground = new SolidColorBrush(Color.FromRgb(4, 120, 87));
                txtPredictSla.Text = chkIsExpress.IsChecked == true ? "⚡ Hỏa tốc 2H" : "Giao trong ngày (2H - 4H)";
            }
            else
            {
                borderRoutePrediction.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                borderRoutePrediction.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                txtPredictTag.Text = "🚛 TUYẾN LIÊN TỈNH (XUẤT XE TẢI TRUNG CHUYỂN)";
                txtPredictTag.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
                txtPredictDesc.Text = $"Người nhận tại {khuVuc} (Ngoài tỉnh Thái Nguyên). Sau khi tiếp nhận tại kho, bưu gửi sẽ được gom chuyến và chuyển lên xe tải trung chuyển (Linehaul Transit) về kho đích.";
                txtPredictVehicle.Text = "🚛 Xe tải trung chuyển 29C-889.12";
                txtPredictVehicle.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
                txtPredictSla.Text = chkIsExpress.IsChecked == true ? "⚡ Chuyển phát nhanh 12H - 24H" : "Tiêu chuẩn 24H - 48H";
            }
        }

        private void BtnCreateOrderSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtReceiverName.Text))
            {
                MessageBox.Show("Vui lòng nhập họ tên người nhận!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtReceiverName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtReceiverPhone.Text))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại người nhận!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtReceiverPhone.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtReceiverAddress.Text))
            {
                MessageBox.Show("Vui lòng nhập địa chỉ giao hàng cụ thể!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtReceiverAddress.Focus();
                return;
            }

            int soThuTu = _kho.GetAllShippingOrders().Count + 1;
            string maVanDonMoi = $"LOGIX-SHOP-{DateTime.Now:yyMMdd}-{soThuTu:D3}";

            decimal.TryParse(txtCodAmount.Text.Replace(".", "").Replace(",", ""), out decimal tienCod);
            double.TryParse(txtWeight.Text, out double khoiLuong);
            if (khoiLuong <= 0) khoiLuong = 1.0;

            string tenKhuVuc = (cboDestinationArea.SelectedItem is ComboBoxItem item ? item.Content.ToString() : "Thái Nguyên") ?? "Thái Nguyên";
            bool isExpress = chkIsExpress.IsChecked == true;

            var donHangMoi = new ShippingOrder
            {
                OrderCode = maVanDonMoi,
                SenderName = txtSenderName.Text.Trim(),
                SenderPhone = txtSenderPhone.Text.Trim(),
                SenderAddress = txtSenderAddress.Text.Trim(),
                ReceiverName = txtReceiverName.Text.Trim(),
                ReceiverPhone = txtReceiverPhone.Text.Trim(),
                ReceiverAddress = txtReceiverAddress.Text.Trim(),
                DestinationArea = tenKhuVuc,
                ProductSummary = txtProductSummary.Text.Trim(),
                Weight = khoiLuong,
                IsExpress = isExpress,
                CodAmount = tienCod,
                ShippingFee = isExpress ? 35000 : 20000,
                ExpressSurcharge = isExpress ? 15000 : 0,
                ReceiverPaysFee = cboPayer.SelectedIndex == 0,
                Status = ShippingOrderStatus.NewReceived,
                CreatedDate = DateTime.Now,
                EstimatedDeliveryDate = isExpress ? DateTime.Now.AddHours(4) : DateTime.Now.AddHours(24),
                Notes = $"Đơn tạo trực tuyến bởi {txtSenderName.Text.Trim()}",
                CreatedBySource = "Shop Online",
                SenderShopName = txtSenderName.Text.Trim(),
                IsWarehouseCheckedIn = false,
                GpsSpeedKmH = 0,
                GpsDistanceKm = 3.5,
                GpsEtaMinutes = isExpress ? 30 : 120,
                GpsStatusDescription = "Bưu kiện mới khởi tạo trực tuyến, chờ mang tới kho Hub Tuấn Đạt"
            };

            _kho.AddShippingOrder(donHangMoi);
            ThemChipGoiYMaDon(maVanDonMoi);
            NapDanhSachDonHang();
            CapNhatThongKeShop();

            // Hiển thị Thẻ Tiếp Nhận Kỹ Thuật Số & Mã QR (Khách hàng chỉ cần bấm OK, không cần in phiếu)
            txtSuccessOrderCode.Text = maVanDonMoi;
            txtSuccessReceiver.Text = $"{donHangMoi.ReceiverName} • {donHangMoi.ReceiverPhone}";
            txtSuccessReceiverAddress.Text = donHangMoi.ReceiverAddress;
            txtSuccessProduct.Text = $"{donHangMoi.ProductSummary} ({donHangMoi.Weight:F1} kg)";
            txtSuccessCod.Text = $"{donHangMoi.CodAmount:N0} đ";
            txtSuccessRouting.Text = donHangMoi.RoutingCategoryName;

            try
            {
                imgSuccessQrCode.Source = BarcodeHelper.TaoAnhQRCode(donHangMoi.OrderCode, 200);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QR Error] {ex.Message}");
            }

            txtScanOrderCode.Text = maVanDonMoi;
            modalOrderCreatedSuccess.Visibility = Visibility.Visible;
        }
        #endregion

        #region TAB 2: Quét Tiếp Nhận Tại Kho & Phân Xe
        private void TxtScanOrderCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ThucHienKiemTraMaDon(txtScanOrderCode.Text.Trim());
            }
        }

        private void BtnScanSearch_Click(object sender, RoutedEventArgs e)
        {
            ThucHienKiemTraMaDon(txtScanOrderCode.Text.Trim());
        }

        public void ThucHienKiemTraMaDon(string maDonHang)
        {
            if (string.IsNullOrWhiteSpace(maDonHang))
            {
                MessageBox.Show("Vui lòng nhập hoặc quét mã đơn hàng!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            txtScanOrderCode.Text = maDonHang;

            var donHang = _kho.GetAllShippingOrders().FirstOrDefault(d => d.OrderCode.Equals(maDonHang, StringComparison.OrdinalIgnoreCase)) ??
                          _kho.TimKiemDonHangTheoMaHoacSoDienThoai(maDonHang);

            if (donHang == null)
            {
                _donHangDangTiepNhan = null;

                borderStep1Result.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                borderStep1Result.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                txtStep1Icon.Text = "❌";
                txtStep1Title.Text = "BƯỚC 1: MÃ ĐƠN CHƯA ĐƯỢC TẠO HOẶC KHÔNG TỒN TẠI!";
                txtStep1Title.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                txtStep1Detail.Text = $"Cảnh báo: Bưu gửi mang mã '{maDonHang}' chưa được khởi tạo trên cổng đối tác của Logix. Thủ kho KHÔNG ĐƯỢC PHÉP tiếp nhận trước khi khách/shop lên đơn!";

                borderStep2Result.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                borderStep2Result.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                txtStep2Title.Text = "BƯỚC 2: CHỜ ĐƠN HỢP LỆ";
                txtStep2Title.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                txtStep2Detail.Text = "Chưa có thông tin địa chỉ người nhận.";

                txtCardOrderCode.Text = "-- Chưa có mã --";
                txtCardShopName.Text = "-- Không xác định --";
                txtCardReceiverInfo.Text = "--";
                txtCardCodAmount.Text = "0 đ";
                txtCardReceiverAddress.Text = "Vui lòng kiểm tra lại tem dán trên bưu kiện.";
                txtOrderCurrentStatus.Text = "KHÔNG TỒN TẠI";
                txtOrderCurrentStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));

                btnConfirmWarehouseCheckIn.IsEnabled = false;
                btnPrintLabelFromScan.IsEnabled = false;
                return;
            }

            _donHangDangTiepNhan = donHang;
            btnConfirmWarehouseCheckIn.IsEnabled = true;
            btnPrintLabelFromScan.IsEnabled = true;

            borderStep1Result.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
            borderStep1Result.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            txtStep1Icon.Text = "✅";
            txtStep1Title.Text = "BƯỚC 1: MÃ ĐƠN HỢP LỆ - ĐÃ KHỞI TẠO TRỰC TUYẾN";
            txtStep1Title.Foreground = new SolidColorBrush(Color.FromRgb(4, 120, 87));
            txtStep1Detail.Text = $"Mã bưu gửi '{donHang.OrderCode}' đã được khởi tạo bởi: {donHang.SenderName} (SĐT: {donHang.SenderPhone}) vào lúc {donHang.CreatedDate:HH:mm dd/MM/yyyy}.";

            if (donHang.IsLocalHubDelivery)
            {
                borderStep2Result.Background = new SolidColorBrush(Color.FromRgb(236, 253, 245));
                borderStep2Result.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                txtStep2Icon.Text = "🟢";
                txtStep2Title.Text = "BƯỚC 2: ĐỊA CHỈ THUỘC NỘI VÙNG KHO HUB THÁI NGUYÊN";
                txtStep2Title.Foreground = new SolidColorBrush(Color.FromRgb(4, 120, 87));
                txtStep2Detail.Text = $"Địa chỉ nhận: {donHang.ReceiverAddress} -> Cùng địa bàn với kho Tuấn Đạt. Phân luồng: XUẤT CHO SHIPPER XE MÁY ĐI GIAO NGAY TRONG NGÀY!";
                ChonCheDoPhuongTien(laShipperXeMay: true);
            }
            else
            {
                borderStep2Result.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                borderStep2Result.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                txtStep2Icon.Text = "🟠";
                txtStep2Title.Text = $"BƯỚC 2: ĐỊA CHỈ NGOẠI TỈNH ({donHang.DestinationArea.ToUpper()})";
                txtStep2Title.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
                txtStep2Detail.Text = $"Địa chỉ nhận: {donHang.ReceiverAddress} -> Ngoài địa bàn kho Thái Nguyên. Phân luồng: XUẤT CHO XE TẢI TRUNG CHUYỂN LINEHAUL ĐI KHO ĐÍCH!";
                ChonCheDoPhuongTien(laShipperXeMay: false);
            }

            txtCardOrderCode.Text = donHang.OrderCode;
            txtCardShopName.Text = $"{donHang.SenderName} ({donHang.SenderPhone})";
            txtCardReceiverInfo.Text = $"{donHang.ReceiverName} • {donHang.ReceiverPhone}";
            txtCardCodAmount.Text = $"{donHang.CodAmount:N0} đ";
            txtCardReceiverAddress.Text = donHang.ReceiverAddress;

            if (donHang.IsWarehouseCheckedIn)
            {
                txtOrderCurrentStatus.Text = $"ĐÃ NHẬP KHO ({donHang.StatusDisplayName})";
                txtOrderCurrentStatus.Foreground = new SolidColorBrush(Color.FromRgb(5, 150, 105));
            }
            else
            {
                txtOrderCurrentStatus.Text = "CHỜ QUÉT TIẾP NHẬN";
                txtOrderCurrentStatus.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            }
        }

        private void BorderVehicleShipper_MouseDown(object sender, MouseButtonEventArgs e) => ChonCheDoPhuongTien(true);
        private void BorderVehicleTruck_MouseDown(object sender, MouseButtonEventArgs e) => ChonCheDoPhuongTien(false);

        private void ChonCheDoPhuongTien(bool laShipperXeMay)
        {
            _isVehicleShipperSelected = laShipperXeMay;
            if (laShipperXeMay)
            {
                borderVehicleShipper.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                borderVehicleShipper.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                borderVehicleShipper.BorderThickness = new Thickness(2);

                borderVehicleTruck.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                borderVehicleTruck.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                borderVehicleTruck.BorderThickness = new Thickness(1);
            }
            else
            {
                borderVehicleTruck.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
                borderVehicleTruck.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                borderVehicleTruck.BorderThickness = new Thickness(2);

                borderVehicleShipper.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                borderVehicleShipper.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                borderVehicleShipper.BorderThickness = new Thickness(1);
            }
        }

        private void BtnConfirmWarehouseCheckIn_Click(object sender, RoutedEventArgs e)
        {
            if (_donHangDangTiepNhan == null)
            {
                MessageBox.Show("Vui lòng quét và kiểm tra mã đơn hợp lệ trước khi tiếp nhận!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var donHang = _donHangDangTiepNhan;
            string tenNhanVienKho = UserSession.Current.FullName;

            donHang.IsWarehouseCheckedIn = true;
            donHang.WarehouseCheckedInTime = DateTime.Now;
            donHang.WarehouseStaffReceived = tenNhanVienKho;
            donHang.Status = ShippingOrderStatus.Delivering;

            if (_isVehicleShipperSelected)
            {
                var shipperDuocChon = cboShipperSelect.SelectedItem as Shipper;
                string tenShipper = shipperDuocChon?.FullName ?? "Bùi Văn Đạt";
                string sdtShipper = shipperDuocChon?.Phone ?? "0988 123 456";

                donHang.WarehouseAssignedVehicle = $"🛵 Xe máy Shipper ({tenShipper} - {sdtShipper})";
                donHang.AssignedShipperName = tenShipper;
                donHang.ShipperPhone = sdtShipper;
                donHang.GpsSpeedKmH = 38.0;
                donHang.GpsDistanceKm = 3.5;
                donHang.GpsEtaMinutes = 15;
                donHang.GpsStatusDescription = $"Shipper {tenShipper} đã nhận hàng từ kho Hub, đang di chuyển trên đường phát.";
            }
            else
            {
                string thongTinXeTai = (cboTruckSelect.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "🚛 Xe tải 29C-889.12 (Nguyễn Văn Bắc)";
                donHang.WarehouseAssignedVehicle = thongTinXeTai;
                donHang.AssignedShipperName = "Đội xe trung chuyển liên tỉnh";
                donHang.ShipperPhone = "024 3829 1111";
                donHang.GpsSpeedKmH = 55.0;
                donHang.GpsDistanceKm = 65.0;
                donHang.GpsEtaMinutes = 90;
                donHang.GpsStatusDescription = "Bưu kiện đã xếp lên xe tải trung chuyển, đang xuất bến tuyến cao tốc.";
            }

            _kho.AddWarehouseMovement(new WarehouseMovement
            {
                MovementType = _isVehicleShipperSelected ? WarehouseMovementType.OutboundLastMile : WarehouseMovementType.OutboundTransit,
                ItemName = $"Bưu gửi {donHang.OrderCode} ({donHang.ProductSummary})",
                Weight = donHang.Weight,
                SourceOrDestination = donHang.ReceiverAddress,
                OperatorName = tenNhanVienKho,
                Notes = $"Tiếp nhận từ Shop {donHang.SenderName} và xuất bến phân xe: {donHang.WarehouseAssignedVehicle}"
            });

            _kho.UpdateShippingOrder(donHang);
            NapDanhSachDonHang();
            CapNhatThongKeShop();

            MessageBox.Show(
                $"✅ TIẾP NHẬN BƯU KIỆN & PHÂN XE THÀNH CÔNG!\n\n" +
                $"• Mã vận đơn: {donHang.OrderCode}\n" +
                $"• Người tiếp nhận: {tenNhanVienKho}\n" +
                $"• Phương tiện phân bổ: {donHang.WarehouseAssignedVehicle}\n" +
                $"• Trạng thái mới: ĐANG GIAO HÀNG (DELIVERING)\n\n" +
                "Hệ thống định vị GPS vệ tinh thời gian thực đã được kích hoạt!",
                "Tiếp Nhận & Xuất Bến Thành Công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            tabBtnGpsTracking.IsChecked = true;
            KichHoatTheoDoiGpsChoDonHang(donHang);
        }

        private void BtnPrintLabelFromScan_Click(object sender, RoutedEventArgs e)
        {
            if (_donHangDangTiepNhan != null) MoModalInTemNhiet(_donHangDangTiepNhan);
        }

        private void BtnJumpToGps_Click(object sender, RoutedEventArgs e)
        {
            tabBtnGpsTracking.IsChecked = true;
            if (_donHangDangTiepNhan != null) KichHoatTheoDoiGpsChoDonHang(_donHangDangTiepNhan);
        }
        #endregion

        #region TAB 3: Bản Đồ GPS Định Vị Thời Gian Thực & Next-Gen Navigation
        private void NapDanhSachDonDangGiaoGps()
        {
            var currentUser = UserSession.Current.CurrentUser;
            var query = _kho.GetAllShippingOrders()
                .Where(d => d.Status == ShippingOrderStatus.Delivering || d.IsWarehouseCheckedIn);

            if (currentUser != null && currentUser.Role == UserRole.Customer)
            {
                // Khách hàng CHỈ theo dõi đơn hàng của chính cửa hàng mình
                query = query.Where(d => d.CreatedBySource == "Shop Online" ||
                                         (!string.IsNullOrEmpty(d.SenderShopName) && d.SenderShopName.Contains("Shop", StringComparison.OrdinalIgnoreCase)) ||
                                         (!string.IsNullOrEmpty(d.SenderName) && currentUser.FullName != null && d.SenderName.Equals(currentUser.FullName, StringComparison.OrdinalIgnoreCase)));
            }

            var danhSachDelivering = query
                .OrderByDescending(d => d.CreatedDate)
                .ToList();

            lbDeliveringOrders.ItemsSource = danhSachDelivering;
            if (danhSachDelivering.Any())
            {
                lbDeliveringOrders.SelectedIndex = 0;
            }
        }

        private void LbDeliveringOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbDeliveringOrders.SelectedItem is ShippingOrder donHang)
            {
                KichHoatTheoDoiGpsChoDonHang(donHang);
            }
        }

        private void TxtSearchDeliveringGps_TextChanged(object sender, TextChangedEventArgs e)
        {
            string tuKhoa = (txtSearchDeliveringGps.Text ?? string.Empty).Trim().ToLowerInvariant();
            var currentUser = UserSession.Current.CurrentUser;

            var query = _kho.GetAllShippingOrders()
                .Where(d => d.Status == ShippingOrderStatus.Delivering || d.IsWarehouseCheckedIn);

            if (currentUser != null && currentUser.Role == UserRole.Customer)
            {
                query = query.Where(d => d.CreatedBySource == "Shop Online" ||
                                         (!string.IsNullOrEmpty(d.SenderShopName) && d.SenderShopName.Contains("Shop", StringComparison.OrdinalIgnoreCase)) ||
                                         (!string.IsNullOrEmpty(d.SenderName) && currentUser.FullName != null && d.SenderName.Equals(currentUser.FullName, StringComparison.OrdinalIgnoreCase)));
            }

            var danhSach = query
                .Where(d => string.IsNullOrEmpty(tuKhoa) ||
                            d.OrderCode.ToLowerInvariant().Contains(tuKhoa) ||
                            d.ReceiverName.ToLowerInvariant().Contains(tuKhoa) ||
                            d.ReceiverAddress.ToLowerInvariant().Contains(tuKhoa))
                .ToList();

            lbDeliveringOrders.ItemsSource = danhSach;
        }

        public void KichHoatTheoDoiGpsChoDonHang(ShippingOrder donHang)
        {
            _donHangDangTheoDoiGps = donHang;
            _gpsStepIndex = 0;

            // Cập nhật nhãn điểm đến
            txtMapDestLabel.Text = $"📍 {donHang.ReceiverName} ({donHang.ReceiverPhone})";

            // Biểu tượng phương tiện
            bool laXeMay = donHang.WarehouseAssignedVehicle?.Contains("Xe máy", StringComparison.OrdinalIgnoreCase) == true ||
                           donHang.IsLocalHubDelivery;
            txtVehicleIconOnMap.Text = laXeMay ? "🛵" : "🚛";

            // Cập nhật HUD Telemetry
            txtHudSpeed.Text = $"{donHang.GpsSpeedKmH:F0} km/h";
            txtHudDistance.Text = $"{donHang.GpsDistanceKm:F1} km";
            txtHudEta.Text = $"{donHang.GpsEtaMinutes} phút";
            txtHudDriver.Text = !string.IsNullOrWhiteSpace(donHang.AssignedShipperName) ? donHang.AssignedShipperName : "Bùi Văn Đạt";
            txtHudCod.Text = $"{donHang.CodAmount:N0} đ";

            // Đặt lại slider timeline
            sldGpsTimeline.Value = 0;
            txtTimelineTime.Text = "00:00 / 12:00";

            // Cập nhật điểm ban đầu
            CapNhatViTriGpsTheoBuoc(0);

            // Bắt đầu mô phỏng
            _gpsTimer.Start();
        }

        private void GpsTimer_Tick(object? sender, EventArgs e)
        {
            if (_donHangDangTheoDoiGps == null || _danhSachWaypoint.Count == 0) return;

            if (_gpsStepIndex < _danhSachWaypoint.Count - 1)
            {
                _gpsStepIndex++;
                CapNhatViTriGpsTheoBuoc(_gpsStepIndex);

                // Đồng bộ giá trị lên Slider tua lại
                double phanTram = ((double)_gpsStepIndex / (_danhSachWaypoint.Count - 1)) * 100.0;
                sldGpsTimeline.Value = phanTram;
            }
            else
            {
                // Đã đến nơi
                _gpsTimer.Stop();
                txtHudSpeed.Text = "0 km/h";
                txtHudDistance.Text = "0.0 km";
                txtHudEta.Text = "0 phút (ĐÃ TỚI NƠI)";
                txtVehicleBubble.Text = "GPS: ĐÃ TỚI ĐIỂM GIAO!";
                txtNavTurnIcon.Text = "🎯";
                txtNavInstruction.Text = $"ĐÃ TỚI ĐỊA CHỈ: {_donHangDangTheoDoiGps.ReceiverAddress}";
                txtNavCurrentRoad.Text = "Phương tiện đang dừng chờ trước cửa nhà khách hàng";
                txtLiveGpsDescription.Text = $"🎉 Phương tiện đã đến địa chỉ của khách hàng: {_donHangDangTheoDoiGps.ReceiverName}. Vui lòng bấm 'Báo Giao Thành Công'.";

                // Kích hoạt Geofence Alert
                borderGeofenceAlert.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                borderGeofenceAlert.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                txtGeofenceAlert.Text = "🔔 [GEOFENCE TRỰC TIẾP] ĐÃ GỬI SMS TỰ ĐỘNG CHO KHÁCH: 'Shipper đã có mặt, quý khách vui lòng ra nhận!'";
            }
        }

        /// <summary>
        /// HÀM LOGIC CẬP NHẬT TỌA ĐỘ, HUD VÀ CHỈ DẪN NGÃ RẼ THEO BƯỚC ĐIỂM MỐC
        /// </summary>
        private void CapNhatViTriGpsTheoBuoc(int buocIndex)
        {
            if (buocIndex < 0 || buocIndex >= _danhSachWaypoint.Count) return;

            var waypoint = _danhSachWaypoint[buocIndex];
            _gpsStepIndex = buocIndex;

            // Di chuyển Marker trên Canvas (Căn giữa tâm xe 34x34)
            Canvas.SetLeft(markerVehicle, waypoint.CanvasCoord.X - 17);
            Canvas.SetTop(markerVehicle, waypoint.CanvasCoord.Y - 17);

            // Xoay phương tiện theo góc hướng di chuyển
            if (buocIndex < _danhSachWaypoint.Count - 1)
            {
                var nextWp = _danhSachWaypoint[buocIndex + 1];
                double dx = nextWp.CanvasCoord.X - waypoint.CanvasCoord.X;
                double dy = nextWp.CanvasCoord.Y - waypoint.CanvasCoord.Y;
                double gocXoay = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                vehicleRotateTransform.Angle = gocXoay;
            }

            // Cập nhật đường Breadcrumbs đã đi qua
            CapNhatDuongBreadcrumb(buocIndex);

            // Cập nhật bảng chỉ dẫn Turn-by-Turn
            txtNavTurnIcon.Text = waypoint.TurnIcon;
            txtNavInstruction.Text = waypoint.TurnInstruction;
            txtNavCurrentRoad.Text = $"Đang lưu thông trên: {waypoint.RoadName} • Tốc độ giới hạn {waypoint.SpeedLimitKmH:F0} km/h";

            // Tính toán khoảng cách & ETA còn lại
            double tiLeHoanThanh = (double)buocIndex / (_danhSachWaypoint.Count - 1);
            double quangDuongConLai = Math.Max(0.0, 3.5 * (1.0 - tiLeHoanThanh));
            int etaPhutConLai = (int)Math.Max(0, 12 * (1.0 - tiLeHoanThanh));

            var rd = new Random();
            int tocDo = buocIndex == _danhSachWaypoint.Count - 1 ? 0 : rd.Next(35, 43);

            txtHudSpeed.Text = $"{tocDo} km/h";
            txtHudDistance.Text = $"{quangDuongConLai:F1} km";
            txtHudEta.Text = $"{etaPhutConLai} phút";
            txtVehicleBubble.Text = $"GPS: Đang di chuyển • {tocDo}km/h";

            int elapsedMinutes = (int)(12 * tiLeHoanThanh);
            txtTimelineTime.Text = $"{elapsedMinutes:D2}:00 / 12:00";

            txtGpsCoordinates.Text = $"LAT: {waypoint.Latitude:F6}° N • LONG: {waypoint.Longitude:F6}° E";
            txtLiveGpsDescription.Text = $"{_donHangDangTheoDoiGps?.AssignedShipperName ?? "Shipper"} đang chuyển phát đơn hàng #{_donHangDangTheoDoiGps?.OrderCode} qua trục đường: {waypoint.RoadName}";

            // Kiểm tra hàng rào Geofence (< 0.5 km)
            if (quangDuongConLai <= 0.4 && quangDuongConLai > 0)
            {
                borderGeofenceAlert.Background = new SolidColorBrush(Color.FromRgb(154, 52, 18)); // Cam cảnh báo
                borderGeofenceAlert.BorderBrush = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                txtGeofenceAlert.Text = "🔔 [GEOFENCE 300M] Đã gửi tin nhắn hẹn trước cho khách hàng ra cổng nhận hàng!";
                geofenceCircle.Opacity = 0.35;
            }
            else if (quangDuongConLai <= 0)
            {
                borderGeofenceAlert.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                borderGeofenceAlert.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                txtGeofenceAlert.Text = "🔔 [GEOFENCE TRỰC TIẾP] Shipper đã dừng trước cửa, chờ khách ký nhận COD.";
                geofenceCircle.Opacity = 0.5;
            }
            else
            {
                borderGeofenceAlert.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                borderGeofenceAlert.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                txtGeofenceAlert.Text = $"GEOFENCE: Cách điểm nhận {quangDuongConLai:F1} km (Chưa vào vùng hẹn trước)";
                geofenceCircle.Opacity = 0.12;
            }
        }

        private void CapNhatDuongBreadcrumb(int buocIndex)
        {
            if (buocIndex == 0)
            {
                pathGpsTraveled.Data = Geometry.Parse($"M{_danhSachWaypoint[0].CanvasCoord.X},{_danhSachWaypoint[0].CanvasCoord.Y}");
                return;
            }

            var streamGeom = new StreamGeometry();
            using (var ctx = streamGeom.Open())
            {
                ctx.BeginFigure(_danhSachWaypoint[0].CanvasCoord, false, false);
                for (int i = 1; i <= buocIndex; i++)
                {
                    ctx.LineTo(_danhSachWaypoint[i].CanvasCoord, true, true);
                }
            }
            pathGpsTraveled.Data = streamGeom;
        }

        private void RadarTimer_Tick(object? sender, EventArgs e)
        {
            _radarScale += 0.06;
            if (_radarScale > 2.0)
            {
                _radarScale = 1.0;
            }

            radarRing1.Width = 40 * _radarScale;
            radarRing1.Height = 40 * _radarScale;
            Canvas.SetLeft(radarRing1, -(radarRing1.Width - 34) / 2);
            Canvas.SetTop(radarRing1, -(radarRing1.Height - 34) / 2);
            radarRing1.Opacity = Math.Max(0, 1.0 - (_radarScale - 1.0));

            radarRing2.Width = 60 * _radarScale;
            radarRing2.Height = 60 * _radarScale;
            Canvas.SetLeft(radarRing2, -(radarRing2.Width - 34) / 2);
            Canvas.SetTop(radarRing2, -(radarRing2.Height - 34) / 2);
            radarRing2.Opacity = Math.Max(0, 0.6 - (_radarScale - 1.0) * 0.5);
        }

        #region Thu Phóng & Kéo Rê Bản Đồ (Zoom & Pan)
        private void CanvasGpsMap_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.15 : 0.85;
            _zoomScale = Math.Clamp(_zoomScale * zoomFactor, 0.7, 2.5);
            mapScaleTransform.ScaleX = _zoomScale;
            mapScaleTransform.ScaleY = _zoomScale;
            e.Handled = true;
        }

        private void CanvasGpsMap_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(borderMapViewport);
                borderMapViewport.CaptureMouse();
            }
        }

        private void CanvasGpsMap_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPos = e.GetPosition(borderMapViewport);
                Vector delta = currentPos - _panStartPoint;
                mapTranslateTransform.X += delta.X;
                mapTranslateTransform.Y += delta.Y;
                _panStartPoint = currentPos;
            }
        }

        private void CanvasGpsMap_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            borderMapViewport.ReleaseMouseCapture();
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoomScale = Math.Min(2.5, _zoomScale + 0.2);
            mapScaleTransform.ScaleX = _zoomScale;
            mapScaleTransform.ScaleY = _zoomScale;
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoomScale = Math.Max(0.7, _zoomScale - 0.2);
            mapScaleTransform.ScaleX = _zoomScale;
            mapScaleTransform.ScaleY = _zoomScale;
        }

        private void BtnCenterVehicle_Click(object sender, RoutedEventArgs e)
        {
            double vehicleX = Canvas.GetLeft(markerVehicle) + 17;
            double vehicleY = Canvas.GetTop(markerVehicle) + 17;

            double viewWidth = borderMapViewport.ActualWidth > 0 ? borderMapViewport.ActualWidth : 800;
            double viewHeight = borderMapViewport.ActualHeight > 0 ? borderMapViewport.ActualHeight : 480;

            mapTranslateTransform.X = (viewWidth / 2) - (vehicleX * _zoomScale);
            mapTranslateTransform.Y = (viewHeight / 2) - (vehicleY * _zoomScale);
        }

        private void BtnResetView_Click(object sender, RoutedEventArgs e)
        {
            _zoomScale = 1.0;
            mapScaleTransform.ScaleX = 1.0;
            mapScaleTransform.ScaleY = 1.0;
            mapTranslateTransform.X = 0;
            mapTranslateTransform.Y = 0;
        }
        #endregion

        #region Chuyển Đổi Lớp Bản Đồ (Dark, Light, Traffic)
        private void BtnMapThemeDark_Click(object sender, RoutedEventArgs e)
        {
            borderMapViewport.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            pathMapGrid.Stroke = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            pathSongCau.Stroke = new SolidColorBrush(Color.FromRgb(14, 58, 83));
            pathSongCauInner.Stroke = new SolidColorBrush(Color.FromRgb(2, 132, 199));
            parkSongCau.Fill = new SolidColorBrush(Color.FromRgb(6, 78, 59));
            roadLuongNgocQuyen.Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            roadPhanDinhPhung.Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            roadHoangVanThu.Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            roadCmt8.Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            roadCaoTocCt07.Stroke = new SolidColorBrush(Color.FromRgb(51, 65, 85));

            btnMapThemeDark.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            btnMapThemeDark.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            btnMapThemeDark.BorderBrush = new SolidColorBrush(Color.FromRgb(2, 132, 199));
            btnMapThemeDark.BorderThickness = new Thickness(1);

            btnMapThemeLight.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            btnMapThemeLight.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btnMapThemeLight.BorderThickness = new Thickness(0);

            btnMapThemeTraffic.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            btnMapThemeTraffic.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btnMapThemeTraffic.BorderThickness = new Thickness(0);
        }

        private void BtnMapThemeLight_Click(object sender, RoutedEventArgs e)
        {
            borderMapViewport.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            pathMapGrid.Stroke = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            pathSongCau.Stroke = new SolidColorBrush(Color.FromRgb(186, 230, 253));
            pathSongCauInner.Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            parkSongCau.Fill = new SolidColorBrush(Color.FromRgb(209, 250, 229));
            roadLuongNgocQuyen.Stroke = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            roadPhanDinhPhung.Stroke = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            roadHoangVanThu.Stroke = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            roadCmt8.Stroke = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            roadCaoTocCt07.Stroke = new SolidColorBrush(Color.FromRgb(148, 163, 184));

            btnMapThemeLight.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            btnMapThemeLight.Foreground = Brushes.White;
            btnMapThemeLight.BorderThickness = new Thickness(0);

            btnMapThemeDark.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            btnMapThemeDark.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btnMapThemeDark.BorderThickness = new Thickness(0);

            btnMapThemeTraffic.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            btnMapThemeTraffic.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btnMapThemeTraffic.BorderThickness = new Thickness(0);
        }

        private void BtnMapThemeTraffic_Click(object sender, RoutedEventArgs e)
        {
            borderMapViewport.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            pathMapGrid.Stroke = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            pathSongCau.Stroke = new SolidColorBrush(Color.FromRgb(14, 58, 83));
            pathSongCauInner.Stroke = new SolidColorBrush(Color.FromRgb(2, 132, 199));
            parkSongCau.Fill = new SolidColorBrush(Color.FromRgb(6, 78, 59));

            // Hiển thị dải màu mật độ giao thông thời gian thực
            roadLuongNgocQuyen.Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Xanh lá: Thông thoáng
            roadHoangVanThu.Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11));    // Cam: Đông vừa
            roadPhanDinhPhung.Stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));   // Đỏ: Giờ cao điểm
            roadCmt8.Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129));          // Xanh lá: Thông thoáng
            roadCaoTocCt07.Stroke = new SolidColorBrush(Color.FromRgb(6, 182, 212));      // Cyan: Tốc độ cao 80km/h

            btnMapThemeTraffic.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            btnMapThemeTraffic.Foreground = Brushes.White;
            btnMapThemeTraffic.BorderThickness = new Thickness(0);

            btnMapThemeDark.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            btnMapThemeDark.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btnMapThemeDark.BorderThickness = new Thickness(0);

            btnMapThemeLight.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            btnMapThemeLight.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btnMapThemeLight.BorderThickness = new Thickness(0);

            MessageBox.Show(
                "🚦 CHẾ ĐỘ MẬT ĐỘ GIAO THÔNG THỜI GIAN THỰC (LIVE TRAFFIC):\n\n" +
                "• Đại lộ Lương Ngọc Quyến: 🟢 Thông thoáng (Tốc độ TB: 42 km/h)\n" +
                "• Đường Hoàng Văn Thụ (Vincom): 🟡 Đông vừa (Tốc độ TB: 28 km/h)\n" +
                "• Trục Phan Đình Phùng (Khu Học Liệu): 🔴 Đông đúc / Chậm (Tốc độ TB: 18 km/h)\n" +
                "• Cao tốc CT07 (Hà Nội - Thái Nguyên): 🔵 Tốc độ cao (Tốc độ TB: 80 km/h)",
                "Bản Đồ Mật Độ Giao Thông",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        #endregion

        #region Chế Độ Giám Sát Đơn Lẻ & Radar Đội Xe (Fleet Tracking)
        private void BtnTrackingSingle_Click(object sender, RoutedEventArgs e)
        {
            markerFleetShipper2.Visibility = Visibility.Collapsed;
            markerFleetTruck1.Visibility = Visibility.Collapsed;
            btnTrackingSingle.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            btnTrackingSingle.Foreground = Brushes.White;
            btnTrackingFleet.Background = new SolidColorBrush(Color.FromRgb(51, 65, 85));
            btnTrackingFleet.Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        }

        private void BtnTrackingFleet_Click(object sender, RoutedEventArgs e)
        {
            markerFleetShipper2.Visibility = Visibility.Visible;
            markerFleetTruck1.Visibility = Visibility.Visible;
            btnTrackingFleet.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            btnTrackingFleet.Foreground = Brushes.White;
            btnTrackingSingle.Background = new SolidColorBrush(Color.FromRgb(51, 65, 85));
            btnTrackingSingle.Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225));

            MessageBox.Show(
                "🌐 KÍCH HOẠT RADAR GIÁM SÁT TOÀN BỘ ĐỘI XE LOGISTICS (FLEET OVERVIEW):\n\n" +
                "Bản đồ đang hiển thị toàn bộ 3 phương tiện đang hoạt động trên địa bàn:\n" +
                "1. 🛵 Xe Bùi Văn Đạt (20B1-567.89): Đang giao đơn hiện tại tại Hoàng Văn Thụ\n" +
                "2. 🛵 Xe Nguyễn Hữu Chiến (20B2-112.34): Đang giao tại Đại học Sư Phạm\n" +
                "3. 🚛 Xe tải trung chuyển 29C-889.12: Đang chạy tuyến CT07 về Hà Nội\n\n" +
                "Nhấp chuột vào bất kỳ biểu tượng xe nào trên bản đồ để xem chi tiết tài xế!",
                "Radar Đội Xe Hoạt Động",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MarkerFleetShipper2_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show(
                "🛵 THÔNG TIN SHIPPER ĐỘI XE:\n\n" +
                "• Bưu tá: Nguyễn Hữu Chiến\n" +
                "• SĐT di động: 0912 345 678\n" +
                "• Phương tiện: Honda Wave RSX (Biển số: 20B2-112.34)\n" +
                "• Địa bàn phụ trách: Lương Ngọc Quyến & ĐH Sư Phạm Thái Nguyên\n" +
                "• Số đơn đang chuyển: 4 bưu kiện • Tốc độ GPS: 36 km/h",
                "Chi Tiết Phương Tiện 2",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MarkerFleetTruck1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show(
                "🚛 THÔNG TIN XE TẢI TRUNG CHUYỂN LIÊN TỈNH:\n\n" +
                "• Lái xe: Nguyễn Văn Bắc\n" +
                "• SĐT di động: 024 3829 1111\n" +
                "• Phương tiện: Xe tải Isuzu 3.5 Tấn (Biển số: 29C-889.12)\n" +
                "• Tuyến vận hành: Tuyến Linehaul Hub Thái Nguyên ➔ Hub Long Biên (Hà Nội)\n" +
                "• Tọa độ hiện tại: Nút giao Sóc Sơn (Cao tốc CT07) • Tốc độ GPS: 62 km/h",
                "Chi Tiết Phương Tiện 3",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        #endregion

        #region Tích Hợp Google Maps Live Link
        private void BtnOpenGoogleMaps_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string originLatLon = "21.584200,105.843100"; // Kho Hub Tuấn Đạt Thái Nguyên
                string destLatLon = "21.598250,105.853000";   // Khách nhận Thái Nguyên

                if (_donHangDangTheoDoiGps != null && !_donHangDangTheoDoiGps.IsLocalHubDelivery)
                {
                    destLatLon = "21.033300,105.789000"; // Tuyến liên tỉnh Hà Nội
                }

                string url = $"https://www.google.com/maps/dir/?api=1&origin={originLatLon}&destination={destLatLon}&travelmode=driving";

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                MessageBox.Show(
                    "🌍 ĐÃ MỞ CHỈ ĐƯỜNG TRÊN GOOGLE MAPS!\n\n" +
                    $"• Điểm xuất phát: Kho Hub Tuấn Đạt ({originLatLon})\n" +
                    $"• Điểm đến: {_donHangDangTheoDoiGps?.ReceiverAddress ?? "Thái Nguyên"} ({destLatLon})\n" +
                    "Hệ thống điều hướng vệ tinh Google Maps đang mở trong trình duyệt của bạn.",
                    "Google Maps Dẫn Đường",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở Google Maps: {ex.Message}", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region Tua Lại Hành Trình (Timeline Scrubbing) & Tốc Độ Mô Phỏng
        private void SldGpsTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_danhSachWaypoint == null || _danhSachWaypoint.Count == 0) return;

            int targetIndex = (int)Math.Round((e.NewValue / 100.0) * (_danhSachWaypoint.Count - 1));
            targetIndex = Math.Clamp(targetIndex, 0, _danhSachWaypoint.Count - 1);

            // Cập nhật vị trí tức thời theo thanh trượt
            CapNhatViTriGpsTheoBuoc(targetIndex);
        }

        private void BtnSpeed1x_Click(object sender, RoutedEventArgs e)
        {
            _gpsTimer.Interval = TimeSpan.FromMilliseconds(1500);
            DoiMauNutTocDo(btnSpeed1x);
        }

        private void BtnSpeed2x_Click(object sender, RoutedEventArgs e)
        {
            _gpsTimer.Interval = TimeSpan.FromMilliseconds(750);
            DoiMauNutTocDo(btnSpeed2x);
        }

        private void BtnSpeed4x_Click(object sender, RoutedEventArgs e)
        {
            _gpsTimer.Interval = TimeSpan.FromMilliseconds(375);
            DoiMauNutTocDo(btnSpeed4x);
        }

        private void DoiMauNutTocDo(Button nutChon)
        {
            btnSpeed1x.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            btnSpeed1x.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
            btnSpeed2x.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            btnSpeed2x.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
            btnSpeed4x.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            btnSpeed4x.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));

            nutChon.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            nutChon.Foreground = Brushes.White;
        }

        private void BtnPlayGps_Click(object sender, RoutedEventArgs e) => _gpsTimer.Start();
        private void BtnPauseGps_Click(object sender, RoutedEventArgs e) => _gpsTimer.Stop();

        private void BtnResetGps_Click(object sender, RoutedEventArgs e)
        {
            if (_donHangDangTheoDoiGps != null)
            {
                KichHoatTheoDoiGpsChoDonHang(_donHangDangTheoDoiGps);
            }
        }

        private void BtnCallShipper_Click(object sender, RoutedEventArgs e)
        {
            string tenTaiXe = txtHudDriver.Text;
            string sdt = _donHangDangTheoDoiGps?.ShipperPhone ?? "0988 123 456";

            MessageBox.Show(
                $"📞 ĐANG KẾT NỐI CUỘC GỌI TỚI TÀI XẾ:\n\n" +
                $"• Tài xế: {tenTaiXe}\n" +
                $"• Hotline di động: {sdt}\n" +
                $"• Đang nhận giao đơn: {_donHangDangTheoDoiGps?.OrderCode ?? "N/A"}\n\n" +
                "Đang phát tín hiệu chuông...",
                "Gọi Điện Cho Tài Xế",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnMarkDeliveredGps_Click(object sender, RoutedEventArgs e)
        {
            if (_donHangDangTheoDoiGps == null)
            {
                MessageBox.Show("Vui lòng chọn một đơn hàng đang theo dõi!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var donHang = _donHangDangTheoDoiGps;
            donHang.Status = ShippingOrderStatus.Delivered;
            donHang.DeliveredDate = DateTime.Now;
            donHang.GpsSpeedKmH = 0;
            donHang.GpsDistanceKm = 0;
            donHang.GpsEtaMinutes = 0;
            donHang.GpsStatusDescription = "Giao hàng thành công tới tay người nhận, đã thu tiền COD.";

            _kho.UpdateShippingOrder(donHang);
            _gpsTimer.Stop();

            MessageBox.Show(
                $"🎉 XÁC NHẬN GIAO HÀNG THÀNH CÔNG!\n\n" +
                $"• Đơn hàng: {donHang.OrderCode}\n" +
                $"• Người nhận: {donHang.ReceiverName}\n" +
                $"• Tiền thu hộ COD: {donHang.CodAmount:N0} đ đã được cộng vào Ví Chủ Shop!\n" +
                $"• Thời gian giao: {DateTime.Now:HH:mm dd/MM/yyyy}",
                "Giao Hàng Thành Công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            NapDanhSachDonDangGiaoGps();
            NapDanhSachDonHang();
            CapNhatThongKeShop();
        }
        #endregion
        #endregion

        #region TAB 4: Sổ Bưu Gửi & Ví COD Của Shop
        public void NapDanhSachDonHang()
        {
            var currentUser = UserSession.Current.CurrentUser;
            var query = _kho.GetAllShippingOrders().AsQueryable();

            if (currentUser != null && currentUser.Role == UserRole.Customer)
            {
                // Khách hàng CHỈ xem danh sách bưu gửi của chính cửa hàng mình
                query = query.Where(d => d.CreatedBySource == "Shop Online" ||
                                         (!string.IsNullOrEmpty(d.SenderShopName) && d.SenderShopName.Contains("Shop", StringComparison.OrdinalIgnoreCase)) ||
                                         (!string.IsNullOrEmpty(d.SenderName) && currentUser.FullName != null && d.SenderName.Equals(currentUser.FullName, StringComparison.OrdinalIgnoreCase)) ||
                                         (!string.IsNullOrEmpty(d.SenderPhone) && currentUser.PhoneNumber != null && d.SenderPhone.Equals(currentUser.PhoneNumber, StringComparison.OrdinalIgnoreCase)));
            }

            _danhSachDonHangShop = query
                .OrderByDescending(d => d.CreatedDate)
                .ToList();

            dgShopOrders.ItemsSource = _danhSachDonHangShop;
        }

        private void TxtSearchShopOrders_TextChanged(object sender, TextChangedEventArgs e) => LocDanhSachShopOrders();
        private void CboFilterShopStatus_SelectionChanged(object sender, SelectionChangedEventArgs e) => LocDanhSachShopOrders();

        private void LocDanhSachShopOrders()
        {
            if (dgShopOrders == null || _danhSachDonHangShop == null) return;

            string tuKhoa = (txtSearchShopOrders?.Text ?? string.Empty).Trim().ToLowerInvariant();
            int filterStatus = cboFilterShopStatus?.SelectedIndex ?? 0;

            var ketQua = _danhSachDonHangShop.Where(d =>
            {
                bool khopTuKhoa = string.IsNullOrEmpty(tuKhoa) ||
                                  d.OrderCode.ToLowerInvariant().Contains(tuKhoa) ||
                                  d.ReceiverName.ToLowerInvariant().Contains(tuKhoa) ||
                                  d.ReceiverPhone.ToLowerInvariant().Contains(tuKhoa) ||
                                  d.ReceiverAddress.ToLowerInvariant().Contains(tuKhoa);

                bool khopTrangThai = filterStatus switch
                {
                    1 => d.Status == ShippingOrderStatus.NewReceived && !d.IsWarehouseCheckedIn,
                    2 => d.Status == ShippingOrderStatus.Delivering,
                    3 => d.Status == ShippingOrderStatus.Delivered,
                    _ => true
                };

                return khopTuKhoa && khopTrangThai;
            }).ToList();

            dgShopOrders.ItemsSource = ketQua;
        }

        private void BtnRefreshShopOrders_Click(object sender, RoutedEventArgs e)
        {
            NapDanhSachDonHang();
            CapNhatThongKeShop();
        }

        private void BtnRowViewGps_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ShippingOrder donHang)
            {
                tabBtnGpsTracking.IsChecked = true;
                KichHoatTheoDoiGpsChoDonHang(donHang);
            }
        }

        private void BtnRowPrintLabel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ShippingOrder donHang)
            {
                MoModalInTemNhiet(donHang);
            }
        }
        #endregion

        #region Thẻ Bưu Gửi Kỹ Thuật Số & Mã QR Cho Khách Hàng (Digital Drop-off Pass)
        private void BtnSuccessOk_Click(object sender, RoutedEventArgs e)
        {
            modalOrderCreatedSuccess.Visibility = Visibility.Collapsed;
        }

        private void BtnSuccessClose_Click(object sender, RoutedEventArgs e)
        {
            modalOrderCreatedSuccess.Visibility = Visibility.Collapsed;
        }

        private void BtnCopySuccessOrderCode_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtSuccessOrderCode.Text))
            {
                Clipboard.SetText(txtSuccessOrderCode.Text.Trim());
                MessageBox.Show($"Đã sao chép mã vận đơn '{txtSuccessOrderCode.Text.Trim()}' vào bộ nhớ tạm!", "Đã Sao Chép", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region In Tem Nhãn Nhiệt 100x150mm (Thermal Label Modal)
        private void MoModalInTemNhiet(ShippingOrder donHang)
        {
            txtThermalOrderCode.Text = donHang.OrderCode;
            txtThermalSender.Text = $"{donHang.SenderName} ({donHang.SenderPhone})";
            txtThermalSenderAddr.Text = donHang.SenderAddress ?? "Hub Thái Nguyên - Phan Đình Phùng";

            txtThermalReceiver.Text = $"{donHang.ReceiverName} - {donHang.ReceiverPhone}";
            txtThermalReceiverAddr.Text = donHang.ReceiverAddress;

            txtThermalProduct.Text = $"{donHang.ProductSummary} ({donHang.Weight:F1} kg)";
            txtThermalCod.Text = $"COD: {donHang.CodAmount:N0} VNĐ";

            try
            {
                imgThermalBarcode.Source = BarcodeHelper.TaoAnhBarcode(donHang.OrderCode, 360, 48);
                string qrData = $"{donHang.OrderCode}|{donHang.ReceiverName}|{donHang.ReceiverPhone}|{donHang.ReceiverAddress}|COD:{donHang.CodAmount}";
                imgThermalQrCode.Source = BarcodeHelper.TaoAnhQRCode(qrData, 100);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Barcode Generate Error] {ex.Message}");
            }

            modalPrintThermalLabel.Visibility = Visibility.Visible;
        }

        private void BtnCloseThermalModal_Click(object sender, RoutedEventArgs e)
        {
            modalPrintThermalLabel.Visibility = Visibility.Collapsed;
        }

        private void BtnExecutePrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDlg = new PrintDialog();
                if (printDlg.ShowDialog() == true)
                {
                    printDlg.PrintVisual(borderLabelToPrint, $"Tem_Van_Don_{txtThermalOrderCode.Text}");
                    MessageBox.Show("Đã gửi lệnh in tem nhãn vận đơn tới máy in thành công!", "In Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                    modalPrintThermalLabel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thực hiện in: {ex.Message}", "Lỗi In Ấn", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Hỗ Trợ Dữ Liệu Phụ & Thống Kê
        private void NapDanhSachShipper()
        {
            var shippers = _kho.GetAllShippers().ToList();
            if (!shippers.Any())
            {
                shippers.Add(new Shipper { Id = 1, FullName = "Bùi Văn Đạt", Phone = "0988 123 456", VehiclePlate = "20B1-567.89", DeliveryArea = "Nội thành Thái Nguyên" });
                shippers.Add(new Shipper { Id = 2, FullName = "Nguyễn Hữu Chiến", Phone = "0912 345 678", VehiclePlate = "20B2-112.34", DeliveryArea = "Phan Đình Phùng & Đồng Hỷ" });
            }
            cboShipperSelect.ItemsSource = shippers;
            cboShipperSelect.DisplayMemberPath = "FullName";
            cboShipperSelect.SelectedIndex = 0;
        }

        private void CapNhatThongKeShop()
        {
            var currentUser = UserSession.Current.CurrentUser;
            var query = _kho.GetAllShippingOrders().AsQueryable();

            if (currentUser != null && currentUser.Role == UserRole.Customer)
            {
                query = query.Where(d => d.CreatedBySource == "Shop Online" ||
                                         (!string.IsNullOrEmpty(d.SenderShopName) && d.SenderShopName.Contains("Shop", StringComparison.OrdinalIgnoreCase)) ||
                                         (!string.IsNullOrEmpty(d.SenderName) && currentUser.FullName != null && d.SenderName.Equals(currentUser.FullName, StringComparison.OrdinalIgnoreCase)) ||
                                         (!string.IsNullOrEmpty(d.SenderPhone) && currentUser.PhoneNumber != null && d.SenderPhone.Equals(currentUser.PhoneNumber, StringComparison.OrdinalIgnoreCase)));
            }

            var tatCaDon = query.ToList();
            int tongTao = tatCaDon.Count;
            int dangGiao = tatCaDon.Count(d => d.Status == ShippingOrderStatus.Delivering);
            decimal tienCod = tatCaDon.Where(d => d.Status == ShippingOrderStatus.Delivered).Sum(d => d.CodAmount);

            txtStatTotalCreated.Text = tongTao.ToString();
            txtStatDeliveringGps.Text = dangGiao.ToString();
            txtStatCodWallet.Text = $"{tienCod:N0} đ";
        }

        private void KhoiTaoGoiYMaDon()
        {
            panelRecentOrderChips.Children.Clear();
            var cacDonMoi = _kho.GetAllShippingOrders().OrderByDescending(d => d.CreatedDate).Take(3).ToList();
            foreach (var don in cacDonMoi)
            {
                ThemChipGoiYMaDon(don.OrderCode);
            }
        }

        private void ThemChipGoiYMaDon(string maDon)
        {
            var btnChip = new Button
            {
                Content = maDon,
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand
            };

            btnChip.Click += (s, e) =>
            {
                txtScanOrderCode.Text = maDon;
                ThucHienKiemTraMaDon(maDon);
            };

            panelRecentOrderChips.Children.Insert(0, btnChip);
        }
        #endregion
    }
}
