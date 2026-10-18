using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Algetric.Desktop;

internal static class UiText
{
    public static LanguagePreferences Current { get; private set; } = new();
    public static void Initialize(string dataDirectory) => Current = new(Path.Combine(dataDirectory, "language.json"));
    public static string T(string text) => Current.Translate(text);
    public static string F(string template, params object[] arguments) => Current.Format(template, arguments);
    public static Binding Binding(string text) => new(nameof(LanguagePreferences.Language))
    {
        Source = Current,
        Converter = new FuncValueConverter<AppLanguage, string>(_ => T(text))
    };
}
