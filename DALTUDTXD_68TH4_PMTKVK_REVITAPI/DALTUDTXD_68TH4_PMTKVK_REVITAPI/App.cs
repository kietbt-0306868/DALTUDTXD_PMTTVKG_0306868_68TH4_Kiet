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

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Quản Lý & Tính Toán");

            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            
            PushButtonData btn1Data = new PushButtonData("BtnDuLieu", "1. Quản Lý\nDữ Liệu", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdDuLieu");
            PushButtonData btn2Data = new PushButtonData("BtnThongSo", "2. Khai Báo\nThông Số", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdThongSo");
            PushButtonData btn3Data = new PushButtonData("BtnTinhToan", "3. Tính Toán\n& Kiểm Tra", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdTinhToan");
            PushButtonData btn4Data = new PushButtonData("BtnKetQua", "4. Xuất\nKết Quả", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdKetQua");
            PushButtonData btn5Data = new PushButtonData("BtnPreview3D", "5. Xem\nMô Hình 3D", assemblyPath, "DALTUDTXD_68TH4_PMTKVK_REVITAPI.CmdPreview3D");

            panel.AddItem(btn1Data);
            panel.AddItem(btn2Data);
            panel.AddItem(btn3Data);
            panel.AddItem(btn4Data);
            panel.AddItem(btn5Data);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
