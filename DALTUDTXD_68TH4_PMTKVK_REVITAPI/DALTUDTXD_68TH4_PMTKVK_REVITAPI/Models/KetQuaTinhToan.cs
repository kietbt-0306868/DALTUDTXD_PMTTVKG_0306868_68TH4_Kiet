using System;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public enum LoaiKiemTra
    {
        CuongDoUon,
        CuongDoCat,
        DoVong,
        ChiuLucDoc,
        Khac
    }

    public class KetQuaTinhToan
    {
        // Tên cấu kiện được kiểm tra (Ván mặt, Xà gồ phụ, Xà gồ chính, Cây chống)
        public string TenCauKien { get; private set; }

        // Nội dung kiểm tra (Cường độ uốn, Cường độ cắt, Độ võng, Khả năng chịu lực dọc)
        public string NoiDungKiemTra { get; private set; }

        public LoaiKiemTra LoaiKiemTra { get; private set; }

        // Công thức kiểm tra áp dụng (ví dụ: σ = M/W ≤ R_u, f = 5qL^4/384EI ≤ [f])
        public string CongThucKiemTra { get; private set; }

        // Giá trị tính toán thực tế nhận được
        public double GiaTriThucTe { get; private set; }

        // Giá trị giới hạn cho phép tối đa
        public double GiaTriChoPhep { get; private set; }

        // Đơn vị đo lường (MPa, mm, kN)
        public string DonVi { get; private set; }

        // Tỷ số phần trăm hiệu suất sử dụng vật liệu (Stress/Strength Ratio)
        public double PhanTramSuDung
        {
            get
            {
                if (GiaTriChoPhep > 0)
                {
                    return (GiaTriThucTe / GiaTriChoPhep) * 100.0;
                }
                return 0;
            }
        }

        // Đánh giá kết luận đạt hay không đạt điều kiện kỹ thuật
        public bool IsThoaman => GiaTriChoPhep > 0 && GiaTriThucTe >= 0 && GiaTriThucTe <= GiaTriChoPhep;

        // Trạng thái kết luận bằng chuỗi
        public string KetLuan => IsThoaman ? "ĐẠT" : "KHÔNG ĐẠT";

        /// <summary>
        /// Khởi tạo kết quả tính toán.
        /// Giả thiết: Với bài toán ván khuôn sàn, giá trị thực tế (thucTe) được quy ước luôn >= 0.
        /// Nếu mở rộng sang các bài toán có ứng suất âm/dương, cần xem xét điều chỉnh lại logic validation này.
        /// </summary>
        public KetQuaTinhToan(string tenCauKien, string noiDung, LoaiKiemTra loaiKiemTra, string congThuc, double thucTe, double choPhep, string donVi)
        {
            if (string.IsNullOrWhiteSpace(tenCauKien))
            {
                throw new ArgumentException("Tên cấu kiện không được để trống.", nameof(tenCauKien));
            }
            if (string.IsNullOrWhiteSpace(noiDung))
            {
                throw new ArgumentException("Nội dung kiểm tra không được để trống.", nameof(noiDung));
            }
            if (string.IsNullOrWhiteSpace(congThuc))
            {
                throw new ArgumentException("Công thức kiểm tra không được để trống.", nameof(congThuc));
            }
            if (string.IsNullOrWhiteSpace(donVi))
            {
                throw new ArgumentException("Đơn vị đo lường không được để trống.", nameof(donVi));
            }
            if (choPhep <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(choPhep), "Giá trị cho phép tối đa phải lớn hơn 0.");
            }
            if (thucTe < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(thucTe), "Giá trị thực tế không được âm (theo quy ước bài toán ván khuôn).");
            }

            TenCauKien = tenCauKien;
            NoiDungKiemTra = noiDung;
            LoaiKiemTra = loaiKiemTra;
            CongThucKiemTra = congThuc;
            GiaTriThucTe = thucTe;
            GiaTriChoPhep = choPhep;
            DonVi = donVi;
        }

        public KetQuaTinhToan(string tenCauKien, string noiDung, string congThuc, double thucTe, double choPhep, string donVi)
            : this(tenCauKien, noiDung, LoaiKiemTra.Khac, congThuc, thucTe, choPhep, donVi)
        {
        }
    }
}
