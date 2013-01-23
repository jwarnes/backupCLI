using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace BackupMonitorCLI
{
    public class Monitor
    {
        private List<Server> servers;
        private List<Report> reports;

        private string mailString;
        private string defaultMail;

        public Monitor()
        {
        }

        public void Start()
        {
            servers = new List<Server>();
            reports = new List<Report>();
            LoadConfiguration();
            ScanFolders();
        }

        public void LoadConfiguration(string path = @"config.xml")
        {
            Console.Write("Loading {0}...", path);
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            settings.IgnoreWhitespace = true;

            XmlReader r = XmlReader.Create(path, settings);

            r.ReadToDescendant("Mail");
            defaultMail = r["default"];
            mailString = r.ReadString().Replace(" ", string.Empty);

            r.ReadToFollowing("Servers");
            r.ReadToDescendant("Server");
            do
            {
                Server server = new Server();
                server.Name = r["name"];
                server.Space = Convert.ToDouble(r["spaceValue"]);
                server.spaceType = (SpaceType)Convert.ToInt16(r["spaceType"]);

                //folders loop

                r.ReadToDescendant("Folder");
                do
                {
                    Folder folder = new Folder(r["path"], Convert.ToBoolean(Convert.ToInt16(r["recurse"])));
                    server.AddFolder(folder);
                } while (r.ReadToNextSibling("Folder"));


                servers.Add(server);
            } while (r.ReadToNextSibling("Server"));

            r.Close();

            Console.WriteLine(" {0} servers found\n", servers.Count);

        }

        public void ScanFolders()
        {
            foreach (Server s in servers)
            {
                bool updatedToday = false;
                Console.WriteLine("Scanning folders for '{0}'...", s.Name);
                foreach (Folder f in s.Folders)
                {
                    Console.Write("\t{0}: ", f.Path);
                    
                    //get a list of files from the directory
                    SearchOption recurse = (f.RecurseSubdirectories)
                                               ? SearchOption.AllDirectories
                                               : SearchOption.TopDirectoryOnly;

                    var directory = new DirectoryInfo(f.Path);
                    var files = directory.GetFiles("*.tib", recurse).OrderByDescending(w => w.LastWriteTime);

                    if(files.Any())
                        Console.WriteLine("updated {0} days ago", (DateTime.Now - files.First().LastWriteTime).Days);
                    else
                        Console.WriteLine("no backups found");

                    if (files.Any() && (DateTime.Now - files.First().LastWriteTime).Hours <= 24)
                    {
                        updatedToday = true;
                    }
                   
                }
                
            }
        }

    }
}
