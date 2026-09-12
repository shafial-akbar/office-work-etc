using ETCAdminUI.Data;
using ETCAdminUI.Models;
using Microsoft.EntityFrameworkCore;

// Npgsql স্বয়ংক্রিয়ভাবে Unspecified তারিখগুলোকে PostgreSQL-এ সেভ করতে দেবে।
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// WebApplicationBuilder ইন্সট্যান্স তৈরি ও অ্যাপ্লিকেশনের সার্ভিস কনফিগারেশন সূচনা
var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. DATABASE CONFIGURATION (PostgreSQL Engine Setup)
// =========================================================================
// appsettings.json থেকে ConnectionString নিয়ে Entity Framework Core-এর মাধ্যমে PostgreSQL সংযোগ যুক্ত করা হচ্ছে
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<Etc.Shared.Interfaces.ISettlementService, ETCAdminUI.Services.SettlementService>();


// সেশন কনফিগারেশন (আপনার কোড অনুযায়ী)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

// এপিআই কল (APIProcessHelper) করার জন্য HttpClient সার্ভিসটি লাগবে, তাই এটি যোগ করা হলো
builder.Services.AddHttpClient();

// আপনার কাস্টম কনফিগারেশন ক্লাসের বাইন্ডিং (আপনার কোড অনুযায়ী)
builder.Services.Configure<ConfigValue>(builder.Configuration.GetSection("ConfigValue"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

// মিডলওয়্যার সিকোয়েন্স (আপনার কোড অনুযায়ী)
app.UseSession();
app.UseAuthorization();

// প্রথমবার প্রজেক্ট রান করলে যাতে লগইন পেজ আসে, সেজন্য Account/Login সেট করা ভালো
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
