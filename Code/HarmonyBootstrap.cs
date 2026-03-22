using HarmonyLib;

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
            _applied = true;
            VulkanMod.LogInfo("Harmony patches applied.");
        }
    }
}
