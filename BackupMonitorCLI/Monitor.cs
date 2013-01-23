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
            CheckSpace();
            CheckQueuedReports();
            GenerateReports();
            MailReports();
            End();
        }

        private void End()
        {
        }

        private void LoadConfiguration(string path = @"config.xml")
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

        private void ScanFolders()
        {
            foreach (Server s in servers)
            {
                bool updatedToday = false;
                Console.WriteLine("\nScanning folders for '{0}'...", s.Name);
                foreach (Folder f in s.Folders)
                {
                    Console.Write("\t{0}: ", f.Path);
                    
                    //get a list of files from the directory
                    SearchOption recurse = (f.RecurseSubdirectories)
                                               ? SearchOption.AllDirectories
                                               : SearchOption.TopDirectoryOnly;

                    var directory = new DirectoryInfo(f.Path);
                    var files = directory.GetFiles("*.tib", recurse).OrderByDescending(w => w.LastWriteTime);

                    if (files.Any())
                    {
                        s.LastUpdate = (DateTime.Now - files.First().LastWriteTime).Days;
                        Console.WriteLine("updated {0} days ago", s.LastUpdate);
                    }
                    else
                    {
                        Console.WriteLine("no backups found");
                        s.LastUpdate = -1;
                    }

                    if (files.Any() && (DateTime.Now - files.First().LastWriteTime).Hours <= 24)
                        s.UpdatedToday = true;
                }
                
            }
        }

        private void CheckSpace()
        {
            foreach (Server s in servers)
            {
                Console.WriteLine("\nChecking space for '{0}'...", s.Name);
                s.LowOnSpace = false;
                foreach (Folder f in s.Folders)
                {
                    //var rootPath = ;
                    var drive = new DriveInfo(Path.GetPathRoot(f.Path));
                    var freeSpace = (double)drive.AvailableFreeSpace/1073741824;

                    double spaceWarning = s.Space;

                    //determine acceptible space remaining levels
                    switch (s.spaceType)
                    {
                          case SpaceType.TB:
                            spaceWarning *= 1024;
                            break;
                          case SpaceType.MB:
                            spaceWarning /= 1024;
                            break;
                          case SpaceType.Percent:
                            spaceWarning = ((double)drive.TotalSize/1073741824)*(s.Space/100);
                            break;
                    }

                    Console.WriteLine("\t {0} {1}gb available", drive.Name, Math.Round(freeSpace, 1));
                    if (freeSpace < spaceWarning)
                    {
                        Console.WriteLine("\t\tWARNING: Low space threshhold of {0}gb exceeded", Math.Round(spaceWarning, 1));
                        s.LowOnSpace = true;
                    }
                }
            }
        }

        private void CheckQueuedReports()
        {
        }

        private void GenerateReports()
        {

        }

        private void MailReports()
        {

        }

        private void SaveReports()
        {
            
        }

        private void LoadReports()
        {
            
        }
    }
}
