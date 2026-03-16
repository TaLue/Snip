using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Snip.Data;
using Snip.Filters;
using Snip.Models.ViewModels;

namespace Snip.Controllers;

[TypeFilter(typeof(RequireAuthFilter))]
public class DashboardController(SnipDbContext db) : Controller
{
    [HttpGet("/dashboard")]
    public async Task<IActionResult> Index()
    {
        var links = await db.ShortLinks
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new LinkSummaryDto
            {
                Id = s.Id,
                Slug = s.Slug,
                OriginalUrl = s.OriginalUrl,
                Label = s.Label,
                CreatedAt = s.CreatedAt,
                IsActive = s.IsActive,
                TotalClicks = s.Clicks.Count
            })
            .ToListAsync();

        return View(links);
    }

    [HttpGet("/dashboard/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var link = await db.ShortLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (link is null) return NotFound();

        var clicks = await db.ClickLogs
            .Where(c => c.ShortLinkId == id)
            .Select(c => new { c.ClickedAt, c.Referrer })
            .ToListAsync();

        var vm = new LinkDetailDto
        {
            Id = link.Id,
            Slug = link.Slug,
            OriginalUrl = link.OriginalUrl,
            Label = link.Label,
            CreatedAt = link.CreatedAt,
            IsActive = link.IsActive,
            TotalClicks = clicks.Count,
            ClicksByDay = clicks
                .GroupBy(c => c.ClickedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new ClicksByDayDto(g.Key.ToString("MMM d"), g.Count()))
                .ToList(),
            TopReferrers = clicks
                .GroupBy(c => string.IsNullOrEmpty(c.Referrer) ? "Direct" : c.Referrer)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new ReferrerDto(g.Key, g.Count()))
                .ToList()
        };

        return View(vm);
    }

    [HttpPost("/dashboard/{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var link = await db.ShortLinks.FindAsync(id);
        if (link is null) return NotFound();

        link.IsActive = !link.IsActive;
        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}
