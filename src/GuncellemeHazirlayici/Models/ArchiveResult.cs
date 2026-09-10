namespace GuncellemeHazirlayici.Models;

public sealed record ArchiveResult(bool Created, string? ArchivePath, int FileCount)
{
    public static ArchiveResult NoFiles { get; } = new(false, null, 0);
}

