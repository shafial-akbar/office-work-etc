using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface IOtpService
    {
        Task<(bool Success, string Message)> SendOtpAsync(string mobileNo, string refId);
        Task<(bool Success, string Message)> VerifyOtpAsync(string mobileNo, string refId, string otp);
    }

}
