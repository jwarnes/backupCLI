using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BackupMonitorCLI
{
    public class Folder
    {
        private string path;

        public string Path
        {
            get { return path; }
            set { Path = value; }
        }

        public bool RecurseSubdirectories { get; set; }

        public Folder(string path, bool recurse)
        {
            this.path = path;
            RecurseSubdirectories = recurse;
        }
    }
}
