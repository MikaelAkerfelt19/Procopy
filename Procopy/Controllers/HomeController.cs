using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models; // Kendi proje isminle kontrol et
using System.Diagnostics;

namespace Procopy.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ProcopyContext _context; // Veritabaný baðlantýmýz

        // Context'i buraya enjekte ediyoruz (Dependency Injection)
        public HomeController(ILogger<HomeController> logger, ProcopyContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Veritabanýndan 'IsFeatured' (Öne Çýkan) olan ve 'IsActive' (Aktif) olan ürünleri getir.
            // Son eklenen 8 ürünü alalým.
            var vitrinUrunleri = await _context.Products
                                        .Include(p => p.Category) // Kategori adýný da çekmek için
                                        .Where(p => p.IsFeatured == true && p.IsActive == true)
                                        .OrderByDescending(p => p.ProductId)
                                        .Take(8)
                                        .ToListAsync();

            return View(vitrinUrunleri);
        }

        public IActionResult Kurumsal()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}