using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UltraCinematic.Core
{
    internal sealed class UltraCinematicRuntimeHost : MonoBehaviour
    {
        private CinematicController controller;
        private ManualLogSource log;
        private bool initialized;

        internal void Initialize(CinematicController cinematicController, ManualLogSource logger)
        {
            if (initialized) return;
            initialized = true;
            controller = cinematicController;
            log = logger;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (IsSameLevel(previous, next))
            {
                controller?.PrepareForSameLevelReload();
                log?.LogInfo("Preserved cinematic timeline across reload of scene '" + next.name + "'.");
            }
            else
            {
                controller?.ClearForSceneChange();
            }
        }

        private static bool IsSameLevel(Scene previous, Scene next)
        {
            if (!previous.IsValid() || !next.IsValid()) return false;
            return previous.buildIndex == next.buildIndex &&
                   string.Equals(previous.name, next.name, System.StringComparison.Ordinal) &&
                   string.Equals(previous.path, next.path, System.StringComparison.Ordinal);
        }

        private void OnApplicationQuit() => controller?.ClearForSceneChange();
        private void OnDestroy() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
}
