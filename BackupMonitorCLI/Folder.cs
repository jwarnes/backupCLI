using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BackupMonitor
{
    public class Folder
    {
        private string path;
        private bool recurse;

        public string Path
        {
            get { return path; }
            set { Path = value; }
        }

        public bool RecurseSubdirectories
        {
            get { return recurse; }
            set { recurse = value; }
        }
        
        public Folder(string path, bool recurse)
        {
            this.path = path;
            this.recurse = recurse;
        }
    }
}
