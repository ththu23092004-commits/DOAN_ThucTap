using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VeSuKienWeb.Models
{
    public class LoaiVe
    {
        [Key]
        public int LoaiVeId { get; set; }

        [Required]
        [ForeignKey(nameof(SuKien))]
        public int SuKienId { get; set; }

        [MaxLength(50)]
        [Display(Name = "Tên loại vé")]
        public string? TenLoai { get; set; }   // VIP, Thường, Early Bird...

        [Display(Name = "Giá vé")]
        [Column(TypeName = "decimal(12,2)")]
        public decimal GiaVe { get; set; }

        [Display(Name = "Số lượng vé")]
        public int SoLuong { get; set; }

        [MaxLength(200)]
        [Display(Name = "Mô tả")]
        public string? MoTa { get; set; }

        // Navigation tới SuKien
        public SuKien SuKien { get; set; } = null!;

        // Nếu sau này bạn có entity Ve thì có thể thêm lại navigation:
        // public ICollection<Ve> DanhSachVe { get; set; } = new List<Ve>();
    }
}
