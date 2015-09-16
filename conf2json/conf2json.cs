using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace conf2json {
    class conf2json {

        StreamReader mFile;
        JObjectComment mOut;

        public conf2json(StreamReader iFile) {
            mFile = iFile;
            mOut = new JObjectComment();
            mOut.Add("$schema", "../schema/ItemSchema.json");
        }

        public string Convert() {
            JObjectComment lCurrent = mOut;
            int lItemLevel = 0;
            //StringBuilder lComments = new StringBuilder();
            while (!mFile.EndOfStream) {
                string lLine = mFile.ReadLine().Trim();
                string lComment = "";
                if (lLine.Contains("#")) {
                    string[] lSplit = lLine.Split(new char[] { '#' }, 2);
                    lLine = lSplit[0].Trim();
                    lComment = lSplit[1];
                }
                MatchCollection lMatch = Regex.Matches(lLine, @"(\[+)([0-9a-zA-Z_]*)(\]+)");
                if (lMatch.Count == 0) {
                    if (lLine == "" && lComment != "") {
                        lCurrent.CommentAdd(lComment);
                    }
                    //normal property line, we need nothing special
                    if (lLine != "") {
                        OutputProperty(lCurrent, lLine, lComment);
                        //lComments = new StringBuilder();
                    }
                } else if (lMatch.Count == 1) {
                    int lLevel = lMatch[0].Groups[1].ToString().Length;
                    string lOut = lMatch[0].Groups[2].ToString();
                    lOut = lOut.Substring(0, 1).ToUpper() + lOut.Substring(1);
                    if (lItemLevel == lLevel - 1) {
                        //new level ist greater, we introduce a sublevel
                        JObjectComment lItem = new JObjectComment();
                        while (lCurrent.Property(lOut) != null) {
                            lItem.CommentAdd("Duplicate with " + lOut);
                            lOut += "X";
                        }
                        lCurrent.Add(lOut, lItem);
                        lCurrent = lItem;
                    } else if (lItemLevel > lLevel) {
                        //new level is lower, we iterate to same
                        //if (lKomma || mCommentCache != null) OutputComment(lItemLevel, false, false, false, lComment, true);
                        //lKomma = false;
                        for (; lItemLevel > lLevel; lItemLevel--) lCurrent = lCurrent.Parent.Parent as JObjectComment;
                    }
                    if (lItemLevel == lLevel) {
                        //new level is same, we close this level
                        JObjectComment lItem = new JObjectComment();
                        var lTarget = lCurrent.Parent.Parent as JObjectComment;
                        while (lTarget.Property(lOut) != null) {
                            lItem.CommentAdd("Duplicate with " + lOut);
                            lOut += "X";
                        }
                        lTarget.Add(lOut, lItem);
                        lCurrent = lItem;
                    }
                    lItemLevel = lLevel;
                } else throw new Exception("Multiple Items in one line");
            }
            return mOut.ToString();
        }

        private void OutputProperty(JObject iCurrent, string lLine, string iComment) {
            int lPos = lLine.IndexOf('=');
            bool lKomma = true;
            string lProperty = lLine.Substring(0, lPos).Trim();
            string lValue = lLine.Substring(lPos + 1).Trim();
            if (lProperty == "cache" || lProperty == "enforce_updates") {
                lValue = lValue.ToLower();
                AddProperty(iCurrent, lProperty, "1|yes|true|y|on".Contains(lValue), iComment);
            } else if (lProperty == "sqlite") {
                lValue = lValue.ToLower();
                lKomma = !"0|no|false|n|off".Contains(lValue);
                if (lKomma) {
                    AddProperty(iCurrent, lProperty, "1|yes|true|y|on".Contains(lValue) ? "yes" : "init", iComment);
                }
            } else if (lProperty == "knx_listen" && lValue.Contains("|")) {
                JArray lArray = new JArray();
                string[] lEntries = lValue.Split('|');
                foreach (var lEntry in lEntries) {
                    lArray.Add(lEntry.Trim());
                }
                AddProperty(iCurrent, lProperty, lArray, iComment);
            } else if (lProperty == "knx_dpt") {
                if (mDptMapping.ContainsKey(lValue)) lValue = mDptMapping[lValue];
                AddProperty(iCurrent, lProperty, lValue, iComment);
            } else if (lProperty == "autotimer") {
                string[] lEntries = lValue.Split('=');
                JObjectComment lAutotimer = new JObjectComment();
                string lTime = lEntries[0].Trim();
                if (lTime.EndsWith("m")) {
                    lAutotimer.Add("minutes", int.Parse(lTime.TrimEnd('m')));
                } else {
                    lAutotimer.Add("seconds", int.Parse(lTime));
                }
                lAutotimer.Add("value", lEntries[1].Trim());
                AddProperty(iCurrent, lProperty, lAutotimer, iComment);
            } else AddProperty(iCurrent,lProperty, lValue, iComment);
        }

        private void AddProperty(JObject iCurrent, string iProperty, JToken iToken, string iComment) {
            try {
                JPropertyComment lProperty = new JPropertyComment(iProperty, iToken, iComment);
                iCurrent.Add(lProperty);
            } catch (Exception) {

            }
            
        }

        private static Dictionary<string, string> mDptMapping = new Dictionary<string, string> {
            {"4002", "4.002"},
            {"5001", "5.001"},
            {"16000", "16"},
            {"16001", "16.001" }
        };

    }
}
