using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace json2conf {

    class Program {
        static void Main(string[] args) {
            if (args.Length < 1) {
                Console.Out.WriteLine("Usage: json2conf <Item-DirectoryName> [.knxproj-File]");
            } else {
                var lDirName = args[0];
                var lDirCurrent = Directory.GetCurrentDirectory();
                var lFullDir = Path.Combine(lDirCurrent, lDirName);
                var lDir = new DirectoryInfo(lFullDir);
                foreach (var lFile in lDir.EnumerateFiles("*.json")) {
                    var lWorker = new ConvertJson2Conf(lFile);
                }
                Util.OutputGA(Path.Combine(lFullDir, "GA", "Smarthome.txt"), Util.gGroupAddressLookup);
                if (args.Length == 2) {
                    var lEts4 = new ExtractGA();
                    if (lEts4.ExtractEtsProject(args[1])) {
                        Util.OutputGA(Path.Combine(lFullDir, "GA", "ETS.txt"), lEts4.GA);
                        var lDiff = new SortedDictionary<string, List<string>>(new Util());
                        foreach (var lEntry in Util.gGroupAddressLookup) {
                            if (!lEts4.GA.ContainsKey(lEntry.Key)) lDiff.Add(lEntry.Key, lEntry.Value);
                        }
                        Util.OutputGA(Path.Combine(lFullDir, "GA", "FehlenInETS.txt"), lDiff);
                        lDiff = new SortedDictionary<string, List<string>>(new Util());
                        foreach (var lEntry in lEts4.GA) {
                            if (!Util.gGroupAddressLookup.ContainsKey(lEntry.Key)) lDiff.Add(lEntry.Key, lEntry.Value);
                        }
                        Util.OutputGA(Path.Combine(lFullDir, "GA", "FehlenInSmarthome.txt"), lDiff);
                    }
                }
                //check if all referenced Items are avalilable
                Debug.WriteLine("");
                bool lFound = false;
                foreach (var lItem in Util.gReferencedItems) {
                    var lKey = lItem.Item;
                    lFound = false;
                    if (lKey.Contains("*")) {
                        lKey = lKey.Replace(".", @"\.");
                        lKey = lKey.Replace("*", @".*");
                        foreach (var lExisting in Util.gExistingItems.Keys) {
                            var lMatch = Regex.Match(lExisting, lKey);
                            if (lMatch.Length > 0) {
                                lFound = true;
                                break;
                            }
                        }
                    } else if (Util.gExistingItems.ContainsKey(lKey)) lFound = true;
                    if (!lFound) {
                        Util.gMessages.AppendLine(string.Format("Item not found: {0}, referenced in File: {1}, Path: {2}", lItem.Item, lItem.FileName, lItem.JsonPath));
                    }
                }
                string lMessage = Util.gMessages.ToString();

                File.WriteAllText(Path.Combine(lFullDir, "GA", "Messages.txt"), lMessage);
                Console.Out.WriteLine(lMessage);
                Debug.WriteLine("*** Messages ****************************************");
                Debug.WriteLine(lMessage);
            }
        }
    }
}
