using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

using EtcMwApi.Auth;
using EtcMwApi.Data;
using EtcMwApi.Services;
using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Npgsql স্বয়ংক্রিয়ভাবে Unspecified তারিখগুলোকে PostgreSQL-এ সেভ করতে দেবে।
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. DATABASE CONFIGURATION (PostgreSQL)
// =========================================================================
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DatabaseContext' not found in appsettings.json.")
    )
);

// =========================================================================
// 2. EXTERNAL HTTP CLIENT & SETTINGS CONFIGURATION
// =========================================================================
// Configure Strong-typed settings for external APIs
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ExternalApiSettings"));

// Named HttpClient for RHD External API integrations
builder.Services.AddHttpClient("RhdApiClient", client =>
{
    var baseUrl = builder.Configuration["ExternalApiSettings:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

// =========================================================================
// 3. DEPENDENCY INJECTION (Services & Repositories)
// =========================================================================
// Core Business Services
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<ICustomerOnboardingService, CustomerOnboardingService>();
builder.Services.AddScoped<ICustomerInquiryService, CustomerInquiryService>();

// External & Utility Services
builder.Services.AddScoped<IRhdApiService, RhdApiService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRequestLogService, RequestLogService>();

// HTTP Context Accessor for inspecting headers/ip in services
builder.Services.AddHttpContextAccessor();

// =========================================================================
// 4. CONTROLLERS & CORS CONFIGURATION
// =========================================================================
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// =========================================================================
// 5. AUTHENTICATION & AUTHORIZATION SERVICES
// =========================================================================
builder.Services.AddAuthorization();

// =========================================================================
// 6. SWAGGER / OPENAPI CONFIGURATION (Basic Auth Schema)
// =========================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Vehicle Information Middleware API",
        Version = "v1",
        Description = "Middleware API for handling ETC transactions and RHD integrations"
    });

    // Basic Authentication setup for Swagger UI
    c.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Basic Authorization header (Username & Password)"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "basic"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =========================================================================
// 7. LOGGING CONFIGURATION (Serilog)
// =========================================================================
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/etcmiddleware-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// =========================================================================
// 8. BUILD APPLICATION PIPELINE
// =========================================================================
var app = builder.Build();

// Enable Swagger in both Development and Production environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ETC Middleware API v1");
});

// Global Middleware Pipeline
app.UseCors("AllowAll");

app.UseRouting();

// Custom Middleware for Basic Authentication Handling
app.UseMiddleware<BasicAuthMiddleware>();

// Security Middlewares (Order is important)
app.UseAuthentication();
app.UseAuthorization();



// Map Controller Endpoints
app.MapControllers();

app.Run();