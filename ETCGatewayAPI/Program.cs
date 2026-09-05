using ETCGatewayAPI.Data;
using ETCGatewayAPI.Services;
using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using System;
using EtcMwApi.Services;

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


// =========================================================================
// 2. DEPENDENCY INJECTION (Services & Business Logic Lifetimes)
// =========================================================================
// অ্যাপ্লিকেশন লেভেলের বিজনেস লজিক সার্ভিসসমূহ Scope স্পেসিফিকেশনসহ ইনজেক্ট করা হচ্ছে (Per HTTP Request Lifetime)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICustomerInquiryServiceGW, CustomerInquiryServiceGW>();
builder.Services.AddScoped<IDoTranService, TranService>();
builder.Services.AddScoped<IWalletTransactionService, WalletTransactionService>();


// =========================================================================
// 3. SECURITY & AUTHENTICATION (JWT Bearer Token Validation)
// =========================================================================
// ইনকামিং HTTP রিকোয়েস্টের সাথে প্রেরিত JWT Bearer টোকেন যাচাইকরণের নিয়মাবলী কনফিগার করা হচ্ছে
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,                                            // টোকেন ইস্যুকারী সিগনেচার যাচাই
            ValidateAudience = true,                                          // ক্লায়েন্ট বা গ্রহীতার সিগনেচার যাচাই
            ValidateLifetime = true,                                          // টোকেনের মেয়াদের সময়সীমা যাচাই
            ValidateIssuerSigningKey = true,                                  // গোপন সিক্রেট কি (Secret Key) সিগনেচার ম্যাচিং যাচাই
            ValidIssuer = builder.Configuration["Jwt:Issuer"],                 // অনুমোদিত ইস্যুকারী ডোমেইন/নাম
            ValidAudience = builder.Configuration["Jwt:Audience"],             // অনুমোদিত অডিয়েন্স ডোমেইন/নাম
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))     // সিমেট্রিক এনক্রিপশন কি ডিকোড
        };
    });


// =========================================================================
// 4. STRUCTURED LOGGING CONFIGURATION (Serilog Setup)
// =========================================================================
// অ্যাপ্লিকেশনের ইভেন্ট এবং এরর ফাইল-ভিত্তিক অডিটিংয়ের জন্য Serilog কনফিগারেশন (দৈনিক রোটেটিং ফাইল অপশনসহ)
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/etcmiddleware-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

// ডিফল্ট ASP.NET Core প্রোভাইডারের জায়গায় Serilog সেটআপ যুক্ত করা
builder.Host.UseSerilog();


// =========================================================================
// 5. API CONTROLLERS & OPENAPI/SWAGGER DOCUMENTATION SETUP
// =========================================================================
// কন্ট্রোলার সার্ভিসেস এবং এন্ডপয়েন্ট এক্সপ্লোরার রেজিস্টার
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger UI ডকুমেন্টেশন কনফিগারেশন (JWT Bearer Token দিয়ে টেস্ট করার অপশনসহ)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ETC Middleware API", Version = "v1" });

    // 👈 Route Conflict (Duplicate GET api/check-account) সমাধানের জন্য এই লাইনটি যুক্ত করুন
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    // Swagger UI-তে Authorization Header এনাবল করার জন্য সিকিউরিটি ডেফিনিশন যুক্তকরণ
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme (e.g., 'Bearer <your_token>')",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // মেথড বা কন্ট্রোলার টেস্ট করার সময় ডাইনামিকালি Bearer টোকেন পাস করার স্কিম লিংক করা
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// =========================================================================
// 6. PIPELINE & MIDDLEWARE EXECUTION BUILD
// =========================================================================
var app = builder.Build();

// শুধুমাত্র ডেভলপমেন্ট এনভায়রনমেন্টে Swagger ডকুমেন্টেশন ভিজ্যুয়ালাইজ করার পাইপলাইন
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTP রিকোয়েস্ট নিরাপদ HTTPS প্রোটোকলে রিডাইরেক্ট করা
app.UseHttpsRedirection();

app.UseRouting();

// CORS (যদি ফ্রন্টএন্ড ভিন্ন ডোমেইন/পোর্টে থাকে)
app.UseCors();

// মিডলওয়্যার পাইপলাইন আর্কিটেকচার (Authentication অবশ্যই Authorization-এর পূর্বে থাকতে হবে)
app.UseAuthentication();  // রিকোয়েস্ট থেকে ইউজার বা ক্লায়েন্টের পরিচয় সনাক্তকরণ
app.UseAuthorization();   // শনাক্তকৃত ইউজারের নির্দিষ্ট মেথড অ্যাক্সেসের অধিকার যাচাই

// এন্ডপয়েন্ট হিসেবে সকল API কন্ট্রোলার রাউটিং ম্যাপ করা
app.MapControllers();

// অ্যাপ্লিকেশন চালুকরণ
app.Run();