using System;
using HarmonyLib;
using UltraCinematic.Integration;

namespace UltraCinematic.Patches
{
    [HarmonyPatch(typeof(CheatsManager), nameof(CheatsManager.ShowMenu))]
    internal static class CheatsManagerShowMenuPatch
    {
        [HarmonyPrefix]
        private static void Prefix(CheatsManager __instance)
        {
            try
            {
                CinematicCheatRegistrar.TryRegister(__instance, UltraCinematicPlugin.Log);
            }
            catch (Exception error) { UltraCinematicPlugin.Log?.LogError("Manage Cheats cinematic registration failed: " + error); }
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                UltraCinematicPlugin.Controller?.MenuCoordinator.Refresh();
                UltraCinematicPlugin.Controller?.MenuCoordinator.RequestRefresh();
            }
            catch (Exception error) { UltraCinematicPlugin.Log?.LogError("Manage Cheats cinematic refresh failed: " + error); }
        }
    }
}
