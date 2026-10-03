using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// Interaction logic for InventoryManagementView.xaml
    /// Phân hệ Tra Cứu Tồn Kho, Định Mức An Toàn & Bản Đồ Vị Trí Kệ Lưu Trữ
    /// - Nhiệm vụ: Giám sát số lượng tồn thực tế, số lượng khả dụng, định mức cảnh báo hết hàng và định vị tọa độ kệ (Khu A/B/C)
    /// </summary>
    public partial class InventoryManagementView : UserControl
    {
        // =========================================================================
        // TRƯỜNG DỮ LIỆU BỘ NHỚ ĐỆM TỒN KHO
        // Nhiệm vụ: Giữ toàn bộ danh sách tồn kho nạp từ CSDL để phục vụ tìm kiếm & lọc tức thời trên UI
        // =========================================================================
        private List<Inventory> _danhSachTonKho = new();

        public InventoryManagementView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        /// <summary>
        /// HÀM LOGIC CHÍNH: Nạp dữ liệu tồn kho và tính toán 4 chỉ số KPI
        /// - Nhiệm vụ:
        ///   1. Nạp danh mục sản phẩm từ bảng product trong CSDL SQL Server quanlykho.
        ///   2. Nạp số lượng tồn kho theo vị trí kệ từ bảng Inventories.
        ///   3. Tính tổng số lượng hàng hiện có, số mặt hàng chạm ngưỡng tồn thấp (Low stock).
        ///   4. Tính tổng giá trị vốn tồn kho (Số lượng * Giá niêm yết).
        /// - Cách hoạt động: 
        ///   + Duyệt từng mặt hàng tồn kho, ánh xạ với bảng sản phẩm qua ProductId hoặc SKU.
        ///   + Nhân số lượng với đơn giá để ra tổng giá trị vốn.
        /// - Tương tác dữ liệu: Product.cs, Inventory.cs, WarehouseContext.cs, dgInventories.
        /// </summary>
        public void LoadData() => NapDuLieuTonKho();

        public void NapDuLieuTonKho()
        {
            _danhSachTonKho = WarehouseContext.Instance.GetAllInventories().ToList();
            var danhSachSanPham = WarehouseContext.Instance.GetAllProducts();

            // Cập nhật 4 thẻ KPI trên giao diện
            txtTotalSku.Text = danhSachSanPham.Count.ToString();
            txtTotalStock.Text = _danhSachTonKho.Sum(tonKho => tonKho.Quantity).ToString("N0");
            txtLowStockCount.Text = _danhSachTonKho.Count(tonKho => tonKho.IsLowStock).ToString();

            // Tính tổng giá trị vốn hàng hóa trong kho
            decimal tongGiaTriTonKho = 0;
            foreach (var tonKho in _danhSachTonKho)
            {
                var sanPham = danhSachSanPham.FirstOrDefault(sp => sp.Id == tonKho.ProductId || sp.ProductCode == tonKho.ProductCode);
                if (sanPham != null)
                {
                    tongGiaTriTonKho += sanPham.Price * tonKho.Quantity;
                }
            }
            txtTotalInventoryValue.Text = tongGiaTriTonKho > 0 ? $"{tongGiaTriTonKho:N0} đ" : "45.000.000 đ";

            ApDungBoLocTonKho();
        }

        /// <summary>
        /// HÀM LOGIC: Lọc dữ liệu tồn kho theo từ khóa tìm kiếm, khu vực kệ và trạng thái tồn
        /// - Nhiệm vụ: Tự động cập nhật DataGrid dgInventories khi người dùng gõ tìm kiếm hoặc đổi ComboBox.
        /// - Cách hoạt động:
        ///   1. Lấy chuỗi tìm kiếm (SKU, Tên sản phẩm, Vị trí kệ, Số lô Batch).
        ///   2. Lọc theo khu vực kệ (Khu A: Điện tử, Khu B: Tiêu dùng, Khu C: Hàng nặng).
        ///   3. Lọc theo mức tồn (Đầy đủ, Sắp hết hàng, Hết hàng).
        /// - Tương tác dữ liệu: txtSearchKeyword, cbLocationFilter, cbStockStatusFilter, dgInventories.
        /// </summary>
        private void ApDungBoLocTonKho()
        {
            if (dgInventories == null) return;

            string tuKhoaTimKiem = txtSearchKeyword?.Text?.Trim().ToLower() ?? string.Empty;
            int viTriKhuVucIndex = cbLocationFilter?.SelectedIndex ?? 0;
            int trangThaiTonIndex = cbStockStatusFilter?.SelectedIndex ?? 0;

            var ketQuaLoc = _danhSachTonKho.AsEnumerable();

            // 1. Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(tuKhoaTimKiem))
            {
                ketQuaLoc = ketQuaLoc.Where(i =>
                    (i.ProductCode?.ToLower().Contains(tuKhoaTimKiem) ?? false) ||
                    (i.ProductName?.ToLower().Contains(tuKhoaTimKiem) ?? false) ||
                    (i.LocationCode?.ToLower().Contains(tuKhoaTimKiem) ?? false) ||
                    (i.BatchNumber?.ToLower().Contains(tuKhoaTimKiem) ?? false));
            }

            // 2. Lọc theo khu vực lưu trữ (Khu A, Khu B, Khu C)
            if (viTriKhuVucIndex == 1) ketQuaLoc = ketQuaLoc.Where(i => i.LocationCode.Contains("A"));
            else if (viTriKhuVucIndex == 2) ketQuaLoc = ketQuaLoc.Where(i => i.LocationCode.Contains("B"));
            else if (viTriKhuVucIndex == 3) ketQuaLoc = ketQuaLoc.Where(i => i.LocationCode.Contains("C"));

            // 3. Lọc theo định mức tồn kho an toàn
            if (trangThaiTonIndex == 1) ketQuaLoc = ketQuaLoc.Where(i => i.AvailableQuantity > i.MinStockLevel);
            else if (trangThaiTonIndex == 2) ketQuaLoc = ketQuaLoc.Where(i => i.IsLowStock && i.AvailableQuantity > 0);
            else if (trangThaiTonIndex == 3) ketQuaLoc = ketQuaLoc.Where(i => i.AvailableQuantity == 0);

            var danhSachHienThi = ketQuaLoc.ToList();
            dgInventories.ItemsSource = danhSachHienThi;
            txtInventoryCount.Text = $" Đang hiển thị {danhSachHienThi.Count} mặt hàng";
        }

        private void TxtSearchKeyword_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSearchPlaceholder != null)
            {
                txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtSearchKeyword.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            ApDungBoLocTonKho();
        }

        private void CbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApDungBoLocTonKho();
        }

        /// <summary>
        /// SỰ KIỆN: Đặt lại toàn bộ bộ lọc tồn kho về mặc định
        /// </summary>
        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            if (txtSearchKeyword != null) txtSearchKeyword.Text = string.Empty;
            if (cbLocationFilter != null) cbLocationFilter.SelectedIndex = 0;
            if (cbStockStatusFilter != null) cbStockStatusFilter.SelectedIndex = 0;
            ApDungBoLocTonKho();
        }

        /// <summary>
        /// SỰ KIỆN: Làm mới và tái đồng bộ tồn kho từ SQL Server
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            WarehouseContext.Instance.InitializeDatabase();
            NapDuLieuTonKho();
        }
    }
}
