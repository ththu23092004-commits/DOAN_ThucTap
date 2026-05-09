using System;

namespace VeSuKienWeb.Models
{
    public class ThongBao
    {
        public int ThongBaoId { get; set; }
        public int NguoiDungId { get; set; }
        public string NoiDung { get; set; } = string.Empty;
        public DateTime NgayGui { get; set; }
        public bool DaDoc { get; set; } = false;

        public NguoiDung NguoiDung { get; set; } = null!;
    }
}

