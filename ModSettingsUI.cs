using DXVision;
using DXVision.GUI;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using DXVision.Serialization;
using TABModLoader.Utils;

namespace TABHelperMod
{
    internal static class ModSettingsUI
    {
        private static readonly List<int> AutoSaveIntervals = new List<int> { 300, 600, 900, 1200, 1800, 2400, 3600 };
        private static readonly List<int> WatchTowerRadii = new List<int> { 5, 10, 15, 20, 25, 30, 40, 100};
        private static readonly List<int> BackupCounts = Enumerable.Range(1, 10).ToList();
        private static readonly List<int> QuickBuyDelays = new List<int> { 0, 50, 100, 200, 400, 800 };

        internal static void OnStartScreenLoaded(object __instance)
        {
            try
            {
                DXGridLayout menuGrid = Traverse.Create(__instance).FieldWithDecrypt("_OMainGameMenu").GetValue<DXGridLayout>();
                if (menuGrid == null) { FileLog.Log("[ModSettingsUI] _OMainGameMenu null"); return; }

                ConstructorInfo ctor = AccessTools.Constructor(
                    AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXButtonLink"),
                    new Type[] { typeof(string), typeof(DXKeys[]) });

                DXTextButton modButton = (DXTextButton)ctor.Invoke(new object[] { "Mod Settings", Array.Empty<DXKeys>() });
                menuGrid.AddRow(new DXObject[] { modButton });
                menuGrid.Update();
                modButton.Activated += delegate { ShowModSettingsWindow(); };

                TryAddToMenuCategories(__instance, modButton);
                RefreshCurrentMenu(__instance);
            }
            catch (Exception e)
            {
                FileLog.Log("[ModSettingsUI] Error in OnStartScreenLoaded: " + e);
            }
        }

        private static void ShowModSettingsWindow()
        {
            try
            {
                Type windowType = AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXWindowTab");
                if (windowType == null) { FileLog.Log("[ModSettingsUI] ZXWindowTab type not found"); return; }

                object window = Activator.CreateInstance(windowType, new object[] { "Mod Settings" });

                InvokeShow(windowType, window);
                AddCloseButton(windowType, window);
                AddTogglePage(windowType, window, "Options", BuildEntries(ModOptions.Instance));
                AddKeyBindPage(windowType, window, "Key Binds");
                AddRulesPage(windowType, window, "Rules");
            }
            catch (Exception e)
            {
                FileLog.Log("[ModSettingsUI] Error in ShowModSettingsWindow: " + e);
            }
        }

        private static void InvokeShow(Type windowType, object window)
        {
            try
            {
                Type baseWindowType = AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXWindow") ?? windowType.BaseType;
                MethodInfo show = AccessToolsEX.MethodWithDecrypt(baseWindowType, "Show");
                if (show == null) show = FindMethodDeep(baseWindowType, "Show");
                FileLog.Log("[ModSettingsUI] Show method: " + (show == null ? "null" : show.ToString()));
                if (show == null) return;

                var ps = show.GetParameters();
                object[] args = BuildArgs(ps, "Blend");
                show.Invoke(window, args);
            }
            catch (Exception e)
            {
                FileLog.Log("[ModSettingsUI] Error invoking Show: " + e);
            }
        }

        private static void AddCloseButton(Type windowType, object window)
        {
            try
            {
                Type baseWindowType = AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXWindow") ?? windowType.BaseType;
                // Prefer the built-in default close wiring
                Type btnPosType = AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXWindow+TButtonPosition");
                MethodInfo setDefaultClose = AccessToolsEX.MethodWithDecrypt(baseWindowType, "SetDefaultButtonClose", new Type[] { typeof(Action), btnPosType });
                if (setDefaultClose == null) setDefaultClose = FindMethodDeep(baseWindowType, "SetDefaultButtonClose");
                if (setDefaultClose != null && btnPosType != null)
                {
                    object right = GetEnumOrDefault(btnPosType, "Right");
                    // Action saves options and refreshes from disk; window handles Hide internally
                    Action onClose = delegate
                    {
                        ModOptions.Instance.Save();
                        ModOptions.Instance.ReloadFromDisk();
                    };
                    setDefaultClose.Invoke(window, new object[] { onClose, right });
                    FileLog.Log("[ModSettingsUI] SetDefaultButtonClose wired");
                    return;
                }

                MethodInfo addButton = AccessToolsEX.MethodWithDecrypt(baseWindowType, "AddButton");
                if (addButton == null) addButton = FindMethodDeep(baseWindowType, "AddButton");
                FileLog.Log("[ModSettingsUI] AddButton method: " + (addButton == null ? "null" : addButton.ToString()));
                if (addButton != null)
                {
                    var ps = addButton.GetParameters();
                    object[] args = new object[ps.Length];
                    for (int i = 0; i < ps.Length; i++)
                    {
                        Type pt = ps[i].ParameterType;
                        if (pt.IsEnum) { args[i] = GetEnumOrDefault(pt, "Right"); continue; }
                        if (pt == typeof(string)) { args[i] = "Close"; continue; }
                        if (pt == typeof(DXKeys)) { args[i] = DXKeys.Escape; continue; }
                        if (pt == typeof(int)) { args[i] = 0; continue; }
                        if (!pt.IsValueType || Nullable.GetUnderlyingType(pt) != null) { args[i] = null; continue; }
                        args[i] = Activator.CreateInstance(pt);
                    }

                    object closeBtn = addButton.Invoke(window, args);
                    DXButton dxClose = closeBtn as DXButton;
                    if (dxClose != null)
                        dxClose.Activated += delegate { HideWindow(windowType, window); };
                    return;
                }

                // Fallback: try the built-in right button on ZXWindow
                try
                {
                    var rightButton = Traverse.Create(window).Field("_OButtonRight").GetValue<object>();
                    if (rightButton != null && rightButton.GetType().Name == "ZXButtonSteam")
                    {
                        dynamic rb = rightButton;
                        rb.Visible = true;
                        rb.Text = "Close";
                        rb.ShortcutKeys = new DXKeys[] { DXKeys.Escape };
                        rb.Activated += new Action<DXButton>(_ => HideWindow(windowType, window));
                        FileLog.Log("[ModSettingsUI] Fallback close button wired");
                    }
                }
                catch (Exception ex)
                {
                    FileLog.Log("[ModSettingsUI] Fallback AddCloseButton failed: " + ex);
                }

            }
            catch (Exception e)
            {
                FileLog.Log("[ModSettingsUI] Error in AddCloseButton: " + e);
            }
        }

        private static void HideWindow(Type windowType, object window)
        {
            try
            {
                Type baseWindowType = AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXWindow") ?? windowType.BaseType;
                MethodInfo hide = AccessToolsEX.MethodWithDecrypt(baseWindowType, "Hide", new Type[] { typeof(object) });
                if (hide == null) hide = FindMethodDeep(baseWindowType, "Hide");
                if (hide != null)
                {
                    var ps = hide.GetParameters();
                    object[] args = ps.Length == 1 ? new object[] { null } : new object[ps.Length];
                    hide.Invoke(window, args);
                }
                ModOptions.Instance.Save();
                ModOptions.Instance.ReloadFromDisk(); // pull fresh values immediately
            }
            catch { }
        }

        private static void AddTogglePage(Type windowType, object window, string tabTitle, IEnumerable<(Func<string> Text, Action Toggle)> entries)
        {
            try
            {
                MethodInfo addPage = AccessToolsEX.MethodWithDecrypt(windowType, "AddPage", new Type[] { typeof(string) });
                if (addPage == null) addPage = FindMethodDeep(windowType, "AddPage");
                FileLog.Log("[ModSettingsUI] AddPage method: " + (addPage == null ? "null" : addPage.ToString()));
                if (addPage == null) return;

                object page = addPage.Invoke(window, new object[] { tabTitle });
                if (page == null) { FileLog.Log("[ModSettingsUI] page null"); return; }

                object panel = page.GetType().GetProperty("Panel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(page, null);
                if (panel == null) { FileLog.Log("[ModSettingsUI] panel null"); return; }

                DXObject content = panel.GetType().GetProperty("Content", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(panel, null) as DXObject;
                object cwObj = panel.GetType().GetProperty("ContentWidth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(panel, null);
                float contentWidth = cwObj is float ? (float)cwObj : 0f;
                if (content == null || contentWidth <= 0f) { FileLog.Log("[ModSettingsUI] content null or width <=0"); return; }

                DXGridLayout grid = DXGridLayout.NewFormLayout(contentWidth, 0, true);
                content.AddObject(grid);

                grid.X = -30f * DXScene.Unit;
                grid.DefaultRowHeight = DXScene.Unit * 20f;
                grid.Border = null;
                grid.GridMargin = new RectangleF(DXScene.Unit * 1f, DXScene.Unit * 6f, DXScene.Unit * 147f, DXScene.Unit * 2f);
                grid.DefaultCellSpacing = new SizeF(DXScene.Unit * 2f, DXScene.Unit * 4f);

                ConstructorInfo ctor = AccessTools.Constructor(
                    AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXButtonLink"),
                    new Type[] { typeof(string), typeof(DXKeys[]) });

                foreach (var entry in entries)
                {
                    DXTextObject label = new DXTextObject();
                    label.Text = entry.Text();

                    DXTextButton btn = (DXTextButton)ctor.Invoke(new object[] { entry.Text(), Array.Empty<DXKeys>() });
                    btn.MinTimeBetweenActivations = 200;
                    btn.Width = contentWidth / 1.5f;
                    btn.Activated += delegate
                    {
                        entry.Toggle();
                        string newText = entry.Text();
                        btn.Text = newText;
                        label.Text = newText;
                        ModOptions.Instance.Save();
                    };

                    grid.AddRow(new DXObject[] { label, btn });
                }

                grid.Update();
            }
            catch (Exception e)
            {
                FileLog.Log("[ModSettingsUI] Error in AddIniPage: " + e);
            }
        }

        private static void AddKeyBindPage(Type windowType, object window, string tabTitle)
        {
            try
            {
                MethodInfo addPage = AccessToolsEX.MethodWithDecrypt(windowType, "AddPage", new Type[] { typeof(string) });
                if (addPage == null) addPage = FindMethodDeep(windowType, "AddPage");
                if (addPage == null) { FileLog.Log("[ModSettingsUI] AddPage not found for key binds"); return; }

                object page = addPage.Invoke(window, new object[] { tabTitle });
                object panel = page.GetType().GetProperty("Panel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(page, null);
                if (panel == null) { FileLog.Log("[ModSettingsUI] keybind panel null"); return; }
                DXObject content = panel.GetType().GetProperty("Content", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(panel, null) as DXObject;
                object cwObj = panel.GetType().GetProperty("ContentWidth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(panel, null);
                float contentWidth = cwObj is float ? (float)cwObj : 0f;
                if (content == null || contentWidth <= 0f) { FileLog.Log("[ModSettingsUI] keybind content null/width"); return; }

                DXGridLayout grid = DXGridLayout.NewFormLayout(contentWidth, 0, true);
                content.AddObject(grid);
                grid.X = -142f * DXScene.Unit;
                grid.DefaultRowHeight = DXScene.Unit * 28f;
                grid.Border = null;
                grid.GridMargin = new RectangleF(DXScene.Unit * 1f, DXScene.Unit * 6f, DXScene.Unit * 7f, DXScene.Unit * 2f);
                grid.DefaultCellSpacing = new SizeF(DXScene.Unit * 2f, DXScene.Unit * 4f);

                Type msgBoxType = AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXMessageBox");
                MethodInfo askForKey = AccessToolsEX.MethodWithDecrypt(msgBoxType, "AskForKey", new Type[] { typeof(Action<DXKeys>), typeof(Action) });
                if (askForKey == null)
                {
                    FileLog.Log("[ModSettingsUI] AskForKey not found");
                    return;
                }

                var bindings = new List<(string Label, Func<string> Get, Action<string> Set)>
                {
                    ("Display All Life Meters", () => ModOptions.Instance.KeyDisplayAllLifeMeters, v => ModOptions.Instance.KeyDisplayAllLifeMeters = v),
                    ("Auto Go Watchtower", () => ModOptions.Instance.KeyAutoGoWatchTower, v => ModOptions.Instance.KeyAutoGoWatchTower = v),
                    ("Game Speed Up", () => ModOptions.Instance.KeyGameSpeedUp, v => ModOptions.Instance.KeyGameSpeedUp = v),
                    ("Game Speed Down", () => ModOptions.Instance.KeyGameSpeedDown, v => ModOptions.Instance.KeyGameSpeedDown = v),
                    ("Filter Veteran", () => ModOptions.Instance.KeyFilterVeteran, v => ModOptions.Instance.KeyFilterVeteran = v),
                    ("Find Bonus Item", () => ModOptions.Instance.KeyFindBonusItem, v => ModOptions.Instance.KeyFindBonusItem = v),
                    ("Auto Disperse", () => ModOptions.Instance.KeyAutoDisperse, v => ModOptions.Instance.KeyAutoDisperse = v),
                    ("Destroy Selected Units", () => ModOptions.Instance.KeyDestroyAllSelected, v => ModOptions.Instance.KeyDestroyAllSelected = v),
                    ("Batch Cancel", () => ModOptions.Instance.KeyBatchCancel, v => ModOptions.Instance.KeyBatchCancel = v)
                };

                foreach (var binding in bindings)
                {
                    DXTextObject label = new DXTextObject();
                    label.Text = binding.Label;

                    DXTextButton changeBtn = new DXTextButton($"{binding.Label}: {binding.Get()} (Change)");
                    changeBtn.MinTimeBetweenActivations = 200;
                    changeBtn.Width = contentWidth / 1.5f;
                    changeBtn.Height = DXScene.Unit * 30f;
                    var tr = changeBtn.TextRenderer;
                    if (tr != null)
                    {
                        var clone = (DXTextRenderer)tr.Clone();
                        clone.Scale(0.9f);
                        changeBtn.TextRenderer = clone;
                    }

                    Action rebind = () =>
                    {
                        askForKey.Invoke(null, new object[]
                        {
                            (Action<DXKeys>)(pressed =>
                            {
                                binding.Set(pressed.ToString());
                                changeBtn.Text = $"{binding.Label}: {binding.Get()} (Change)";
                                ModOptions.Instance.Save();
                                ModOptions.Instance.ReloadFromDisk();
                            }),
                            (Action)(() => { })
                        });
                    };

                    changeBtn.Activated += delegate { rebind(); };

                    grid.AddRow(new DXObject[] { label, changeBtn });
                }

                grid.Update();
            }
            catch (Exception e)
            {
                FileLog.Log("[ModSettingsUI] Error in AddKeyBindingRows: " + e);
            }
        }

        private static void AddRulesPage(Type windowType, object window, string tabTitle)
        {
            try
            {
                MethodInfo addPage = AccessToolsEX.MethodWithDecrypt(windowType, "AddPage", new Type[] { typeof(string) });
                if (addPage == null) addPage = FindMethodDeep(windowType, "AddPage");
                if (addPage == null) { FileLog.Log("[ModSettingsUI] AddPage not found for rules"); return; }

                object page = addPage.Invoke(window, new object[] { tabTitle });
                if (page == null) { FileLog.Log("[ModSettingsUI] rules page null"); return; }

                object panel = page.GetType().GetProperty("Panel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(page, null);
                if (panel == null) { FileLog.Log("[ModSettingsUI] rules panel null"); return; }

                DXObject content = panel.GetType().GetProperty("Content", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(panel, null) as DXObject;
                object cwObj = panel.GetType().GetProperty("ContentWidth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(panel, null);
                float contentWidth = cwObj is float f ? f : 0f;
                if (content == null || contentWidth <= 0f) { FileLog.Log("[ModSettingsUI] rules content null/width"); return; }

                DXGridLayout grid = DXGridLayout.NewFormLayout(contentWidth, 0, true);
                content.AddObject(grid);
                grid.X = 145f * DXScene.Unit;
                grid.DefaultRowHeight = DXScene.Unit * 28f;
                grid.Border = null;
                grid.GridMargin = new RectangleF(DXScene.Unit * 1f, DXScene.Unit * 6f, DXScene.Unit * 7f, DXScene.Unit * 2f);
                grid.DefaultCellSpacing = new SizeF(DXScene.Unit * 2f, DXScene.Unit * 4f);

                ConstructorInfo ctor = AccessTools.Constructor(
                    AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXButtonLink"),
                    new Type[] { typeof(string), typeof(DXKeys[]) });

                Action<string, Action> addBtn = (text, action) =>
                {
                    try
                    {
                        DXTextButton btn = (DXTextButton)ctor.Invoke(new object[] { text, Array.Empty<DXKeys>() });
                        btn.MinTimeBetweenActivations = 200;
                        btn.Width = contentWidth / 1.1f;
                        btn.Height = DXScene.Unit * 30f;
                        btn.Activated += delegate { action(); };
                        grid.AddRow(new DXObject[] { btn });
                    }
                    catch (Exception ex)
                    {
                        FileLog.Log("[ModSettingsUI] addBtn failed: " + ex);
                    }
                };

                string gameDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string baseDir = Directory.GetCurrentDirectory();
                // Prefer current working directory (game root), fall back to assembly location.
                string rulesRoot = File.Exists(Path.Combine(baseDir, "ZXRules.dat")) ? baseDir : gameDir;
                string modsRoot = Path.Combine(rulesRoot, "Mods", "Modfiles");
                string defaultFolder = Path.Combine(modsRoot, "Default");
                string[] ruleFiles = new[]
                {
                    "ZXRules.dat",
                    "ZXCampaign.dat",
                    "ZXCampaignStrings.dat",
                    "ZXGame.dxprj",
                    "ZXStrings.dat"
                };

                // collect potential roots (game dir + current dir) to find mod ZXRules
                var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(modsRoot)) roots.Add(modsRoot);
                string cwdRoot = Path.Combine(Directory.GetCurrentDirectory(), "Mods", "Modfiles");
                if (Directory.Exists(cwdRoot)) roots.Add(cwdRoot);
                FileLog.Log("[ModSettingsUI] Searching ZXRules in: " + string.Join(" | ", roots));

                // Built-in default button: copy default set (fallback to root)
                addBtn("Default rules (built-in)", () =>
                {
                    ModOptions.Instance.SelectedRulesDir = "Default";
                    ModOptions.Instance.Save();
                    ModOptions.Instance.ReloadFromDisk();
                    FileLog.Log("[ModSettingsUI] Rules: default selected, loading from default folder");
                    if (!TryHotReloadViaSteamAndSpecialKey(defaultFolder, defaultFolder) && !TryHotReloadRules(defaultFolder))
                        PromptRestartForRules();
                });

                int found = 0;
                foreach (var root in roots)
                {
                    foreach (var dir in Directory.GetDirectories(root))
                    {
                        var displayName = Path.GetFileName(dir);
                        if (string.Equals(displayName, "Default", StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // default handled separately
                        }
                        found++;
                        addBtn($"Load Mod: {displayName}", () =>
                        {
                            ModOptions.Instance.SelectedRulesDir = displayName;
                            ModOptions.Instance.Save();
                            ModOptions.Instance.ReloadFromDisk();

                            FileLog.Log("[ModSettingsUI] Rules: switched to " + displayName + ", loading rule set without copying");
                            if (!TryHotReloadViaSteamAndSpecialKey(dir, defaultFolder) && !TryHotReloadRules(dir))
                                PromptRestartForRules();
                        });
                    }
                }

                if (found == 0)
                {
                    FileLog.Log("[ModSettingsUI] No mod ZXRules.dat found under Mods/Modfiles");
                }

                grid.Update();
            }
            catch (Exception e)
            {
                FileLog.Log("[ModSettingsUI] Error in AddRulesPage: " + e);
            }
        }

        private static void CopyRuleSetWithFallback(string sourceDir, string defaultDir, string targetRoot, string[] files)
        {
            try
            {
                foreach (var file in files)
                {
                    string src = Path.Combine(sourceDir, file);
                    string fallback = Path.Combine(defaultDir, file);
                    string target = Path.Combine(targetRoot, file);
                    string chosen = File.Exists(src) ? src : fallback;
                    if (!File.Exists(chosen))
                    {
                        FileLog.Log("[ModSettingsUI] Missing file (no fallback): " + file);
                        continue;
                    }
                    File.Copy(chosen, target, true);
                    FileLog.Log("[ModSettingsUI] Copied " + chosen + " -> " + target);
                }
            }
            catch (Exception ex)
            {
                FileLog.Log("[ModSettingsUI] CopyRuleSetWithFallback failed: " + ex);
            }
        }

        private static MethodInfo FindMethodDeep(Type type, string name)
        {
            Type t = type;
            while (t != null)
            {
                MethodInfo found = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                    .FirstOrDefault(m => m.Name == name);
                if (found != null) return found;
                t = t.BaseType;
            }
            return null;
        }

        private static object[] BuildArgs(ParameterInfo[] ps, string preferredEnumName)
        {
            object[] args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                Type pt = ps[i].ParameterType;
                if (pt.IsEnum)
                {
                    args[i] = GetEnumOrDefault(pt, preferredEnumName);
                }
                else if (!pt.IsValueType || Nullable.GetUnderlyingType(pt) != null)
                {
                    args[i] = null;
                }
                else
                {
                    args[i] = Activator.CreateInstance(pt);
                }
            }
            return args;
        }

        private static object GetEnumOrDefault(Type enumType, string preferred)
        {
            if (enumType == null || !enumType.IsEnum) return null;
            try { if (Enum.GetNames(enumType).Contains(preferred)) return Enum.Parse(enumType, preferred); } catch { }
            try { Array vals = Enum.GetValues(enumType); if (vals.Length > 0) return vals.GetValue(0); } catch { }
            return null;
        }

        private static void ShowToast(string message)
        {
            var glType = AccessToolsEX.TypeByNameWithDecrypt("ZX.ZXSystem_GameLevel");
            var current = Traverse.Create(glType).MethodWithDecrypt("get_Current").GetValue();
            if (current != null)
            {
                Traverse.Create(current).MethodWithDecrypt("ShowMessage", new Type[] { typeof(string), typeof(System.Drawing.Color), typeof(int) })
                    .GetValue(message, System.Drawing.Color.White, 2000);
            }
        }

        private static bool TryHotReloadRules(string sourceDir = null)
        {
            try
            {
                var zxGameType = AccessToolsEX.TypeByNameWithDecrypt("ZX.ZXGame");
                if (zxGameType == null) { FileLog.Log("[ModSettingsUI] HotReload: ZXGame type null"); return false; }
                var current = Traverse.Create(zxGameType).PropertyWithDecrypt("Current").GetValue();
                if (current == null) { FileLog.Log("[ModSettingsUI] HotReload: ZXGame.Current null"); return false; }

                string baseDir = sourceDir ?? Directory.GetCurrentDirectory();
                string rulesRoot = Directory.Exists(baseDir) ? baseDir : Directory.GetCurrentDirectory();

                string rulesX = Path.Combine(rulesRoot, "ZXRules.xlsx");
                string stringsX = Path.Combine(rulesRoot, "ZXStrings.xlsx");
                string campaignX = Path.Combine(rulesRoot, "ZXCampaign.xlsx");
                if (!File.Exists(rulesX) || !File.Exists(stringsX) || !File.Exists(campaignX))
                {
                    FileLog.Log("[ModSettingsUI] HotReload: missing xlsx file(s) in " + rulesRoot);
                    return false;
                }

                var tmDef = DXTableManager.FromExcel(rulesX, false);
                var tmStr = DXTableManager.FromExcel(stringsX, false);
                var tmCamp = DXTableManager.FromExcel(campaignX, false);

                var trav = Traverse.Create(current);
                trav.Property("TableManagerDefinitions").SetValue(tmDef);
                trav.Property("TableManagerStrings").SetValue(tmStr);
                trav.Property("TableManagerCampaign").SetValue(tmCamp);

                // Update campaign strings tables if available
                var updCampaign = AccessTools.Method(zxGameType, "UpdateCampaignStringsTables");
                updCampaign?.Invoke(current, null);

                // Refresh defaults per template
                var proj = trav.Property("CurrentProject").GetValue();
                var dicDefaults = trav.Field("DicDefaultParamsPerTemplate").GetValue<Dictionary<string, object>>();
                if (proj != null && dicDefaults != null)
                {
                    var entTemplates = Traverse.Create(proj).Property("EntityTemplates").GetValue<IDictionary>();
                    if (entTemplates != null)
                    {
                        foreach (DictionaryEntry kv in entTemplates)
                        {
                            var tmpl = kv.Value;
                            var ent = Traverse.Create(tmpl).Property("Entity").GetValue();
                            if (ent != null && ent.GetType().Name == "ZXEntity")
                            {
                                string name = Traverse.Create(tmpl).Property("Name").GetValue<string>();
                                if (name != null && dicDefaults.TryGetValue(name, out var defObj) && defObj != null)
                                {
                                    Traverse.Create(defObj).Method("ReadParameters", new Type[] { typeof(string) }).GetValue(name);
                                }
                            }
                        }
                    }
                }

                // Refresh command params
                var commands = trav.Field("Commands").GetValue<IEnumerable>();
                if (commands != null)
                {
                    foreach (var cmd in commands)
                    {
                        Traverse.Create(cmd).Method("ReadParams").GetValue();
                    }
                }

                // Update themes/campaign caches
                var mapTheme = AccessToolsEX.TypeByNameWithDecrypt("ZX.ZXMapTheme");
                AccessTools.Method(mapTheme, "UpdateThemes", new Type[] { typeof(bool) })?.Invoke(null, new object[] { false });
                var campaign = AccessToolsEX.TypeByNameWithDecrypt("ZX.ZXCampaign");
                AccessTools.Method(campaign, "LoadAll")?.Invoke(null, null);

                FileLog.Log("[ModSettingsUI] HotReload: finished via full paths");
                return true;
            }
            catch (Exception ex)
            {
                FileLog.Log("[ModSettingsUI] HotReload failed: " + ex);
                return false;
            }
        }

        private static bool TryHotReloadViaSteamAndSpecialKey(string sourceDir, string defaultDir)
        {
            try
            {
                var zxGameType = AccessToolsEX.TypeByNameWithDecrypt("ZX.ZXGame");
                var current = Traverse.Create(zxGameType).PropertyWithDecrypt("Current").GetValue();
                if (current == null) { FileLog.Log("[ModSettingsUI] HotReloadSteam: ZXGame.Current null"); return false; }

                string rulesRoot = Directory.Exists(sourceDir) ? sourceDir : Directory.GetCurrentDirectory();
                string fallbackRoot = Directory.Exists(defaultDir) ? defaultDir : Directory.GetCurrentDirectory();

                string rulesDat    = File.Exists(Path.Combine(rulesRoot, "ZXRules.dat"))    ? Path.Combine(rulesRoot, "ZXRules.dat")    : Path.Combine(fallbackRoot, "ZXRules.dat");
                string stringsDat  = File.Exists(Path.Combine(rulesRoot, "ZXStrings.dat"))  ? Path.Combine(rulesRoot, "ZXStrings.dat")  : Path.Combine(fallbackRoot, "ZXStrings.dat");
                string campaignDat = File.Exists(Path.Combine(rulesRoot, "ZXCampaign.dat")) ? Path.Combine(rulesRoot, "ZXCampaign.dat") : Path.Combine(fallbackRoot, "ZXCampaign.dat");

                if (!File.Exists(rulesDat))
                {
                    FileLog.Log("[ModSettingsUI] HotReloadSteam: missing ZXRules.dat in " + rulesRoot);
                    return false;
                }

                Exception lastEx = null;
                DXTableManager tmDef = LoadTableWithPasswordFallback(rulesDat,    "2025656990-254722460-3866451362025656990-254722460-386645136334454FADSFASDF45345", ref lastEx);
                DXTableManager tmStr = LoadTableWithPasswordFallback(stringsDat,  "2025656990-254722460-3866451362025656990-254722460-386645136334454FADSFASDF45345", ref lastEx);
                DXTableManager tmCamp= LoadTableWithPasswordFallback(campaignDat, "1688788812-163327433-2005584771", ref lastEx);

                if (tmDef == null)
                {
                    FileLog.Log("[ModSettingsUI] HotReloadSteam: failed to load ZXRules");
                    return false;
                }
                if (tmDef != null)
                {
                    string rulesX    = Path.Combine(rulesRoot, "ZXRules.xlsx");
                    tmDef.ToExcel(rulesX);
                    FileLog.Log("[ModSettingsUI] ZXRules.xlsx written to " + rulesX);
                }
                else
                {
                    FileLog.Log("Failed to find ZXRules");
                }
                if (tmStr != null)
                {
                    string stringsX = Path.Combine(rulesRoot, "ZXStrings.xlsx");
                    tmStr.ToExcel(stringsX);
                    FileLog.Log("[ModSettingsUI] ZXStrings.xlsx written to " + stringsX);
                }
                else
                {
                    FileLog.Log("Failed to find ZXStrings.dat");
                }
                if (tmCamp != null)
                {
                    string campaignX = Path.Combine(rulesRoot, "ZXCampaign.xlsx");
                    tmCamp.ToExcel(campaignX);
                    FileLog.Log("[ModSettingsUI] ZXCampaign.xlsx written to " + campaignX);
                }
                else
                {
                    FileLog.Log("Failed to find ZXCampaign.dat");
                }

                MethodInfo proc = AccessTools.Method(zxGameType, "ProcessSpecialKeys_KeyUp", new[] { typeof(DXKeys) }) ??
                                zxGameType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                            .FirstOrDefault(m => m.Name.Contains("ProcessSpecialKeys_KeyUp") && m.GetParameters().Length == 1);
                if (proc == null)
                {
                    FileLog.Log("[ModSettingsUI] HotReloadSteam: ProcessSpecialKeys_KeyUp not found");
                    return true;
                }

                string prevDir = Directory.GetCurrentDirectory();
                try
                {
                    Directory.SetCurrentDirectory(rulesRoot);
                    proc.Invoke(current, new object[] { (DXKeys)122 });
                    FileLog.Log("[ModSettingsUI] HotReloadSteam: ProcessSpecialKeys_KeyUp(122) invoked with cwd=" + rulesRoot);
                }
                finally
                {
                    Directory.SetCurrentDirectory(prevDir);
                }

                return true;
            }
            catch (Exception ex)
            {
                FileLog.Log("[ModSettingsUI] HotReloadSteam failed: " + ex);
                return false;
            }
        }

        // Helper to load a DXTableManager from DAT using crypted, plain, or custom password paths.
        private static DXTableManager LoadTableWithPasswordFallback(string datPath, string password, ref Exception lastEx)
        {
            DXTableManager tm = null;
            try { tm = DXTableManager.FromDatFileCrypted(datPath); } catch (Exception ex) { lastEx = ex; }
            if (tm != null) return tm;
            try { tm = DXTableManager.FromDatFile(datPath); } catch (Exception ex) { lastEx = ex; }
            if (tm != null) return tm;

            try
            {
                var zipType = AccessToolsEX.TypeByNameWithDecrypt("DXVision.Serialization.ZipSerializer") ?? typeof(ZipSerializer);
                var currentInst = AccessTools.Property(zipType, "Current")?.GetValue(null, null);
                var pwdProp = AccessTools.Property(zipType, "Password");
                string oldPwd = pwdProp?.GetValue(currentInst, null) as string;
                pwdProp?.SetValue(currentInst, password, null);
                var deser = AccessTools.Method(zipType, "Deserialize", new[] { typeof(string), typeof(string) });
                var obj = deser?.Invoke(currentInst, new object[] { datPath, null });
                tm = obj as DXTableManager;
                pwdProp?.SetValue(currentInst, oldPwd, null);
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
            return tm;
        }


        private static void PromptRestartForRules()
        {
            // Skip runtime reload attempts; just prompt restart
            if (ShowRestartDialogInGame())
                return;
            ShowToast("Restart required. Restart manually to apply rules.");
        }

        private static bool ShowRestartDialogInGame()
        {
            try
            {
                var mbType = AccessToolsEX.TypeByNameWithDecrypt("ZX.GUI.ZXMessageBox");
                if (mbType == null) { FileLog.Log("[ModSettingsUI] Restart dialog: ZXMessageBox type null"); return false; }
                MethodInfo showOk =
                    AccessToolsEX.MethodWithDecrypt(mbType, "ShowOK", new Type[] { typeof(string), typeof(string), typeof(Action) }) ??
                    AccessTools.Method(mbType, "ShowOK", new Type[] { typeof(string), typeof(string), typeof(Action) }) ??
                    mbType.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m =>
                        m.Name.Contains("ShowOK") &&
                        m.GetParameters().Length == 3 &&
                        typeof(Delegate).IsAssignableFrom(m.GetParameters()[2].ParameterType));
                FileLog.Log("[ModSettingsUI] Restart dialog ShowOK: " + (showOk == null ? "null" : showOk.ToString()));
                if (showOk == null) return false;

                Action onOk = () =>
                {
                    try
                    {
                        FileLog.Log("[ModSettingsUI] Restart dialog OK clicked");
                        var zxGameType = AccessToolsEX.TypeByNameWithDecrypt("ZX.ZXGame");
                        var zxGameStateType = AccessToolsEX.TypeByNameWithDecrypt("ZX.ZXGameState");
                        var dxGameType = AccessToolsEX.TypeByNameWithDecrypt("DXVision.DXGame");

                        var restart = AccessToolsEX.MethodWithDecrypt(zxGameType, "Restart") ?? AccessTools.Method(zxGameType, "Restart");
                        var saveCfg = AccessToolsEX.MethodWithDecrypt(dxGameType, "SaveConfiguration") ?? AccessTools.Method(dxGameType, "SaveConfiguration");
                        var dxCurrent = AccessToolsEX.MethodWithDecrypt(dxGameType, "get_Current")?.Invoke(null, null) ?? AccessTools.Method(dxGameType, "get_Current")?.Invoke(null, null);

                        var gameState = Traverse.Create(zxGameStateType).MethodWithDecrypt("get_Current").GetValue();
                        var zxCurrent = Traverse.Create(zxGameType).MethodWithDecrypt("get_Current").GetValue();
                        var saveGame = AccessToolsEX.MethodWithDecrypt(zxGameType, "SaveGame", new Type[] { typeof(string), typeof(Action), typeof(bool), typeof(bool) }) ??
                                       AccessTools.Method(zxGameType, "SaveGame", new Type[] { typeof(string), typeof(Action), typeof(bool), typeof(bool) });

                        if (gameState != null && saveGame != null && zxCurrent != null)
                        {
                            string name = Traverse.Create(gameState).PropertyWithDecrypt("Name").GetValue<string>();
                            FileLog.Log("[ModSettingsUI] Restart: saving game " + name);
                            saveGame.Invoke(zxCurrent, new object[]
                            {
                                name,
                                (Action)(() =>
                                {
                                    try { saveCfg?.Invoke(dxCurrent, null); } catch { }
                                    try { restart?.Invoke(null, null); } catch { }
                                }),
                                true,
                                true
                            });
                            return;
                        }

                        // fallback: just save config and restart
                        try { saveCfg?.Invoke(dxCurrent, null); } catch { }
                        try { restart?.Invoke(null, null); } catch { }
                    }
                    catch
                    {
                        Environment.Exit(-1);
                    }
                };

                var zxGameTypeForW = AccessToolsEX.TypeByNameWithDecrypt("ZX.ZXGame");
                Func<string, string> W = id =>
                {
                    try { return Traverse.Create(zxGameTypeForW).MethodWithDecrypt("W", new Type[] { typeof(string) }).GetValue<string>(id); }
                    catch { return null; }
                };
                string title = W("RestartGameTitle") ?? "Restart Required";
                string text = W("RestartGameText") ?? "Game requires restart to apply the selected rules.";

                FileLog.Log("[ModSettingsUI] Showing restart dialog");
                showOk.Invoke(null, new object[] { title, text, onOk });
                return true;
            }
            catch (Exception ex)
            {
                FileLog.Log("[ModSettingsUI] ShowRestartDialogInGame failed: " + ex);
                return false;
            }
        }

        private static void RestartGameExecutable()
        {
            string exe = Path.Combine(Directory.GetCurrentDirectory(), "TheyAreBillions.exe");
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = true
                });
            }
            catch (Exception startEx)
            {
                FileLog.Log("[ModSettingsUI] Failed to start game for restart: " + startEx);
            }
        }

        private static void LogMethods(Type t)
        {
            try
            {
                var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                               .Select(m => m.Name + " (" + m.GetParameters().Length + " params)")
                               .Distinct();
                }
            catch { }
        }

        private static bool CopyRulesFile(string sourcePath, string destPath)
        {
            try
            {
                using (var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    src.CopyTo(dst);
                }
                FileLog.Log("[ModSettingsUI] Copied rules: " + sourcePath + " -> " + destPath);
                return true;
            }
            catch (Exception ex)
            {
                FileLog.Log("[ModSettingsUI] CopyRulesFile failed: " + ex);
                return false;
            }
        }

        private static IEnumerable<(Func<string> Text, Action Toggle)> BuildEntries(ModOptions o)
        {
            Func<bool, string> OnOff = v => v ? "On" : "Off";

            yield return (() => "KeepDisplayAllLifeMeters: " + OnOff(o.KeepDisplayAllLifeMeters), () => o.KeepDisplayAllLifeMeters = !o.KeepDisplayAllLifeMeters);
            yield return (() => "AutoGoWatchTower: " + OnOff(o.AutoGoWatchTower), () => o.AutoGoWatchTower = !o.AutoGoWatchTower);
            yield return (() => "AutoGoWatchTowerRadius: " + o.AutoGoWatchTowerRadius, () => o.AutoGoWatchTowerRadius = NextFromList(o.AutoGoWatchTowerRadius, WatchTowerRadii));
            yield return (() => "NumpadFilterVeteran: " + OnOff(o.NumpadFilterVeteran), () => o.NumpadFilterVeteran = !o.NumpadFilterVeteran);
            yield return (() => "GameSpeedChange: " + OnOff(o.GameSpeedChange), () => o.GameSpeedChange = !o.GameSpeedChange);
            yield return (() => "FilterVeteranUnit: " + OnOff(o.FilterVeteranUnit), () => o.FilterVeteranUnit = !o.FilterVeteranUnit);
            yield return (() => "AutoFindBonusItem: " + OnOff(o.AutoFindBonusItem), () => o.AutoFindBonusItem = !o.AutoFindBonusItem);
            yield return (() => "AutoDisperse: " + OnOff(o.AutoDisperse), () => o.AutoDisperse = !o.AutoDisperse);
            yield return (() => "FastAttack: " + OnOff(o.FastAttack), () => o.FastAttack = !o.FastAttack);
            yield return (() => "OptimizeTrainingSequence: " + OnOff(o.OptimizeTrainingSequence), () => o.OptimizeTrainingSequence = !o.OptimizeTrainingSequence);
            yield return (() => "DestroyAllSelectedUnits: " + OnOff(o.DestroyAllSelectedUnits), () => o.DestroyAllSelectedUnits = !o.DestroyAllSelectedUnits);
            yield return (() => "GameMenuEnhancer: " + OnOff(o.GameMenuEnhancer), () => o.GameMenuEnhancer = !o.GameMenuEnhancer);
            yield return (() => "OptimizeAttackPriority: " + OnOff(o.OptimizeAttackPriority), () => o.OptimizeAttackPriority = !o.OptimizeAttackPriority);
            yield return (() => "CancelResearchAnytime: " + OnOff(o.CancelResearchAnytime), () => o.CancelResearchAnytime = !o.CancelResearchAnytime);
            yield return (() => "DisableAutoSave: " + OnOff(o.DisableAutoSave), () => o.DisableAutoSave = !o.DisableAutoSave);
            yield return (() => "AutoSaveInterval: " + o.AutoSaveInterval + "s", () => o.AutoSaveInterval = NextFromList(o.AutoSaveInterval, AutoSaveIntervals));
            yield return (() => "MaxSaveBackup: " + o.MaxSaveBackup, () => o.MaxSaveBackup = NextFromList(o.MaxSaveBackup, BackupCounts));
            yield return (() => "AutoDeleteBackups: " + OnOff(o.AutoDeleteBackups), () => o.AutoDeleteBackups = !o.AutoDeleteBackups);
            yield return (() => "EnhancedSelection: " + OnOff(o.EnhancedSelection), () => o.EnhancedSelection = !o.EnhancedSelection);
            yield return (() => "DeselectUnitsAfterTowerSearch: " + OnOff(o.DeselectUnitsAfterTowerSearch), () => o.DeselectUnitsAfterTowerSearch = !o.DeselectUnitsAfterTowerSearch);
            yield return (() => "BatchCancelCommand: " + OnOff(o.BatchCancelCommand), () => o.BatchCancelCommand = !o.BatchCancelCommand);
            yield return (() => "QuickBuyResource: " + OnOff(o.QuickBuyResource), () => o.QuickBuyResource = !o.QuickBuyResource);
            yield return (() => "QuickBuyResourceDelay: " + o.QuickBuyResourceDelay + "ms", () => o.QuickBuyResourceDelay = NextFromList(o.QuickBuyResourceDelay, QuickBuyDelays));
        }

        private static int NextFromList(int current, List<int> candidates)
        {
            int idx = candidates.IndexOf(current);
            if (idx < 0 || idx + 1 >= candidates.Count) return candidates[0];
            return candidates[idx + 1];
        }

        private static void RefreshCurrentMenu(object instance)
        {
            try
            {
                Type t = instance.GetType();
                Type enumType = t.GetNestedType("TMenuCategory", BindingFlags.Public | BindingFlags.NonPublic);
                object current = Traverse.Create(instance).FieldWithDecrypt("_CurrentCategory").GetValue();
                if (enumType == null || current == null) return;
                MethodInfo updateMenu = AccessTools.Method(t, "UpdateMenu", new Type[] { enumType });
                if (updateMenu != null) updateMenu.Invoke(instance, new object[] { current });
            }
            catch { }
        }

        private static void TryAddToMenuCategories(object instance, DXTextButton modButton)
        {
            try
            {
                IDictionary menuCategories = Traverse.Create(instance).FieldWithDecrypt("_MenuCategories").GetValue<IDictionary>();
                if (menuCategories == null) return;
                Type enumType = instance.GetType().GetNestedType("TMenuCategory", BindingFlags.Public | BindingFlags.NonPublic);
                if (enumType == null) return;
                object mainValue = Enum.Parse(enumType, "Main");
                IList list = menuCategories[mainValue] as IList;
                if (list == null) return;
                if (!list.Contains(modButton)) list.Add(modButton);
            }
            catch { }
        }
    }
}
