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
}
