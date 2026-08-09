using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Alchemy.Kit;

public static class MacTitleBar
{
    private const double VisualScale = 1.10;
    private const double SpacingScale = 1.15;
    private const double VerticalOffset = 9;
    private const double HorizontalOffset = 8;
    private static readonly Dictionary<nint, WindowButtonLayout> WindowLayouts = [];

    public static void AlignTrafficLights(Window window)
    {
        if (!System.OperatingSystem.IsMacOS())
        {
            return;
        }

        var nsWindow = window.TryGetPlatformHandle()?.Handle ?? nint.Zero;
        if (nsWindow == nint.Zero)
        {
            return;
        }

        var standardWindowButton = Selector("standardWindowButton:");
        var frame = Selector("frame");
        var setFrame = Selector("setFrame:");
        var setBoundsSize = Selector("setBoundsSize:");
        var closeButton = SendInt(nsWindow, standardWindowButton, 0);
        var miniButton = SendInt(nsWindow, standardWindowButton, 1);
        var zoomButton = SendInt(nsWindow, standardWindowButton, 2);
        if (closeButton == nint.Zero ||
            miniButton == nint.Zero ||
            zoomButton == nint.Zero)
        {
            return;
        }

        if (!WindowLayouts.TryGetValue(nsWindow, out var layout) ||
            !layout.Matches(closeButton, miniButton, zoomButton))
        {
            var closeOriginal = SendRect(closeButton, frame);
            var miniOriginal = SendRect(miniButton, frame);
            var zoomOriginal = SendRect(zoomButton, frame);

            layout = new WindowButtonLayout(
                closeButton,
                miniButton,
                zoomButton,
                closeOriginal,
                miniOriginal,
                zoomOriginal);

            WindowLayouts[nsWindow] = layout;
        }

        var buttons = new[] { closeButton, miniButton, zoomButton };
        var originals = new[]
        {
            layout.CloseOriginal,
            layout.MiniOriginal,
            layout.ZoomOriginal
        };

        var firstCenterX = originals[0].Origin.X + originals[0].Size.Width / 2;

        for (var buttonKind = 0; buttonKind < 3; buttonKind++)
        {
            var original = originals[buttonKind];
            var width = original.Size.Width * VisualScale;
            var height = original.Size.Height * VisualScale;
            var originalCenterX = original.Origin.X + original.Size.Width / 2;
            var centerX = firstCenterX + HorizontalOffset +
                          (originalCenterX - firstCenterX) * SpacingScale;
            var centered = new NativeRect(
                new NativePoint(
                    centerX - width / 2,
                    original.Origin.Y - VerticalOffset - (height - original.Size.Height) / 2),
                new NativeSize(width, height));

            SendRect(buttons[buttonKind], setFrame, centered);

            // Keep the original drawing coordinate space while enlarging the
            // frame so AppKit scales the visible traffic-light artwork too.
            SendSize(buttons[buttonKind], setBoundsSize, original.Size);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativePoint(double X, double Y);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeSize(double Width, double Height);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeRect(NativePoint Origin, NativeSize Size);

    private readonly record struct WindowButtonLayout(
        nint CloseButton,
        nint MiniButton,
        nint ZoomButton,
        NativeRect CloseOriginal,
        NativeRect MiniOriginal,
        NativeRect ZoomOriginal)
    {
        public bool Matches(nint closeButton, nint miniButton, nint zoomButton)
        {
            return CloseButton == closeButton &&
                   MiniButton == miniButton &&
                   ZoomButton == zoomButton;
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern nint Selector(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint SendInt(nint receiver, nint selector, nint argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern NativeRect SendRect(nint receiver, nint selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendRect(nint receiver, nint selector, NativeRect frame);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendSize(nint receiver, nint selector, NativeSize size);
}