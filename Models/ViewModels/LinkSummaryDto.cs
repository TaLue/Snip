namespace Snip.Models.ViewModels;

public class LinkSummaryDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public int TotalClicks { get; set; }
}
