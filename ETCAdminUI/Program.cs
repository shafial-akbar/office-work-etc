using ETCAdminUI.Data;
using ETCAdminUI.Models;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Interfaces;
using ETCGatewayAPI.Services;

// Npgsql স্বয়ংক্রিয়ভাবে Unspecified তারিখগুলোকে PostgreSQL-এ সেভ করতে দেবে।
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. DATABASE CONFIGURATION
// =========================================================================
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

// ETCAdminUI এর নিজস্ব DbContext (SettlementService ইত্যাদির জন্য)
builder.Services.AddDbContext<ETCAdminUI.Data.DatabaseContext>(options =>
    options.UseNpgsql(connectionString));

// Gateway-এর ReportService-এর জন্য প্রয়োজনীয় DbContext (👈 এই লাইনটি যোগ করুন)
builder.Services.AddDbContext<ETCGatewayAPI.Data.DatabaseContext>(options =>
    options.UseNpgsql(connectionString));

// =========================================================================
// 2. SERVICES REGISTRATION
// =========================================================================
builder.Services.AddControllersWithViews();

builder.Services.AddScoped<ISettlementService, ETCAdminUI.Services.SettlementService>();

// Other Project services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IReportService, ETCGatewayAPI.Services.ReportService>();
builder.Services.AddScoped<IWalletTransactionService, ETCGatewayAPI.Services.WalletTransactionService>();

// সেশন কনফিগারেশন
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// এপিআই কল করার জন্য HttpClient
builder.Services.AddHttpClient();

// কনফিগারেশন ক্লাস বাইন্ডিং
builder.Services.Configure<ConfigValue>(builder.Configuration.GetSection("ConfigValue"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();