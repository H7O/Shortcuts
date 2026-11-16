using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Shortcuts.Helpers;

public static partial class ShellIconHelper
{
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct SHFILEINFOW
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        public fixed ushort szDisplayName[260];
        public fixed ushort szTypeName[80];
    }

    [LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr SHGetFileInfo(string path, uint fileAttributes, ref SHFILEINFOW info, uint infoSize, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr icon);

    /// <summary>
    /// Returns the icon Explorer shows for the path, frozen so it can be handed to the UI thread.
    /// This reads the disk and can be slow for unreachable network paths, so call it off the UI thread.
    /// </summary>
    public static ImageSource? GetIcon(string path, bool isFolder)
    {
        var info = new SHFILEINFOW();
        var size = (uint)Marshal.SizeOf<SHFILEINFOW>();

        SHGetFileInfo(path, 0, ref info, size, SHGFI_ICON | SHGFI_LARGEICON);

        if (info.hIcon == IntPtr.Zero)
        {
            // The target is missing, so ask for the generic icon for its kind instead of reading the disk.
            var attributes = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
            SHGetFileInfo(path, attributes, ref info, size, SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
        }

        if (info.hIcon == IntPtr.Zero)
            return null;

        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }
}
