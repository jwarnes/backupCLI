using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BackupMonitorCLI
{
    public enum SpaceType
    {
        GB, Percent, MB, TB
    }

    public class Server
    {
        #region Fields & Constructors
        public string Name { get; set; }
        public double Space { get; set; }
        public SpaceType spaceType { get; set; }

        //CLE only fields
        public bool UpdatedToday { get; set; }
        public bool LowOnSpace { get; set; }
        public bool NoUpdates { get; set; }
        public DateTime LastUpdate { get; set; }

        public List<DriveInfo> Drives { get; private set; }

        private List<Folder> folders;

        public List<Folder> Folders
        {
            get { return folders; }
        }

        public Server()
        {
            folders = new List<Folder>();
        }

        public Server(string name, double space, SpaceType type)
        {
            Name = name;
            this.Space = space;
            this.spaceType = type;

            folders = new List<Folder>();
        }

        #endregion

        public void GenerateDriveList()
        {
            Drives = new List<DriveInfo>();

            if(folders.Count < 1)
                return;
            foreach (var f in folders)
            {
                var drive = new DriveInfo(Path.GetPathRoot(f.Path));
                if(Drives.Count(s => s.Name == drive.Name) < 1)
                    Drives.Add(drive);
            }
        }

        #region Folder List Methods
        public void AddFolder(Folder folder)
        {
            folders.Add(folder);
        }

        public void RemoveFolder(Folder folder)
        {
            folders.Remove(folder);
        }

        public void ChangeFolder(int index, Folder folder)
        {
            folders[index] = folder;
        }
        #endregion

    }
}
