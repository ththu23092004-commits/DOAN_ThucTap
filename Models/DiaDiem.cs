
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VeSuKienWeb.Models
{
    public class DiaDiem
    {
        [Key]
        public int DiaDiemId { get; set; }

        [Required]
        [ForeignKey(nameof(SuKien))]
        public int SuKienId { get; set; }

        [Display(Name = "Tên địa điểm")]
        [MaxLength(200)]
        public string? TenDiaDiem { get; set; }

        [Display(Name = "Địa chỉ")]
        [MaxLength(300)]
        public string? DiaChi { get; set; }

        [Display(Name = "Số chỗ ngồi")]
        public int SoChoNgoi { get; set; }

        [Display(Name = "Link bản đồ")]
        [MaxLength(500)]
        public string? BanDo { get; set; }

        // Điều hướng ngược về sự kiện
        public SuKien SuKien { get; set; } = null!;
    }
}
