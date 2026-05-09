using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VeSuKienWeb.Models
{
    [Table("ThamGiaSuKien")]
    public class ThamGiaSuKien
    {
        [Key] // 🔑 khai báo rõ khóa chính
        public int ThamGiaId { get; set; }

        [Required]
        public int VeId { get; set; }

        public DateTime? ThoiGianCheckIn { get; set; }

        // ChuaCheckIn, DaCheckIn
        [Required]
        [MaxLength(20)]
        public string TrangThai { get; set; } = "ChuaCheckIn";

        // Navigation
        public Ve Ve { get; set; } = null!;
    }
}
