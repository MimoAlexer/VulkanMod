using HarmonyLib;
using System.Linq;

namespace VulkanMod.Code
{
    internal static class HarmonyBootstrap
    {
        private const string HarmonyId = "lehma.vulkanmod.performance";
        private static bool _applied;

        internal static void Apply()
        {
            if (_applied)
            {
                return;
            }

            Harmony harmony = new Harmony(HarmonyId);
            harmony.PatchAll(typeof(HarmonyBootstrap).Assembly);
            int patchedMethodCount = System.Linq.Enumerable.Count(harmony.GetPatchedMethods());
            _applied = true;
            VulkanMod.LogInfo(
                string.Format(
                    "Harmony patches applied: id={0}, patchedMethods={1}.",
                    HarmonyId,
                    patchedMethodCount
                )
            );
        }
    }
}
