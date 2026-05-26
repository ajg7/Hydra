namespace Hydra.Models;
public class ScrapeResult
{
    public required string Url { get; set; }
    public List<string> Links { get; set; } = [];
    public string? Title { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
