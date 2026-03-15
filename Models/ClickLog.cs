namespace Snip.Models;

public class ClickLog
{
    public int Id { get; set; }
    public int ShortLinkId { get; set; }
    public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
    public string? Referrer { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }

    public ShortLink ShortLink { get; set; } = null!;
}
