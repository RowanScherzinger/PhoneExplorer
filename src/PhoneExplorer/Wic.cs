using System.Runtime.InteropServices;

namespace PhoneExplorer;

// Minimal Windows Imaging Component interop: decode the first frame of any image Windows has a codec
// for (HEIC needs the HEIF and HEVC extensions from the Microsoft Store) into 32-bit BGRA pixels.
// GDI+ can't decode HEIC, so the exact-match comparison goes through WIC. WIC is free-threaded.

[ComImport, Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IWICImagingFactory
{
    IWICBitmapDecoder CreateDecoderFromFilename([MarshalAs(UnmanagedType.LPWStr)] string wzFilename, IntPtr pguidVendor, uint dwDesiredAccess, int metadataOptions);
    void CreateDecoderFromStream();
    void CreateDecoderFromFileHandle();
    void CreateComponentInfo();
    void CreateDecoder();
    void CreateEncoder();
    void CreatePalette();
    IWICFormatConverter CreateFormatConverter();
}

[ComImport, Guid("9edde9e7-8dee-47ea-99df-e6faf2ed44bf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IWICBitmapDecoder
{
    void QueryCapability();
    void Initialize();
    void GetContainerFormat();
    void GetDecoderInfo();
    void CopyPalette();
    void GetMetadataQueryReader();
    void GetPreview();
    void GetColorContexts();
    void GetThumbnail();
    uint GetFrameCount();
    IWICBitmapSource GetFrame(uint index);
}

[ComImport, Guid("00000120-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IWICBitmapSource
{
    void GetSize(out uint width, out uint height);
    void GetPixelFormat(out Guid format);
    void GetResolution(out double dpiX, out double dpiY);
    void CopyPalette(IntPtr palette);
    void CopyPixels(IntPtr rect, uint stride, uint bufferSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer);
}

[ComImport, Guid("00000301-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IWICFormatConverter
{
    // IWICBitmapSource
    void GetSize(out uint width, out uint height);
    void GetPixelFormat(out Guid format);
    void GetResolution(out double dpiX, out double dpiY);
    void CopyPalette(IntPtr palette);
    void CopyPixels(IntPtr rect, uint stride, uint bufferSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer);
    // IWICFormatConverter
    void Initialize(IWICBitmapSource source, [In] ref Guid dstFormat, int dither, IntPtr palette, double alphaThreshold, int paletteType);
}

public static class Wic
{
    static readonly Guid CLSID_WICImagingFactory = new("cacaf262-9370-4615-a13b-9f5539da4c0a");
    static Guid GUID_WICPixelFormat32bppBGRA = new("6fddc324-4e03-4bfe-b185-3d77768dc90f");
    const uint GENERIC_READ = 0x80000000;

    /// <summary>The first frame's pixels as 32-bit BGRA, or null if Windows can't decode the file.</summary>
    public static (int Width, int Height, byte[] Pixels)? Decode(string path)
    {
        object? factoryObj = null, decoder = null, frame = null, converter = null;
        try
        {
            factoryObj = Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_WICImagingFactory)!)!;
            var factory = (IWICImagingFactory)factoryObj;
            var dec = factory.CreateDecoderFromFilename(path, IntPtr.Zero, GENERIC_READ, 0 /* WICDecodeMetadataCacheOnDemand */);
            decoder = dec;
            if (dec.GetFrameCount() == 0) return null;
            var src = dec.GetFrame(0);
            frame = src;
            var conv = factory.CreateFormatConverter();
            converter = conv;
            conv.Initialize(src, ref GUID_WICPixelFormat32bppBGRA, 0 /* none */, IntPtr.Zero, 0, 0 /* custom */);
            conv.GetSize(out var w, out var h);
            long size = (long)w * h * 4;
            if (w == 0 || h == 0 || size > int.MaxValue) return null;
            var pixels = new byte[size];
            conv.CopyPixels(IntPtr.Zero, w * 4, (uint)size, pixels);
            return ((int)w, (int)h, pixels);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or OutOfMemoryException or FileNotFoundException) { return null; }
        finally
        {
            foreach (var o in new[] { converter, frame, decoder, factoryObj })
                if (o != null) Marshal.ReleaseComObject(o);
        }
    }

    /// <summary>
    /// True when both files decode to exactly the same pixels. Null when the reference (a) can't be
    /// decoded, which means there's no codec for it. False when a decodes but b doesn't: b is damaged.
    /// </summary>
    public static bool? SamePixels(string a, string b)
    {
        var x = Decode(a);
        if (x == null) return null;
        var y = Decode(b);
        if (y == null) return false;
        return x.Value.Width == y.Value.Width && x.Value.Height == y.Value.Height &&
               x.Value.Pixels.AsSpan().SequenceEqual(y.Value.Pixels);
    }
}
