using System;
using System.Collections.Generic;
using System.Linq;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// THỰC THỂ: Chuyến Gom Đơn Theo Tuyến Đường (Route Batch)
    /// - Nhiệm vụ: Gom các đơn hàng có cùng địa bàn / tuyến đường thành một chuyến giao tập trung.
    /// - Cách hoạt động:
    ///   + Tự động tính toán tổng số đơn, tổng trọng lượng (kg), tổng tiền thu hộ COD và số đơn hỏa tốc.
    ///   + Kiểm tra giới hạn tải trọng xe máy (ngưỡng 50kg) để cảnh báo dispatcher cần điều xe tải.
    ///   + Sắp xếp thứ tự các điểm dừng (Stops) tối ưu: Đơn Express giao trước, gom theo khu vực.
    /// - Tương tác dữ liệu: RouteBatchingView.xaml, ShippingOrder.cs, WarehouseContext.cs.
    /// </summary>
    public class RouteBatch
    {
        /// <summary>
        /// Tên tuyến đường / Cụm khu vực giao hàng (VD: Tuyến Cầu Giấy - Nam Từ Liêm)
        /// </summary>
        public string TenTuyenDuong { get; set; } = string.Empty;

        /// <summary>
        /// Mã định danh tuyến (VD: TUYEN-CG-NTL)
        /// </summary>
        public string MaTuyen { get; set; } = string.Empty;

        /// <summary>
        /// Biểu tượng nhận diện tuyến
        /// </summary>
        public string BieuTuongTuyen { get; set; } = "🗺️";

        /// <summary>
        /// Danh sách các đơn hàng thuộc chuyến gom này
        /// </summary>
        public List<ShippingOrder> DanhSachDonHang { get; set; } = new();

        /// <summary>
        /// Tổng số lượng đơn hàng trong tuyến gom
        /// </summary>
        public int TongSoDon => DanhSachDonHang.Count;

        /// <summary>
        /// Tổng khối lượng hàng hóa của cả chuyến gom (kg)
        /// </summary>
        public double TongTrongLuongKg => DanhSachDonHang.Sum(donHang => donHang.Weight);

        /// <summary>
        /// Tổng số tiền thu hộ COD của cả chuyến đi (VNĐ)
        /// </summary>
        public decimal TongTienCod => DanhSachDonHang.Sum(donHang => donHang.CodAmount);

        /// <summary>
        /// Số lượng đơn hỏa tốc Express cần ưu tiên giao sớm
        /// </summary>
        public int SoDonHoaToc => DanhSachDonHang.Count(donHang => donHang.IsExpress);

        /// <summary>
        /// Số đơn chưa phân công tài xế
        /// </summary>
        public int SoDonChuaPhanCong => DanhSachDonHang.Count(donHang => donHang.AssignedShipperId == null || donHang.Status == ShippingOrderStatus.NewReceived || donHang.Status == ShippingOrderStatus.PendingProcessing);

        /// <summary>
        /// Cảnh báo quá tải trọng xe máy (trên 45 kg cần đề xuất xe tải)
        /// </summary>
        public bool CanhBaoQuaTai => TongTrongLuongKg > 45.0;

        /// <summary>
        /// Gợi ý loại phương tiện vận chuyển phù hợp
        /// </summary>
        public string PhuongTienGoiY => CanhBaoQuaTai ? "Xe tải 1.25 tấn (Do khối lượng > 45kg)" : "Xe máy chở hàng tiêu chuẩn";

        /// <summary>
        /// Tên Shipper gợi ý cho tuyến này (theo khu vực phụ trách)
        /// </summary>
        public string ShipperGoiY { get; set; } = "Tự động đề xuất";

        /// <summary>
        /// Sắp xếp thứ tự các điểm dừng giao hàng tối ưu:
        /// - Đơn Express ưu tiên xếp trước (Stop 1, Stop 2...)
        /// - Đơn khối lượng nặng giao tiếp theo
        /// - Đơn tiêu chuẩn còn lại
        /// </summary>
        public List<DiemDungLoTrinh> LayDanhSachDiemDungToiUu()
        {
            var danhSachSapXep = DanhSachDonHang
                .OrderByDescending(donHang => donHang.IsExpress)
                .ThenByDescending(donHang => donHang.Weight)
                .ToList();

            var ketQua = new List<DiemDungLoTrinh>();
            for (int thuTu = 0; thuTu < danhSachSapXep.Count; thuTu++)
            {
                var donHang = danhSachSapXep[thuTu];
                ketQua.Add(new DiemDungLoTrinh
                {
                    ThuTuDung = thuTu + 1,
                    DonHang = donHang,
                    GhiChuLoTrinh = donHang.IsExpress ? "⚡ ƯU TIÊN GIAO HỎA TỐC" : $"Điểm dừng #{thuTu + 1}"
                });
            }

            return ketQua;
        }
    }

    /// <summary>
    /// THỰC THỂ PHỤ TRỢ: Điểm dừng trên lộ trình giao hàng (Route Stop)
    /// </summary>
    public class DiemDungLoTrinh
    {
        public int ThuTuDung { get; set; }
        public ShippingOrder DonHang { get; set; } = new();
        public string GhiChuLoTrinh { get; set; } = string.Empty;

        public string MaDonHang => DonHang.OrderCode;
        public string TenNguoiNhan => DonHang.ReceiverName;
        public string SoDienThoaiNguoiNhan => DonHang.ReceiverPhone;
        public string DiaChiNhan => DonHang.ReceiverAddress;
        public decimal TienCod => DonHang.CodAmount;
        public double TrongLuongKg => DonHang.Weight;
        public bool LaHoaToc => DonHang.IsExpress;
        public string TrangThaiHienThi => DonHang.StatusDisplayName;
    }
}
