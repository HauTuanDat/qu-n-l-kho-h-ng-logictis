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
    /// PHÂN HỆ QUẢN LÝ NHÂN VIÊN GIAO HÀNG (SHIPPER MANAGEMENT)
    /// - Nhiệm vụ: Quản lý toàn bộ 7 nhánh chức năng điều hành Shipper:
    ///   1. Danh sách Shipper (Thêm, sửa, khóa/mở khóa)
    ///   2. Thêm Shipper mới (Hồ sơ, CCCD, Phương tiện)
    ///   3. Khu vực phụ trách (Thiết lập địa bàn quận/huyện)
    ///   4. Phương tiện đội xe (Kiểm soát xe máy, xe tải)
    ///   5. Năng lực giao hàng (Giới hạn số đơn/ngày, % công suất)
    ///   6. Lịch làm việc (Ca trực, điểm danh vào ca / nghỉ ca)
    ///   7. Lịch sử giao hàng (Theo dõi đơn đã phân công & tiền COD đã thu)
    /// - Đối tượng sử dụng: Admin, Quản lý, Điều phối viên.
    /// - Tương tác dữ liệu: WarehouseContext.cs, Shipper.cs, ShippingOrder.cs.
    /// </summary>
    public partial class ShipperManagementView : UserControl
    {
        private List<Shipper> _danhSachGocShipper = new();
        private int _idShipperDangSua = 0;

        public ShipperManagementView()
        {
            InitializeComponent();
            NapDuLieuShipper();
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp toàn bộ dữ liệu đội ngũ tài xế Shipper và các phân nhánh
        /// </summary>
        public void NapDuLieuShipper()
        {
            var khoDuLieu = WarehouseContext.Instance;
            _danhSachGocShipper = khoDuLieu.GetAllShippers().ToList();
            var tatCaDonHang = khoDuLieu.GetAllShippingOrders();

            // 1. CẬP NHẬT 4 THẺ KPI ĐẦU TRANG
            int tongSoShipper = _danhSachGocShipper.Count;
            int shipperDangHoatDong = _danhSachGocShipper.Count(s => !s.IsLocked && (s.Status == ShipperStatus.Active || s.Status == ShipperStatus.Available));
            int tongDonDangGiao = _danhSachGocShipper.Sum(s => s.ActiveDeliveringCount);
            double diemTrungBinh = _danhSachGocShipper.Count > 0 ? _danhSachGocShipper.Average(s => s.Rating) : 5.0;

            txtTongSoShipper.Text = $"{tongSoShipper} tài xế";
            txtShipperDangHoatDong.Text = $"{shipperDangHoatDong} tài xế";
            txtDonDangPhanCong.Text = $"{tongDonDangGiao} đơn hàng";
            txtDanhGiaTrungBinh.Text = $"⭐ {diemTrungBinh:N1} / 5.0";

            // 2. NẠP DỮ LIỆU CÁC BẢNG DATAGRID
            ApDungBoLocShipper();
            dgKhuVucPhuTrach.ItemsSource = _danhSachGocShipper;
            dgPhuongTien.ItemsSource = _danhSachGocShipper;
            dgNangLucShipper.ItemsSource = _danhSachGocShipper;
            dgLichLamViec.ItemsSource = _danhSachGocShipper;

            // 3. NẠP DỮ LIỆU CÁC COMBOBOX PHỤ TRỢ
            cbDoiTuyenShipper.Items.Clear();
            cbNangLucShipper.Items.Clear();
            cbLocLichSuShipper.Items.Clear();

            cbLocLichSuShipper.Items.Add(new ComboBoxItem { Content = "-- Tất Cả Shipper --", Tag = 0 });

            foreach (var taiXe in _danhSachGocShipper)
            {
                cbDoiTuyenShipper.Items.Add(new ComboBoxItem { Content = $"{taiXe.FullName} ({taiXe.Phone}) - Tuyến: {taiXe.DeliveryArea}", Tag = taiXe.Id });
                cbNangLucShipper.Items.Add(new ComboBoxItem { Content = $"{taiXe.FullName} (Đang gán: {taiXe.MaxOrdersPerDay} đơn/ngày)", Tag = taiXe.Id });
                cbLocLichSuShipper.Items.Add(new ComboBoxItem { Content = $"🛵 {taiXe.FullName} ({taiXe.VehiclePlate})", Tag = taiXe.Id });
            }

            if (cbDoiTuyenShipper.Items.Count > 0) cbDoiTuyenShipper.SelectedIndex = 0;
            if (cbNangLucShipper.Items.Count > 0) cbNangLucShipper.SelectedIndex = 0;
            if (cbLocLichSuShipper.Items.Count > 0) cbLocLichSuShipper.SelectedIndex = 0;

            // Mặc định nạp lịch sử đơn hàng
            CapNhatLichSuDonShipper(0);
        }

        public void LoadData() => NapDuLieuShipper();

        /// <summary>
        /// SỰ KIỆN: Chuyển đổi giữa 7 nhánh chức năng qua RadioButton
        /// </summary>
        private void TabShipper_Checked(object sender, RoutedEventArgs e)
        {
            if (panelDanhSachShipper == null) return;

            // Ẩn tất cả các panel
            panelDanhSachShipper.Visibility = Visibility.Collapsed;
            panelThemShipper.Visibility = Visibility.Collapsed;
            panelKhuVucPhuTrach.Visibility = Visibility.Collapsed;
            panelPhuongTien.Visibility = Visibility.Collapsed;
            panelNangLucGiaoHang.Visibility = Visibility.Collapsed;
            panelLichLamViec.Visibility = Visibility.Collapsed;
            panelLichSuGiaoHang.Visibility = Visibility.Collapsed;

            // Bật panel được chọn
            if (tabDanhSachShipper.IsChecked == true) panelDanhSachShipper.Visibility = Visibility.Visible;
            else if (tabThemShipper.IsChecked == true) panelThemShipper.Visibility = Visibility.Visible;
            else if (tabKhuVucPhuTrach.IsChecked == true) panelKhuVucPhuTrach.Visibility = Visibility.Visible;
            else if (tabPhuongTien.IsChecked == true) panelPhuongTien.Visibility = Visibility.Visible;
            else if (tabNangLucGiaoHang.IsChecked == true) panelNangLucGiaoHang.Visibility = Visibility.Visible;
            else if (tabLichLamViec.IsChecked == true) panelLichLamViec.Visibility = Visibility.Visible;
            else if (tabLichSuGiaoHang.IsChecked == true) panelLichSuGiaoHang.Visibility = Visibility.Visible;
        }

        #region Nhánh 1: Danh Sách Shipper & Tìm Kiếm / Lọc
        private void TxtTimKiemShipper_TextChanged(object sender, TextChangedEventArgs e) => ApDungBoLocShipper();
        private void CbLocTrangThaiShipper_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApDungBoLocShipper();

        private void ApDungBoLocShipper()
        {
            if (dgDanhSachShipper == null) return;

            string tuKhoa = (txtTimKiemShipper?.Text ?? "").Trim().ToLower();
            int trangThaiIndex = cbLocTrangThaiShipper?.SelectedIndex ?? 0;

            var ketQuaLoc = _danhSachGocShipper.Where(s =>
            {
                bool khopTuKhoa = string.IsNullOrEmpty(tuKhoa) ||
                                  s.FullName.ToLower().Contains(tuKhoa) ||
                                  s.Phone.Contains(tuKhoa) ||
                                  s.VehiclePlate.ToLower().Contains(tuKhoa) ||
                                  s.DeliveryArea.ToLower().Contains(tuKhoa);

                bool khopTrangThai = trangThaiIndex switch
                {
                    1 => !s.IsLocked && s.Status == ShipperStatus.Active,
                    2 => !s.IsLocked && s.Status == ShipperStatus.Available,
                    3 => !s.IsLocked && s.Status == ShipperStatus.Offline,
                    4 => s.IsLocked,
                    _ => true
                };

                return khopTuKhoa && khopTrangThai;
            }).ToList();

            dgDanhSachShipper.ItemsSource = ketQuaLoc;
        }

        /// <summary>
        /// SỰ KIỆN: Khóa hoặc Mở khóa tài khoản Shipper
        /// </summary>
        private void BtnKhoaShipper_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is Shipper taiXe)
            {
                string hanhDong = taiXe.IsLocked ? "mở khóa" : "tạm khóa";
                var xacNhan = MessageBox.Show(
                    $"Bạn có chắc chắn muốn {hanhDong} tài khoản Shipper [{taiXe.FullName}]?\n\n" +
                    (taiXe.IsLocked ? "Tài xế sẽ có thể nhận đơn trở lại." : "Tài xế sẽ bị ngừng nhận đơn ngay lập tức."),
                    "Xác Nhận Thay Đổi Trạng Thái", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (xacNhan == MessageBoxResult.Yes)
                {
                    WarehouseContext.Instance.ToggleShipperLock(taiXe.Id);
                    NapDuLieuShipper();
                }
            }
        }

        /// <summary>
        /// SỰ KIỆN: Mở Modal chỉnh sửa thông tin Shipper
        /// </summary>
        private void BtnSuaShipper_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is Shipper taiXe)
            {
                _idShipperDangSua = taiXe.Id;
                txtSuaHoTen.Text = taiXe.FullName;
                txtSuaSdt.Text = taiXe.Phone;
                txtSuaBienSo.Text = taiXe.VehiclePlate;
                txtSuaKhuVuc.Text = taiXe.DeliveryArea;
                txtSuaGioiHanDon.Text = taiXe.MaxOrdersPerDay.ToString();

                modalSuaShipper.Visibility = Visibility.Visible;
            }
        }

        private void BtnDongModalSua_Click(object sender, RoutedEventArgs e)
        {
            modalSuaShipper.Visibility = Visibility.Collapsed;
        }

        private void BtnXacNhanSuaShipper_Click(object sender, RoutedEventArgs e)
        {
            string hoTen = txtSuaHoTen.Text.Trim();
            string sdt = txtSuaSdt.Text.Trim();
            string bienSo = txtSuaBienSo.Text.Trim();
            string khuVuc = txtSuaKhuVuc.Text.Trim();

            if (string.IsNullOrWhiteSpace(hoTen) || string.IsNullOrWhiteSpace(sdt))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Họ tên và Số điện thoại!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int.TryParse(txtSuaGioiHanDon.Text, out int gioiHanDon);
            if (gioiHanDon <= 0) gioiHanDon = 25;

            var taiXe = _danhSachGocShipper.FirstOrDefault(s => s.Id == _idShipperDangSua);
            if (taiXe != null)
            {
                taiXe.FullName = hoTen;
                taiXe.Phone = sdt;
                taiXe.VehiclePlate = bienSo;
                taiXe.DeliveryArea = khuVuc;
                taiXe.MaxOrdersPerDay = gioiHanDon;

                WarehouseContext.Instance.UpdateShipperAreaAndLimit(_idShipperDangSua, khuVuc, gioiHanDon);
            }

            modalSuaShipper.Visibility = Visibility.Collapsed;
            MessageBox.Show("Đã cập nhật thông tin Shipper thành công!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
            NapDuLieuShipper();
        }

        private void BtnXemLichSuShipper_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is Shipper taiXe)
            {
                tabLichSuGiaoHang.IsChecked = true;
                // Chọn tài xế trong combobox lịch sử
                for (int i = 0; i < cbLocLichSuShipper.Items.Count; i++)
                {
                    if (cbLocLichSuShipper.Items[i] is ComboBoxItem item && item.Tag is int id && id == taiXe.Id)
                    {
                        cbLocLichSuShipper.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        #endregion

        #region Nhánh 2: Thêm Shipper Mới
        private void BtnLuuDangKyShipper_Click(object sender, RoutedEventArgs e)
        {
            string hoTen = txtThemHoTen.Text.Trim();
            string sdt = txtThemSdt.Text.Trim();
            string cccd = txtThemCccd.Text.Trim();
            string loaiXe = (cbThemLoaiXe.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Xe máy Honda Wave";
            string bienSo = txtThemBienSo.Text.Trim();
            string khuVuc = (cbThemKhuVuc.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cầu Giấy - Nam Từ Liêm";
            string caLamViec = (cbThemCaLamViec.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ca Sáng (07:00 - 15:00)";

            if (string.IsNullOrWhiteSpace(hoTen) || string.IsNullOrWhiteSpace(sdt) || string.IsNullOrWhiteSpace(bienSo))
            {
                MessageBox.Show("Vui lòng điền đầy đủ Họ tên, Số điện thoại và Biển kiểm soát xe!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double.TryParse(txtThemTaiTrong.Text, out double taiTrong);
            if (taiTrong <= 0) taiTrong = 50.0;

            int.TryParse(txtThemGioiHanDon.Text, out int gioiHanDon);
            if (gioiHanDon <= 0) gioiHanDon = 25;

            var taiXeMoi = new Shipper
            {
                FullName = hoTen,
                Phone = sdt,
                CitizenId = cccd,
                VehicleType = loaiXe,
                VehiclePlate = bienSo,
                DeliveryArea = khuVuc,
                WorkShift = caLamViec,
                MaxWeightCapacity = taiTrong,
                MaxOrdersPerDay = gioiHanDon,
                Status = ShipperStatus.Available,
                CompletedTodayCount = 0,
                Rating = 5.0
            };

            WarehouseContext.Instance.AddShipper(taiXeMoi);

            MessageBox.Show(
                $"ĐÃ ĐĂNG KÝ SHIPPER MỚI THÀNH CÔNG!\n\n" +
                $"• Họ và tên: {hoTen}\n" +
                $"• Số điện thoại: {sdt}\n" +
                $"• Phương tiện: {loaiXe} ({bienSo})\n" +
                $"• Tuyến phụ trách: {khuVuc}\n" +
                $"• Ca làm việc: {caLamViec}\n\n" +
                $"Tài xế đã sẵn sàng tiếp nhận điều phối đơn hàng.",
                "Đăng Ký Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

            NapDuLieuShipper();
            tabDanhSachShipper.IsChecked = true;
        }
        #endregion

        #region Nhánh 3: Khu Vực Phụ Trách
        private void BtnCapNhatKhuVuc_Click(object sender, RoutedEventArgs e)
        {
            if (cbDoiTuyenShipper.SelectedItem is not ComboBoxItem mucShipper || mucShipper.Tag is not int maTaiXe)
            {
                MessageBox.Show("Vui lòng chọn Shipper cần chuyển tuyến!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string khuVucMoi = (cbTuyenMoiGán.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cầu Giấy - Nam Từ Liêm";
            var taiXe = _danhSachGocShipper.FirstOrDefault(s => s.Id == maTaiXe);
            int gioiHanHienTai = taiXe?.MaxOrdersPerDay ?? 25;

            WarehouseContext.Instance.UpdateShipperAreaAndLimit(maTaiXe, khuVucMoi, gioiHanHienTai);

            MessageBox.Show($"Đã cập nhật tuyến phụ trách mới [{khuVucMoi}] cho Shipper {taiXe?.FullName} thành công!", "Cập Nhật Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            NapDuLieuShipper();
        }
        #endregion

        #region Nhánh 5: Năng Lực & Hạn Mức Đơn
        private void BtnLuuDinhMucDon_Click(object sender, RoutedEventArgs e)
        {
            if (cbNangLucShipper.SelectedItem is not ComboBoxItem mucShipper || mucShipper.Tag is not int maTaiXe)
            {
                MessageBox.Show("Vui lòng chọn Shipper cần cài đặt hạn mức!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtDinhMucDonMoi.Text, out int dinhMucMoi) || dinhMucMoi <= 0)
            {
                MessageBox.Show("Định mức đơn tối đa phải là số nguyên dương!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var taiXe = _danhSachGocShipper.FirstOrDefault(s => s.Id == maTaiXe);
            string khuVucHienTai = taiXe?.DeliveryArea ?? "Khu Vực Chung";

            WarehouseContext.Instance.UpdateShipperAreaAndLimit(maTaiXe, khuVucHienTai, dinhMucMoi);

            MessageBox.Show($"Đã thiết lập định mức mới [{dinhMucMoi} đơn/ngày] cho Shipper {taiXe?.FullName} thành công!", "Thiết Lập Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            NapDuLieuShipper();
        }
        #endregion

        #region Nhánh 6: Lịch Làm Việc & Ca Trực
        private void BtnDiemDanhTrucCa_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is Shipper taiXe)
            {
                WarehouseContext.Instance.UpdateShipperShiftAndStatus(taiXe.Id, taiXe.WorkShift, ShipperStatus.Available);
                MessageBox.Show($"Điểm danh thành công! Shipper {taiXe.FullName} đã sẵn sàng nhận đơn trong {taiXe.WorkShift}.", "Điểm Danh Ca Trực", MessageBoxButton.OK, MessageBoxImage.Information);
                NapDuLieuShipper();
            }
        }

        private void BtnNghiCa_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button nut && nut.DataContext is Shipper taiXe)
            {
                WarehouseContext.Instance.UpdateShipperShiftAndStatus(taiXe.Id, taiXe.WorkShift, ShipperStatus.Offline);
                MessageBox.Show($"Shipper {taiXe.FullName} đã chuyển sang trạng thái nghỉ ca.", "Nghỉ Ca", MessageBoxButton.OK, MessageBoxImage.Information);
                NapDuLieuShipper();
            }
        }
        #endregion

        #region Nhánh 7: Lịch Sử Giao Hàng & Xuất Báo Cáo CSV
        private void CbLocLichSuShipper_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbLocLichSuShipper?.SelectedItem is ComboBoxItem mucChon && mucChon.Tag is int maTaiXe)
            {
                CapNhatLichSuDonShipper(maTaiXe);
            }
        }

        private void CapNhatLichSuDonShipper(int maTaiXe)
        {
            if (dgLichSuDonShipper == null) return;

            var tatCaDon = WarehouseContext.Instance.GetAllShippingOrders();
            var donLoc = maTaiXe == 0 
                ? tatCaDon.Where(d => d.AssignedShipperId != null).ToList() 
                : tatCaDon.Where(d => d.AssignedShipperId == maTaiXe).ToList();

            dgLichSuDonShipper.ItemsSource = donLoc;
        }

        private void BtnXuatLichSuShipperCsv_Click(object sender, RoutedEventArgs e)
        {
            if (dgLichSuDonShipper.ItemsSource is not IEnumerable<ShippingOrder> danhSachDon || !danhSachDon.Any())
            {
                MessageBox.Show("Không có dữ liệu đơn hàng để xuất báo cáo!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hopThoaiLuu = new SaveFileDialog
            {
                Filter = "Tệp CSV Báo Cáo (*.csv)|*.csv",
                FileName = $"BaoCao_LichSuGiaoHang_Shipper_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Lưu Báo Cáo Lịch Sử Giao Hàng Của Shipper"
            };

            if (hopThoaiLuu.ShowDialog() == true)
            {
                try
                {
                    var noiDungCsv = new StringBuilder();
                    noiDungCsv.AppendLine("Mã Vận Đơn;Shipper Phụ Trách;Khách Nhận;Số Điện Thoại;Địa Chỉ Giao Hàng;Hàng Hóa;Dịch Vụ;Thu Hộ COD;Cước Vận Chuyển;Trạng Thái;Hạn SLA");

                    foreach (var d in danhSachDon)
                    {
                        string dichVu = d.IsExpress ? "Express Hỏa Tốc" : "Tiêu Chuẩn";
                        noiDungCsv.AppendLine($"{d.OrderCode};{d.AssignedShipperName};{d.ReceiverName};{d.ReceiverPhone};{d.ReceiverAddress.Replace(';', ',')};{d.ProductSummary};{d.Weight};{dichVu};{d.CodAmount};{d.ShippingFee};{d.StatusDisplayName};{d.EstimatedDeliveryDate:dd/MM/yyyy HH:mm}");
                    }

                    File.WriteAllText(hopThoaiLuu.FileName, noiDungCsv.ToString(), new UTF8Encoding(true));
                    MessageBox.Show($"Đã xuất báo cáo lịch sử giao hàng thành công ra file:\n{hopThoaiLuu.FileName}", "Xuất Báo Cáo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ngoaiLe)
                {
                    MessageBox.Show($"Lỗi khi ghi file: {ngoaiLe.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        private void BtnLamMoiShipper_Click(object sender, RoutedEventArgs e)
        {
            NapDuLieuShipper();
        }
    }
}
