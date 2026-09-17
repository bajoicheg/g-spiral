using System.Diagnostics;
using System.IO;

namespace GSpiral.Services;

public static class ShellOpenHelper
{
    public static ProcessStartInfo ForFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new ProcessStartInfo(path) { UseShellExecute = true };
    }

    public static ProcessStartInfo ForContainingFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The report path must have a containing directory.", nameof(path));
        return new ProcessStartInfo(directory) { UseShellExecute = true };
    }
}
