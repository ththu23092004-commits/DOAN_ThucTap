
using System.Collections.Generic;

namespace VeSuKienWeb.Models.SuKienViewModels
{
    public class SuKienChiTietKhachVM
    {
        public SuKien SuKien { get; set; } = null!;
        public LoaiSuKien? LoaiSuKien { get; set; }
        public ToChucSuKien? ToChuc { get; set; }

        public DiaDiem? DiaDiem { get; set; }
        public SuKienTrucTuyen? SuKienTrucTuyen { get; set; }

        public List<LoaiVe> LoaiVes { get; set; } = new();

        public bool DaMuaVe { get; set; }

        
    }
}
