using System.Text;
using System.Text.Json;
using Etc.Shared.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EtcMwApi.Services
{
    public class OtpService : IOtpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OtpService> _logger;
        private readonly string _isEncPwd;
        private readonly string _owner;

        public OtpService(HttpClient httpClient, IConfiguration configuration, ILogger<OtpService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // appsettings.json থেকে রিড করা হচ্ছে (না পেলে ডিফল্ট ভ্যালু ফলব্যাক হিসেবে থাকবে)
            var otpConfig = configuration.GetSection("SblOtpService");
            _isEncPwd = otpConfig["IsEncPwd"] ?? "true";
            _owner = otpConfig["Owner"] ?? "linkAccount";
        }

        public async Task<(bool Success, string Message)> SendOtpAsync(string mobileNo, string refId)
        {
            try
            {
                // ১. রিকোয়েস্ট পে-লোড (owner: "linkAccount" যুক্ত করা হয়েছে)
                var payload = new
                {
                    strRefId = refId,
                    strMobileNo = mobileNo,
                    isEncPwd = _isEncPwd,
                    owner = _owner
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                string requestUrl = $"{_httpClient.BaseAddress}api/sblservice/SendOTP1";
                var response = await _httpClient.PostAsync(requestUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HTTP Error from SBL SendOTP for {MobileNo}. Status: {Status}, Response: {Response}",
                        mobileNo, response.StatusCode, responseString);
                    return (false, "Failed to connect to SMS Gateway.");
                }

                // ২. এসকেপড/ডাবল সিরিয়ালাইজড JSON হ্যান্ডলিং
                string cleanJson = UnescapeJson(responseString);

                using var doc = JsonDocument.Parse(cleanJson);
                string statusCode = "";
                string message = "";

                if (doc.RootElement.TryGetProperty("StatusCode", out var codeProp))
                {
                    statusCode = codeProp.GetString() ?? "";
                }

                if (doc.RootElement.TryGetProperty("Message", out var msgProp))
                {
                    message = msgProp.GetString() ?? "";
                }

                // ৩. বডির ভেতরের StatusCode যাচাই ("200" হলে সফল)
                if (statusCode == "200")
                {
                    return (true, string.IsNullOrWhiteSpace(message) ? "OTP sent successfully." : message);
                }

                _logger.LogWarning("SBL SendOTP rejected for {MobileNo}. Code: {Code}, Message: {Message}",
                    mobileNo, statusCode, message);

                return (false, string.IsNullOrWhiteSpace(message) ? "Failed to send OTP through SMS Gateway." : message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while sending OTP to {MobileNo}", mobileNo);
                return (false, "OTP service is temporarily unavailable.");
            }
        }

        public async Task<(bool Success, string Message)> VerifyOtpAsync(string mobileNo, string refId, string otp)
        {
            try
            {
                // ১. ভেরিফিকেশন রিকোয়েস্ট পে-লোড
                var payload = new
                {
                    strRequstId = refId,
                    strAcMobileNo = mobileNo,
                    strReOTP = otp
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                string requestUrl = $"{_httpClient.BaseAddress}api/sblservice/VerifyOTP";
                var response = await _httpClient.PostAsync(requestUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HTTP Error from SBL VerifyOTP for {MobileNo}. Status: {Status}, Response: {Response}",
                        mobileNo, response.StatusCode, responseString);
                    return (false, "Failed to connect to OTP verification service.");
                }

                // ২. এসকেপড/ডাবল সিরিয়ালাইজড JSON হ্যান্ডলিং
                string cleanJson = UnescapeJson(responseString);

                using var doc = JsonDocument.Parse(cleanJson);

                // ৩. Status বুলিয়ান প্রোপার্টি রিড করা {"Status": true}
                if (doc.RootElement.TryGetProperty("Status", out var statusProp))
                {
                    bool isVerified = false;

                    if (statusProp.ValueKind == JsonValueKind.True)
                    {
                        isVerified = true;
                    }
                    else if (statusProp.ValueKind == JsonValueKind.False)
                    {
                        isVerified = false;
                    }
                    else if (statusProp.ValueKind == JsonValueKind.String)
                    {
                        bool.TryParse(statusProp.GetString(), out isVerified);
                    }

                    if (isVerified)
                    {
                        return (true, "OTP verified successfully.");
                    }
                }

                _logger.LogWarning("Invalid OTP response from SBL for {MobileNo}: {Response}", mobileNo, cleanJson);
                return (false, "Invalid or expired OTP.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while verifying OTP for {MobileNo}", mobileNo);
                return (false, "OTP verification service error.");
            }
        }

        // হেল্পার মেথড: রেসপন্স ডাবল-সিরিয়ালাইজড স্ট্রিং ("{\"StatusCode\":\"200\"}") হলে আন-কোট করে ক্লিন JSON অবজেক্ট বানায়
        private static string UnescapeJson(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return "{}";

            var trimmed = rawJson.Trim();
            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
            {
                try
                {
                    return JsonSerializer.Deserialize<string>(trimmed) ?? trimmed;
                }
                catch
                {
                    return trimmed;
                }
            }
            return trimmed;
        }
    }
}