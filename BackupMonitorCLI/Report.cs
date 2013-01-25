using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BackupMonitorCLI
{
    public class Report
    {
        public List<String> Addresses { get; set; }
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

        public string EmailSubject
        {
            get
            {
                
                if (server.UpdatedToday && !server.LowOnSpace)
                    return string.Format("Backup OK! '{0}' on {1}", server.Name, DateTime.Now.ToShortDateString());
                else
                {
                    var subject = (server.UpdatedToday) ? "Backup OK! " : "Backup FAILED!";
                    subject += (server.LowOnSpace) ? "LOW SPACE WARNING " : "";
                    subject += string.Format("'{0}' on {1}", server.Name, DateTime.Now.ToShortDateString());
                    return subject;
                }
                
            }
        }

        public string EmailBody
        {
            get
            {
                var body = string.Format("Report for server '{0}', generated {1}\n", server.Name,
                                            DateTime.Now.ToString("g"));
                if (!server.UpdatedToday)
                    body += "\t**Backup FAILED!**\n";
                if (server.LowOnSpace)
                    body += "\t**LOW SPACE WARNING!**\n";

                body += (!server.NoUpdates)
                            ? string.Format("\tLast Backup: {0}\n", server.LastUpdate.ToString("g"))
                            : "\tNo updates found!\n";
                body += string.Format("\tDisk Capacity: {0}/{1} ({2}%)", Math.Round(server.FreeSpace, 1), Math.Round(server.TotalSpace,1),
                                      Math.Round((server.FreeSpace/server.TotalSpace)*100, 1));

                return body;
            }
        }

    }
}
