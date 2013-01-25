using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Mail;

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
                s.UpdatedToday = false;
                s.NoUpdates = true;

                Console.WriteLine("\nScanning folders for '{0}'...", s.Name);
                foreach (Folder f in s.Folders)
                {
                    Console.Write("\t{0}: ", f.Path);
                    
                    //get a list of files from the directory
                    SearchOption recurse = (f.RecurseSubdirectories)
                                               ? SearchOption.AllDirectories
                                               : SearchOption.TopDirectoryOnly;

                    var directory = new DirectoryInfo(f.Path);

                    //returns a List<FileInfo> of *.tib files sorted by creation date
                    var files = directory.GetFiles("*.tib", recurse).OrderByDescending(w => w.LastWriteTime);

                    if (files.Any())
                    {
                        if (files.First().LastWriteTime > s.LastUpdate)
                            s.LastUpdate = files.First().LastWriteTime;
                        Console.WriteLine("updated {0} days ago", (DateTime.Now - files.First().LastWriteTime).Days);
                        s.NoUpdates = false;
                    }
                    else
                    {
                        Console.WriteLine("no backups found");
                    }

                    var temp = (DateTime.Now - s.LastUpdate).Hours;

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
                    var drive = new DriveInfo(Path.GetPathRoot(f.Path));
                    s.FreeSpace = (double)drive.AvailableFreeSpace/1073741824;
                    s.TotalSpace = (double) drive.TotalSize/1073741824;

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
                            spaceWarning = (s.TotalSpace)*(s.Space/100);
                            break;
                    }

                    Console.WriteLine("\t {0} {1}gb available", drive.Name, Math.Round(s.FreeSpace, 1));
                    if (s.FreeSpace < spaceWarning)
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
            foreach (var s in servers)
            {
                reports.Add(new Report(s));
            }
        }

        private void MailReports()
        {
            var fromAddress = new MailAddress("backupmonitorreport@gmail.com", "Backup Report");
            var toAddress = new MailAddress("JWarnes@samaritan.org", "Justin Warnes");
            const string password = "testPassword1";

            var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 465,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, password)
                };

            foreach (var r in reports)
            {
                var message = new MailMessage(fromAddress, toAddress)
                    {
                        Subject = r.EmailSubject,
                        Body = r.EmailBody
                    };
                //smtp.Send(message);
                Console.WriteLine("\n\n");
                Console.WriteLine(message.Subject);
                Console.WriteLine(message.Body);
                Console.WriteLine("\n\n");
            }
            

        }

        private void SaveReports()
        {
            //todo: method saves the reoprts to disk
        }

        private void LoadReports()
        {
            //todo: method reads the reports from disk
        }
    }
}
