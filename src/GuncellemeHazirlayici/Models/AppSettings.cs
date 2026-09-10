namespace GuncellemeHazirlayici.Models;

public sealed class AppSettings
{
    public DateTime SelectedDate { get; set; } = DateTime.Today;

    public string SourceFolder { get; set; } = string.Empty;

    public string TargetFolder { get; set; } = string.Empty;

    public static AppSettings CreateDefault() => new();
}

