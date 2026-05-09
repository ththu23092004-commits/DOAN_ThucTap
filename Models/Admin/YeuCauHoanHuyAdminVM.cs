using System;

namespace VeSuKienWeb.Models.Admin
{
    public class YeuCauHoanHuyAdminVM
    {
        public int YeuCauId { get; set; }
        public int VeId { get; set; }
        public string MaVe { get; set; } = string.Empty;
        public string TenSuKien { get; set; } = string.Empty;
        public string TenNguoiDung { get; set; } = string.Empty;
        public string EmailNguoiDung { get; set; } = string.Empty;
        public decimal SoTien { get; set; }
        public DateTime NgayYeuCau { get; set; }
        public string LyDo { get; set; } = string.Empty;
        public string HinhThuc { get; set; } = string.Empty; // HoanTien / ChuyenVe
        public string TrangThai { get; set; } = string.Empty; // ChoDuyet / DaDuyet / TuChoi
    }
}

