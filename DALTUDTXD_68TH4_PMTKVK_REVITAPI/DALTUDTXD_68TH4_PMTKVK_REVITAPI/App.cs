using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI
{
    /// <summary>
    /// Builds the ribbon as the same left-to-right construction workflow used by the
    /// reference application: input -> take-off preparation -> design -> schedule -> 3D.
    /// Expensive actions are deliberately kept out of the navigation buttons.
    /// </summary>
    public class RevitApp : IExternalApplication
    {
        private const string TabName = "Ván Khuôn";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                try { application.CreateRibbonTab(TabName); } catch { /* tab already exists */ }

                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                RibbonPanel input = application.CreateRibbonPanel(TabName, "01 · Dữ liệu đầu vào");
                AddLarge(input, "BtnDuLieu", "Quản lý\ncấu kiện", assemblyPath,
                    typeof(CmdDuLieu), "CK", Color.FromRgb(2, 132, 199),
                    "Chọn, phân nhóm và kiểm tra cấu kiện Sàn, Dầm, Cột, Móng trong Revit.");
                AddLarge(input, "BtnThongSo", "Khai báo\nthông số", assemblyPath,
                    typeof(CmdThongSo), "TS", Color.FromRgb(14, 116, 144),
                    "Khai báo tải trọng, vật liệu và thông số phụ kiện ván khuôn.");
                AddSmallStack(input, assemblyPath,
                    Button("BtnSelectComponent", "Bấm chọn cấu kiện", typeof(CmdSelectComponent),
                        "1", Color.FromRgb(2, 132, 199), "Chọn nhiều cấu kiện bằng PickObjects."),
                    Button("BtnSweepSelectComponent", "Quét chọn cấu kiện", typeof(CmdSweepSelectComponent),
                        "Q", Color.FromRgb(2, 132, 199), "Quét vùng để chọn nhiều cấu kiện."),
                    Button("BtnSelectGroundFloor", "Chọn tầng trệt", typeof(CmdSelectGroundFloor),
                        "T", Color.FromRgb(2, 132, 199), "Chỉ định tầng trệt/nền để áp dụng đúng quy tắc chống."));

                RibbonPanel takeoff = application.CreateRibbonPanel(TabName, "02 · Chuẩn bị bóc tách");
                AddLarge(takeoff, "BtnFullWorkflow", "Chạy toàn bộ\n6 bước", assemblyPath,
                    typeof(CmdRunFullTakeoffWorkflow), "6", Color.FromRgb(217, 119, 6),
                    "Thực hiện tuần tự JOIN, đặt tên, Shared Parameter, khởi tạo tính toán, bảng thống kê và trích xuất diện tích.");
                AddSmallStack(takeoff, assemblyPath,
                    Button("BtnJoinSequence", "1  JOIN theo trình tự", typeof(CmdJoinByConstructionSequence),
                        "J", Color.FromRgb(217, 119, 6), "JOIN theo trình tự thi công để loại vùng giao nhau."),
                    Button("BtnNameComponents", "2  Đặt tên cấu kiện", typeof(CmdNameFormworkComponents),
                        "N", Color.FromRgb(217, 119, 6), "Đặt tên cấu kiện theo tầng, loại và quy ước thi công."),
                    Button("BtnLoadShared", "3  Load Shared Parameter", typeof(CmdLoadFormworkSharedParameters),
                        "P", Color.FromRgb(217, 119, 6), "Nạp bộ Shared Parameter của quy trình ván khuôn."));
                AddSmallStack(takeoff, assemblyPath,
                    Button("BtnInitCalculation", "4  Khởi tạo tính toán VK", typeof(CmdInitializeFormworkCalculation),
                        "C", Color.FromRgb(217, 119, 6), "Khởi tạo dữ liệu tính toán từ hình học và tải trọng."),
                    Button("BtnCreateSchedules", "5  Tạo bảng thống kê", typeof(CmdCreateFormworkSchedules),
                        "S", Color.FromRgb(217, 119, 6), "Tạo/cập nhật các bảng thống kê ván khuôn."),
                    Button("BtnExtractAreas", "6  Trích xuất diện tích", typeof(CmdExtractFormworkAreas),
                        "A", Color.FromRgb(217, 119, 6), "Tính diện tích ván khuôn ròng sau khi trừ giao nhau."));

                RibbonPanel design = application.CreateRibbonPanel(TabName, "03 · Tính toán & thiết kế");
                AddLarge(design, "BtnTinhToan", "Tính toán\n& kiểm tra", assemblyPath,
                    typeof(CmdTinhToan), "KT", Color.FromRgb(5, 150, 105),
                    "Mở kết quả kiểm tra của từng cấu kiện đã chọn.");
                AddLarge(design, "BtnAutoDesign", "Tự động thiết kế\nđạt chuẩn", assemblyPath,
                    typeof(CmdAutoDesign), "OK", Color.FromRgb(5, 150, 105),
                    "Lặp thiết kế phụ kiện đến khi toàn bộ phép kiểm tra đạt yêu cầu.");
                AddSmallStack(design, assemblyPath,
                    Button("BtnUpdateResults", "Cập nhật dữ liệu", typeof(CmdUpdateResults),
                        "U", Color.FromRgb(5, 150, 105), "Đọc lại hình học và dữ liệu lựa chọn hiện hành."),
                    Button("BtnRunAnalysis", "Chạy phân tích", typeof(CmdRunAnalysis),
                        "R", Color.FromRgb(5, 150, 105), "Chạy lại phân tích cho phạm vi cấu kiện đã chọn."),
                    Button("BtnRecalculate", "Chạy tính toán lại", typeof(CmdRecalculate),
                        "↻", Color.FromRgb(5, 150, 105), "Tính lại mà không dựng mô hình 3D."));

                RibbonPanel schedule = application.CreateRibbonPanel(TabName, "04 · Thống kê & xuất");
                AddLarge(schedule, "BtnKetQua", "Bảng thống kê\nvật tư", assemblyPath,
                    typeof(CmdKetQua), "TK", Color.FromRgb(79, 70, 229),
                    "Mở bảng tiên lượng dùng đúng thiết kế đã kiểm tra.");
                AddLarge(schedule, "BtnCalculateAllFloors", "Thống kê\ntoàn dự án", assemblyPath,
                    typeof(CmdCalculateAllFloors), "Σ", Color.FromRgb(79, 70, 229),
                    "Tổng hợp Sàn, Dầm, Cột, Móng và toàn bộ phụ kiện.");
                AddLarge(schedule, "BtnExportExcel", "Xuất\nExcel", assemblyPath,
                    typeof(CmdExportExcel), "XL", Color.FromRgb(22, 163, 74),
                    "Xuất bảng tiên lượng và kết quả kiểm tra sang Excel.");

                RibbonPanel visualization = application.CreateRibbonPanel(TabName, "05 · Diễn họa 3D");
                AddLarge(visualization, "BtnPreview3D", "Preview 3D\nnhanh", assemblyPath,
                    typeof(CmdPreview3D), "3D", Color.FromRgb(124, 58, 237),
                    "Mở mô hình Revit gốc và ván khuôn ở chế độ nhẹ; không tạo hàng nghìn family phụ kiện.");
                AddLarge(visualization, "BtnCreateRevitModel", "Dựng Family\nchi tiết", assemblyPath,
                    typeof(CmdCreateRevitModel), "F", Color.FromRgb(109, 40, 217),
                    "Dựng phụ kiện Family chi tiết theo yêu cầu. Chỉ dùng khi cần kiểm tra/chỉnh sửa chi tiết.");

                RibbonPanel settings = application.CreateRibbonPanel(TabName, "Cài đặt");
                AddSmallStack(settings, assemblyPath,
                    Button("BtnSaveSettings", "Lưu cấu hình", typeof(CmdSaveSettings),
                        "S", Color.FromRgb(71, 85, 105), "Lưu bộ thông số hiện hành."),
                    Button("BtnResetSettings", "Đặt lại mặc định", typeof(CmdResetSettings),
                        "D", Color.FromRgb(71, 85, 105), "Khôi phục thông số mặc định an toàn."));

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                TaskDialog.Show("Khởi tạo Add-in Ván Khuôn",
                    "Không thể tạo đầy đủ giao diện ribbon.\n\n" + exception.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

        private sealed class ButtonSpec
        {
            public string Name { get; set; }
            public string Text { get; set; }
            public Type CommandType { get; set; }
            public string Glyph { get; set; }
            public Color Color { get; set; }
            public string ToolTip { get; set; }
        }

        private static ButtonSpec Button(string name, string text, Type commandType, string glyph,
            Color color, string toolTip) => new ButtonSpec
        {
            Name = name, Text = text, CommandType = commandType, Glyph = glyph, Color = color, ToolTip = toolTip
        };

        private static void AddLarge(RibbonPanel panel, string name, string text, string assemblyPath,
            Type commandType, string glyph, Color color, string toolTip)
        {
            panel.AddItem(CreateData(name, text, assemblyPath, commandType, glyph, color, toolTip));
        }

        private static void AddSmallStack(RibbonPanel panel, string assemblyPath, params ButtonSpec[] buttons)
        {
            if (buttons == null || buttons.Length < 2 || buttons.Length > 3) return;
            var data = new RibbonItemData[buttons.Length];
            for (int index = 0; index < buttons.Length; index++)
            {
                ButtonSpec item = buttons[index];
                data[index] = CreateData(item.Name, item.Text, assemblyPath, item.CommandType,
                    item.Glyph, item.Color, item.ToolTip);
            }
            if (data.Length == 2) panel.AddStackedItems(data[0], data[1]);
            else panel.AddStackedItems(data[0], data[1], data[2]);
        }

        private static PushButtonData CreateData(string name, string text, string assemblyPath,
            Type commandType, string glyph, Color color, string toolTip)
        {
            return new PushButtonData(name, text, assemblyPath, commandType.FullName)
            {
                Image = RibbonIconFactory.Create(glyph, color, 16),
                LargeImage = RibbonIconFactory.Create(glyph, color, 32),
                ToolTip = toolTip,
                LongDescription = toolTip
            };
        }

        private static class RibbonIconFactory
        {
            public static ImageSource Create(string glyph, Color color, int size)
            {
                var visual = new DrawingVisual();
                using (DrawingContext drawing = visual.RenderOpen())
                {
                    double inset = Math.Max(1, size * 0.06);
                    drawing.DrawRoundedRectangle(new SolidColorBrush(color), null,
                        new Rect(inset, inset, size - 2 * inset, size - 2 * inset), size * 0.18, size * 0.18);
                    var text = new FormattedText(glyph ?? string.Empty, CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"),
                        Math.Max(7, size * (glyph?.Length > 1 ? 0.38 : 0.52)), Brushes.White, 1.0);
                    drawing.DrawText(text, new Point((size - text.Width) / 2, (size - text.Height) / 2));
                }
                var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                bitmap.Freeze();
                return bitmap;
            }
        }
    }
}
