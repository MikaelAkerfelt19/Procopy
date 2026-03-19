using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models; 
using System.Diagnostics;

namespace Procopy.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ProcopyContext _context; // Veritaban� ba�lant�m�z

        // Context'i buraya enjekte ediyoruz (Dependency Injection)
        public HomeController(ILogger<HomeController> logger, ProcopyContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vitrinUrunleri = await _context.Products
                                        .Include(p => p.Category)
                                        .Where(p => p.IsFeatured == true && p.IsActive == true)
                                        .OrderByDescending(p => p.ProductId)
                                        .Take(8)
                                        .ToListAsync();

            var anaKategoriler = await _context.Categories
                                        .Where(c => c.IsActive == true && c.ParentId == null && c.Slug != "indirimli-firsatlar")
                                        .OrderBy(c => c.DisplayOrder)
                                        .AsNoTracking()
                                        .ToListAsync();

            ViewBag.TopCategories = anaKategoriler;

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