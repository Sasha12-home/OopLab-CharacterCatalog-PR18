using System.Windows;
using System.Windows.Controls;
using OopLab.Core.Services;
using OopLab.Core.Specifications;
using OopLab.Core.ViewModels;
using OopLab.Data.Caching;
using OopLab.Data.Persistence;
using OopLab.Data.Remote;
using OopLab.Data.Storage;

namespace OopLab.Gui;

public partial class MainWindow : Window
{
    private readonly CharacterCatalogService _catalogService;
    private bool _isInitialized;

    public MainWindow()
    {
        InitializeComponent();

        var catalogJsonPath = AppStoragePaths.GetCatalogJsonFilePath();
        var portraitsCacheDir = AppStoragePaths.GetPortraitsCacheDirectory();

        var repository = new JsonCharacterRepository(catalogJsonPath);
        var imageCache = new DiskImageCacheStorage(portraitsCacheDir);
        var remoteProvider = new RickAndMortyApiClient();

        _catalogService = new CharacterCatalogService(repository, remoteProvider, imageCache);
        CachePathTextBlock.Text = $"Буфер портретів: {portraitsCacheDir}";

        _isInitialized = true;
        RefreshCards();
    }

    private void RefreshCards()
    {
        if (!_isInitialized) return;

        var selectedStatus = (StatusComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var selectedSort = (SortComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        var criteria = new CharacterFilterCriteria
        {
            SearchText = SearchTextBox.Text,
            Status = string.IsNullOrWhiteSpace(selectedStatus) ? null : selectedStatus,
            SortBy = selectedSort,
            OnlyCachedPortraits = OnlyCachedCheckBox.IsChecked == true ? true : null
        };

        var viewModels = _catalogService.GetAllViewModels(criteria);
        CharactersItemsControl.ItemsSource = viewModels;

        var stats = _catalogService.GetImageCacheStats();
        StatusTextBlock.Text =
            $"Відображено карток: {viewModels.Count} | Закешовано портретів на диску: {stats.CachedFilesCount} ({stats.TotalSizeBytes / 1024.0:F1} КБ)";
    }

    private void FilterControls_Changed(object sender, RoutedEventArgs e)
    {
        RefreshCards();
    }

    private async void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        SyncButton.IsEnabled = false;
        SyncButton.Content = "⏳ Завантаження з API...";
        StatusTextBlock.Text = "Синхронізація персонажів та буферизація портретів на диск...";

        try
        {
            var (synced, newlyCached) = await _catalogService.SyncWithRemoteAsync(limit: 20, downloadPortraits: true);
            RefreshCards();
            StatusTextBlock.Text =
                $"Синхронізовано {synced} персонажів. Нових портретів записано у локальний буфер: {newlyCached}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Помилка синхронізації: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SyncButton.Content = "⟳ Завантажити з API та закешувати (20)";
            SyncButton.IsEnabled = true;
        }
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CharacterViewModel vm })
        {
            var detailWindow = new CharacterDetailWindow(vm)
            {
                Owner = this
            };
            detailWindow.ShowDialog();
        }
    }
}
