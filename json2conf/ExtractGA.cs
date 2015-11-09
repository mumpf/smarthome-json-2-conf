using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace json2conf {
    class ExtractGA {
        public SortedDictionary<string, List<string>> GA = new SortedDictionary<string, List<string>>(new Util());

        private void ExtractEtsGA(string iFileName) {
            //string lFileName = Path.Combine(iDirName, "0.xml");
            XElement xElement = XElement.Load(iFileName);
            var lXmlns = xElement.Attribute("xmlns").Value;
            foreach (var lElement in xElement.Descendants("{" + lXmlns + "}GroupAddress")) {
                var lGa = OutputGA(lElement.Attribute("Address").Value);
                var lName = lElement.Attribute("Name").Value;
                //Debug.WriteLine(string.Format("{0} - {1}", lGa, lName));
                var lList = new List<string>();
                lList.Add(lName);
                GA.Add(lGa, lList);
            }
        }

        private string OutputGA(string iGaDezimal) {
            int lGaDezimal = Convert.ToInt16(iGaDezimal);
            return string.Format("{0}/{1}/{2}", (lGaDezimal & 0x7800) >> 11, (lGaDezimal & 0x700) >> 8, lGaDezimal & 0xff);
        }

        private void ExtractFileFromZip(string iZipFileName, string iEntryName, string iExtractionTarget) {
            using (ZipArchive zip = ZipFile.Open(iZipFileName, ZipArchiveMode.Read))
                foreach (ZipArchiveEntry entry in zip.Entries)
                    if (entry.Name == iEntryName) {
                        entry.ExtractToFile(iExtractionTarget, true);
                        File.SetLastAccessTimeUtc(iExtractionTarget, DateTime.UtcNow);
                    }
        }

        public bool ExtractEtsProject(string iEtsProjectFileName) {
            //check if ets project file is given
            if (Path.GetExtension(iEtsProjectFileName) == ".knxproj") {
                //get temp file name
                string lTempPath = Path.Combine(Path.GetTempPath(), "json2conf");
                string lTempFileName = Path.Combine(lTempPath, Path.GetFileNameWithoutExtension(iEtsProjectFileName) + "-0.xml");
                if (!File.Exists(lTempFileName) || File.GetLastAccessTimeUtc(iEtsProjectFileName) > File.GetLastAccessTimeUtc(lTempFileName)) {
                    if (!Directory.Exists(lTempPath)) Directory.CreateDirectory(lTempPath);
                    //We have a knxproj file, which is newer than out cached GA file
                    //We extract the newer GA file from the knxproj file
                    ExtractFileFromZip(iEtsProjectFileName, "0.xml", lTempFileName);
                }
                ExtractEtsGA(lTempFileName);
                return true;
            }
            return false;
        }
    }
}
