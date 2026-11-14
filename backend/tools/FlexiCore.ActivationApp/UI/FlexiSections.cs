using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace FlexiCore.ActivationApp;

internal static class FlexiSections
{
    public const string IconInfo = "M11 7h2v2h-2zm0 4h2v6h-2zm1-9C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z";

    private static Border Card(Control child, Thickness padding, double bottom = 12) =>
        new()
        {
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusLarge),
            Padding = padding,
            Margin = new Thickness(0, 0, 0, bottom),
            BoxShadow = ActivationDesignSystem.CardShadow,
            Child = child
        };

    private static Border NumberBadge(int number, double size, int accent)
    {
        var (fill, tint) = FlexiUi.Accent(accent);
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = tint,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock
            {
                Text = number.ToString(),
                FontSize = size * 0.42,
                FontWeight = FontWeight.Bold,
                Foreground = fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    public static Control Header(string title, string? subtitle, string icon)
    {
        var (fill, tint) = FlexiUi.Accent(0);
        var glyph = FlexiUi.Icon(icon, fill, 18);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.VerticalAlignment = VerticalAlignment.Center;
        var badge = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(12),
            Background = tint,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 12, 0),
            Child = glyph
        };
        var texts = new StackPanel { Spacing = 2 };
        texts.Children.Add(FlexiUi.Text(title, 19, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText));
        if (!string.IsNullOrWhiteSpace(subtitle))
            texts.Children.Add(FlexiUi.Text(subtitle, 13, FontWeight.Normal, ActivationDesignSystem.SecondaryText));
        Grid.SetColumn(texts, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 12, 0, 12) };
        grid.Children.Add(badge);
        grid.Children.Add(texts);
        return grid;
    }

    public static Control? KeyChip(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var s = status.ToLowerInvariant();
        IBrush surface;
        IBrush text;
        if (s.Contains("expired") || s.Contains("revoked") || s.Contains("compromised") || s.Contains("mismatch"))
        { surface = ActivationDesignSystem.StatusErrorSurface; text = ActivationDesignSystem.StatusErrorText; }
        else if (s.Contains("retired") || s.Contains("inactive") || s.Contains("disabled") || s.Contains("pending"))
        { surface = ActivationDesignSystem.StatusWarningSurface; text = ActivationDesignSystem.Warning; }
        else if (s.Contains("active") || s.Contains("current") || s.Contains("in use"))
        { surface = ActivationDesignSystem.StatusSuccessSurface; text = ActivationDesignSystem.StatusSuccessText; }
        else
        { surface = ActivationDesignSystem.ActionSurface; text = ActivationDesignSystem.SecondaryText; }
        return new Border
        {
            Background = surface,
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(10, 4),
            Child = FlexiUi.Text(status, 12, FontWeight.SemiBold, text, false)
        };
    }

    public static Control SelectableCard(string? title, IEnumerable<string?> lines, int index, Control? chip,
        bool compact, bool selected, Action onSelect, string iconData)
    {
        var (fill, tint) = FlexiUi.Accent(index);
        var glyph = FlexiUi.Icon(selected ? FlexiUi.IconCheck : iconData,
            selected ? ActivationDesignSystem.OnPrimary : fill, 20);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.VerticalAlignment = VerticalAlignment.Center;
        var avatar = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = selected ? ActivationDesignSystem.Primary : tint,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 14, 0),
            Child = glyph
        };
        var details = new StackPanel { Spacing = 3 };
        details.Children.Add(FlexiUi.Text(title, 16, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText));
        if (chip is not null && compact)
        {
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            chip.Margin = new Thickness(0, 2, 0, 2);
            details.Children.Add(chip);
        }
        foreach (var line in lines)
            if (!string.IsNullOrWhiteSpace(line))
                details.Children.Add(FlexiUi.Text(line, 12, FontWeight.Normal, ActivationDesignSystem.SecondaryText));

        var wide = chip is not null && !compact;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(wide ? "Auto,*,Auto" : "Auto,*") };
        Grid.SetColumn(details, 1);
        grid.Children.Add(avatar);
        grid.Children.Add(details);
        if (wide)
        {
            chip!.VerticalAlignment = VerticalAlignment.Top;
            chip.Margin = new Thickness(12, 2, 0, 0);
            Grid.SetColumn(chip, 2);
            grid.Children.Add(chip);
        }
        var card = new Border
        {
            Background = selected ? ActivationDesignSystem.SurfaceElevated : ActivationDesignSystem.Surface,
            BorderBrush = selected ? ActivationDesignSystem.Primary : ActivationDesignSystem.Border,
            BorderThickness = new Thickness(selected ? 2 : 1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusLarge),
            Padding = new Thickness(selected ? 15 : 16),
            Margin = new Thickness(0, 0, 0, 10),
            BoxShadow = ActivationDesignSystem.CardShadow,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            Child = grid
        };
        card.PointerPressed += (_, _) => onSelect();
        card.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space) { e.Handled = true; onSelect(); }
        };
        return card;
    }

    public static Control Info(string? text)
    {
        var glyph = FlexiUi.Icon(IconInfo, ActivationDesignSystem.Information, 20);
        glyph.VerticalAlignment = VerticalAlignment.Top;
        glyph.Margin = new Thickness(0, 1, 12, 0);
        var label = FlexiUi.Text(text, 13, FontWeight.Medium, ActivationDesignSystem.PrimaryText);
        Grid.SetColumn(label, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(glyph);
        grid.Children.Add(label);
        return new Border
        {
            Background = ActivationDesignSystem.StatusInfoSurface,
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium),
            Padding = new Thickness(16, 14),
            Margin = new Thickness(0, 0, 0, 12),
            Child = grid
        };
    }

    public static Control MonoValue(string? text) =>
        new SelectableTextBlock
        {
            Text = text ?? string.Empty,
            FontFamily = new FontFamily("Cascadia Mono,JetBrains Mono,DejaVu Sans Mono,Consolas,monospace"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ActivationDesignSystem.PrimaryText
        };

    public static Control DetailsCard(IEnumerable<(string Label, string Value)> items)
    {
        var stack = new StackPanel();
        var first = true;
        foreach (var (label, value) in items)
        {
            if (!first)
                stack.Children.Add(new Border
                {
                    Height = 1,
                    Background = ActivationDesignSystem.Border,
                    Margin = new Thickness(0, 12)
                });
            first = false;
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(FlexiUi.Text(label, 12, FontWeight.Medium, ActivationDesignSystem.SecondaryText));
            if (value.Contains('/') || value.Contains('\\'))
                row.Children.Add(MonoValue(value));
            else
                row.Children.Add(new SelectableTextBlock
                {
                    Text = value,
                    FontSize = 14,
                    FontWeight = FontWeight.Medium,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ActivationDesignSystem.PrimaryText
                });
            stack.Children.Add(row);
        }
        return Card(stack, new Thickness(18, 16));
    }

    public static Control HelpSection(int number, string? heading, string? body)
    {
        var texts = new StackPanel { Spacing = 6 };
        texts.Children.Add(FlexiUi.Text(heading, 16, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText));
        texts.Children.Add(FlexiUi.Text(body, 13, FontWeight.Normal, ActivationDesignSystem.SecondaryText));
        Grid.SetColumn(texts, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(NumberBadge(number, 32, number - 1));
        grid.Children.Add(texts);
        return Card(grid, new Thickness(18));
    }

    public static Control StepsCard(string title, IEnumerable<string> steps)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(FlexiUi.Text(title, 16, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText));
        var n = 1;
        foreach (var step in steps)
        {
            var text = FlexiUi.Text(step, 13, FontWeight.Medium, ActivationDesignSystem.PrimaryText);
            text.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(text, 1);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var badge = NumberBadge(n++, 26, 0);
            badge.VerticalAlignment = VerticalAlignment.Center;
            badge.Margin = new Thickness(0, 0, 12, 0);
            grid.Children.Add(badge);
            grid.Children.Add(text);
            stack.Children.Add(grid);
        }
        return Card(stack, new Thickness(18));
    }

    public static Control EventRow(string? when, string? keyId, string? action, string? detail)
    {
        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(FlexiUi.Text($"{action}  ·  {keyId}", 14, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText));
        if (!string.IsNullOrWhiteSpace(detail))
            stack.Children.Add(FlexiUi.Text(detail, 12, FontWeight.Normal, ActivationDesignSystem.SecondaryText));
        stack.Children.Add(FlexiUi.Text(when, 12, FontWeight.Medium, ActivationDesignSystem.SecondaryText));
        return Card(stack, new Thickness(16, 12), 8);
    }
}
