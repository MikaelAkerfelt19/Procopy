using Hangfire;
using Microsoft.EntityFrameworkCore;
using Procopy.Jobs;
using Procopy.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ProcopyContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Bot için HTTP Client
builder.Services.AddHttpClient();

// Hangfire Kurulumu
builder.Services.AddHangfire(cfg =>
    cfg.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer();

// Asýl veri çekme iþlemini yapacak sýnýf
builder.Services.AddScoped<TreeImportJob>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// Yetkilendirme (Giriþ iþlemleri için þart)
app.UseAuthorization();

// Hangfire Ýzleme Paneli
app.UseHangfireDashboard("/hangfire");

// Admin Paneli Rotasý (Bunu eklemezsek Admin açýlmaz)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Admin}/{action=Index}/{id?}");

// Varsayýlan Rota
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();