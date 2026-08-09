using System;
using Avalonia;

namespace Alchemy;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<AlchemyApp>()
            .UsePlatformDetect()
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode =
                [
                    AvaloniaNativeRenderingMode.Software
                ]
            })
            .WithInterFont()
            .LogToTrace();
}
