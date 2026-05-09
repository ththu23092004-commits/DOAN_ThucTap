
using System.Collections.Generic;
using VeSuKienWeb.Models;

namespace VeSuKienWeb.Models.BanToChuc
{
    // Thống kê theo từng loại vé
    public class ThongKeLoaiVeBtcVM
    {
        public int LoaiVeId { get; set; }
        public string TenLoai { get; set; } = string.Empty;
        public decimal GiaVe { get; set; }

        // Số lượng cấu hình ban đầu (field SoLuong trong bảng LoaiVe)
        public int SoLuongConfig { get; set; }

        // Số vé đã bán (dựa trên bảng Ve)
        public int SoLuongDaBan { get; set; }

        // Số lượng còn lại
        public int SoLuongConLai => SoLuongConfig - SoLuongDaBan;

        // Doanh thu = đã bán * giá
        public decimal DoanhThu => SoLuongDaBan * GiaVe;
    }

    // ViewModel cho màn chi tiết BTC
    public class SuKienChiTietBtcVM
    {
        public SuKien SuKien { get; set; } = null!;
        public LoaiSuKien? LoaiSuKien { get; set; }
        public ToChucSuKien? ToChuc { get; set; }
        public DiaDiem? DiaDiem { get; set; }
        public SuKienTrucTuyen? SuKienTrucTuyen { get; set; }

        public List<ThongKeLoaiVeBtcVM> ThongKeTheoLoaiVe { get; set; } = new();

        // Tổng vé đã bán (tất cả loại vé)
        public int TongVeDaBan { get; set; }

        // Tổng vé đã check-in
        public int TongCheckIn { get; set; }

        // Tổng yêu cầu hoàn/hủy
        public int TongYeuCauHoanHuy { get; set; }

        // Số yêu cầu hoàn/hủy đang chờ duyệt
        public int TongYeuCauHoanHuyChoDuyet { get; set; }

        // Tổng doanh thu (ước tính)
        public decimal TongDoanhThu { get; set; }
    }
}
