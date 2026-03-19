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
        public async Task<IActionResult> Index(string category, int page = 1)
        {
            const int pageSize = 24;

            // Sidebar için tüm aktif kategorileri çek (küçük veri - hızlı)
            var tumKategoriler = await _context.Categories
                .Where(c => c.IsActive == true)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Categories = tumKategoriler;

            // Temel sorgu — sadece Category (display için), AsNoTracking ile izleme yok
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive == true)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(category))
            {
                // A) İNDİRİM KATEGORİSİ
                if (category == "indirimli-firsatlar")
                {
                    ViewBag.PageTitle = "🔥 İndirimli Fırsatlar";
                    ViewBag.CurrentSlug = category;
                    productsQuery = productsQuery.Where(p => p.OldPrice.HasValue && p.OldPrice > p.Price);
                }
                else
                {
                    // B) NORMAL KATEGORİ — slug ile kategoriyi bul
                    var selectedCat = tumKategoriler
                        .FirstOrDefault(c => c.Slug.ToLower() == category.ToLower());

                    if (selectedCat != null)
                    {
                        ViewBag.PageTitle = selectedCat.CategoryName;
                        ViewBag.CurrentSlug = selectedCat.Slug;

                        // Tüm alt kategorileri bul (her derinlikte)
                        var catIds = new HashSet<int>();
                        void ToplaAltKategoriler(int parentId)
                        {
                            catIds.Add(parentId);
                            foreach (var alt in tumKategoriler.Where(c => c.ParentId == parentId))
                                ToplaAltKategoriler(alt.CategoryId);
                        }
                        ToplaAltKategoriler(selectedCat.CategoryId);

                        // Seçilen kategorinin doğrudan çocukları var mı?
                        var dorudanCocuklar = tumKategoriler
                            .Where(c => c.ParentId == selectedCat.CategoryId)
                            .OrderBy(c => c.DisplayOrder)
                            .ToList();

                        if (dorudanCocuklar.Any())
                        {
                            // GRUPLU GÖRÜNÜM: Her alt kategori için önizleme
                            var gruplar = new List<ProductGroupViewModel>();

                            foreach (var cocuk in dorudanCocuklar)
                            {
                                var cocukIds = new HashSet<int>();
                                void ToplaCocukIds(int pid)
                                {
                                    cocukIds.Add(pid);
                                    foreach (var c in tumKategoriler.Where(c => c.ParentId == pid))
                                        ToplaCocukIds(c.CategoryId);
                                }
                                ToplaCocukIds(cocuk.CategoryId);

                                var urunler = await _context.Products
                                    .Include(p => p.Category)
                                    .Where(p => p.IsActive == true && cocukIds.Contains(p.CategoryId))
                                    .AsNoTracking()
                                    .OrderByDescending(p => p.ProductId)
                                    .Take(8)
                                    .ToListAsync();

                                var toplam = await _context.Products
                                    .CountAsync(p => p.IsActive == true && cocukIds.Contains(p.CategoryId));

                                if (urunler.Any())
                                    gruplar.Add(new ProductGroupViewModel
                                    {
                                        Category = cocuk,
                                        Products = urunler,
                                        TotalCount = toplam
                                    });
                            }

                            ViewBag.ProductGroups = gruplar;
                            ViewBag.Category = category;
                            return View(new List<Product>());
                        }

                        // DÜZLEM GÖRÜNÜM: Yaprak kategori
                        productsQuery = productsQuery.Where(p => catIds.Contains(p.CategoryId));
                    }
                    else
                    {
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

            // Sayfalama (düzlem görünüm)
            var toplamUrun = await productsQuery.CountAsync();
            var products = await productsQuery
                .OrderByDescending(p => p.ProductId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)toplamUrun / pageSize);
            ViewBag.TotalCount = toplamUrun;
            ViewBag.Category = category;

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
        // GET: Products/Details/5010SYH
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductOptions)
                    .ThenInclude(o => o.Values)  // ← Values kullanıyoruz
                .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                .FirstOrDefaultAsync(m => m.ProductCode == id);

            if (product == null) return NotFound();

            var related = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == product.CategoryId
                         && p.ProductId != product.ProductId
                         && p.IsActive == true)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = related;
            return View(product);
        }
    }
}