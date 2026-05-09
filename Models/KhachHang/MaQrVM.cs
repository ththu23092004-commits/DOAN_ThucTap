namespace VeSuKienWeb.Models.KhachHang
{
    public class MaQrVM
    {
        public int VeId { get; set; }
        public string MaVe { get; set; } = string.Empty;
        public string QrCodeBase64 { get; set; } = string.Empty;
    }
}

