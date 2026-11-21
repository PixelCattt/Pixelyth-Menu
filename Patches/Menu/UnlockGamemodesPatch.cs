using System.Collections.Generic;
using GorillaGameModes;
using HarmonyLib;

namespace Pixelyth.Patches.Menu
{
    [HarmonyPatch(typeof(GameModeZoneMapping), nameof(GameModeZoneMapping.GetModesForZone))]
    public static class UnlockGamemodesPatch
    {
        public static bool enabled;

        private static void Postfix(ref HashSet<GameModeType> __result)
        {
            if (!enabled || __result == null) return;
            var modes = new HashSet<GameModeType>(__result)
            {
                GameModeType.HuntDown,
                GameModeType.Paintbrawl,
                GameModeType.Ambush,
                GameModeType.FreezeTag,
                GameModeType.Ghost,
                GameModeType.Guardian
            };
            __result = modes;
        }
    }
}
