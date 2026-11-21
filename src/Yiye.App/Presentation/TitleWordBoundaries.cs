using System.Runtime.InteropServices;

namespace Yiye.Presentation;

internal static class TitleWordBoundaries
{
    public static HashSet<int>? Find(string text)
    {
        // Windows ICU's word iterator uses its dictionary for Chinese text.
        // The UTF-16 buffer must remain pinned until the iterator is closed.
        var pinnedText = GCHandle.Alloc(text, GCHandleType.Pinned);
        var iterator = IntPtr.Zero;
        try
        {
            var status = 0;
            iterator = Open(1, "zh", pinnedText.AddrOfPinnedObject(), text.Length, ref status);
            if (status > 0 || iterator == IntPtr.Zero) return null;

            var boundaries = new HashSet<int>();
            for (var position = First(iterator); position != -1; position = Next(iterator))
                boundaries.Add(position);
            return boundaries;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            // Older Windows versions retain character-aware title layout.
            return null;
        }
        finally
        {
            if (iterator != IntPtr.Zero) Close(iterator);
            pinnedText.Free();
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("icu.dll", EntryPoint = "ubrk_open", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Open(int type, [MarshalAs(UnmanagedType.LPStr)] string locale,
        IntPtr text, int textLength, ref int status);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("icu.dll", EntryPoint = "ubrk_first", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int First(IntPtr iterator);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("icu.dll", EntryPoint = "ubrk_next", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Next(IntPtr iterator);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("icu.dll", EntryPoint = "ubrk_close", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Close(IntPtr iterator);
}
