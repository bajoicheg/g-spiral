using System.Xml.Linq;
using GSpiral.Services;

namespace GSpiral.Tests;

public sealed class ReleaseMetadataTests
{
    [Fact]
    public void V131_HasMatchingApplicationVersionAndSpiralIconResource()
    {
        Assert.Equal("1.3.1", AppMetadata.Version);

        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "src", "GSpiral", "GSpiral.csproj");
        var project = XDocument.Load(projectPath);
        var applicationIcon = project.Descendants("ApplicationIcon").SingleOrDefault()?.Value;
        var version = project.Descendants("Version").SingleOrDefault()?.Value;

        Assert.Equal("1.3.1", version);
        Assert.Equal("Assets\\G-Spiral.ico", applicationIcon);
        Assert.True(File.Exists(Path.Combine(root, "src", "GSpiral", "Assets", "G-Spiral.ico")));
        Assert.True(File.Exists(Path.Combine(root, "src", "GSpiral", "Assets", "G-Spiral-icon.png")));
    }

    [Fact]
    public void MainWindow_ShowsSpiralIconExplainsBothPercentSemanticsAndUsesV131Exporter()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "GSpiral", "MainWindow.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "src", "GSpiral", "MainWindow.xaml.cs"));

        Assert.Contains("Icon=\"Assets/G-Spiral.ico\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ResultsPercentExplanation", xaml, StringComparison.Ordinal);
        Assert.Contains("PieShareExplanation", xaml, StringComparison.Ordinal);
        Assert.Contains("ExcelReportExporterV131.Export", codeBehind, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GSpiral.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing GSpiral.sln was not found.");
    }
}
