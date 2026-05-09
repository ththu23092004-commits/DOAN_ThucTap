using System.Security.Cryptography;
using System.Text;

namespace VeSuKienWeb.Helpers
{
    public static class MatKhauHelper
    {
        public static string BamMatKhau(string matKhau)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(matKhau);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash); // .NET 5+
        }
    }
}

