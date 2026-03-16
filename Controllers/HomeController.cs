using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Snip.Data;
using Snip.Models;

namespace Snip.Controllers;

public class HomeController(SnipDbContext db, IConfiguration config) : Controller
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9\-_]{1,30}$", RegexOptions.Compiled);

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string originalUrl, string? label, string? customSlug)
    {
        if (string.IsNullOrWhiteSpace(originalUrl) ||
            !Uri.TryCreate(originalUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ModelState.AddModelError("", "Please enter a valid URL (http/https)");
            return View("Index");
        }

        var slug = string.IsNullOrWhiteSpace(customSlug)
            ? GenerateSlug()
            : customSlug.Trim().ToLower();

        if (!SlugRegex.IsMatch(slug))
        {
            ModelState.AddModelError("", "Slug can only contain lowercase letters, numbers, dashes and underscores (max 30 chars)");
            return View("Index");
        }

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
    [ValidateAntiForgeryToken]
    public IActionResult Login(string password)
    {
        var adminPassword = config["Snip:AdminPassword"];
        if (password == adminPassword)
        {
            HttpContext.Session.SetString("auth", "1");
            return RedirectToAction("Index", "Dashboard");
        }
        ModelState.AddModelError("", "Wrong password");
        return View();
    }

    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
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

        return Redirect(link.OriginalUrl);
    }

    private static string GenerateSlug()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[6];
        for (var i = 0; i < result.Length; i++)
            result[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(result);
    }
}
