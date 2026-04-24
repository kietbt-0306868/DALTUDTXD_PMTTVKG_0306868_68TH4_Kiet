using System.Collections.ObjectModel;

namespace DALTUDTXD_VANKHUON_68TH4.Models
{
    // 1. Class này đại diện cho từng dòng trong bảng DataGrid 
    public class CheckResultItem
    {
        public string CauKien { get; set; }
        public string Noidungkiemtra { get; set; }
        public string GiaTriThucTe { get; set; }
        public string GiaTriChoPhep { get; set; }
        public string TrangThai { get; set; }
        public string MauSacTrangThai { get; set; }
    }

    // 2. Class Model chính cho trang Kiểm tra
    public class KiemtraModel
    {
        // Thông số vật liệu 
        public double CuongDoGoUon { get; set; }
        public double MoDunDanHoi { get; set; }
        public string DoVongChoPhepCuaSan { get; set; }   // (L/400)
        public string DoVongChoPhepCuaXaGo { get; set; }  // (L/500)

        // Danh sách kết quả để hiển thị lên DataGrid

        public ObservableCollection<CheckResultItem> CheckResults { get; set; }

        public KiemtraModel()
        {
            CheckResults = new ObservableCollection<CheckResultItem>();

            // Khởi tạo giá trị mặc định 
            CuongDoGoUon = 14.5;
            MoDunDanHoi = 10000;
            DoVongChoPhepCuaSan = "L/400";
            DoVongChoPhepCuaXaGo = "L/500";
        }
    }
}