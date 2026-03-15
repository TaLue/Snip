using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Snip.Data;
using Snip.Models;

namespace Snip.Controllers;

public class HomeController(SnipDbContext db, IConfiguration config) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> Create(string originalUrl, string? label, string? customSlug)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            ModelState.AddModelError("", "URL is required");
            return View("Index");
        }

        var slug = string.IsNullOrWhiteSpace(customSlug)
            ? GenerateSlug()
            : customSlug.Trim().ToLower();

        if (await db.ShortLinks.AnyAsync(s => s.Slug == slug))
        {
            ModelState.AddModelError("", "Slug already taken");
            return View("Index");
        }

        var link = new ShortLink
        {
            Slug = slug,
            OriginalUrl = originalUrl.Trim(),
            Label = label?.Trim()
        };

        db.ShortLinks.Add(link);
        await db.SaveChangesAsync();

        TempData["CreatedSlug"] = slug;
        TempData["CreatedUrl"] = $"{Request.Scheme}://{Request.Host}/{slug}";
        return RedirectToAction("Index");
    }

    [HttpGet("/login")]
    public IActionResult Login() => View();

    [HttpPost("/login")]
    public IActionResult Login(string password)
    {
        var adminPassword = config["Snip:AdminPassword"];
        if (password == adminPassword)
        {
            HttpContext.Session.SetString("auth", "1");
            return RedirectToAction("Index", "Dashboard");
        }
        ViewBag.Error = "Wrong password";
        return View();
    }

    [HttpPost("/logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }

    [HttpGet("/{slug}")]
    public async Task<IActionResult> Go(string slug)
    {
        var link = await db.ShortLinks.FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);
        if (link is null) return NotFound();

        var click = new ClickLog
        {
            ShortLinkId = link.Id,
            Referrer = Request.Headers.Referer.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        db.ClickLogs.Add(click);
        await db.SaveChangesAsync();

        return base.Redirect(link.OriginalUrl);
    }

    private static string GenerateSlug()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
