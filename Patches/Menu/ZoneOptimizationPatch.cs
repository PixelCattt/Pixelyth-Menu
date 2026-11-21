using GorillaNetworking;
using HarmonyLib;
using Pixelyth.Mods;

namespace Pixelyth.Patches.Menu
{
    [HarmonyPatch(typeof(ZoneManagement), nameof(ZoneManagement.SetActiveZones))]
    public static class ZoneOptimizationPatch
    {
        public static bool enabled;

        private static void Prefix(ref GTZone[] zones)
        {
            if (enabled && zones != null && zones.Length > 1)
                zones = new[] { zones[zones.Length - 1] };
        }
    }

    [HarmonyPatch(typeof(ZoneManagement), nameof(ZoneManagement.AddSceneToForceStayLoaded))]
    public static class ForceStayLoadedPatch
    {
        private static bool Prefix(string sceneName) => !ZoneOptimizationPatch.enabled || sceneName != "City";
    }

    [HarmonyPatch(typeof(GorillaNetworkJoinTrigger), nameof(GorillaNetworkJoinTrigger.OnBoxTriggered))]
    public static class OptimizeNetworkTriggerPatch
    {
        private static void Postfix(GorillaNetworkJoinTrigger __instance)
        {
            if (ZoneOptimizationPatch.enabled && __instance != null)
                Important.OnOptimizationZoneTrigger(__instance);
        }
    }
}
