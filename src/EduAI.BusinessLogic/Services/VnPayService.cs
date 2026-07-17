using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EduAI.BusinessLogic.IService;
using Microsoft.Extensions.Configuration;

namespace EduAI.BusinessLogic.Services;

public class VnPayService : IVnPayService
{
    private readonly IConfiguration _configuration;

    public VnPayService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreatePaymentUrl(string txnRef, decimal amount, string orderInfo, string ipAddress, string returnUrl)
    {
        // appsettings: "VNPay" → lấy Url/TmnCode/HashSecret để tạo link redirect sang cổng VNPay.
        var (vnpUrl, tmnCode, hashSecret) = RequireVnPayConfig();
        var amountInCents = (long)(amount * 100);

        var paramsMap = new Dictionary<string, string>
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", tmnCode },
            { "vnp_Amount", amountInCents.ToString() },
            { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") },
            { "vnp_CurrCode", "VND" },
            { "vnp_IpAddr", ipAddress },
            { "vnp_Locale", "vn" },
            { "vnp_OrderInfo", orderInfo },
            { "vnp_OrderType", "other" },
            { "vnp_ReturnUrl", returnUrl },
            { "vnp_TxnRef", txnRef }
        };

        var sortedParams = paramsMap.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        var rawData = string.Join("&", sortedParams.Select(p =>
            $"{System.Net.WebUtility.UrlEncode(p.Key)}={System.Net.WebUtility.UrlEncode(p.Value)}"));

        // appsettings: "VNPay:vnp_HashSecret" → ký HMAC-SHA512, gắn vào vnp_SecureHash trên URL thanh toán.
        var secureHash = HmacSha512(hashSecret, rawData);
        return $"{vnpUrl}?{rawData}&vnp_SecureHash={secureHash}";
    }

    public bool ValidateSignature(IReadOnlyDictionary<string, string> vnpayParams, string secureHash)
    {
        // appsettings: "VNPay:vnp_HashSecret" → dùng kiểm tra chữ ký callback từ VNPay (Return/IPN).
        var (_, _, hashSecret) = RequireVnPayConfig();

        var filteredParams = vnpayParams
            .Where(p => p.Key.StartsWith("vnp_") && p.Key != "vnp_SecureHash" && p.Key != "vnp_SecureHashType")
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToList();

        var signData = string.Join("&", filteredParams.Select(p =>
            $"{System.Net.WebUtility.UrlEncode(p.Key)}={System.Net.WebUtility.UrlEncode(p.Value)}"));
        var calculatedHash = HmacSha512(hashSecret, signData);

        return string.Equals(calculatedHash, secureHash, StringComparison.OrdinalIgnoreCase);
    }

    // appsettings: "VNPay" (vnp_Url, vnp_TmnCode, vnp_HashSecret) → đọc cấu hình cổng thanh toán; thiếu thì throw.
    private (string Url, string TmnCode, string HashSecret) RequireVnPayConfig()
    {
        var section = _configuration.GetSection("VNPay");
        var url = section["vnp_Url"];
        var tmnCode = section["vnp_TmnCode"];
        var hashSecret = section["vnp_HashSecret"];

        if (string.IsNullOrWhiteSpace(url)
            || string.IsNullOrWhiteSpace(tmnCode)
            || string.IsNullOrWhiteSpace(hashSecret))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình VNPay (vnp_Url, vnp_TmnCode, vnp_HashSecret). Kiểm tra appsettings hoặc User Secrets.");
        }

        return (url.Trim(), tmnCode.Trim(), hashSecret.Trim());
    }

    private static string HmacSha512(string key, string inputData)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);
        using var hmac = new HMACSHA512(keyBytes);
        var hashValue = hmac.ComputeHash(inputBytes);
        return string.Concat(hashValue.Select(b => b.ToString("x2"))).ToUpper();
    }
}
