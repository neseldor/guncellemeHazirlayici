using GuncellemeHazirlayici.Models;
using GuncellemeHazirlayici.Services;

namespace GuncellemeHazirlayici.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public void SavesAndLoadsAllValues()
    {
        using var testDirectory = new TestDirectory();
        var settingsPath = System.IO.Path.Combine(testDirectory.Path, "Ayarlar", "settings.json");
        var store = new JsonSettingsStore(settingsPath);
        var expected = new AppSettings
        {
            SelectedDate = new DateTime(2026, 9, 4),
            SourceFolder = @"C:\Kaynak",
            TargetFolder = @"D:\Hedef"
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected.SelectedDate, actual.SelectedDate);
        Assert.Equal(expected.SourceFolder, actual.SourceFolder);
        Assert.Equal(expected.TargetFolder, actual.TargetFolder);
    }

    [Fact]
    public void UsesDefaultsWhenSettingsJsonIsInvalid()
    {
        using var testDirectory = new TestDirectory();
        var settingsPath = testDirectory.CreateFile("settings.json", "geçersiz json");
        var store = new JsonSettingsStore(settingsPath);

        var settings = store.Load();

        Assert.Equal(DateTime.Today, settings.SelectedDate);
        Assert.Empty(settings.SourceFolder);
        Assert.Empty(settings.TargetFolder);
    }
}
