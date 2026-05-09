using System;
using System.Collections.Generic;

namespace VeSuKienWeb.Models
{
    public class Ve
    {
        public int VeId { get; set; }

        public int NguoiDungId { get; set; }
        public int LoaiVeId { get; set; }
        public int SuKienId { get; set; }
        public int? ChoNgoiId { get; set; }

        public string MaVe { get; set; } = string.Empty;
        public DateTime NgayDat { get; set; } = DateTime.Now;

        // ChuaThanhToan, DaThanhToan, DaCheckIn, Huy
        public string TrangThai { get; set; } = "ChuaThanhToan";

        // Khóa ngoại tới DonHang (nếu sau này dùng), 
        // NHƯNG KHÔNG CẦN navigation DonHang để tránh lỗi.
        public int? DonHangId { get; set; }

        // ===== Navigation properties =====

        public NguoiDung NguoiDung { get; set; } = null!;
        public LoaiVe LoaiVe { get; set; } = null!;
        public SuKien SuKien { get; set; } = null!;
        public SoDoChoNgoi? ChoNgoi { get; set; }

        public ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();
        public ICollection<ThamGiaSuKien> ThamGiaSuKiens { get; set; } = new List<ThamGiaSuKien>();
        public ICollection<YeuCauHoanHuyVe> YeuCauHoanHuyVes { get; set; } = new List<YeuCauHoanHuyVe>();
    }
}
