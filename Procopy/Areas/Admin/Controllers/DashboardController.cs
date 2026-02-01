using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models;

namespace Procopy.Areas.Admin.Controllers
{
    [Area("Admin")] // Bu Controller'ın Admin bölgesinde olduğunu belirtir (Çok Önemli!)
    public class DashboardController : Controller
    {
        private readonly ProcopyContext _context;

        public DashboardController(ProcopyContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // İSTATİSTİKLERİ TOPLA

            // 1. Toplam Ürün Sayısı
            ViewBag.TotalProducts = await _context.Products.CountAsync();

            // 2. Toplam Kategori Sayısı
            ViewBag.TotalCategories = await _context.Categories.CountAsync();

            // 3. Toplam WhatsApp Tıklaması (SQL'de Trigger ile topladığımız alan)
            // Eğer trigger çalışmadıysa veya eski veri varsa null gelmesin diye ?? 0 diyoruz.
            ViewBag.TotalClicks = await _context.Products.SumAsync(p => (int?)p.TotalWhatsappClicks) ?? 0;

            // 4. Gelen Mesaj Sayısı (ContactMessages tablosu)
            // Tabloyu eklemiştik ama veri yoksa hata vermesin diye try-catch koyabiliriz veya direkt sayarız.
            // Şimdilik basitçe sayalım.
            ViewBag.TotalMessages = await _context.ContactMessages.CountAsync();

            return View();
        }
    }
}