using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ValheimLegends
{
    public class VL_ConfigSync
    {
        public static string ConfigPath = Path.GetDirectoryName(Paths.BepInExConfigPath) + Path.DirectorySeparatorChar + "ValheimLegends.cfg";

        // Último valor recibido del server por cada key sincronizada (usado para
        // revertir cambios locales del cliente).
        public static Dictionary<string, object> ServerValues = new Dictionary<string, object>();

        private static string FormatInvariant(object v)
        {
            if (v is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (v is double d) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (v is bool b) return b ? "true" : "false";
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static void RPC_VL_ConfigSync(long sender, ZPackage configPkg)
        {
            if (ZNet.instance.IsServer()) //Server
            {
                ZPackage pkg = new ZPackage();
                //ZLog.Log("VL SERVER -------------- Sending client #" + sender + " server configs");
                string[] rawConfigData = File.ReadAllLines(ConfigPath);
                List<string> cleanConfigData = new List<string>();
                HashSet<string> seenKeys = new HashSet<string>();

                for (int i = 0; i < rawConfigData.Length; i++)
                {
                    //if (rawConfigData[i].Trim().StartsWith(";") ||
                    //    rawConfigData[i].Trim().StartsWith("#")) continue; //Skip comments
                    //if (rawConfigData[i].Trim().IsNullOrWhiteSpace()) continue; //Skip blank lines
                    if (!rawConfigData[i].Trim().StartsWith("vl_svr_")) continue; //Skip local lines

                    //Add to clean data
                    cleanConfigData.Add(rawConfigData[i]);
                    // Track key para no duplicar
                    string t = rawConfigData[i].Trim();
                    int eq = t.IndexOf('=');
                    if (eq > 0) seenKeys.Add(t.Substring(0, eq).Trim());
                    //ZLog.Log("VL SERVER -------------- sending config: " + rawConfigData[i]);
                }

                // Emitir todas las SyncedEntries del fork (VL_TweakConfig) desde memoria,
                // por si no aparecen en el archivo o si fueron editadas en runtime.
                foreach (var kvp in VL_TweakConfig.SyncedEntries)
                {
                    if (kvp.Value == null) continue;
                    if (seenKeys.Contains(kvp.Key)) continue; // ya emitido desde archivo
                    cleanConfigData.Add(kvp.Key + " = " + FormatInvariant(kvp.Value.BoxedValue));
                }

                cleanConfigData.Add("vl_svr_version = " + ValheimLegends.Version);
                //ZLog.Log("VL SERVER -------------- sending config: " + "vl_svr_version = " + ValheimLegends.VersionF);
                //Add number of clean lines to package
                pkg.Write(cleanConfigData.Count);

                //Add each line to the package
                foreach (string line in cleanConfigData)
                {
                    pkg.Write(line);
                }

                ZRoutedRpc.instance.InvokeRoutedRPC(sender, "VL_ConfigSync", new object[]
                {
                    pkg
                });

                ZLog.Log("Valheim Legends server configurations synced to peer #" + sender);
            }
            else //Client
            {
                if (configPkg != null &&
                    configPkg.Size() > 0 &&
                    sender == ValheimLegends.ServerID)
                {
                    int numLines = configPkg.ReadInt();
                    //ZLog.Log("VL CLIENT -------------- Receiving server configs from #" + sender);
                    if (numLines == 0)
                    {
                        ZLog.LogWarning("Got zero line config file from server. Cannot load.");
                        return;
                    }

                    char[] trm = { ' ', '=' };
                    bool syncOrVersionFailure = false;
                    for (int i = 0; i < numLines; i++)
                    {
                        string line = configPkg.ReadString();
                        //ZLog.Log("VL CLIENT -------------- Received line: " + line);
                        //ZLog.Log("reading line: " + line);
                        string key = line.Substring(0, line.IndexOf('=') + 1);  //line.Substring(0, line.IndexOf('=') + 1);    
                        key = key.Trim(trm);
                        //ZLog.Log("key string is " + key);
                        if (key == "vl_svr_version")
                        {
                            string val = line.Substring(line.IndexOf('=') + 1);
                            val = val.Trim(trm);
                            //ZLog.Log("val is " + val + " server evrsion is " + ValheimLegends.Version);
                            if (val != ValheimLegends.Version)
                            {
                                char[] trm_e = { '.', ',', '0' };
                                string val2 = val.Trim(trm_e);
                                string keyString = VL_GlobalConfigs.ConfigStrings[key].ToString();
                                string key2 = keyString.Trim(trm_e);
                                //ZLog.Log("VL CLIENT -------------- Initial version check FAILED --- Europeon localization check: server had version [" + val2 + "] and client had version [" + key2 + "]");
                                //if (val2 == key2)
                                //{
                                //    ZLog.Log("VL CLIENT -------------- version MATCH for european localization: server had version [" + val2 + "] and client had version [" + key2 + "] VL DLL constant set to [" + ValheimLegends.VersionF + "]");
                                //}
                                ZLog.Log("VL CLIENT -------------- version failure: server had version [" + val + "] and client had version [" + ValheimLegends.Version + "]");
                                syncOrVersionFailure = true;
                            }

                        }
                        else if (VL_GlobalConfigs.ConfigStrings.ContainsKey(key))
                        {
                            //ZLog.Log("VL CLIENT -------------- found config match for: " + key + " ----- changing running modifiers ");
                            string val = line.Substring(line.IndexOf('=') + 1);
                            val = val.Trim(trm);
                            if (key == "vl_svr_enforceConfigClass")
                            {
                                val = val.ToLower().ToString() == "true" ? "1" : "0";
                            }
                            else if(key == "vl_svr_aoeRequiresLoS")
                            {
                                val = val.ToLower().ToString() == "true" ? "1" : "0";
                            }
                            else if (key == "vl_svr_allowAltarClassChange")
                            {
                                val = val.ToLower().ToString() == "true" ? "1" : "0";
                            }
                            //ZLog.Log("value is: " + val + " parsed to " + float.Parse(val));
                            float val2 = 1f;
                            try
                            {
                                val2 = float.Parse(val);
                            }
                            catch
                            {
                                val = val.Replace(",", ".");
                            }
                            try
                            {
                                val2 = float.Parse(val);
                            }
                            catch
                            {
                                val = val.Replace(".", ",");
                            }
                            try
                            {
                                val2 = float.Parse(val);
                            }
                            catch
                            {
                                ZLog.Log("Valheim Legends: unable to sync modifiers - setting to default");
                                val2 = 1f;
                            }
                            VL_GlobalConfigs.ConfigStrings[key] = val2;
                            //ZLog.Log("config value is " + VL_GlobalConfigs.ConfigStrings[key]);
                        }
                        else if (VL_GlobalConfigs.ItemStrings.ContainsKey(key))
                        {
                            //ZLog.Log("VL CLIENT -------------- found config match for: " + key + " ----- changing running modifiers ");
                            string val = line.Substring(line.IndexOf('=') + 1);
                            val = val.Trim(trm);
                            if (val != "")
                            {
                                VL_GlobalConfigs.ItemStrings[key] = val;
                                //ZLog.Log("config value is " + VL_GlobalConfigs.ConfigStrings[key]);
                            }
                        }
                        else if (VL_TweakConfig.SyncedEntries.ContainsKey(key))
                        {
                            // Fork: sincronizar entries de VL_TweakConfig (prefijo vl_svr_)
                            string val = line.Substring(line.IndexOf('=') + 1).Trim(trm);
                            try
                            {
                                ConfigEntryBase entry = VL_TweakConfig.SyncedEntries[key];
                                Type t = entry.SettingType;
                                object parsed = null;
                                if (t == typeof(float))
                                {
                                    string vv = val.Replace(",", ".");
                                    parsed = float.Parse(vv, System.Globalization.CultureInfo.InvariantCulture);
                                }
                                else if (t == typeof(int))
                                {
                                    parsed = int.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                                }
                                else if (t == typeof(bool))
                                {
                                    parsed = val.Trim().ToLower() == "true";
                                }
                                else if (t == typeof(string))
                                {
                                    parsed = val;
                                }
                                if (parsed != null)
                                {
                                    entry.BoxedValue = parsed;
                                    ServerValues[key] = parsed;
                                }
                            }
                            catch (Exception ex)
                            {
                                ZLog.LogWarning("VL sync tweak failed for " + key + ": " + ex.Message);
                            }
                        }
                    }
                    if (syncOrVersionFailure)
                    {
                        ZLog.LogWarning("Valheim Legends version mismatch; disabling.");
                        ValheimLegends.playerEnabled = false;
                        //ValheimLegends._Harmony.UnpatchSelf();
                    }
                    else
                    {
                        ZLog.Log("Valheim Legends configurations synced to server.");
                    }
                }
            }
        }
    }
}