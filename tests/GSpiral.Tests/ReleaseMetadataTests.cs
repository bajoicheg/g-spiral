using System.Xml.Linq;
using GSpiral.Services;

namespace GSpiral.Tests;

public sealed class ReleaseMetadataTests
{
    [Fact]
    public void V13_HasMatchingApplicationVersionAndSpiralIconResource()
    {
        Assert.Equal("1.3.0", AppMetadata.Version);

        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "src", "GSpiral", "GSpiral.csproj");
        var project = XDocument.Load(projectPath);
        var applicationIcon = project.Descendants("ApplicationIcon").SingleOrDefault()?.Value;

        Assert.Equal("Assets\\G-Spiral.ico", applicationIcon);
        Assert.True(File.Exists(Path.Combine(root, "src", "GSpiral", "Assets", "G-Spiral.ico")));
        Assert.True(File.Exists(Path.Combine(root, "src", "GSpiral", "Assets", "G-Spiral-icon.png")));
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
