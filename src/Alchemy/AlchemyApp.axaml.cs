using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Alchemy.Core;

namespace Alchemy;

public partial class AlchemyApp : Application
{
    private bool _shutdownConfirmed;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = CreateWindow(desktop.Args);
            desktop.ShutdownRequested += DesktopShutdownRequested;

            if (desktop is IActivatableLifetime activatable)
            {
                activatable.Activated += (_, _) => ReopenWindow(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void DesktopShutdownRequested(
        object? sender,
        ShutdownRequestedEventArgs e)
    {
        if (_shutdownConfirmed)
        {
            return;
        }

        if (sender is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not AlchemyWindow window)
        {
            return;
        }

        e.Cancel = true;
        if (!await window.ConfirmApplicationExitAsync())
        {
            return;
        }

        _shutdownConfirmed = true;
        desktop.Shutdown();
    }

    private static AlchemyWindow CreateWindow(string[]? args = null)
    {
        var documentPath = args?
            .FirstOrDefault(path =>
                File.Exists(path) &&
                string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase));
        var settings = AlchemySettingsStore.Load();
        var context = new ToolLaunchContext(
            Guid.NewGuid(),
            documentPath,
            settings.RootPath);
        return new AlchemyWindow(context);
    }

    private static void ReopenWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var window = desktop.Windows.OfType<AlchemyWindow>().FirstOrDefault();
        if (window is null)
        {
            window = CreateWindow();
            desktop.MainWindow = window;
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    private void OpenSettings(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is AlchemyWindow window)
        {
            window.ShowSettings();
            window.Activate();
        }
    }

}
