using Hangfire;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Procopy.Jobs;
using Procopy.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ProcopyContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Bot için HTTP Client
builder.Services.AddHttpClient();

// Hangfire Kurulumu - Sadece Development ortamında
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHangfire(cfg =>
        cfg.UseSqlServerStorage(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            new Hangfire.SqlServer.SqlServerStorageOptions
            {
                PrepareSchemaIfNecessary = true,
                SchemaName = "HangFire",
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,
                EnableHeavyMigrations = true
            }));
    builder.Services.AddHangfireServer();
}

// Asıl veri çekme işlemini yapacak sınıf
builder.Services.AddScoped<TreeImportJob>();

// Cookie tabanlı kimlik doğrulama
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Account/Login";
        options.LogoutPath = "/Admin/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

var app = builder.Build();

// Development'ta geliştirici hata sayfası WebApplication tarafından otomatik eklenir;
// production'da hatalar yakalanmazsa boş 500 dönüyordu.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire İzleme Paneli - Sadece Development'ta
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new Procopy.Auth.HangfireAuthFilter()]
    });
}

// Admin Paneli Rotası
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Admin}/{action=Index}/{id?}");

// Varsayılan Rota
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
