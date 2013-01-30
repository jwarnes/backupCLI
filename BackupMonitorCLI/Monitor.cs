using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Exchange.WebServices.Autodiscover;
using System.Security;
using System.Runtime.InteropServices;


namespace BackupMonitorCLI
{
    public class Monitor
    {

        #region Fields
        private List<Server> servers;
        private List<Report> reports;
        private List<Report> unsent;

        private const int MaxAttempts = 4;
        private string user;
        private SecureString password;

        private List<string> recipients;

        public Monitor()
        {
        }
        #endregion
        
        #region Program Flow 
        public void Start()
        {
            servers = new List<Server>();
            reports = new List<Report>();
            unsent = new List<Report>();
            recipients = new List<string>();

            //get user exchange credentials
            Console.Write("Username: ");
            user = Console.ReadLine();
            password = PromptForPassword();

            //program flow
            LoadConfiguration();
            ScanFolders();
            CheckSpace();
            CheckQueuedReports();
            GenerateReports();
            MailReports(); 
            End();
        }

        private void LoadConfiguration(string path = @"config.xml")
        {
            Console.Write("Loading {0}...", path);
            var settings = new XmlReaderSettings() {IgnoreComments =  true, IgnoreWhitespace = true};

            XmlReader r = XmlReader.Create(path, settings);

            r.ReadToDescendant("Mail");
            recipients.Add(r["default"]);

            recipients.AddRange(r["recipients"].Replace(" ", string.Empty).Split(','));

            r.ReadToFollowing("Servers");
            r.ReadToDescendant("Server");
            do
            {
                var server = new Server
                    {
                        Name = r["name"],
                        Space = Convert.ToDouble(r["spaceValue"]),
                        spaceType = (SpaceType) Convert.ToInt16(r["spaceType"])
                    };

                //folders loop

                r.ReadToDescendant("Folder");
                do
                {
                    var folder = new Folder(r["path"], Convert.ToBoolean(Convert.ToInt16(r["recurse"])));
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
            foreach (var s in servers)
            {
                Console.WriteLine("\nChecking space for '{0}'...", s.Name);
                s.LowOnSpace = false;
                s.GenerateDriveList();
                foreach (var drive in s.Drives)
                {
                    var freeSpace = (double)drive.AvailableFreeSpace/1073741824;
                    var totalSpace = (double) drive.TotalSize/1073741824;

                    //spaceWarning represents the minimum amount of free space configured for the server before a warning is issued
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
                            spaceWarning = (totalSpace)*(s.Space/100);
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
            if (File.Exists("queue.xml"))
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
                r.Body = r.GenerateHtmlEmail();
                r.GenerateImportance();
                reports.Add(r);
            }

        }

        private void MailReports()
        {

            //save reports early in case the program doesn't get through a lengthy mailing list
            SaveReports(reports);

            var mailService = new ExchangeService(ExchangeVersion.Exchange2010_SP2);
            mailService.Credentials = new WebCredentials(user, Marshal.PtrToStringBSTR(Marshal.SecureStringToBSTR(password)));
            
            //attempt to connect to exchange server
            Console.WriteLine("\nConnecting to exchange server...");
            int attempts = 1;
            bool connected;
            do
            {
                try
                {
                    //mailService.AutodiscoverUrl(user + "@samaritan.org", redirect => true);
                    mailService.Url = new Uri("https://spex11.samaritan.org/EWS/Exchange.asmx");
                    connected = true;
                }
                catch (AutodiscoverLocalException ex)
                {
                    connected = false;
                    Console.WriteLine("Attempt {0}/{1}  failed. \n\t{2}\nRetrying connection...", attempts, MaxAttempts,
                                      ex.Message);
                }
                catch (AutodiscoverRemoteException ex)
                {
                    connected = false;
                    Console.WriteLine("Credentials invalid. \n\tEx:{0}", ex.Message);
                    break;

                }
                catch (ServiceLocalException ex)
                {
                    Console.WriteLine("Credentials invalid. \n\tEx:{0}", ex.Message);
                    connected = false;
                    break;
                }

                attempts++;
            } while (!connected && attempts <= MaxAttempts);

            if (!connected)
            {
                Console.WriteLine("Couldn't connect to exchange server.");
                return;
            }


            Console.WriteLine("Mailing reports...\n");
            int reportNum = 0;
            foreach (var r in reports)
            {
                reportNum++;

                //create message
                var message = new EmailMessage(mailService) {Subject = r.Subject, Body = r.Body};
                message.Body.BodyType = BodyType.HTML;

                foreach (var recipient in recipients)
                    message.ToRecipients.Add(recipient);

                message.Importance = r.importance;

                //see if the server accepts the credentials
                try
                {
                    message.Save();
                }
                catch (ServiceRequestException)
                {
                    Console.WriteLine("Server did not accept credentials.");
                    break;
                }

                //attempt to mail
                attempts = 1;
                do
                {
                    try
                    {
                        Console.WriteLine("\tSending report {0}/{1}, attempt {2}", reportNum, reports.Count, attempts);
                        message.SendAndSaveCopy();
                        r.Mailed = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("\tSend Failed: {0}\n\t{1}", ex.GetType().ToString(), ex.Message );
                    }
                    attempts++;
                } while (attempts <= MaxAttempts  && !r.Mailed);
            }
            DeleteSavedReports();
         
        }

        private void End()
        {
            //save unsent reports to disk
            foreach (var report in reports)
            {
                if(!report.Mailed)
                    unsent.Add(report);
            }
            if (unsent.Count > 0)
            {
                SaveReports(unsent);
                Console.WriteLine("Unsaved reports saved to disk.");
            }

            Console.WriteLine("\nBackup reporting complete.");

        }

        #endregion

        #region Persistant Data

        private void SaveReports(List<Report> reports, string path = @"queue.xml")
        {

            var w = XmlWriter.Create(path, new XmlWriterSettings() { Indent = true, IndentChars = "    " });
            w.WriteStartDocument();
            w.WriteStartElement("Reports");

            foreach (var report in reports)
            {
                w.WriteStartElement("Report");
                {
                    w.WriteAttributeString("subject", report.Subject);
                    w.WriteAttributeString("body", report.Body.Replace("\r", "#R#").Replace("\n", "#NEW#"));
                    w.WriteAttributeString("importance", ((int)report.importance).ToString());
                }
                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndDocument();
            w.Close();
        }

        private void LoadReports(string path = @"queue.xml")
        {

            var r = XmlReader.Create(path, new XmlReaderSettings() { IgnoreComments = true, IgnoreWhitespace = true });

            r.ReadToDescendant("Reports");
            r.ReadToDescendant("Report");
            do
            {
                var report = new Report();
                report.Subject = r["subject"];
                report.Body = r["body"].Replace("#R#", "\r").Replace("#NEW#", "\n");
                report.importance = (Importance)Convert.ToInt16(r["importance"]);
                reports.Add(report);

            } while (r.ReadToNextSibling("Report"));

            r.Close();
        }

        private void DeleteSavedReports(string path = @"queue.xml")
        {
            File.Delete(path);
        }
        #endregion

        #region User Prompts
        public SecureString PromptForPassword()
        {
            Console.Write("Password: ");
            ConsoleKeyInfo key;
            var securePass = new SecureString();
            do
            {
                key = Console.ReadKey(true);

                if (char.IsSymbol(key.KeyChar) || char.IsLetterOrDigit(key.KeyChar) || char.IsPunctuation(key.KeyChar) || key.Key == ConsoleKey.Spacebar)
                {
                    securePass.AppendChar(key.KeyChar);
                    Console.Write("*");
                }
                if (key.Key == ConsoleKey.Backspace && securePass.Length > 0)
                {
                    Console.Write("\b \b");
                    securePass.RemoveAt(securePass.Length-1);
                }
            } while (key.Key != ConsoleKey.Enter);
            Console.WriteLine();

            return securePass;
        }
        #endregion
    }
}
