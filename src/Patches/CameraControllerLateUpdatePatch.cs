using System;
using HarmonyLib;

namespace UltraCinematic.Patches
{
    [HarmonyPatch(typeof(CameraController), "LateUpdate")]
    internal static class CameraControllerLateUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try { UltraCinematicPlugin.Controller?.ApplyCameraState(); }
            catch (Exception error) { UltraCinematicPlugin.Log?.LogError("Camera state application failed: " + error); }
        }
    }
}
