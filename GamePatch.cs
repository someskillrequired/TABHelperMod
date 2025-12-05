using DXVision;
using DXVision.GUI;
using EasySharpIni.Converters;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TABModLoader;
using TABModLoader.Utils;
using Fasterflect;
using static TABModLoader.Utils.Decryptor;
using System.Reflection.Emit;
using ZX.Entities;
using System.Collections;
using HarmonyLib;

namespace TABHelperMod
{
    public class GamePatch
    {
        // Inject extra Survival options into the duration list (Tiny 60 days) and leave infected list untouched.
        internal static IEnumerable<CodeInstruction> NewSurvivalGameTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var listAdd = AccessTools.Method(typeof(List<ValueTuple<float, string>>), "Add");
            if (listAdd == null)
            {
                FileLog.Log("[GamePatch] NewSurvivalGameTranspiler: missing Add method");
                return code;
            }

            var helper = AccessTools.Method(typeof(GamePatch), nameof(AddDurations));
            if (helper == null)
            {
                FileLog.Log("[GamePatch] NewSurvivalGameTranspiler: missing helper");
                return code;
            }

            int addCount = 0;
            int replaced = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (!code[i].Calls(listAdd))
                    continue;

                addCount++;

                // First 4 adds are durations; replace those with helper. Leave infected list (adds 5+) untouched.
                if (addCount <= 4)
                {
                    code[i].operand = helper;
                    code[i].opcode = OpCodes.Call;
                    replaced++;
                }
            }

            FileLog.Log("[GamePatch] NewSurvivalGameTranspiler: replaced " + replaced + " duration Add calls with AddDurations");
            return code;
        }

        private static bool loggedDurationIL = false;

        

        internal static void AddDurations(List<ValueTuple<float, string>> durations, ValueTuple<float, string> entry)
        {
            try
            {
                durations.Add(entry);
                if (Math.Abs(entry.Item1 - 0.8f) < 0.0001f && !durations.Any(d => Math.Abs(d.Item1 - 0.6f) < 0.0001f))
                {
                    durations.Add(new ValueTuple<float, string>(0.7f, "70 Days (Merciless)"));
                    durations.Add(new ValueTuple<float, string>(0.6f, "60 Days (Cruel)"));
                    durations.Add(new ValueTuple<float, string>(0.5f, "50 Days (No Rest)"));
                }
            }
            catch (Exception ex)
            {
                try { FileLog.Log("[GamePatch] AddDurations failed: " + ex); } catch { }
            }
        }

        // Inject two harder infected population options after the vanilla list (6 entries).
        internal static IEnumerable<CodeInstruction> NewSurvivalGameInfectedTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var tupleCtor = AccessTools.Constructor(typeof(ValueTuple<float, string>), new Type[] { typeof(float), typeof(string) });
            var listAdd = AccessTools.Method(typeof(List<ValueTuple<float, string>>), "Add");
            if (tupleCtor == null || listAdd == null)
            {
                FileLog.Log("[GamePatch] NewSurvivalGameInfectedTranspiler: missing tuple ctor or Add");
                return code;
            }

            int infectedStfldIndex = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Stfld)
                    continue;

                var fi = code[i].operand as FieldInfo;
                if (fi == null)
                    continue;

                if (fi.FieldType == typeof(List<ValueTuple<float, string>>))
                {
                    infectedStfldIndex++;
                    // First stfld of this type = durations list; second = infected list.
                    if (infectedStfldIndex == 2)
                    {
                        var inject = new List<CodeInstruction>
                        {
                            new CodeInstruction(OpCodes.Ldloc_0),
                            new CodeInstruction(OpCodes.Ldfld, fi),
                            new CodeInstruction(OpCodes.Ldc_R4, 1.7f),
                            new CodeInstruction(OpCodes.Ldstr, "Max+ (Population Apocalypse)"),
                            new CodeInstruction(OpCodes.Newobj, tupleCtor),
                            new CodeInstruction(OpCodes.Callvirt, listAdd),

                            new CodeInstruction(OpCodes.Ldloc_0),
                            new CodeInstruction(OpCodes.Ldfld, fi),
                            new CodeInstruction(OpCodes.Ldc_R4, 2.0f),
                            new CodeInstruction(OpCodes.Ldstr, "Max++ (Population Extinction)"),
                            new CodeInstruction(OpCodes.Newobj, tupleCtor),
                            new CodeInstruction(OpCodes.Callvirt, listAdd),

                            new CodeInstruction(OpCodes.Ldloc_0),
                            new CodeInstruction(OpCodes.Ldfld, fi),
                            new CodeInstruction(OpCodes.Ldc_R4, 2.2f),
                            new CodeInstruction(OpCodes.Ldstr, "Max+++ (Death Lovely Death)"),
                            new CodeInstruction(OpCodes.Newobj, tupleCtor),
                            new CodeInstruction(OpCodes.Callvirt, listAdd)
                            
                        };
                        code.InsertRange(i + 1, inject);
                        FileLog.Log("[GamePatch] NewSurvivalGameInfectedTranspiler: injected after infected stfld (second List<float,string>)");
                        return code;
                    }
                }
            }

            FileLog.Log("[GamePatch] NewSurvivalGameInfectedTranspiler: no injection point found (no second List<float,string> stfld)");
            return code;
        }
    }
}
