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
            KeepDisplayAllLifeMeters,
            AutoGoWatchTower,
            AutoGoWatchTowerRadius,
            NumpadFilterVeteran,
            GameSpeedChange,
            FilterVeteranUnit,
            AutoFindBonusItem,
            AutoDisperse,
            FastAttack,
            OptimizeTrainingSequence,
            DestroyAllSelectedUnits,
            GameMenuEnhancer,
            OptimizeAttackPriority,
            CancelResearchAnytime,
            DisableAutoSave,
            AutoSaveInterval,
            MaxSaveBackup,
            AutoDeleteBackups,
            EnhancedSelection,
            DeselectUnitsAfterTowerSearch,
            BatchCancelCommand,
            QuickBuyResource,
            QuickBuyResourceDelay,
            KeyDisplayAllLifeMeters,
            KeyAutoGoWatchTower,
            KeyGameSpeedUp,
            KeyGameSpeedDown,
            KeyFilterVeteran,
            KeyFindBonusItem,
            KeyAutoDisperse,
            KeyDestroyAllSelected,
            KeyBatchCancel,
            SelectedRulesDir
        }
        public bool KeepDisplayAllLifeMeters { get; set; } = true;
        public bool AutoGoWatchTower { get; set; } = true;
        public int AutoGoWatchTowerRadius { get; set; } = 10;
        public bool NumpadFilterVeteran { get; set; } = true;
        public bool GameSpeedChange { get; set; } = true;
        public bool FilterVeteranUnit { get; set; } = true;
        public bool AutoFindBonusItem { get; set; } = true;
        public bool AutoDisperse { get; set; } = false;
        public bool FastAttack { get; set; } = false;
        public bool OptimizeTrainingSequence { get; set; } = true;
        public bool DestroyAllSelectedUnits { get; set; } = true;
        public bool GameMenuEnhancer { get; set; } = true;
        public bool OptimizeAttackPriority { get; set; } = false;
        public bool CancelResearchAnytime { get; set; } = false;
        public bool DisableAutoSave { get; set; } = false;
        public int AutoSaveInterval { get; set; } = 1200;
        public int MaxSaveBackup { get; set; } = 5;
        public bool AutoDeleteBackups { get; set; } = true;
        public bool EnhancedSelection { get; set; } = true;
        public bool DeselectUnitsAfterTowerSearch { get; set; } = true;
        public bool BatchCancelCommand { get; set; } = true;
        public bool QuickBuyResource { get; set; } = true;
        public int QuickBuyResourceDelay { get; set; } = 100;
        public string KeyDisplayAllLifeMeters { get; set; } = "Y";
        public string KeyAutoGoWatchTower { get; set; } = "F";
        public string KeyGameSpeedUp { get; set; } = "Oemplus";
        public string KeyGameSpeedDown { get; set; } = "OemMinus";
        public string KeyFilterVeteran { get; set; } = "V";
        public string KeyFindBonusItem { get; set; } = "L";
        public string KeyAutoDisperse { get; set; } = "E";
        public string KeyDestroyAllSelected { get; set; } = "Delete";
        public string KeyBatchCancel { get; set; } = "N";
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
                path = "Mods/ModData/" + modInfos.SteamID + "/TABHelperMod.ini";
            else
                path = "Mods/ModData/TAB Helper/TABHelperMod.ini";
            IniPath = path;
            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            }

            ini = new EasySharpIni.IniFile(path);
            ini.AddField("KeepDisplayAllLifeMeters", "true", "true");
            ini.AddField("AutoGoWatchTower", "true", "true");
            ini.AddField("AutoGoWatchTowerRadius", "10", "10");
            ini.AddField("NumpadFilterVeteran", "true", "true");
            ini.AddField("GameSpeedChange", "true", "true");
            ini.AddField("FilterVeteranUnit", "true", "true");
            ini.AddField("AutoFindBonusItem", "true", "true");
            ini.AddField("AutoDisperse", "false", "false");
            ini.AddField("FastAttack", "false", "false");
            ini.AddField("OptimizeTrainingSequence", "true", "true");
            ini.AddField("DestroyAllSelectedUnits", "true", "true");
            ini.AddField("GameMenuEnhancer", "true", "true");
            ini.AddField("OptimizeAttackPriority", "false", "false");
            ini.AddField("CancelResearchAnytime", "false", "false");
            ini.AddField("DisableAutoSave", "false", "false");
            ini.AddField("AutoSaveInterval", "1200", "1200");
            ini.AddField("MaxSaveBackup", "5", "5");
            ini.AddField("AutoDeleteBackups", "true", "true");
            ini.AddField("EnhancedSelection", "true", "true");
            ini.AddField("DeselectUnitsAfterTowerSearch", "true", "true");
            ini.AddField("BatchCancelCommand", "true", "true");
            ini.AddField("QuickBuyResource", "true", "true");
            ini.AddField("QuickBuyResourceDelay", "100", "100");
            ini.AddField("KeyDisplayAllLifeMeters", "Y", "Y");
            ini.AddField("KeyAutoGoWatchTower", "F", "F");
            ini.AddField("KeyGameSpeedUp", "Oemplus", "Oemplus");
            ini.AddField("KeyGameSpeedDown", "OemMinus", "OemMinus");
            ini.AddField("KeyFilterVeteran", "V", "V");
            ini.AddField("KeyFindBonusItem", "L", "L");
            ini.AddField("KeyAutoDisperse", "E", "E");
            ini.AddField("KeyDestroyAllSelected", "Delete", "Delete");
            ini.AddField("KeyBatchCancel", "N", "N");
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

            KeepDisplayAllLifeMeters = bool.Parse(ini.GetField("KeepDisplayAllLifeMeters", "true").Get());
            AutoGoWatchTower = bool.Parse(ini.GetField("AutoGoWatchTower", "true").Get());
            AutoGoWatchTowerRadius = int.Parse(ini.GetField("AutoGoWatchTowerRadius", "10").Get());
            NumpadFilterVeteran = bool.Parse(ini.GetField("NumpadFilterVeteran", "true").Get());
            GameSpeedChange = bool.Parse(ini.GetField("GameSpeedChange", "true").Get());
            FilterVeteranUnit = bool.Parse(ini.GetField("FilterVeteranUnit", "true").Get());
            AutoFindBonusItem = bool.Parse(ini.GetField("AutoFindBonusItem", "true").Get());
            AutoDisperse = bool.Parse(ini.GetField("AutoDisperse", "false").Get());
            FastAttack = bool.Parse(ini.GetField("FastAttack", "false").Get());
            OptimizeTrainingSequence = bool.Parse(ini.GetField("OptimizeTrainingSequence", "true").Get());
            DestroyAllSelectedUnits = bool.Parse(ini.GetField("DestroyAllSelectedUnits", "true").Get());
            GameMenuEnhancer = bool.Parse(ini.GetField("GameMenuEnhancer", "true").Get());
            OptimizeAttackPriority = bool.Parse(ini.GetField("OptimizeAttackPriority", "false").Get());
            CancelResearchAnytime = bool.Parse(ini.GetField("CancelResearchAnytime", "false").Get());
            DisableAutoSave = bool.Parse(ini.GetField("DisableAutoSave", "false").Get());
            AutoSaveInterval = int.Parse(ini.GetField("AutoSaveInterval", "1200").Get());
            MaxSaveBackup = int.Parse(ini.GetField("MaxSaveBackup", "5").Get());
            AutoDeleteBackups = bool.Parse(ini.GetField("AutoDeleteBackups", "true").Get());
            EnhancedSelection = bool.Parse(ini.GetField("EnhancedSelection", "true").Get());
            DeselectUnitsAfterTowerSearch = bool.Parse(ini.GetField("DeselectUnitsAfterTowerSearch", "true").Get());
            BatchCancelCommand = bool.Parse(ini.GetField("BatchCancelCommand", "true").Get());
            QuickBuyResource = bool.Parse(ini.GetField("QuickBuyResource", "true").Get());
            QuickBuyResourceDelay = int.Parse(ini.GetField("QuickBuyResourceDelay", "100").Get());
            KeyDisplayAllLifeMeters = ini.GetField("KeyDisplayAllLifeMeters", "Y").Get();
            KeyAutoGoWatchTower = ini.GetField("KeyAutoGoWatchTower", "F").Get();
            KeyGameSpeedUp = ini.GetField("KeyGameSpeedUp", "Oemplus").Get();
            KeyGameSpeedDown = ini.GetField("KeyGameSpeedDown", "OemMinus").Get();
            KeyFilterVeteran = ini.GetField("KeyFilterVeteran", "V").Get();
            KeyFindBonusItem = ini.GetField("KeyFindBonusItem", "L").Get();
            KeyAutoDisperse = ini.GetField("KeyAutoDisperse", "E").Get();
            KeyDestroyAllSelected = ini.GetField("KeyDestroyAllSelected", "Delete").Get();
            KeyBatchCancel = ini.GetField("KeyBatchCancel", "N").Get();
            SelectedRulesDir = ini.GetField("SelectedRulesDir", "").Get();
            ReloadDefaultRules = bool.Parse(ini.GetField("ReloadDefaultRules", "false").Get());
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(IniPath))
                return;

            var ini = new EasySharpIni.IniFile(IniPath).Parse();
            void Set(string key, string value) => ini.GetField(key, value).Set(value);

            Set("KeepDisplayAllLifeMeters", KeepDisplayAllLifeMeters.ToString().ToLower());
            Set("AutoGoWatchTower", AutoGoWatchTower.ToString().ToLower());
            Set("AutoGoWatchTowerRadius", AutoGoWatchTowerRadius.ToString());
            Set("NumpadFilterVeteran", NumpadFilterVeteran.ToString().ToLower());
            Set("GameSpeedChange", GameSpeedChange.ToString().ToLower());
            Set("FilterVeteranUnit", FilterVeteranUnit.ToString().ToLower());
            Set("AutoFindBonusItem", AutoFindBonusItem.ToString().ToLower());
            Set("AutoDisperse", AutoDisperse.ToString().ToLower());
            Set("FastAttack", FastAttack.ToString().ToLower());
            Set("OptimizeTrainingSequence", OptimizeTrainingSequence.ToString().ToLower());
            Set("DestroyAllSelectedUnits", DestroyAllSelectedUnits.ToString().ToLower());
            Set("GameMenuEnhancer", GameMenuEnhancer.ToString().ToLower());
            Set("OptimizeAttackPriority", OptimizeAttackPriority.ToString().ToLower());
            Set("CancelResearchAnytime", CancelResearchAnytime.ToString().ToLower());
            Set("DisableAutoSave", DisableAutoSave.ToString().ToLower());
            Set("AutoSaveInterval", AutoSaveInterval.ToString());
            Set("MaxSaveBackup", MaxSaveBackup.ToString());
            Set("AutoDeleteBackups", AutoDeleteBackups.ToString().ToLower());
            Set("EnhancedSelection", EnhancedSelection.ToString().ToLower());
            Set("DeselectUnitsAfterTowerSearch", DeselectUnitsAfterTowerSearch.ToString().ToLower());
            Set("BatchCancelCommand", BatchCancelCommand.ToString().ToLower());
            Set("QuickBuyResource", QuickBuyResource.ToString().ToLower());
            Set("QuickBuyResourceDelay", QuickBuyResourceDelay.ToString());
            Set("KeyDisplayAllLifeMeters", KeyDisplayAllLifeMeters);
            Set("KeyAutoGoWatchTower", KeyAutoGoWatchTower);
            Set("KeyGameSpeedUp", KeyGameSpeedUp);
            Set("KeyGameSpeedDown", KeyGameSpeedDown);
            Set("KeyFilterVeteran", KeyFilterVeteran);
            Set("KeyFindBonusItem", KeyFindBonusItem);
            Set("KeyAutoDisperse", KeyAutoDisperse);
            Set("KeyDestroyAllSelected", KeyDestroyAllSelected);
            Set("KeyBatchCancel", KeyBatchCancel);
            Set("SelectedRulesDir", SelectedRulesDir);
            Set("ReloadDefaultRules", ReloadDefaultRules.ToString().ToLower());

            ini.Write();
        }

        public void ReloadFromDisk()
        {
            if (string.IsNullOrEmpty(IniPath))
                return;
            var ini = new EasySharpIni.IniFile(IniPath).Parse();

            KeepDisplayAllLifeMeters = bool.Parse(ini.GetField("KeepDisplayAllLifeMeters", "true").Get());
            AutoGoWatchTower = bool.Parse(ini.GetField("AutoGoWatchTower", "true").Get());
            AutoGoWatchTowerRadius = int.Parse(ini.GetField("AutoGoWatchTowerRadius", "10").Get());
            NumpadFilterVeteran = bool.Parse(ini.GetField("NumpadFilterVeteran", "true").Get());
            GameSpeedChange = bool.Parse(ini.GetField("GameSpeedChange", "true").Get());
            FilterVeteranUnit = bool.Parse(ini.GetField("FilterVeteranUnit", "true").Get());
            AutoFindBonusItem = bool.Parse(ini.GetField("AutoFindBonusItem", "true").Get());
            AutoDisperse = bool.Parse(ini.GetField("AutoDisperse", "false").Get());
            FastAttack = bool.Parse(ini.GetField("FastAttack", "false").Get());
            OptimizeTrainingSequence = bool.Parse(ini.GetField("OptimizeTrainingSequence", "true").Get());
            DestroyAllSelectedUnits = bool.Parse(ini.GetField("DestroyAllSelectedUnits", "true").Get());
            GameMenuEnhancer = bool.Parse(ini.GetField("GameMenuEnhancer", "true").Get());
            OptimizeAttackPriority = bool.Parse(ini.GetField("OptimizeAttackPriority", "false").Get());
            CancelResearchAnytime = bool.Parse(ini.GetField("CancelResearchAnytime", "false").Get());
            DisableAutoSave = bool.Parse(ini.GetField("DisableAutoSave", "false").Get());
            AutoSaveInterval = int.Parse(ini.GetField("AutoSaveInterval", "1200").Get());
            MaxSaveBackup = int.Parse(ini.GetField("MaxSaveBackup", "5").Get());
            AutoDeleteBackups = bool.Parse(ini.GetField("AutoDeleteBackups", "true").Get());
            EnhancedSelection = bool.Parse(ini.GetField("EnhancedSelection", "true").Get());
            DeselectUnitsAfterTowerSearch = bool.Parse(ini.GetField("DeselectUnitsAfterTowerSearch", "true").Get());
            BatchCancelCommand = bool.Parse(ini.GetField("BatchCancelCommand", "true").Get());
            QuickBuyResource = bool.Parse(ini.GetField("QuickBuyResource", "true").Get());
            QuickBuyResourceDelay = int.Parse(ini.GetField("QuickBuyResourceDelay", "100").Get());
            KeyDisplayAllLifeMeters = ini.GetField("KeyDisplayAllLifeMeters", "Y").Get();
            KeyAutoGoWatchTower = ini.GetField("KeyAutoGoWatchTower", "F").Get();
            KeyGameSpeedUp = ini.GetField("KeyGameSpeedUp", "Oemplus").Get();
            KeyGameSpeedDown = ini.GetField("KeyGameSpeedDown", "OemMinus").Get();
            KeyFilterVeteran = ini.GetField("KeyFilterVeteran", "V").Get();
            KeyFindBonusItem = ini.GetField("KeyFindBonusItem", "L").Get();
            KeyAutoDisperse = ini.GetField("KeyAutoDisperse", "E").Get();
            KeyDestroyAllSelected = ini.GetField("KeyDestroyAllSelected", "Delete").Get();
            KeyBatchCancel = ini.GetField("KeyBatchCancel", "N").Get();
            SelectedRulesDir = ini.GetField("SelectedRulesDir", "").Get();
            ReloadDefaultRules = bool.Parse(ini.GetField("ReloadDefaultRules", "false").Get());
        }
    }
}
