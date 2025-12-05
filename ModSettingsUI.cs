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
                    if (!TryHotReloadViaSteamAndSpecialKey(defaultFolder, defaultFolder))
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
                            if (!TryHotReloadViaSteamAndSpecialKey(dir, defaultFolder))
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
