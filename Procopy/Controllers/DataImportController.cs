using Microsoft.AspNetCore.Mvc;
using HtmlAgilityPack;
using Procopy.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Procopy.Controllers
{
    public class DataImportController : Controller
    {
        private readonly ProcopyContext _context;
        private readonly IWebHostEnvironment _env;

        public DataImportController(ProcopyContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ===================================================================
        // 1. BAŞLAT (CategoryList'teki Buton Buraya Gider)
        // ===================================================================
        public async Task<IActionResult> StartTreeImport(int mainCategoryId)
        {
            var mainCategory = await _context.Categories.FindAsync(mainCategoryId);
            if (mainCategory == null || string.IsNullOrEmpty(mainCategory.ImportUrl))
                return Content("Hata: Ana kategori bulunamadı.");

            List<string> logs = new List<string>();
            logs.Add($"<b>TARAMA BAŞLADI:</b> {mainCategory.CategoryName}<br>Hedef: {mainCategory.ImportUrl}<hr>");
            var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Örümcek Başlıyor
                await CrawlRecursive(mainCategory.ImportUrl, mainCategoryId, logs, visitedUrls);

                return Content($@"
                    <div style='font-family:Segoe UI, sans-serif; padding:20px; color:#333;'>
                        <h3 style='color:#28a745;'>✔ İşlem Tamamlandı</h3>
                        <div style='background:#f4f4f4; padding:15px; border:1px solid #ddd; max-height:600px; overflow-y:auto; font-size:14px;'>
                            {string.Join("<br>", logs)}
                        </div>
                        <br><a href='/Admin/Admin/Products' class='btn btn-primary'>Ürünlere Git</a>
                    </div>", "text/html");
            }
            catch (Exception ex)
            {
                return Content($"<h3>KRİTİK HATA:</h3> {ex.Message}");
            }
        }

        // ===================================================================
        // 2. ÖRÜMCEK (Alt Kategorileri Bulur, Yoksa Ürünleri Çeker)
        // ===================================================================
        private async Task CrawlRecursive(string url, int parentId, List<string> logs, HashSet<string> visitedUrls)
        {
            if (!visitedUrls.Add(url))
            {
                logs.Add($"<span style='color:gray'>[⤴] Zaten tarandı: {url}</span>");
                return;
            }

            var web = new HtmlWeb();
            web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

            HtmlDocument doc;
            try { doc = await web.LoadFromWebAsync(url); }
            catch (Exception ex) { logs.Add($"<span style='color:red'>HATA: Linke gidilemedi ({url}) - {ex.Message}</span>"); return; }

            // A) SAYFADA ALT KATEGORİ VAR MI? (Kutu kutu listelenenler)
            var categoryGridNodes = doc.DocumentNode.SelectNodes("//ul[contains(@class,'products')]//li[contains(@class,'product-category')]//a");
            var subCats = categoryGridNodes
                          ?? doc.DocumentNode.SelectNodes("//li[contains(@class,'product-category')]//a")
                          ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'category')]//a")
                          ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'sub-menu')]//a");
            var hasProductList = doc.DocumentNode.SelectNodes("//ul[contains(@class,'products')]//li[contains(@class,'product')]//a[contains(@class,'woocommerce-LoopProduct-link')]")
                                 ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'product')]//a[contains(@class,'woocommerce-LoopProduct-link')]")
                                 ?? doc.DocumentNode.SelectNodes("//div[@class='image']/a")
                                 ?? doc.DocumentNode.SelectNodes("//h3/a")
                                 ?? doc.DocumentNode.SelectNodes("//h4/a");
            bool hasProducts = hasProductList != null && hasProductList.Count > 0;

            // Eğer sayfa bir ürün listesi değil de kategori listesi ise:
            if (subCats != null && subCats.Count > 0)
            {
                // Linklerin gerçekten kategori olup olmadığını kontrol et
                var validSubCats = subCats
                    .Where(x => !x.GetAttributeValue("href", "").Contains("product") && !x.InnerText.Contains("Sepete"))
                    .GroupBy(x => x.GetAttributeValue("href", "").Trim())
                    .Select(g => g.First())
                    .ToList();

                if (validSubCats.Any() && (categoryGridNodes != null || !hasProducts))
                {
                    logs.Add($"<b>📂 Alt Kategori Tespit Edildi ({validSubCats.Count} adet). İçlerine giriliyor...</b>");
                    var createdCategories = new List<(string Url, Category Category)>();
                    foreach (var node in validSubCats)
                    {
                        string href = node.GetAttributeValue("href", "");
                        string name = node.InnerText.Trim();
                        // Temizlik
                        name = Regex.Replace(name, @"\(\d+\)", "").Trim(); // (15) gibi sayıları sil
                        name = WebUtility.HtmlDecode(name);

                        if (string.IsNullOrEmpty(href) || name.Length < 2) continue;

                        string fullUrl = href.StartsWith("http") ? href : new Uri(new Uri(url), href).ToString();

                        // Veritabanı Kontrol / Ekleme (Önce hepsini oluştur)
                        var cat = await GetOrCreateCategory(name, parentId, fullUrl);
                        createdCategories.Add((fullUrl, cat));
                    }
                    foreach (var created in createdCategories)
                    {
                        // Recursion: O kategorinin de içine gir
                        await CrawlRecursive(created.Url, created.Category.CategoryId, logs, visitedUrls);
                    }
                    return; // Alt kategori varsa ürün arama, direkt çık.
                }
            }

            // B) ALT KATEGORİ YOKSA ÜRÜNLERİ TOPLA
            await ScrapeProducts(doc, url, parentId, logs, visitedUrls);
        }

        // ===================================================================
        // 3. ÜRÜN TOPLAYICI (LİSTEDEN LİNKLERİ BULUR)
        // ===================================================================
        private async Task ScrapeProducts(HtmlDocument doc, string baseUrl, int catId, List<string> logs, HashSet<string> visitedUrls)
        {
            // Ürün Linklerini Bul (Turkuaz vb. siteler için genel yapılar)
            var prodNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'product')]//a[contains(@class,'woocommerce-LoopProduct-link')]")
                            ?? doc.DocumentNode.SelectNodes("//div[@class='image']/a")
                            ?? doc.DocumentNode.SelectNodes("//h3/a")
                            ?? doc.DocumentNode.SelectNodes("//h4/a");

            if (prodNodes == null)
            {
                logs.Add($"<span style='color:orange'>[!] Bu sayfada ürün bulunamadı. (URL: {baseUrl})</span>");
                return;
            }

            var links = prodNodes.Select(x => x.GetAttributeValue("href", "")).Distinct().Where(x => x.Length > 5).ToList();
            logs.Add($"&rArr; <b>{links.Count}</b> ürün bulundu, detaylar çekiliyor...");

            int added = 0, skipped = 0;
            foreach (var link in links)
            {
                string fullUrl = link.StartsWith("http") ? link : new Uri(new Uri(baseUrl), link).ToString();

                // Detay sayfasına git ve veriyi çek
                var result = await ProcessProduct(fullUrl, catId);

                if (result.Status == "ok") added++;
                else
                {
                    skipped++;
                    // Neden atlandığını loga yaz (Hata ayıklamak için önemli)
                    // logs.Add($"<small style='color:gray'>Atlandı: {result.Message} - {fullUrl}</small>"); 
                }
            }
            logs.Add($"&nbsp;&nbsp;&nbsp; Sonuç: {added} Eklendi, {skipped} Atlandı.");

            var nextPageNode = doc.DocumentNode.SelectSingleNode("//a[contains(@class,'next') and contains(@class,'page-numbers')]")
                               ?? doc.DocumentNode.SelectSingleNode("//a[@rel='next']")
                               ?? doc.DocumentNode.SelectSingleNode("//link[@rel='next']");

            if (nextPageNode != null)
            {
                var nextHref = nextPageNode.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(nextHref))
                {
                    string nextUrl = nextHref.StartsWith("http") ? nextHref : new Uri(new Uri(baseUrl), nextHref).ToString();
                    if (visitedUrls.Add(nextUrl))
                    {
                        var web = new HtmlWeb();
                        web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                        try
                        {
                            var nextDoc = await web.LoadFromWebAsync(nextUrl);
                            logs.Add($"<small>➡ Sayfa devamı bulundu, devam ediliyor: {nextUrl}</small>");
                            await ScrapeProducts(nextDoc, nextUrl, catId, logs, visitedUrls);
                        }
                        catch (Exception ex)
                        {
                            logs.Add($"<span style='color:orange'>[!] Sonraki sayfa alınamadı ({nextUrl}) - {ex.Message}</span>");
                        }
                    }
                }
            }
        }

        // ===================================================================
        // 4. DETAY SAYFASI İŞLEME (FİYAT AVCISI BURADA)
        // ===================================================================
        private async Task<(string Status, string Message)> ProcessProduct(string url, int catId)
        {
            try
            {
                var web = new HtmlWeb();
                var doc = await web.LoadFromWebAsync(url);
                var root = doc.DocumentNode;

                // --- 1. İSİM ---
                var h1 = root.SelectSingleNode("//h1");
                string name = h1?.InnerText.Trim() ?? "İsimsiz Ürün";
                name = WebUtility.HtmlDecode(name);

                // --- 2. FİYAT (GELİŞMİŞ BULUCU) ---
                decimal price = ExtractPriceValues(root);

                // Eğer fiyat hala 0 ise ve "Fiyat Sorunuz" değilse, belki stok yoktur ama ürünü yine de ekleyelim mi?
                // İsteğine göre: Fiyat 0 ise ATLA diyordun.
                if (price <= 0) return ("skip", "Fiyat Bulunamadı veya 0");

                // --- 3. RESİM ---
                string imgPath = null;
                var imgNode = root.SelectSingleNode("//div[contains(@class,'gallery')]//img")
                              ?? root.SelectSingleNode("//figure//img")
                              ?? root.SelectSingleNode("//a[@data-fancybox='gallery']//img");

                if (imgNode != null)
                {
                    string src = imgNode.GetAttributeValue("src", "") != "" ? imgNode.GetAttributeValue("src", "") : imgNode.GetAttributeValue("href", "");
                    if (src.Length > 5) imgPath = await DownloadImage(src, name);
                }

                // --- 4. KAYDET ---
                var product = await _context.Products.Include(p => p.ProductOptions).FirstOrDefaultAsync(p => p.SourceUrl == url);
                bool isNew = (product == null);

                if (isNew)
                {
                    product = new Product { CreateDate = DateTime.Now, IsActive = true, SourceUrl = url, CategoryId = catId };
                    _context.Products.Add(product);
                }

                product.ProductName = name;
                product.Price = price;
                if (imgPath != null) product.MainImageUrl = imgPath;

                // Ürün Kodu (SKU)
                var skuNode = root.SelectSingleNode("//span[@class='sku']");
                product.ProductCode = skuNode != null ? skuNode.InnerText.Trim() : ("PR-" + new Random().Next(10000, 99999));

                if (string.IsNullOrEmpty(product.Slug)) product.Slug = GenerateSlug(name);

                await _context.SaveChangesAsync();

                // --- 5. ÖZELLİKLERİ ÇEK ---
                await ExtractFeatures(root, product.ProductId);

                return ("ok", "Eklendi");
            }
            catch (Exception ex) { return ("error", ex.Message); }
        }

        // ===================================================================
        // 5. YENİ FİYAT AYIKLAMA MOTORU (Regex Destekli)
        // ===================================================================
        private decimal ExtractPriceValues(HtmlNode root)
        {
            // 1. Önce standart class'lara bak
            var nodes = root.SelectNodes("//p[@class='price'] | //span[@class='price'] | //span[@class='amount'] | //div[contains(@class,'price')]");
            string rawText = "";

            if (nodes != null)
            {
                foreach (var node in nodes) rawText += " " + node.InnerText;
            }
            else
            {
                // Class yoksa, içinde 'TL' veya '₺' geçen herhangi bir metni al
                var textNodes = root.SelectNodes("//*[contains(text(),'TL')] | //*[contains(text(),'₺')]");
                if (textNodes != null)
                {
                    foreach (var t in textNodes) rawText += " " + t.InnerText;
                }
            }

            if (rawText.ToLowerInvariant().Contains("fiyat sor"))
            {
                return 0;
            }

            // 2. Metin içinden fiyatı söküp al (Regex)
            // Örnekler: "9.90 TL", "1,250.00 TL", "1.250 TL"
            // Desen: Rakamlar + (Nokta veya Virgül + Rakamlar)
            var matches = Regex.Matches(rawText, @"[\d]+(?:[.,]\d+)*");

            decimal bestPrice = 0;

            foreach (Match m in matches)
            {
                string val = m.Value;

                // Sadece yıl (2024, 2025) veya stok kodu (5055) olmasın diye basit kontrol:
                // Genelde fiyatlarda kuruş olur veya 'TL' kelimesine yakındır.
                // Biz en büyük mantıklı sayıyı veya TL'nin yanındakini almaya çalışalım.

                // TEMİZLİK:
                // Türkiye standardı: 1.250,50 (Binlik nokta, ondalık virgül)
                // Turkuaz sitesi (gözlem): 9.90 (Ondalık nokta) olabilir.

                decimal current = 0;
                string cleanVal = val;

                // Eğer hem nokta hem virgül varsa (1.250,50) -> Noktayı sil, virgülü nokta yap
                if (val.Contains(".") && val.Contains(","))
                {
                    cleanVal = val.Replace(".", "").Replace(",", ".");
                }
                // Sadece virgül varsa (9,90) -> Nokta yap
                else if (val.Contains(","))
                {
                    cleanVal = val.Replace(",", ".");
                }
                // Sadece nokta varsa (9.90 veya 1.000)
                else if (val.Contains("."))
                {
                    // Eğer son nokta 3 hane gerideyse (1.000) bu binliktir, sil.
                    // Değilse (9.90) ondalıktır, kalsın.
                    var parts = val.Split('.');
                    if (parts.Last().Length == 3 && parts.Length > 1)
                        cleanVal = val.Replace(".", ""); // Binlik ayırıcıyı sil
                }

                if (decimal.TryParse(cleanVal, NumberStyles.Any, CultureInfo.InvariantCulture, out current))
                {
                    // 0'dan büyük ve mantıklı bir fiyatsa (Örn: Yıl olan 2024'ü fiyat sanma)
                    // Genelde fiyatlar sayfada en belirgin yerdedir ama biz bulunan ilk geçerli fiyatı alalım
                    // ya da "TL" kelimesine en yakın olanı.
                    if (current > 0)
                    {
                        bestPrice = current;
                        // Eğer bu sayı "TL" kelimesinin hemen yanındaysa kesin fiyattır, döngüyü kır.
                        if (rawText.Contains(val + " TL") || rawText.Contains(val + "TL")) break;
                    }
                }
            }

            return bestPrice;
        }

        // ===================================================================
        // YARDIMCI METOTLAR
        // ===================================================================
        private async Task<Category> GetOrCreateCategory(string name, int parentId, string url)
        {
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryName == name && c.ParentId == parentId);
            if (cat == null)
            {
                cat = new Category
                {
                    CategoryName = name,
                    ParentId = parentId,
                    ImportUrl = url,
                    IsActive = true,
                    CreateDate = DateTime.Now,
                    Slug = GenerateSlug(name)
                };
                _context.Categories.Add(cat);
                await _context.SaveChangesAsync();
            }
            return cat;
        }

        private async Task ExtractFeatures(HtmlNode root, int productId)
        {
            // Eski özellikleri temizle
            var existing = _context.ProductOptions.Where(x => x.ProductId == productId);
            _context.ProductOptions.RemoveRange(existing);
            await _context.SaveChangesAsync();

            // Tabloları Tara
            var rows = root.SelectNodes("//table//tr");
            int order = 1;
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("th|td");
                    if (cells != null && cells.Count >= 2)
                    {
                        string key = cells[0].InnerText.Replace(":", "").Trim();
                        string val = cells[1].InnerText.Trim();
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                        {
                            var opt = new ProductOption { ProductId = productId, Name = key, DisplayOrder = order++ };
                            _context.ProductOptions.Add(opt);
                            await _context.SaveChangesAsync();
                            _context.ProductOptionValues.Add(new ProductOptionValue { OptionId = opt.OptionId, Name = val });
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }
        }

        private async Task<string> DownloadImage(string url, string name)
        {
            try
            {
                if (url.StartsWith("//")) url = "https:" + url;
                if (!url.StartsWith("http")) url = "https://www.turkuazpromosyon.com.tr" + url; // Relatif link düzeltme

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                    var bytes = await client.GetByteArrayAsync(url);
                    string ext = Path.GetExtension(url).Split('?')[0];
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                    string fileName = GenerateSlug(name) + "-" + Guid.NewGuid().ToString().Substring(0, 4) + ext;
                    string path = Path.Combine(_env.WebRootPath, "images", "products", fileName);
                    await System.IO.File.WriteAllBytesAsync(path, bytes);
                    return "/images/products/" + fileName;
                }
            }
            catch { return null; }
        }

        private string GenerateSlug(string text)
        {
            text = text.ToLowerInvariant().Replace("ş", "s").Replace("ı", "i").Replace("ğ", "g").Replace("ç", "c").Replace("ö", "o").Replace("ü", "u");
            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
            return Regex.Replace(text, @"\s+", "-");
        }
    }
}
