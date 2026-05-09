
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VeSuKienWeb.Models
{
    public class SuKienTrucTuyen
    {
        [Key]
        public int TrucTuyenId { get; set; }

        [Required]
        [ForeignKey(nameof(SuKien))]
        public int SuKienId { get; set; }

        [Display(Name = "Nền tảng")]
        [MaxLength(100)]
        public string? NenTang { get; set; }   // Zoom, Google Meet...

        [Display(Name = "Link xem")]
        [MaxLength(500)]
        public string? LinkXem { get; set; }

        [Display(Name = "Mật khẩu phòng")]
        [MaxLength(50)]
        public string? MatKhauPhong { get; set; }

        [Display(Name = "Hướng dẫn tham gia")]
        public string? HuongDan { get; set; }

        public SuKien SuKien { get; set; } = null!;
    }
}
