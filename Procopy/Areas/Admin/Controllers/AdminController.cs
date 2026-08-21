using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models;
using System.Linq;

namespace Procopy.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ProcopyContext _context;

        // Veritabanı bağlantısını buraya da alıyoruz
        public AdminController(ProcopyContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Önceki adımda oluşturmanı istediğim ViewModel'i dolduruyoruz
            var model = new DashboardViewModel
            {
                // Products tablosundaki toplam sayı
                ToplamUrunSayisi = _context.Products.Count(),

                // Categories tablosundaki toplam sayı (Tablo adın Category ise Category.Count() yap)
                KategoriSayisi = _context.Categories.Count(),

                GelenMesajSayisi = _context.ContactMessages.Count(),

                // Şimdilik 0 olsun, sonra ekleriz
                WhatsappTiklanmaSayisi = 0
            };

            return View(model);
        }
        // KATEGORİ LİSTELEME
        // KATEGORİ LİSTELEME (SINIRSIZ DERİNLİK)
        public IActionResult CategoryList()
        {
              var tumKategoriler = _context.Categories.ToList();

            var anaKategoriler = tumKategoriler
                                      .Where(c => c.ParentId == null)
                                      .OrderBy(c => c.DisplayOrder)
                                      .ToList();

            return View(anaKategoriler);
        }
        // YENİ EKLEME SAYFASI (GET)
        [HttpGet]
        public IActionResult CategoryCreate(int? parentId)
        {
            // Eğer alt kategori ekliyorsak, kimin altına eklediğimizi View'a taşıyoruz
            ViewBag.ParentId = parentId;
            return View();
        }

        // KAYDETME İŞLEMİ (POST) - BOT ENTEGRASYONLU
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CategoryCreate(Category p)
        {
            // Slug ve diğer zorunlu alanlar boş gelirse hata vermemesi için
            if (string.IsNullOrEmpty(p.Slug))
            {
                p.Slug = (p.CategoryName ?? "kategori-" + DateTime.Now.Ticks).ToLower()
                    .Replace(" ", "-").Replace("ş", "s").Replace("ı", "i")
                    .Replace("ğ", "g").Replace("ç", "c").Replace("ö", "o")
                    .Replace("ü", "u").Replace(".", "").Replace("/", "");
            }

            // Ekleneceği yerdeki en son sırayı bulup +1 verelim
            var sonSira = _context.Categories
                                  .Where(x => x.ParentId == p.ParentId)
                                  .Max(x => (int?)x.DisplayOrder) ?? 0;

            p.DisplayOrder = sonSira + 1;
            p.CreateDate = DateTime.Now;
            p.IsActive = true;

            _context.Categories.Add(p);
            _context.SaveChanges();

            // --- ÖRÜMCEK AĞI BOTU TETİKLEYİCİ ---
            // Eğer kategori eklenirken "Kaynak Link (ImportUrl)" girildiyse botu çalıştır
            if (!string.IsNullOrEmpty(p.ImportUrl))
            {
                // DataImportController içindeki StartTreeImport metoduna yeni oluşan kategorinin ID'si ile gidiyoruz.
                // Bot bu ID'yi alıp o linkteki alt menüleri ve ürünleri taramaya başlayacak.
                return Redirect($"/DataImport/StartTreeImport?mainCategoryId={p.CategoryId}");
            }

            // Link girilmediyse normal listeye dön
            return RedirectToAction("CategoryList");
        }

        // SIRALAMA DEĞİŞTİRME (YUKARI/AŞAĞI)
        public IActionResult ChangeOrder(int id, string direction)
        {
            var kategori = _context.Categories.Find(id);
            if (kategori == null) return NotFound();

            // Kardeşlerini bul (Aynı babaya sahip olanlar)
            var kardesler = _context.Categories
                                    .Where(x => x.ParentId == kategori.ParentId)
                                    .OrderBy(x => x.DisplayOrder)
                                    .ToList();

            var index = kardesler.IndexOf(kategori);

            if (direction == "up" && index > 0)
            {
                var ustteki = kardesler[index - 1];
                // Swap (Takas) işlemi
                (kategori.DisplayOrder, ustteki.DisplayOrder) = (ustteki.DisplayOrder, kategori.DisplayOrder);
            }
            else if (direction == "down" && index < kardesler.Count - 1)
            {
                var alttaki = kardesler[index + 1];
                // Swap (Takas) işlemi
                (kategori.DisplayOrder, alttaki.DisplayOrder) = (alttaki.DisplayOrder, kategori.DisplayOrder);
            }

            _context.SaveChanges();
            return RedirectToAction("CategoryList");
        }

        // SİLME
        public IActionResult DeleteCategory(int id)
        {
            var kategori = _context.Categories.Include(x => x.Children).FirstOrDefault(x => x.CategoryId == id);
            if (kategori != null)
            {
                // Altında çocukları varsa sildirmiyoruz
                if (kategori.Children.Any())
                {
                    TempData["Hata"] = "Bu kategorinin alt başlıkları var! Önce onları silmelisin.";
                    return RedirectToAction("CategoryList");
                }

                _context.Categories.Remove(kategori);
                _context.SaveChanges();
            }
            return RedirectToAction("CategoryList");
        }
        // --- BU KISIM EKSİKTİ, BUNU EKLE ---

        // DÜZENLEME SAYFASINI AÇAR (GET)
        [HttpGet]
        public IActionResult CategoryEdit(int id)
        {
            var kategori = _context.Categories.Find(id);
            if (kategori == null) return NotFound();

            // Dropdown için diğer kategorileri gönderiyoruz (Kendisi hariç)
            // Böylece kategorinin yerini değiştirebilirsin (Başka birinin altına taşıyabilirsin)
            ViewBag.Kategoriler = _context.Categories
                                          .Where(x => x.CategoryId != id)
                                          .ToList();

            return View(kategori);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CategoryEdit(Category p)
        {
            var existing = _context.Categories.Find(p.CategoryId);
            if (existing != null)
            {
                existing.CategoryName = p.CategoryName;
                existing.IsActive = p.IsActive;

                // --- BOT LİNKİ GÜNCELLEME ---
                existing.ImportUrl = p.ImportUrl;
                // ----------------------------

                if (!string.IsNullOrEmpty(p.Slug))
                    existing.Slug = p.Slug;
                else
                    existing.Slug = p.CategoryName.ToLower()
                        .Replace("ş", "s").Replace("ı", "i").Replace("ğ", "g")
                        .Replace("ç", "c").Replace("ö", "o").Replace("ü", "u")
                        .Replace(" ", "-");

                _context.SaveChanges();
                return RedirectToAction("CategoryList");
            }
            return View(p);
        }
        public IActionResult Products()
        {
            // Ürünleri çekerken kategorilerini de (Include) yanına alıyoruz.
            // Böylece tabloda Kategori ID'si yerine Kategori Adını gösterebiliriz.
            var urunler = _context.Products
                                  .Include(p => p.Category) // Kategoriyi dahil et
                                  .ToList();

            return View(urunler);
        }
 
        [HttpGet]
        public IActionResult Create()
        {
            // Kategorileri Dropdown (Açılır Liste) için gönderiyoruz
            ViewBag.Categories = _context.Categories.ToList();
            return View();
        }

        // YENİ ÜRÜN KAYDETME (POST)
        // YENİ ÜRÜN KAYDET (POST) - GÜNCELLENMİŞ HALİ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product p)
        {

            if (!string.IsNullOrEmpty(p.SourceUrl) && string.IsNullOrEmpty(p.ProductName))
            {
                p.ProductName = "Yeni Ürün (Taranıyor...)";
            }

            // Slug Oluştur
            if (string.IsNullOrEmpty(p.Slug))
                p.Slug = (p.ProductName ?? "urun-" + DateTime.Now.Ticks).ToLower()
                    .Replace(" ", "-").Replace("ş", "s").Replace("ı", "i")
                    .Replace("ğ", "g").Replace("ç", "c").Replace("ö", "o")
                    .Replace("ü", "u").Replace(".", "").Replace("/", "");

            p.CreateDate = DateTime.Now;
            p.IsActive = true;

            _context.Products.Add(p);
            _context.SaveChanges();

            // Eğer Link girildiyse, doğrudan veri çekme sayfasına yönlendirebiliriz!
            if (!string.IsNullOrEmpty(p.SourceUrl))
            {
                // Kullanıcıyı direkt "Veri Çekme" işlemine yönlendiriyoruz
                return Redirect($"/DataImport/PullFromSource?productId={p.ProductId}");
            }

            return RedirectToAction("Products");
        }

        // ÜRÜN DÜZENLEME SAYFASI (GET)
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();

            ViewBag.Categories = _context.Categories.ToList();
            return View(product);
        }

        // ÜRÜN GÜNCELLEME (POST)
        [HttpPost]
        public IActionResult Edit(Product p)
        {
            var existing = _context.Products.Find(p.ProductId);
            if (existing != null)
            {
                existing.ProductName = p.ProductName;
                existing.CategoryId = p.CategoryId;
                existing.Price = p.Price;
                existing.OldPrice = p.OldPrice;
                existing.Description = p.Description;
                existing.MainImageUrl = p.MainImageUrl;
                existing.IsActive = p.IsActive;
                existing.IsFeatured = p.IsFeatured;
                existing.SourceUrl = p.SourceUrl;

                // Slug boşsa yenile
                if (string.IsNullOrEmpty(p.Slug))
                    existing.Slug = p.ProductName.ToLower().Replace(" ", "-");
                else
                    existing.Slug = p.Slug;

                _context.SaveChanges();
                return RedirectToAction("Products");
            }

            ViewBag.Categories = _context.Categories.ToList();
            return View(p);
        }

        public IActionResult Delete(int id)
        {
            var product = _context.Products.Find(id);
            if (product != null)
            {

                _context.Products.Remove(product);
                _context.SaveChanges();
            }
            return RedirectToAction("Products");
        }

        // GELEN MESAJLAR
        public IActionResult Messages(string? search, string? dateFilter, int page = 1)
        {
            int pageSize = 20;

            var query = _context.ContactMessages.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(m => m.Name.Contains(search) || m.Email.Contains(search) || m.Message.Contains(search) || (m.Subject != null && m.Subject.Contains(search)));

            if (!string.IsNullOrWhiteSpace(dateFilter))
            {
                var now = DateTime.Now;
                query = dateFilter switch
                {
                    "today" => query.Where(m => m.CreatedAt.Date == now.Date),
                    "week"  => query.Where(m => m.CreatedAt >= now.AddDays(-7)),
                    "month" => query.Where(m => m.CreatedAt >= now.AddMonths(-1)),
                    _       => query
                };
            }

            int total = query.Count();
            var messages = query.OrderByDescending(m => m.CreatedAt)
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .ToList();

            ViewBag.Search     = search;
            ViewBag.DateFilter = dateFilter;
            ViewBag.Page       = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Total      = total;

            // Raporlama istatistikleri
            ViewBag.TodayCount   = _context.ContactMessages.Count(m => m.CreatedAt.Date == DateTime.Today);
            ViewBag.WeekCount    = _context.ContactMessages.Count(m => m.CreatedAt >= DateTime.Now.AddDays(-7));
            ViewBag.MonthCount   = _context.ContactMessages.Count(m => m.CreatedAt >= DateTime.Now.AddMonths(-1));
            ViewBag.TotalCount   = _context.ContactMessages.Count();

            return View(messages);
        }

        public IActionResult DeleteMessage(int id)
        {
            var msg = _context.ContactMessages.Find(id);
            if (msg != null)
            {
                _context.ContactMessages.Remove(msg);
                _context.SaveChanges();
            }
            return RedirectToAction("Messages");
        }

        // VİTRİN YÖNETİMİ
        public IActionResult Vitrin(string? search, string? category)
        {
            var query = _context.Products
                                .Include(p => p.Category)
                                .Where(p => p.IsActive == true)
                                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category.CategoryName == category);

            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Categories = _context.Categories.OrderBy(c => c.CategoryName).ToList();
            ViewBag.FeaturedCount = _context.Products.Count(p => p.IsFeatured == true && p.IsActive == true);

            return View(query.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.ProductName).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleFeatured(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();

            product.IsFeatured = !(product.IsFeatured ?? false);
            _context.SaveChanges();

            return Json(new { success = true, isFeatured = product.IsFeatured });
        }

    }
}