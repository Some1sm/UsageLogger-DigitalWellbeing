using System;
using System.Runtime.InteropServices;

namespace DigitalWellbeingWinUI3.Helpers
{
    /// <summary>
    /// Native folder picker using COM IFileOpenDialog.
    /// More reliable than WinRT FolderPicker for unpackaged WinUI 3 apps.
    /// </summary>
    public class NativeFolderDialog
    {
        [DllImport("shell32.dll")]
        private static extern int SHILCreateFromPath([MarshalAs(UnmanagedType.LPWStr)] string pszPath, out IntPtr ppIdl, ref uint rgflnOut);

        [DllImport("shell32.dll")]
        private static extern int SHCreateItemFromIDList(IntPtr pidl, ref Guid riid, out IShellItem ppv);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pv);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialog { }

        [ComImport]
        [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr hwndOwner);
            void SetFileTypes();
            void SetFileTypeIndex();
            void GetFileTypeIndex();
            void Advise();
            void Unadvise();
            void SetOptions(uint fos);
            void GetOptions(out uint fos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int alignment);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults();
            void GetSelectedItems();
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler();
            void GetParent();
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes();
            void Compare();
        }

        private const uint FOS_PICKFOLDERS = 0x20;
        private const uint FOS_FORCEFILESYSTEM = 0x40;
        private const uint FOS_NOCHANGEDIR = 0x8;
        private const uint SIGDN_FILESYSPATH = 0x80058000;

        public string ShowDialog(IntPtr hwndOwner, string initialFolder = null)
        {
            IFileOpenDialog dialog = null;
            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialog();
                dialog.SetOptions(FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_NOCHANGEDIR);
                dialog.SetTitle("Select Log Folder");

                // Set initial folder if provided
                if (!string.IsNullOrEmpty(initialFolder) && System.IO.Directory.Exists(initialFolder))
                {
                    try
                    {
                        uint rgflnOut = 0;
                        if (SHILCreateFromPath(initialFolder, out IntPtr pidl, ref rgflnOut) == 0)
                        {
                            Guid shellItemGuid = typeof(IShellItem).GUID;
                            if (SHCreateItemFromIDList(pidl, ref shellItemGuid, out IShellItem folderItem) == 0)
                            {
                                dialog.SetFolder(folderItem);
                            }
                            CoTaskMemFree(pidl);
                        }
                    }
                    catch { }
                }

                int hr = dialog.Show(hwndOwner);
                if (hr == 0) // S_OK
                {
                    dialog.GetResult(out IShellItem result);
                    result.GetDisplayName(SIGDN_FILESYSPATH, out string path);
                    return path;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NativeFolderDialog error: {ex.Message}");
            }
            finally
            {
                if (dialog != null)
                    Marshal.ReleaseComObject(dialog);
            }
            return null;
        }
    }
}
