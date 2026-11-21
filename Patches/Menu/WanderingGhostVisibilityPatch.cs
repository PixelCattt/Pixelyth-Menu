using HarmonyLib;
using Pixelyth.Mods;

namespace Pixelyth.Patches.Menu
{
    [HarmonyPatch(typeof(WanderingGhost), "Patrol")]
    public class WanderingGhostVisibilityPatch
    {
        public static bool enabled;

        public static void Postfix(WanderingGhost __instance)
        {
            if (!enabled || __instance != Overpowered._wanderer || __instance.mrenderer == null)
                return;

            __instance.mrenderer.enabled = true;
            __instance.mrenderer.sharedMaterial = __instance.visibleMaterial;
        }
    }
}
