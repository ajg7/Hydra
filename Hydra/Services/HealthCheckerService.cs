using Hydra.Models;
using Hydra.Services.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Hydra.Services;
public class HealthCheckerService : IHealthCheckerService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HealthCheckerService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            stopwatch.Stop();

            return new HealthCheckResult()
            {
                Url = url,
                StatusCode = (int?)response.StatusCode,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                IsUp = response.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckResult()
            {
                Url = url,
                Error = ex.Message,
                IsUp = false,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public async Task<List<HealthCheckResult>> CheckManyAsync(List<string> urls, int maxConcurrency, IProgress<HealthCheckResult> progress, CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<HealthCheckResult>();
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = urls.Select(async url =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var result = await CheckAsync(url, cancellationToken);
                results.Add(result);
                progress?.Report(result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results.ToList();
    }
}
