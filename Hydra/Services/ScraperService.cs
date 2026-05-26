using Hydra.Models;
using Hydra.Services.Interfaces;

namespace Hydra.Services;
public class ScraperService : IScraperService
{
    public async Task ScrapeAsync(ScrapeJob job, IProgress<ScrapeResult> progress, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
