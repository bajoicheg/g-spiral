namespace GSpiral.Services;

public static class AppMetadata
{
    public const string Version = "1.3.1";
    public const string ProductName = "G-Spiral";
    public const string CopyrightText = "© 2026 V. Vasilev";
    public const string RepositoryUrl = "https://github.com/bajoicheg/g-spiral";

    public const string AboutText =
        "G-Spiral — локальное Windows-приложение для структурированного опроса и визуализации корпоративных культур. " +
        "Цветовой подход вдохновлён Spiral Dynamics Дона Бека и Криса Коуэна, основанной на работах Клэра Грейвза. " +
        "Программа не является официальным диагностическим инструментом Spiral Dynamics и предназначена для практической визуализации ответов.";

    public static string FooterText => $"Сформировано {ProductName} {Version}";
}
