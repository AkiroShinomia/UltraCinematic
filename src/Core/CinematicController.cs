using BepInEx.Logging;
using UltraCinematic.Data;
using UltraCinematic.Timeline;
using UltraCinematic.UI;
using UltraCinematic.Visualization;
using UnityEngine;

namespace UltraCinematic.Core
{
    public sealed class CinematicController : MonoBehaviour
    {
        internal const string PlaybackStateKey = "ultracinematic-playback";
        internal const string TimelineStateKey = "ultracinematic-timeline";
        private ManualLogSource log;
        private PlayerPlaybackController playerPlayback;
        private PhotoPauseMovementController photoPause;
        private TimelineUiController timelineUi;
        private CinematicWorldVisualizer visualizer;
        private CinematicCheatMenuCoordinator menuCoordinator;
        private UnityEngine.Camera camera;
        private CameraState cameraTarget;
        private float playbackTime;
        private bool finishPlaybackNextUpdate;
        private bool previewActive;
        private bool timelineSessionActive;
        private float timeScaleBeforeTimeline = 1f;
        private bool frozenPlayback;
        private float timeScaleBeforeFrozenPlayback = 1f;
        private bool restorePhotoPauseAfterPlayback;
        private bool initialized;

        public bool EditModeEnabled { get; private set; }
        public bool PlaybackActive { get; private set; }
        public bool PhotoPauseActive => photoPause != null && photoPause.Active;
        public CinematicTimeline Timeline { get; } = new CinematicTimeline();
        public bool TimelineOpen => timelineUi != null && timelineUi.IsOpen;
        internal CinematicCheatMenuCoordinator MenuCoordinator => menuCoordinator;
        internal bool OwnsCamera => PlaybackActive || previewActive || PhotoPauseActive;

        internal void Initialize(ManualLogSource logger)
        {
            if (initialized) return;
            initialized = true;
            log = logger;
            playerPlayback = new PlayerPlaybackController(log);
            photoPause = new PhotoPauseMovementController(log);
            timelineUi = gameObject.AddComponent<TimelineUiController>();
            timelineUi.Initialize(this);
            visualizer = gameObject.AddComponent<CinematicWorldVisualizer>();
            visualizer.Initialize(this);
            menuCoordinator = gameObject.AddComponent<CinematicCheatMenuCoordinator>();
            menuCoordinator.Initialize(this);
        }

        public void EnableEditMode()
        {
            if (EditModeEnabled) return;
            EditModeEnabled = true;
            visualizer.Show();
            menuCoordinator.RequestRefresh();
            log.LogInfo("Cinematic Edit Mode enabled.");
        }

        public void DisableEditMode()
        {
            if (!EditModeEnabled) return;
            if (PlaybackActive)
            {
                restorePhotoPauseAfterPlayback = false;
                StopPlayback();
            }
            DisablePhotoPause();
            CloseTimeline();
            visualizer.Hide();
            EditModeEnabled = false;
            menuCoordinator.RequestRefresh();
            log.LogInfo("Cinematic Edit Mode disabled.");
        }

        public void AddCameraPoint()
        {
            if (!EditModeEnabled || PlaybackActive || !ResolveCamera()) return;
            Timeline.Add(new CameraPoint { Position = camera.transform.position, Rotation = camera.transform.rotation, FieldOfView = camera.fieldOfView });
            Timeline.CursorTime = Timeline.Duration;
            visualizer.Rebuild();
            menuCoordinator.RequestRefresh();
            log.LogInfo("Added Camera Point " + Timeline.Points.Count + ".");
        }

        public void DeleteLastPoint()
        {
            if (!EditModeEnabled || PlaybackActive) return;
            if (!Timeline.RemoveLast()) { log.LogWarning("Delete Last Point ignored: timeline is empty."); return; }
            ReleasePreview();
            visualizer.Rebuild();
            menuCoordinator.RequestRefresh();
            log.LogInfo("Deleted last Camera Point. Remaining: " + Timeline.Points.Count + ".");
        }

        public void ToggleTimeline()
        {
            if (!EditModeEnabled || PlaybackActive) return;
            DisablePhotoPause();
            if (timelineUi.IsOpen) CloseTimeline();
            else OpenTimeline();
        }

        public void EnablePhotoPause()
        {
            if (!EditModeEnabled || PlaybackActive || TimelineOpen || PhotoPauseActive || !ResolveCamera()) return;
            if (!photoPause.Begin(camera)) return;
            cameraTarget = photoPause.CameraState;
            CheatsManager manager = MonoSingleton<CheatsManager>.Instance;
            if (manager != null && manager.IsMenuOpen()) manager.HideMenu();
            menuCoordinator.RequestRefresh();
        }

        public void TogglePhotoPause()
        {
            if (PhotoPauseActive) DisablePhotoPause();
            else EnablePhotoPause();
        }

        public void DisablePhotoPause()
        {
            if (!PhotoPauseActive) return;
            photoPause.End();
            menuCoordinator.RequestRefresh();
        }

        public void StartPlayback()
        {
            if (!EditModeEnabled || PlaybackActive) return;
            if (Timeline.Keyframes.Count < 2) { log.LogWarning("Playback requires at least two Camera Points."); return; }
            restorePhotoPauseAfterPlayback = PhotoPauseActive;
            DisablePhotoPause();
            CloseTimeline();
            if (!ResolveCamera() || !playerPlayback.Begin(camera, restorePhotoPauseAfterPlayback))
            {
                RestorePhotoPauseIfNeeded();
                return;
            }
            ExitPlaybackGameState();
            frozenPlayback = Timeline.PlaybackMode == CinematicPlaybackMode.FrozenWorld;
            if (frozenPlayback)
            {
                timeScaleBeforeFrozenPlayback = Time.timeScale;
                Time.timeScale = 0f;
            }
            if (GameStateManager.Instance != null)
            {
                GameState playbackState = new GameState(PlaybackStateKey) { playerInputLock = LockMode.Lock, cameraInputLock = LockMode.Lock, cursorLock = LockMode.Lock };
                if (frozenPlayback) playbackState.timerModifier = 0f;
                GameStateManager.Instance.RegisterState(playbackState);
            }
            previewActive = false;
            visualizer.Hide();
            playbackTime = 0f;
            finishPlaybackNextUpdate = false;
            PlaybackActive = true;
            cameraTarget = TimelineEvaluator.Evaluate(Timeline, 0f);
            playerPlayback.Follow(cameraTarget);
            menuCoordinator.RequestRefresh();
            log.LogInfo("Started Cinematic Playback.");
        }

        public void StopPlayback()
        {
            if (!PlaybackActive) return;
            PlaybackActive = false;
            finishPlaybackNextUpdate = false;
            ExitPlaybackGameState();
            if (frozenPlayback) Time.timeScale = timeScaleBeforeFrozenPlayback;
            frozenPlayback = false;
            playerPlayback.Restore();
            RestorePhotoPauseIfNeeded();
            if (EditModeEnabled) visualizer.Show();
            menuCoordinator.RequestRefresh();
            log.LogInfo("Stopped Cinematic Playback and returned to Edit Mode.");
        }

        private void RestorePhotoPauseIfNeeded()
        {
            if (!restorePhotoPauseAfterPlayback) return;
            restorePhotoPauseAfterPlayback = false;
            if (!EditModeEnabled) return;
            EnablePhotoPause();
            if (PhotoPauseActive) log.LogInfo("Restored Pause Game state after Cinematic Playback.");
        }

        internal bool BeginTimelinePreview()
        {
            if (!timelineSessionActive || !EditModeEnabled || PlaybackActive || Timeline.Keyframes.Count == 0) return false;
            if (!ResolveCamera() || !playerPlayback.Begin(camera, true)) return false;
            previewActive = true;
            return true;
        }

        internal void PreviewTimelineAt(float time)
        {
            if (!previewActive && !BeginTimelinePreview()) return;
            Timeline.CursorTime = Mathf.Clamp(time, 0f, Timeline.Duration);
            cameraTarget = TimelineEvaluator.Evaluate(Timeline, Timeline.CursorTime);
            playerPlayback.Follow(cameraTarget);
            camera.transform.SetPositionAndRotation(cameraTarget.Position, cameraTarget.Rotation);
            camera.fieldOfView = cameraTarget.FieldOfView;
            visualizer.Rebuild();
        }

        internal void PreviewCameraPoint(CameraPoint point)
        {
            if (point == null || (!previewActive && !BeginTimelinePreview())) return;
            cameraTarget = new CameraState(point.Position, point.Rotation, point.FieldOfView);
            playerPlayback.Follow(cameraTarget);
            camera.transform.SetPositionAndRotation(cameraTarget.Position, cameraTarget.Rotation);
            camera.fieldOfView = cameraTarget.FieldOfView;
            visualizer.Rebuild();
        }

        internal void EndTimelinePreview()
        {
            if (!previewActive) return;
            previewActive = false;
            playerPlayback.Restore();
            visualizer.Rebuild();
        }

        internal void ReleasePreview() => EndTimelinePreview();
        internal void RefreshVisualization() => visualizer.Rebuild();
        internal void NotifyTimelineProjectChanged()
        {
            EndTimelinePreview();
            visualizer.Rebuild();
            menuCoordinator.RequestRefresh();
        }

        private void Update()
        {
            if (PhotoPauseActive)
            {
                cameraTarget = photoPause.Tick();
                return;
            }
            if (!PlaybackActive) return;
            if (finishPlaybackNextUpdate) { StopPlayback(); return; }
            float delta = frozenPlayback ? Time.unscaledDeltaTime : Time.deltaTime;
            playbackTime = Mathf.Min(playbackTime + delta, Timeline.Duration);
            cameraTarget = TimelineEvaluator.Evaluate(Timeline, playbackTime);
            playerPlayback.Follow(cameraTarget);
            if (playbackTime >= Timeline.Duration) finishPlaybackNextUpdate = true;
        }

        public void ApplyCameraState()
        {
            if (!OwnsCamera || camera == null) return;
            camera.transform.SetPositionAndRotation(cameraTarget.Position, cameraTarget.Rotation);
            camera.fieldOfView = cameraTarget.FieldOfView;
            CameraController cameraController = camera.GetComponent<CameraController>();
            if (cameraController != null) cameraController.StopShake();
        }

        public void ClearForSceneChange()
        {
            if (PlaybackActive) StopPlayback();
            DisablePhotoPause();
            CloseTimeline();
            EditModeEnabled = false;
            previewActive = false;
            Timeline.Clear();
            visualizer.Hide();
            camera = null;
            menuCoordinator.RequestRefresh();
            log.LogInfo("Cleared cinematic timeline for scene change.");
        }

        private bool ResolveCamera()
        {
            camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                GameObject player = GameObject.Find("Player");
                if (player != null)
                {
                    Transform mainCamera = player.transform.Find("MainCamera");
                    if (mainCamera != null) camera = mainCamera.GetComponent<UnityEngine.Camera>();
                }
            }
            if (camera == null) log.LogError("MainCamera not found.");
            return camera != null;
        }

        private void OpenTimeline()
        {
            if (timelineSessionActive) return;
            timelineSessionActive = true;
            timeScaleBeforeTimeline = Time.timeScale;
            Time.timeScale = 0f;
            if (GameStateManager.Instance != null && !GameStateManager.Instance.IsStateActive(TimelineStateKey))
                GameStateManager.Instance.RegisterState(new GameState(TimelineStateKey)
                {
                    playerInputLock = LockMode.Lock,
                    cameraInputLock = LockMode.Lock,
                    cursorLock = LockMode.Unlock,
                    timerModifier = 0f
                });
            timelineUi.Open();
        }

        private void CloseTimeline()
        {
            EndTimelinePreview();
            timelineUi.Close();
            if (!timelineSessionActive) return;
            timelineSessionActive = false;
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsStateActive(TimelineStateKey))
                GameStateManager.Instance.PopState(TimelineStateKey);
            Time.timeScale = timeScaleBeforeTimeline;
        }

        private void ExitPlaybackGameState()
        {
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsStateActive(PlaybackStateKey))
                GameStateManager.Instance.PopState(PlaybackStateKey);
        }

        private void OnDestroy()
        {
            if (PlaybackActive) StopPlayback();
            DisablePhotoPause();
            CloseTimeline();
        }
    }
}
