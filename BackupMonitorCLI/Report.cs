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

        public Report()
        {
            Addresses = new List<string>();
        }

        public void PrintToConsole()
        {
            Console.WriteLine("===============Report===============");
        }

    }
}
