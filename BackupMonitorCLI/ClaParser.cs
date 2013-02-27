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
                    if (args.Length - 1 >= i + 1)
                        dictionary.Add(a.TrimStart('-'), args[i + 1]);
                    else
                        dictionary.Add(a.TrimStart('-'), "null");
                }
                i++;
            }
            return dictionary;
        }
    }
}
