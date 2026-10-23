using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WallpaperSceneExporter.ViewModels;

namespace WallpaperSceneExporter.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        bool valid = e.Data.GetDataPresent(DataFormats.FileDrop) &&
                     ((string[])e.Data.GetData(DataFormats.FileDrop)).Any(path =>
                         string.Equals(Path.GetExtension(path), ".pkg", StringComparison.OrdinalIgnoreCase));
        e.Effects = valid ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.BorderBrush = valid ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("ErrorBrush");
        DropZone.BorderThickness = new Thickness(2);
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => ResetDropZone();

    private async void OnDrop(object sender, DragEventArgs e)
    {
        ResetDropZone();
        if (DataContext is not MainViewModel viewModel || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        string? path = ((string[])e.Data.GetData(DataFormats.FileDrop)).FirstOrDefault(file =>
            string.Equals(Path.GetExtension(file), ".pkg", StringComparison.OrdinalIgnoreCase));
        if (path is null)
        {
            viewModel.ReportUiError("Escolha um arquivo de cena válido.");
            return;
        }
        try { await viewModel.ImportSceneAsync(path); }
        catch (Exception ex) { viewModel.ReportUiError(ex.Message); }
    }

    private void ResetDropZone()
    {
        DropZone.BorderBrush = (Brush)FindResource("DropBorderBrush");
        DropZone.BorderThickness = new Thickness(1);
    }
}
