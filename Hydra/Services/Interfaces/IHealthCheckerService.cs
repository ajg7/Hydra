using Hydra.Models;

namespace Hydra.Services.Interfaces;
public interface IHealthCheckerService
{
    Task<HealthCheckResult> CheckAsync(string url, CancellationToken cancellationToken);
    Task<List<HealthCheckResult>> CheckManyAsync(List<string> urls, int maxConcurrency, IProgress<HealthCheckResult> progress, CancellationToken cancellationToken);
}
