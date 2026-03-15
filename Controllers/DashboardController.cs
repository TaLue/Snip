using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Snip.Data;

namespace Snip.Controllers;

public class DashboardController(SnipDbContext db) : Controller
{
    private bool IsAuthed => HttpContext.Session.GetString("auth") == "1";

    [HttpGet("/dashboard")]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthed) return RedirectToAction("Login", "Home");

        var links = await db.ShortLinks
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.Slug,
                s.OriginalUrl,
                s.Label,
                s.CreatedAt,
                s.IsActive,
                TotalClicks = s.Clicks.Count
            })
            .ToListAsync();

        return View(links);
    }

    [HttpGet("/dashboard/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        if (!IsAuthed) return RedirectToAction("Login", "Home");

        var link = await db.ShortLinks
            .Include(s => s.Clicks)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (link is null) return NotFound();
        return View(link);
    }

    [HttpPost("/dashboard/{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        if (!IsAuthed) return Unauthorized();

        var link = await db.ShortLinks.FindAsync(id);
        if (link is null) return NotFound();

        link.IsActive = !link.IsActive;
        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}
