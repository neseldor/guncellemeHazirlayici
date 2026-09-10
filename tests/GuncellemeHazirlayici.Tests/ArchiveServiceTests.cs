using System.IO.Compression;
using GuncellemeHazirlayici.Models;
using GuncellemeHazirlayici.Services;

namespace GuncellemeHazirlayici.Tests;

public sealed class ArchiveServiceTests
{
    private static readonly DateTimeOffset ArchiveTime =
        new(2026, 9, 4, 15, 30, 45, TimeSpan.Zero);

    [Fact]
    public async Task InstallationIncludesOldAndConfigurationFilesButSkipsOnlyRootArchives()
    {
        using var testDirectory = new TestDirectory();
        var source = testDirectory.CreateDirectory("Kaynak");
        string[] included = ["old.txt", "web.CONFIG", "DeepZoom.aspx", "Alt/app.config",
            "Alt/deepzoom.aspx", "Alt/backup.zip", "temp/file.txt", "pdf/file.txt",
            "Alt/excell/file.txt", "indir/file.txt"];
        foreach (var name in included.Concat(["backup.ZIP", "backup.RaR", "backup.7z", "backup.tar.gz"]))
        {
            var path = testDirectory.CreateFile("Kaynak/" + name, name);
            File.SetLastWriteTime(path, new DateTime(2020, 1, 1));
        }

        var service = new ArchiveService(new FixedTimeProvider(ArchiveTime));
        var result = await service.CreateArchiveAsync(
            new ArchiveRequest(source, source, new DateTime(2030, 1, 1), true));

        Assert.True(result.Created);
        Assert.Equal(included.Length, result.FileCount);
        using var archive = ZipFile.OpenRead(result.ArchivePath!);
        Assert.Equal(included.OrderBy(name => name), archive.Entries.Select(entry => entry.FullName).OrderBy(name => name));
    }

    [Fact]
    public async Task UpdateExcludesConfigurationAndDeepZoomAtEveryDepth()
    {
        using var testDirectory = new TestDirectory();
        var source = testDirectory.CreateDirectory("Kaynak");
        var target = testDirectory.CreateDirectory("Hedef");
        foreach (var name in new[] { "web.config", "DeepZoom.ASPX", "Alt/app.CONFIG", "Alt/deepzoom.aspx",
            "Alt/keep.aspx", "web.config.bak", "backup.zip" })
        {
            testDirectory.CreateFile("Kaynak/" + name, name);
        }

        var result = await new ArchiveService().CreateArchiveAsync(
            new ArchiveRequest(source, target, DateTime.Today.AddDays(-1)));

        Assert.Equal(3, result.FileCount);
        using var archive = ZipFile.OpenRead(result.ArchivePath!);
        Assert.Equal(new[] { "Alt/keep.aspx", "backup.zip", "web.config.bak" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [Fact]
    public async Task IncludesBoundaryAndNewerFilesWhileKeepingRelativeFolders()
    {
        using var testDirectory = new TestDirectory();
        var source = testDirectory.CreateDirectory("Kaynak");
        var target = testDirectory.CreateDirectory("Hedef");
        var oldFile = testDirectory.CreateFile(@"Kaynak\eski.txt", "eski");
        var boundaryFile = testDirectory.CreateFile(@"Kaynak\sinir.txt", "sinir");
        var nestedFile = testDirectory.CreateFile(@"Kaynak\Alt\yeni.txt", "yeni");
        var threshold = new DateTime(2026, 9, 1, 0, 0, 0);
        File.SetLastWriteTime(oldFile, threshold.AddSeconds(-1));
        File.SetLastWriteTime(boundaryFile, threshold);
        File.SetLastWriteTime(nestedFile, threshold.AddHours(2));
        var service = new ArchiveService(new FixedTimeProvider(ArchiveTime));

        var result = await service.CreateArchiveAsync(
            new ArchiveRequest(source, target, threshold));

        Assert.True(result.Created);
        Assert.Equal(2, result.FileCount);
        Assert.Equal("Kaynak_04092026_153045.zip", System.IO.Path.GetFileName(result.ArchivePath));

        using var archive = ZipFile.OpenRead(result.ArchivePath!);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();
        Assert.Contains("sinir.txt", entries);
        Assert.Contains("Alt/yeni.txt", entries);
        Assert.DoesNotContain("eski.txt", entries);
    }

    [Fact]
    public async Task SkipsExcludedDirectoryNamesAtEveryDepthIgnoringCase()
    {
        using var testDirectory = new TestDirectory();
        var source = testDirectory.CreateDirectory("Kaynak");
        var target = testDirectory.CreateDirectory("Hedef");
        testDirectory.CreateFile(@"Kaynak\temp\gecici.txt", "gecici");
        testDirectory.CreateFile(@"Kaynak\PDF\belge.pdf", "belge");
        testDirectory.CreateFile(@"Kaynak\Alt\ExCeLl\tablo.xlsx", "tablo");
        testDirectory.CreateFile(@"Kaynak\Alt\indir\paket.zip", "paket");
        testDirectory.CreateFile(@"Kaynak\temporary\korunacak.txt", "icerik");
        testDirectory.CreateFile(@"Kaynak\Alt\uygulama.dll", "icerik");
        var service = new ArchiveService(new FixedTimeProvider(ArchiveTime));

        var result = await service.CreateArchiveAsync(
            new ArchiveRequest(source, target, DateTime.Today.AddYears(-10)));

        Assert.True(result.Created);
        Assert.Equal(2, result.FileCount);
        using var archive = ZipFile.OpenRead(result.ArchivePath!);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();
        Assert.Contains("temporary/korunacak.txt", entries);
        Assert.Contains("Alt/uygulama.dll", entries);
        Assert.DoesNotContain(entries, entry =>
            entry.StartsWith("temp/", StringComparison.OrdinalIgnoreCase)
            || entry.StartsWith("PDF/", StringComparison.OrdinalIgnoreCase)
            || entry.Contains("/excell/", StringComparison.OrdinalIgnoreCase)
            || entry.Contains("/indir/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReturnsNoFilesAndDoesNotCreateArchiveWhenNothingMatches()
    {
        using var testDirectory = new TestDirectory();
        var source = testDirectory.CreateDirectory("Kaynak");
        var target = testDirectory.CreateDirectory("Hedef");
        var file = testDirectory.CreateFile(@"Kaynak\eski.txt", "eski");
        File.SetLastWriteTime(file, new DateTime(2025, 1, 1));
        var service = new ArchiveService(new FixedTimeProvider(ArchiveTime));

        var result = await service.CreateArchiveAsync(
            new ArchiveRequest(source, target, new DateTime(2026, 1, 1)));

        Assert.False(result.Created);
        Assert.Null(result.ArchivePath);
        Assert.Empty(Directory.EnumerateFiles(target));
    }

    [Fact]
    public async Task AddsNumericSuffixWhenArchiveNameAlreadyExists()
    {
        using var testDirectory = new TestDirectory();
        var source = testDirectory.CreateDirectory("Kaynak");
        var target = testDirectory.CreateDirectory("Hedef");
        testDirectory.CreateFile(@"Kaynak\dosya.txt", "icerik");
        var service = new ArchiveService(new FixedTimeProvider(ArchiveTime));
        var request = new ArchiveRequest(source, target, DateTime.Today.AddYears(-10));

        var firstResult = await service.CreateArchiveAsync(request);
        var secondResult = await service.CreateArchiveAsync(request);

        Assert.EndsWith("Kaynak_04092026_153045.zip", firstResult.ArchivePath);
        Assert.EndsWith("Kaynak_04092026_153045_2.zip", secondResult.ArchivePath);
        Assert.True(File.Exists(firstResult.ArchivePath));
        Assert.True(File.Exists(secondResult.ArchivePath));
    }

    [Fact]
    public async Task DeletesTemporaryArchiveWhenAFileCannotBeRead()
    {
        using var testDirectory = new TestDirectory();
        var source = testDirectory.CreateDirectory("Kaynak");
        var target = testDirectory.CreateDirectory("Hedef");
        var lockedFile = testDirectory.CreateFile(@"Kaynak\kilitli.txt", "icerik");
        using var lockStream = new FileStream(
            lockedFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
        var service = new ArchiveService(new FixedTimeProvider(ArchiveTime));

        await Assert.ThrowsAsync<IOException>(() => service.CreateArchiveAsync(
            new ArchiveRequest(source, target, DateTime.Today.AddYears(-10))));

        Assert.Empty(Directory.EnumerateFiles(target));
    }
}
