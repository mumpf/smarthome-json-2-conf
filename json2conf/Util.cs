using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace json2conf {
    public struct ItemRefrence {
        public string Item;
        public string FileName;
        public string JsonPath;

        public ItemRefrence(string iItem, string iFileName, string iPath) {
            Item = iItem;
            FileName = Util.FileNameToString(iFileName);
            JsonPath = iPath;
        }
    }

    class Util : IComparer<string> {
        //hier stehen alle gefundenen GA drin
        public static SortedDictionary<string, List<string>> gGroupAddressLookup = new SortedDictionary<string, List<string>>(new Util());
        //hier stehen alle Items (aufgelöst, also path), die irgendwo als Refernz verwendet werden, mit ihrer json-Source-Info (filename, line)
        public static List<ItemRefrence> gReferencedItems = new List<ItemRefrence>();
        //hier stehen alle jemals definierten Items (path) mit ihrer json-source-info (filename, line)
        public static Dictionary<string, string> gExistingItems = new Dictionary<string, string>();
        //hier stehen alle jemals in AutoBlind definierten as_item_*, mit ihrer json-Source-Info(filename, line)
        public static Dictionary<string, string> gAutoblindItems = new Dictionary<string, string>();
        //hier stehen alle jemals in AutoBlind benutzten as_(value|min|max|agemin|agemax)_*, mit ihrer json-Source-Info(filename, line)
        public static List<ItemRefrence> gAutoblindUsage = new List<ItemRefrence>();
        //alle zur Laufzeit gesammelten messages
        public static StringBuilder gMessages = new StringBuilder();

        public static JObject ReadJsonFromFile(string iFileName) {
            // read JSON directly from a file
            JObject lObject = null;
            using (StreamReader file = File.OpenText(iFileName))
            using (JsonTextReader reader = new JsonTextReader(file)) {
                lObject = (JObject)JToken.ReadFrom(reader);

            }
            return lObject;
        }

        public int Compare(string x, string y) {
            string[] lx = x.Split('/');
            string[] ly = y.Split('/');
            for (int i = 0; i < 3; i++) {
                int lCompare = Convert.ToInt16(lx[i]).CompareTo(Convert.ToInt16(ly[i]));
                if (lCompare != 0) return lCompare;
            }
            return 0;
        }

        public static void GroupAddressLookupAdd(string iKey, string iValue) {
            string[] lKeys;
            if (iKey.Contains("|")) {
                lKeys = iKey.Split('|');
            } else {
                lKeys = new string[] { iKey};
            }
            //in value, get rid of ".knx_*"
            int lPos = iValue.LastIndexOf(".knx_");
            if (lPos > 0) iValue = iValue.Substring(0, lPos);
            foreach (var lKey in lKeys) {
                if (!gGroupAddressLookup.ContainsKey(lKey)) {
                    Util.gGroupAddressLookup.Add(lKey, new List<string>());
                }
                if (!Util.gGroupAddressLookup[lKey].Contains(iValue)) Util.gGroupAddressLookup[lKey].Add(iValue);

            }
        }

        public static void OutputGA(string iFileName, SortedDictionary<string, List<string>> iGA) {
            //var lFileName = Path.Combine(iDirName, "Gruppenadressen.txt");

            //Debug.WriteLine(iFileName);
            StringBuilder lOut = new StringBuilder();
            foreach (var lItem in iGA) {
                StringBuilder lLine = new StringBuilder();
                lLine.AppendFormat("{0,8} - ", lItem.Key);
                bool lKomma = false;
                foreach (var lPath in lItem.Value) {
                    if (lKomma) {
                        lLine.Append(", ");
                    }
                    lKomma = true;
                    lLine.Append(lPath);
                }
                lOut.AppendLine(lLine.ToString());
            }
            //Debug.WriteLine(lOut.ToString());
            //Debug.WriteLine("");
            File.WriteAllText(iFileName, lOut.ToString());
        }

        public static string FileNameToString(string iFileName) {
            string lPath = Path.GetDirectoryName(iFileName);
            return Path.Combine(Path.GetFileName(lPath), Path.GetFileName(iFileName));
        }
    }
}
