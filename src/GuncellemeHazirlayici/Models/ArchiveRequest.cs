namespace GuncellemeHazirlayici.Models;

public sealed record ArchiveRequest(
    string SourceFolder,
    string TargetFolder,
    DateTime ModifiedSince,
    bool IsInstallationArchive = false);
