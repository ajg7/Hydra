namespace Hydra.Models;
public class HealthCheckResult
{
    public required string Url { get; set; }
    public int? StatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public bool IsUp { get; set; }
    public string? Error { get; set; }
}
