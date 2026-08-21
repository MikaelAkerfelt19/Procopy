using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models;
using System.Diagnostics;

namespace Procopy.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ProcopyContext _context; // Veritabanı bağlantımız

        // Context'i buraya enjekte ediyoruz (Dependency Injection)
        public HomeController(ILogger<HomeController> logger, ProcopyContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Veritabanından 'IsFeatured' (öne çıkan) ve 'IsActive' (aktif) olan ürünleri getir.
            // Son eklenen 8 ürünü alalım.
            var vitrinUrunleri = await _context.Products
                                        .Include(p => p.Category) // Kategori adını da çekmek için
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

        [HttpGet]
        public IActionResult Contact()
        {
            return View(new ContactMessage());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(
            [Bind("Name,Email,Phone,Subject,Message")] ContactMessage model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // CreatedAt ve IpAddress formdan değil sunucudan doldurulur.
            model.CreatedAt = DateTime.Now;
            model.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            _context.ContactMessages.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Mesajınız bize ulaştı. En kısa sürede dönüş yapacağız.";
            return RedirectToAction(nameof(Contact));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
