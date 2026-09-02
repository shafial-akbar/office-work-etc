using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using EtcMwApi.Data;
using Microsoft.EntityFrameworkCore;

namespace EtcMwApi.Services
{
    public class RhdApiService : IRhdApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApiSettings _apiSettings;
        private readonly ITokenService _tokenService;
        private readonly ILogger<RhdApiService> _logger;

        public RhdApiService(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiSettings, ITokenService tokenService, ILogger<RhdApiService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _apiSettings = apiSettings.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<ApiResponse<Vehicle>> GetVehicleInformation(string registrationNumber, int companyOid)
        {
            try
            {
                var _httpClient = _httpClientFactory.CreateClient("RhdApiClient");

                var token = await _tokenService.GetToken();
                if (string.IsNullOrEmpty(token))
                {
                    return new ApiResponse<Vehicle>
                    {
                        Success = false,
                        Message = "Failed to get authentication token",
                        Reason = "Failed",
                        StatusCode = 401
                    };
                }

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var request = new
                {
                    vehicleRegistrationNumber = registrationNumber,
                    companyOid = companyOid
                };

                var response = await _httpClient.PostAsJsonAsync($"{_apiSettings.BaseUrl}/api/v2/wallet/vehicle-info", request);
                var content = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // Handle case insensitivity
                };

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<VehicleResponse>(content, options);
                    return new ApiResponse<Vehicle> { Success = result.Success, Reason = result.Reason, Message = result.Message, Data = result.Data, StatusCode = (int)result.Code };
                }
                
                return new ApiResponse<Vehicle>
                {
                    Success = false,
                    Message = $"API call failed: {content}",
                    Reason="Failed",
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<Vehicle>
                {
                    Success = false,
                    Message = ex.Message,
                    Reason = "Exception",
                    StatusCode = 500
                };
            }
        }

        public async Task<ApiResponse<VehicleRegiInformation>> RegisterVehicle(VehicleRegistrationRequest request)
        {
            try
            {
                var _httpClient = _httpClientFactory.CreateClient("RhdApiClient");

                // 1. Get authentication token
                var token = await _tokenService.GetToken();
                if (string.IsNullOrEmpty(token))
                {
                    return new ApiResponse<VehicleRegiInformation>
                    {
                        Success = false,
                        Message = "Failed to get authentication token",
                        StatusCode = 401
                    };
                }

                
                // 4. Prepare request to external API
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var payload = new
                {
                    vehicleRegistrationNumber = request.VehicleRegistrationNumber,
                    companyOid = request.CompanyOid,
                    mobileNumber = request.MobileNumber,
                    walletNumber = request.WalletNumber,
                    description = request.Description,
                    currentBalance = request.CurrentBalance,
                    dueBalance = request.DueBalance
                };

                // 5. Call external API
                var response = await _httpClient.PostAsJsonAsync($"{_apiSettings.BaseUrl}/api/v2/wallet/register-vehicle", payload);
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // Handle case insensitivity
                };

                // 6. Handle response
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<VehicleRegiResponse>(content, options);

                    if (result.Success)
                    {
                        var wallet = new Wallet();
                        wallet.CompanyName = "SONALI BANK PLC";
                        wallet.WalletNo = request.WalletNumber;
                        wallet.MobileNo = request.MobileNumber;

                        result.Data.Wallet = wallet;

                        return new ApiResponse<VehicleRegiInformation> { Success = result.Success, Reason = result.Reason, Message = result.Message, Data = result.Data, StatusCode = (int)result.Code };

                    }
                    else
                    {
                        var result1 = JsonSerializer.Deserialize<VehicleRegiConflictResponse>(content, options);
                        var data = new VehicleRegiInformation();
                        data.VehicleRegistrationNumber = request.VehicleRegistrationNumber;
                        var wallet = new Wallet();
                        wallet.CompanyName = result1.Data.CompanyName;
                        wallet.WalletNo = result1.Data.WalletNumber;
                        wallet.MobileNo = request.MobileNumber;
                        data.Wallet = wallet;

                        return new ApiResponse<VehicleRegiInformation> { Success = result1.Success, Reason = result1.Reason, Message = result1.Message, Data = data, StatusCode = (int)result1.Code };
                    }
                }

                // 7. Handle error responses
                return new ApiResponse<VehicleRegiInformation>
                {
                    Success = false,
                    Message = $"API call failed: {content}",
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error registering vehicle");
                return new ApiResponse<VehicleRegiInformation>
                {
                    Success = false,
                    Message = ex.Message,
                    StatusCode = 500
                };
            }
        }

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<VehicleUnregisterResponse> UnregisterVehicle(VehicleUnregisterRequest request)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("RhdApiClient");

                // ১. Token সংগ্রহ
                var token = await _tokenService.GetToken();
                if (string.IsNullOrEmpty(token))
                {
                    return new VehicleUnregisterResponse
                    {
                        Success = false,
                        Reason = "UNAUTHORIZED",
                        Message = "Failed to get authentication token.",
                        StatusCode = 401
                    };
                }

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // ২. Payload তৈরি (HTTP PUT)
                var payload = new
                {
                    vehicleRegistrationNumber = request.VehicleRegistrationNumber,
                    companyOid = request.CompanyOid,
                    walletNumber = request.WalletNumber,
                    status = request.Status
                };

                // 3. Call external API (POST request use later as PUT not woking mentioned in documentation)
                var response = await httpClient.PostAsJsonAsync($"{_apiSettings.BaseUrl}/api/v2/wallet/unregister-vehicle", payload);
                var content = await response.Content.ReadAsStringAsync();

                // ৪. HTTP 200 OK Response Handling
                if (response.IsSuccessStatusCode)
                {
                    var rhdResult = JsonSerializer.Deserialize<RhdUnregisterResponse>(content, _jsonOptions);

                    return new VehicleUnregisterResponse
                    {
                        Success = true,
                        Reason = rhdResult?.HttpStatus ?? "OK",
                        Message = rhdResult?.Message ?? "Wallet status updated successfully.",
                        StatusCode = rhdResult?.HttpCode > 0 ? rhdResult.HttpCode : 200,
                        VehicleRegistrationNumber = rhdResult?.Body?.VehicleRegistrationNumber,
                        CompanyName = rhdResult?.Body?.CompanyName,
                        Type = rhdResult?.Body?.Type,
                        Status = rhdResult?.Body?.Status ?? 0
                    };
                }

                // ৫. Error Handling (404 / 409 / Default)
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    var errorResult = JsonSerializer.Deserialize<RhdErrorResponse>(content, _jsonOptions);
                    return new VehicleUnregisterResponse
                    {
                        Success = false,
                        Reason = errorResult?.Reason ?? "NOT_FOUND",
                        Message = errorResult?.Message ?? "No wallet records exist with the specified Vehicle Registration Number.",
                        StatusCode = 404
                    };
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    var conflictResult = JsonSerializer.Deserialize<RhdUnregisterResponse>(content, _jsonOptions);
                    return new VehicleUnregisterResponse
                    {
                        Success = false,
                        Reason = conflictResult?.HttpStatus ?? "CONFLICT",
                        Message = conflictResult?.Message ?? "An active wallet already exists for the vehicle registration number.",
                        StatusCode = 409,
                        VehicleRegistrationNumber = conflictResult?.Body?.VehicleRegistrationNumber,
                        CompanyName = conflictResult?.Body?.CompanyName,
                        Type = conflictResult?.Body?.Type,
                        Status = conflictResult?.Body?.Status ?? 1
                    };
                }

                return new VehicleUnregisterResponse
                {
                    Success = false,
                    Reason = "API_ERROR",
                    Message = $"API call failed with status {response.StatusCode}: {content}",
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during UnregisterVehicle for RegNo: {RegNo}", request?.VehicleRegistrationNumber);

                return new VehicleUnregisterResponse
                {
                    Success = false,
                    Reason = "EXCEPTION",
                    Message = ex.InnerException?.Message ?? ex.Message,
                    StatusCode = 500
                };
            }
        }
    }
}
