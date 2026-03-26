using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Procopy.Models;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Procopy.Jobs
{
    public class TreeImportJob
    {
        private readonly ProcopyContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        private static readonly Random _rng = new Random();

        public TreeImportJob(ProcopyContext context, IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        public async Task Run(int runId)
        {
            var run = await _context.ImportRuns.FirstOrDefaultAsync(x => x.RunId == runId);
            if (run == null) throw new Exception("ImportRun bulunamadı.");

            run.Status = "running";
            await _context.SaveChangesAsync();

            var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                await Log(runId, "info", run.StartUrl, "Başladı", $"MainCategoryId={run.MainCategoryId}");
                await CrawlRecursive(run.StartUrl, run.MainCategoryId, runId, visitedUrls);
                run.Status = "done";
            }
            catch (Exception ex)
            {
                run.Status = "failed";
                run.Errors += 1;
                await Log(runId, "error", null, "Kritik Hata", ex.ToString());
            }
            finally
            {
                run.EndAt = DateTime.Now;
                run.PagesVisited = visitedUrls.Count;
                await _context.SaveChangesAsync();
                await Log(runId, "info", null, "Bitti", $"Status={run.Status}, Pages={run.PagesVisited}, Added={run.ProductsAdded}, Skipped={run.ProductsSkipped}, Errors={run.Errors}");
            }
        }

        private HtmlWeb CreateHtmlWeb()
        {
            return new HtmlWeb
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            };
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            url = url.Trim();
            if (url.StartsWith("//")) url = "https:" + url;
            return url;
        }

        private static string BuildAbsoluteUrl(string baseUrl, string href)
        {
            if (string.IsNullOrWhiteSpace(href)) return baseUrl;
            href = href.Trim();
            if (href.StartsWith("//")) href = "https:" + href;
            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return href;
            return new Uri(new Uri(baseUrl), href).ToString();
        }

        private async Task CrawlRecursive(string url, int parentId, int runId, HashSet<string> visitedUrls)
        {
            url = NormalizeUrl(url);

            if (!visitedUrls.Add(url))
            {
                await Log(runId, "debug", url, "Zaten tarandı", null);
                return;
            }

            var web = CreateHtmlWeb();
            HtmlDocument doc;

            try
            {
                doc = await web.LoadFromWebAsync(url);
            }
            catch (Exception ex)
            {
                var r = await _context.ImportRuns.FirstAsync(x => x.RunId == runId);
                r.Errors += 1;
                await _context.SaveChangesAsync();
                await Log(runId, "warn", url, "Sayfa alınamadı", ex.Message);
                return;
            }

            var categoryGridNodes = doc.DocumentNode.SelectNodes("//ul[contains(@class,'products')]//li[contains(@class,'product-category')]//a");

            var subCats =
                categoryGridNodes
                ?? doc.DocumentNode.SelectNodes("//li[contains(@class,'product-category')]//a")
                ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'category')]//a")
                ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'sub-menu')]//a");

            // CrawlRecursive metodu içindeki hasProductList tanımını şu şekilde değiştir:

            var hasProductList =
                doc.DocumentNode.SelectNodes("//div[contains(@class,'product-block')]") // YENİ EKLENEN SATIR
                ?? doc.DocumentNode.SelectNodes("//ul[contains(@class,'products')]//li[contains(@class,'product')]//a[contains(@class,'woocommerce-LoopProduct-link')]")
                ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'product')]//a[contains(@class,'woocommerce-LoopProduct-link')]")
                ?? doc.DocumentNode.SelectNodes("//div[@class='image']/a")
                ?? doc.DocumentNode.SelectNodes("//h3/a")
                ?? doc.DocumentNode.SelectNodes("//h4/a");

            bool hasProducts = hasProductList != null && hasProductList.Count > 0;

            if (subCats != null && subCats.Count > 0)
            {
                var validSubCats = subCats
                    .Select(n => new
                    {
                        Href = (n.GetAttributeValue("href", "") ?? "").Trim(),
                        Text = WebUtility.HtmlDecode((n.InnerText ?? "").Trim())
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Href))
                    .Where(x => !x.Text.Contains("Sepete", StringComparison.OrdinalIgnoreCase))
                    .Where(x => !Regex.IsMatch(x.Href, @"/product(/|$)", RegexOptions.IgnoreCase))
                    .GroupBy(x => x.Href, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (validSubCats.Any() && (categoryGridNodes != null || !hasProducts))
                {
                    await Log(runId, "info", url, "Alt kategori bulundu", $"Count={validSubCats.Count}");

                    var created = new List<(string Url, int CatId, string Name)>();

                    foreach (var item in validSubCats)
                    {
                        var name = Regex.Replace(item.Text, @"\(\d+\)", "").Trim();
                        if (string.IsNullOrWhiteSpace(name) || name.Length < 2) continue;

                        var fullUrl = NormalizeUrl(BuildAbsoluteUrl(url, item.Href));
                        var cat = await GetOrCreateCategory(name, parentId, fullUrl);
                        created.Add((fullUrl, cat.CategoryId, name));
                    }

                    foreach (var c in created)
                    {
                        await Log(runId, "debug", c.Url, "Kategoriye giriliyor", $"{c.Name} (CatId={c.CatId})");
                        await CrawlRecursive(c.Url, c.CatId, runId, visitedUrls);
                    }

                    return;
                }
            }

            await ScrapeProducts(doc, url, parentId, runId, visitedUrls);
        }

        private async Task ScrapeProducts(HtmlDocument doc, string baseUrl, int catId, int runId, HashSet<string> visitedUrls)
        {
            baseUrl = NormalizeUrl(baseUrl);

            // 1. Ürün bloklarını bul
            var productBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class,'product-block')]");

            if (productBlocks == null || productBlocks.Count == 0)
            {
                await Log(runId, "warn", baseUrl, "Ürün bulunamadı", null);
                return;
            }

        
            var productsToProcess = new List<(string Link, string ImageUrl, string Code, string RawPrice)>();

            foreach (var block in productBlocks)
            {
                var linkNode = block.SelectSingleNode(".//div[@class='image']/a");
                var imgNode = block.SelectSingleNode(".//div[@class='image']//img");

                // "Ürün Kodu :" yazan small'dan sonraki a > span'ı hedef al
                var codeNode = block.SelectSingleNode(
                    ".//small[contains(text(),'Ürün Kodu')]/following-sibling::a[1]/span"
                );

                // Yukarıdaki çalışmazsa fallback
                if (codeNode == null)
                    codeNode = block.SelectSingleNode(
                        ".//span[contains(@class,'special-price') and contains(@class,'kalin')]"
                    );
                var priceNode = block.SelectSingleNode(
      ".//div[contains(@class,'price-gruop')]"
  );

                string rawPrice = WebUtility.HtmlDecode(
                    priceNode?.InnerText ?? ""
                ).Replace("TL", "").Replace("₺", "").Trim();
                string href = linkNode?.GetAttributeValue("href", "")?.Trim();
                string imgSrc = imgNode?.GetAttributeValue("src", "")?.Trim();

                string productCode = codeNode?.InnerText?.Trim();

                // Boşluk, &nbsp; ve HTML entity temizliği
                if (!string.IsNullOrWhiteSpace(productCode))
                {
                    productCode = WebUtility.HtmlDecode(productCode).Trim();
                    productCode = Regex.Replace(productCode, @"\s+", ""); // tüm boşlukları sil
                }

                if (!string.IsNullOrWhiteSpace(href))
                {
                    productsToProcess.Add((href, imgSrc, productCode, rawPrice));
                }

            }

            // Aynı linkleri temizle
            productsToProcess = productsToProcess
                .Where(x => x.Link.Length > 5)
                .GroupBy(x => x.Link, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            await Log(runId, "info", baseUrl, "Ürün listesi", $"Count={productsToProcess.Count}, CatId={catId}");

            int added = 0;
            int skipped = 0;

            foreach (var item in productsToProcess)
            {
                var fullUrl = NormalizeUrl(BuildAbsoluteUrl(baseUrl, item.Link));

                // 4 elemanı da ProcessProduct'a başarıyla gönderiyoruz
                var result = await ProcessProduct(fullUrl, item.ImageUrl, item.Code, item.RawPrice, catId, runId);

                if (result.Status == "ok") added++;
                else skipped++;
            }

            var run = await _context.ImportRuns.FirstAsync(x => x.RunId == runId);
            run.ProductsAdded += added;
            run.ProductsSkipped += skipped;
            await _context.SaveChangesAsync();

            await Log(runId, "info", baseUrl, "Sayfa sonucu", $"Added={added}, Skipped={skipped}");

            // Pagination kısmı aynen kalıyor
            var nextPageNode = doc.DocumentNode.SelectSingleNode("//a[contains(@class,'next') and contains(@class,'page-numbers')]")
                               ?? doc.DocumentNode.SelectSingleNode("//a[@rel='next']")
                               ?? doc.DocumentNode.SelectSingleNode("//link[@rel='next']");

            if (nextPageNode != null)
            {
                var nextHref = (nextPageNode.GetAttributeValue("href", "") ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(nextHref))
                {
                    var nextUrl = NormalizeUrl(BuildAbsoluteUrl(baseUrl, nextHref));
                    if (visitedUrls.Add(nextUrl))
                    {
                        var web = CreateHtmlWeb();
                        try
                        {
                            var nextDoc = await web.LoadFromWebAsync(nextUrl);
                            await Log(runId, "debug", nextUrl, "Pagination", "Next page");
                            await ScrapeProducts(nextDoc, nextUrl, catId, runId, visitedUrls);
                        }
                        catch (Exception ex)
                        {
                            run.Errors += 1;
                            await _context.SaveChangesAsync();
                            await Log(runId, "warn", nextUrl, "Pagination alınamadı", ex.Message);
                        }
                    }
                }
            }
        }

        // DİKKAT: Metot imzasına listImageUrl eklendi
        // DİKKAT: Parametre sayısı 6'ya çıkarıldı!
        private async Task<(string Status, string Message)> ProcessProduct(string url, string listImageUrl, string productCode, string rawPrice, int catId, int runId)
        {
            try
            {
                url = NormalizeUrl(url);

                var web = CreateHtmlWeb();
                var doc = await web.LoadFromWebAsync(url);
                var root = doc.DocumentNode;

                var h1 = root.SelectSingleNode("//h1");
                string name = WebUtility.HtmlDecode(h1?.InnerText?.Trim() ?? "İsimsiz Ürün");

                // 1. Fiyatı listeden gelen metinden ayrıştır (ParsePriceFromText metodunu eklediğinden emin ol)
                decimal price = ParsePriceFromText(rawPrice);

                // Eğer listeden fiyat gelmediyse (teklif iste vb.), detay sayfasında aramaya çalış
                if (price <= 0)
                {
                    price = ExtractPriceValues(root);
                }

                if (price <= 0)
                {
                    await Log(runId, "debug", url, "Atlandı", "Fiyat Bulunamadı/0/Teklif");
                    return ("skip", "no-price");
                }

                string imgPath = null;

                // 2. Resmi doğrudan kategori sayfasından gelen URL'den indir
                if (!string.IsNullOrWhiteSpace(listImageUrl))
                {
                    imgPath = await DownloadImage(listImageUrl, name, url);
                }

                var product = await _context.Products.FirstOrDefaultAsync(p => p.SourceUrl == url);
                bool isNew = product == null;

                if (isNew)
                {
                    product = new Product
                    {
                        CreateDate = DateTime.Now,
                        IsActive = true,
                        SourceUrl = url,
                        CategoryId = catId
                    };
                    _context.Products.Add(product);
                }

                product.ProductName = name;
                product.Price = price;

                if (!string.IsNullOrWhiteSpace(imgPath))
                    product.MainImageUrl = imgPath;

                // 3. Ürün Kodunu Kaydet (Listeden gelen 5010SYH gibi kodu yaz)
                if (!string.IsNullOrWhiteSpace(productCode))
                    product.ProductCode = productCode;
                else
                    product.ProductCode = $"PR-{_rng.Next(10000, 99999)}";

                // 4. SEO ve Link Yapısı (plastik-kalem-5010syh şeklinde oluştur)
                if (string.IsNullOrWhiteSpace(product.Slug) || isNew)
                {
                    product.Slug = GenerateSlug(name) + "-" + product.ProductCode.ToLowerInvariant();
                }

                await _context.SaveChangesAsync();

                // 5. Özellikleri detay sayfasından (tablodan) çek
                await ExtractFeatures(root, product.ProductId);

                await Log(runId, "info", url, isNew ? "Ürün eklendi" : "Ürün güncellendi", $"{name} | {price.ToString("0.00")} TL | KOD: {product.ProductCode}");

                return ("ok", "saved");
                await Log(runId, "debug", url, "Kod Debug",
    $"RawCode='{productCode}' | RawPrice='{rawPrice}'");
            }
            catch (Exception ex)
            {
                var run = await _context.ImportRuns.FirstAsync(x => x.RunId == runId);
                run.Errors += 1;
                await _context.SaveChangesAsync();
                await Log(runId, "error", url, "Ürün hatası", ex.ToString());
                return ("error", ex.Message);
            }
        }
        private decimal ParsePriceFromText(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return 0;

            var matches = Regex.Matches(rawText, @"\d+(?:[.,]\d+)*");
            decimal best = 0;

            foreach (Match m in matches)
            {
                string val = m.Value;
                string clean = val;

                if (val.Contains('.') && val.Contains(','))
                    clean = val.Replace(".", "").Replace(",", ".");
                else if (val.Contains(','))
                    clean = val.Replace(",", ".");
                else if (val.Contains('.'))
                {
                    var parts = val.Split('.');
                    if (parts.Length > 1 && parts.Last().Length == 3)
                        clean = val.Replace(".", "");
                }

                if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var current))
                {
                    if (current > 0)
                    {
                        best = current;
                        break;
                    }
                }
            }
            return best;
        }
        private decimal ExtractPriceValues(HtmlNode root)
        {
            var nodes = root.SelectNodes("//p[@class='price'] | //span[@class='price'] | //span[@class='amount'] | //div[contains(@class,'price')]");
            string rawText = "";

            if (nodes != null)
            {
                foreach (var node in nodes)
                    rawText += " " + WebUtility.HtmlDecode(node.InnerText);
            }
            else
            {
                var textNodes = root.SelectNodes("//*[contains(text(),'TL')] | //*[contains(text(),'₺')]");
                if (textNodes != null)
                {
                    foreach (var t in textNodes)
                        rawText += " " + WebUtility.HtmlDecode(t.InnerText);
                }
            }

            if (string.IsNullOrWhiteSpace(rawText))
                return 0;

            var lower = rawText.ToLowerInvariant();
            if (lower.Contains("fiyat sor") || lower.Contains("teklif") || lower.Contains("sorunuz"))
                return 0;

            var matches = Regex.Matches(rawText, @"\d+(?:[.,]\d+)*");
            decimal best = 0;

            foreach (Match m in matches)
            {
                string val = m.Value;
                string clean = val;

                if (val.Contains('.') && val.Contains(','))
                    clean = val.Replace(".", "").Replace(",", ".");
                else if (val.Contains(','))
                    clean = val.Replace(",", ".");
                else if (val.Contains('.'))
                {
                    var parts = val.Split('.');
                    if (parts.Length > 1 && parts.Last().Length == 3)
                        clean = val.Replace(".", "");
                }

                if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var current))
                {
                    if (current > 0)
                    {
                        best = current;
                        if (Regex.IsMatch(rawText, Regex.Escape(val) + @"\s*(TL|₺)", RegexOptions.IgnoreCase))
                            break;
                    }
                }
            }

            return best;
        }

        private async Task ExtractFeatures(HtmlNode root, int productId)
        {
            var optionIds = await _context.ProductOptions
                .Where(x => x.ProductId == productId)
                .Select(x => x.OptionId)
                .ToListAsync();

            if (optionIds.Count > 0)
            {
                var existingValues = await _context.ProductOptionValues
                    .Where(v => optionIds.Contains(v.OptionId))
                    .ToListAsync();

                if (existingValues.Count > 0)
                    _context.ProductOptionValues.RemoveRange(existingValues);

                var existingOptions = await _context.ProductOptions
                    .Where(x => x.ProductId == productId)
                    .ToListAsync();

                if (existingOptions.Count > 0)
                    _context.ProductOptions.RemoveRange(existingOptions);

                await _context.SaveChangesAsync();
            }

            var rows = root.SelectNodes("//table//tr");
            if (rows == null || rows.Count == 0) return;

            var options = new List<ProductOption>();
            int order = 1;

            foreach (var row in rows)
            {
                var cells = row.SelectNodes("th|td");
                if (cells == null || cells.Count < 2) continue;

                string key = WebUtility.HtmlDecode((cells[0].InnerText ?? "").Replace(":", "").Trim());
                string val = WebUtility.HtmlDecode((cells[1].InnerText ?? "").Trim());

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(val)) continue;

                options.Add(new ProductOption
                {
                    ProductId = productId,
                    Name = key,
                    DisplayOrder = order++
                });
            }

            if (options.Count == 0) return;

            _context.ProductOptions.AddRange(options);
            await _context.SaveChangesAsync();

            var values = new List<ProductOptionValue>();

            foreach (var opt in options)
            {
                var row = rows.FirstOrDefault(r =>
                {
                    var c = r.SelectNodes("th|td");
                    if (c == null || c.Count < 2) return false;
                    string k = WebUtility.HtmlDecode((c[0].InnerText ?? "").Replace(":", "").Trim());
                    return string.Equals(k, opt.Name, StringComparison.OrdinalIgnoreCase);
                });

                if (row != null)
                {
                    var c = row.SelectNodes("th|td");
                    string v = WebUtility.HtmlDecode((c[1].InnerText ?? "").Trim());
                    if (!string.IsNullOrWhiteSpace(v))
                    {
                        values.Add(new ProductOptionValue
                        {
                            OptionId = opt.OptionId,
                            Name = v
                        });
                    }
                }
            }

            if (values.Count > 0)
            {
                _context.ProductOptionValues.AddRange(values);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<string> DownloadImage(string url, string name, string productPageUrl)
        {
            // 1. URL boşsa hiç işleme başlama
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                url = NormalizeUrl(url);
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    url = BuildAbsoluteUrl(productPageUrl, url);

                var client = _httpClientFactory.CreateClient();

                // 2. Tarayıcıyı taklit eden güçlü Header'lar (Bot korumasını aşmak için)
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.Add("Referer", productPageUrl); // Hotlink koruması için kritik!

                // 3. GetByteArrayAsync yerine GetAsync kullanıp yanıtı kontrol edelim
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    // Eğer resim indirilemezse sessizce kalmasın, konsola veya debug'a düşsün
                    Console.WriteLine($"Resim İndirilemedi! Status: {response.StatusCode} - URL: {url}");
                    return null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();

                // 4. Uzantı yakalama mantığını geliştirme
                string ext = Path.GetExtension(url.Split('?')[0]);
                if (string.IsNullOrWhiteSpace(ext) || ext.Length > 5)
                    ext = ".jpg"; // Varsayılan uzantı

                string fileName = $"{GenerateSlug(name)}-{Guid.NewGuid().ToString("N")[..6]}{ext}";
                string folder = Path.Combine(_env.WebRootPath, "images", "products");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, fileName);
                await File.WriteAllBytesAsync(path, bytes);

                return "/images/products/" + fileName;
            }
            catch (Exception ex)
            {
                // Hatayı yutmak yerine en azından konsola basalım
                Console.WriteLine($"Resim İndirme Hatası: {ex.Message} - URL: {url}");
                return null;
            }
        }

        private async Task<Category> GetOrCreateCategory(string name, int parentId, string url)
        {
            name = (name ?? "").Trim();
            url = NormalizeUrl(url);

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
            else
            {
                if (!string.Equals(cat.ImportUrl, url, StringComparison.OrdinalIgnoreCase))
                {
                    cat.ImportUrl = url;
                    await _context.SaveChangesAsync();
                }
            }

            return cat;
        }

        private string GenerateSlug(string text)
        {
            text = (text ?? "").ToLowerInvariant()
                .Replace("ş", "s").Replace("ı", "i").Replace("ğ", "g")
                .Replace("ç", "c").Replace("ö", "o").Replace("ü", "u");

            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
            text = Regex.Replace(text, @"\s+", "-").Trim('-');
            return text;
        }

        private async Task Log(int runId, string level, string url, string title, string message)
        {
            _context.ImportLogs.Add(new ImportLog
            {
                RunId = runId,
                At = DateTime.Now,
                Level = level,
                Url = url,
                Title = title,
                Message = message
            });

            await _context.SaveChangesAsync();
        }
    }
}