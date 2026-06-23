using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public class XuatKetQua
    {
        // Tên file xuất ra
        public string TenFile { get; private set; }

        // Đường dẫn đầy đủ của file xuất ra
        public string DuongDanFile { get; private set; }

        // Định dạng xuất (Excel, PDF, Text)
        public string DinhDang { get; private set; }

        // Thời gian xuất file
        public DateTime NgayXuat { get; private set; }

        // Người thực hiện xuất báo cáo
        public string NguoiThucHien { get; private set; }

        /// <summary>
        /// Khởi tạo đối tượng xuất kết quả
        /// </summary>
        public XuatKetQua(string duongDan, string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(duongDan))
            {
                throw new ArgumentException("Đường dẫn file không được để trống.", nameof(duongDan));
            }
            if (string.IsNullOrWhiteSpace(nguoiThucHien))
            {
                throw new ArgumentException("Người thực hiện không được để trống.", nameof(nguoiThucHien));
            }

            try
            {
                // Kiểm tra tính hợp lệ của path và chuẩn hóa thành đường dẫn tuyệt đối
                DuongDanFile = Path.GetFullPath(duongDan);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Đường dẫn file không hợp lệ: {ex.Message}", nameof(duongDan), ex);
            }

            TenFile = Path.GetFileName(DuongDanFile);
            DinhDang = Path.GetExtension(DuongDanFile).ToUpper().Replace(".", "");
            NguoiThucHien = nguoiThucHien;
            NgayXuat = DateTime.Now;
        }

        /// <summary>
        /// Xuất dữ liệu tính toán và kiểm tra ván khuôn ra tệp văn bản.
        /// Lưu ý: Hiện tại hàm này chỉ hỗ trợ ghi văn bản thuần túy (TXT/CSV). 
        /// Nếu đường dẫn là Excel/PDF, file tạo ra vẫn mang nội dung Text.
        /// </summary>
        public bool ExportToReportFile(List<KetQuaTinhToan> listKetQua)
        {
            try
            {
                if (listKetQua == null || listKetQua.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Cảnh báo: Danh sách kết quả tính toán null hoặc rỗng. Hủy thao tác xuất.");
                    return false;
                }

                // Thời điểm thực tế ghi file — dùng biến cục bộ, không mutate state của object
                DateTime thoiDiemXuat = DateTime.Now;

                if (DinhDang != "TXT" && DinhDang != "CSV")
                {
                    System.Diagnostics.Debug.WriteLine($"Cảnh báo: Hàm xuất hiện tại chỉ hỗ trợ định dạng Text thuần, nhưng đường dẫn có định dạng là: {DinhDang}");
                }

                // Ghi dữ liệu kiểm tra vào file văn bản thuyết minh đơn giản làm mẫu
                using (StreamWriter writer = new StreamWriter(DuongDanFile, false, Encoding.UTF8))
                {
                    writer.WriteLine("          BÁO CÁO THUYẾT MINH TÍNH TOÁN VÁN KHUÔN SÀN      ");
                    writer.WriteLine($"Ngày xuất báo cáo: {thoiDiemXuat:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine($"Người thực hiện:   {NguoiThucHien}");
                    writer.WriteLine("Tiêu chuẩn áp dụng: TCVN 9356:2012 (Kết cấu gỗ - Tiêu chuẩn thiết kế)");
                    writer.WriteLine();

                    writer.WriteLine("KẾT QUẢ KIỂM TRA ĐIỀU KIỆN KỸ THUẬT VÀ CƠ HỌC:");
                    writer.WriteLine(string.Format("{0,-25} | {1,-15} | {2,-15} | {3,-10}", "Nội dung kiểm tra", "Thực tế", "Cho phép", "Kết luận"));

                    foreach (var kq in listKetQua)
                    {
                        writer.WriteLine(string.Format("{0,-25} | {1,-15} | {2,-15} | {3,-10}", 
                            kq.NoiDungKiemTra, 
                            kq.GiaTriThucTe.ToString("F2") + " " + kq.DonVi, 
                            kq.GiaTriChoPhep.ToString("F2") + " " + kq.DonVi, 
                            kq.KetLuan));
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi xuất file: " + ex.Message);
                return false;
            }
        }
    }
}
