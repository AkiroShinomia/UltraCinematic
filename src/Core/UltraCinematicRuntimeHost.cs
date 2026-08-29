using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UltraCinematic.Core
{
    internal sealed class UltraCinematicRuntimeHost : MonoBehaviour
    {
        private CinematicController controller;
        private bool initialized;

        internal void Initialize(CinematicController cinematicController, ManualLogSource logger)
        {
            if (initialized) return;
            initialized = true;
            controller = cinematicController;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            controller?.ClearForSceneChange();
        }

        private void OnApplicationQuit() => controller?.ClearForSceneChange();
        private void OnDestroy() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
}
