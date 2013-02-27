using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;
using System.IO;

namespace BackupMonitorCLI
{
    public class CDriveInfo
    {
        [DllImport("shlwapi.dll")]
        private static extern bool PathIsNetworkPath(string pszPath);

        public long AvailableFreeSpace { get; set; }
        public long TotalSize { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }

        public CDriveInfo(string path)
        {
            //use different components depending on whether the folder is a local or networked location
            if (!PathIsNetworkPath(path))
            {
                //local drive
                var drive = new DriveInfo(Path.GetPathRoot(path));
                this.Name = drive.Name;
                this.AvailableFreeSpace = drive.AvailableFreeSpace;
                this.TotalSize = drive.TotalSize;
                this.Type = "drive";
            }
            else
            {
                //network share
                if (!path.EndsWith("\\"))
                    path += "\\";

                this.Name = path;

                long freeSpace = 0, totalSpace = 0, empty = 0;
                GetDiskFreeSpaceEx(path, ref freeSpace, ref totalSpace, ref empty);

                this.AvailableFreeSpace = freeSpace;
                this.TotalSize = totalSpace;
                this.Type = "share";
            }
        }

        #region Interop externals for network drive
        public static long NetFreeSpace(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (!folderName.EndsWith("\\"))
            {
                folderName += '\\';
            }

            long free = 0, dummy1 = 0, dummy2 = 0;

            if (GetDiskFreeSpaceEx(folderName, ref free, ref dummy1, ref dummy2))
            {
                return free;
            }
            else
            {
                return -1;
            }
        }

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage"), SuppressUnmanagedCodeSecurity]
        [DllImport("Kernel32", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx
        (
            string lpszPath,                  
            ref long lpFreeBytesAvailable,
            ref long lpTotalNumberOfBytes,
            ref long lpTotalNumberOfFreeBytes
        );

        #endregion
    }
}
