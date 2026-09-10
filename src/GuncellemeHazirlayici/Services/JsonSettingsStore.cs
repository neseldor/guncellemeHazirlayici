using System.Text;
using System.Text.Json;
using GuncellemeHazirlayici.Models;

namespace GuncellemeHazirlayici.Services;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public JsonSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GuncellemeHazirlayici",
            "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);

            if (settings is null)
            {
                return AppSettings.CreateDefault();
            }

            settings.SelectedDate = settings.SelectedDate == default
                ? DateTime.Today
                : settings.SelectedDate.Date;
            settings.SourceFolder ??= string.Empty;
            settings.TargetFolder ??= string.Empty;
            return settings;
        }
        catch (JsonException)
        {
            return AppSettings.CreateDefault();
        }
        catch (IOException)
        {
            return AppSettings.CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var settingsFolder = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("Ayar klasörü belirlenemedi.");
        Directory.CreateDirectory(settingsFolder);

        var temporaryPath = Path.Combine(
            settingsFolder,
            $"settings-{Guid.NewGuid():N}.tmp");

        try
        {
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // Asıl ayar kaydetme hatasını koru.
            }

            throw;
        }
    }
}
