using System.Collections.Generic;

namespace VeSuKienWeb.Models.Admin
{
    public class SuKienChiTietVM
    {
        public SuKien SuKien { get; set; } = null!;
        public LoaiSuKien? LoaiSuKien { get; set; }
        public ToChucSuKien? ToChuc { get; set; }

        // Sự kiện trực tiếp
        public DiaDiem? DiaDiem { get; set; }

        // Sự kiện trực tuyến
        public SuKienTrucTuyen? SuKienTrucTuyen { get; set; }

        // Các loại vé
        public List<LoaiVe> LoaiVes { get; set; } = new();
    }
}

