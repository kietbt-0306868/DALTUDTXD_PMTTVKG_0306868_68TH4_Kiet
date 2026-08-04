using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI
{
    public class RevitApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "Add-in Ván Khuôn";
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch { }

            RibbonPanel panelQuanLy = application.CreateRibbonPanel(tabName, "Quản Lý & Tính Toán");

            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            
            PushButtonData btn1Data = new PushButtonData("BtnDuLieu", "1. Quản Lý\nDữ Liệu", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdDuLieu");
            PushButtonData btn2Data = new PushButtonData("BtnThongSo", "2. Khai Báo\nThông Số", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdThongSo");
            PushButtonData btn3Data = new PushButtonData("BtnTinhToan", "3. Tính Toán\n& Kiểm Tra", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdTinhToan");
            PushButtonData btn4Data = new PushButtonData("BtnKetQua", "4. Xuất\nKết Quả", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdKetQua");
            PushButtonData btn5Data = new PushButtonData("BtnPreview3D", "5. Xem\nMô Hình 3D", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdPreview3D");

            panelQuanLy.AddItem(btn1Data);
            panelQuanLy.AddItem(btn2Data);
            panelQuanLy.AddItem(btn3Data);
            panelQuanLy.AddItem(btn4Data);
            panelQuanLy.AddItem(btn5Data);

            // ── Lựa Chọn ──
            RibbonPanel panelLuaChon = application.CreateRibbonPanel(tabName, "Lựa Chọn");
            panelLuaChon.AddItem(new PushButtonData("BtnSelectComponent", "Bấm chọn\ncấu kiện", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdSelectComponent"));
            panelLuaChon.AddItem(new PushButtonData("BtnSweepSelectComponent", "Quét chọn\nnhiều cấu kiện", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdSweepSelectComponent"));

            // ── Dữ Liệu ──
            RibbonPanel panelDuLieu = application.CreateRibbonPanel(tabName, "Dữ Liệu");
            panelDuLieu.AddItem(new PushButtonData("BtnSelectGroundFloor", "Chọn tầng\ntrệt", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdSelectGroundFloor"));
            panelDuLieu.AddItem(new PushButtonData("BtnUpdateResults", "Cập nhật\nkết quả", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdUpdateResults"));
            panelDuLieu.AddItem(new PushButtonData("BtnRunAnalysis", "Chạy\nphân tích", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdRunAnalysis"));

            // ── Tính Toán ──
            RibbonPanel panelTinhToan = application.CreateRibbonPanel(tabName, "Tính Toán");
            panelTinhToan.AddItem(new PushButtonData("BtnRecalculate", "Chạy tính toán\nlại", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdRecalculate"));

            // ── Xuất & Mô Hình ──
            RibbonPanel panelXuat = application.CreateRibbonPanel(tabName, "Xuất & Mô Hình");
            panelXuat.AddItem(new PushButtonData("BtnCreateRevitModel", "Dựng hình\nRevit 3D", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdCreateRevitModel"));
            panelXuat.AddItem(new PushButtonData("BtnCalculateAllFloors", "Thống kê\ntoàn dự án", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdCalculateAllFloors"));
            panelXuat.AddItem(new PushButtonData("BtnExportExcel", "Xuất\nExcel", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdExportExcel"));

            // ── Cài Đặt ──
            RibbonPanel panelCaiDat = application.CreateRibbonPanel(tabName, "Cài Đặt");
            panelCaiDat.AddItem(new PushButtonData("BtnSaveSettings", "Lưu\ncấu hình", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdSaveSettings"));
            panelCaiDat.AddItem(new PushButtonData("BtnResetSettings", "Đặt lại\nmặc định", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdResetSettings"));

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
