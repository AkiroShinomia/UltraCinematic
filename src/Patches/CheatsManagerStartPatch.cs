using System;
using HarmonyLib;
using UltraCinematic.Integration;

namespace UltraCinematic.Patches
{
    [HarmonyPatch(typeof(CheatsManager), "Start")]
    internal static class CheatsManagerStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CheatsManager __instance)
        {
            try { CinematicCheatRegistrar.TryRegister(__instance, UltraCinematicPlugin.Log); }
            catch (Exception error) { UltraCinematicPlugin.Log?.LogError("CheatsManager runtime bootstrap failed: " + error); }
        }
    }
}
