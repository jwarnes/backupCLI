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
            Console.Title = "Backup Monitor";

            if (File.Exists(@"config.xml"))
                new Monitor().Start();
            else
                Console.WriteLine("No configuration file found!");


            Console.ReadKey();
        }



    }
}
