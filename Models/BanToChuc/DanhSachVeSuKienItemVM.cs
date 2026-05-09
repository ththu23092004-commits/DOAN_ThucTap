namespace VeSuKienWeb.Models.BanToChuc
{
    public class DanhSachVeSuKienItemVM
    {
        public int SuKienId { get; set; }
        public string TenSuKien { get; set; } = string.Empty;
        public DateTime ThoiGianBatDau { get; set; }
        public DateTime ThoiGianKetThuc { get; set; }

        public int TongVe { get; set; }
        public int VeDaThanhToan { get; set; }
        public int VeDaHuy { get; set; }
    }
}

