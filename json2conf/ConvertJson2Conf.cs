using System;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;

namespace json2conf {
    class ConvertJson2Conf {
        private StringBuilder mConf;
        private Dictionary<string, string> mNameLookup;
        private Dictionary<string, string> mItemLookup;
        private JToken mRoot;
        private string mCurrentFileName;

        private void Initialize(JObject iJson, string iFileName) {
            mConf = new StringBuilder();
            mNameLookup = new Dictionary<string, string>();
            mItemLookup = new Dictionary<string, string>();
            mRoot = iJson.Root;
            mCurrentFileName = iFileName;
            this.ResolveTemplates(iJson, iFileName, "");
            this.Preprocess(iJson);
            this.Conversion(iJson);
            this.ReplaceNames();
            this.ReplaceWidgetIds();
        }

        public ConvertJson2Conf(FileInfo iFile) {
            JObject lJson = Util.ReadJsonFromFile(iFile.FullName);
            Initialize(lJson, iFile.FullName);
            this.Output(iFile.FullName);
        }

        private void ReplaceNames() {
            foreach (var lPair in mNameLookup) {
                mConf.Replace(lPair.Key, lPair.Value);
            }
        }

        private void ReplaceWidgetIds() {
            string lOut = mConf.ToString();
            int lCount = 0;
            lOut = Regex.Replace(lOut, @"\.\(#\)", delegate (Match match) {
                //string v = match.ToString();
                lCount++;
                return ".auto" + lCount.ToString();
            });
            mConf.Clear();
            mConf.AppendLine(lOut);
        }

        private void ResolveTemplates(JObject iJson, string iFileName, string iKnxReplace) {
            //evaluate template
            JProperty lTemplate = null;
            string lFileName = iFileName;
            string lKnxReplace = iKnxReplace;
            while ((lTemplate = iJson.Property("$template")) != null) {
                JObject lTemplateValue = lTemplate.Value as JObject;
                lFileName = lTemplateValue.Property("source").Value.ToString();
                lFileName = Path.Combine(Path.GetDirectoryName(iFileName), "..", "templates", lFileName + ".json");
                JProperty lKnx = lTemplateValue.Property("knx");
                if (lKnx != null) {
                    lKnxReplace = lKnx.Value.ToString();
                }
                try {
                    if (lKnxReplace == "") {
                        var lConvert = new Merge(lFileName, iJson);
                    } else {
                        var lConvert = new Merge(lFileName, iJson, lKnxReplace);
                    }
                } catch (FileNotFoundException) {
                    Util.gMessages.AppendLine(string.Format("Template '{0}' nicht gefunden, Aufrufstelle '{1}'", Util.FileNameToString(lFileName), Util.FileNameToString(iFileName)));
                    iJson.Remove("$template");
                    break;
                }
                //iJson.Remove("$template");
                //Debug.WriteLine( iJson.ToString());
            };
            foreach (var lProperty in iJson.Properties()) {
                if (lProperty.Value.Type == JTokenType.Object) {
                    var lPropertyName = MapProperyName(lProperty.Name);
                    if (lPropertyName.Substring(0, 1) == lPropertyName.Substring(0, 1).ToUpper()) {
                        JObject lTarget = lProperty.Value as JObject;
                        this.ResolveTemplates(lTarget, lFileName, lKnxReplace);
                        //delete can be just evaluated afterwards on subobject, otherwise it would change the collection
                        JProperty lDelete = lTarget.Property("$delete");
                        if (lDelete != null) {
                            foreach (var lToken in lDelete.Value.Children()) {
                                // we parse the current token and check for array notation
                                string[] lAddress = lToken.ToString().Split(new char[] { '[', ',', ']' }, StringSplitOptions.RemoveEmptyEntries);
                                if (lAddress.Length == 1) {
                                    lTarget.Remove(lToken.ToString());
                                } else {
                                    //evaluate array notation and delete according entry
                                    var lArray = lTarget.Property(lAddress[0]).Value;
                                    for (int i = 1; i < lAddress.Length; i++) {
                                        if (lArray.Type == JTokenType.Array) {
                                            var lEntry = (lArray as JArray)[int.Parse(lAddress[i])];
                                            if (i == lAddress.Length - 1) {
                                                lEntry.Remove();
                                            } else {
                                                lArray = lEntry;
                                            }
                                        }
                                    }
                                }
                            }
                            lTarget.Remove("$delete");
                        }
                        JProperty lOverride = lTarget.Property("$override");
                        if (lOverride != null) lTarget.Remove("$override");
                    }
                }
            }
        }

        private static Dictionary<string, string> cWeekdays = new Dictionary<string, string>() { { "monday", "0" }, { "tuesday", "1" }, { "wednesday", "2" }, { "thursday", "3" }, { "friday", "4" }, { "saturday", "5" }, { "sunday", "6" } };

        private bool IsCustomProp(string iName) {
            return (iName == "autotimer" || iName == "crontab");
        }

        private string ConvertCustomProp(JProperty iProperty, JObject iValue) {
            string lValue = "";
            if (iProperty.Name == "autotimer") {
                JObject lAutotimer = iValue;
                if (lAutotimer.Property("seconds") != null) {
                    if (lAutotimer.Property("minutes") == null) {
                        lValue = lAutotimer.Property("seconds").Value.ToString();
                    } else {
                        lValue = (Convert.ToInt32(lAutotimer.Property("minutes").Value) * 60 + Convert.ToInt32(lAutotimer.Property("seconds").Value)).ToString();
                    }
                } else if (lAutotimer.Property("minutes") != null) {
                    lValue = lAutotimer.Property("minutes").Value.ToString() + "m";
                }
                lValue += " = " + lAutotimer.Property("value").Value.ToString();
            } else if (iProperty.Name == "crontab") {
                JObject lCrontab = iValue;
                if (lCrontab != null) {
                    lValue = CrontabValue(lCrontab.Property("minute"));
                    lValue += CrontabValue(lCrontab.Property("hour"));
                    lValue += CrontabValue(lCrontab.Property("day"));
                    JProperty lProperty = lCrontab.Property("weekday");
                    if (lProperty == null) {
                        lValue += "* ";
                    } else if (lProperty.Value.Type == JTokenType.Array) {
                        //int[] lWeekdays = new int[lProperty.Value.Count()];
                        string lSeparator = "";
                        foreach (var lV in lProperty.Value) {
                            lValue += lSeparator + cWeekdays[lV.ToString().ToLower()];
                            lSeparator = ",";
                        }
                        lValue += " ";
                    } else {
                        lValue += cWeekdays[lCrontab.Property("weekday").Value.ToString().ToLower()] + " ";
                    }
                    if (lCrontab.Property("value") != null) lValue += "= " + lCrontab.Property("value").Value.ToString();
                }
            }
            return lValue;
        }

        private void ConvertCustomProp(JProperty iProperty, int iLevel) {
            this.OutputSimpleProperty(iLevel, iProperty, true, ConvertCustomProp(iProperty, iProperty.Value as JObject));
        }

        private static string CrontabValue(JProperty iProperty) {
            string lValue;
            if (iProperty == null) {
                lValue = "*";
            } else if (iProperty.Value.Type == JTokenType.Array) {
                lValue = string.Join<JToken>(",", iProperty.Value.ToArray());
            } else {
                lValue = iProperty.Value.ToString();
            }
            return lValue + " ";
        }

        private void Preprocess(JObject iJson) {
            //after the introduction of autoblind with ist special handling (autoBlind is an item, but starts with a lowercase letter)
            //we have to preprocess the model to adapt corrections
            //This has to happen as an preprocessing step, the first try to do it "in between" failed due to complexity
            foreach (var lProperty in iJson.Properties().ToArray()) {
                if (lProperty.Value.Type == JTokenType.Object) {
                    Preprocess(lProperty.Value as JObject);
                    var lNewName = MapProperyName(lProperty.Name);
                    if (lProperty.Name != lNewName) {
                        lProperty.Replace(new JProperty(lNewName, lProperty.Value));
                    }
                }
            }
        }

        private void Conversion(JObject iJson, int iLevel = 1) {
            //smarthome conf format requires the output of all simple properties per level before we step down
            //to the next level. Json (especially with templates) has an unordered structure, so we need a 2 pass approach:
            //1st pass, we serialize all simple properties, ...
            foreach (var lProperty in iJson.Properties()) {
                if (lProperty.Value.Type == JTokenType.Object) {
                    if (IsCustomProp(lProperty.Name)) {
                        ConvertCustomProp(lProperty, iLevel);
                    } else {
                        JObject lObject = lProperty.Value as JObject;
                        string lValue = ConvertMultiline(lProperty, lObject);
                        if (lValue != null) this.OutputSimpleProperty(iLevel, lProperty, true, lValue);
                    }
                } else if (lProperty.Value.Type == JTokenType.Array) {

                    OutputPropertyName(lProperty, iLevel, false);
                    JArray lArray = lProperty.Value as JArray;
                    bool lAppend = false;
                    for (int i = 0; i < lArray.Count; i++) {
                        if (lAppend) mConf.Append(" | ");
                        lAppend = false;
                        string lValue = null;
                        switch (lArray[i].Type) {
                            case JTokenType.Object:
                                lValue = ConvertMultiline(lProperty, lArray[i] as JObject);
                                break;
                            case JTokenType.Array:
                                lValue = ConvertMultiline(lArray[i] as JArray);
                                break;
                            case JTokenType.String:
                                lValue = lArray[i].ToString();
                                break;
                            default:
                                break;
                        }
                        if (lValue != null && lValue.Length > 0) {
                            OutputPropertyValue(lProperty, false, lValue);
                            lAppend = true;
                        }
                    }
                    mConf.AppendLine();
                } else if (lProperty.Name == "description") {
                    OutputComment(iLevel, lProperty.Value.ToString());
                } else {
                    if (lProperty.Name != "$schema" && lProperty.Name != "$templated") {
                        OutputSimpleProperty(iLevel, lProperty, true, null, (lProperty.Name == "$order"));
                    }
                }
            }
            //now we sort all object properties
            var lList = iJson.Properties().ToList();
            var lQuery = iJson.Properties().OrderBy(p => {
                int lResult = 10000;
                if (p.Value.Type == JTokenType.Object) {
                    JObject lObj = p.Value as JObject;
                    if (lObj.Property("$order") != null) {
                        lResult = (int)lObj.Property("$order").Value;
                        //lObj.Remove("$order");
                    }
                }
                return lResult;
            });
            //2nd pass: now all objects
            foreach (var lProperty in lQuery) // iJson.Properties())
            {
                if (lProperty.Value.Type == JTokenType.Object) {
                    string lPropertyName = MapProperyName(lProperty.Name);
                    if (!IsCustomProp(lPropertyName)) {
                        {
                            mConf.Append(Convert.ToChar(9), iLevel - 1);
                            mConf.Append('[', iLevel);
                            mConf.Append(lPropertyName);
                            mConf.Append(']', iLevel);
                            mConf.AppendLine();
                            string lPath = lProperty.Path;
                            lPath = lPath.Replace("autoBlind", "AutoBlind");
                            if (!Util.gExistingItems.ContainsKey(lPath)) {
                                Util.gExistingItems.Add(lPath, "");
                            }
                            //we try to ensure existence of all autoblind as_item_* references
                            if (lPropertyName == "AutoBlind") CheckAutoblind(lProperty);
                            this.Conversion(lProperty.Value as JObject, iLevel + 1);
                        }
                    }
                }
            }
        }

        private void CheckAutoblind(JProperty iProperty) {
            //get all dependent properties belonging to AutoBlind namespace (starting with as_*)
            var lDescendants = iProperty.Descendants().OfType<JProperty>().Where(p => p.Name.StartsWith("as_"));
            //create lookup with defined item names (as_item_([A-Z].*)) 
            var lLookup = lDescendants.Where(p => p.Name.StartsWith("as_item_")).Select(p => p.Name.Substring(8));
            //now we check existence
            Regex searchTerm = new Regex(@"as_[a-z]*_([A-Z].*)");
            foreach (var lProp in lDescendants) {
                var lGroups = searchTerm.Match(lProp.Name).Groups;
                if (lGroups.Count > 1 && !lLookup.Contains(lGroups[1].Value)) {
                    Util.gMessages.AppendLine(string.Format("AutoBlind reference not found: as_item_{0}, referenced in File: {1}, Path: {2}", lGroups[1].Value, Util.FileNameToString(mCurrentFileName), lProp.Path));
                }
            }
        }

        private string ConvertMultiline(JProperty iProperty, JObject iObject) {
            JProperty lMultiline = iObject.Property("multiline");
            string lValue = null;
            if (IsCustomProp(iProperty.Name)) {
                lValue = ConvertCustomProp(iProperty, iObject);
            } else if (lMultiline != null) {
                JArray lArray = lMultiline.Value as JArray;
                lValue = ConvertMultiline(lArray);
            }
            return lValue;
        }

        private static string ConvertMultiline(JArray lArray) {
            string lValue;
            StringBuilder lConf = new StringBuilder();
            foreach (var lItem in lArray) {
                string lLine = lItem.ToString().Trim();
                lConf.Append(lLine);
            }
            lValue = lConf.ToString();
            return lValue;
        }

        private void OutputSimpleProperty(int iLevel, JProperty iProperty, bool iNewLine = false, string iValue = null, bool iAsComment = false) {
            OutputPropertyName(iProperty, iLevel, iAsComment);
            OutputPropertyValue(iProperty, iNewLine, iValue);
        }

        private void OutputPropertyValue(JProperty iProperty, bool iNewLine = false, string iValue = null) {
            string lPropValue = null;
            if (iProperty.Value.Type == JTokenType.Float)
                lPropValue = ((float)iProperty.Value).ToString("F", CultureInfo.InvariantCulture);
            else
                lPropValue = iProperty.Value.ToString();
            string lValue = (iValue == null) ? lPropValue : iValue;
            if (lValue.Contains("~")) lValue = ParseRelativePath(iProperty, lValue);
            ParseItemReference(iProperty, lValue);
            mConf.Append(lValue);
            if (iNewLine) mConf.AppendLine();
            if (iProperty.Name == "name") mNameLookup.Add(iProperty.Path, lValue);
            //manage knx GA lookup
            if (!lValue.Contains("-")) {
                if (iProperty.Name.StartsWith("knx_") && iProperty.Name != "knx_dpt") {
                    Util.GroupAddressLookupAdd(lValue, iProperty.Path);
                }

            }
        }

        private void ParseItemReference(JProperty iProperty, string iValue) {
            string[] lItems = null;
            //just specific properties allow item references
            if (iProperty.Name == "eval_trigger" || iProperty.Name == "as_value_laststate") {
                lItems = iValue.Split('|');
            } else if (iProperty.Name == "eval" || (iProperty.Name.StartsWith("as_") && iValue.StartsWith("eval:"))) {
                MatchCollection matches = Regex.Matches(iValue, @"sh\.([a-zA-Z\.]*)\(\)");
                // Here we check the Match instance.
                if (matches.Count > 0) {
                    lItems = new string[matches.Count];
                    for (int lCount = 0; lCount < matches.Count; lCount++) {
                        //we have to get rid of sh functions, which are not items
                        //they have always the pattern .<lowercase>()
                        string lMatch = matches[lCount].ToString();
                        //string lItem = RemoveLowercaseFunction(lMatch, @"sh(.*)\.[a-z]*\(\)", matches[lCount].Groups[1].ToString());
                        string lItem = RemoveLowercaseFunction(lMatch, @"sh((\.[A-Z][A-Za-z]*)*)", matches[lCount].Groups[1].ToString());
                        if (lItem != null && lItem != "") lItems[lCount] = lItem;
                    }
                }
            } else if (iProperty.Name.StartsWith("as_") && iValue.StartsWith("item:")) {
                lItems = new string[1];
                lItems[0] = iValue.Substring(5);
            } else if (iProperty.Name.StartsWith("as_item_")) {
                lItems = new string[1];
                string lValue = RemoveLowercaseFunction(iValue, @"(.*)\.[a-z][a-zA-Z_]*");
                lItems[0] = lValue;
            } else if (iProperty.Name == "sv_widget") {
                MatchCollection matches = Regex.Matches(iValue, @"'(([A-Z][a-zA-Z_0-9]*)\.)+([A-Z][a-zA-Z_0-9]*)['|\.]");
                // Here we check the Match instance.
                if (matches.Count > 0) {
                    lItems = new string[matches.Count];
                    for (int lCount = 0; lCount < matches.Count; lCount++) {
                        string lMatch = matches[lCount].ToString().Trim('\'', '.');
                        lItems[lCount] = lMatch;
                    }
                }
            }
            if (lItems != null) {
                foreach (var lItem in lItems) {
                    if (lItem != null) {
                        string lValue = lItem.Trim();
                        lValue = lValue.TrimEnd('*', '.');
                        Util.gReferencedItems.Add(new ItemRefrence(lValue, mCurrentFileName, iProperty.Path));
                    }
                }
            }
        }

        private static string RemoveLowercaseFunction(string iItem, string iRegex, string iDefaultResult = null) {
            MatchCollection lCheck = Regex.Matches(iItem, iRegex);
            if (iDefaultResult == null) iDefaultResult = iItem;
            string lItem = null;
            if (lCheck.Count == 0) {
                lItem = iDefaultResult;
            } else if (lCheck.Count == 1) {
                //we want to get rid of the sh function
                lItem = lCheck[0].Groups[1].ToString().Trim('.');
            } else throw new Exception("unexpected item reference format");
            return lItem;
        }

        private string ParseRelativePath(JProperty iProperty, string iValue) {
            iValue = iValue.Replace("icon0~", "icon0°");
            iValue = iValue.Replace("icon1~", "icon1°");
            iValue = MapPropertyPath(iValue);
            for (int lPathLength = 10; lPathLength > 0; lPathLength--) {
                var lReplace = new string('~', lPathLength);
                int lPos = iValue.IndexOf(lReplace);
                if (lPos >= 0) {
                    var lProperty = iProperty;
                    for (int lIteration = 0; lIteration < lPathLength && lProperty != null; lIteration++) {
                        lProperty = lProperty.Parent.Parent as JProperty;
                    }
                    if (lProperty == null) lProperty = new JProperty("Unknown", null);
                    CheckItemExistence(lProperty, iValue, lReplace, iProperty);
                    string lPath = lProperty.Path;
                    if (iProperty.Name == "eval") {
                        iValue = iValue.Replace("'" + lReplace, "'" + lPath);
                        iValue = iValue.Replace(lReplace, "sh." + lPath);
                    } else {
                        iValue = iValue.Replace(lReplace, lPath);
                    }
                }
            }
            iValue = iValue.Replace("icon1°", "icon1~");
            iValue = iValue.Replace("icon0°", "icon0~");
            return iValue;
        }

        private void CheckItemExistence(JProperty iProperty, string iValue, string iReplace, JProperty iProcessedProperty) {
            string lReplace = iReplace + @"(\.([A-Z]\w*))+";
            MatchCollection matches = Regex.Matches(iValue, lReplace);
            // Here we check the Match instance.
            if (matches.Count > 0) {
                foreach (var lMatch in matches) {
                    string lPath = lMatch.ToString().Replace(iReplace, iProperty.Path);
                    if (!lPath.EndsWith(".age")) {
                        try {
                            var lToken = this.mRoot.SelectToken(lPath, true);

                        } catch (Exception) {
                            Util.gReferencedItems.Add(new ItemRefrence(lMatch.ToString(), mCurrentFileName, iProcessedProperty.Path));
                            //Debug.Fail(string.Format("\rItem {1} not found, \rrelative path is {0}, \rprocessed Item is {2}", lMatch, lPath, iProperty.Path));
                        }
                    }
                    //Debug.WriteLine("Path: {0}, Token: {1}", lPath, lToken);
                }
            }

        }

        private void OutputPropertyName(JProperty iProperty, int iLevel, bool iAsComment) {
            string lPropertyName = MapProperyName(iProperty.Name);
            mConf.Append(Convert.ToChar(9), iLevel - 1);
            if (iAsComment) mConf.Append("#");
            mConf.Append(lPropertyName);
            mConf.Append(" = ");
        }

        private void OutputComment(int iLevel, string iComment) {
            mConf.Append(Convert.ToChar(9), iLevel - 1);
            mConf.Append("#");
            mConf.AppendLine(iComment);
        }

        private string MapProperyName(string iName) {
            string lResult = iName;
            if (iName == "autoBlind") lResult = "AutoBlind";
            return lResult;
        }

        private string MapPropertyPath(string iPath) {
            string lResult = iPath;
            string[] lPropNames = iPath.Split('.');
            for (int i = 0; i < lPropNames.Length; i++) {
                lPropNames[i] = MapProperyName(lPropNames[i]);
            }
            lResult = string.Join(".", lPropNames);
            return lResult;
        }

        private void Output(string iFileName) {
            var lFileName = Path.ChangeExtension(iFileName, "conf");
            //Debug.WriteLine(lFileName);
            //Debug.WriteLine(mConf.ToString());
            //Debug.WriteLine("");
            File.WriteAllText(lFileName, mConf.ToString());
        }
    }
}
