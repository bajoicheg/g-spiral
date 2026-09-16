using System.IO;
using System.Windows;
using DocumentFormat.OpenXml.Packaging;
using GSpiral.Presentation;
using GSpiral.Services;
using Microsoft.Win32;

namespace GSpiral;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void SaveReport_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
            OverwritePrompt = true,
            FileName = ReportFileName.Build(vm.TrimmedCompanyName, vm.CurrentReportDate)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            ExcelReportExporter.Export(dialog.FileName, vm.TrimmedCompanyName, vm.CurrentReportDate, vm.SurveyState);
            MessageBox.Show(this, $"Файл сохранён:\n{dialog.FileName}", "G-Spiral", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OpenXmlPackageException)
        {
            MessageBox.Show(
                this,
                $"Не удалось сохранить файл. Выберите другое место или закройте открытый файл и повторите попытку.\n\n{ex.Message}",
                "G-Spiral",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
