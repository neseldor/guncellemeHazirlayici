using System.IO.Compression;
using GuncellemeHazirlayici.Models;

namespace GuncellemeHazirlayici.Services;

public sealed class ArchiveService : IArchiveService
{
    private static readonly HashSet<string> ArchiveExtensions = new(
        [".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".tbz2", ".xz", ".txz", ".cab"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ExcludedDirectoryNames = new(
        ["temp", "pdf", "excell", "indir"],
        StringComparer.OrdinalIgnoreCase);

    private readonly TimeProvider _timeProvider;

    public ArchiveService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<ArchiveResult> CreateArchiveAsync(
        ArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(
            () => CreateArchive(request, cancellationToken),
            cancellationToken);
    }

    private ArchiveResult CreateArchive(
        ArchiveRequest request,
        CancellationToken cancellationToken)
    {
        var sourceFolder = Path.GetFullPath(request.SourceFolder);
        var targetFolder = Path.GetFullPath(request.TargetFolder);

        if (!Directory.Exists(sourceFolder))
        {
            throw new DirectoryNotFoundException($"Kaynak klasör bulunamadı: {sourceFolder}");
        }

        if (!Directory.Exists(targetFolder))
        {
            throw new DirectoryNotFoundException($"Hedef klasör bulunamadı: {targetFolder}");
        }

        var files = FindMatchingFiles(
            sourceFolder,
            request.ModifiedSince.Date,
            request.IsInstallationArchive,
            cancellationToken);

        if (files.Count == 0)
        {
            return ArchiveResult.NoFiles;
        }

        var archiveName = BuildArchiveName(sourceFolder);
        var timestamp = _timeProvider.GetLocalNow().DateTime;
        var baseFileName = $"{archiveName}_{timestamp:ddMMyyyy_HHmmss}";
        var temporaryPath = Path.Combine(
            targetFolder,
            $".guncelleme-hazirlayici-{Guid.NewGuid():N}.tmp");

        try
        {
            WriteArchive(temporaryPath, files, cancellationToken);
            var finalPath = MoveToUniqueDestination(
                temporaryPath,
                targetFolder,
                baseFileName,
                cancellationToken);

            return new ArchiveResult(true, finalPath, files.Count);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static List<ArchiveFile> FindMatchingFiles(
        string sourceFolder,
        DateTime modifiedSince,
        bool isInstallationArchive,
        CancellationToken cancellationToken)
    {
        var results = new List<ArchiveFile>();
        var pendingFolders = new Stack<string>();
        pendingFolders.Push(sourceFolder);

        while (pendingFolders.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentFolder = pendingFolders.Pop();

            foreach (var entryPath in Directory.EnumerateFileSystemEntries(currentFolder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entryPath);

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (!attributes.HasFlag(FileAttributes.ReparsePoint)
                        && (isInstallationArchive || !ExcludedDirectoryNames.Contains(Path.GetFileName(entryPath))))
                    {
                        pendingFolders.Push(entryPath);
                    }

                    continue;
                }

                var extension = Path.GetExtension(entryPath);
                if (isInstallationArchive
                    ? currentFolder == sourceFolder && ArchiveExtensions.Contains(extension)
                    : extension.Equals(".config", StringComparison.OrdinalIgnoreCase)
                        || Path.GetFileName(entryPath).Equals("deepzoom.aspx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lastWriteTime = File.GetLastWriteTime(entryPath);
                if (!isInstallationArchive && lastWriteTime < modifiedSince)
                {
                    continue;
                }

                results.Add(new ArchiveFile(
                    entryPath,
                    Path.GetRelativePath(sourceFolder, entryPath),
                    lastWriteTime));
            }
        }

        results.Sort(static (left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));

        return results;
    }

    private static void WriteArchive(
        string temporaryPath,
        IReadOnlyCollection<ArchiveFile> files,
        CancellationToken cancellationToken)
    {
        using var outputStream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None);
        using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryName = file.RelativePath.Replace(Path.DirectorySeparatorChar, '/');
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

            if (file.LastWriteTime.Year is >= 1980 and <= 2107)
            {
                entry.LastWriteTime = new DateTimeOffset(file.LastWriteTime);
            }

            using var inputStream = new FileStream(
                file.FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);
            using var entryStream = entry.Open();
            inputStream.CopyTo(entryStream);
        }
    }

    private static string MoveToUniqueDestination(
        string temporaryPath,
        string targetFolder,
        string baseFileName,
        CancellationToken cancellationToken)
    {
        for (var suffix = 1; ; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = suffix == 1
                ? $"{baseFileName}.zip"
                : $"{baseFileName}_{suffix}.zip";
            var destinationPath = Path.Combine(targetFolder, fileName);

            if (File.Exists(destinationPath))
            {
                continue;
            }

            try
            {
                File.Move(temporaryPath, destinationPath, overwrite: false);
                return destinationPath;
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
                // Başka bir işlem aynı adı oluşturduysa sıradaki adı dene.
            }
        }
    }

    private static string BuildArchiveName(string sourceFolder)
    {
        var directoryName = new DirectoryInfo(sourceFolder).Name;

        if (string.IsNullOrWhiteSpace(directoryName))
        {
            var rootName = Path.GetPathRoot(sourceFolder)?
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .TrimEnd(':');
            directoryName = string.IsNullOrWhiteSpace(rootName)
                ? "Arsiv"
                : $"{rootName}_Drive";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(directoryName
            .Select(character =>
                invalidCharacters.Contains(character) || char.IsControl(character)
                    ? '_'
                    : character)
            .ToArray())
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrWhiteSpace(safeName) ? "Arsiv" : safeName;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Asıl arşivleme hatasının korunması için temizlik hatası yutulur.
        }
    }

    private sealed record ArchiveFile(
        string FullPath,
        string RelativePath,
        DateTime LastWriteTime);
}
