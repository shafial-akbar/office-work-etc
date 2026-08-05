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

        public RhdApiService(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiSettings, ITokenService tokenService)
        {
            _httpClientFactory = httpClientFactory;
            _apiSettings = apiSettings.Value;
            _tokenService = tokenService;
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

        public async Task<ApiResponse<Wallet>> UnregisterVehicle(VehicleUnregisterRequest request)
        {
            try
            {
                var _httpClient = _httpClientFactory.CreateClient("RhdApiClient");

                // 1. Get authentication token
                var token = await _tokenService.GetToken();
                if (string.IsNullOrEmpty(token))
                {
                    return new ApiResponse<Wallet>
                    {
                        Success = false,
                        Message = "Failed to get authentication token",
                        StatusCode = 401
                    };
                }

                // 2. Prepare request to external API
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var payload = new
                {
                    vehicleRegistrationNumber = request.VehicleRegistrationNumber,
                    companyOid = request.CompanyOid,
                    walletNumber = request.WalletNumber,
                    status = request.Status
                };

                // 3. Call external API (POST request use later as PUT not woking mentioned in documentation)
                var response = await _httpClient.PostAsJsonAsync($"{_apiSettings.BaseUrl}/api/v2/wallet/unregister-vehicle", payload);
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // Handle case insensitivity
                };

                // 4. Handle successful response
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<VehicleRegistrationResponse>(content, options);

                    return new ApiResponse<Wallet>
                    {
                        Success = result.Success,
                        Reason = result.Reason,
                        Message = result.Message,
                        Data = result.Data, // The wallet info is in the 'body' property
                        StatusCode = (int)result.Code
                    };
                }

                // 5. Handle specific error cases based on status code
                switch (response.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        return new ApiResponse<Wallet>
                        {
                            Success = false,
                            Reason ="FAILED",
                            Message = "No wallet records exist with the specified Vehicle Registration Number",
                            StatusCode = (int)response.StatusCode
                        };

                    case HttpStatusCode.Conflict:
                        var conflictResponse = JsonSerializer.Deserialize<ConflictResponse>(content);
                        return new ApiResponse<Wallet>
                        {
                            Success = false,
                            Reason = "FAILED",
                            Message = conflictResponse.Message,
                            Data = conflictResponse.Body,
                            StatusCode = (int)response.StatusCode
                        };

                    default:
                        return new ApiResponse<Wallet>
                        {
                            Success = false,
                            Reason = "FAILED",
                            Message = $"API call failed: {content}",
                            StatusCode = (int)response.StatusCode
                        };
                }
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error unregistering vehicle");
                return new ApiResponse<Wallet>
                {
                    Success = false,
                    Reason = "EXCEPTION",
                    Message = ex.Message,
                    StatusCode = 500
                };
            }
        }
    }
}
