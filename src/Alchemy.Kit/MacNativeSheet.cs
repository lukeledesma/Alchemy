using System.Runtime.InteropServices;

namespace Alchemy.Kit;

public static class MacNativeSheet
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private const string SystemLibrary = "/usr/lib/libSystem.B.dylib";
    private const nint FirstButtonResponse = 1000;

    private static readonly BlockInvoke CompletionCallback =
        CompleteSheet;
    private static readonly nint BlockDescriptor =
        CreateBlockDescriptor();
    private static readonly nint StackBlockClass =
        GetStackBlockClass();

    public static Task<int?> ShowAsync(
        nint ownerWindow,
        string message,
        string informativeText,
        params string[] buttons)
    {
        if (!OperatingSystem.IsMacOS() ||
            ownerWindow == nint.Zero ||
            StackBlockClass == nint.Zero ||
            buttons.Length == 0)
        {
            return Task.FromResult<int?>(null);
        }

        var alertClass = objc_getClass("NSAlert");
        var alert = SendIntPtr(
            SendIntPtr(alertClass, Selector("alloc")),
            Selector("init"));
        if (alert == nint.Zero)
        {
            return Task.FromResult<int?>(null);
        }

        SendVoid(
            alert,
            Selector("setMessageText:"),
            NativeString(message));
        SendVoid(
            alert,
            Selector("setInformativeText:"),
            NativeString(informativeText));
        foreach (var button in buttons)
        {
            SendIntPtr(
                alert,
                Selector("addButtonWithTitle:"),
                NativeString(button));
        }

        var state = new SheetState(alert);
        var stateHandle = GCHandle.Alloc(state);
        var stackBlock = Marshal.AllocHGlobal(
            Marshal.SizeOf<BlockLiteral>());
        Marshal.StructureToPtr(
            new BlockLiteral
            {
                Isa = StackBlockClass,
                Invoke = Marshal.GetFunctionPointerForDelegate(
                    CompletionCallback),
                Descriptor = BlockDescriptor,
                Context = GCHandle.ToIntPtr(stateHandle)
            },
            stackBlock,
            false);

        try
        {
            state.CopiedBlock = BlockCopy(stackBlock);
            SendVoid(
                alert,
                Selector("beginSheetModalForWindow:completionHandler:"),
                ownerWindow,
                state.CopiedBlock);
            return state.Completion.Task;
        }
        catch
        {
            if (state.CopiedBlock != nint.Zero)
            {
                BlockRelease(state.CopiedBlock);
            }

            stateHandle.Free();
            SendVoid(alert, Selector("release"));
            return Task.FromResult<int?>(null);
        }
        finally
        {
            Marshal.FreeHGlobal(stackBlock);
        }
    }

    private static void CompleteSheet(nint block, nint response)
    {
        var literal = Marshal.PtrToStructure<BlockLiteral>(block);
        var handle = GCHandle.FromIntPtr(literal.Context);
        var state = (SheetState)handle.Target!;
        state.Completion.TrySetResult(
            checked((int)(response - FirstButtonResponse)));
        SendVoid(state.Alert, Selector("release"));
        BlockRelease(state.CopiedBlock);
        handle.Free();
    }

    private static nint CreateBlockDescriptor()
    {
        var pointer = Marshal.AllocHGlobal(
            Marshal.SizeOf<BlockDescriptorLayout>());
        Marshal.StructureToPtr(
            new BlockDescriptorLayout
            {
                Size = (nuint)Marshal.SizeOf<BlockLiteral>()
            },
            pointer,
            false);
        return pointer;
    }

    private static nint GetStackBlockClass()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return nint.Zero;
        }

        var library = NativeLibrary.Load(SystemLibrary);
        return NativeLibrary.GetExport(
            library,
            "_NSConcreteStackBlock");
    }

    private static nint NativeString(string value)
    {
        return SendIntPtr(
            objc_getClass("NSString"),
            Selector("stringWithUTF8String:"),
            value);
    }

    private static nint Selector(string name) => sel_registerName(name);

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        internal nint Isa;
        internal int Flags;
        internal int Reserved;
        internal nint Invoke;
        internal nint Descriptor;
        internal nint Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptorLayout
    {
        internal nuint Reserved;
        internal nuint Size;
    }

    private sealed class SheetState(nint alert)
    {
        internal nint Alert { get; } = alert;
        internal nint CopiedBlock { get; set; }
        internal TaskCompletionSource<int?> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void BlockInvoke(nint block, nint response);

    [DllImport(ObjectiveCLibrary)]
    private static extern nint objc_getClass(string name);

    [DllImport(ObjectiveCLibrary)]
    private static extern nint sel_registerName(string name);

    [DllImport(
        ObjectiveCLibrary,
        EntryPoint = "objc_msgSend")]
    private static extern nint SendIntPtr(nint receiver, nint selector);

    [DllImport(
        ObjectiveCLibrary,
        EntryPoint = "objc_msgSend")]
    private static extern nint SendIntPtr(
        nint receiver,
        nint selector,
        nint argument);

    [DllImport(
        ObjectiveCLibrary,
        EntryPoint = "objc_msgSend")]
    private static extern nint SendIntPtr(
        nint receiver,
        nint selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string argument);

    [DllImport(
        ObjectiveCLibrary,
        EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector);

    [DllImport(
        ObjectiveCLibrary,
        EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(
        nint receiver,
        nint selector,
        nint argument);

    [DllImport(
        ObjectiveCLibrary,
        EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(
        nint receiver,
        nint selector,
        nint firstArgument,
        nint secondArgument);

    [DllImport(SystemLibrary, EntryPoint = "_Block_copy")]
    private static extern nint BlockCopy(nint block);

    [DllImport(SystemLibrary, EntryPoint = "_Block_release")]
    private static extern void BlockRelease(nint block);
}
