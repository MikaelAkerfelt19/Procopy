using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Models;

namespace Procopy.Controllers
{
    public class MatbaaController : Controller
    {
        private readonly ProcopyContext _context;

        public MatbaaController(ProcopyContext context)
        {
            _context = context;
        }

        // GET: /matbaa
        public async Task<IActionResult> Index()
        {
            var types = await _context.PrintProductTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.GroupName)
                .ThenBy(t => t.DisplayOrder)
                .AsNoTracking()
                .ToListAsync();

            return View(types);
        }

        // GET: /matbaa/hesapla/{slug}
        public async Task<IActionResult> Hesapla(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return NotFound();

            var productType = await _context.PrintProductTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);

            if (productType == null) return NotFound();

            var parameters = await _context.PrintParameters
                .Include(p => p.Values.Where(v => v.IsActive))
                .Where(p => p.ProductTypeId == productType.ProductTypeId && p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .AsNoTracking()
                .ToListAsync();

            var sizes = await _context.PrintSizes
                .Where(s => s.ProductTypeId == productType.ProductTypeId && s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .AsNoTracking()
                .ToListAsync();

            var quantities = await _context.PrintQuantities
                .Where(q => q.ProductTypeId == productType.ProductTypeId && q.IsActive)
                .OrderBy(q => q.DisplayOrder)
                .AsNoTracking()
                .ToListAsync();

            var vm = new PrintCalculatorViewModel
            {
                ProductType = productType,
                Parameters = parameters,
                Sizes = sizes,
                Quantities = quantities
            };

            return View(vm);
        }
    }
}
