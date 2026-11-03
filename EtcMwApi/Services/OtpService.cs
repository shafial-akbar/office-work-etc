
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;

using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using EtcMwApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace EtcMwApi.Services
{
    public class OtpService : IOtpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OtpService> _logger;

        public OtpService(HttpClient httpClient, ILogger<OtpService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;            
        }

        public async Task<(bool Success, string Message)> SendOtpAsync(string mobileNo, string refId)
        {
            try
            {
                var payload = new
                {
                    strRefId = refId,
                    strMobileNo = mobileNo,
                    isEncPwd = "true"
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/sblservice/SendOTP", content);

                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to send OTP to {MobileNo}. SBL Response: {Response}", mobileNo, responseString);
                    return (false, "Failed to send OTP through SMS Gateway.");
                }

                return (true, "OTP sent successfully.");
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
                var payload = new
                {
                    strRequstId = refId,
                    strAcMobileNo = mobileNo,
                    strReOTP = otp
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/sblservice/VerifyOTP", content);

                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OTP verification failed for {MobileNo}. SBL Response: {Response}", mobileNo, responseString);
                    return (false, "Invalid or expired OTP.");
                }

                // SBL রেসপন্সে যদি স্ট্রিং "true"/"success" বা JSON অবজেক্ট থাকে তা হ্যান্ডেল করা
                if (responseString.Contains("false", StringComparison.OrdinalIgnoreCase) ||
                    responseString.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Invalid OTP provided.");
                }

                return (true, "OTP verified successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while verifying OTP for {MobileNo}", mobileNo);
                return (false, "OTP verification service error.");
            }
        }
    }
}
