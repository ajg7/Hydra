using Hydra.Models;

namespace Hydra.Services.Interfaces;
public interface IScraperService
{
    Task ScrapeAsync(ScrapeJob job, IProgress<ScrapeResult> progress, CancellationToken cancellationToken);
}
