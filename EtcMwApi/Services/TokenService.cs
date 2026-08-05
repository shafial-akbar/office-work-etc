
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;

using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using EtcMwApi.Data;
using Microsoft.EntityFrameworkCore;

namespace EtcMwApi.Services
{
    public class TokenService : ITokenService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApiSettings _apiSettings;
        private readonly DatabaseContext _context;
        private readonly ILogger<TokenService> _logger;

        public TokenService(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiSettings,
            DatabaseContext context, ILogger<TokenService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _apiSettings = apiSettings.Value;
            _context = context;
            _logger = logger;
        }

        public async Task<string> GetToken()
        {
            try
            {
                var token = "";

                var _httpClient = _httpClientFactory.CreateClient("RhdApiClient");

                // Check for valid token in database
                var existingToken = await _context.ApiTokens
                    .Where(t => t.Expiry > DateTime.UtcNow)
                    .OrderByDescending(t => t.Expiry)
                    .FirstOrDefaultAsync();

                if (existingToken != null)
                {
                    return existingToken.Token;
                }

                // Get new token
                var loginRequest = new
                {
                    username = _apiSettings.Username,
                    password = _apiSettings.Password
                };

                var response = await _httpClient.PostAsJsonAsync($"{_apiSettings.BaseUrl}/api/v2/login", loginRequest);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get token. Status: {response.StatusCode}");
                    return null;
                }


                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // Handle case insensitivity
                };

                var content = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<AuthenticationResponse>(content, options);


                if (tokenResponse.Data==null)
                {
                    _logger.LogError("Token not found in response");
                    return null;
                }
                else
                {
                    token = tokenResponse.Data.Token;
                }

                
                var expiry = DateTime.UtcNow.AddHours(_apiSettings.TokenExpiryHours - 1); // Subtract 1 hour as buffer

                // Save token to database
                var apiToken = new ApiToken
                {
                    Token = token,
                    Expiry = expiry,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ApiTokens.Add(apiToken);
                await _context.SaveChangesAsync();

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token");
                return null;
            }
        }
    }
}
