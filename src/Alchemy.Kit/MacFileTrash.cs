using System;
using System.Runtime.InteropServices;

namespace Alchemy.Kit;

public static class MacFileTrash
{
    public static bool TryMoveToTrash(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        var fileManagerClass = GetClass("NSFileManager");
        var stringClass = GetClass("NSString");
        var urlClass = GetClass("NSURL");
        if (fileManagerClass == nint.Zero ||
            stringClass == nint.Zero ||
            urlClass == nint.Zero)
        {
            return false;
        }

        var utf8Path = Marshal.StringToCoTaskMemUTF8(path);
        try
        {
            var nativePath = SendPointer(
                stringClass,
                Selector("stringWithUTF8String:"),
                utf8Path);
            var url = SendPointer(
                urlClass,
                Selector("fileURLWithPath:"),
                nativePath);
            var manager = SendPointer(
                fileManagerClass,
                Selector("defaultManager"));

            if (url == nint.Zero || manager == nint.Zero)
            {
                return false;
            }

            return SendTrash(
                manager,
                Selector("trashItemAtURL:resultingItemURL:error:"),
                url,
                out _,
                out _);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8Path);
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern nint GetClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern nint Selector(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint SendPointer(nint receiver, nint selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint SendPointer(
        nint receiver,
        nint selector,
        nint argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendTrash(
        nint receiver,
        nint selector,
        nint url,
        out nint resultingItemUrl,
        out nint error);
}