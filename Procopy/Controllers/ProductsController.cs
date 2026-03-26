using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models;

namespace Procopy.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ProcopyContext _context;

        public ProductsController(ProcopyContext context)
        {
            _context = context;
        }

        // GET: Products
        public async Task<IActionResult> Index(string category)
        {
            // 1. Temel Sorgu: Sadece aktif ürünleri getir ve ilişkili tabloları dahil et
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                .Where(p => p.IsActive == true)
                .AsQueryable();

            // Sidebar için tüm aktif kategorileri çek
            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive == true).ToListAsync();

            if (!string.IsNullOrEmpty(category))
            {
                // A) İNDİRİM KATEGORİSİ ÖZEL DURUMU
                if (category == "indirimli-firsatlar")
                {
                    ViewBag.PageTitle = "🔥 İndirimli Fırsatlar";
                    ViewBag.CurrentSlug = category;

                    // Eski fiyatı olan ve indirimde olan ürünleri filtrele
                    productsQuery = productsQuery.Where(p => p.OldPrice.HasValue && p.OldPrice > p.Price);
                }
                else
                {
                    // B) NORMAL KATEGORİ FİLTRELEME
                    // Veritabanındaki slug ile eşleşen kategoriyi bul
                    var selectedCat = await _context.Categories
                        .FirstOrDefaultAsync(c => c.Slug.ToLower() == category.ToLower());

                    if (selectedCat != null)
                    {
                        ViewBag.PageTitle = selectedCat.CategoryName;
                        ViewBag.CurrentSlug = selectedCat.Slug;

                        if (selectedCat.ParentId == null)
                        {
                            // ANA KATEGORİ SEÇİLDİ: 
                            // Ürün ya bu ana kategoriye ya da bu kategorinin alt kategorilerinden birine bağlı olmalı.
                            productsQuery = productsQuery.Where(p =>
                                p.ProductCategories.Any(pc =>
                                    pc.CategoryId == selectedCat.CategoryId ||
                                    pc.Category.ParentId == selectedCat.CategoryId));
                        }
                        else
                        {
                            // ALT KATEGORİ SEÇİLDİ:
                            // Ürün direkt olarak bu alt kategoriye bağlı olmalı.
                            productsQuery = productsQuery.Where(p =>
                                p.ProductCategories.Any(pc => pc.CategoryId == selectedCat.CategoryId));
                        }
                    }
                    else
                    {
                        // Kategori slug bulunamadıysa "Tüm Ürünler"e geri dön
                        ViewBag.PageTitle = "Tüm Ürünler";
                        ViewBag.CurrentSlug = "";
                    }
                }
            }
            else
            {
                ViewBag.PageTitle = "Tüm Ürünler";
                ViewBag.CurrentSlug = "";
            }

            // Sonuçları ID'ye göre azalan sırada getir (En yeni en üstte)
            var products = await productsQuery.OrderByDescending(p => p.ProductId).ToListAsync();
            return View(products);
        }

        // POST: Products/LogWhatsappClick/5
        [HttpPost]
        public async Task<IActionResult> LogWhatsappClick(int id)
        {
            var stat = new ProductStat
            {
                ProductId = id,
                InteractionDate = DateTime.Now,
                InteractionType = 2, // 2: WhatsApp Tıklama
                Ipaddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            _context.ProductStats.Add(stat);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null) return NotFound();

            return View(product);
        }
    }
}