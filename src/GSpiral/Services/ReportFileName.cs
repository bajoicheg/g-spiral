using System.Globalization;
using System.IO;

namespace GSpiral.Services;

public static class ReportFileName
{
    public static string Build(string companyName, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(companyName);

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var company = new string(companyName.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.Create(CultureInfo.InvariantCulture, $"Корпоративная_культура_{company}_{date:yyyy-MM-dd}.xlsx");
    }
}
