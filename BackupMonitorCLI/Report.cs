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
            else if(server.LastUpdate > 0)
                Console.WriteLine("\tBackup: Last recorded backup was {0} days ago", server.LastUpdate);
            else
                Console.WriteLine("\tBackup: No backup files found!");



        }

    }
}
