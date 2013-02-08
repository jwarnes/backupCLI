using System;

namespace BackupMonitorCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Backup Monitor";

            new Monitor().Start(ClaParser.GetArgs(args));
        }



    }
}
