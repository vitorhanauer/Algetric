using Algetric.Core;
using Algetric.Desktop;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

public sealed partial class SmokeApplication
{
    private static MenuItem LanguageChoice(MainWindow window, string name) => window.GetVisualDescendants().OfType<Menu>().Single()
        .Items.OfType<MenuItem>().Single(item => item.Name == "LanguageMenu").Items.OfType<MenuItem>().Single(item => item.Name == name);

    private static async Task VerifyLanguagesAsync(MainWindow window, EditorViewModel model)
    {
        var before = model.Editor.Document; var revision = model.Revision;
        var english = LanguageChoice(window, "EnglishLanguage");
        var portuguese = LanguageChoice(window, "PortugueseLanguage");
        try
        {
            english.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(100);
            var menus = window.GetVisualDescendants().OfType<Menu>().Single().Items.OfType<MenuItem>().ToArray();
            CheckLayer(Equals(menus[0].Header, "File") && Equals(menus[3].Header, "Appearance") && Equals(menus[4].Header, "Language"),
                "Changing language must translate existing menus immediately.");
            CheckLayer(Equals(english.Icon, "✓") && Equals(portuguese.Icon, ""), "The active language must be marked.");
            CheckLayer(window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Properties"), "Sidebar headings must translate.");
            CheckLayer(window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "PropertyVertex").Items[0]?.ToString() == "Vertex 1",
                "The existing properties panel must translate its vertex choices.");
            var select = window.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Select");
            CheckLayer(ToolTip.GetTip(select)?.ToString() == "Select (V)", "Tooltips must translate and retain their shortcuts.");
            CheckLayer(new LanguagePreferences(Path.GetFullPath("artifacts/smoke/profile/language.json")).Language == AppLanguage.English,
                "The selected language must be saved for the next launch.");
            menus[1].Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Generate point path…"))
                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(100);
            var dialog = LayerDialog("Generate point path");
            CheckLayer(dialog.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Obstructions (optional)"),
                "New coverage dialogs must use the selected language.");
            var start = dialog.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "CoverageStartPoint");
            CheckLayer(start.Items.Cast<object>().Any(choice => choice.ToString()!.StartsWith("Vertex 1 —")), "Endpoint vertex choices must translate.");
            dialog.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "CoverageWidth").Text = "0";
            dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "GenerateCoveragePath")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(150);
            CheckLayer(dialog.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "CoverageError").Text == "Width must be greater than zero.",
                "Validation errors from the planner must translate.");
            SaveCoverageScreenshot(dialog, "language-english-dialog"); dialog.Close();
            CheckLayer(ReferenceEquals(before, model.Editor.Document) && model.Revision == revision,
                "Changing language must preserve the document, object names, selection, and history.");
            SaveCoverageScreenshot(window, "language-english");
        }
        finally { portuguese.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(100); }
        CheckLayer(window.GetVisualDescendants().OfType<Menu>().Single().Items.OfType<MenuItem>().First().Header?.ToString() == "Arquivo",
            "Switching back must restore Portuguese in existing controls.");
        Console.WriteLine("LANGUAGE PASS: live menus, properties, tooltips, dialogs, validation, persistence and unchanged project.");
    }
}
