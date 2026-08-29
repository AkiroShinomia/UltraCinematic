using BepInEx.Logging;
using UltraCinematic.Data;
using UnityEngine;

namespace UltraCinematic.Core
{
    internal sealed class PhotoPauseMovementController
    {
        internal const string StateKey = "ultracinematic-photo-pause";
        private readonly ManualLogSource log;
        private readonly PlayerPlaybackController passenger;
        private UnityEngine.Camera camera;
        private float previousTimeScale = 1f;
        private float yaw;
        private float pitch;

        internal bool Active { get; private set; }
        internal CameraState CameraState { get; private set; }

        internal PhotoPauseMovementController(ManualLogSource logger)
        {
            log = logger;
            passenger = new PlayerPlaybackController(log);
        }

        internal bool Begin(UnityEngine.Camera targetCamera)
        {
            if (Active) return true;
            if (targetCamera == null || !passenger.Begin(targetCamera)) return false;
            camera = targetCamera;
            Vector3 euler = camera.transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = NormalizeAngle(euler.x);
            CameraState = new CameraState(camera.transform.position, Quaternion.Euler(pitch, yaw, 0f), camera.fieldOfView);
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (GameStateManager.Instance != null && !GameStateManager.Instance.IsStateActive(StateKey))
                GameStateManager.Instance.RegisterState(new GameState(StateKey)
                {
                    playerInputLock = LockMode.Lock,
                    cameraInputLock = LockMode.Lock,
                    cursorLock = LockMode.Lock,
                    timerModifier = 0f
                });
            Active = true;
            log.LogInfo("Pause Game enabled with unscaled cinematic movement.");
            return true;
        }

        internal CameraState Tick()
        {
            if (!Active || camera == null) return CameraState;
            CheatsManager cheats = MonoSingleton<CheatsManager>.Instance;
            if (cheats != null && cheats.IsMenuOpen()) return CameraState;

            float dt = Time.unscaledDeltaTime;
            yaw += Input.GetAxisRaw("Mouse X") * 2.5f;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 2.5f, -89f, 89f);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 movement = rotation * Vector3.forward * Input.GetAxisRaw("Vertical") + rotation * Vector3.right * Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.Space)) movement += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) movement += Vector3.down;
            if (movement.sqrMagnitude > 1f) movement.Normalize();
            float speed = Input.GetKey(KeyCode.LeftShift) ? 24f : 8f;
            CameraState = new CameraState(CameraState.Position + movement * speed * dt, rotation, CameraState.FieldOfView);
            passenger.FollowUpright(CameraState);
            return CameraState;
        }

        internal void End()
        {
            if (!Active) return;
            Active = false;
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsStateActive(StateKey))
                GameStateManager.Instance.PopState(StateKey);
            passenger.Restore();
            if (camera != null)
            {
                CameraController cameraController = camera.GetComponent<CameraController>();
                if (cameraController != null)
                {
                    cameraController.ResetCamera(yaw, pitch);
                    cameraController.ApplyRotations();
                }
            }
            Time.timeScale = previousTimeScale;
            camera = null;
            log.LogInfo("Pause Game disabled; normal player control restored.");
        }

        private static float NormalizeAngle(float value)
        {
            while (value > 180f) value -= 360f;
            while (value < -180f) value += 360f;
            return value;
        }
    }
}
