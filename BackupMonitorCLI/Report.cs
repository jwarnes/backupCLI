using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.CSharp;

namespace BackupMonitorCLI
{
    public class Report
    {
        public List<String> Addresses { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public bool Mailed { get; set; }
        public Importance importance { get; set; }
        private Server server;
        public Server Server
        {
            get { return server; }
            set { server = value; }
        }

        public Report(Server server)
        {
            Addresses = new List<string>();
            this.server = server;
        }

        public Report()
        {

        }

        public void PrintToConsole()
        {
            Console.WriteLine("===============Report===============");
            Console.WriteLine("\tName: {0}", server.Name);
            if (server.UpdatedToday)
                Console.WriteLine("\tBackup: Backup completed within the last 24 hours");
            else if(!server.NoUpdates)
                Console.WriteLine("\tBackup: Last recorded backup was {0} days ago", (DateTime.Now - server.LastUpdate).Days);
            else
                Console.WriteLine("\tBackup: No backup files found!");
        }

        public string GenerateEmailSubject()
        {

            if (server.UpdatedToday && !server.LowOnSpace)
                return string.Format("{0} on {1} - Backup OK!", server.Name, DateTime.Now.ToShortDateString());
            else
            {
                var subject = string.Format("{0} on {1} - ", server.Name, DateTime.Now.ToShortDateString());
                subject += (server.UpdatedToday) ? "Backup OK! " : "Backup FAILED! ";
                subject += (server.LowOnSpace) ? "LOW SPACE WARNING! " : "";
                return subject;
            }


        }

        public string GenerateEmailBody()
        {

            var body = string.Format("Report for server '{0}', generated {1}<br>", server.Name,
                                     DateTime.Now.ToString("g"));
            if (!server.UpdatedToday)
                body += "<b>**Backup FAILED!**</b><br>";
            if (server.LowOnSpace)
                body += "<b>**Low Space Warning!**</b><br><br>";

            body += (!server.NoUpdates)
                        ? string.Format("Last Backup: {0}<br>", server.LastUpdate.ToString("g"))
                        : "No updates found!<br>";
            body += "Disk Capacity: <ul>";
            foreach (var drive in server.Drives)
            {
                var freeSpace = (double)drive.AvailableFreeSpace / 1073741824;
                var totalSpace = (double)drive.TotalSize / 1073741824;

                body += string.Format("<li>{0} {1}/{2}Gb ({3}%)</li>",
                                      drive.Name,
                                      Math.Round(freeSpace, 1),
                                      Math.Round(totalSpace, 1),
                                      Math.Round((freeSpace/totalSpace)*100, 1));
            }
            body += "</ul>";
            return body;

        }

        public string GenerateHtmlEmail(string path = @"report.htm")
        {
            var body = File.ReadAllText(path)
                               .Replace("#serverName", server.Name)
                               .Replace("#reportDate", DateTime.Now.ToString("g"));
            var lastBackup = (server.NoUpdates) ? "No backups found!" : server.LastUpdate.ToString("g");
            body = body.Replace("#lastBackup", lastBackup);
            
            var warningSpace = (server.LowOnSpace) ? "<tr><td colspan=2 class=\"warning\">**Low Space Warning!**</td></tr>": String.Empty;
            var warningFailure = (!server.UpdatedToday) ? "<tr><td colspan=2 class=\"warning\">**Backup FAILED!**</td></tr>" : String.Empty;

            body = body.Replace("#warningSpace", warningSpace).Replace("#warningFailure", warningFailure);

            var driveString = "";
            foreach (var drive in server.Drives)
            {
                var freeSpace = (double) drive.AvailableFreeSpace/1073741824;
                var totalSpace = (double) drive.TotalSize/1073741824;

                driveString += string.Format("<tr><td class=\"{4}\">{0}</td><td class=\"right\">{1}/{2}Gb ({3}%)</td></tr>",
                                      drive.Name,
                                      Math.Round(freeSpace, 1),
                                      Math.Round(totalSpace, 1),
                                      Math.Round((freeSpace/totalSpace)*100, 1),
                                      drive.Type);
            }
            if (server.Drives.Count < 1)
                driveString = "<tr><td class=\"warning\">Drives not found!</td><td class=\"right\">&nbsp;</td></tr>";
            body = body.Replace("#drives", driveString);

            return body;

        }

        public void GenerateImportance()
        {
            this.importance = (server.LowOnSpace || !server.UpdatedToday) ? Importance.High : Importance.Normal;
        }

    }
}
