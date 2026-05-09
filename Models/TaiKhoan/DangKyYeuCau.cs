using System.ComponentModel.DataAnnotations;

namespace VeSuKienWeb.Models.TaiKhoan
{
    public class DangKyYeuCau
    {
        [Required]
        public string HoTen { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string DienThoai { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6)]
        public string MatKhau { get; set; } = string.Empty;

        [Required, Compare("MatKhau")]
        public string XacNhanMatKhau { get; set; } = string.Empty;
    }
}
