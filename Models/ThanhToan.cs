using System;

namespace VeSuKienWeb.Models
{
    public class ThanhToan
    {
        public int ThanhToanId { get; set; }
        public int VeId { get; set; }
        public string PhuongThuc { get; set; } = string.Empty;
        public string MaGiaoDich { get; set; } = string.Empty;
        public string TrangThai { get; set; } = "ThanhCong";
        public DateTime NgayThanhToan { get; set; } = DateTime.Now;
        public decimal SoTien { get; set; }

        public Ve? Ve { get; set; }   // navigation
    }
}
