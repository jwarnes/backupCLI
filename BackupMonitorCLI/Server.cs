using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BackupMonitor
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

        #region Folder List Methods
        public void AddFolder(Folder folder)
        {
            folders.Add(folder);
        }

        public void RemoveFolder(Folder folder)
        {
            folders.Remove(folder);
        }

        public void ChangeFolder(Folder folder)
        {
            int i = 0;
            foreach (Folder f in folders)
            {
                if (f.Equals(folder))
                {
                    folders[i] = folder;
                    break;
                }
                i++;
            }
        }
        #endregion

    }
}
