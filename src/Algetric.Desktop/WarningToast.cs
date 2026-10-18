using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Media;
using Avalonia.Threading;

namespace Algetric.Desktop;

internal sealed class WarningToast : Popup
{
    private readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer dismissTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    public WarningToast()
    {
        Name = "WarningToast";
        Placement = PlacementMode.AnchorAndGravity;
        PlacementAnchor = PopupAnchor.Bottom; PlacementGravity = PopupGravity.Top;
        VerticalOffset = -24; IsLightDismissEnabled = false; TakesFocusFromNativeControl = false;
        var card = new Border { Child = message, Padding = new Thickness(18, 12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
        UiTheme.Bind(card, Border.BackgroundProperty, "WarningSurface");
        UiTheme.Bind(card, Border.BorderBrushProperty, "WarningBorder");
        UiTheme.Bind(message, TextBlock.ForegroundProperty, "WarningText");
        Child = card;
        dismissTimer.Tick += (_, _) => IsOpen = false;
        Closed += (_, _) => dismissTimer.Stop();
    }
    public void Show(string text)
    {
        message.Text = text;
        message.MaxWidth = Math.Max(180, Math.Min(500, PlacementTarget!.Bounds.Width - 84));
        dismissTimer.Stop(); IsOpen = true; dismissTimer.Start();
    }
}
