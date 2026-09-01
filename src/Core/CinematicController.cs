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
        private bool restorePhotoPauseAfterTimeline;
        private PlayerInput timelineInputSource;
        private bool timelineInputWasEnabled;
        private bool pendingTimelineInputRestore;
        private int pendingInsertSegment = -1;
        private bool initialized;

        public bool EditModeEnabled { get; private set; }
        public bool PlaybackActive { get; private set; }
        public bool PhotoPauseActive => photoPause != null && photoPause.Active;
        public CinematicTimeline Timeline { get; } = new CinematicTimeline();
        public bool TimelineOpen => timelineUi != null && timelineUi.IsOpen;
        internal CinematicCheatMenuCoordinator MenuCoordinator => menuCoordinator;
        internal bool OwnsCamera => PlaybackActive || previewActive || PhotoPauseActive;
        internal int PendingInsertSegment => pendingInsertSegment;

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
            restorePhotoPauseAfterTimeline = false;
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
            if (!EditModeEnabled || PlaybackActive) return;
            CameraState pointState;
            if (PhotoPauseActive) pointState = photoPause.CameraState;
            else
            {
                if (!ResolveCamera()) return;
                pointState = new CameraState(camera.transform.position, camera.transform.rotation, camera.fieldOfView);
            }
            CameraPoint point = new CameraPoint { Position = pointState.Position, Rotation = pointState.Rotation, FieldOfView = pointState.FieldOfView };
            int insertedPoint = Timeline.Points.Count;
            if (pendingInsertSegment == -2 && Timeline.InsertBeforeFirst(point))
            {
                insertedPoint = 0;
                pendingInsertSegment = -1;
                Timeline.CursorTime = 0f;
                timelineUi.NotifyPointInserted(insertedPoint, true, true);
                log.LogInfo("Inserted Camera Point 1 before the previous first point.");
            }
            else if (pendingInsertSegment >= 0 && Timeline.InsertAfterSegment(pendingInsertSegment, point))
            {
                insertedPoint = pendingInsertSegment + 1;
                pendingInsertSegment = -1;
                Timeline.CursorTime = Timeline.Keyframes[insertedPoint].Time;
                timelineUi.NotifyPointInserted(insertedPoint, true);
                log.LogInfo("Inserted Camera Point " + (insertedPoint + 1) + ".");
            }
            else
            {
                pendingInsertSegment = -1;
                Timeline.Add(point);
                Timeline.CursorTime = Timeline.Duration;
                timelineUi.NotifyPointInserted(insertedPoint, false);
                log.LogInfo("Added Camera Point " + Timeline.Points.Count + ".");
            }
            visualizer.Rebuild();
            menuCoordinator.RequestRefresh();
        }

        public void DeleteLastPoint()
        {
            if (!EditModeEnabled || PlaybackActive) return;
            if (!Timeline.RemoveLast()) { log.LogWarning("Delete Last Point ignored: timeline is empty."); return; }
            pendingInsertSegment = -1;
            ReleasePreview();
            visualizer.Rebuild();
            menuCoordinator.RequestRefresh();
            log.LogInfo("Deleted last Camera Point. Remaining: " + Timeline.Points.Count + ".");
        }

        internal bool DeleteCameraPoint(int pointIndex)
        {
            if (!EditModeEnabled || PlaybackActive || !Timeline.RemoveAt(pointIndex)) return false;
            pendingInsertSegment = -1;
            ReleasePreview();
            visualizer.Rebuild();
            menuCoordinator.RequestRefresh();
            log.LogInfo("Deleted Camera Point " + (pointIndex + 1) + ". Remaining: " + Timeline.Points.Count + ".");
            return true;
        }

        internal bool ArmPointInsertion(int segmentIndex)
        {
            if (!EditModeEnabled || PlaybackActive || segmentIndex < 0 || segmentIndex >= Timeline.SegmentCount) return false;
            pendingInsertSegment = segmentIndex;
            return true;
        }

        internal bool ArmPointInsertionBeforeFirst()
        {
            if (!EditModeEnabled || PlaybackActive || Timeline.Points.Count == 0) return false;
            pendingInsertSegment = -2;
            return true;
        }

        internal void CancelPointInsertion()
        {
            pendingInsertSegment = -1;
        }

        public void ToggleTimeline()
        {
            if (!EditModeEnabled || PlaybackActive) return;
            if (timelineUi.IsOpen)
            {
                CloseTimeline();
                RestorePhotoPauseAfterTimelineIfNeeded();
            }
            else
            {
                restorePhotoPauseAfterTimeline = PhotoPauseActive;
                DisablePhotoPause();
                OpenTimeline();
            }
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
            restorePhotoPauseAfterPlayback = PhotoPauseActive || restorePhotoPauseAfterTimeline;
            restorePhotoPauseAfterTimeline = false;
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

        private void RestorePhotoPauseAfterTimelineIfNeeded()
        {
            if (!restorePhotoPauseAfterTimeline) return;
            restorePhotoPauseAfterTimeline = false;
            if (!EditModeEnabled || PlaybackActive) return;
            EnablePhotoPause();
            if (PhotoPauseActive) log.LogInfo("Restored Pause Game state after closing Timeline.");
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
            visualizer.UpdateCursor(cameraTarget.Position);
        }

        internal void PreviewCameraPoint(CameraPoint point)
        {
            if (point == null || (!previewActive && !BeginTimelinePreview())) return;
            cameraTarget = new CameraState(point.Position, point.Rotation, point.FieldOfView);
            playerPlayback.Follow(cameraTarget);
            camera.transform.SetPositionAndRotation(cameraTarget.Position, cameraTarget.Rotation);
            camera.fieldOfView = cameraTarget.FieldOfView;
            visualizer.UpdateCursor(cameraTarget.Position);
        }

        internal void EndTimelinePreview()
        {
            if (!previewActive) return;
            previewActive = false;
            playerPlayback.Restore();
            if (Timeline.Keyframes.Count > 0)
                visualizer.UpdateCursor(TimelineEvaluator.Evaluate(Timeline, Timeline.CursorTime).Position);
        }

        internal void ReleasePreview() => EndTimelinePreview();
        internal void RefreshVisualization() => visualizer.Rebuild();
        internal void NotifyTimelineProjectChanged()
        {
            pendingInsertSegment = -1;
            EndTimelinePreview();
            visualizer.Rebuild();
            menuCoordinator.RequestRefresh();
        }

        internal void NotifyInterfaceSettingsChanged()
        {
            menuCoordinator.RequestRefresh();
        }

        internal bool TryGetPresetAnchor(out Vector3 anchor)
        {
            anchor = Vector3.zero;
            if (PhotoPauseActive)
            {
                anchor = photoPause.CameraState.Position;
                return true;
            }
            if (!ResolveCamera()) return false;
            anchor = camera.transform.position;
            return true;
        }

        private void Update()
        {
            RestoreTimelineInputIfReady();
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
            restorePhotoPauseAfterTimeline = false;
            if (PlaybackActive) StopPlayback();
            DisablePhotoPause();
            CloseTimeline();
            RestoreTimelineInputNow();
            EditModeEnabled = false;
            previewActive = false;
            Timeline.Clear();
            pendingInsertSegment = -1;
            visualizer.Hide();
            camera = null;
            menuCoordinator.RequestRefresh();
            log.LogInfo("Cleared cinematic timeline for scene change.");
        }

        internal void PrepareForSameLevelReload()
        {
            restorePhotoPauseAfterTimeline = false;
            restorePhotoPauseAfterPlayback = false;
            if (PlaybackActive) StopPlayback();
            DisablePhotoPause();
            CloseTimeline();
            RestoreTimelineInputNow();
            previewActive = false;
            camera = null;
            visualizer.Hide();
            if (EditModeEnabled) visualizer.Show();
            menuCoordinator.RequestRefresh();
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
            CaptureAndDisableTimelineInput();
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
            QueueTimelineInputRestore();
        }

        private void CaptureAndDisableTimelineInput()
        {
            RestoreTimelineInputNow();
            InputManager inputManager = MonoSingleton<InputManager>.Instance;
            timelineInputSource = inputManager == null ? null : inputManager.InputSource;
            timelineInputWasEnabled = timelineInputSource != null && timelineInputSource.Actions != null && timelineInputSource.Actions.asset.enabled;
            pendingTimelineInputRestore = false;
            if (timelineInputSource != null) timelineInputSource.Disable();
        }

        private void QueueTimelineInputRestore()
        {
            if (timelineInputSource == null) return;
            pendingTimelineInputRestore = timelineInputWasEnabled;
            if (!timelineInputWasEnabled)
            {
                timelineInputSource = null;
                timelineInputWasEnabled = false;
            }
        }

        private void RestoreTimelineInputIfReady()
        {
            if (!pendingTimelineInputRestore || PlaybackActive || Input.GetMouseButton(0)) return;
            RestoreTimelineInputNow();
        }

        private void RestoreTimelineInputNow()
        {
            if (timelineInputSource != null && timelineInputWasEnabled) timelineInputSource.Enable();
            timelineInputSource = null;
            timelineInputWasEnabled = false;
            pendingTimelineInputRestore = false;
        }

        private void ExitPlaybackGameState()
        {
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsStateActive(PlaybackStateKey))
                GameStateManager.Instance.PopState(PlaybackStateKey);
        }

        private void OnDestroy()
        {
            restorePhotoPauseAfterTimeline = false;
            if (PlaybackActive) StopPlayback();
            DisablePhotoPause();
            CloseTimeline();
            RestoreTimelineInputNow();
        }
    }
}
