using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;

namespace UltraCinematic.Integration
{
    internal static class CinematicCheatRegistrar
    {
        private static CheatsManager registeredManager;
        private static CheatBinds restoredBindHost;
        private static readonly HashSet<string> bindRestoreAttempts = new HashSet<string>();

        internal static void TryRegister(CheatsManager manager, ManualLogSource log)
        {
            if (!IsLiveManager(manager)) return;
            if (registeredManager == manager)
            {
                UltraCinematicPlugin.Controller?.MenuCoordinator.Attach(manager);
                return;
            }
            try
            {
                if (UltraCinematicPlugin.EnsureRuntime(manager) == null) return;
                ICheat[] cheats = { new CinematicEditCheat(), new AddCameraPointCheat(), new DeleteLastPointCheat(), new OpenTimelineCheat(), new StartCinematicCheat(), new PauseGameCheat() };
                Dictionary<string, List<ICheat>> categories = GetCategories(manager);
                List<ICheat> cinematic;
                if (categories != null && categories.TryGetValue("CINEMATIC", out cinematic))
                {
                    foreach (ICheat cheat in cheats)
                        if (!cinematic.Any(existing => existing.Identifier == cheat.Identifier)) cinematic.Add(cheat);
                    cheats = cinematic.ToArray();
                }
                else manager.RegisterCheats(cheats, "Cinematic");

                registeredManager = manager;
                manager.RebuildMenu();
                UltraCinematicPlugin.Controller?.MenuCoordinator.Attach(manager);

                RestoreMissingBinds(cheats, log);
                log.LogInfo("Registered six UltraCinematic cheats.");
            }
            catch (Exception error) { log.LogError("Cinematic registration failed: " + error); }
        }

        private static bool IsLiveManager(CheatsManager manager)
        {
            if (manager == null || manager.gameObject == null || manager.gameObject.name != "Cheat Menu") return false;
            CheatsManager singleton = MonoSingleton<CheatsManager>.Instance;
            return singleton != null && singleton == manager;
        }

        private static void RestoreMissingBinds(ICheat[] cheats, ManualLogSource log)
        {
            CheatBinds binds = MonoSingleton<CheatBinds>.Instance;
            if (binds == null || binds.registeredCheatBinds == null) return;
            if (restoredBindHost != binds)
            {
                restoredBindHost = binds;
                bindRestoreAttempts.Clear();
            }

            List<ICheat> missing = new List<ICheat>();
            foreach (ICheat cheat in cheats)
                if (!binds.registeredCheatBinds.ContainsKey(cheat.Identifier) && bindRestoreAttempts.Add(cheat.Identifier)) missing.Add(cheat);
            if (missing.Count == 0) return;

            try { binds.RestoreBinds(new Dictionary<string, List<ICheat>> { { "CINEMATIC", missing } }); }
            catch (ArgumentException error) { log.LogWarning("Duplicate cinematic bind restore was skipped: " + error.Message); }
        }

        private static Dictionary<string, List<ICheat>> GetCategories(CheatsManager manager)
        {
            FieldInfo field = typeof(CheatsManager).GetField("allRegisteredCheats", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(manager) as Dictionary<string, List<ICheat>>;
        }
    }
}
