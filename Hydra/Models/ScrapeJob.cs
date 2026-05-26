namespace Hydra.Models;
public class ScrapeJob
{
    public required string SeedUrl { get; set; }
    public int MaxDepth { get; set; }
    public int MaxPages { get; set; }
}
