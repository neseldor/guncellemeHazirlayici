using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GuncellemeHazirlayici.Models;
using GuncellemeHazirlayici.Services;
using Microsoft.Win32;

namespace GuncellemeHazirlayici;

public partial class MainWindow : Window
{
    private static readonly Brush InformationBackground = CreateBrush("#EFF6FF");
    private static readonly Brush InformationBorder = CreateBrush("#BFDBFE");
    private static readonly Brush InformationText = CreateBrush("#1E3A8A");
    private static readonly Brush SuccessBackground = CreateBrush("#ECFDF3");
    private static readonly Brush SuccessBorder = CreateBrush("#ABEFC6");
    private static readonly Brush SuccessText = CreateBrush("#067647");
    private static readonly Brush WarningBackground = CreateBrush("#FFFAEB");
    private static readonly Brush WarningBorder = CreateBrush("#FEDF89");
    private static readonly Brush WarningText = CreateBrush("#B54708");
    private static readonly Brush ErrorBackground = CreateBrush("#FEF3F2");
    private static readonly Brush ErrorBorder = CreateBrush("#FECDCA");
    private static readonly Brush ErrorText = CreateBrush("#B42318");

    private readonly IArchiveService _archiveService;
    private readonly JsonSettingsStore _settingsStore;
    private bool _isInitializing = true;
    private bool _isBusy;
    private bool _isCompactLayout;

    public MainWindow()
        : this(new ArchiveService(), new JsonSettingsStore())
    {
    }

    internal MainWindow(IArchiveService archiveService, JsonSettingsStore settingsStore)
    {
        _archiveService = archiveService;
        _settingsStore = settingsStore;

        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SizeChanged += MainWindow_SizeChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = _settingsStore.Load();
        SourceFolderTextBox.Text = settings.SourceFolder;
        TargetFolderTextBox.Text = settings.TargetFolder;
        SelectedDatePicker.SelectedDate = settings.SelectedDate.Date;
        _isInitializing = false;

        ApplyResponsiveLayout(ActualWidth);
        RefreshReadyState(showStatus: true);
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double windowWidth)
    {
        var useCompactLayout = windowWidth < 700;
        if (_isCompactLayout == useCompactLayout)
        {
            return;
        }

        _isCompactLayout = useCompactLayout;

        if (useCompactLayout)
        {
            HeaderBorder.Padding = new Thickness(20, 18, 20, 18);
            FormCard.Margin = new Thickness(14);
            FormCard.Padding = new Thickness(18);

            DateColumn.Width = new GridLength(1, GridUnitType.Star);
            DetailsSpacerColumn.Width = new GridLength(0);
            StatusColumn.Width = new GridLength(0);
            DetailsGapRow.Height = new GridLength(14);

            Grid.SetRow(DateSection, 0);
            Grid.SetColumn(DateSection, 0);
            Grid.SetColumnSpan(DateSection, 3);
            Grid.SetRow(StatusBorder, 2);
            Grid.SetColumn(StatusBorder, 0);
            Grid.SetColumnSpan(StatusBorder, 3);

            FooterGapRow.Height = new GridLength(14);
            Grid.SetRow(FooterDescriptionTextBlock, 0);
            Grid.SetColumn(FooterDescriptionTextBlock, 0);
            Grid.SetColumnSpan(FooterDescriptionTextBlock, 2);
            Grid.SetRow(CreateArchiveButton, 2);
            Grid.SetColumn(CreateArchiveButton, 0);
            Grid.SetColumnSpan(CreateArchiveButton, 2);
            CreateArchiveButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            return;
        }

        HeaderBorder.Padding = new Thickness(32, 25, 32, 25);
        FormCard.Margin = new Thickness(28);
        FormCard.Padding = new Thickness(26);

        DateColumn.Width = new GridLength(230);
        DetailsSpacerColumn.Width = new GridLength(18);
        StatusColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailsGapRow.Height = new GridLength(0);

        Grid.SetRow(DateSection, 0);
        Grid.SetColumn(DateSection, 0);
        Grid.SetColumnSpan(DateSection, 1);
        Grid.SetRow(StatusBorder, 0);
        Grid.SetColumn(StatusBorder, 2);
        Grid.SetColumnSpan(StatusBorder, 1);

        FooterGapRow.Height = new GridLength(0);
        Grid.SetRow(FooterDescriptionTextBlock, 0);
        Grid.SetColumn(FooterDescriptionTextBlock, 0);
        Grid.SetColumnSpan(FooterDescriptionTextBlock, 1);
        Grid.SetRow(CreateArchiveButton, 0);
        Grid.SetColumn(CreateArchiveButton, 1);
        Grid.SetColumnSpan(CreateArchiveButton, 1);
        CreateArchiveButton.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveSettings(showErrors: false);
    }

    private void SourceBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedFolder = SelectFolder(
            "Güncellenecek dosyaların bulunduğu klasörü seçin",
            SourceFolderTextBox.Text);

        if (selectedFolder is null)
        {
            return;
        }

        SourceFolderTextBox.Text = selectedFolder;
        RefreshReadyState(showStatus: true);
        SaveSettings(showErrors: true);
    }

    private void TargetBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedFolder = SelectFolder(
            "ZIP dosyasının kaydedileceği klasörü seçin",
            TargetFolderTextBox.Text);

        if (selectedFolder is null)
        {
            return;
        }

        TargetFolderTextBox.Text = selectedFolder;
        RefreshReadyState(showStatus: true);
        SaveSettings(showErrors: true);
    }

    private void SelectedDatePicker_SelectedDateChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        RefreshReadyState(showStatus: true);
        SaveSettings(showErrors: true);
    }

    private void InstallationArchiveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        SelectedDatePicker.IsEnabled = !_isBusy && InstallationArchiveCheckBox.IsChecked != true;
        FooterDescriptionTextBlock.Text = InstallationArchiveCheckBox.IsChecked == true
            ? "Tüm klasör tarihe bakılmadan arşivlenir; ana dizindeki arşiv dosyaları atlanır."
            : "Alt klasör yapısı korunur; temp, pdf, excell, indir, .config ve deepzoom.aspx atlanır.";
        RefreshReadyState(showStatus: true);
    }

    private async void CreateArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryValidateInputs(out var validationMessage))
        {
            SetStatus("Eksik bilgi", validationMessage, StatusKind.Warning);
            return;
        }

        SaveSettings(showErrors: true);
        SetBusy(true);
        SetStatus(
            "Arşiv hazırlanıyor",
            "Dosyalar taranıyor ve ZIP paketine ekleniyor...",
            StatusKind.Information);

        try
        {
            var request = new ArchiveRequest(
                SourceFolderTextBox.Text.Trim(),
                TargetFolderTextBox.Text.Trim(),
                SelectedDatePicker.SelectedDate?.Date ?? DateTime.Today,
                InstallationArchiveCheckBox.IsChecked == true);
            var result = await _archiveService.CreateArchiveAsync(request);

            if (!result.Created)
            {
                SetStatus(
                    "Dosya bulunamadı",
                    "Arşivleme koşullarına uygun dosya bulunmadığı için ZIP oluşturulmadı.",
                    StatusKind.Warning);
                return;
            }

            SetStatus(
                "ZIP hazır",
                $"{result.FileCount} dosya arşivlendi: {result.ArchivePath}",
                StatusKind.Success);
        }
        catch (OperationCanceledException)
        {
            SetStatus("İşlem iptal edildi", "ZIP oluşturma işlemi tamamlanmadı.", StatusKind.Warning);
        }
        catch (Exception exception)
        {
            var message = $"ZIP oluşturulamadı: {exception.Message}";
            SetStatus("İşlem başarısız", message, StatusKind.Error);
            MessageBox.Show(
                this,
                message,
                "Güncelleme Hazırlayıcı",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string? SelectFolder(string title, string currentFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (Directory.Exists(currentFolder))
        {
            dialog.InitialDirectory = currentFolder;
        }

        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private void RefreshReadyState(bool showStatus)
    {
        var isValid = TryValidateInputs(out var validationMessage);
        CreateArchiveButton.IsEnabled = !_isBusy && isValid;

        if (!showStatus || _isBusy)
        {
            return;
        }

        if (isValid)
        {
            SetStatus(
                "Hazır",
                InstallationArchiveCheckBox.IsChecked == true
                    ? "Kurulum ZIP paketi tüm tarihlerdeki dosyalarla hazırlanabilir."
                    : "Seçilen tarihten itibaren değiştirilen dosyalar ZIP paketine alınabilir.",
                StatusKind.Information);
        }
        else
        {
            SetStatus("Seçim bekleniyor", validationMessage, StatusKind.Information);
        }
    }

    private bool TryValidateInputs(out string message)
    {
        if (string.IsNullOrWhiteSpace(SourceFolderTextBox.Text))
        {
            message = "Kaynak klasörü seçin.";
            return false;
        }

        if (!Directory.Exists(SourceFolderTextBox.Text.Trim()))
        {
            message = "Seçilen kaynak klasör artık mevcut değil.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(TargetFolderTextBox.Text))
        {
            message = "Hedef klasörü seçin.";
            return false;
        }

        if (!Directory.Exists(TargetFolderTextBox.Text.Trim()))
        {
            message = "Seçilen hedef klasör artık mevcut değil.";
            return false;
        }

        if (InstallationArchiveCheckBox.IsChecked != true && SelectedDatePicker.SelectedDate is null)
        {
            message = "Başlangıç tarihini seçin.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        SourceBrowseButton.IsEnabled = !isBusy;
        TargetBrowseButton.IsEnabled = !isBusy;
        InstallationArchiveCheckBox.IsEnabled = !isBusy;
        SelectedDatePicker.IsEnabled = !isBusy && InstallationArchiveCheckBox.IsChecked != true;
        OperationProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        RefreshReadyState(showStatus: false);
    }

    private void SaveSettings(bool showErrors)
    {
        if (_isInitializing)
        {
            return;
        }

        try
        {
            _settingsStore.Save(new AppSettings
            {
                SourceFolder = SourceFolderTextBox.Text.Trim(),
                TargetFolder = TargetFolderTextBox.Text.Trim(),
                SelectedDate = SelectedDatePicker.SelectedDate?.Date ?? DateTime.Today
            });
        }
        catch (Exception exception) when (!showErrors)
        {
            System.Diagnostics.Debug.WriteLine($"Ayarlar kaydedilemedi: {exception}");
        }
        catch (Exception exception)
        {
            SetStatus(
                "Ayarlar kaydedilemedi",
                exception.Message,
                StatusKind.Error);
        }
    }

    private void SetStatus(string title, string message, StatusKind kind)
    {
        var (background, border, text) = kind switch
        {
            StatusKind.Success => (SuccessBackground, SuccessBorder, SuccessText),
            StatusKind.Warning => (WarningBackground, WarningBorder, WarningText),
            StatusKind.Error => (ErrorBackground, ErrorBorder, ErrorText),
            _ => (InformationBackground, InformationBorder, InformationText)
        };

        StatusBorder.Background = background;
        StatusBorder.BorderBrush = border;
        StatusTitleTextBlock.Foreground = text;
        StatusTextBlock.Foreground = text;
        StatusTitleTextBlock.Text = title;
        StatusTextBlock.Text = message;
    }

    private static Brush CreateBrush(string color)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }

    private enum StatusKind
    {
        Information,
        Success,
        Warning,
        Error
    }
}
