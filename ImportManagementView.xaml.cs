using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// Model cấu hình xe tải trung chuyển đối tác
    /// </summary>
    public class TruckFleetInfo
    {
        public string LicensePlate { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string DriverPhone { get; set; } = string.Empty;
        public int VehicleTypeIndex { get; set; } = 1;
        public string VehicleTypeName { get; set; } = "Xe tải 2.5 Tấn";
        public int OriginHubIndex { get; set; } = 0;
        public string OriginHubName { get; set; } = "Kho Tổng Cầu Giấy (Hà Nội)";
        public int DockIndex { get; set; } = 0;
        public string DockName { get; set; } = "Cửa Dock số 1";
        public double TotalWeight { get; set; }
        public int ExpectedQuantity { get; set; }
        public string ManifestCode { get; set; } = string.Empty;
        public string SealCode { get; set; } = string.Empty;
        public int FlowTypeIndex { get; set; } = 0;
        public string FlowTypeName { get; set; } = "Cross-Docking";
        public string Notes { get; set; } = string.Empty;

        // Thuộc tính hiển thị trực quan trên dropdown danh sách xe tải
        public string DriverSummary => string.IsNullOrWhiteSpace(DriverPhone) ? DriverName : $"Tài xế: {DriverName} ({DriverPhone})";
        public string RouteSummary => $"{VehicleTypeName} • Tuyến: {OriginHubName}";
        public string QuantityWeightSummary => ExpectedQuantity > 0 ? $"{ExpectedQuantity} kiện | {TotalWeight:N0} kg" : "Tự do nhập";
        public string DisplayText => $"{LicensePlate}  —  {DriverName} ({ExpectedQuantity} kiện / {TotalWeight:N0} kg)";

        public override string ToString() => LicensePlate;
    }

    /// <summary>
    /// Interaction logic for ImportManagementView.xaml
    /// Phân hệ Nghiệp Vụ Tiếp Nhận &amp; Nhập Kho Logistics (Inbound Logistics Hub)
    /// - Nhánh 1: Tiếp nhận chuyến xe tải trung chuyển (Linehaul / Truck Inbound / B2B)
    /// - Nhánh 2: Tiếp nhận bưu kiện khách lẻ tại quầy (Retail POS Counter)
    /// - Nhánh 3: Sổ nhật ký nhập kho &amp; Lịch sử đối soát lô hàng
    /// </summary>
    public partial class ImportManagementView : UserControl
    {
        private List<ImportOrder> _danhSachLichSuNhap = new();

        /// <summary>
        /// DANH SÁCH BIỂN SỐ XE TẢI VỚI THÔNG TIN TÀI XẾ &amp; SỐ LƯỢNG HÀNG VẬN CHUYỂN KHÁC NHAU
        /// Mỗi biển số đại diện cho một phương tiện, tài xế, tuyến đường và số lượng kiện hàng riêng biệt.
        /// </summary>
        private readonly List<TruckFleetInfo> _danhSachXeTaiDoiTac = new()
        {
            new TruckFleetInfo
            {
                LicensePlate = "29C-889.12",
                DriverName = "Nguyễn Văn Hùng",
                DriverPhone = "0988 567 890",
                VehicleTypeIndex = 1, // 2.5 Tấn
                VehicleTypeName = "Xe tải 2.5 Tấn",
                OriginHubIndex = 0,   // Kho Tổng Cầu Giấy
                OriginHubName = "Kho Tổng Cầu Giấy (Hà Nội)",
                DockIndex = 0,        // Dock 1
                DockName = "Cửa Dock số 1 (Bến hàng nặng)",
                TotalWeight = 2150.0,
                ExpectedQuantity = 95,
                ManifestCode = "TRIP-HN-TN-042",
                SealCode = "SEAL-HN-88219",
                FlowTypeIndex = 0,    // Cross-docking
                FlowTypeName = "1. Hàng Cross-Docking",
                Notes = "Chuyến xe sáng từ Kho Tổng Hà Nội giao Thái Nguyên, seal kẹp chì nguyên vẹn."
            },
            new TruckFleetInfo
            {
                LicensePlate = "20H-012.34",
                DriverName = "Trần Quốc Tuấn",
                DriverPhone = "0912 445 889",
                VehicleTypeIndex = 2, // 5.0 Tấn
                VehicleTypeName = "Xe tải 5.0 Tấn",
                OriginHubIndex = 3,   // Hub Thái Nguyên
                OriginHubName = "Hub Thái Nguyên (Sông Công)",
                DockIndex = 1,        // Dock 2
                DockName = "Cửa Dock số 2 (Bến bưu kiện thường)",
                TotalWeight = 4800.0,
                ExpectedQuantity = 180,
                ManifestCode = "TRIP-TN-HN-105",
                SealCode = "SEAL-TN-45210",
                FlowTypeIndex = 1,    // Hàng lưu kho
                FlowTypeName = "2. Hàng Lưu Kho",
                Notes = "Hàng linh kiện và hàng may mặc từ KCN Sông Công về kho tổng, pallet đóng thùng chắc chắn."
            },
            new TruckFleetInfo
            {
                LicensePlate = "15C-345.67",
                DriverName = "Phạm Văn Long",
                DriverPhone = "0936 789 102",
                VehicleTypeIndex = 3, // 10 Tấn
                VehicleTypeName = "Xe tải 10 Tấn (Liên tỉnh)",
                OriginHubIndex = 1,   // Hub Hải Phòng
                OriginHubName = "Hub Hải Phòng (Cảng Đình Vũ)",
                DockIndex = 0,        // Dock 1
                DockName = "Cửa Dock số 1 (Bến hàng nặng)",
                TotalWeight = 9250.0,
                ExpectedQuantity = 340,
                ManifestCode = "TRIP-HP-HN-208",
                SealCode = "SEAL-HP-77123",
                FlowTypeIndex = 1,    // Hàng lưu kho
                FlowTypeName = "2. Hàng Lưu Kho",
                Notes = "Lô hàng bách hóa cảng Đình Vũ Hải Phòng, tải trọng nặng đưa vào kệ sàn tầng trệt Khu C."
            },
            new TruckFleetInfo
            {
                LicensePlate = "99C-223.88",
                DriverName = "Hoàng Minh Đức",
                DriverPhone = "0971 234 567",
                VehicleTypeIndex = 0, // 1.25 Tấn
                VehicleTypeName = "Xe tải 1.25 Tấn",
                OriginHubIndex = 2,   // Hub Bắc Ninh
                OriginHubName = "Hub Bắc Ninh (KCN Quế Võ)",
                DockIndex = 2,        // Dock 3
                DockName = "Cửa Dock số 3 (Bến Cross-docking nhanh)",
                TotalWeight = 1150.0,
                ExpectedQuantity = 45,
                ManifestCode = "TRIP-BN-HN-033",
                SealCode = "SEAL-BN-33901",
                FlowTypeIndex = 2,    // Giao chặng cuối
                FlowTypeName = "3. Hàng Giao Chặng Cuối",
                Notes = "Hàng phụ kiện điện thoại, linh kiện máy tính giao gấp trong ca cho các bưu cục nội thành."
            },
            new TruckFleetInfo
            {
                LicensePlate = "89C-556.77",
                DriverName = "Lê Anh Dũng",
                DriverPhone = "0966 888 999",
                VehicleTypeIndex = 1, // 2.5 Tấn
                VehicleTypeName = "Xe tải 2.5 Tấn",
                OriginHubIndex = 4,   // Kho ICD Tiên Sơn
                OriginHubName = "Kho ICD Tiên Sơn",
                DockIndex = 1,        // Dock 2
                DockName = "Cửa Dock số 2 (Bến bưu kiện thường)",
                TotalWeight = 3200.0,
                ExpectedQuantity = 120,
                ManifestCode = "TRIP-ICD-HN-089",
                SealCode = "SEAL-ICD-91024",
                FlowTypeIndex = 1,    // Hàng lưu kho
                FlowTypeName = "2. Hàng Lưu Kho",
                Notes = "Hàng thương mại điện tử chuyển từ kho ngoại quan ICD Tiên Sơn."
            },
            new TruckFleetInfo
            {
                LicensePlate = "51D-998.11",
                DriverName = "Vũ Đình Nam",
                DriverPhone = "0903 112 233",
                VehicleTypeIndex = 4, // Container 40ft
                VehicleTypeName = "Xe Đầu Kéo Container",
                OriginHubIndex = 0,   // Kho Tổng Cầu Giấy
                OriginHubName = "Kho Tổng Cầu Giấy (Hà Nội)",
                DockIndex = 0,        // Dock 1
                DockName = "Cửa Dock số 1 (Bến hàng nặng)",
                TotalWeight = 22500.0,
                ExpectedQuantity = 850,
                ManifestCode = "TRIP-BNAM-HN-999",
                SealCode = "SEAL-CONT-00192",
                FlowTypeIndex = 0,    // Cross-docking
                FlowTypeName = "1. Hàng Cross-Docking",
                Notes = "Chuyến container trục Bắc Nam, bốc dỡ hàng nguyên seal kẹp chì hải quan."
            },
            new TruckFleetInfo
            {
                LicensePlate = "30F-678.90",
                DriverName = "Đỗ Mạnh Cường",
                DriverPhone = "0983 221 445",
                VehicleTypeIndex = 1, // 2.5 Tấn
                VehicleTypeName = "Xe tải 2.5 Tấn",
                OriginHubIndex = 5,   // Nhà Cung Cấp B2B
                OriginHubName = "Nhà Cung Cấp B2B Giao Trực Tiếp",
                DockIndex = 1,        // Dock 2
                DockName = "Cửa Dock số 2 (Bến bưu kiện thường)",
                TotalWeight = 1680.0,
                ExpectedQuantity = 75,
                ManifestCode = "TRIP-B2B-HN-056",
                SealCode = "SEAL-B2B-55120",
                FlowTypeIndex = 0,    // Cross-docking
                FlowTypeName = "1. Hàng Cross-Docking",
                Notes = "Hàng tiêu dùng và thiết bị từ nhà cung cấp B2B chuyển về phân phối."
            },
            new TruckFleetInfo
            {
                LicensePlate = "14C-456.78",
                DriverName = "Bùi Hữu Đạt",
                DriverPhone = "0978 990 123",
                VehicleTypeIndex = 2, // 5.0 Tấn
                VehicleTypeName = "Xe tải 5.0 Tấn",
                OriginHubIndex = 1,   // Hub Hải Phòng
                OriginHubName = "Hub Hải Phòng (Cảng Đình Vũ)",
                DockIndex = 0,        // Dock 1
                DockName = "Cửa Dock số 1 (Bến hàng nặng)",
                TotalWeight = 5400.0,
                ExpectedQuantity = 210,
                ManifestCode = "TRIP-QN-HN-077",
                SealCode = "SEAL-QN-66341",
                FlowTypeIndex = 1,    // Hàng lưu kho
                FlowTypeName = "2. Hàng Lưu Kho",
                Notes = "Chuyến xe gom bưu phẩm các bưu cục ven biển Quảng Ninh về kho."
            },
            new TruckFleetInfo
            {
                LicensePlate = "+ Nhập Biển Số Xe Mới...",
                DriverName = "(Tự do nhập tài xế & số lượng hàng)",
                DriverPhone = "",
                VehicleTypeIndex = 1,
                VehicleTypeName = "Xe tải ngoài danh bạ",
                OriginHubIndex = 0,
                OriginHubName = "Kho Tự Chọn",
                DockIndex = 0,
                DockName = "Cửa Dock",
                TotalWeight = 0,
                ExpectedQuantity = 0,
                ManifestCode = "",
                SealCode = "",
                FlowTypeIndex = 0,
                FlowTypeName = "Tự chọn",
                Notes = "Xe tải mới ngoài danh bạ cập bến dỡ hàng."
            }
        };

        public ImportManagementView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            NapDanhSachXeTai();
            NapDuLieuNhapKho();
            DatLaiFormKhachLeMacDinh();
        }

        private void NapDanhSachXeTai()
        {
            if (cbTruckPlate == null) return;
            cbTruckPlate.ItemsSource = null;
            cbTruckPlate.ItemsSource = _danhSachXeTaiDoiTac;
            cbTruckPlate.SelectedIndex = 0;
            if (_danhSachXeTaiDoiTac.Count > 0)
            {
                ApDungThongTinXeTai(_danhSachXeTaiDoiTac[0]);
            }
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ dữ liệu phiếu nhập kho và cập nhật các thẻ KPI
        /// </summary>
        public void NapDuLieuNhapKho()
        {
            var khoDuLieu = WarehouseContext.Instance;
            _danhSachLichSuNhap = khoDuLieu.GetAllImportOrders().ToList();

            if (_danhSachLichSuNhap.Count == 0)
            {
                KhoiTaoDuLieuMauNhapKho();
                _danhSachLichSuNhap = khoDuLieu.GetAllImportOrders().ToList();
            }

            int tongSoPhieu = _danhSachLichSuNhap.Count;
            int soChuyenXeTai = _danhSachLichSuNhap.Count(p => p.SourceType == ImportSourceType.TransitHub || p.SourceType == ImportSourceType.Company);
            int soKhachLe = _danhSachLichSuNhap.Count(p => p.SourceType == ImportSourceType.Individual || p.SourceType == ImportSourceType.Shop);
            double tongTrongLuongKg = _danhSachLichSuNhap.Sum(p => p.TotalWeight);

            if (txtTotalImportOrders != null) txtTotalImportOrders.Text = $"{tongSoPhieu} phiếu";
            if (txtTotalTruckBatches != null) txtTotalTruckBatches.Text = $"{soChuyenXeTai} chuyến xe";
            if (txtTotalRetailParcels != null) txtTotalRetailParcels.Text = $"{soKhachLe} bưu kiện";
            if (txtTotalInboundWeight != null) txtTotalInboundWeight.Text = $"{tongTrongLuongKg:N1} kg";

            ApDungBoLocLichSu();
        }

        public void LoadData() => NapDuLieuNhapKho();

        private void KhoiTaoDuLieuMauNhapKho()
        {
            var khoDuLieu = WarehouseContext.Instance;

            khoDuLieu.AddImportOrder(new ImportOrder
            {
                ImportCode = "NK-TRUCK-260901",
                SourceType = ImportSourceType.TransitHub,
                SenderName = "Kho Tổng Cầu Giấy (Hà Nội)",
                SenderPhone = "0988 567 890",
                SenderAddress = "Km9 Đại Lộ Thăng Long, Hà Nội",
                VehiclePlate = "29C-889.12",
                DriverName = "Nguyễn Văn Hùng",
                WaybillNumber = "TRIP-HN-TN-042",
                TotalWeight = 2150.0,
                TotalValue = 120000000,
                Status = ImportOrderStatus.Approved,
                CreatedDate = DateTime.Now.AddHours(-5),
                CreatedByName = "Thủ kho tiếp nhận",
                Notes = "Seal: SEAL-HN-88219 (Nguyên vẹn) | Cửa Dock 1 | Vị trí: Khu Cross-Docking"
            });

            khoDuLieu.AddImportOrder(new ImportOrder
            {
                ImportCode = "NK-RETAIL-260902",
                SourceType = ImportSourceType.Individual,
                SenderName = "Chị Lê Hoàng Yến",
                SenderPhone = "0977 456 789",
                SenderAddress = "Số 12 Duy Tân, Cầu Giấy, Hà Nội",
                VehiclePlate = "Khách gửi tại quầy",
                DriverName = "Khách vãng lai",
                WaybillNumber = "POS-00129",
                TotalWeight = 2.4,
                TotalValue = 850000,
                Status = ImportOrderStatus.Approved,
                CreatedDate = DateTime.Now.AddHours(-2),
                CreatedByName = "Giao dịch viên quầy",
                Notes = "Gửi giày thể thao đi TP Thái Nguyên | Cước: 32.000đ | Vị trí: Khu B - Kệ 02"
            });
        }

        #region Chuyển Đổi Tab Chuyên Dụng (Segmented Tab Bar)
        private void TabInbound_Checked(object sender, RoutedEventArgs e)
        {
            if (panelTruckInbound == null || panelRetailInbound == null || panelHistoryInbound == null) return;

            if (tabTruckInbound?.IsChecked == true)
            {
                panelTruckInbound.Visibility = Visibility.Visible;
                panelRetailInbound.Visibility = Visibility.Collapsed;
                panelHistoryInbound.Visibility = Visibility.Collapsed;
            }
            else if (tabRetailInbound?.IsChecked == true)
            {
                panelTruckInbound.Visibility = Visibility.Collapsed;
                panelRetailInbound.Visibility = Visibility.Visible;
                panelHistoryInbound.Visibility = Visibility.Collapsed;
                TinhToanCuocKhachLe();
            }
            else if (tabHistoryInbound?.IsChecked == true)
            {
                panelTruckInbound.Visibility = Visibility.Collapsed;
                panelRetailInbound.Visibility = Visibility.Collapsed;
                panelHistoryInbound.Visibility = Visibility.Visible;
                NapDuLieuNhapKho();
            }
        }
        #endregion

        #region Nghiệp Vụ 1: Nhập Hàng Xe Tải Theo Danh Sách Xe & Gợi Ý Vị Trí Thông Minh

        /// <summary>
        /// SỰ KIỆN: Khi người dùng chọn biển số xe tải từ danh sách
        /// - Mỗi biển số xe sẽ tự động nạp thông tin tài xế, SĐT, loại xe, số kiện manifest và tải trọng khác nhau
        /// </summary>
        private void CbTruckPlate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbTruckPlate == null) return;

            if (cbTruckPlate.SelectedItem is TruckFleetInfo xe)
            {
                ApDungThongTinXeTai(xe);
            }
            else if (cbTruckPlate.SelectedIndex >= 0 && cbTruckPlate.SelectedIndex < _danhSachXeTaiDoiTac.Count)
            {
                ApDungThongTinXeTai(_danhSachXeTaiDoiTac[cbTruckPlate.SelectedIndex]);
            }

            XoaDanhDauLoi(cbTruckPlate);
        }

        /// <summary>
        /// HÀM XỬ LÝ SỐ THỰC CHUYÊN DỤNG CHO LOGISTICS (KHẮC PHỤC TRIỆT ĐỂ LỖI DẤU CHẤM/PHẨY ĐỊNH DẠNG)
        /// - Hỗ trợ cả định dạng Việt Nam (chấm hàng nghìn, phẩy thập phân: "22.500", "22.500,5")
        /// - Hỗ trợ cả định dạng Quốc tế (phẩy hàng nghìn, chấm thập phân: "22,500", "22,500.5")
        /// - Hỗ trợ số nguyên thuần: "22500", "1150"
        /// - Ngăn chặn hoàn toàn lỗi biến 22.500 kg thành 22.5 kg!
        /// </summary>
        public static bool ThuPhanTichKhoiLuong(string? text, out double ketQua, bool laXeTai = false)
        {
            ketQua = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string s = text.Trim().Replace(" ", "");

            int lastDot = s.LastIndexOf('.');
            int lastComma = s.LastIndexOf(',');

            if (lastDot >= 0 && lastComma >= 0)
            {
                if (lastComma > lastDot)
                {
                    // Định dạng VN: "22.500,5" -> xóa chấm, đổi phẩy thành chấm -> "22500.5"
                    s = s.Replace(".", "").Replace(",", ".");
                }
                else
                {
                    // Định dạng US: "22,500.5" -> xóa phẩy -> "22500.5"
                    s = s.Replace(",", "");
                }
            }
            else if (lastDot >= 0)
            {
                int phanSauDau = s.Length - 1 - lastDot;
                if (phanSauDau == 3)
                {
                    // Dấu chấm phân cách hàng nghìn (VD: 22.500 -> 22500)
                    s = s.Replace(".", "");
                }
            }
            else if (lastComma >= 0)
            {
                int phanSauDau = s.Length - 1 - lastComma;
                if (phanSauDau == 3)
                {
                    // Dấu phẩy phân cách hàng nghìn (VD: 22,500 -> 22500)
                    s = s.Replace(",", "");
                }
                else
                {
                    // Phẩy thập phân: "22,5" -> "22.5"
                    s = s.Replace(",", ".");
                }
            }

            if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                ketQua = val;
                return ketQua > 0;
            }

            return false;
        }

        /// <summary>
        /// Nạp toàn bộ thông tin phương tiện, tài xế, tải trọng và số lượng kiện hàng theo xe được chọn
        /// </summary>
        private void ApDungThongTinXeTai(TruckFleetInfo xe)
        {
            if (xe == null) return;

            if (xe.LicensePlate.StartsWith("+")) // Tùy chọn nhập xe tải mới
            {
                cbTruckPlate.Text = "";
                txtTruckDriverName.Text = "";
                txtTruckDriverPhone.Text = "";
                txtTruckManifestCode.Text = $"TRIP-NEW-{DateTime.Now:MMddHHmm}";
                txtTruckSealCode.Text = $"SEAL-VN-{new Random().Next(10000, 99999)}";
                cbTruckSealStatus.SelectedIndex = 0;
                txtTruckTotalWeight.Text = "";
                txtTruckExpectedQuantity.Text = "";
                txtTruckActualQuantity.Text = "";
                txtTruckDamagedQuantity.Text = "0";
                cbTruckFlowType.SelectedIndex = 0;
                txtTruckNotes.Text = "Xe tải mới ngoài danh bạ cập bến dỡ hàng.";
                cbTruckPlate.Focus();
            }
            else
            {
                cbTruckPlate.Text = xe.LicensePlate;
                cbTruckType.SelectedIndex = xe.VehicleTypeIndex;
                txtTruckDriverName.Text = xe.DriverName;
                txtTruckDriverPhone.Text = xe.DriverPhone;
                cbTruckOriginHub.SelectedIndex = xe.OriginHubIndex;
                cbTruckDock.SelectedIndex = xe.DockIndex;
                txtTruckManifestCode.Text = xe.ManifestCode;
                txtTruckSealCode.Text = xe.SealCode;
                cbTruckSealStatus.SelectedIndex = 0;
                // Chuẩn hóa định dạng số nguyên kg sạch, không dùng dấu chấm/phẩy gây nhầm lẫn
                txtTruckTotalWeight.Text = xe.TotalWeight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                txtTruckExpectedQuantity.Text = xe.ExpectedQuantity.ToString();
                txtTruckActualQuantity.Text = xe.ExpectedQuantity.ToString();
                txtTruckDamagedQuantity.Text = "0";
                cbTruckFlowType.SelectedIndex = xe.FlowTypeIndex;
                txtTruckNotes.Text = xe.Notes;
            }

            CapNhatGoiYViTriXeTai();
            XoaTatCaDanhDauLoi();
        }

        private void TruckCalculationInputs_Changed(object sender, TextChangedEventArgs e)
        {
            if (sender is Control ctrl)
            {
                XoaDanhDauLoi(ctrl);
            }
            CapNhatGoiYViTriXeTai();
        }

        private void CbTruckFlowType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CapNhatGoiYViTriXeTai();
        }

        /// <summary>
        /// THUẬT TOÁN GỢI Ý VỊ TRÍ LƯU KỆ THÔNG MINH CHO XE TẢI (SMART PUT-AWAY)
        /// - Nếu là hàng Cross-docking: Giữ ngay tại cửa xuất, không cần cất vào kệ sâu.
        /// - Nếu trọng lượng kiện trung bình hoặc tổng xe lớn (> 15kg/kiện): Khuyên đặt tầng 1 Khu C (sàn chịu lực).
        /// - Nếu hàng tiêu chuẩn: Khuyên đặt Khu B - Kệ 02.
        /// </summary>
        private void CapNhatGoiYViTriXeTai()
        {
            if (txtTruckPutAwaySuggestion == null || borderTruckPutAway == null || cbTruckLocationCode == null) return;

            int loaiPhanLuong = cbTruckFlowType?.SelectedIndex ?? 0;
            ThuPhanTichKhoiLuong(txtTruckTotalWeight?.Text, out double tongTrongLuong, laXeTai: true);
            int.TryParse(txtTruckActualQuantity?.Text?.Trim(), out int soKien);
            if (soKien <= 0) soKien = 1;

            // Cập nhật nhãn gợi ý chuyển đổi Tấn / kg ngay dưới ô nhập
            if (lblTruckWeightHint != null)
            {
                if (tongTrongLuong >= 1000)
                {
                    lblTruckWeightHint.Text = $"⚖️ Quy đổi tải trọng: {tongTrongLuong / 1000.0:0.##} Tấn ({tongTrongLuong:N0} kg)";
                    lblTruckWeightHint.Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52));
                }
                else if (tongTrongLuong > 0 && tongTrongLuong < 50)
                {
                    lblTruckWeightHint.Text = $"⚠️ Chú ý: Đang nhập {tongTrongLuong:N1} kg. Nếu xe chở {tongTrongLuong} Tấn, hãy nhập {tongTrongLuong * 1000:0} kg!";
                    lblTruckWeightHint.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                }
                else if (tongTrongLuong > 0)
                {
                    lblTruckWeightHint.Text = $"⚖️ Khối lượng tải: {tongTrongLuong:N1} kg";
                    lblTruckWeightHint.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                }
                else
                {
                    lblTruckWeightHint.Text = "Nhập khối lượng theo kg (VD: 22500 kg = 22.5 tấn)";
                    lblTruckWeightHint.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                }
            }

            double trongLuongTrungBinhKien = tongTrongLuong / soKien;

            if (loaiPhanLuong == 0 || loaiPhanLuong == 2) // Cross-docking hoặc Giao chặng cuối (Nội vùng)
            {
                txtTruckPutAwayIcon.Text = "⚡";
                txtTruckPutAwaySuggestion.Text = "Khuyên đặt: Khu Soạn Phát Giao Ngay / Cross-Docking (Hàng nội vùng Thái Nguyên, xuất bến phát luôn không cất vào kệ sâu)";
                borderTruckPutAway.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                borderTruckPutAway.BorderBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172));
                cbTruckLocationCode.SelectedIndex = 0;
            }
            else if (trongLuongTrungBinhKien > 15.0 || tongTrongLuong > 2000) // Hàng nặng
            {
                txtTruckPutAwayIcon.Text = "🏋️";
                txtTruckPutAwaySuggestion.Text = $"Khuyên đặt: Khu C - Kệ 01 (Tầng 1 - Sàn trệt chịu lực an toàn cho hàng nặng {trongLuongTrungBinhKien:N1} kg/kiện)";
                borderTruckPutAway.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                borderTruckPutAway.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 211, 77));
                cbTruckLocationCode.SelectedIndex = 1;
            }
            else
            {
                txtTruckPutAwayIcon.Text = "📦";
                txtTruckPutAwaySuggestion.Text = "Khuyên đặt: Khu B - Kệ 02 (Kệ pallet tiêu chuẩn thông thường)";
                borderTruckPutAway.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
                borderTruckPutAway.BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254));
                cbTruckLocationCode.SelectedIndex = 2;
            }
        }

        private void BtnFillSampleTruck_Click(object sender, RoutedEventArgs e)
        {
            if (cbTruckPlate != null && _danhSachXeTaiDoiTac.Count > 0)
            {
                cbTruckPlate.SelectedIndex = 0; // Chọn xe Hà Nội - Thái Nguyên (29C-889.12)
                ApDungThongTinXeTai(_danhSachXeTaiDoiTac[0]);
            }
            MessageBox.Show("Đã chọn và tự động nạp chuyến xe tải mẫu (29C-889.12 - Tuyến Hà Nội - Thái Nguyên)!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// SỰ KIỆN: Xác nhận nhập kho chuyến xe tải VỚI KIỂM TRA DỮ LIỆU ĐẦU VÀO
        /// - Tự động tạo phiếu ImportOrder
        /// - Tự động đẩy đơn hàng sang phân hệ Gom Tuyến &amp; Điều Phối
        /// - Cập nhật số lượng đơn chờ lên badge của MainWindow
        /// </summary>
        private void BtnSaveTruckInbound_Click(object sender, RoutedEventArgs e)
        {
            var danhSachLoi = new List<string>();
            Control? controlLoiDauTien = null;

            // 1. Trích xuất biển số xe
            string bienSoXe = cbTruckPlate.Text?.Trim() ?? "";
            if (cbTruckPlate.SelectedItem is TruckFleetInfo xeChon && !xeChon.LicensePlate.StartsWith("+"))
            {
                bienSoXe = xeChon.LicensePlate;
            }
            else if (cbTruckPlate.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
            {
                string noiDung = cbi.Content.ToString()!;
                if (noiDung.Contains("(") && noiDung.Contains(" "))
                {
                    bienSoXe = noiDung.Split(' ')[0].Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(bienSoXe) || bienSoXe.StartsWith("+"))
            {
                danhSachLoi.Add("Biển số xe tải không được để trống (vui lòng chọn từ danh sách hoặc nhập biển số hợp lệ).");
                DanhDauLoi(cbTruckPlate);
                controlLoiDauTien ??= cbTruckPlate;
            }

            // 2. Họ tên lái xe
            string tenTaiXe = txtTruckDriverName.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(tenTaiXe))
            {
                danhSachLoi.Add("Họ tên tài xế lái xe không được để trống.");
                DanhDauLoi(txtTruckDriverName);
                controlLoiDauTien ??= txtTruckDriverName;
            }

            // 3. Số điện thoại tài xế
            string sdtTaiXe = txtTruckDriverPhone.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(sdtTaiXe))
            {
                danhSachLoi.Add("Số điện thoại tài xế không được để trống.");
                DanhDauLoi(txtTruckDriverPhone);
                controlLoiDauTien ??= txtTruckDriverPhone;
            }
            else if (!KiemTraSoDienThoaiHopLe(sdtTaiXe))
            {
                danhSachLoi.Add("Số điện thoại tài xế không hợp lệ (cần 10 chữ số).");
                DanhDauLoi(txtTruckDriverPhone);
                controlLoiDauTien ??= txtTruckDriverPhone;
            }

            // 4. Mã Chuyến Xe / Manifest
            string maManifest = txtTruckManifestCode.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(maManifest))
            {
                danhSachLoi.Add("Mã Chuyến Xe / Manifest không được để trống.");
                DanhDauLoi(txtTruckManifestCode);
                controlLoiDauTien ??= txtTruckManifestCode;
            }

            // 5. Khối lượng và số kiện
            if (!ThuPhanTichKhoiLuong(txtTruckTotalWeight.Text, out double trongLuongXe, laXeTai: true) || trongLuongXe <= 0)
            {
                danhSachLoi.Add("Tổng khối lượng xe tải phải là số dương lớn hơn 0 kg.");
                DanhDauLoi(txtTruckTotalWeight);
                controlLoiDauTien ??= txtTruckTotalWeight;
            }
            else if (trongLuongXe < 50)
            {
                danhSachLoi.Add($"Tổng khối lượng xe tải đang là {trongLuongXe:N1} kg (quá nhỏ cho chuyến xe tải). Vui lòng nhập bằng đơn vị kg (VD: xe 22.5 tấn -> nhập 22500 kg).");
                DanhDauLoi(txtTruckTotalWeight);
                controlLoiDauTien ??= txtTruckTotalWeight;
            }

            if (!int.TryParse(txtTruckActualQuantity.Text.Trim(), out int soKienThucNhan) || soKienThucNhan <= 0)
            {
                danhSachLoi.Add("Số kiện thực nhận từ xe tải phải là số nguyên lớn hơn 0.");
                DanhDauLoi(txtTruckActualQuantity);
                controlLoiDauTien ??= txtTruckActualQuantity;
            }

            string maSeal = txtTruckSealCode.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(maSeal))
            {
                danhSachLoi.Add("Mã seal chì niêm phong không được để trống.");
                DanhDauLoi(txtTruckSealCode);
                controlLoiDauTien ??= txtTruckSealCode;
            }

            if (danhSachLoi.Count > 0)
            {
                if (borderTruckValidationAlert != null && txtTruckValidationDetails != null)
                {
                    borderTruckValidationAlert.Visibility = Visibility.Visible;
                    txtTruckValidationDetails.Text = string.Join("\n• ", danhSachLoi);
                }

                MessageBox.Show(
                    $"KIỂM TRA DỮ LIỆU ĐẦU VÀO XE TẢI CHƯA ĐẠT CHUẨN!\n\n" +
                    $"Phát hiện {danhSachLoi.Count} thông tin cần bổ sung:\n\n" +
                    string.Join("\n", danhSachLoi.Select((l, i) => $"{i + 1}. {l}")) +
                    $"\n\nVui lòng kiểm tra lại các trường viền đỏ!",
                    "Cảnh Báo Dữ Liệu Xe Tải", MessageBoxButton.OK, MessageBoxImage.Warning);

                controlLoiDauTien?.Focus();
                return;
            }

            if (borderTruckValidationAlert != null)
            {
                borderTruckValidationAlert.Visibility = Visibility.Collapsed;
            }

            int.TryParse(txtTruckExpectedQuantity.Text.Trim(), out int soKienKhaiBao);
            int.TryParse(txtTruckDamagedQuantity.Text.Trim(), out int soKienHong);

            if (soKienKhaiBao <= 0) soKienKhaiBao = soKienThucNhan;

            string hubGui = (cbTruckOriginHub.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Kho Tổng Cầu Giấy (Hà Nội)";
            string cuaDock = (cbTruckDock.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cửa Dock số 1";
            string tinhTrangSeal = (cbTruckSealStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Nguyên vẹn";
            string viTriKe = (cbTruckLocationCode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Khu Cross-Docking";

            string maPhieu = $"NK-TRUCK-{DateTime.Now:yyMMddHHmm}";

            var phieuNhapXeTai = new ImportOrder
            {
                ImportCode = maPhieu,
                SourceType = ImportSourceType.TransitHub,
                SenderName = $"{hubGui} (Xe {bienSoXe})",
                SenderPhone = sdtTaiXe,
                SenderAddress = hubGui,
                VehiclePlate = bienSoXe,
                DriverName = tenTaiXe,
                WaybillNumber = maManifest,
                TotalWeight = trongLuongXe > 0 ? trongLuongXe : 1000.0,
                TotalValue = 50000000,
                Status = ImportOrderStatus.Approved,
                CreatedDate = DateTime.Now,
                CreatedByName = UserSession.Current.CurrentUser?.FullName ?? "Nhân viên tiếp nhận xe",
                Notes = $"Seal: {maSeal} ({tinhTrangSeal}) | {cuaDock} | Vị trí cất: {viTriKe} | Hỏng: {soKienHong} kiện | {txtTruckNotes.Text}"
            };

            // 1. Lưu phiếu nhập kho vào CSDL
            WarehouseContext.Instance.AddImportOrder(phieuNhapXeTai);

            // 2. TỰ ĐỘNG ĐẨY ĐƠN HÀNG SANG PHÂN HỆ GOM TUYẾN & ĐIỀU PHỐI (Tạo đơn đại diện cho chuyến xe)
            var donDaiDienChuyenXe = new ShippingOrder
            {
                OrderCode = $"LOGIX-TRK-{DateTime.Now:yyMMdd}-{new Random().Next(100, 999)}",
                SenderName = hubGui,
                SenderPhone = sdtTaiXe,
                SenderAddress = hubGui,
                ReceiverName = "Trung Tâm Phân Phối Thái Nguyên",
                ReceiverPhone = "0944 555 666",
                ReceiverAddress = "Tổ 10, Phường Thịnh Đán, TP Thái Nguyên",
                DestinationArea = "Thái Nguyên",
                ProductSummary = $"Lô hàng từ xe tải {bienSoXe} ({soKienThucNhan} kiện - {trongLuongXe:N0} kg)",
                Weight = trongLuongXe > 0 ? trongLuongXe : 100.0,
                IsExpress = false,
                CodAmount = 0,
                ShippingFee = 150000,
                ExpressSurcharge = 0,
                ReceiverPaysFee = false,
                Status = ShippingOrderStatus.NewReceived,
                CreatedDate = DateTime.Now,
                EstimatedDeliveryDate = DateTime.Now.AddHours(24),
                Notes = $"Hàng dỡ từ xe tải {bienSoXe}, Seal: {maSeal}. Đã chuyển vào {viTriKe} chờ điều phối."
            };
            WarehouseContext.Instance.AddShippingOrder(donDaiDienChuyenXe);

            // 3. Ghi vết biến động kho (WarehouseMovement)
            WarehouseContext.Instance.AddWarehouseMovement(new WarehouseMovement
            {
                TransactionCode = $"GD-NK-{DateTime.Now:yyMMddHHmm}",
                Timestamp = DateTime.Now,
                MovementType = WarehouseMovementType.InboundReceiving,
                ItemName = $"Lô hàng xe tải {bienSoXe} ({soKienThucNhan} kiện)",
                ReferenceCode = maPhieu,
                Quantity = soKienThucNhan,
                Weight = trongLuongXe,
                SourceOrDestination = hubGui,
                LocationCode = viTriKe,
                OperatorName = UserSession.Current.CurrentUser?.FullName ?? "Thủ kho",
                Notes = $"Dỡ tại {cuaDock}, Seal: {maSeal}"
            });

            // Cập nhật giao diện
            NapDuLieuNhapKho();

            // Cập nhật huy hiệu đơn chờ trên MainWindow
            if (Application.Current.MainWindow is MainWindow cuaSoChinh)
            {
                cuaSoChinh.CapNhatHuyHieuDonChoXuLy();
            }

            var luaChon = MessageBox.Show(
                $"TIẾP NHẬN CHUYẾN XE TẢI THÀNH CÔNG!\n\n" +
                $"• Mã phiếu nhập: {phieuNhapXeTai.ImportCode}\n" +
                $"• Xe tải: {bienSoXe} ({phieuNhapXeTai.DriverName})\n" +
                $"• Số lượng thực nhận: {soKienThucNhan} / {soKienKhaiBao} kiện (Hư hại: {soKienHong})\n" +
                $"• Tổng tải trọng: {phieuNhapXeTai.TotalWeight:N0} kg (≈ {phieuNhapXeTai.TotalWeight / 1000.0:0.##} Tấn)\n" +
                $"• Vị trí cất giữ chỉ định: {viTriKe}\n\n" +
                $"Lô hàng đã được TỰ ĐỘNG ĐẨY SANG PHÂN HỆ GOM TUYẾN & ĐIỀU PHỐI SHIPPER!\n\n" +
                $"Bạn có muốn chuyển sang màn hình GOM TUYẾN ngay bây giờ không?",
                "Tiếp Nhận Xe Tải Hoàn Tất", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (luaChon == MessageBoxResult.Yes)
            {
                if (Application.Current.MainWindow is MainWindow cuaSoChinh2)
                {
                    cuaSoChinh2.ChuyenSangTrangGomTuyen("Thái Nguyên");
                }
            }
        }
        #endregion

        #region Nghiệp Vụ 2: Nhận Hàng Khách Lẻ Tại Quầy & Kiểm Tra Dữ Liệu Đầu Vào

        private void RetailInputs_Changed(object sender, RoutedEventArgs e)
        {
            TinhToanCuocKhachLe();
        }

        private void RetailInputs_Changed(object sender, TextChangedEventArgs e)
        {
            TinhToanCuocKhachLe();
        }

        private void RetailInputs_Changed(object sender, SelectionChangedEventArgs e)
        {
            TinhToanCuocKhachLe();
        }

        /// <summary>
        /// HÀM LOGIC CỐT LÕI: Tính cước tự động &amp; quy đổi thể tích D x R x C / 5000 cho khách lẻ
        /// </summary>
        private void TinhToanCuocKhachLe()
        {
            if (txtRetailWeight == null || txtRetailLength == null || txtRetailWidth == null || txtRetailHeight == null ||
                txtVolumetricFormula == null || txtBillableWeightText == null || txtRetailShippingFee == null || txtRetailTotalAmount == null)
                return;

            bool coCanNang = ThuPhanTichKhoiLuong(txtRetailWeight.Text, out double canNangThuc);
            bool coChieuDai = ThuPhanTichKhoiLuong(txtRetailLength.Text, out double dai);
            bool coChieuRong = ThuPhanTichKhoiLuong(txtRetailWidth.Text, out double rong);
            bool coChieuCao = ThuPhanTichKhoiLuong(txtRetailHeight.Text, out double cao);

            decimal tienCod = 0;
            if (txtRetailCodAmount != null && !string.IsNullOrWhiteSpace(txtRetailCodAmount.Text))
            {
                decimal.TryParse(txtRetailCodAmount.Text.Replace(".", "").Replace(",", "").Trim(), out tienCod);
                if (tienCod < 0) tienCod = 0;
            }

            // Trường hợp form ở trạng thái mặc định rỗng chưa nhập thông số đo đạc
            if (!coCanNang && !coChieuDai && !coChieuRong && !coChieuCao)
            {
                txtVolumetricFormula.Text = "• Vui lòng nhập trọng lượng và kích thước (D x R x C cm) để tính quy đổi...";
                txtBillableWeightText.Text = "• Trọng lượng tính cước: 0.0 kg (Chờ cân đo)";
                txtRetailShippingFee.Text = "0 đ";
                txtRetailSurcharge.Text = "0 đ";
                if (txtRetailCodDisplay != null) txtRetailCodDisplay.Text = $"{tienCod:N0} đ";
                txtRetailTotalAmount.Text = $"{tienCod:N0} đ";

                if (txtRetailPutAwaySuggestion != null && borderRetailPutAway != null)
                {
                    txtRetailPutAwaySuggestion.Text = "Khuyên đặt: Chờ nhập trọng lượng và loại hàng để gợi ý vị trí ô kệ.";
                    borderRetailPutAway.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                    borderRetailPutAway.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                }
                return;
            }

            if (dai <= 0) dai = 10;
            if (rong <= 0) rong = 10;
            if (cao <= 0) cao = 10;
            if (canNangThuc <= 0) canNangThuc = 0.5;

            // Công thức tính trọng lượng thể tích chuẩn quốc tế: (D x R x C) / 5000
            double canNangQuyDoi = Math.Round((dai * rong * cao) / 5000.0, 1);
            double trongLuongTinhCuoc = Math.Max(canNangThuc, canNangQuyDoi);

            txtVolumetricFormula.Text = $"• Thể tích: ({dai:0} x {rong:0} x {cao:0}) / 5000 = {canNangQuyDoi:N1} kg quy đổi";
            txtBillableWeightText.Text = $"• Trọng lượng tính cước: {trongLuongTinhCuoc:N1} kg (Lấy số lớn hơn)";

            // Cước vận chuyển chính: <= 2kg: 22.000đ, mỗi kg vượt + 5.000đ
            decimal cuocChinh = 22000;
            if (trongLuongTinhCuoc > 2.0)
            {
                int soKgVuot = (int)Math.Ceiling(trongLuongTinhCuoc - 2.0);
                cuocChinh += soKgVuot * 5000;
            }

            // Phụ phí
            bool laHoaToc = rbRetailExpressService?.IsChecked == true;
            bool laDeVo = chkRetailFragile?.IsChecked == true;
            bool laGiaTriCao = chkRetailHighValue?.IsChecked == true;

            decimal phuPhi = 0;
            if (laHoaToc) phuPhi += 20000;
            if (laDeVo) phuPhi += 10000;
            if (laGiaTriCao) phuPhi += 15000;

            // Người chịu cước
            bool nguoiGuiTra = rbRetailSenderPays?.IsChecked == true;
            decimal tongCuocPhi = cuocChinh + phuPhi;
            decimal tongTienKhachTra = nguoiGuiTra ? tongCuocPhi : (tienCod + tongCuocPhi);

            txtRetailShippingFee.Text = $"{cuocChinh:N0} đ";
            txtRetailSurcharge.Text = $"{phuPhi:N0} đ";
            if (txtRetailCodDisplay != null) txtRetailCodDisplay.Text = $"{tienCod:N0} đ";
            txtRetailTotalAmount.Text = $"{tongTienKhachTra:N0} đ";

            // Thuật toán gợi ý vị trí lưu kệ thông minh cho khách lẻ (Smart Put-away)
            if (txtRetailPutAwaySuggestion != null && borderRetailPutAway != null)
            {
                bool laDonNoiVung = cbRetailDestinationArea?.SelectedIndex == 1 || // Thái Nguyên
                                   (txtRetailReceiverAddress?.Text ?? "").ToLower().Contains("thái nguyên") ||
                                   (txtRetailReceiverAddress?.Text ?? "").ToLower().Contains("thịnh đán") ||
                                   (txtRetailReceiverAddress?.Text ?? "").ToLower().Contains("phan đình phùng") ||
                                   (txtRetailReceiverAddress?.Text ?? "").ToLower().Contains("sông công") ||
                                   (txtRetailReceiverAddress?.Text ?? "").ToLower().Contains("phổ yên");

                if (laHoaToc || laDonNoiVung)
                {
                    txtRetailPutAwaySuggestion.Text = "⚡ ĐƠN NỘI VÙNG (THÁI NGUYÊN) - Khuyên đặt: Bàn Soạn Xuất Phát (Cross-Docking Giao Ngay trong ca, KHÔNG cất vào kệ sâu)";
                    borderRetailPutAway.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                    borderRetailPutAway.BorderBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172));
                }
                else if (trongLuongTinhCuoc > 15.0)
                {
                    txtRetailPutAwaySuggestion.Text = $"Khuyên đặt: Khu C - Kệ 01 (Tầng 1 sàn trệt an toàn cho hàng nặng {trongLuongTinhCuoc:N1} kg)";
                    borderRetailPutAway.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                    borderRetailPutAway.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 211, 77));
                }
                else if (laGiaTriCao || cbRetailProductType?.SelectedIndex == 2)
                {
                    txtRetailPutAwaySuggestion.Text = "Khuyên đặt: Khu A - Tủ Khóa An Toàn (Hàng giá trị cao / đồ điện tử)";
                    borderRetailPutAway.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
                    borderRetailPutAway.BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254));
                }
                else
                {
                    txtRetailPutAwaySuggestion.Text = "Khuyên đặt: Khu B - Kệ 02 (Kệ bưu kiện tiêu chuẩn)";
                    borderRetailPutAway.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
                    borderRetailPutAway.BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254));
                }
            }
        }

        /// <summary>
        /// SỰ KIỆN: Điền nhanh mẫu thông tin khách lẻ khi cần demo
        /// </summary>
        private void BtnFillSampleRetail_Click(object sender, RoutedEventArgs e)
        {
            txtRetailSenderName.Text = "Chị Mai Phương";
            txtRetailSenderPhone.Text = "0944888999";
            txtRetailSenderAddress.Text = "Cửa Hàng Thời Trang H&M, Cầu Giấy, Hà Nội";
            txtRetailReceiverName.Text = "Anh Hoàng Nam";
            txtRetailReceiverPhone.Text = "0912345678";
            txtRetailReceiverAddress.Text = "Số 45 Đường Lương Ngọc Quyến, Phường Hoàng Văn Thụ";
            cbRetailDestinationArea.SelectedIndex = 1; // Thái Nguyên
            txtRetailProductName.Text = "Giày thể thao & Quần áo";
            cbRetailProductType.SelectedIndex = 0;
            txtRetailWeight.Text = "1.8";
            txtRetailLength.Text = "32";
            txtRetailWidth.Text = "24";
            txtRetailHeight.Text = "18";
            rbRetailStandardService.IsChecked = true;
            chkRetailFragile.IsChecked = false;
            chkRetailHighValue.IsChecked = false;
            txtRetailCodAmount.Text = "480000";
            rbRetailReceiverPays.IsChecked = true;
            rbRetailPayCash.IsChecked = true;

            XoaTatCaDanhDauLoi();
            TinhToanCuocKhachLe();
            MessageBox.Show("Đã điền nhanh thông tin khách lẻ mẫu gửi hàng đi Thái Nguyên!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// SỰ KIỆN: Lưu đơn nhập kho khách lẻ với KIỂM TRA DỮ LIỆU ĐẦU VÀO CHẶT CHẼ (VALIDATION)
        /// </summary>
        private void BtnSaveRetailInbound_Click(object sender, RoutedEventArgs e)
        {
            var danhSachLoi = new List<string>();
            Control? controlLoiDauTien = null;

            // 1. KIỂM TRA THÔNG TIN BÊN GỬI
            string tenNguoiGui = txtRetailSenderName.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(tenNguoiGui))
            {
                danhSachLoi.Add("Họ tên người gửi: Không được để trống.");
                DanhDauLoi(txtRetailSenderName);
                controlLoiDauTien ??= txtRetailSenderName;
            }

            string sdtGui = txtRetailSenderPhone.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(sdtGui))
            {
                danhSachLoi.Add("Số điện thoại người gửi: Không được để trống.");
                DanhDauLoi(txtRetailSenderPhone);
                controlLoiDauTien ??= txtRetailSenderPhone;
            }
            else if (!KiemTraSoDienThoaiHopLe(sdtGui))
            {
                danhSachLoi.Add("Số điện thoại người gửi: Sai định dạng (cần 10 chữ số, VD: 09xx, 08xx, 03xx...).");
                DanhDauLoi(txtRetailSenderPhone);
                controlLoiDauTien ??= txtRetailSenderPhone;
            }

            string diaChiGui = txtRetailSenderAddress.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(diaChiGui))
            {
                danhSachLoi.Add("Địa chỉ người gửi: Không được để trống (nhập địa chỉ hoặc bưu cục tiếp nhận).");
                DanhDauLoi(txtRetailSenderAddress);
                controlLoiDauTien ??= txtRetailSenderAddress;
            }

            // 2. KIỂM TRA THÔNG TIN BÊN NHẬN
            string tenNguoiNhan = txtRetailReceiverName.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(tenNguoiNhan))
            {
                danhSachLoi.Add("Họ tên người nhận: Không được để trống.");
                DanhDauLoi(txtRetailReceiverName);
                controlLoiDauTien ??= txtRetailReceiverName;
            }

            string sdtNhan = txtRetailReceiverPhone.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(sdtNhan))
            {
                danhSachLoi.Add("Số điện thoại người nhận: Không được để trống.");
                DanhDauLoi(txtRetailReceiverPhone);
                controlLoiDauTien ??= txtRetailReceiverPhone;
            }
            else if (!KiemTraSoDienThoaiHopLe(sdtNhan))
            {
                danhSachLoi.Add("Số điện thoại người nhận: Sai định dạng (cần 10 chữ số).");
                DanhDauLoi(txtRetailReceiverPhone);
                controlLoiDauTien ??= txtRetailReceiverPhone;
            }

            string diaChiNhan = txtRetailReceiverAddress.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(diaChiNhan) || diaChiNhan.Length < 5)
            {
                danhSachLoi.Add("Địa chỉ giao hàng: Không được để trống (cần số nhà, tên đường, phường/xã để Shipper giao).");
                DanhDauLoi(txtRetailReceiverAddress);
                controlLoiDauTien ??= txtRetailReceiverAddress;
            }

            if (cbRetailDestinationArea.SelectedIndex <= 0)
            {
                danhSachLoi.Add("Tỉnh / Thành phố giao đến: Vui lòng chọn địa bàn giao hàng.");
                DanhDauLoi(cbRetailDestinationArea);
                controlLoiDauTien ??= cbRetailDestinationArea;
            }

            // 3. KIỂM TRA THÔNG TIN HÀNG HÓA
            string tenHang = txtRetailProductName.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(tenHang))
            {
                danhSachLoi.Add("Tên mặt hàng: Vui lòng nhập tên hàng hóa / bưu phẩm gửi.");
                DanhDauLoi(txtRetailProductName);
                controlLoiDauTien ??= txtRetailProductName;
            }

            // 4. KIỂM TRA CÂN NẶNG VÀ KÍCH THƯỚC
            if (!ThuPhanTichKhoiLuong(txtRetailWeight.Text, out double canNangThuc) || canNangThuc <= 0)
            {
                danhSachLoi.Add("Cân nặng thực tế: Bắt buộc nhập và phải là số dương lớn hơn 0 kg.");
                DanhDauLoi(txtRetailWeight);
                controlLoiDauTien ??= txtRetailWeight;
            }

            if (!ThuPhanTichKhoiLuong(txtRetailLength.Text, out double dai) || dai <= 0)
            {
                danhSachLoi.Add("Chiều dài bưu kiện: Phải là số dương lớn hơn 0 cm.");
                DanhDauLoi(txtRetailLength);
                controlLoiDauTien ??= txtRetailLength;
            }

            if (!ThuPhanTichKhoiLuong(txtRetailWidth.Text, out double rong) || rong <= 0)
            {
                danhSachLoi.Add("Chiều rộng bưu kiện: Phải là số dương lớn hơn 0 cm.");
                DanhDauLoi(txtRetailWidth);
                controlLoiDauTien ??= txtRetailWidth;
            }

            if (!ThuPhanTichKhoiLuong(txtRetailHeight.Text, out double cao) || cao <= 0)
            {
                danhSachLoi.Add("Chiều cao bưu kiện: Phải là số dương lớn hơn 0 cm.");
                DanhDauLoi(txtRetailHeight);
                controlLoiDauTien ??= txtRetailHeight;
            }

            // 5. KIỂM TRA TIỀN COD
            decimal tienCod = 0;
            if (!string.IsNullOrWhiteSpace(txtRetailCodAmount.Text))
            {
                if (!decimal.TryParse(txtRetailCodAmount.Text.Replace(".", "").Replace(",", "").Trim(), out tienCod) || tienCod < 0)
                {
                    danhSachLoi.Add("Tiền thu hộ COD: Phải là số không âm (>= 0 VNĐ).");
                    DanhDauLoi(txtRetailCodAmount);
                    controlLoiDauTien ??= txtRetailCodAmount;
                }
            }

            // NẾU CÓ BẤT KỲ LỖI NÀO -> DỪNG LẠI & BÁO LỖI
            if (danhSachLoi.Count > 0)
            {
                if (borderRetailValidationAlert != null && txtRetailValidationDetails != null)
                {
                    borderRetailValidationAlert.Visibility = Visibility.Visible;
                    txtRetailValidationDetails.Text = string.Join("\n• ", danhSachLoi);
                }

                MessageBox.Show(
                    $"KIỂM TRA DỮ LIỆU ĐẦU VÀO KHÁCH LẺ THẤT BẠI!\n\n" +
                    $"Phát hiện {danhSachLoi.Count} trường thông tin chưa hợp lệ:\n\n" +
                    string.Join("\n", danhSachLoi.Select((l, i) => $"{i + 1}. {l}")) +
                    $"\n\nVui lòng bổ sung đầy đủ các trường viền đỏ trước khi lưu đơn!",
                    "Cảnh Báo Dữ Liệu Khách Lẻ", MessageBoxButton.OK, MessageBoxImage.Warning);

                controlLoiDauTien?.Focus();
                return;
            }

            if (borderRetailValidationAlert != null)
            {
                borderRetailValidationAlert.Visibility = Visibility.Collapsed;
            }

            // Tính cước vận chuyển
            double canNangQuyDoi = Math.Round((dai * rong * cao) / 5000.0, 1);
            double trongLuongTinhCuoc = Math.Max(canNangThuc, canNangQuyDoi);

            decimal cuocChinh = 22000;
            if (trongLuongTinhCuoc > 2.0)
            {
                int soKgVuot = (int)Math.Ceiling(trongLuongTinhCuoc - 2.0);
                cuocChinh += soKgVuot * 5000;
            }

            bool laHoaToc = rbRetailExpressService?.IsChecked == true;
            bool laDeVo = chkRetailFragile?.IsChecked == true;
            bool laGiaTriCao = chkRetailHighValue?.IsChecked == true;

            decimal phuPhi = 0;
            if (laHoaToc) phuPhi += 20000;
            if (laDeVo) phuPhi += 10000;
            if (laGiaTriCao) phuPhi += 15000;

            bool nguoiNhanTraCuoc = rbRetailReceiverPays?.IsChecked == true;
            string khuVucGiao = (cbRetailDestinationArea.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Thái Nguyên (TP Thái Nguyên)";

            string maVanDon = laHoaToc 
                ? $"LOGIX-EXP-{DateTime.Now:yyMMdd}-{new Random().Next(100, 999)}"
                : $"LOGIX-RET-{DateTime.Now:yyMMdd}-{new Random().Next(100, 999)}";

            string maPhieuNhap = $"NK-LE-{DateTime.Now:yyMMddHHmm}";

            // 1. Tạo phiếu nhập kho ImportOrder
            var phieuNhapKhachLe = new ImportOrder
            {
                ImportCode = maPhieuNhap,
                SourceType = ImportSourceType.Individual,
                SenderName = tenNguoiGui,
                SenderPhone = sdtGui,
                SenderAddress = diaChiGui,
                VehiclePlate = "Khách gửi tại quầy",
                DriverName = "Khách vãng lai",
                WaybillNumber = maVanDon,
                TotalWeight = trongLuongTinhCuoc,
                TotalValue = tienCod > 0 ? tienCod : 500000,
                Status = ImportOrderStatus.Approved,
                CreatedDate = DateTime.Now,
                CreatedByName = UserSession.Current.CurrentUser?.FullName ?? "Giao dịch viên quầy",
                Notes = $"Bưu gửi: {tenHang} ({trongLuongTinhCuoc:N1} kg) | Vị trí: {txtRetailPutAwaySuggestion.Text}"
            };
            WarehouseContext.Instance.AddImportOrder(phieuNhapKhachLe);

            // 2. TỰ ĐỘNG ĐẨY ĐƠN HÀNG SANG PHÂN HỆ VẬN CHUYỂN & GOM TUYẾN & ĐIỀU PHỐI SHIPPER
            var donHangMoi = new ShippingOrder
            {
                OrderCode = maVanDon,
                SenderName = tenNguoiGui,
                SenderPhone = sdtGui,
                SenderAddress = diaChiGui,
                ReceiverName = tenNguoiNhan,
                ReceiverPhone = sdtNhan,
                ReceiverAddress = diaChiNhan,
                DestinationArea = khuVucGiao,
                ProductSummary = $"{tenHang} (KT: {dai:0}x{rong:0}x{cao:0}cm)",
                Weight = trongLuongTinhCuoc,
                IsExpress = laHoaToc,
                CodAmount = tienCod,
                ShippingFee = cuocChinh,
                ExpressSurcharge = phuPhi,
                ReceiverPaysFee = nguoiNhanTraCuoc,
                Status = ShippingOrderStatus.NewReceived,
                CreatedDate = DateTime.Now,
                EstimatedDeliveryDate = laHoaToc ? DateTime.Now.AddHours(4) : DateTime.Now.AddHours(24),
                Notes = $"Tiếp nhận tại quầy bưu cục. {txtRetailPutAwaySuggestion.Text}"
            };
            WarehouseContext.Instance.AddShippingOrder(donHangMoi);

            // 3. Ghi vết biến động kho
            WarehouseContext.Instance.AddWarehouseMovement(new WarehouseMovement
            {
                TransactionCode = $"GD-NK-{DateTime.Now:yyMMddHHmm}",
                Timestamp = DateTime.Now,
                MovementType = WarehouseMovementType.InboundReceiving,
                ItemName = $"{tenHang} ({donHangMoi.OrderCode})",
                ReferenceCode = maVanDon,
                Quantity = 1,
                Weight = trongLuongTinhCuoc,
                SourceOrDestination = diaChiGui,
                LocationCode = laHoaToc ? "Khu Cross-Docking" : (trongLuongTinhCuoc > 15 ? "Khu C - Kệ 01" : "Khu B - Kệ 02"),
                OperatorName = UserSession.Current.CurrentUser?.FullName ?? "Giao dịch viên",
                Notes = $"Khách lẻ gửi hàng đi {khuVucGiao}"
            });

            // Cập nhật huy hiệu đơn chờ trên Sidebar
            if (Application.Current.MainWindow is MainWindow cuaSoChinh)
            {
                cuaSoChinh.CapNhatHuyHieuDonChoXuLy();
            }

            NapDuLieuNhapKho();

            // Đặt lại form về mặc định rỗng sẵn sàng cho bưu gửi tiếp theo
            DatLaiFormKhachLeMacDinh();

            var luaChon = MessageBox.Show(
                $"TIẾP NHẬN BƯU KIỆN KHÁCH LẺ THÀNH CÔNG!\n\n" +
                $"• Mã vận đơn: {donHangMoi.OrderCode}\n" +
                $"• Người gửi: {donHangMoi.SenderName} ({donHangMoi.SenderPhone})\n" +
                $"• Người nhận: {donHangMoi.ReceiverName} ({donHangMoi.DestinationArea})\n" +
                $"• Dịch vụ: {(laHoaToc ? "⚡ HỎA TỐC EXPRESS (2-4h)" : "📦 TIÊU CHUẨN")}\n" +
                $"• Trọng lượng tính cước: {trongLuongTinhCuoc:N1} kg (Thể tích: {canNangQuyDoi:N1} kg)\n" +
                $"• Cước vận chuyển: {cuocChinh:N0} đ (+ Phụ phí: {phuPhi:N0} đ)\n" +
                $"• Tiền thu hộ COD: {tienCod:N0} đ\n" +
                $"• Vị trí cất giữ: {txtRetailPutAwaySuggestion.Text}\n\n" +
                $"Đơn hàng đã được TỰ ĐỘNG ĐẨY SANG PHÂN HỆ VẬN CHUYỂN & GOM TUYẾN!\n\n" +
                $"Bạn có muốn chuyển sang XEM DANH SÁCH ĐƠN HÀNG ngay bây giờ không?",
                "Nhập Kho Khách Lẻ Hoàn Tất", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (luaChon == MessageBoxResult.Yes)
            {
                if (Application.Current.MainWindow is MainWindow cuaSoChinh2)
                {
                    cuaSoChinh2.ChuyenSangTrangDonHang(donHangMoi.OrderCode);
                }
            }
        }

        private void BtnPrintRetailThermalLabel_Click(object sender, RoutedEventArgs e)
        {
            string nguoiGui = string.IsNullOrWhiteSpace(txtRetailSenderName.Text) ? "Người gửi lẻ" : txtRetailSenderName.Text.Trim();
            string sdtGui = string.IsNullOrWhiteSpace(txtRetailSenderPhone.Text) ? "0988 888 888" : txtRetailSenderPhone.Text.Trim();
            string nguoiNhan = string.IsNullOrWhiteSpace(txtRetailReceiverName.Text) ? "Người nhận" : txtRetailReceiverName.Text.Trim();
            string sdtNhan = string.IsNullOrWhiteSpace(txtRetailReceiverPhone.Text) ? "0912 345 678" : txtRetailReceiverPhone.Text.Trim();
            string diaChiNhan = string.IsNullOrWhiteSpace(txtRetailReceiverAddress.Text) ? "TP Thái Nguyên" : txtRetailReceiverAddress.Text.Trim();
            string tenHang = string.IsNullOrWhiteSpace(txtRetailProductName.Text) ? "Bưu kiện hàng hóa" : txtRetailProductName.Text.Trim();
            string khuVuc = (cbRetailDestinationArea.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Thái Nguyên";
            bool laHoaToc = rbRetailExpressService?.IsChecked == true;

            string maVanDon = $"LOGIX-{(laHoaToc ? "EXP" : "RET")}-{DateTime.Now:yyMMdd}-{new Random().Next(100, 999)}";

            string mauInTem = 
                "==========================================================\n" +
                "               LOGIX WMS - PHIẾU GỬI BƯU KIỆN             \n" +
                "              TEM NHIỆT DÁN GÓI HÀNG (100x150mm)          \n" +
                "==========================================================\n\n" +
                $"MÃ VẬN ĐƠN:  {maVanDon}\n" +
                $"DỊCH VỤ:     {(laHoaToc ? "⚡ HỎA TỐC EXPRESS (2-4H)" : "📦 CHUYỂN PHÁT TIÊU CHUẨN")}\n" +
                $"NGÀY TIẾP NHẬN: {DateTime.Now:dd/MM/yyyy HH:mm}\n" +
                "----------------------------------------------------------\n" +
                $"NGƯỜI GỬI:   {nguoiGui} - SĐT: {sdtGui}\n" +
                $"ĐỊA CHỈ GỬI: {txtRetailSenderAddress.Text}\n" +
                "----------------------------------------------------------\n" +
                $"NGƯỜI NHẬN:  {nguoiNhan} - SĐT: {sdtNhan}\n" +
                $"ĐỊA CHỈ NHẬN: {diaChiNhan}\n" +
                $"KHU VỰC:     {khuVuc}\n" +
                "----------------------------------------------------------\n" +
                $"HÀNG HÓA:    {tenHang}\n" +
                $"TRỌNG LƯỢNG: {txtBillableWeightText.Text}\n" +
                $"TIỀN THU COD:{txtRetailCodAmount.Text} VNĐ\n" +
                $"TỔNG CƯỚC:   {txtRetailShippingFee.Text}\n" +
                $"VỊ TRÍ KHO:  {txtRetailPutAwaySuggestion.Text}\n" +
                "----------------------------------------------------------\n" +
                "           |||| |||||| |||||||| |||| ||||||||||           \n" +
                $"                    *{maVanDon}*                         \n" +
                "==========================================================";

            MessageBox.Show(mauInTem, "Xem Trước Tem In Nhiệt Bưu Kiện (100x150mm)", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// SỰ KIỆN: Xóa trắng toàn bộ form khách lẻ về mặc định rỗng
        /// </summary>
        private void BtnResetRetailForm_Click(object sender, RoutedEventArgs e)
        {
            DatLaiFormKhachLeMacDinh();
        }

        /// <summary>
        /// Đưa toàn bộ form nhận hàng lẻ về trạng thái mặc định rỗng
        /// </summary>
        private void DatLaiFormKhachLeMacDinh()
        {
            txtRetailSenderName.Text = "";
            txtRetailSenderPhone.Text = "";
            txtRetailSenderAddress.Text = "";
            txtRetailReceiverName.Text = "";
            txtRetailReceiverPhone.Text = "";
            txtRetailReceiverAddress.Text = "";
            cbRetailDestinationArea.SelectedIndex = 0; // "-- Chọn Tỉnh / Thành Phố Giao Đến --"
            txtRetailProductName.Text = "";
            cbRetailProductType.SelectedIndex = 0;
            txtRetailWeight.Text = "";
            txtRetailLength.Text = "";
            txtRetailWidth.Text = "";
            txtRetailHeight.Text = "";
            txtRetailCodAmount.Text = "0";
            rbRetailStandardService.IsChecked = true;
            rbRetailExpressService.IsChecked = false;
            chkRetailFragile.IsChecked = false;
            chkRetailHighValue.IsChecked = false;
            rbRetailReceiverPays.IsChecked = true;
            rbRetailPayCash.IsChecked = true;

            XoaTatCaDanhDauLoi();
            if (borderRetailValidationAlert != null)
            {
                borderRetailValidationAlert.Visibility = Visibility.Collapsed;
            }

            TinhToanCuocKhachLe();
        }

        #region Các Hàm Hỗ Trợ Kiểm Tra Dữ Liệu & Giao Diện Viền Đỏ
        private bool KiemTraSoDienThoaiHopLe(string sdt)
        {
            if (string.IsNullOrWhiteSpace(sdt)) return false;
            string digits = new string(sdt.Where(char.IsDigit).ToArray());
            if (sdt.StartsWith("+84") && digits.Length == 11) return true;
            if (digits.Length == 10 && digits.StartsWith("0")) return true;
            return false;
        }

        private void DanhDauLoi(Control? ctrl)
        {
            if (ctrl == null) return;
            ctrl.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Đỏ #EF4444
            ctrl.BorderThickness = new Thickness(1.5);
        }

        private void XoaDanhDauLoi(Control? ctrl)
        {
            if (ctrl == null) return;
            ctrl.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // Xám #CBD5E1
            ctrl.BorderThickness = new Thickness(1);
        }

        private void ControlValue_ClearedError(object sender, RoutedEventArgs e)
        {
            if (sender is Control ctrl)
            {
                XoaDanhDauLoi(ctrl);
            }
            if (borderRetailValidationAlert != null)
            {
                borderRetailValidationAlert.Visibility = Visibility.Collapsed;
            }
            if (borderTruckValidationAlert != null)
            {
                borderTruckValidationAlert.Visibility = Visibility.Collapsed;
            }
        }

        private void XoaTatCaDanhDauLoi()
        {
            var dsControls = new Control?[]
            {
                txtRetailSenderName, txtRetailSenderPhone, txtRetailSenderAddress,
                txtRetailReceiverName, txtRetailReceiverPhone, txtRetailReceiverAddress,
                cbRetailDestinationArea, txtRetailProductName,
                txtRetailWeight, txtRetailLength, txtRetailWidth, txtRetailHeight, txtRetailCodAmount,
                cbTruckPlate, txtTruckDriverName, txtTruckDriverPhone, txtTruckManifestCode,
                txtTruckSealCode, txtTruckTotalWeight, txtTruckActualQuantity
            };

            foreach (var c in dsControls)
            {
                if (c != null) XoaDanhDauLoi(c);
            }
        }
        #endregion
        #endregion

        #region Nghiệp Vụ 3: Sổ Nhật Ký Nhập Kho & Tra Cứu Lịch Sử
        private void CbHistoryFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApDungBoLocLichSu();
        }

        private void TxtHistorySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtHistorySearchPlaceholder != null)
            {
                txtHistorySearchPlaceholder.Visibility = string.IsNullOrEmpty(txtHistorySearch.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            ApDungBoLocLichSu();
        }

        private void BtnHistoryResetFilter_Click(object sender, RoutedEventArgs e)
        {
            txtHistorySearch.Text = "";
            cbHistoryFilterType.SelectedIndex = 0;
            ApDungBoLocLichSu();
        }

        private void ApDungBoLocLichSu()
        {
            if (dgImportHistory == null || _danhSachLichSuNhap == null) return;

            int loaiNguonIndex = cbHistoryFilterType?.SelectedIndex ?? 0;
            string tuKhoa = txtHistorySearch?.Text?.Trim().ToLower() ?? "";

            var truyVan = _danhSachLichSuNhap.AsEnumerable();

            if (loaiNguonIndex == 1) // Chỉ xe tải
            {
                truyVan = truyVan.Where(p => p.SourceType == ImportSourceType.TransitHub || p.SourceType == ImportSourceType.Company);
            }
            else if (loaiNguonIndex == 2) // Chỉ khách lẻ
            {
                truyVan = truyVan.Where(p => p.SourceType == ImportSourceType.Individual || p.SourceType == ImportSourceType.Shop);
            }

            if (!string.IsNullOrEmpty(tuKhoa))
            {
                truyVan = truyVan.Where(p =>
                    p.ImportCode.ToLower().Contains(tuKhoa) ||
                    p.SenderName.ToLower().Contains(tuKhoa) ||
                    p.VehiclePlate.ToLower().Contains(tuKhoa) ||
                    p.DriverName.ToLower().Contains(tuKhoa) ||
                    p.Notes.ToLower().Contains(tuKhoa));
            }

            dgImportHistory.ItemsSource = truyVan.ToList();
        }

        private void BtnHistoryExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var danhSachXuat = dgImportHistory.ItemsSource as IEnumerable<ImportOrder> ?? _danhSachLichSuNhap;
                var list = danhSachXuat.ToList();

                if (list.Count == 0)
                {
                    MessageBox.Show("Không có dữ liệu phiếu nhập để xuất báo cáo!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Mã Phiếu,Thời Gian,Nguồn Nhập,Bên Gửi / Xe Tải,Biển Số / Chuyến Xe,Tài Xế,SĐT,Khối Lượng (kg),Trạng Thái,Ghi Chú");

                foreach (var p in list)
                {
                    sb.AppendLine($"\"{p.ImportCode}\",\"{p.CreatedDate:dd/MM/yyyy HH:mm}\",\"{p.SourceTypeName}\",\"{p.SenderName}\",\"{p.VehiclePlate}\",\"{p.DriverName}\",\"{p.SenderPhone}\",{p.TotalWeight},\"{p.StatusDisplayName}\",\"{p.Notes}\"");
                }

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = $"BaoCao_NhapKho_Logistics_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullPath = Path.Combine(desktop, fileName);

                File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(true));

                MessageBox.Show($"XUẤT BÁO CÁO THÀNH CÔNG!\n\n• Tổng số phiếu: {list.Count}\n• Tệp đã lưu tại Desktop:\n{fullPath}",
                    "Báo Cáo Nhập Kho", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xuất file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Điều Hướng Nhanh Sang Các Phân Hệ Khác
        private void BtnJumpToRouteBatching_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow cuaSoChinh)
            {
                cuaSoChinh.ChuyenSangTrangGomTuyen("Thái Nguyên");
            }
        }

        private void BtnJumpToOrders_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow cuaSoChinh)
            {
                cuaSoChinh.ChuyenSangTrangDonHang();
            }
        }
        #endregion
    }
}
