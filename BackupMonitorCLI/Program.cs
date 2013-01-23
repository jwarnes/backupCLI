using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BackupMonitorCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("================");
            Console.WriteLine(" Backup Monitor");
            Console.WriteLine("================\n");
            
            if (!File.Exists(@"config.xml"))
            {
                Console.WriteLine("No configuration file found!");
                Environment.Exit(1);
            }

            Monitor monitor = new Monitor();
            monitor.Start();


            Console.ReadKey();
        }



    }
}
