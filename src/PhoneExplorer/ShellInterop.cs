using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace PhoneExplorer;

// Minimal COM interop for the Windows Shell. The iPhone is exposed through the WPD shell
// namespace, whose items cannot be re-created from a path string, so we always keep and pass
// the IShellItem objects themselves.

[ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem
{
    [return: MarshalAs(UnmanagedType.Interface)]
    object BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid);
    IShellItem GetParent();
    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetDisplayName(SIGDN sigdnName);
    uint GetAttributes(uint sfgaoMask);
    int Compare(IShellItem psi, uint hint);
}

[ComImport, Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem2
{
    // IShellItem
    [return: MarshalAs(UnmanagedType.Interface)]
    object BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid);
    IShellItem GetParent();
    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetDisplayName(SIGDN sigdnName);
    uint GetAttributes(uint sfgaoMask);
    int Compare(IShellItem psi, uint hint);
    // IShellItem2
    void GetPropertyStore(int flags, [In] ref Guid riid, out IntPtr ppv);
    void GetPropertyStoreWithCreateObject(int flags, IntPtr punk, [In] ref Guid riid, out IntPtr ppv);
    void GetPropertyStoreForKeys(IntPtr rgKeys, uint cKeys, int flags, [In] ref Guid riid, out IntPtr ppv);
    void GetPropertyDescriptionList([In] ref PROPERTYKEY keyType, [In] ref Guid riid, out IntPtr ppv);
    void Update(IntPtr pbc);
    void GetProperty([In] ref PROPERTYKEY key, IntPtr ppropvar);
    void GetCLSID([In] ref PROPERTYKEY key, out Guid pclsid);
    [PreserveSig] int GetFileTime([In] ref PROPERTYKEY key, out FILETIME pft);
    void GetInt32([In] ref PROPERTYKEY key, out int pi);
    void GetString([In] ref PROPERTYKEY key, [MarshalAs(UnmanagedType.LPWStr)] out string ppsz);
    void GetUInt32([In] ref PROPERTYKEY key, out uint pui);
    [PreserveSig] int GetUInt64([In] ref PROPERTYKEY key, out ulong pull);
    void GetBool([In] ref PROPERTYKEY key, out int pf);
}

[ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IEnumShellItems
{
    [PreserveSig]
    int Next(uint celt, [MarshalAs(UnmanagedType.Interface)] out IShellItem rgelt, out uint pceltFetched);
    void Skip(uint celt);
    void Reset();
    IEnumShellItems Clone();
}

[ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItemImageFactory
{
    [PreserveSig]
    int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
}

[ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IFileOperation
{
    uint Advise(IntPtr pfops);
    void Unadvise(uint dwCookie);
    void SetOperationFlags(uint dwOperationFlags);
    void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
    void SetProgressDialog(IntPtr popd);
    void SetProperties(IntPtr pproparray);
    void SetOwnerWindow(IntPtr hwndOwner);
    void ApplyPropertiesToItem(IShellItem psiItem);
    void ApplyPropertiesToItems(IntPtr punkItems);
    void RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
    void RenameItems(IntPtr pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
    void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, IntPtr pfopsItem);
    void MoveItems(IntPtr punkItems, IShellItem psiDestinationFolder);
    void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszCopyName, IntPtr pfopsItem);
    void CopyItems(IntPtr punkItems, IShellItem psiDestinationFolder);
    void DeleteItem(IShellItem psiItem, IntPtr pfopsItem);
    void DeleteItems(IntPtr punkItems);
    void NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string? pszTemplateName, IntPtr pfopsItem);
    void PerformOperations();
    [return: MarshalAs(UnmanagedType.Bool)]
    bool GetAnyOperationsAborted();
}

[StructLayout(LayoutKind.Sequential)]
public struct SIZE
{
    public int cx, cy;
    public SIZE(int x, int y) { cx = x; cy = y; }
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;
    public PROPERTYKEY(Guid f, uint p) { fmtid = f; pid = p; }
}

public enum SIGDN : uint
{
    NORMALDISPLAY = 0,
    PARENTRELATIVEPARSING = 0x80018001,
    DESKTOPABSOLUTEPARSING = 0x80028000,
    PARENTRELATIVEEDITING = 0x80031001,
    DESKTOPABSOLUTEEDITING = 0x8004c000,
    FILESYSPATH = 0x80058000,
}

[Flags]
public enum SIIGBF
{
    RESIZETOFIT = 0x00,
    BIGGERSIZEOK = 0x01,
    MEMORYONLY = 0x02,
    ICONONLY = 0x04,
    THUMBNAILONLY = 0x08,
    INCACHEONLY = 0x10,
}

public static class Shell
{
    public const uint SFGAO_FOLDER = 0x20000000;
    public const uint SFGAO_CANDELETE = 0x00000020;

    public const uint FOF_NOCONFIRMMKDIR = 0x0200;
    public const uint FOF_NOERRORUI = 0x0400;
    public const uint FOFX_KEEPNEWERFILE = 0x00400000;
    public const uint FOF_NO_UI = 0x0614; // FOF_SILENT | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_NOCONFIRMMKDIR

    static Guid FOLDERID_ComputerFolder = new("0AC0837C-BBF8-452A-850D-79D08E667CA7");
    static Guid BHID_EnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");
    static Guid IID_IEnumShellItems = typeof(IEnumShellItems).GUID;
    static Guid IID_IShellItem = typeof(IShellItem).GUID;
    static readonly Guid CLSID_FileOperation = new("3ad05575-8857-4850-9277-11b85bdb8e09");

    static readonly Guid FMTID_Storage = new("B725F130-47EF-101A-A5F1-02608C9EEBAC");
    public static PROPERTYKEY PKEY_Size = new(FMTID_Storage, 12);
    public static PROPERTYKEY PKEY_DateModified = new(FMTID_Storage, 14);
    public static PROPERTYKEY PKEY_DateCreated = new(FMTID_Storage, 15);
    public static PROPERTYKEY PKEY_Media_Duration = new(new Guid("64440490-4C8B-11D1-8B70-080036B11A03"), 3);

    [DllImport("shell32.dll", PreserveSig = false)]
    static extern void SHGetKnownFolderItem([In] ref Guid rfid, int flags, IntPtr hToken, [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string pszPath, IntPtr pbc, [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);

    public static IShellItem ThisPC()
    {
        SHGetKnownFolderItem(ref FOLDERID_ComputerFolder, 0, IntPtr.Zero, ref IID_IShellItem, out var item);
        return item;
    }

    public static IShellItem FromFileSystemPath(string path)
    {
        SHCreateItemFromParsingName(path, IntPtr.Zero, ref IID_IShellItem, out var item);
        return item;
    }

    public static IEnumerable<IShellItem> Children(IShellItem folder)
    {
        var e = (IEnumShellItems)folder.BindToHandler(IntPtr.Zero, ref BHID_EnumItems, ref IID_IEnumShellItems);
        while (e.Next(1, out var child, out var fetched) == 0 && fetched == 1)
            yield return child;
    }

    public static string Name(IShellItem item) => item.GetDisplayName(SIGDN.NORMALDISPLAY);

    public static bool IsFolder(IShellItem item) => (item.GetAttributes(SFGAO_FOLDER) & SFGAO_FOLDER) != 0;

    public static long Size(IShellItem item)
    {
        var key = PKEY_Size;
        return ((IShellItem2)item).GetUInt64(ref key, out var v) == 0 ? (long)v : -1;
    }

    public static DateTime? Date(IShellItem item, PROPERTYKEY key)
    {
        if (((IShellItem2)item).GetFileTime(ref key, out var ft) != 0) return null;
        long ticks = ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
        if (ticks <= 0) return null;
        return DateTime.FromFileTimeUtc(ticks).ToLocalTime();
    }

    /// <summary>Returns the shell's thumbnail, or null if the shell has no real thumbnail for this item.</summary>
    public static Bitmap? Thumbnail(IShellItem item, int size)
    {
        var factory = (IShellItemImageFactory)item;
        if (factory.GetImage(new SIZE(size, size), SIIGBF.THUMBNAILONLY, out var hbm) != 0 || hbm == IntPtr.Zero)
            return null;
        try { return Image.FromHbitmap(hbm); }
        finally { DeleteObject(hbm); }
    }

    public static TimeSpan? Duration(IShellItem item)
    {
        var key = PKEY_Media_Duration;
        try
        {
            return ((IShellItem2)item).GetUInt64(ref key, out var v) == 0 && v > 0 ? TimeSpan.FromTicks((long)v) : null;
        }
        catch (COMException) { return null; }
    }

    /// <summary>Whether the device says this item may be deleted.</summary>
    public static bool CanDelete(IShellItem item) => (item.GetAttributes(SFGAO_CANDELETE) & SFGAO_CANDELETE) != 0;

    /// <summary>Deletes one item from the device without any shell UI. Blocks until done. Not undoable.</summary>
    public static void Delete(IShellItem item)
    {
        var op = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOperation)!)!;
        try
        {
            op.SetOperationFlags(FOF_NO_UI);
            op.DeleteItem(item, IntPtr.Zero);
            op.PerformOperations();
            if (op.GetAnyOperationsAborted()) throw new IOException("The delete was aborted.");
        }
        finally { Marshal.ReleaseComObject(op); }
    }

    /// <summary>Copies one item into a local folder without any shell UI. Blocks until done.</summary>
    public static void Copy(IShellItem item, string destFolder, string? newName)
    {
        Directory.CreateDirectory(destFolder);
        var op = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOperation)!)!;
        try
        {
            op.SetOperationFlags(FOF_NO_UI);
            op.CopyItem(item, FromFileSystemPath(destFolder), newName, IntPtr.Zero);
            op.PerformOperations();
            if (op.GetAnyOperationsAborted()) throw new IOException("The copy was aborted.");
        }
        finally { Marshal.ReleaseComObject(op); }
    }
}
