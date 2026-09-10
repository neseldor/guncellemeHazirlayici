# Güncelleme Hazırlayıcı

Seçilen klasörde, belirtilen tarihten itibaren değiştirilmiş dosyaları alt klasör
yapısını koruyarak ZIP arşivine dönüştüren .NET 10 WPF uygulamasıdır.

## Çalıştırma

```powershell
dotnet run --project .\src\GuncellemeHazirlayici\GuncellemeHazirlayici.csproj
```

## Testler

```powershell
dotnet test .\GuncellemeHazirlayici.slnx
```

Ayarlar `%LocalAppData%\GuncellemeHazirlayici\settings.json` dosyasında saklanır.

