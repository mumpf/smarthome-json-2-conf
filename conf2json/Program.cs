using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace conf2json {
    class Program {
        static StringBuilder mOut = new StringBuilder();

        static void Main(string[] args) {
            if (args.Length < 1) {
                Console.Out.WriteLine("Usage: conf2json <conf-Filename>");
            } else {
                var lDirName = args[0];
                var lDirCurrent = Directory.GetCurrentDirectory();
                var lFullDir = Path.Combine(lDirCurrent, lDirName);
                var lDir = new DirectoryInfo(lFullDir);
                foreach (var lFile in lDir.EnumerateFiles("*.conf")) {
                    ConvertConf2Json(lFile.FullName);
                }
            }
        }

        private static void ConvertConf2Json(string iFileName) {
            StreamReader lFile = File.OpenText(iFileName);
            conf2json lConverter = new conf2json(lFile);
            string lOut = lConverter.Convert();
            var lOutFileName = Path.ChangeExtension(iFileName, "json");
            Debug.WriteLine(iFileName);
            Debug.WriteLine(lOut.ToString());
            Debug.WriteLine("");
            File.WriteAllText(lOutFileName, lOut.ToString());
        }
    }
}
