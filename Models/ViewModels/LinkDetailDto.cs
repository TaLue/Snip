namespace Snip.Models.ViewModels;

public record ClicksByDayDto(string Date, int Count);
public record ReferrerDto(string Source, int Count);

public class LinkDetailDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public int TotalClicks { get; set; }
    public List<ClicksByDayDto> ClicksByDay { get; set; } = [];
    public List<ReferrerDto> TopReferrers { get; set; } = [];
}
