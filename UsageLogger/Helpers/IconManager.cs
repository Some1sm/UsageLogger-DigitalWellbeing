using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using UsageLogger.Core;
using Microsoft.UI.Xaml.Media.Imaging;

namespace UsageLogger.Helpers
{
    public static class IconManager
    {
        // --- P/Invoke declarations ---
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", EntryPoint = "#727")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, ref IImageList ppv);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        // COM IImageList interface for extracting icons from the shell image list
        [ComImport]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageList
        {
            [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
            [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
            [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
            [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
            [PreserveSig] int Draw(ref IMAGELISTDRAWPARAMS pimldp);
            [PreserveSig] int Remove(int i);
            [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGELISTDRAWPARAMS
        {
            public int cbSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SYSICONINDEX = 0x4000;     // Get the icon index for image list
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        // Shell image list sizes
        private const int SHIL_JUMBO = 0x4;       // 256x256
        private const int SHIL_EXTRALARGE = 0x2;  // 48x48
        private const int ILD_TRANSPARENT = 0x1;

        public static BitmapImage GetIconSource(string appName)
        {
            CreateAppDirectories();

            // PRIORITY 1: Check for custom icon
            string customIconPath = UserPreferences.GetCustomIconPath(appName);
            if (!string.IsNullOrEmpty(customIconPath) && File.Exists(customIconPath))
            {
                try
                {
                    BitmapImage img = new BitmapImage();
                    img.UriSource = new Uri(customIconPath);
                    return img;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CUSTOM ICON - FAILED to load: {ex}");
                }
            }

            // PRIORITY 2: Try to get cached image
            BitmapImage cachedImage = GetCachedImage(appName);
            if (cachedImage != null)
            {
                return cachedImage;
            }

            // PRIORITY 3: Extract from running process
            try
            {
                Process[] processes = Process.GetProcessesByName(appName);

                if (processes.Length > 0)
                {
                    string filePath = "";
                    try 
                    {
                        filePath = processes[0].MainModule.FileName;
                    } 
                    catch 
                    {
                        // Some system processes access denied
                        return null; 
                    }

                    Debug.WriteLine($"Extracting icon for: {filePath}");

                    using (Icon icon = GetIcon(filePath))
                    {
                        if (icon != null)
                        {
                            using (Bitmap bmp = icon.ToBitmap())
                            {
                                CacheImage(bmp, appName);
                                return GetCachedImage(appName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ICON - FAILED to extract or cache: {ex}");
            }

            return null;
        }

        /// <summary>
        /// Extracts a high-resolution icon from the given file path.
        /// Fallback chain: SHIL_JUMBO (256x256) → SHIL_EXTRALARGE (48x48) → SHGFI_LARGEICON (32x32).
        /// </summary>
        private static Icon GetIcon(string filePath)
        {
            // Step 1: Get the icon index from the system image list
            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr result = SHGetFileInfo(filePath, FILE_ATTRIBUTE_NORMAL, ref shinfo,
                (uint)Marshal.SizeOf(shinfo), SHGFI_SYSICONINDEX);

            if (result == IntPtr.Zero)
            {
                return null;
            }

            int iconIndex = shinfo.iIcon;

            // Step 2: Try SHIL_JUMBO (256x256) first, then SHIL_EXTRALARGE (48x48)
            int[] sizes = { SHIL_JUMBO, SHIL_EXTRALARGE };
            Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

            foreach (int size in sizes)
            {
                try
                {
                    IImageList imgList = null;
                    int hr = SHGetImageList(size, ref iidImageList, ref imgList);
                    if (hr == 0 && imgList != null)
                    {
                        IntPtr hIcon = IntPtr.Zero;
                        hr = imgList.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon);
                        if (hr == 0 && hIcon != IntPtr.Zero)
                        {
                            Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
                            DestroyIcon(hIcon);
                            Debug.WriteLine($"ICON - Extracted at SHIL={size} for: {filePath}");
                            return icon;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ICON - SHIL={size} failed: {ex.Message}");
                }
            }

            // Step 3: Fallback to SHGFI_LARGEICON (32x32)
            Debug.WriteLine($"ICON - Falling back to SHGFI_LARGEICON for: {filePath}");
            shinfo = new SHFILEINFO();
            result = SHGetFileInfo(filePath, FILE_ATTRIBUTE_NORMAL, ref shinfo,
                (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);

            if (result != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                Icon fallbackIcon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
                DestroyIcon(shinfo.hIcon);
                return fallbackIcon;
            }

            return null;
        }

        private static void CreateAppDirectories()
        {
            Directory.CreateDirectory(ApplicationPath.GetImageCacheLocation());
        }

        private static void CacheImage(Bitmap bitmap, string appName)
        {
            try
            {
                string filePath = Path.Combine(ApplicationPath.GetImageCacheLocation(), $"{appName}.png");
                bitmap.Save(filePath, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CACHE - FAILED: {ex}");
            }
        }

        private static BitmapImage GetCachedImage(string appName)
        {
            try
            {
                string filePath = Path.Combine(ApplicationPath.GetImageCacheLocation(), $"{appName}.png");
                if (File.Exists(filePath))
                {
                    BitmapImage img = new BitmapImage();
                    // In WinUI 3, we should set UriSource. 
                    // Use absolute URI "file:///"
                    img.UriSource = new Uri(filePath); 
                    return img;
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CACHE - NOT FOUND: {ex}");
                return null;
            }
        }

        public static bool ClearCachedImages()
        {
            // Assuming StorageManager is available or using Directory.Delete
            try {
                if (Directory.Exists(ApplicationPath.GetImageCacheLocation()))
                {
                    Directory.Delete(ApplicationPath.GetImageCacheLocation(), true);
                    return true;
                }
            } catch {}
            return false;
        }

        /// <summary>
        /// Copies a user-selected icon file to the CustomIcons directory and returns the new path.
        /// </summary>
        public static string CopyCustomIcon(string sourceFilePath, string processName)
        {
            try
            {
                // Create CustomIcons directory if it doesn't exist
                string customIconsDir = ApplicationPath.GetCustomIconsLocation();
                Directory.CreateDirectory(customIconsDir);

                // Always save as PNG for consistency
                string destFileName = $"{processName}.png";
                string destFilePath = Path.Combine(customIconsDir, destFileName);

                // Load and convert the source image
                using (var sourceImage = Image.FromFile(sourceFilePath))
                {
                    // Save as PNG
                    sourceImage.Save(destFilePath, ImageFormat.Png);
                }

                Debug.WriteLine($"Custom icon copied: {sourceFilePath} -> {destFilePath}");
                return destFilePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CUSTOM ICON - COPY FAILED: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Deletes a custom icon file for the specified process.
        /// </summary>
        public static bool DeleteCustomIcon(string processName)
        {
            try
            {
                string customIconsDir = ApplicationPath.GetCustomIconsLocation();
                string iconPath = Path.Combine(customIconsDir, $"{processName}.png");

                if (File.Exists(iconPath))
                {
                    File.Delete(iconPath);
                    Debug.WriteLine($"Custom icon deleted: {iconPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CUSTOM ICON - DELETE FAILED: {ex}");
            }
            return false;
        }
    }
}
