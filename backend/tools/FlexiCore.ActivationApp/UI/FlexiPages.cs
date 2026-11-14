using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace FlexiCore.ActivationApp;

internal static class FlexiPages
{
    public const string IconWarning = "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z";
    public const string IconInbox = "M19 3H4.99c-1.11 0-1.98.89-1.98 2L3 19c0 1.1.88 2 1.99 2H19c1.1 0 2-.9 2-2V5c0-1.11-.9-2-2-2zm0 12h-4c0 1.66-1.35 3-3 3s-3-1.34-3-3H4.99V5H19v10z";

    public static Control Callout(string? text, IBrush surface, IBrush foreground, string icon)
    {
        var glyph = FlexiUi.Icon(icon, foreground, 20);
        glyph.VerticalAlignment = VerticalAlignment.Top;
        glyph.Margin = new Thickness(0, 1, 12, 0);
        var label = FlexiUi.Text(text, 13, FontWeight.Medium, foreground);
        Grid.SetColumn(label, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(glyph);
        grid.Children.Add(label);
        return new Border
        {
            Background = surface,
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium),
            Padding = new Thickness(16, 14),
            Margin = new Thickness(0, 0, 0, 12),
            Child = grid
        };
    }

    public static Control EmptyState(string? text)
    {
        var (fill, tint) = FlexiUi.Accent(0);
        var badge = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(16),
            Background = tint,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = FlexiUi.Icon(IconInbox, fill, 24)
        };
        var message = FlexiUi.Text(text, 14, FontWeight.Medium, ActivationDesignSystem.SecondaryText);
        message.TextAlignment = TextAlignment.Center;
        message.HorizontalAlignment = HorizontalAlignment.Center;
        var stack = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(badge);
        stack.Children.Add(message);
        return new Border
        {
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusLarge),
            Padding = new Thickness(24, 28),
            Margin = new Thickness(0, 0, 0, 12),
            BoxShadow = ActivationDesignSystem.CardShadow,
            Child = stack
        };
    }

    public static Control StatusChip(string? status)
    {
        var s = (status ?? string.Empty).ToLowerInvariant();
        IBrush surface;
        IBrush text;
        if (s.Contains("expired")) { surface = ActivationDesignSystem.StatusErrorSurface; text = ActivationDesignSystem.StatusErrorText; }
        else if (s.Contains("not yet") || s.Contains("pending")) { surface = ActivationDesignSystem.StatusInfoSurface; text = ActivationDesignSystem.PrimaryText; }
        else if (s.Contains("grace")) { surface = ActivationDesignSystem.StatusWarningSurface; text = ActivationDesignSystem.Warning; }
        else if (s.Contains("active")) { surface = ActivationDesignSystem.StatusSuccessSurface; text = ActivationDesignSystem.StatusSuccessText; }
        else { surface = ActivationDesignSystem.ActionSurface; text = ActivationDesignSystem.SecondaryText; }
        return new Border
        {
            Background = surface,
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(10, 4),
            Child = FlexiUi.Text(status, 12, FontWeight.SemiBold, text, false)
        };
    }

    public static Control AvatarCard(string? title, IEnumerable<string> lines, int index, Control? trailing, bool compact)
    {
        var (fill, tint) = FlexiUi.Accent(index);
        var name = title ?? string.Empty;
        var initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
        var avatar = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = tint,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock
            {
                Text = initial,
                FontSize = 17,
                FontWeight = FontWeight.Bold,
                Foreground = fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var details = new StackPanel { Spacing = 3 };
        details.Children.Add(FlexiUi.Text(name, 16, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText));
        if (trailing is not null && compact)
        {
            trailing.HorizontalAlignment = HorizontalAlignment.Left;
            trailing.Margin = new Thickness(0, 2, 0, 2);
            details.Children.Add(trailing);
        }
        foreach (var line in lines)
            details.Children.Add(FlexiUi.Text(line, 12, FontWeight.Normal, ActivationDesignSystem.SecondaryText));

        var wide = trailing is not null && !compact;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(wide ? "Auto,*,Auto" : "Auto,*") };
        Grid.SetColumn(details, 1);
        grid.Children.Add(avatar);
        grid.Children.Add(details);
        if (wide)
        {
            trailing!.VerticalAlignment = VerticalAlignment.Top;
            trailing.Margin = new Thickness(12, 2, 0, 0);
            Grid.SetColumn(trailing, 2);
            grid.Children.Add(trailing);
        }
        return new Border
        {
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusLarge),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 10),
            BoxShadow = ActivationDesignSystem.CardShadow,
            Child = grid
        };
    }
}
