using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;
using Etc.Shared.Constants;
using Etc.Shared.Models;
using ETCGatewayAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System;
using Microsoft.EntityFrameworkCore;

namespace ETCGatewayAPI.Controllers
{
    [ApiController]
    //[Route("api/[controller]")]
    [Route("api")]
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly DatabaseContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IConfiguration configuration,
            DatabaseContext context,
            ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Etc.Shared.DTOs.LoginRequest request)
        {
            try
            {
                // ১. Async DB Call
                var user = await _context.ApiUsers.FirstOrDefaultAsync(u => u.Username == request.Username);

                if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
                {
                    return Unauthorized(new { Message = "Invalid username or password" });
                }

                var token = GenerateJwtToken(user);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { Message = "Internal server error" });
            }
        }

        private string GenerateJwtToken(ApiUser user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, user.Role ?? "User"),
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(3), // UTC Time ব্যবহার করা আবশ্যক
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            // Real application-এ BCrypt.Net.BCrypt.Verify(password, storedHash) ব্যবহার করবেন
            return password == storedHash;
        }
    }
}
