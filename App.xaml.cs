using System.Configuration;
using System.Data;
using System.Windows;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// ĐIỂM KHỞI CHẠY TOÀN CỤC ỨNG DỤNG (WPF APPLICATION ENTRY POINT)
    /// - Nhiệm vụ:
    ///   1. Nạp và quản lý toàn bộ tài nguyên giao diện chung (Styles, Brushes, DataTemplates) định nghĩa trong App.xaml.
    ///   2. Điều khiển vòng đời ứng dụng từ khi khởi động (StartupUri: login.xaml) tới khi tắt hoàn toàn.
    ///   3. Đảm bảo tính nhất quán của bảng màu Logistics Modern SaaS và font chữ hệ thống (Segoe UI).
    /// </summary>
    public partial class App : Application
    {
    }
}
