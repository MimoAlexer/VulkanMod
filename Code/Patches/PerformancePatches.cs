using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace VulkanMod.Code.Patches
{
    [HarmonyPatch(typeof(DebugConfig), "isOn")]
    internal static class ForcePerfDebugOptionsPatch
    {
        private static void Postfix(DebugOption pOption, ref bool __result)
        {
            if (pOption == DebugOption.ParallelJobsUpdater || pOption == DebugOption.ParallelChunks)
            {
                __result = true;
                return;
            }

            if (pOption == DebugOption.ScaleEffectEnabled || pOption == DebugOption.LavaGlow)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(MapBox), "Update")]
    internal static class MapBoxUpdatePatch
    {
        private static void Prefix()
        {
            RuntimeTuner.ApplyFrameCriticalSettings();
        }
    }

    [HarmonyPatch(typeof(NameplateManager), "update")]
    internal static class DisableNameplateUpdatesPatch
    {
        private static bool Prefix(NameplateManager __instance)
        {
            RuntimeTuner.PrepareNameplatesForDisable(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(QuantumSpriteManager), "updateScaleEffect")]
    internal static class DisableQuantumScaleEffectPatch
    {
        private static bool Prefix()
        {
            RuntimeTuner.NoteQuantumScaleEffectsDisabled();
            return false;
        }
    }

    [HarmonyPatch(typeof(MapBox), "resetRedrawTimer")]
    internal static class ThrottleRedrawTimerResetPatch
    {
        private static bool Prefix(MapBox __instance)
        {
            __instance._redraw_timer = RuntimeTuner.GetMinimapRedrawInterval();
            return false;
        }
    }

    [HarmonyPatch(typeof(ActorManager), "precalculateRenderDataParallel")]
    internal static class ActorRenderBatchSizePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return BatchSizeTranspiler.ReplaceFixedBatchSize(instructions);
        }
    }

    [HarmonyPatch(typeof(BuildingManager), "precalculateRenderDataParallel")]
    internal static class BuildingRenderBatchSizePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return BatchSizeTranspiler.ReplaceFixedBatchSize(instructions);
        }
    }

    [HarmonyPatch(typeof(MapBox), "renderStuff")]
    internal static class MinimapRenderIntervalPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return FloatConstantTranspiler.ReplaceFixedMinimapInterval(instructions);
        }
    }

    internal static class BatchSizeTranspiler
    {
        private static readonly System.Reflection.MethodInfo BatchSizeMethod =
            AccessTools.Method(typeof(RuntimeTuner), "GetDynamicRenderBatchSize");

        internal static IEnumerable<CodeInstruction> ReplaceFixedBatchSize(IEnumerable<CodeInstruction> instructions)
        {
            bool replaced = false;

            foreach (CodeInstruction instruction in instructions)
            {
                if (!replaced && instruction.opcode == OpCodes.Ldc_I4 && Equals(instruction.operand, 256))
                {
                    replaced = true;
                    yield return new CodeInstruction(OpCodes.Call, BatchSizeMethod);
                    continue;
                }

                yield return instruction;
            }
        }
    }

    internal static class FloatConstantTranspiler
    {
        private static readonly System.Reflection.MethodInfo MinimapIntervalMethod =
            AccessTools.Method(typeof(RuntimeTuner), "GetMinimapRedrawInterval");

        internal static IEnumerable<CodeInstruction> ReplaceFixedMinimapInterval(IEnumerable<CodeInstruction> instructions)
        {
            bool replaced = false;

            foreach (CodeInstruction instruction in instructions)
            {
                object operand = instruction.operand;
                bool hasTargetValue = operand is float && Math.Abs((float)operand - 0.001f) < 0.0001f;

                if (!replaced && instruction.opcode == OpCodes.Ldc_R4 && hasTargetValue)
                {
                    replaced = true;
                    yield return new CodeInstruction(OpCodes.Call, MinimapIntervalMethod);
                    continue;
                }

                yield return instruction;
            }
        }
    }
}
