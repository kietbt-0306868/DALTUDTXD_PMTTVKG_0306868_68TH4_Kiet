using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    // 1. Class đại diện cho một dòng vật tư trong bảng thống kê
    public class MaterialSummaryItem
    {
        public int STT { get; set; }
        public string TenVatTu { get; set; }    // Tên vật tư
        public string QuyCach { get; set; }     // Quy cách (mm)
        public string DonVi { get; set; }              // Đơn vị
        public double SoLuong { get; set; }          // Số lượng
        public string GhiChu { get; set; }              // Ghi chú
    }

    // 2. Class Model chính cho trang Thống kê
    public class ThongkeModel
    {
        // Các thuộc tính cho phần "TỔNG HỢP NHANH" bên phải
        public double DienTichSan { get; set; }       // Diện tích sàn (m2)
        public int TongSoXaGoLop1 { get; set; }      // Tổng số xà gồ lớp 1 (Thanh)
        public int TongSoCayChong { get; set; }         // Tổng số cây chống (Bộ)

        // Cài đặt cho việc tính toán
        public bool IncludeAccessories { get; set; }    // 
        public double WasteFactor { get; set; }         // Hệ số hao hụt (%)

        // Danh sách chi tiết cho DataGrid
        public ObservableCollection<MaterialSummaryItem> MaterialDetails { get; set; }

        public ThongkeModel()
        {
            MaterialDetails = new ObservableCollection<MaterialSummaryItem>();

            // Giá trị mặc định ban đầu
            IncludeAccessories = true;
            WasteFactor = 5.0; // Mặc định hao hụt 5%
        }
    }
}
