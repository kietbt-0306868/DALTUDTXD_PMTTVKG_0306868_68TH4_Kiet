using System;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public enum LoaiTietDien
    {
        /// <summary>Tiết diện tấm phẳng (ví dụ: Ván mặt)</summary>
        VanMat,
        
        /// <summary>Tiết diện chữ nhật đặc (ví dụ: Xà gồ gỗ)</summary>
        XaGo,
        
        /// <summary>Tiết diện ống tròn rỗng (ví dụ: Cây chống thép)</summary>
        CayChong,
        
        /// <summary>Khác</summary>
        Khac
    }

    public class CauKienModel
    {
        // Tên cấu kiện (ví dụ: "Ván mặt", "Xà gồ phụ", "Xà gồ chính", "Cây chống")
        public string TenCauKien { get; set; }
        
        // Loại vật liệu (ví dụ: Gỗ xẻ, Gỗ dán phủ phim, Thép ống)
        public string LoaiVatLieu { get; set; }

        // Loại tiết diện dùng cho tính toán đặc trưng hình học
        public LoaiTietDien LoaiTietDien { get; set; }
        
        private double _chieuRong_b;
        /// <summary>
        /// Chiều rộng tiết diện, hoặc đường kính ngoài với ống tròn (đơn vị: mm)
        /// </summary>
        public double ChieuRong_b 
        { 
            get => _chieuRong_b; 
            set => _chieuRong_b = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Chiều rộng/Đường kính phải lớn hơn 0.");
        } 

        private double _chieuCao_h;
        /// <summary>
        /// Chiều cao tiết diện (đơn vị: mm)
        /// </summary>
        public double ChieuCao_h 
        { 
            get => _chieuCao_h; 
            set => _chieuCao_h = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Chiều cao phải lớn hơn 0.");
        }  

        private double _chieuDay_t;
        /// <summary>
        /// Chiều dày (đối với ván mặt hoặc thành ống tròn rỗng) (đơn vị: mm)
        /// </summary>
        public double ChieuDay_t 
        { 
            get => _chieuDay_t; 
            set => _chieuDay_t = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Chiều dày không được âm.");
        }  

        /// <summary>
        /// Đường kính trong của tiết diện ống rỗng (đơn vị: mm)
        /// </summary>
        private double DuongKinhTrong_d 
        {
            get
            {
                double d = ChieuRong_b - 2 * ChieuDay_t;
                if (d <= 0)
                {
                    throw new InvalidOperationException("Chiều dày t quá lớn so với đường kính b, làm đường kính trong <= 0.");
                }
                return d;
            }
        }

        // Các thuộc tính tự động tính toán đặc trưng hình học tiết diện
        
        /// <summary>
        /// Diện tích mặt cắt ngang A (đơn vị: mm^2)
        /// </summary>
        public double DienTichMatCat_A
        {
            get
            {
                switch (LoaiTietDien)
                {
                    case LoaiTietDien.VanMat:
                        // Tấm ván phẳng: A = b * t
                        return ChieuRong_b * ChieuDay_t;
                    
                    case LoaiTietDien.CayChong:
                        // Tiết diện ống thép tròn rỗng: A = pi * (D^2 - d^2) / 4
                        return Math.PI * (Math.Pow(ChieuRong_b, 2) - Math.Pow(DuongKinhTrong_d, 2)) / 4.0;
                    
                    case LoaiTietDien.XaGo:
                        // Tiết diện chữ nhật đặc (xà gồ gỗ): A = b * h
                        return ChieuRong_b * ChieuCao_h;

                    case LoaiTietDien.Khac:
                    default:
                        return 0;
                }
            }
        }

        /// <summary>
        /// Mô men quán tính I (đơn vị: mm^4)
        /// </summary>
        public double MomenQuanTinh_I
        {
            get
            {
                switch (LoaiTietDien)
                {
                    case LoaiTietDien.VanMat:
                        // Tấm ván phẳng: I = b * t^3 / 12
                        return (ChieuRong_b * Math.Pow(ChieuDay_t, 3)) / 12.0;

                    case LoaiTietDien.CayChong:
                        // Tiết diện ống thép tròn rỗng: I = pi * (D^4 - d^4) / 64
                        return Math.PI * (Math.Pow(ChieuRong_b, 4) - Math.Pow(DuongKinhTrong_d, 4)) / 64.0;

                    case LoaiTietDien.XaGo:
                        // Tiết diện chữ nhật (xà gồ gỗ): I = b * h^3 / 12
                        return (ChieuRong_b * Math.Pow(ChieuCao_h, 3)) / 12.0;

                    case LoaiTietDien.Khac:
                    default:
                        return 0;
                }
            }
        }

        /// <summary>
        /// Mô men kháng uốn W (đơn vị: mm^3)
        /// </summary>
        public double MomenKhangUon_W
        {
            get
            {
                switch (LoaiTietDien)
                {
                    case LoaiTietDien.VanMat:
                        // Tấm ván phẳng: W = b * t^2 / 6
                        return (ChieuRong_b * Math.Pow(ChieuDay_t, 2)) / 6.0;

                    case LoaiTietDien.CayChong:
                        // Tiết diện ống thép tròn rỗng: W = pi * (D^4 - d^4) / (32 * D)
                        return Math.PI * (Math.Pow(ChieuRong_b, 4) - Math.Pow(DuongKinhTrong_d, 4)) / (32.0 * ChieuRong_b);

                    case LoaiTietDien.XaGo:
                        // Tiết diện chữ nhật (xà gồ gỗ): W = b * h^2 / 6
                        return (ChieuRong_b * Math.Pow(ChieuCao_h, 2)) / 6.0;

                    case LoaiTietDien.Khac:
                    default:
                        return 0;
                }
            }
        }

        /// <summary>
        /// Bán kính quán tính r (đơn vị: mm)
        /// </summary>
        public double BanKinhQuanTinh_r
        {
            get
            {
                double a = DienTichMatCat_A;
                if (a > 0)
                {
                    return Math.Sqrt(MomenQuanTinh_I / a);
                }
                return 0;
            }
        }

        // Thông số sơ đồ tính toán
        
        private double _khoangCachBoTri;
        /// <summary>
        /// Khoảng cách rải cấu kiện (đơn vị: mm, khoảng cách tim đến tim)
        /// </summary>
        public double KhoangCachBoTri 
        { 
            get => _khoangCachBoTri; 
            set => _khoangCachBoTri = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Khoảng cách bố trí phải lớn hơn 0.");
        }
        
        private double _nhipTinhToan;
        /// <summary>
        /// Nhịp thanh trên sơ đồ tĩnh học (đơn vị: mm, khoảng cách từ tim gối tựa này đến tim gối tựa kia)
        /// </summary>
        public double NhipTinhToan 
        { 
            get => _nhipTinhToan; 
            set => _nhipTinhToan = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Nhịp tính toán phải lớn hơn 0.");
        }    

        private int _soNhipTinhToan = 3;
        /// <summary>
        /// Số nhịp tính toán (ví dụ: 1 nhịp, 2 nhịp, 3 nhịp liên tục). Giá trị hợp lệ: 1, 2, 3.
        /// </summary>
        public int SoNhipTinhToan
        {
            get => _soNhipTinhToan;
            set
            {
                if (value >= 1 && value <= 3)
                {
                    _soNhipTinhToan = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Số nhịp tính toán chỉ được phép là 1, 2 hoặc 3.");
                }
            }
        }

        public CauKienModel()
        {
            SoNhipTinhToan = 3; // Mặc định sơ đồ dầm liên tục 3 nhịp
        }

        public CauKienModel(string ten, LoaiTietDien loaiTietDien, string vatLieu, double b, double h, double t = 0) : this()
        {
            TenCauKien = ten;
            LoaiTietDien = loaiTietDien;
            LoaiVatLieu = vatLieu;
            ChieuRong_b = b;
            ChieuCao_h = h;
            ChieuDay_t = t;
        }
    }
}
