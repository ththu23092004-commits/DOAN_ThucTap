namespace VeSuKienWeb.Models
{
    public class ToChucSuKien
    {
        public int ToChucId { get; set; }
        public string? TenToChuc { get; set; }
        public string? EmailLienHe { get; set; }
        public string? MoTa { get; set; }

        public int NguoiDungId { get; set; }
        public NguoiDung? NguoiDung { get; set; }
    }
}

