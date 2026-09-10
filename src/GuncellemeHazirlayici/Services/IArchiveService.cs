using GuncellemeHazirlayici.Models;

namespace GuncellemeHazirlayici.Services;

public interface IArchiveService
{
    Task<ArchiveResult> CreateArchiveAsync(
        ArchiveRequest request,
        CancellationToken cancellationToken = default);
}

