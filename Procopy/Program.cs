using Microsoft.EntityFrameworkCore;
using Procopy.Models;

namespace Procopy
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // MVC ve View servislerini ekle
            builder.Services.AddControllersWithViews();

            // Veritabaný baðlantýsýný ekle
            builder.Services.AddDbContext<ProcopyContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            var app = builder.Build();

            // HTTP request pipeline yapýlandýrmasý
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            // Area Rotasý (Admin paneli vb. için en üstte olmalý)
            app.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Admin}/{action=Index}/{id?}");

            // Varsayýlan Rota
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}