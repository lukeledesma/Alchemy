using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Alchemy.Kit;

/// <summary>Shared behavior and visual baseline for every Alchemy tool window.</summary>
public class ToolWindow : Window
{
    public ToolWindow()
    {
        Width = 760;
        Height = 540;
        MinWidth = 440;
        MinHeight = 320;
        ApplyThemeSurface();
        ActualThemeVariantChanged += (_, _) => ApplyThemeSurface();
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void ApplyThemeSurface()
    {
        Background = ActualThemeVariant == ThemeVariant.Light
            ? Brush.Parse("#F4F4F4")
            : Brush.Parse("#171717");
    }
}
