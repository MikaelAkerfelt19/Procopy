using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procopy.Jobs;
using Procopy.Models;
using System.Net;
using System.Text;

namespace Procopy.Controllers
{
    public class DataImportController : Controller
    {
        private readonly ProcopyContext _context;
        private readonly IBackgroundJobClient _jobs;

        public DataImportController(ProcopyContext context, IBackgroundJobClient jobs)
        {
            _context = context;
            _jobs = jobs;
        }

        public async Task<IActionResult> StartTreeImport(int mainCategoryId)
        {
            var mainCategory = await _context.Categories.FindAsync(mainCategoryId);
            if (mainCategory == null || string.IsNullOrWhiteSpace(mainCategory.ImportUrl))
                return Content("Ana kategori bulunamadı veya ImportUrl boş.");

            var run = new ImportRun
            {
                MainCategoryId = mainCategoryId,
                StartUrl = mainCategory.ImportUrl.Trim(),
                StartAt = DateTime.Now,
                Status = "queued",
                PagesVisited = 0,
                ProductsAdded = 0,
                ProductsSkipped = 0,
                Errors = 0
            };

            _context.ImportRuns.Add(run);
            await _context.SaveChangesAsync();

            _jobs.Enqueue<TreeImportJob>(j => j.Run(run.RunId));

            var html = $@"
<div style='font-family:Segoe UI, sans-serif; padding:20px; color:#333;'>
  <h3 style='color:#0d6efd;'>İş kuyruğa alındı</h3>
  <div style='background:#f4f4f4; padding:12px; border:1px solid #ddd;'>
    <div><b>RunId:</b> {run.RunId}</div>
    <div><b>Url:</b> {WebUtility.HtmlEncode(run.StartUrl)}</div>
    <div><b>Durum:</b> queued</div>
  </div>
  <br>
  <a href='/DataImport/Run/{run.RunId}' style='display:inline-block;padding:10px 14px;background:#198754;color:#fff;text-decoration:none;border-radius:6px;'>Logları Gör</a>
  <a href='/DataImport/Runs' style='display:inline-block;margin-left:8px;padding:10px 14px;background:#6c757d;color:#fff;text-decoration:none;border-radius:6px;'>Tüm Çalışmalar</a>
  <a href='/hangfire' style='display:inline-block;margin-left:8px;padding:10px 14px;background:#0d6efd;color:#fff;text-decoration:none;border-radius:6px;'>Hangfire</a>
</div>";
            return Content(html, "text/html");
        }

        public async Task<IActionResult> Runs()
        {
            var runs = await _context.ImportRuns
                .OrderByDescending(x => x.RunId)
                .Take(200)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.Append("<div style='font-family:Segoe UI, sans-serif; padding:20px; color:#333;'>");
            sb.Append("<h3>Import Çalışmaları</h3>");
            sb.Append("<div style='margin-bottom:12px;'><a href='/hangfire'>Hangfire</a></div>");
            sb.Append("<table style='width:100%;border-collapse:collapse;font-size:14px;'>");
            sb.Append("<tr style='background:#f4f4f4;'>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>RunId</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>Status</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>StartAt</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>EndAt</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>Pages</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>Added</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>Skipped</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>Errors</th>");
            sb.Append("</tr>");

            foreach (var r in runs)
            {
                sb.Append("<tr>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'><a href='/DataImport/Run/{r.RunId}'>{r.RunId}</a></td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'>{WebUtility.HtmlEncode(r.Status ?? "")}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'>{r.StartAt:yyyy-MM-dd HH:mm:ss}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'>{(r.EndAt.HasValue ? r.EndAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "")}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'>{r.PagesVisited}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'>{r.ProductsAdded}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'>{r.ProductsSkipped}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'>{r.Errors}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</table></div>");
            return Content(sb.ToString(), "text/html");
        }

        [Route("DataImport/Run/{runId:int}")]
        public async Task<IActionResult> Run(int runId, int page = 1, int pageSize = 200)
        {
            if (page < 1) page = 1;
            if (pageSize < 50) pageSize = 50;
            if (pageSize > 500) pageSize = 500;

            var run = await _context.ImportRuns.FirstOrDefaultAsync(x => x.RunId == runId);
            if (run == null) return Content("Run bulunamadı.");

            var total = await _context.ImportLogs.CountAsync(x => x.RunId == runId);

            var logs = await _context.ImportLogs
                .Where(x => x.RunId == runId)
                .OrderByDescending(x => x.LogId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            int totalPages = (int)Math.Ceiling(total / (double)pageSize);

            var sb = new StringBuilder();
            sb.Append("<div style='font-family:Segoe UI, sans-serif; padding:20px; color:#333;'>");
            sb.Append($"<h3>Run {run.RunId} Logları</h3>");
            sb.Append("<div style='background:#f4f4f4; padding:12px; border:1px solid #ddd;margin-bottom:12px;'>");
            sb.Append($"<div><b>Status:</b> {WebUtility.HtmlEncode(run.Status ?? "")}</div>");
            sb.Append($"<div><b>Start:</b> {run.StartAt:yyyy-MM-dd HH:mm:ss}</div>");
            sb.Append($"<div><b>End:</b> {(run.EndAt.HasValue ? run.EndAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "")}</div>");
            sb.Append($"<div><b>Pages:</b> {run.PagesVisited} | <b>Added:</b> {run.ProductsAdded} | <b>Skipped:</b> {run.ProductsSkipped} | <b>Errors:</b> {run.Errors}</div>");
            sb.Append($"<div><b>Url:</b> {WebUtility.HtmlEncode(run.StartUrl ?? "")}</div>");
            sb.Append("</div>");

            sb.Append("<div style='margin-bottom:10px;'>");
            sb.Append($"<a href='/DataImport/Runs'>Tüm Çalışmalar</a> | <a href='/hangfire'>Hangfire</a>");
            sb.Append("</div>");

            sb.Append("<div style='margin-bottom:10px;'>");
            if (page > 1) sb.Append($"<a href='/DataImport/Run/{runId}?page={page - 1}&pageSize={pageSize}'>Önceki</a> ");
            sb.Append($"<span> Sayfa {page}/{(totalPages == 0 ? 1 : totalPages)} (Toplam {total}) </span>");
            if (page < totalPages) sb.Append($" <a href='/DataImport/Run/{runId}?page={page + 1}&pageSize={pageSize}'>Sonraki</a>");
            sb.Append("</div>");

            sb.Append("<table style='width:100%;border-collapse:collapse;font-size:13px;'>");
            sb.Append("<tr style='background:#f4f4f4;'>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>At</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>Level</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>Title</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>Url</th>");
            sb.Append("<th style='border:1px solid #ddd;padding:8px;text-align:left;'>Message</th>");
            sb.Append("</tr>");

            foreach (var l in logs)
            {
                sb.Append("<tr>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;white-space:nowrap;'>{l.At:yyyy-MM-dd HH:mm:ss}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;white-space:nowrap;'>{WebUtility.HtmlEncode(l.Level ?? "")}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'>{WebUtility.HtmlEncode(l.Title ?? "")}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;max-width:360px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;'>{WebUtility.HtmlEncode(l.Url ?? "")}</td>");
                sb.Append($"<td style='border:1px solid #ddd;padding:8px;'>{WebUtility.HtmlEncode(l.Message ?? "")}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</table></div>");
            return Content(sb.ToString(), "text/html");
        }
    }
}