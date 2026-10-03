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
    /// PHÂN HỆ NGHIỆP VỤ (TÍNH NĂNG 5): Gom Đơn Theo Tuyến Đường & Tối Ưu Lộ Trình Giao Hàng (Route Batching)
    /// - Nhiệm vụ: Tự động gom các đơn hàng theo từng cụm địa bàn quận/huyện,
    ///             tối ưu hóa thứ tự các điểm dừng (đơn Express lên đầu),
    ///             và điều phối hàng loạt cho Shipper chỉ với 1 click.
    /// - Tương tác dữ liệu: WarehouseContext.cs, ShippingOrder.cs, RouteBatch.cs, Shipper.cs.
    /// </summary>
    public partial class RouteBatchingView : UserControl
    {
        private List<RouteBatch> _danhSachTuyenGom = new();
        private RouteBatch? _tuyenDangChon = null;

        public RouteBatchingView()
        {
            InitializeComponent();
            NapDuLieuGomTuyen();
        }

        /// <summary>
        /// HÀM LOGIC: Phân tích toàn bộ đơn hàng và nhóm thành các tuyến giao tối ưu
        /// - Nhiệm vụ:
        ///   1. Lấy toàn bộ đơn hàng từ WarehouseContext.
        ///   2. Phân loại theo từ khóa địa chỉ (Quận Cầu Giấy, Đống Đa, Ba Đình, Hoàn Kiếm, v.v.).
        ///   3. Khởi tạo các RouteBatch với danh sách đơn tương ứng.
        ///   4. Tính toán tổng số đơn, tải trọng kg, tổng COD và hiển thị lên các thẻ KPI.
        ///   5. Nạp danh bạ Shipper vào ComboBox điều phối.
        /// - Tương tác dữ liệu: WarehouseContext.Instance, _danhSachTuyenGom, lvDanhSachTuyen.
        /// </summary>
        public void NapDuLieuGomTuyen()
        {
            var tatCaDonHang = WarehouseContext.Instance.GetAllShippingOrders();
            var danhSachTaiXe = WarehouseContext.Instance.GetAllShippers().Where(s => s.Status == ShipperStatus.Active).ToList();

            // Khởi tạo các tuyến đường trọng điểm
            var tuyenCauGiay = new RouteBatch
            {
                TenTuyenDuong = "Tuyến Cầu Giấy - Nam Từ Liêm",
                MaTuyen = "TUYEN-CG-NTL",
                BieuTuongTuyen = "🏢",
                ShipperGoiY = "Trần Đình Trọng (Xe máy)"
            };

            var tuyenDongDa = new RouteBatch
            {
                TenTuyenDuong = "Tuyến Đống Đa - Ba Đình",
                MaTuyen = "TUYEN-DD-BD",
                BieuTuongTuyen = "🏛️",
                ShipperGoiY = "Nguyễn Văn Tuấn (Xe máy)"
            };

            var tuyenHoanKiem = new RouteBatch
            {
                TenTuyenDuong = "Tuyến Hoàn Kiếm - Hai Bà Trưng",
                MaTuyen = "TUYEN-HK-HBT",
                BieuTuongTuyen = "🌆",
                ShipperGoiY = "Vũ Đình Duy (Xe máy)"
            };

            var tuyenHaDong = new RouteBatch
            {
                TenTuyenDuong = "Tuyến Hà Đông - Thanh Xuân",
                MaTuyen = "TUYEN-HD-TX",
                BieuTuongTuyen = "🚚",
                ShipperGoiY = "Lê Hoàng Long (Xe tải 1.25T)"
            };

            var tuyenThaiNguyen = new RouteBatch
            {
                TenTuyenDuong = "Tuyến Thái Nguyên - Trung Chuyển Liên Tỉnh",
                MaTuyen = "TUYEN-TN-LIENTINH",
                BieuTuongTuyen = "🚛",
                ShipperGoiY = "Vũ Đình Nam (Xe tải/Container)"
            };

            var tuyenNgoaiThanh = new RouteBatch
            {
                TenTuyenDuong = "Tuyến Tây Hồ - Hoàng Mai - Khác",
                MaTuyen = "TUYEN-TH-HM",
                BieuTuongTuyen = "📦",
                ShipperGoiY = "Phân phối linh hoạt"
            };

            // Phân loại đơn hàng vào từng tuyến dựa theo địa chỉ
            foreach (var donHang in tatCaDonHang)
            {
                string diaChi = (donHang.ReceiverAddress + " " + donHang.DestinationArea).ToLower();

                if (diaChi.Contains("thái nguyên") || diaChi.Contains("sông công") || diaChi.Contains("phổ yên") || diaChi.Contains("thịnh đán") || diaChi.Contains("liên tỉnh"))
                {
                    tuyenThaiNguyen.DanhSachDonHang.Add(donHang);
                }
                else if (diaChi.Contains("cầu giấy") || diaChi.Contains("duy tân") || diaChi.Contains("xuân thủy") || diaChi.Contains("keangnam") || diaChi.Contains("từ liêm"))
                {
                    tuyenCauGiay.DanhSachDonHang.Add(donHang);
                }
                else if (diaChi.Contains("đống đa") || diaChi.Contains("chùa láng") || diaChi.Contains("láng hạ") || diaChi.Contains("ba đình") || diaChi.Contains("giảng võ") || diaChi.Contains("đội cấn") || diaChi.Contains("lotte"))
                {
                    tuyenDongDa.DanhSachDonHang.Add(donHang);
                }
                else if (diaChi.Contains("hoàn kiếm") || diaChi.Contains("lý thường kiệt") || diaChi.Contains("hai bà trưng") || diaChi.Contains("phố huế") || diaChi.Contains("bà triệu"))
                {
                    tuyenHoanKiem.DanhSachDonHang.Add(donHang);
                }
                else if (diaChi.Contains("hà đông") || diaChi.Contains("thanh xuân") || diaChi.Contains("nguyễn trãi") || diaChi.Contains("khuất duy tiến"))
                {
                    tuyenHaDong.DanhSachDonHang.Add(donHang);
                }
                else
                {
                    tuyenNgoaiThanh.DanhSachDonHang.Add(donHang);
                }
            }

            _danhSachTuyenGom = new List<RouteBatch>
            {
                tuyenThaiNguyen,
                tuyenCauGiay,
                tuyenDongDa,
                tuyenHoanKiem,
                tuyenHaDong,
                tuyenNgoaiThanh
            };

            // Cập nhật các thẻ KPI tổng quát
            int tongSoDon = _danhSachTuyenGom.Sum(t => t.TongSoDon);
            int soTuyenCoDon = _danhSachTuyenGom.Count(t => t.TongSoDon > 0);
            double tongKhoiLuong = _danhSachTuyenGom.Sum(t => t.TongTrongLuongKg);
            decimal tongCod = _danhSachTuyenGom.Sum(t => t.TongTienCod);

            txtTongDonChoGom.Text = tongSoDon.ToString();
            txtSoTuyenHoatDong.Text = soTuyenCoDon.ToString();
            txtTongKhoiLuongKg.Text = $"{tongKhoiLuong:N1} kg";
            txtTongTienCodTuyen.Text = $"{tongCod:N0} đ";
            txtSoLuongTuyenBadge.Text = $"{_danhSachTuyenGom.Count} tuyến";

            // Nạp danh sách tuyến vào ListView bên trái
            lvDanhSachTuyen.ItemsSource = null;
            lvDanhSachTuyen.ItemsSource = _danhSachTuyenGom;

            // Nạp danh bạ Shipper vào ComboBox
            cbShipperTuyen.Items.Clear();
            foreach (var taiXe in danhSachTaiXe)
            {
                cbShipperTuyen.Items.Add(new ComboBoxItem
                {
                    Content = $"🛵 {taiXe.FullName} ({taiXe.VehicleType} - {taiXe.VehiclePlate}) [Khu vực: {taiXe.DeliveryArea}]",
                    Tag = taiXe.Id
                });
            }

            if (cbShipperTuyen.Items.Count > 0)
            {
                cbShipperTuyen.SelectedIndex = 0;
            }

            // Mặc định chọn tuyến đầu tiên có đơn
            var tuyenMacDinh = _danhSachTuyenGom.FirstOrDefault(t => t.TongSoDon > 0) ?? _danhSachTuyenGom[0];
            lvDanhSachTuyen.SelectedItem = tuyenMacDinh;
        }

        /// <summary>
        /// SỰ KIỆN: Người dùng chọn một tuyến từ danh sách bên trái
        /// - Nhiệm vụ: Hiển thị thứ tự lộ trình các điểm dừng tối ưu sang bảng bên phải.
        /// - Tương tác dữ liệu: lvDanhSachTuyen, dgDiemDungLoTrinh.
        /// </summary>
        private void LvDanhSachTuyen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvDanhSachTuyen.SelectedItem is not RouteBatch tuyenDuocChon) return;

            _tuyenDangChon = tuyenDuocChon;
            txtChiTietTenTuyen.Text = $"{tuyenDuocChon.BieuTuongTuyen} {tuyenDuocChon.TenTuyenDuong}";
            badgeChiTietSoDon.Visibility = Visibility.Visible;
            txtBadgeSoDonText.Text = $"{tuyenDuocChon.TongSoDon} đơn • {tuyenDuocChon.TongTrongLuongKg:N1} kg";

            // Sắp xếp thứ tự các điểm dừng tối ưu
            var danhSachDiemDung = tuyenDuocChon.LayDanhSachDiemDungToiUu();
            dgDiemDungLoTrinh.ItemsSource = danhSachDiemDung;

            // Tự động gợi ý chọn Shipper phù hợp trong ComboBox
            for (int chiSo = 0; chiSo < cbShipperTuyen.Items.Count; chiSo++)
            {
                if (cbShipperTuyen.Items[chiSo] is ComboBoxItem mucShipper)
                {
                    string thongTinShipper = mucShipper.Content?.ToString() ?? "";
                    if (tuyenDuocChon.TenTuyenDuong.Contains("Cầu Giấy") && thongTinShipper.Contains("Trần Đình Trọng"))
                    {
                        cbShipperTuyen.SelectedIndex = chiSo;
                        break;
                    }
                    else if (tuyenDuocChon.TenTuyenDuong.Contains("Đống Đa") && thongTinShipper.Contains("Nguyễn Văn Tuấn"))
                    {
                        cbShipperTuyen.SelectedIndex = chiSo;
                        break;
                    }
                    else if (tuyenDuocChon.TenTuyenDuong.Contains("Hà Đông") && thongTinShipper.Contains("Lê Hoàng Long"))
                    {
                        cbShipperTuyen.SelectedIndex = chiSo;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// TIỆN ÍCH CÔNG KHAI: Tự động tìm và chọn tuyến giao hàng theo từ khóa địa bàn (VD: "Thái Nguyên", "Cầu Giấy"...)
        /// </summary>
        public void ChonTuyenTheoKhuVuc(string tuKhoaKhuVuc)
        {
            if (_danhSachTuyenGom == null || _danhSachTuyenGom.Count == 0 || lvDanhSachTuyen == null) return;

            string tk = (tuKhoaKhuVuc ?? "").Trim().ToLower();
            if (string.IsNullOrEmpty(tk)) return;

            var tuyenPhuHop = _danhSachTuyenGom.FirstOrDefault(t => 
                t.TenTuyenDuong.ToLower().Contains(tk) ||
                t.MaTuyen.ToLower().Contains(tk) ||
                t.DanhSachDonHang.Any(d => (d.ReceiverAddress + " " + d.DestinationArea).ToLower().Contains(tk)))
                ?? _danhSachTuyenGom.FirstOrDefault(t => t.TongSoDon > 0)
                ?? _danhSachTuyenGom[0];

            lvDanhSachTuyen.SelectedItem = tuyenPhuHop;
        }

        /// <summary>
        /// SỰ KIỆN: Bấm nút "Phát Hành Chuyến Giao (Batch Dispatch)"
        /// - Nhiệm vụ: Gán Shipper đã chọn cho TOÀN BỘ đơn hàng thuộc tuyến,
        ///             chuyển trạng thái sang "Đang giao", cập nhật SQL Server và hiển thị thông báo.
        /// - Tương tác dữ liệu: WarehouseContext.Instance.DieuPhoiGomChuyenTuyen.
        /// </summary>
        private void BtnPhatHanhChuyenGom_Click(object sender, RoutedEventArgs e)
        {
            if (_tuyenDangChon == null || _tuyenDangChon.TongSoDon == 0)
            {
                MessageBox.Show("Vui lòng chọn một tuyến đường có ít nhất 1 đơn hàng để phát hành chuyến giao!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cbShipperTuyen.SelectedItem is not ComboBoxItem mucTaiXe || mucTaiXe.Tag is not int maTaiXe)
            {
                MessageBox.Show("Vui lòng chọn Shipper phụ trách chuyến gom này!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var taiXe = WarehouseContext.Instance.GetAllShippers().FirstOrDefault(s => s.Id == maTaiXe);
            string tenTaiXe = taiXe?.FullName ?? "Shipper Hệ Thống";
            string sdtTaiXe = taiXe?.Phone ?? "";

            // Kiểm tra cảnh báo quá tải nếu đi xe máy
            if (_tuyenDangChon.CanhBaoQuaTai && taiXe?.VehicleType.Contains("Xe máy") == true)
            {
                var xacNhan = MessageBox.Show(
                    $"CẢNH BÁO QUÁ TẢI TRỌNG XE MÁY!\n\n" +
                    $"Tuyến '{_tuyenDangChon.TenTuyenDuong}' có tổng khối lượng {_tuyenDangChon.TongTrongLuongKg:N1} kg (vượt mức an toàn 45kg của xe máy).\n" +
                    $"Shipper được chọn ({tenTaiXe}) đang sử dụng xe máy.\n\n" +
                    $"Bạn có chắc chắn vẫn muốn điều phối chuyến này?",
                    "Cảnh Báo Sức Tải Vận Chuyển", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (xacNhan != MessageBoxResult.Yes) return;
            }

            var danhSachMaDon = _tuyenDangChon.DanhSachDonHang.Select(d => d.Id).ToList();

            // Thực thi điều phối hàng loạt trong WarehouseContext
            WarehouseContext.Instance.DieuPhoiGomChuyenTuyen(danhSachMaDon, maTaiXe, tenTaiXe, sdtTaiXe, _tuyenDangChon.TenTuyenDuong);

            MessageBox.Show(
                $"ĐÃ PHÁT HÀNH THÀNH CÔNG CHUYẾN GOM LỘ TRÌNH!\n\n" +
                $"• Tuyến đường: {_tuyenDangChon.TenTuyenDuong}\n" +
                $"• Số lượng bưu kiện: {_tuyenDangChon.TongSoDon} đơn\n" +
                $"• Tài xế phụ trách: {tenTaiXe} ({sdtTaiXe})\n" +
                $"• Tổng tiền COD cần thu: {_tuyenDangChon.TongTienCod:N0} đ\n\n" +
                $"Tất cả đơn hàng trong tuyến đã chuyển sang trạng thái 'Đang Giao Hàng'.",
                "Phát Hành Tuyến Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

            // Làm mới giao diện
            NapDuLieuGomTuyen();

            // Cập nhật huy hiệu trên MainWindow nếu có
            if (Application.Current.MainWindow is MainWindow cuaSoChinh)
            {
                cuaSoChinh.CapNhatHuyHieuDonChoXuLy();
            }
        }

        /// <summary>
        /// SỰ KIỆN: Bấm nút "Xuất Bảng Kê Chuyến Đi (Manifest CSV)"
        /// - Nhiệm vụ: Xuất file bảng kê các điểm dừng Stop 1..N kèm chữ ký để bàn giao cho tài xế.
        /// </summary>
        private void BtnXuatBangKeLoTrinh_Click(object sender, RoutedEventArgs e)
        {
            if (_tuyenDangChon == null || _tuyenDangChon.TongSoDon == 0)
            {
                MessageBox.Show("Không có dữ liệu đơn hàng trong tuyến này để xuất bảng kê!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hopThoaiLuuFile = new SaveFileDialog
            {
                Filter = "Tệp CSV Bảng Kê Tuyến (*.csv)|*.csv",
                FileName = $"BangKeLoTrinh_{_tuyenDangChon.MaTuyen}_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Lưu Bảng Kê Lộ Trình Giao Hàng Cho Shipper"
            };

            if (hopThoaiLuuFile.ShowDialog() == true)
            {
                try
                {
                    var noiDungCsv = new StringBuilder();
                    // Tiêu đề bảng kê
                    noiDungCsv.AppendLine($"BẢNG KÊ LỘ TRÌNH ĐIỂM DỪNG GIAO HÀNG (ROUTE MANIFEST)");
                    noiDungCsv.AppendLine($"Tuyến đường:;{_tuyenDangChon.TenTuyenDuong};Mã tuyến:;{_tuyenDangChon.MaTuyen}");
                    noiDungCsv.AppendLine($"Ngày phát hành:;{DateTime.Now:dd/MM/yyyy HH:mm};Tổng số đơn:;{_tuyenDangChon.TongSoDon}");
                    noiDungCsv.AppendLine($"Tổng tải trọng:;{_tuyenDangChon.TongTrongLuongKg:N1} kg;Tổng COD cần thu:;{_tuyenDangChon.TongTienCod:N0} đ");
                    noiDungCsv.AppendLine();

                    // Tiêu đề cột
                    noiDungCsv.AppendLine("Thứ Tự Điểm Dừng;Mã Vận Đơn;Loại Dịch Vụ;Khách Nhận;Số Điện Thoại;Địa Chỉ Giao Hàng Chi Tiết;Khối Lượng (kg);Tiền Thu Hộ COD;Chữ Ký Người Nhận");

                    var danhSachDiemDung = _tuyenDangChon.LayDanhSachDiemDungToiUu();
                    foreach (var diemDung in danhSachDiemDung)
                    {
                        string dichVu = diemDung.LaHoaToc ? "HỎA TỐC 2H" : "Tiêu chuẩn";
                        string diaChiChuan = diemDung.DiaChiNhan.Replace(";", ",");
                        noiDungCsv.AppendLine($"Stop #{diemDung.ThuTuDung};{diemDung.MaDonHang};{dichVu};{diemDung.TenNguoiNhan};{diemDung.SoDienThoaiNguoiNhan};{diaChiChuan};{diemDung.TrongLuongKg};{diemDung.TienCod:N0};[                    ]");
                    }

                    // Ghi ra file với chuẩn UTF-8 có BOM
                    File.WriteAllText(hopThoaiLuuFile.FileName, noiDungCsv.ToString(), new UTF8Encoding(true));

                    MessageBox.Show($"Đã xuất Bảng Kê Lộ Trình thành công ra file:\n{hopThoaiLuuFile.FileName}", "Xuất Báo Cáo Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ngoaiLe)
                {
                    MessageBox.Show($"Lỗi khi ghi file bảng kê: {ngoaiLe.Message}", "Lỗi Xuất File", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// SỰ KIỆN: In Lệnh Điều Động Chuyến Đi
        /// </summary>
        private void BtnInLenhGiaoHang_Click(object sender, RoutedEventArgs e)
        {
            if (_tuyenDangChon == null || _tuyenDangChon.TongSoDon == 0)
            {
                MessageBox.Show("Vui lòng chọn tuyến đường có đơn để in lệnh điều động!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hopThoaiIn = new PrintDialog();
            if (hopThoaiIn.ShowDialog() == true)
            {
                hopThoaiIn.PrintVisual(dgDiemDungLoTrinh, $"LenhDieuDong_{_tuyenDangChon.MaTuyen}");
                MessageBox.Show("Đã gửi lệnh in bảng kê điểm dừng tới máy in!", "In Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// SỰ KIỆN: Quét lại đơn và cập nhật dữ liệu
        /// </summary>
        private void BtnLamMoiDuLieu_Click(object sender, RoutedEventArgs e)
        {
            NapDuLieuGomTuyen();
        }
    }
}
