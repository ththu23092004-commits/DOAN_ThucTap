
namespace VeSuKienWeb.Models.BanToChuc
{
    public class ThongKeDoanhThuSuKienVM
    {
        public int SuKienId { get; set; }
        public string TenSuKien { get; set; } = "";
        public DateTime ThoiGianBatDau { get; set; }
        public DateTime ThoiGianKetThuc { get; set; }

        public int SoVeDaThanhToan { get; set; }
        public decimal DoanhThu { get; set; }
    }

    public class ThongKeTongQuanPageVM
    {
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public List<ThongKeDoanhThuSuKienVM> Items { get; set; } = new();
        public decimal TongDoanhThu { get; set; }
    }
}
