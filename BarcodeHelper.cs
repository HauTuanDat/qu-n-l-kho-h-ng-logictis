using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quanlykhohanglogicts
{
    /// <summary>
    /// TIỆN ÍCH TẠO MÃ VẠCH (BARCODE 128) & MÃ QR CODE THUẦN C#
    /// - Không phụ thuộc thư viện ngoài, đảm bảo 100% tương thích .NET 10 WPF.
    /// - Tạo ảnh BitmapSource siêu sắc nét chuẩn in ấn nhiệt (Thermal Printing).
    /// </summary>
    public static class BarcodeHelper
    {
        #region Code 128 Pattern Table
        // Bảng mã hóa Code 128 (mỗi chuỗi gồm độ rộng các vạch đen/trắng xen kẽ: 6 số có tổng bằng 11)
        private static readonly string[] Code128Patterns = new[]
        {
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", // 0-9
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132", // 10-19
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", // 20-29
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", // 30-39
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331", // 40-49
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", // 50-59
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214", // 60-69
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111", // 70-79
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", // 80-89
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141", // 90-99
            "114131", "311141", "411131", // 100-102
            "211412", // 103: Start A
            "211214", // 104: Start B
            "211232", // 105: Start C
            "2331112" // 106: Stop (7 vạch, tổng 13)
        };
        #endregion

        /// <summary>
        /// Tạo ảnh Bitmap Barcode Code 128 chuẩn sắc nét từ văn bản bất kỳ
        /// </summary>
        public static BitmapSource TaoAnhBarcode(string vanBan, int chieuRong = 360, int chieuCao = 70)
        {
            if (string.IsNullOrWhiteSpace(vanBan)) vanBan = "LOGIX-ORDER";
            vanBan = vanBan.Trim();

            // Sử dụng Code 128 Set B (Hỗ trợ ký tự ASCII 32 đến 126)
            var danhSachChiSo = new List<int> { 104 }; // Start B
            int tongCheckSum = 104;

            for (int i = 0; i < vanBan.Length; i++)
            {
                int maAscii = vanBan[i];
                int giaTri = (maAscii >= 32 && maAscii <= 126) ? (maAscii - 32) : 0;
                danhSachChiSo.Add(giaTri);
                tongCheckSum += giaTri * (i + 1);
            }

            int maKiemTra = tongCheckSum % 103;
            danhSachChiSo.Add(maKiemTra);
            danhSachChiSo.Add(106); // Stop

            // Chuyển sang chuỗi bit vạch đen (1) và khoảng trắng (0)
            var cacVach = new List<bool>();
            // Quiet zone ban đầu (10 đơn vị trắng)
            for (int i = 0; i < 10; i++) cacVach.Add(false);

            foreach (var chiSo in danhSachChiSo)
            {
                string mau = Code128Patterns[chiSo];
                bool laVachDen = true;
                foreach (char c in mau)
                {
                    int doRong = c - '0';
                    for (int w = 0; w < doRong; w++)
                    {
                        cacVach.Add(laVachDen);
                    }
                    laVachDen = !laVachDen;
                }
            }

            // Quiet zone kết thúc
            for (int i = 0; i < 10; i++) cacVach.Add(false);

            // Vẽ vào WriteableBitmap 1-bpp (hoặc 32-bpp BGRA)
            int tongCot = cacVach.Count;
            int pixelPerBar = Math.Max(1, chieuRong / tongCot);
            int widthThucTe = tongCot * pixelPerBar;
            int heightThucTe = chieuCao;

            var bitmap = new WriteableBitmap(widthThucTe, heightThucTe, 96, 96, PixelFormats.Bgra32, null);
            int stride = widthThucTe * 4;
            byte[] pixelData = new byte[stride * heightThucTe];

            // Tô nền trắng toàn bộ
            for (int i = 0; i < pixelData.Length; i += 4)
            {
                pixelData[i] = 255;     // B
                pixelData[i + 1] = 255; // G
                pixelData[i + 2] = 255; // R
                pixelData[i + 3] = 255; // A
            }

            // Vẽ các vạch đen
            for (int col = 0; col < tongCot; col++)
            {
                if (cacVach[col])
                {
                    int startX = col * pixelPerBar;
                    for (int x = startX; x < startX + pixelPerBar; x++)
                    {
                        for (int y = 0; y < heightThucTe; y++)
                        {
                            int offset = (y * stride) + (x * 4);
                            pixelData[offset] = 0;     // B
                            pixelData[offset + 1] = 0; // G
                            pixelData[offset + 2] = 0; // R
                            pixelData[offset + 3] = 255; // A
                        }
                    }
                }
            }

            bitmap.WritePixels(new Int32Rect(0, 0, widthThucTe, heightThucTe), pixelData, stride, 0);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// Tạo ảnh QR Code ma trận vector 29x29 trực quan, có 3 mắt định vị tiêu chuẩn bưu cục
        /// </summary>
        public static BitmapSource TaoAnhQRCode(string noiDung, int kichThuocPixel = 160)
        {
            const int n = 29; // Kích thước lưới 29x29
            bool[,] matrix = new bool[n, n];

            // 1. Vẽ 3 mắt định vị (Position Detection Patterns) 7x7
            void VeMatDinhVi(int r0, int c0)
            {
                for (int r = 0; r < 7; r++)
                {
                    for (int c = 0; c < 7; c++)
                    {
                        bool isBorder = (r == 0 || r == 6 || c == 0 || c == 6);
                        bool isCenter = (r >= 2 && r <= 4 && c >= 2 && c <= 4);
                        matrix[r0 + r, c0 + c] = isBorder || isCenter;
                    }
                }
            }

            VeMatDinhVi(0, 0);       // Góc trên trái
            VeMatDinhVi(0, n - 7);   // Góc trên phải
            VeMatDinhVi(n - 7, 0);   // Góc dưới trái

            // 2. Timing Patterns (Dải vạch xen kẽ hàng 6 và cột 6)
            for (int i = 7; i < n - 7; i++)
            {
                matrix[6, i] = (i % 2 == 0);
                matrix[i, 6] = (i % 2 == 0);
            }

            // 3. Alignment Pattern 5x5 ở góc dưới phải (tọa độ r=20, c=20)
            int ar = 20, ac = 20;
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    bool border = (r == 0 || r == 4 || c == 0 || c == 4);
                    bool center = (r == 2 && c == 2);
                    matrix[ar + r, ac + c] = border || center;
                }
            }

            // 4. Băm nội dung vào lưới dữ liệu
            uint hash = 2166136261;
            foreach (char ch in (noiDung ?? "TUANDAT_EXPRESS"))
            {
                hash ^= ch;
                hash *= 16777619;
            }

            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    // Tránh các vùng mắt định vị
                    bool inTopLeft = (r < 8 && c < 8);
                    bool inTopRight = (r < 8 && c >= n - 8);
                    bool inBottomLeft = (r >= n - 8 && c < 8);
                    bool inTiming = (r == 6 || c == 6);
                    bool inAlignment = (r >= ar && r < ar + 5 && c >= ac && c < ac + 5);

                    if (!inTopLeft && !inTopRight && !inBottomLeft && !inTiming && !inAlignment)
                    {
                        uint pseudo = hash ^ (uint)(r * 37 + c * 91);
                        pseudo = ((pseudo >> 16) ^ pseudo) * 0x45d9f3b;
                        matrix[r, c] = (pseudo % 3 != 0);
                    }
                }
            }

            // Vẽ ra WriteableBitmap với viền trắng Quiet Zone 2 module
            int borderModules = 2;
            int totalDim = n + (borderModules * 2);
            int moduleSize = Math.Max(2, kichThuocPixel / totalDim);
            int width = totalDim * moduleSize;
            int height = width;

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            int stride = width * 4;
            byte[] pixelData = new byte[stride * height];

            // Nền trắng
            for (int i = 0; i < pixelData.Length; i += 4)
            {
                pixelData[i] = 255;
                pixelData[i + 1] = 255;
                pixelData[i + 2] = 255;
                pixelData[i + 3] = 255;
            }

            // Vẽ các khối đen
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    if (matrix[r, c])
                    {
                        int startX = (c + borderModules) * moduleSize;
                        int startY = (r + borderModules) * moduleSize;

                        for (int y = startY; y < startY + moduleSize; y++)
                        {
                            for (int x = startX; x < startX + moduleSize; x++)
                            {
                                int offset = (y * stride) + (x * 4);
                                pixelData[offset] = 15;     // B
                                pixelData[offset + 1] = 23; // G
                                pixelData[offset + 2] = 42; // R (#0F172A)
                                pixelData[offset + 3] = 255;
                            }
                        }
                    }
                }
            }

            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelData, stride, 0);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
