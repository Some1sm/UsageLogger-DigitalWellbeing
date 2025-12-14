using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using DigitalWellbeing.Core;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DigitalWellbeingWinUI3.Helpers
{
    public static class IconManager
    {
        // --- P/Invoke declarations ---
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

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
        private const uint SHGFI_LARGEICON = 0x0;    // 'Large icon
        private const uint SHGFI_SMALLICON = 0x1;    // 'Small icon
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

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
                    // Fall through to cached/extracted icon
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

            return null; // Return null if failed, UI should handle fallback
        }

        private static Icon GetIcon(string filePath)
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr hImgSmall = SHGetFileInfo(filePath, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);

            if (hImgSmall == IntPtr.Zero)
            {
                return null;
            }

            Icon icon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
            DestroyIcon(shinfo.hIcon);
            return icon;
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
