using System.Collections.Generic;

namespace BackupMonitorCLI
{
    public static class ClaParser
    {
        static ClaParser()
        {
        }

        public static Dictionary<string, string> GetArgs(string[] args)
        {
            var dictionary = new Dictionary<string, string>();
            if (args.Length == 0) return dictionary;

            int i = 0;
            foreach (var a in args)
            {
                if (a.StartsWith("-"))
                {
                    dictionary.Add(a.TrimStart('-'), args[i+1]);
                }
                i++;
            }
            return dictionary;
        }
    }
}
