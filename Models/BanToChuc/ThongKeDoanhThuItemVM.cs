using System;

namespace VeSuKienWeb.Models.BanToChuc
{
    public class ThongKeDoanhThuItemVM
    {
        public int SuKienId { get; set; }
        public string TenSuKien { get; set; } = string.Empty;

        public DateTime ThoiGianBatDau { get; set; }
        public DateTime ThoiGianKetThuc { get; set; }

        public int TongSoVe { get; set; }
        public int VeDaThanhToan { get; set; }
        public int VeDaHuy { get; set; }

        /// <summary>
        /// Doanh thu thực tế sau khi trừ các vé đã hủy
        /// (chỉ tính vé đang còn trạng thái DaThanhToan)
        /// </summary>
        public decimal DoanhThuThucTe { get; set; }
    }
}

