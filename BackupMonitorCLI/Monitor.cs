using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using Microsoft.Exchange.WebServices;
using Microsoft.Exchange.WebServices.Data;


namespace BackupMonitorCLI
{
    public class Monitor
    {
        private List<Server> servers;
        private List<Report> reports;
        private List<Report> unsent;

        private const int MaxAttempts = 4;

        private string mailString;
        private string defaultMail;

        public Monitor()
        {
        }

        public void Start()
        {
            servers = new List<Server>();
            reports = new List<Report>();
            unsent = new List<Report>();
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
            //save unsent reports to disk
            if(unsent.Count > 0)
                SaveReports(unsent);

            Console.WriteLine("Backup reporting complete.");

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

                    var temp = (DateTime.Now - s.LastUpdate).TotalHours;

                    if (files.Any() && (DateTime.Now - files.First().LastWriteTime).TotalHours <= 24)
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
            if (File.Exists("queue.txt"))
            {
                LoadReports();
            }
        }

        private void GenerateReports()
        {
            foreach (var s in servers)
            {
                var r = new Report(s);
                r.Subject = r.GenerateEmailSubject();
                r.Body = r.GenerateEmailBody();
                r.GenerateImportance();
                reports.Add(r);
            }

        }

        private void MailReports()
        {

            //save reports early in case the program doesn't get through a lengthy mailing list
            SaveReports(reports);

            Console.WriteLine("\nMailing reports...\n");

            //configure exchange server
            ExchangeService mailService = new ExchangeService();
            mailService.AutodiscoverUrl("jwarnes@samaritan.org");

            int reportNum = 0;
            foreach(var r in reports)
            {
                reportNum++;
                var message = new EmailMessage(mailService);
                message.Subject = r.Subject;
                message.Body = r.Body;
                message.Body.BodyType = BodyType.HTML;
                message.ToRecipients.Add("jwarnes@gmail.com");
                message.Importance = r.importance;
                
                message.Save();
               
                //attempt to mail
                int attempts = 1;
                do
                {
                    try
                    {
                        Console.WriteLine("\tSending report {0}/{1}, attempt {2}", reportNum, reports.Count, attempts);
                        //Console.WriteLine("{0}\n{1}\n", message.Subject, message.Body);
                        message.SendAndSaveCopy();
                        r.Mailed = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("\tSend Failed: {0}\n\t{1}", ex.GetType().ToString(), ex.Message );
                    }
                    attempts++;
                } while (attempts < MaxAttempts + 1 && !r.Mailed);

                //save any unsent messages
                if(!r.Mailed)
                    unsent.Add(r);
            }
            DeleteSavedReports();
         
        }

        private void SaveReports(List<Report> reports, string path = @"queue.txt")
        {
            var settings = new XmlWriterSettings();
            settings.IndentChars = "    ";
            
            settings.Indent = true;

            var w = XmlWriter.Create(path, settings);
            w.WriteStartDocument();
            w.WriteStartElement("Reports");

            foreach (var report in reports)
            {
                w.WriteStartElement("Report");
                {
                    w.WriteAttributeString("subject", report.Subject);
                    w.WriteAttributeString("body", report.Body.Replace("\t", "#T#").Replace("\n", "#NEW#"));
                    w.WriteAttributeString("importance", ((int)report.importance).ToString());
                }
                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndDocument();
            w.Close();
        }

        private void LoadReports(string path = @"queue.txt")
        {
            var settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            settings.IgnoreWhitespace = true;

            var r = XmlReader.Create(path, settings);

            r.ReadToDescendant("Reports");
            r.ReadToDescendant("Report");
            do
            {
                var report = new Report();
                report.Subject = r["subject"];
                report.Body = r["body"].Replace("#T#", "\t").Replace("#NEW#", "\n");
                report.importance = (Importance)Convert.ToInt16(r["importance"]);
                reports.Add(report);

            } while (r.ReadToNextSibling("Report"));

            r.Close();
        }

        private void DeleteSavedReports(string path = @"queue.txt")
        {
            File.Delete(path);
        }
    }
}
