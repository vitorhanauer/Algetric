using Algetric.Desktop;
using Xunit;

namespace Algetric.Tests;

public class LanguagePreferencesTests
{
    [Fact]
    public void SwitchesTranslationsInBothDirectionsAndNotifiesBindings()
    {
        var preferences = new LanguagePreferences(); var notifications = 0;
        preferences.PropertyChanged += (_, _) => notifications++;
        Assert.Equal("Arquivo", preferences.Translate("Arquivo"));
        preferences.Select(AppLanguage.English);
        Assert.Equal("File", preferences.Translate("Arquivo"));
        Assert.Equal("Generate point path", preferences.Translate("Gerar caminho de pontos"));
        Assert.Equal("Vertex 3", preferences.Format("Vértice {0}", 3));
        Assert.Equal("My custom polygon", preferences.Translate("My custom polygon"));
        preferences.Select(AppLanguage.Portuguese);
        Assert.Equal("Arquivo", preferences.Translate("Arquivo"));
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void PersistsTheChoiceAcrossApplicationStarts()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var path = Path.Combine(directory, "language.json");
            new LanguagePreferences(path).Select(AppLanguage.English);
            Assert.Equal(AppLanguage.English, new LanguagePreferences(path).Language);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("invalid json")]
    [InlineData("\"fr\"")]
    public void InvalidSettingsFallBackToPortuguese(string content)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, content); Assert.Equal(AppLanguage.Portuguese, new LanguagePreferences(path).Language); }
        finally { File.Delete(path); }
    }
}
