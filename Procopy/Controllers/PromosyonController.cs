using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models;

namespace Procopy.Controllers
{
    public class PromosyonController : Controller
    {
        private readonly ProcopyContext _context;

        public PromosyonController(ProcopyContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? category, int page = 1)
        {
            const int pageSize = 24;

            var tumKategoriler = await _context.Categories
                .Where(c => c.IsActive == true)
                .AsNoTracking()
                .ToListAsync();

            var anaKategori = tumKategoriler.FirstOrDefault(c => c.Slug == "promosyon");

            // Tüm promosyon id ağacı
            var tumPromosyonIds = new HashSet<int>();
            if (anaKategori != null)
            {
                void ToplaIds(int pid)
                {
                    tumPromosyonIds.Add(pid);
                    foreach (var alt in tumKategoriler.Where(c => c.ParentId == pid))
                        ToplaIds(alt.CategoryId);
                }
                ToplaIds(anaKategori.CategoryId);
            }

            // Ürün sayıları (tüm promosyon kategorileri için)
            var sayilarDict = await _context.Products
                .Where(p => p.IsActive == true && tumPromosyonIds.Contains(p.CategoryId))
                .GroupBy(p => p.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            // Recursive CategoryMenuItem oluşturucu
            CategoryMenuItem BuildMenuItem(Category cat)
            {
                var item = new CategoryMenuItem
                {
                    Id = cat.CategoryId,
                    Name = cat.CategoryName,
                    Slug = cat.Slug,
                    IconClass = GetCategoryIcon(cat.CategoryName),
                    ProductCount = sayilarDict.TryGetValue(cat.CategoryId, out var cnt) ? cnt : 0
                };

                var cocuklar = tumKategoriler
                    .Where(c => c.ParentId == cat.CategoryId)
                    .OrderBy(c => c.DisplayOrder)
                    .ToList();

                foreach (var cocuk in cocuklar)
                {
                    var cocukItem = BuildMenuItem(cocuk);
                    // Parent'ın sayısına çocukların sayısını ekle
                    item.ProductCount += cocukItem.Children.Sum(x => x.ProductCount);
                    item.Children.Add(cocukItem);
                }

                return item;
            }

            // Doğrudan alt kategoriler → iç içe ağaç
            var menuItems = anaKategori != null
                ? tumKategoriler
                    .Where(c => c.ParentId == anaKategori.CategoryId)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(k => BuildMenuItem(k))
                    .ToList()
                : new List<CategoryMenuItem>();

            // Aktif kategori
            Category? secilenKat = null;
            var seciliIds = new HashSet<int>();
            if (!string.IsNullOrEmpty(category))
            {
                secilenKat = tumKategoriler.FirstOrDefault(c => c.Slug == category);
                if (secilenKat != null)
                {
                    void ToplaSecili(int pid)
                    {
                        seciliIds.Add(pid);
                        foreach (var c in tumKategoriler.Where(c => c.ParentId == pid))
                            ToplaSecili(c.CategoryId);
                    }
                    ToplaSecili(secilenKat.CategoryId);
                }
            }

            // Ürün sorgusu
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive == true)
                .AsNoTracking()
                .AsQueryable();

            if (seciliIds.Any())
                query = query.Where(p => seciliIds.Contains(p.CategoryId));
            else if (tumPromosyonIds.Any())
                query = query.Where(p => tumPromosyonIds.Contains(p.CategoryId));

            var toplamUrun = await query.CountAsync();
            var products = await query
                .OrderBy(p => p.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new PromosyonCategoryViewModel
            {
                Categories = menuItems,
                ActiveCategory = secilenKat == null ? null : new CategoryMenuItem
                {
                    Id = secilenKat.CategoryId,
                    Name = secilenKat.CategoryName,
                    Slug = secilenKat.Slug,
                    IconClass = GetCategoryIcon(secilenKat.CategoryName),
                    ProductCount = toplamUrun
                },
                Products = products,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)toplamUrun / pageSize),
                TotalCount = toplamUrun,
                CurrentCategorySlug = category
            };

            return View(vm);
        }

        private static string GetCategoryIcon(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return "fas fa-tag";
            var n = categoryName.ToLowerInvariant();
            if (n.Contains("kalem")) return "fas fa-pen";
            if (n.Contains("defter") || n.Contains("ajanda")) return "fas fa-book";
            if (n.Contains("organizer")) return "fas fa-book-open";
            if (n.Contains("usb") || n.Contains("bellek")) return "fas fa-usb";
            if (n.Contains("power") || n.Contains("sarj") || n.Contains("şarj")) return "fas fa-battery-full";
            if (n.Contains("termos") || n.Contains("matara") || n.Contains("bardak") || n.Contains("kupa")) return "fas fa-mug-hot";
            if (n.Contains("saat")) return "fas fa-clock";
            if (n.Contains("cakmak") || n.Contains("çakmak")) return "fas fa-fire";
            if (n.Contains("tekstil") || n.Contains("tisort") || n.Contains("tişört")) return "fas fa-shirt";
            if (n.Contains("sapka") || n.Contains("şapka")) return "fas fa-hat-cowboy";
            if (n.Contains("canta") || n.Contains("çanta")) return "fas fa-briefcase";
            if (n.Contains("anahtarlik") || n.Contains("anahtarlık")) return "fas fa-key";
            if (n.Contains("takvim")) return "fas fa-calendar";
            if (n.Contains("bayrak")) return "fas fa-flag";
            if (n.Contains("teknoloj") || n.Contains("mouse")) return "fas fa-microchip";
            if (n.Contains("hesap")) return "fas fa-calculator";
            if (n.Contains("plaket") || n.Contains("odul") || n.Contains("ödül")) return "fas fa-award";
            if (n.Contains("etiket")) return "fas fa-tags";
            if (n.Contains("semsiye") || n.Contains("şemsiye")) return "fas fa-umbrella";
            if (n.Contains("vip") || n.Contains("set")) return "fas fa-gem";
            if (n.Contains("tabela")) return "fas fa-sign-hanging";
            return "fas fa-gift";
        }
    }
}
