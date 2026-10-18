using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace Algetric.Desktop;

public enum AppLanguage { Portuguese, English }

public sealed class LanguagePreferences : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, string> English = LoadTranslations();
    private readonly string? settingsPath;
    public LanguagePreferences(string? settingsPath = null)
    {
        this.settingsPath = settingsPath;
        try
        {
            if (settingsPath is not null && File.Exists(settingsPath))
                Language = JsonSerializer.Deserialize<string>(File.ReadAllText(settingsPath)) == "en" ? AppLanguage.English : AppLanguage.Portuguese;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
    }
    public AppLanguage Language { get; private set; }
    public string Code => Language == AppLanguage.English ? "en" : "pt-BR";
    public CultureInfo Culture => CultureInfo.GetCultureInfo(Code);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Select(AppLanguage language)
    {
        if (!Enum.IsDefined(language)) throw new ArgumentOutOfRangeException(nameof(language));
        if (Language == language) return;
        if (settingsPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!);
            var temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(language == AppLanguage.English ? "en" : "pt-BR"));
            File.Move(temporaryPath, settingsPath, true);
        }
        Language = language;
        PropertyChanged?.Invoke(this, new(nameof(Language)));
    }
    public string Translate(string text) => Language == AppLanguage.English && English.TryGetValue(text, out var translated) ? translated : text;
    public string Format(string template, params object[] arguments) => string.Format(Culture, Translate(template), arguments);
    private static IReadOnlyDictionary<string, string> LoadTranslations()
    {
        using var resource = typeof(LanguagePreferences).Assembly.GetManifestResourceStream("Algetric.Desktop.Localization.en.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(resource)!;
    }
}
