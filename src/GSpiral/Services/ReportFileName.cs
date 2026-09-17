using System.Globalization;
using System.IO;

namespace GSpiral.Services;

public static class ReportFileName
{
    public static string Build(string respondentName, string companyName, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(respondentName);
        ArgumentNullException.ThrowIfNull(companyName);

        var respondent = Sanitize(respondentName);
        var company = Sanitize(companyName);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Spiral_{respondent}_{company}_{date:yyyy-MM-dd}.xlsx");
    }

    public static string Build(string companyName, DateOnly date) =>
        Build(Environment.UserName, companyName, date);

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return new string(value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
