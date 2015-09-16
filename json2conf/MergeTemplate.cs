using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace json2conf {
    class Merge {

        public static string gKnxReplace = "-/-/-";
        public Merge(string iTemplateFileName, JObject iTargetObject) {
            JObject lTemplateJson = Util.ReadJsonFromFile(iTemplateFileName);
            MergeTemplate(iTargetObject, lTemplateJson);
        }

        private void MergeTemplate(JObject iTarget, JObject iTemplate) {
            //evaluate template
            JProperty lTemplate = iTarget.Property("$template");
            if (lTemplate != null) {
                JProperty lKnx = (lTemplate.Value as JObject).Property("knx");
                if (lKnx != null) gKnxReplace = lKnx.Value.ToString();
                JObject lTemplateObject = iTemplate.Properties().Last().Value as JObject;
                iTarget.Remove("$template");
                MergeTemplate(iTarget, lTemplateObject, gKnxReplace);
            }
        }

        private void MergeTemplate(JObject iTarget, JObject iTemplate, string iKnxReplace) {
            JProperty lOverride = iTarget.Property("$override");
            foreach (var lTemplateProperty in iTemplate.Properties()) {
                var lTargetProperty = iTarget.Property(lTemplateProperty.Name);
                if (lTargetProperty == null || lOverride == null || lOverride.Value.Where(t => t.ToString() == lTemplateProperty.Name).Count() == 0) {
                    if (lTemplateProperty.Value.Type == JTokenType.Object) {
                        //deep processing, all objects are parsed, all templates are replaced
                        bool lRemove = false;
                        if (lTargetProperty == null) {
                            //if there is no property in the target, a dummy will be created
                            iTarget.Add(lTemplateProperty.Name, new JObject(new JProperty("not-allowed-dummy-property", new JValue(false))));
                            lTargetProperty = iTarget.Property(lTemplateProperty.Name);
                            lRemove = true;
                        }
                        //else if ((lTargetProperty.Value as JObject).Property("delete") != null)
                        //{
                        //    iTarget.Remove(lTemplateProperty.Name);
                        //    lTargetProperty = null;
                        //}
                        if (lTargetProperty != null) {
                            MergeTemplate(lTargetProperty.Value as JObject, lTemplateProperty.Value as JObject, iKnxReplace);
                            //if there is a dummy, it will be deleted
                            if (lRemove) (lTargetProperty.Value as JObject).Remove("not-allowed-dummy-property");
                        }
                    } else if (lTemplateProperty.Value.Type == JTokenType.Array && lTargetProperty != null && lTargetProperty.Value.Type == JTokenType.Array) {
                        //merging arrays
                        if (lTargetProperty == null) {
                            iTarget.Add(lTemplateProperty.Name, lTemplateProperty.Value);
                        } else {
                            MergeTemplate(lTargetProperty.Value as JArray, lTemplateProperty.Value as JArray);
                        }
                    } else if (lTargetProperty == null) {
                        KnxMerge(lTemplateProperty, iKnxReplace);
                        iTarget.Add(lTemplateProperty.Name, lTemplateProperty.Value);
                    } 
                }
                //else if (lTargetProperty.Value.ToString() == "delete")
                //{
                //    iTarget.Remove(lTargetProperty.Name);
                //}
            }
        }

        /// <summary>
        /// Merge zweier Arrays
        /// </summary>
        /// <param name="iTarget"></param>
        /// <param name="iTemplate"></param>
        private void MergeTemplate(JArray iTarget, JArray iTemplate) {
            JsonMergeSettings lMergeSettings = new JsonMergeSettings();
            lMergeSettings.MergeArrayHandling = MergeArrayHandling.Union;
            if (iTemplate.First.Type == JTokenType.Array) {
                for (int i = 0; i < iTemplate.Count && i < iTarget.Count; i++) {
                    //(iTarget[i] as JArray).Merge(iTemplate[i], lMergeSettings);
                    //iTemplate[i].Remove();
                    (iTemplate[i] as JArray).Merge(iTarget[i], lMergeSettings);
                    iTarget[i].Remove();
                }
            }
            //iTarget.Merge(iTemplate, lMergeSettings);
            iTemplate.Merge(iTarget, lMergeSettings);
            iTarget.RemoveAll();
            iTarget.Add(iTemplate.Children());
        }

        private readonly char[] cPlusMinus = new char[] { '-', '+' };

        /// <summary>
        /// Merge einer ganzen Gruppenadresse
        /// </summary>
        /// <remarks>
        /// Erlaubt auch die Auswertung von relativen Gruppenadressen, ein +50 meint 50 aufaddieren, ein -50 entsprechend subtrahieren
        /// </remarks>
        /// <param name="iTemplateProperty">Property, dass eine Gruppenadresse enthält (beginnt mit knx)</param>
        /// <param name="iKnxReplace">Gruppenadresse, die ersetzt werden soll</param>
        private void KnxMerge(JProperty iTemplateProperty, string iKnxReplace) {
            var lTemplateValue = iTemplateProperty.Value.ToString();
            if (iTemplateProperty.Name.StartsWith("knx") && iTemplateProperty.Name != "knx_dpt") // && lTemplateValue.IndexOfAny(cPlusMinus) >= 0)
            {
                var lTemplateArray = lTemplateValue.Split('/');
                var lReplaceArray = iKnxReplace.Split('/');
                string lResult = "";
                string lGaPartMessage = null;
                int lGaPartValue = -1;

                for (int i = 0; i < 3; i++) {
                    string lValue = KnxMergeSingle(lTemplateArray[i], lReplaceArray[i], iTemplateProperty.Name == "knx");
                    lResult += lValue;
                    if (i < 2) lResult += "/";
                    if (lValue != "-") {
                        int lCheck = Convert.ToInt32(lValue);
                        switch (i) {
                            case 0:
                                if (lCheck < 0 || lCheck > 15) {
                                    lGaPartMessage = "Main";
                                    lGaPartValue = lCheck;
                                }
                                break;
                            case 1:
                                if (lCheck < 0 || lCheck > 7) {
                                    lGaPartMessage = "Middle";
                                    lGaPartValue = lCheck;
                                }
                                break;
                            case 2:
                                if (lCheck < 0 || lCheck > 255) {
                                    lGaPartMessage = "Low";
                                    lGaPartValue = lCheck;
                                }
                                break;
                            default:
                                break;
                        }

                    }
                }
                if (lGaPartValue >= 0) {
                    throw new Exception(string.Format("KNX: {2} group address part out of bounds: {0} in {1}", lGaPartValue, lResult, lGaPartMessage));
                }
                iTemplateProperty.Value = new JValue(lResult);
            }
        }

        /// <summary>
        /// Merge einer einzelnen Ziffer einer Gruppenadresse
        /// </summary>
        /// <param name="iTemplate">Ziffer im Template</param>
        /// <param name="iReplace">Ziffer in Eretzungsregel</param>
        /// <param name="iAbsoluteOnly">Wenn gesetzt, werden keine relativen Werte aufaddiert</param>
        /// <returns></returns>
        private string KnxMergeSingle(string iTemplate, string iReplace, bool iAbsoluteOnly) {
            string lResult = iTemplate;
            if (iTemplate == "-" && iReplace != "-") {
                lResult = Convert.ToInt16(iReplace).ToString();
            } else if ((iReplace.IndexOfAny(cPlusMinus) >= 0 || iTemplate.IndexOfAny(cPlusMinus) >= 0) && !iAbsoluteOnly && iTemplate != "-" && iReplace != "") {
                lResult = (Convert.ToInt16(iReplace) + Convert.ToInt16(iTemplate)).ToString();
            }
            if (iReplace == "-") lResult = "+0";
            return lResult;
        }


    }

}
