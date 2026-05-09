using System.Security.Cryptography;
using System.Text;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using VeSuKienWeb.Config;

namespace VeSuKienWeb.Services
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(HttpContext httpContext, int donHangId, decimal amount, string orderDesc);
        VnPayReturnResult ParseReturn(IQueryCollection query);
    }

    public class VnPayReturnResult
    {
        public bool IsSuccess { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string TransactionNo { get; set; } = string.Empty;
        public string ResponseCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class VnPayService : IVnPayService
    {
        private readonly VnPaySettings _settings;
        private readonly ILogger<VnPayService> _logger;

        public VnPayService(IOptions<VnPaySettings> options, ILogger<VnPayService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        // ==============================================
        // TẠO URL THANH TOÁN VNPAY
        // ==============================================
        public string CreatePaymentUrl(HttpContext httpContext, int donHangId, decimal amount, string orderDesc)
        {
            var vnp_Amount = ((long)amount * 100).ToString(); // VNPAY yêu cầu *100
            var vnp_TxnRef = donHangId.ToString();

            // ⚠ Quan trọng: luôn dùng IPv4 để tránh ::1
            var vnp_IpAddr = "127.0.0.1";

            var vnp_CreateDate = DateTime.Now.ToString("yyyyMMddHHmmss");

            var vnpParams = new SortedDictionary<string, string>
            {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", _settings.TmnCode },
                { "vnp_Amount", vnp_Amount },
                { "vnp_CreateDate", vnp_CreateDate },
                { "vnp_CurrCode", "VND" },
                { "vnp_IpAddr", vnp_IpAddr },
                { "vnp_Locale", "vn" },
                { "vnp_OrderInfo", orderDesc },
                { "vnp_OrderType", "other" },
                { "vnp_ReturnUrl", _settings.ReturnUrl },
                { "vnp_TxnRef", vnp_TxnRef }
            };

            var query = new StringBuilder();
            var hashData = new StringBuilder();

            foreach (var kv in vnpParams)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    // VNPAY expects application/x-www-form-urlencoded encoding (space => +)
                    var encodedKey = WebUtility.UrlEncode(kv.Key);
                    var encodedValue = WebUtility.UrlEncode(kv.Value);

                    hashData.Append(encodedKey)
                            .Append("=")
                            .Append(encodedValue)
                            .Append("&");

                    // Query string sent to VNPAY must use the same encoded payload used for signature
                    query.Append(encodedKey)
                         .Append("=")
                         .Append(encodedValue)
                         .Append("&");
                }
            }

            if (hashData.Length > 0) hashData.Length -= 1;
            if (query.Length > 0) query.Length -= 1;

            // Tạo chữ ký SHA512
            var secureHash = HmacSHA512(_settings.HashSecret, hashData.ToString());
            _logger.LogInformation("[VNPAY][Create] TmnCode={TmnCode}, ReturnUrl={ReturnUrl}, TxnRef={TxnRef}, Amount={Amount}",
                _settings.TmnCode, _settings.ReturnUrl, vnp_TxnRef, vnp_Amount);
            _logger.LogInformation("[VNPAY][Create] HashData={HashData}", hashData.ToString());
            _logger.LogInformation("[VNPAY][Create] SecureHash={SecureHash}", secureHash);

            var paymentUrl = $"{_settings.BaseUrl}?{query}&vnp_SecureHash={secureHash}";
            _logger.LogInformation("[VNPAY][Create] PaymentUrl={PaymentUrl}", paymentUrl);
            return paymentUrl;
        }

        // ==============================================
        // XỬ LÝ CALLBACK TỪ VNPAY
        // ==============================================
        public VnPayReturnResult ParseReturn(IQueryCollection query)
        {
            var result = new VnPayReturnResult();
            var vnpData = new SortedDictionary<string, string>();

            foreach (var key in query.Keys)
            {
                if (key.StartsWith("vnp_"))
                {
                    var value = query[key].ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        vnpData[key] = value;
                    }
                }
            }

            if (!vnpData.TryGetValue("vnp_SecureHash", out var vnp_SecureHash))
            {
                result.IsSuccess = false;
                result.Message = "Thiếu thông tin chữ ký vnp_SecureHash.";
                return result;
            }

            vnpData.Remove("vnp_SecureHash");
            vnpData.Remove("vnp_SecureHashType");

            var rawData = new StringBuilder();
            foreach (var kv in vnpData)
            {
                var encodedKey = WebUtility.UrlEncode(kv.Key);
                var encodedValue = WebUtility.UrlEncode(kv.Value);
                rawData.Append(encodedKey).Append("=").Append(encodedValue).Append("&");
            }
            if (rawData.Length > 0) rawData.Length -= 1;

            var myHash = HmacSHA512(_settings.HashSecret, rawData.ToString());
            _logger.LogInformation("[VNPAY][Return] RawData={RawData}", rawData.ToString());
            _logger.LogInformation("[VNPAY][Return] VnpSecureHash={VnpSecureHash}", vnp_SecureHash);
            _logger.LogInformation("[VNPAY][Return] MyHash={MyHash}", myHash);

            if (!string.Equals(myHash, vnp_SecureHash, StringComparison.OrdinalIgnoreCase))
            {
                result.IsSuccess = false;
                result.Message = "Chữ ký không hợp lệ.";
                return result;
            }

            vnpData.TryGetValue("vnp_TxnRef", out var orderId);
            vnpData.TryGetValue("vnp_TransactionNo", out var tranNo);
            vnpData.TryGetValue("vnp_ResponseCode", out var responseCode);

            result.OrderId = orderId ?? string.Empty;
            result.TransactionNo = tranNo ?? string.Empty;
            result.ResponseCode = responseCode ?? string.Empty;

            result.IsSuccess = (responseCode == "00");
            result.Message = result.IsSuccess
                ? "Thanh toán thành công."
                : $"Lỗi: {responseCode}";

            return result;
        }

        // ==============================================
        // HMAC SHA512
        // ==============================================
        private static string HmacSHA512(string key, string inputData)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);

            using var hmac = new HMACSHA512(keyBytes);
            var hashBytes = hmac.ComputeHash(inputBytes);

            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
}
