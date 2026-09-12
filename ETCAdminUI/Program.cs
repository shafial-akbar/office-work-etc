using ETCAdminUI.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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
