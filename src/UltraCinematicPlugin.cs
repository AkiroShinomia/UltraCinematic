using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UltraCinematic.Configuration;
using UltraCinematic.Core;
using UnityEngine;

namespace UltraCinematic
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UltraCinematicPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "kiril.ultracinematic"; public const string PluginName = "UltraCinematic"; public const string PluginVersion = "1.6.1";
        internal static CinematicController Controller { get; private set; } internal static ManualLogSource Log { get; private set; }
        internal static UltraCinematicPreferences Preferences { get; private set; }
        private Harmony harmony;
        private bool applicationQuitting;
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Log = Logger;
            Preferences = new UltraCinematicPreferences(Config);
            harmony = new Harmony(PluginGuid); harmony.PatchAll(typeof(UltraCinematicPlugin).Assembly);
            Logger.LogInfo("UltraCinematic loaded.");
            Logger.LogInfo("Assembly path: " + Assembly.GetExecutingAssembly().Location);
            Logger.LogInfo("Assembly version: " + Assembly.GetExecutingAssembly().GetName().Version);
        }

        internal static CinematicController EnsureRuntime(CheatsManager manager)
        {
            if (manager == null) return null;
            GameObject host = manager.gameObject;
            if (Controller != null && Controller.gameObject != host)
            {
                Log?.LogError("Refusing to move UltraCinematic runtime away from the live Cheat Menu.");
                return null;
            }
            CinematicController controller = host.GetComponent<CinematicController>();
            if (controller == null) controller = host.AddComponent<CinematicController>();
            controller.Initialize(Log);
            UltraCinematicRuntimeHost runtimeHost = host.GetComponent<UltraCinematicRuntimeHost>();
            if (runtimeHost == null) runtimeHost = host.AddComponent<UltraCinematicRuntimeHost>();
            runtimeHost.Initialize(controller, Log);
            Controller = controller;
            return controller;
        }
        private void OnApplicationQuit() { applicationQuitting = true; harmony?.UnpatchSelf(); }
        private void OnDestroy()
        {
            if (!applicationQuitting)
            {
                Logger.LogWarning("UltraCinematicPlugin.OnDestroy reached before application quit; Harmony hooks are being preserved until CheatsManager creates the runtime.");
                return;
            }
            Controller = null;
            Preferences = null;
        }
    }
}
