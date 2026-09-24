using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BdoTimers.SetupUi;

/// <summary>
/// The modern Windows folder picker (IFileOpenDialog with FOS_PICKFOLDERS). WPF on .NET Framework only offers the
/// old tree-style dialog, and .NET 8's OpenFolderDialog isn't available here.
/// </summary>
internal static class FolderPicker
{
    const uint FosPickFolders = 0x20;
    const uint FosForceFileSystem = 0x40;
    const uint SigdnFileSysPath = 0x80058000;
    const int ErrorCancelled = unchecked((int)0x800704C7);

    /// <summary>The chosen folder, or null when the user cancels.</summary>
    public static string? Pick(Window? owner, string initialFolder)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        try
        {
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | FosPickFolders | FosForceFileSystem);
            dialog.SetTitle("Choose where the BdoTimers folder goes");
            if (SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, typeof(IShellItem).GUID, out var start) == 0)
                dialog.SetFolder(start);
            var hwnd = owner is null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;
            var hr = dialog.Show(hwnd);
            if (hr == ErrorCancelled) return null;
            Marshal.ThrowExceptionForHR(hr);
            dialog.GetResult(out var item);
            item.GetDisplayName(SigdnFileSysPath, out var path);
            return path;
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr bindCtx, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    class FileOpenDialog { }

    [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint count, IntPtr specs);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr bindCtx, ref Guid handler, ref Guid riid, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint sigdn, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }
}
