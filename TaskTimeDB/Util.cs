using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TaskTimeDB
{
    static class Util
    {
        static public string rootPath = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);

        public static readonly string reWord = @"[\w\+\-\.\@\:\+\*\(\)_ !\?&@・（）、。,/]+";
        public static Regex RegexLog = new Regex($@"^({reWord})\t+({reWord})\t+({reWord})\t+({reWord})\t+({reWord})\t+({reWord})\t+(\d+)$", RegexOptions.Compiled);
    }
}
