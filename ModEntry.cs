using DXVision;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TABModLoader;
using TABModLoader.Utils;
using static TABHelperMod.GamePatch;

namespace TABHelperMod
{
    public class ModEntry : ModBase
    {
        internal static Harmony harmonyInstance = new Harmony("com.example.SSR");
        public override void OnLoad(ModInfos modInfos)
        {
            try
            {
                ModOptions.Instance.Load(modInfos);

                // Add start-screen Mod Settings button (postfix)
                Type type = AccessToolsEX.TypeByNameWithDecrypt("ZX.ZXSystem_StartScreen");
                MethodInfo originalMethod = AccessToolsEX.MethodWithDecrypt(type, "OnLoad");
                HarmonyMethod postfixMethod = new HarmonyMethod(AccessTools.Method(typeof(ModSettingsUI), nameof(ModSettingsUI.OnStartScreenLoaded)));
                harmonyInstance.Patch(originalMethod, postfix: postfixMethod);

                originalMethod = AccessToolsEX.MethodWithDecrypt(type, "NewSurvivalGame", new Type[] { typeof(string) });
                var survivalTranspiler = new HarmonyMethod(AccessTools.Method(typeof(GamePatch), nameof(GamePatch.NewSurvivalGameTranspiler)));
                harmonyInstance.Patch(originalMethod, transpiler: survivalTranspiler);

                originalMethod = AccessToolsEX.MethodWithDecrypt(type, "NewSurvivalGame", new Type[] { typeof(string) });
                var infectedTranspiler = new HarmonyMethod(AccessTools.Method(typeof(GamePatch), nameof(GamePatch.NewSurvivalGameInfectedTranspiler)));
                harmonyInstance.Patch(originalMethod, transpiler: infectedTranspiler);
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }
    }
}
