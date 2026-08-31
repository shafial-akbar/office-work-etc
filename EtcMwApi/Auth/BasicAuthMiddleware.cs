using System.Security.Claims;
using System.Text;

namespace EtcMwApi.Auth
{
    public class BasicAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public BasicAuthMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Authorization header missing");
                return;
            }

            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid authorization scheme");
                return;
            }

            try
            {
                var encodedCredentials = authHeader["Basic ".Length..].Trim();
                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials)).Split(':', 2);

                var expectedUsername = _configuration["BasicAuth:Username"];
                var expectedPassword = _configuration["BasicAuth:Password"];

                if (credentials.Length == 2 &&
                    credentials[0] == expectedUsername &&
                    credentials[1] == expectedPassword)
                {
                    // 1. [Authorize] অ্যাট্রিবিউট কাজ করার জন্য context.User সেট করা জরুরি
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, credentials[0]),
                        new Claim(ClaimTypes.Name, credentials[0])
                    };
                    var identity = new ClaimsIdentity(claims, "Basic");
                    context.User = new ClaimsPrincipal(identity);

                    await _next(context);
                    return;
                }
            }
            catch (FormatException)
            {
                // Base64 ডিক্রিপ্টে সমস্যা হলে ক্র্যাশ না করে 401 ফেরত পাঠাবে
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid username or password");
        }
    }
}