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

            new Monitor().Start(ClaParser.GetArgs(args));


            Console.ReadKey();
        }



    }
}
