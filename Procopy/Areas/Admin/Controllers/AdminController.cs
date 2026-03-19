using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models; 
using System.Linq;

namespace Procopy.Areas.Admin.Controllers
{
    [Area("Admin")]
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

                // Eğer mesaj tablon varsa burayı açabilirsin, yoksa şimdilik 0 kalsın
                // GelenMesajSayisi = _context.Messages.Count(), 
                GelenMesajSayisi = 0,

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

        [HttpPost]
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
            // INNER JOIN sorununu önlemek için kategorileri ayrı yükle
            var kategoriler = _context.Categories.AsNoTracking()
                                      .ToDictionary(c => c.CategoryId);

            var urunler = _context.Products.AsNoTracking()
                                  .OrderByDescending(p => p.ProductId)
                                  .ToList();

            // Navigation property'yi elle bağla (LEFT JOIN etkisi)
            foreach (var u in urunler)
            {
                if (kategoriler.TryGetValue(u.CategoryId, out var cat))
                    u.Category = cat;
            }

            return View(urunler);
        }

        // Mevcut ürünleri ProductCategories tablosuyla senkronize eder
        public IActionResult SyncProductCategories()
        {
            var urunler = _context.Products.AsNoTracking().ToList();
            int eklenen = 0;

            foreach (var urun in urunler)
            {
                bool mevcutMu = _context.ProductCategories
                    .Any(pc => pc.ProductId == urun.ProductId && pc.CategoryId == urun.CategoryId);

                if (!mevcutMu && urun.CategoryId > 0)
                {
                    _context.ProductCategories.Add(new ProductCategory
                    {
                        ProductId = urun.ProductId,
                        CategoryId = urun.CategoryId,
                        IsMainCategory = true
                    });
                    eklenen++;
                }
            }

            _context.SaveChanges();
            TempData["Bilgi"] = $"Senkronizasyon tamamlandı. {eklenen} ürün kategoriye bağlandı.";
            return RedirectToAction("Products");
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

    }
}