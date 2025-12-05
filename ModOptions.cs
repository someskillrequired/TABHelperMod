using Fasterflect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TABModLoader;

namespace TABHelperMod
{
    internal class ModOptions
    {
        public enum FuncConfigList
        {
            SelectedRulesDir
        }

        public string SelectedRulesDir { get; set; } = "";
        public bool ReloadDefaultRules { get; set; } = false;

        // Cached ini path so UI can persist edits.
        internal string IniPath { get; private set; }

        public static ModOptions Instance { get; } = new ModOptions();
        private bool IsLoaded = false;

        public void Load(ModInfos modInfos)
        {
            if (IsLoaded)
                return;
            IsLoaded = true;
            EasySharpIni.IniFile ini;
            string path;
            if (modInfos.SteamID > 0)
                path = "Mods/ModData/" + modInfos.SteamID + "/SSR.ini";
            else
                path = "Mods/ModData/SSR/SSR.ini";
            IniPath = path;
            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            }

            ini = new EasySharpIni.IniFile(path);
            ini.AddField("SelectedRulesDir", "", "");
            ini.AddField("ReloadDefaultRules", "false", "false");
            if (!System.IO.File.Exists(path))
            {
                ini.Write();
            }
            else
            {
                var ini2 = new EasySharpIni.IniFile(path).Parse();
                foreach (var fieldName in Enum.GetNames(typeof(FuncConfigList)))
                {
                    if (ini2.GetField(fieldName) == "")
                    {
                        ini2.AddField(fieldName, ini.GetField(fieldName).Get(), ini.GetField(fieldName).Get());
                    }
                }
                ini2.Write();
                ini = new EasySharpIni.IniFile(path).Parse();
            }

            SelectedRulesDir = ini.GetField("SelectedRulesDir", "").Get();
            ReloadDefaultRules = bool.Parse(ini.GetField("ReloadDefaultRules", "false").Get());
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(IniPath))
                return;

            var ini = new EasySharpIni.IniFile(IniPath).Parse();
            void Set(string key, string value) => ini.GetField(key, value).Set(value);

            Set("SelectedRulesDir", SelectedRulesDir);
            Set("ReloadDefaultRules", ReloadDefaultRules.ToString().ToLower());

            ini.Write();
        }

        public void ReloadFromDisk()
        {
            if (string.IsNullOrEmpty(IniPath))
                return;
            var ini = new EasySharpIni.IniFile(IniPath).Parse();

            SelectedRulesDir = ini.GetField("SelectedRulesDir", "").Get();
            ReloadDefaultRules = bool.Parse(ini.GetField("ReloadDefaultRules", "false").Get());
        }
    }
}
