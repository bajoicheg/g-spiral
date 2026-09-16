using System.ComponentModel;
using System.Diagnostics;
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
            FileName = ReportFileName.Build(vm.TrimmedRespondentName, vm.TrimmedCompanyName, vm.CurrentReportDate)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            ExcelReportExporter.Export(
                dialog.FileName,
                vm.TrimmedRespondentName,
                vm.TrimmedCompanyName,
                DateTime.Now,
                vm.SurveyState);
            vm.MarkReportSaved(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OpenXmlPackageException)
        {
            ShowActionError(
                "Не удалось сохранить файл. Выберите другое место или закройте открытый файл и повторите попытку.",
                ex);
        }
    }

    private void OpenSavedReport_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.HasSavedReport)
        {
            return;
        }

        if (!File.Exists(vm.LastSavedReportPath))
        {
            MessageBox.Show(this, "Сохранённый файл больше не найден по указанному пути.", "G-Spiral", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(ShellOpenHelper.ForFile(vm.LastSavedReportPath));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ShowActionError("Не удалось открыть сохранённый файл.", ex);
        }
    }

    private void OpenSavedFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.HasSavedReport)
        {
            return;
        }

        try
        {
            var startInfo = ShellOpenHelper.ForContainingFolder(vm.LastSavedReportPath);
            if (!Directory.Exists(startInfo.FileName))
            {
                MessageBox.Show(this, "Папка с сохранённым файлом больше не существует.", "G-Spiral", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowActionError("Не удалось открыть папку с сохранённым файлом.", ex);
        }
    }

    private void ShowActionError(string message, Exception ex)
    {
        MessageBox.Show(
            this,
            $"{message}\n\n{ex.Message}",
            "G-Spiral",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
