using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models;

namespace Procopy.Controllers
{
    public class ReklamController : Controller
    {
        private readonly ProcopyContext _context;

        public ReklamController(ProcopyContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string category, int page = 1)
        {
            const int pageSize = 24;

            var anaKategori = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == "reklam" && c.IsActive == true);

            var tumKategoriler = await _context.Categories
                .Where(c => c.IsActive == true)
                .AsNoTracking()
                .ToListAsync();

            var catIds = new HashSet<int>();
            if (anaKategori != null)
            {
                void ToplaIds(int parentId)
                {
                    catIds.Add(parentId);
                    foreach (var alt in tumKategoriler.Where(c => c.ParentId == parentId))
                        ToplaIds(alt.CategoryId);
                }
                ToplaIds(anaKategori.CategoryId);
            }

            var altKategoriler = anaKategori != null
                ? tumKategoriler.Where(c => c.ParentId == anaKategori.CategoryId).OrderBy(c => c.DisplayOrder).ToList()
                : new List<Category>();

            ViewBag.AltKategoriler = altKategoriler;
            ViewBag.CurrentSlug = category;

            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive == true)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(category))
            {
                var secilenKat = tumKategoriler.FirstOrDefault(c => c.Slug == category);
                if (secilenKat != null)
                {
                    var seciliIds = new HashSet<int>();
                    void ToplaSecili(int pid)
                    {
                        seciliIds.Add(pid);
                        foreach (var c in tumKategoriler.Where(c => c.ParentId == pid))
                            ToplaSecili(c.CategoryId);
                    }
                    ToplaSecili(secilenKat.CategoryId);
                    query = query.Where(p => seciliIds.Contains(p.CategoryId));
                    ViewBag.PageTitle = secilenKat.CategoryName;
                }
                else
                {
                    query = catIds.Any() ? query.Where(p => catIds.Contains(p.CategoryId)) : query;
                    ViewBag.PageTitle = "Reklam Çözümleri";
                }
            }
            else
            {
                query = catIds.Any() ? query.Where(p => catIds.Contains(p.CategoryId)) : query;
                ViewBag.PageTitle = "Reklam Çözümleri";
            }

            var toplamUrun = await query.CountAsync();
            var products = await query
                .OrderBy(p => p.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)toplamUrun / pageSize);
            ViewBag.TotalCount = toplamUrun;
            ViewBag.Category = category;

            return View(products);
        }
    }
}
