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
    void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszCopyName, IFileOperationProgressSink? pfopsItem);
    void CopyItems(IntPtr punkItems, IShellItem psiDestinationFolder);
    void DeleteItem(IShellItem psiItem, IntPtr pfopsItem);
    void DeleteItems(IntPtr punkItems);
    void NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string? pszTemplateName, IntPtr pfopsItem);
    void PerformOperations();
    [return: MarshalAs(UnmanagedType.Bool)]
    bool GetAnyOperationsAborted();
}

/// <summary>Callbacks from IFileOperation. The vtable order must match shobjidl.h exactly.</summary>
[ComImport, Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IFileOperationProgressSink
{
    void StartOperations();
    void FinishOperations(int hrResult);
    void PreRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
    void PostRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, int hrRename, IShellItem? psiNewlyCreated);
    void PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
    void PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, int hrMove, IShellItem? psiNewlyCreated);
    void PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
    void PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, int hrCopy, IShellItem? psiNewlyCreated);
    void PreDeleteItem(uint dwFlags, IShellItem psiItem);
    void PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IShellItem? psiNewlyCreated);
    void PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
    void PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, [MarshalAs(UnmanagedType.LPWStr)] string? pszTemplateName, uint dwFileAttributes, int hrNew, IShellItem? psiNewItem);
    void UpdateProgress(uint iWorkTotal, uint iWorkSoFar);
    void ResetTimer();
    void PauseTimer();
    void ResumeTimer();
}

/// <summary>Records how one copy ended: the real error (FOF_NOERRORUI otherwise hides it) and the file it made.</summary>
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
sealed class CopySink : IFileOperationProgressSink
{
    public int Result;
    public string? Created;

    public void PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName, int hrCopy, IShellItem? psiNewlyCreated)
    {
        Result = hrCopy;
        try { Created = psiNewlyCreated?.GetDisplayName(SIGDN.FILESYSPATH); }
        catch (COMException) { }
    }

    public void FinishOperations(int hrResult) { if (Result >= 0 && hrResult < 0) Result = hrResult; }
    public void StartOperations() { }
    public void PreRenameItem(uint dwFlags, IShellItem psiItem, string? pszNewName) { }
    public void PostRenameItem(uint dwFlags, IShellItem psiItem, string? pszNewName, int hrRename, IShellItem? psiNewlyCreated) { }
    public void PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName) { }
    public void PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName, int hrMove, IShellItem? psiNewlyCreated) { }
    public void PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName) { }
    public void PreDeleteItem(uint dwFlags, IShellItem psiItem) { }
    public void PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IShellItem? psiNewlyCreated) { }
    public void PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, string? pszNewName) { }
    public void PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, string? pszNewName, string? pszTemplateName, uint dwFileAttributes, int hrNew, IShellItem? psiNewItem) { }
    public void UpdateProgress(uint iWorkTotal, uint iWorkSoFar) { }
    public void ResetTimer() { }
    public void PauseTimer() { }
    public void ResumeTimer() { }
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
    public static PROPERTYKEY PKEY_Photo_DateTaken = new(new Guid("14B81DA1-0135-4D31-96D9-6CBFC9671A99"), 36867);
    public static PROPERTYKEY PKEY_Media_DateEncoded = new(new Guid("2E4B640D-5019-46D8-8881-55414CC5CAA0"), 100);
    public static PROPERTYKEY PKEY_Image_Width = new(new Guid("6444048F-4C8B-11D1-8B70-080036B11A03"), 3);
    public static PROPERTYKEY PKEY_Image_Height = new(new Guid("6444048F-4C8B-11D1-8B70-080036B11A03"), 4);
    public static PROPERTYKEY PKEY_Video_Width = new(new Guid("64440491-4C8B-11D1-8B70-080036B11A03"), 3);
    public static PROPERTYKEY PKEY_Video_Height = new(new Guid("64440491-4C8B-11D1-8B70-080036B11A03"), 4);

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

    /// <summary>A 32-bit property such as a width, or 0 when the item doesn't have it.</summary>
    public static int UInt32(IShellItem item, PROPERTYKEY key)
    {
        try { ((IShellItem2)item).GetUInt32(ref key, out var v); return (int)Math.Min(v, int.MaxValue); }
        catch (COMException) { return 0; }
    }

    /// <summary>Dates, size and duration of a file on this PC, read the same way as the phone's (FILETIME, as local time).</summary>
    public static (DateTime? Taken, DateTime? Encoded, DateTime? Modified, int Width, int Height, TimeSpan? Duration) LocalProperties(string path)
    {
        var item = FromFileSystemPath(path);
        try
        {
            int w = UInt32(item, PKEY_Image_Width), h = UInt32(item, PKEY_Image_Height);
            if (w == 0 || h == 0) { w = UInt32(item, PKEY_Video_Width); h = UInt32(item, PKEY_Video_Height); }
            return (Date(item, PKEY_Photo_DateTaken), Date(item, PKEY_Media_DateEncoded), Date(item, PKEY_DateModified), w, h, Duration(item));
        }
        finally { Marshal.ReleaseComObject(item); }
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

    /// <summary>
    /// Copies one item into a local folder without any shell UI. Blocks until done. Returns the path
    /// of the file it wrote, or null if Windows didn't say. Throws IOException with the real reason.
    /// </summary>
    public static string? Copy(IShellItem item, string destFolder, string? newName)
    {
        Directory.CreateDirectory(destFolder);
        var dest = FromFileSystemPath(destFolder);
        var op = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOperation)!)!;
        var sink = new CopySink();
        try
        {
            op.SetOperationFlags(FOF_NO_UI);
            op.CopyItem(item, dest, newName, sink);
            try { op.PerformOperations(); }
            catch (COMException ex) { throw new IOException(Reason(sink.Result < 0 ? sink.Result : ex.HResult)); }
            if (sink.Result < 0) throw new IOException(Reason(sink.Result));
            if (op.GetAnyOperationsAborted()) throw new IOException("the phone stopped sending it");
            return sink.Created;
        }
        finally
        {
            Marshal.ReleaseComObject(op);
            Marshal.ReleaseComObject(dest);
        }
    }

    static Guid BHID_Stream = new("1cebb3ab-7c10-499a-a417-92ca16c4cb83");
    static Guid IID_IStream = new("0000000c-0000-0000-C000-000000000046");

    /// <summary>Opens an item's contents for reading, straight from the device (nothing is written to disk).</summary>
    public static Stream OpenRead(IShellItem item) => new ShellReadStream((IStream)item.BindToHandler(IntPtr.Zero, ref BHID_Stream, ref IID_IStream));

    /// <summary>Windows' text for an error code, with the code, so a report says what actually went wrong.</summary>
    static string Reason(int hr)
    {
        var text = Marshal.GetExceptionForHR(hr)?.Message.Trim().TrimEnd('.') ?? "unknown error";
        // Messages for codes Windows has no text for already include the code.
        return text.Contains("0x", StringComparison.OrdinalIgnoreCase) ? text : $"{text} (0x{hr:X8})";
    }
}

/// <summary>A forward-only, read-only Stream over a COM IStream.</summary>
sealed class ShellReadStream : Stream
{
    IStream? com;
    readonly IntPtr count = Marshal.AllocHGlobal(sizeof(int));
    byte[] chunk = Array.Empty<byte>();

    public ShellReadStream(IStream com) => this.com = com;

    public override int Read(byte[] buffer, int offset, int length)
    {
        var target = buffer;
        if (offset != 0)
        {
            if (chunk.Length < length) chunk = new byte[length];
            target = chunk;
        }
        try { (com ?? throw new ObjectDisposedException(nameof(ShellReadStream))).Read(target, length, count); }
        catch (COMException ex) { throw new IOException("the phone stopped sending it: " + ex.Message.Trim(), ex); }
        int n = Marshal.ReadInt32(count);
        if (offset != 0) Buffer.BlockCopy(chunk, 0, buffer, offset, n);
        return n;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (com == null) return;
        Marshal.ReleaseComObject(com);
        com = null;
        Marshal.FreeHGlobal(count);
        base.Dispose(disposing);
    }
}
