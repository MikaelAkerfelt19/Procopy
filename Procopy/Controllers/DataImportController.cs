using Microsoft.AspNetCore.Mvc;
using HtmlAgilityPack;
using Procopy.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;

namespace Procopy.Controllers
{
    // Bu Controller, yönetici girişi gerektirmelidir.
    // [Authorize(Roles = "Admin")] // Eğer rol sisteminiz varsa açın
    public class DataImportController : Controller
    {
        private readonly ProcopyContext _context;
        private readonly IWebHostEnvironment _env;

        public DataImportController(ProcopyContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 1. ADIM: KATEGORİYİ TARAMAYI BAŞLAT
        public async Task<IActionResult> ImportCategory(int categoryId)
        {
            // Kategoriyi ve Linkini Bul
            var category = await _context.Categories.FindAsync(categoryId);
            if (category == null || string.IsNullOrEmpty(category.ImportUrl))
                return Content("<div style='color:red; padding:20px;'>Hata: Kategori bulunamadı veya 'Veri Çekme Linki' (ImportUrl) girilmemiş. <a href='/Admin/Admin/CategoryList'>Geri Dön</a></div>", "text/html");

            try
            {
                var web = new HtmlWeb();
                // Bot Engellemesini Aşmak İçin Gerçek Bir Tarayıcı Gibi Davran
                web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36";

                var doc = await web.LoadFromWebAsync(category.ImportUrl);

                // Ürün Linklerini Bul (Turkuaz vb. siteler için genel yapılar)
                // Genelde ürünler <li> veya <div> içinde <a> etiketindedir.
                var productNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-item')]//a[contains(@class, 'product-image')]")
                                   ?? doc.DocumentNode.SelectNodes("//li[contains(@class, 'product')]//a[contains(@class, 'woocommerce-LoopProduct-link')]")
                                   ?? doc.DocumentNode.SelectNodes("//div[@class='image']/a") // Bazı özel temalar
                                   ?? doc.DocumentNode.SelectNodes("//h3[contains(@class, 'product-title')]/a");

                if (productNodes == null)
                    return Content("<div style='color:red;'>Ürün listesi bulunamadı. Sitenin HTML yapısı farklı olabilir.</div>", "text/html");

                // Linkleri Benzersiz Yap (Aynı ürünü 2 kere çekmemek için)
                var links = productNodes.Select(n => n.GetAttributeValue("href", "")).Distinct().Where(l => !string.IsNullOrEmpty(l)).ToList();

                int addedCount = 0;
                int updatedCount = 0;
                List<string> errors = new List<string>();

                // HER BİR ÜRÜN İÇİN TEK TEK İŞLEM YAP (DÖNGÜ)
                foreach (var link in links)
                {
                    // Linki tam hale getir (https://... ekle)
                    string fullUrl = link.StartsWith("http") ? link : new Uri(new Uri(category.ImportUrl), link).ToString();

                    try
                    {
                        // DETAY SAYFASINA GİT VE VERİLERİ ÇEK
                        bool isNew = await ProcessProductDetail(fullUrl, categoryId);
                        if (isNew) addedCount++; else updatedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Hata ({fullUrl}): {ex.Message}");
                    }

                    // Sunucuyu yormamak için kısa bir bekleme (Opsiyonel)
                    // System.Threading.Thread.Sleep(500); 
                }

                return Content($@"
                    <div style='font-family:sans-serif; padding:20px; line-height:1.6;'>
                        <h2 style='color:green;'>İşlem Tamamlandı!</h2>
                        <ul>
                            <li><strong>Taranan Link Sayısı:</strong> {links.Count}</li>
                            <li><strong>Yeni Eklenen Ürün:</strong> {addedCount}</li>
                            <li><strong>Güncellenen Ürün:</strong> {updatedCount}</li>
                        </ul>
                        {(errors.Any() ? "<h4 style='color:red;'>Hatalar:</h4><ul>" + string.Join("", errors.Select(e => $"<li>{e}</li>")) + "</ul>" : "")}
                        <br>
                        <a href='/Admin/Admin/CategoryList' class='btn btn-primary'>Kategori Listesine Dön</a>
                        <a href='/Admin/Admin/Products' class='btn btn-secondary'>Ürünleri Gör</a>
                    </div>", "text/html");
            }
            catch (Exception ex)
            {
                return Content($"Genel Hata: {ex.Message}");
            }
        }

        // 2. ADIM: ÜRÜN DETAY SAYFASINI İŞLE (EN ÖNEMLİ KISIM)
        private async Task<bool> ProcessProductDetail(string url, int categoryId)
        {
            var web = new HtmlWeb();
            web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36";
            var doc = await web.LoadFromWebAsync(url);
            var root = doc.DocumentNode;

            // A) ÜRÜN ADI
            var titleNode = root.SelectSingleNode("//h1");
            if (titleNode == null) throw new Exception("Ürün başlığı (h1) bulunamadı.");
            string productName = titleNode.InnerText.Trim();
            productName = WebUtility.HtmlDecode(productName); // &amp; gibi karakterleri düzelt

            // B) ÜRÜN KODU (SKU)
            string productCode = "OTOMATIK-" + new Random().Next(1000, 9999);
            var skuNode = root.SelectSingleNode("//span[@class='sku']")
                          ?? root.SelectSingleNode("//span[contains(text(), 'Ürün Kodu')]/following-sibling::span")
                          ?? root.SelectSingleNode("//div[contains(text(), 'Ürün Kodu')]/span");

            if (skuNode != null) productCode = skuNode.InnerText.Trim();

            // C) FİYAT
            decimal price = 0;
            // Birçok farklı fiyat yapısını dene
            var priceNode = root.SelectSingleNode("//p[@class='price']//bdi")
                            ?? root.SelectSingleNode("//span[contains(@class, 'price')]//amount")
                            ?? root.SelectSingleNode("//div[@class='product-price']");

            if (priceNode != null)
            {
                // "1.250,50 TL" -> "1250,50"
                string cleanPrice = Regex.Replace(priceNode.InnerText, @"[^\d,]", "");
                decimal.TryParse(cleanPrice, out price);
            }

            // D) RESİM (İNDİRME İŞLEMİ)
            string localImagePath = null;
            var imgNode = root.SelectSingleNode("//div[contains(@class, 'woocommerce-product-gallery__image')]/a")
                          ?? root.SelectSingleNode("//meta[@property='og:image']")
                          ?? root.SelectSingleNode("//img[@id='bigpic']"); // Alternatif yapı

            if (imgNode != null)
            {
                string remoteUrl = imgNode.GetAttributeValue("href", "") != "" ? imgNode.GetAttributeValue("href", "") : imgNode.GetAttributeValue("content", "");
                if (!string.IsNullOrEmpty(remoteUrl))
                {
                    localImagePath = await DownloadImage(remoteUrl, productName);
                }
            }

            // E) VERİTABANI İŞLEMİ (EKLE VEYA GÜNCELLE)
            bool isNewProduct = false;
            // Ürünü URL'ye veya Koda göre bul
            var product = await _context.Products
                .Include(p => p.ProductOptions)
                .FirstOrDefaultAsync(p => p.SourceUrl == url || (p.ProductCode == productCode && p.ProductCode.Length > 5));

            if (product == null)
            {
                isNewProduct = true;
                product = new Product
                {
                    CreateDate = DateTime.Now,
                    IsActive = true,
                    IsFeatured = false,
                    SourceUrl = url, // Linki kaydet ki sonra tekrar bulabilelim
                    CategoryId = categoryId
                };
                _context.Products.Add(product);
            }

            // Bilgileri Güncelle
            product.ProductName = productName;
            product.ProductCode = productCode;
            product.Price = price;
            // Eğer resim indirdiysek güncelle, indiremediysek ve eskisi varsa dokunma
            if (localImagePath != null) product.MainImageUrl = localImagePath;

            // Slug (URL yapısı) oluştur
            if (string.IsNullOrEmpty(product.Slug)) product.Slug = GenerateSlug(productName);

            // Önce ürünü kaydet ki ID oluşsun (Option'lar için lazım)
            await _context.SaveChangesAsync();


            // F) ÖZEL VERİLERİ ÇEKME (EBAT, RENK, BASKI BİLGİSİ VB.)
            // Bu kısım sayfadaki TABLOLARI okur ve ProductOptions tablosuna yazar.

            // Mevcut seçenekleri temizle (Çift kayıt olmasın)
            if (product.ProductOptions != null && product.ProductOptions.Any())
            {
                _context.ProductOptions.RemoveRange(product.ProductOptions);
                await _context.SaveChangesAsync();
            }

            // 1. Tablo Taraması (Genelde "Ek Bilgi" sekmesinde olur)
            var tableRows = root.SelectNodes("//table[contains(@class, 'woocommerce-product-attributes')]//tr")
                            ?? root.SelectNodes("//table[contains(@class, 'shop_attributes')]//tr");

            if (tableRows != null)
            {
                int order = 1;
                foreach (var row in tableRows)
                {
                    var th = row.SelectSingleNode("th"); // Başlık (Örn: Ebat)
                    var td = row.SelectSingleNode("td"); // Değer (Örn: 10x20 cm)

                    if (th != null && td != null)
                    {
                        string specName = th.InnerText.Trim();
                        string specValue = td.InnerText.Trim();

                        // Gereksiz karakterleri temizle
                        specName = Regex.Replace(specName, ":", "");

                        // Seçeneği Kaydet
                        var option = new ProductOption
                        {
                            ProductId = product.ProductId,
                            Name = specName, // "Ebat", "Malzeme" vb.
                            IsDropdown = false, // Tablodan gelenler genelde bilgi amaçlıdır, seçim değildir.
                            DisplayOrder = order++
                        };
                        _context.ProductOptions.Add(option);
                        await _context.SaveChangesAsync();

                        // Değerini Kaydet
                        _context.ProductOptionValues.Add(new ProductOptionValue
                        {
                            OptionId = option.OptionId,
                            Name = specValue,
                            PriceAdjustment = 0
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            return isNewProduct;
        }

        // RESİM İNDİRME YARDIMCISI
        private async Task<string> DownloadImage(string url, string productName)
        {
            try
            {
                // URL bazen "//cdn..." şeklinde başlar, düzeltelim
                if (url.StartsWith("//")) url = "https:" + url;

                using (var client = new HttpClient())
                {
                    // Tarayıcı gibi davran
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                    var imageBytes = await client.GetByteArrayAsync(url);

                    // Dosya uzantısını al (jpg, png)
                    string ext = Path.GetExtension(url).Split('?')[0];
                    if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";

                    // Benzersiz isim oluştur: "kalem-modeli-guid.jpg"
                    string fileName = GenerateSlug(productName) + "-" + Guid.NewGuid().ToString().Substring(0, 4) + ext;

                    // Klasör yolu: wwwroot/images/products
                    string uploadFolder = Path.Combine(_env.WebRootPath, "images", "products");
                    if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                    string filePath = Path.Combine(uploadFolder, fileName);

                    // Diske yaz
                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                    // Veritabanına kaydedilecek yol
                    return "/images/products/" + fileName;
                }
            }
            catch (Exception)
            {
                // Resim indirilemezse null dön, işlemi bozma
                return null;
            }
        }

        // TÜRKÇE KARAKTER TEMİZLEME (SLUG İÇİN)
        private string GenerateSlug(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.ToLowerInvariant();
            text = text.Replace("ş", "s").Replace("ı", "i").Replace("ğ", "g").Replace("ç", "c").Replace("ö", "o").Replace("ü", "u");
            text = Regex.Replace(text, @"[^a-z0-9\s-]", ""); // Özel karakterleri sil
            text = Regex.Replace(text, @"\s+", "-"); // Boşlukları tire yap
            return text;
        }
    }
}