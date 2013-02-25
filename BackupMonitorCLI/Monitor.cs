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
using System.Threading;
using System.Net.Mail;


namespace BackupMonitorCLI
{
    public class Monitor
    {

        #region Fields
        private List<Server> servers;
        private List<Report> reports;
        private List<Report> unsent;

        private const int MaxEmailAttempts = 4;
        private const int MaxEWSConnectAttempts = 60;
        private const int ReconnectWaitInterval = 600;
        private string user;
        private SecureString password;

        private List<string> recipients;

        public Monitor()
        {
        }
        #endregion

        public void Start(Dictionary<string, string> cla)
        {
            servers = new List<Server>();
            reports = new List<Report>();
            unsent = new List<Report>();
            recipients = new List<string>();

            var configPath = @"config.xml";
            if (cla.ContainsKey("config")) configPath = cla["config"];

            if (!File.Exists(configPath))
            {
                Console.WriteLine("No configuration file found! Run SPFMConfig.exe first.");
                return;
            }
            //load configuration data and exit if there are no servers in the file
            LoadConfiguration(configPath);
            if (servers.Count < 1)
            {
                Console.WriteLine("No servers configured.");
                return;
            }

            //get user's Exchange credentials
            if (cla.ContainsKey("user"))
                user = cla["user"];
            else
            {
                Console.Write("Username: ");
                user = Console.ReadLine();
            }

            if (cla.ContainsKey("password"))
            {
                password = new SecureString();
                foreach (var c in cla["password"])
                {
                    password.AppendChar(c);
                }
            }
            else password = PromptForPassword();
 

            //program flow
            CheckBackupFiles();
            CheckDiskSpace();

            CheckQueuedReports();
            GenerateReports();
            SaveReports(reports);

            //var mailService = ConnectToEws();
            MailReportsSMTP(); 

            End();
        }

        #region Disk Operations

        private void CheckBackupFiles()
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
                    if (!directory.Exists)
                    {
                        Console.WriteLine("Directory not found.");
                        continue;
                    }

                    var files = directory.GetFiles("*.tib", recurse).OrderByDescending(w => w.LastWriteTime);


                    //if we find a file, check how old it is
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

                    if (files.Any() && (DateTime.Now - files.First().LastWriteTime).TotalHours <= 24)
                        s.UpdatedToday = true;
                }
                
            }
        }

        private void CheckDiskSpace()
        {
            foreach (var s in servers)
            {
                Console.WriteLine("\nChecking space for '{0}'...", s.Name);
                s.LowOnSpace = false;
                if(!s.GenerateDriveList())
                    Console.WriteLine("No drives found!");
                foreach (var drive in s.Drives)
                {
                    var freeSpace = (double)drive.AvailableFreeSpace/1073741824;
                    var totalSpace = (double) drive.TotalSize/1073741824;

                    //spaceWarning represents the minimum amount of free space configured for this server before a warning is issued
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

        #endregion

        #region EWS and Mailing

        private ExchangeService ConnectToEws()
        {
            //instatiate service and create credentials
            var mailService = new ExchangeService(ExchangeVersion.Exchange2010_SP2);
            mailService.Credentials = new WebCredentials(user, Marshal.PtrToStringBSTR(Marshal.SecureStringToBSTR(password)), "SAMARITAN");

            //attempt to locate SP exchange server through autodiscover
            bool connected = false;
            int attempts = 1;
            do
            {
                try
                {
                    Console.WriteLine("Locating SP exchange server...");
                    mailService.AutodiscoverUrl(user + "@samaritan.org", redirect => true);
                    connected = true;
                }
                catch (Exception)
                {
                    //attempt to connect to hardcoded URL if the autodiscover fails
                    try
                    {
                        Console.WriteLine("Autodiscover failed. Connecting to last known URL...");
                        mailService.Url = new Uri("https://spex11.samaritan.org/EWS/Exchange.asmx");
                        mailService.GetInboxRules();
                        connected = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Connection failed. Attempting to reconnect in {0} minutes.\n", ReconnectWaitInterval/60000);
                        connected = false;
                    }
                }

                //wait and retry the connection in a few minutes
                if (!connected) Thread.Sleep(ReconnectWaitInterval);
                attempts++;
            } while (!connected && attempts < MaxEWSConnectAttempts);

            return mailService;

        }

        private void MailReportsExchange(ExchangeService mailService)
        {
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
                message.Save();

                //attempt to mail
                int attempts = 1;
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
                        Console.WriteLine("{0}", ex.Message );
                        Thread.Sleep(3000);
                    }
                    attempts++;
                } while (attempts <= MaxEmailAttempts  && !r.Mailed);
            }

            //clear out cache, we will resave it later if there are any reports in the queue that did not get mailed
            DeleteSavedReports();

        }


        private void MailReportsSMTP()
        {
            var client = new SmtpClient("smtp.1and1.com");

            Console.WriteLine("Mailing reports...\n");
            int reportNum = 0;
            foreach (var r in reports)
            {
                reportNum++;

                //create message
                var defaultAddress = new MailAddress("fieldalerts@spnetinfo.org");
                var message = new MailMessage(defaultAddress, new MailAddress("jwarnes@gmail.com"))
                    {
                        IsBodyHtml = true,
                        Body = r.Body
                    };

                foreach (var recipient in recipients)
                    message.CC.Add(new MailAddress(recipient));


                //attempt to mail
                int attempts = 1;
                do
                {
                    try
                    {
                        Console.WriteLine("\tSending report {0}/{1}, attempt {2}", reportNum, reports.Count, attempts);
                        client.Send(message);
                        r.Mailed = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("{0}", ex.Message);
                        Thread.Sleep(3000);
                    }
                    attempts++;
                } while (attempts <= MaxEmailAttempts && !r.Mailed);
            }

            //clear out cache, we will resave it later if there are any reports in the queue that did not get mailed
            DeleteSavedReports();

        }

        #endregion

        #region Persistant Data and Reports

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

        private void LoadConfiguration(string path = @"config.xml")
        {
            Console.Write("Loading {0}...", path);
            var settings = new XmlReaderSettings() {IgnoreComments =  true, IgnoreWhitespace = true};

            XmlReader r = XmlReader.Create(path, settings);

            r.ReadToDescendant("Mail");
            recipients.Add(r["default"]);

            foreach (var recipient in r["recipients"].Replace(" ", string.Empty).Split(','))
            {
                if(recipient != "")
                    recipients.Add(recipient);
            }

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


                if(server.Name != null && server.Folders.Count > 0) 
                    servers.Add(server);
            } while (r.ReadToNextSibling("Server"));

            r.Close();

            Console.WriteLine(" {0} servers found\n", servers.Count);

        }

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

    }
}
