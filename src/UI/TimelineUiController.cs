using System;
using System.Collections.Generic;
using System.Globalization;
using UltraCinematic.Core;
using UltraCinematic.Data;
using UltraCinematic.Persistence;
using UltraCinematic.Timeline;
using UnityEngine;

namespace UltraCinematic.UI
{
    internal sealed class TimelineUiController : MonoBehaviour
    {
        private enum DialogMode { None, Save, Load, ConfirmClear, ConfirmDelete, ConfirmOverwrite }

        private CinematicController controller;
        private Rect window = new Rect(30, 30, 900, 740);
        private readonly TimelineSaveRepository saveRepository = new TimelineSaveRepository();
        private List<TimelineSaveEntry> savedProjects = new List<TimelineSaveEntry>();
        private TimelineSaveEntry pendingSave;
        private DialogMode dialogMode;
        private Vector2 saveListScroll;
        private string saveNameInput = "";
        private string dialogError = "";
        private string statusMessage = "";
        private Texture2D pointMarkerTexture;
        private Texture2D pointMarkerHoverTexture;
        private GUIStyle pointMarkerLabelStyle;
        private int selectedSegment;
        private int selectedPoint = -1;
        private bool dragCursor;
        private bool pointPreviewActive;
        private string flightTimeInput = "5.00";
        private string softIncomingInput = "10.00";
        private string softOutgoingInput = "10.00";
        private readonly string[] pointInputs = new string[7];
        private int pointInputPoint = -1;
        private int scrubField = -1;
        private int scrubControl;
        private float scrubStartX;
        private float scrubStartValue;

        public bool IsOpen { get; private set; }
        public bool IsDragging => dragCursor || scrubField >= 0;

        public void Initialize(CinematicController value) { controller = value; }
        public void Open() { IsOpen = true; SyncFlightTimeInput(); SyncSoftPointInputs(); SyncPointInputs(); }
        public void Close()
        {
            IsOpen = false;
            dragCursor = false;
            pointPreviewActive = false;
            scrubField = -1;
            pointInputPoint = -1;
            dialogMode = DialogMode.None;
            pendingSave = null;
            controller?.EndTimelinePreview();
        }

        private void OnDestroy()
        {
            if (pointMarkerTexture != null) Destroy(pointMarkerTexture);
            if (pointMarkerHoverTexture != null) Destroy(pointMarkerHoverTexture);
        }

        private void Update()
        {
            if (!IsOpen || controller == null) return;

            if (dragCursor)
            {
                if (!Input.GetMouseButton(0))
                {
                    dragCursor = false;
                    controller.EndTimelinePreview();
                }
                else controller.PreviewTimelineAt(controller.Timeline.CursorTime);
            }
            else if (pointPreviewActive)
            {
                if (selectedPoint < 0 || selectedPoint >= controller.Timeline.Points.Count) EndPointPreview();
                else controller.PreviewCameraPoint(controller.Timeline.Points[selectedPoint]);
            }
        }

        private void OnGUI()
        {
            if (!IsOpen || !controller.EditModeEnabled || controller.PlaybackActive) return;
            window = GUI.Window(GetInstanceID(), window, Draw, "Cinematic Timeline");
        }

        private void Draw(int id)
        {
            CinematicTimeline timeline = controller.Timeline;
            if (dialogMode == DialogMode.None)
            {
                if (GUI.Button(new Rect(8f, 2f, 68f, 20f), "SAVE")) OpenSaveDialog();
                if (GUI.Button(new Rect(80f, 2f, 68f, 20f), "LOAD")) OpenLoadDialog();
                if (GUI.Button(new Rect(window.width - 148f, 2f, 68f, 20f), "CLEAR")) { dialogError = ""; dialogMode = DialogMode.ConfirmClear; }
            }
            if (GUI.Button(new Rect(window.width - 76f, 2f, 68f, 20f), "CLOSE")) { controller.ToggleTimeline(); return; }
            if (dialogMode != DialogMode.None)
            {
                DrawDialog(timeline);
                GUI.DragWindow(new Rect(152f, 0f, window.width - 304f, 24f));
                return;
            }
            if (!string.IsNullOrEmpty(statusMessage)) GUI.Label(new Rect(156f, 27f, window.width - 312f, 22f), statusMessage);

            Rect track = new Rect(32, 58, window.width - 64, 42);
            float duration = Mathf.Max(.1f, timeline.Duration);
            GUI.Box(track, "");
            Event e = Event.current;
            bool mouseOverMarker = false;
            EnsurePointMarkerResources();

            for (int i = 0; i < timeline.Keyframes.Count; i++)
            {
                float x = track.x + timeline.Keyframes[i].Time / duration * track.width;
                Rect marker = new Rect(x - 27f, track.y - 6f, 54f, 54f);
                Vector2 offset = e.mousePosition - marker.center;
                bool markerHovered = offset.sqrMagnitude <= 27f * 27f;
                if (markerHovered) mouseOverMarker = true;
                GUI.DrawTexture(marker, markerHovered ? pointMarkerHoverTexture : pointMarkerTexture, ScaleMode.StretchToFill, true);
                GUI.Label(marker, (i + 1) + "\n" + timeline.Keyframes[i].Time.ToString("0.00") + "s", pointMarkerLabelStyle);
                if (markerHovered && e.type == EventType.MouseDown && e.button == 0)
                {
                    SelectPoint(i);
                    e.Use();
                }
            }

            for (int i = 0; i < timeline.SegmentCount; i++)
            {
                float fromX = track.x + timeline.Keyframes[i].Time / duration * track.width;
                float toX = track.x + timeline.Keyframes[i + 1].Time / duration * track.width;
                GUI.Label(new Rect((fromX + toX) * .5f - 34f, track.y + 15f, 68f, 22f), SegmentLabel(i) + ": " + timeline.GetSegmentDuration(i).ToString("0.00") + "s");
            }

            float cursorX = track.x + timeline.CursorTime / duration * track.width;
            Rect cursor = new Rect(cursorX - 8, track.y - 24, 16, 24);
            GUI.Box(cursor, "▼");
            bool mouseOverTimelineControl = track.Contains(e.mousePosition) || cursor.Contains(e.mousePosition);
            if (e.rawType == EventType.MouseDown && e.button == 0 && mouseOverTimelineControl && !mouseOverMarker)
            {
                pointPreviewActive = false;
                controller.EndTimelinePreview();
                dragCursor = controller.BeginTimelinePreview();
                if (dragCursor) UpdateTrackPreview(e.mousePosition.x, track, duration);
            }
            if (e.rawType == EventType.MouseDrag && dragCursor) UpdateTrackPreview(e.mousePosition.x, track, duration);
            if (e.rawType == EventType.MouseUp && dragCursor) { dragCursor = false; controller.EndTimelinePreview(); }

            GUILayout.Space(112);
            DrawSegmentSettings(timeline);
            DrawPointSettings(timeline);
            DrawCinematicSettings(timeline);
            if (timeline.SegmentCount > 0)
            {
                if (GUILayout.Button("START CINEMATIC", GUILayout.Height(30))) controller.StartPlayback();
            }
            else GUILayout.Label("Add at least two Camera Points to create and play a timeline.");
            GUI.DragWindow(new Rect(152f, 0f, window.width - 304f, 24f));
        }

        private void DrawDialog(CinematicTimeline timeline)
        {
            GUILayout.Space(30f);
            if (dialogMode == DialogMode.Save) DrawSaveDialog(timeline);
            else if (dialogMode == DialogMode.Load) DrawLoadDialog(timeline);
            else if (dialogMode == DialogMode.ConfirmClear)
                DrawConfirmation("CLEAR CURRENT PROJECT", "Delete every Camera Point and restore all Timeline settings to defaults?", "CLEAR EVERYTHING", DialogMode.None, ClearCurrentProject);
            else if (dialogMode == DialogMode.ConfirmDelete)
                DrawConfirmation("DELETE SAVE", "Permanently delete \"" + SafePendingName() + "\"?", "DELETE", DialogMode.Load, DeletePendingSave);
            else if (dialogMode == DialogMode.ConfirmOverwrite)
                DrawConfirmation("OVERWRITE SAVE", "Replace \"" + SafePendingName() + "\" with the current Timeline?", "OVERWRITE", DialogMode.Load, () => OverwritePendingSave(timeline));
        }

        private void DrawSaveDialog(CinematicTimeline timeline)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("SAVE TIMELINE PROJECT");
            GUILayout.Label("Level: " + saveRepository.CurrentLevelName);
            GUILayout.Label("This project can only be loaded on this level.");
            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Project name", GUILayout.Width(100f));
            GUI.SetNextControlName("timeline-save-name");
            saveNameInput = GUILayout.TextField(saveNameInput ?? "", 64);
            GUILayout.EndHorizontal();
            DrawDialogError();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CANCEL", GUILayout.Height(30f))) { dialogMode = DialogMode.None; dialogError = ""; }
            if (GUILayout.Button("SAVE", GUILayout.Height(30f)))
            {
                string error;
                if (saveRepository.Create(saveNameInput, timeline, out error))
                {
                    statusMessage = "Saved project: " + saveNameInput.Trim();
                    dialogMode = DialogMode.None;
                    dialogError = "";
                }
                else dialogError = error;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawLoadDialog(CinematicTimeline timeline)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("LOAD TIMELINE PROJECT — " + saveRepository.CurrentLevelName);
            GUILayout.Label("Only projects saved on this exact level are shown.");
            DrawDialogError();
            saveListScroll = GUILayout.BeginScrollView(saveListScroll, GUILayout.Height(window.height - 145f));
            if (savedProjects.Count == 0) GUILayout.Label("No saved Timeline projects on this level.");

            TimelineSaveEntry loadEntry = null, overwriteEntry = null, deleteEntry = null;
            for (int i = 0; i < savedProjects.Count; i++)
            {
                TimelineSaveEntry entry = savedProjects[i];
                GUILayout.BeginHorizontal("box");
                GUILayout.BeginVertical();
                GUILayout.Label(entry.Data.ProjectName);
                GUILayout.Label(entry.Data.Points.Length + " points  •  " + entry.Data.FlightDuration.ToString("0.00") + "s  •  " + FormatModifiedTime(entry.Data.ModifiedUtcTicks));
                if (!string.IsNullOrEmpty(entry.Warning)) GUILayout.Label("WARNING: " + entry.Warning);
                GUILayout.EndVertical();
                if (GUILayout.Button("LOAD", GUILayout.Width(70f), GUILayout.Height(42f))) loadEntry = entry;
                if (GUILayout.Button("OVERWRITE", GUILayout.Width(96f), GUILayout.Height(42f))) overwriteEntry = entry;
                if (GUILayout.Button("DELETE", GUILayout.Width(70f), GUILayout.Height(42f))) deleteEntry = entry;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("BACK", GUILayout.Height(30f))) { dialogMode = DialogMode.None; dialogError = ""; }
            GUILayout.EndVertical();

            if (loadEntry != null) LoadProject(loadEntry, timeline);
            else if (overwriteEntry != null) { pendingSave = overwriteEntry; dialogError = ""; dialogMode = DialogMode.ConfirmOverwrite; }
            else if (deleteEntry != null) { pendingSave = deleteEntry; dialogError = ""; dialogMode = DialogMode.ConfirmDelete; }
        }

        private void DrawConfirmation(string title, string message, string confirmLabel, DialogMode cancelMode, Action confirmAction)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label(title);
            GUILayout.Space(18f);
            GUILayout.Label(message);
            DrawDialogError();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CANCEL", GUILayout.Height(34f))) { dialogMode = cancelMode; pendingSave = null; dialogError = ""; }
            if (GUILayout.Button(confirmLabel, GUILayout.Height(34f))) confirmAction();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void OpenSaveDialog()
        {
            EndPointPreview();
            saveNameInput = "";
            dialogError = "";
            dialogMode = DialogMode.Save;
        }

        private void OpenLoadDialog()
        {
            EndPointPreview();
            RefreshSavedProjects();
            dialogMode = DialogMode.Load;
        }

        private void RefreshSavedProjects()
        {
            string error;
            savedProjects = saveRepository.ListCurrentLevel(out error);
            dialogError = error;
            saveListScroll = Vector2.zero;
        }

        private void LoadProject(TimelineSaveEntry entry, CinematicTimeline timeline)
        {
            controller.EndTimelinePreview();
            string error;
            if (!saveRepository.Apply(entry, timeline, out error)) { dialogError = error; return; }
            ResetEditorSelection();
            controller.NotifyTimelineProjectChanged();
            statusMessage = "Loaded project: " + entry.Data.ProjectName;
            dialogMode = DialogMode.None;
        }

        private void OverwritePendingSave(CinematicTimeline timeline)
        {
            string error;
            if (!saveRepository.Overwrite(pendingSave, timeline, out error)) { dialogError = error; return; }
            statusMessage = "Overwritten project: " + SafePendingName();
            pendingSave = null;
            RefreshSavedProjects();
            dialogMode = DialogMode.Load;
        }

        private void DeletePendingSave()
        {
            string deletedName = SafePendingName();
            string error;
            if (!saveRepository.Delete(pendingSave, out error)) { dialogError = error; return; }
            statusMessage = "Deleted project: " + deletedName;
            pendingSave = null;
            RefreshSavedProjects();
            dialogMode = DialogMode.Load;
        }

        private void ClearCurrentProject()
        {
            controller.EndTimelinePreview();
            controller.Timeline.Clear();
            ResetEditorSelection();
            controller.NotifyTimelineProjectChanged();
            statusMessage = "Current Timeline project cleared.";
            dialogError = "";
            dialogMode = DialogMode.None;
        }

        private void ResetEditorSelection()
        {
            dragCursor = false;
            pointPreviewActive = false;
            selectedPoint = -1;
            selectedSegment = 0;
            pointInputPoint = -1;
            scrubField = -1;
            SyncFlightTimeInput();
            SyncSoftPointInputs();
        }

        private void DrawDialogError()
        {
            if (!string.IsNullOrEmpty(dialogError)) GUILayout.Label("ERROR: " + dialogError);
        }

        private string SafePendingName() => pendingSave == null || pendingSave.Data == null ? "unknown" : pendingSave.Data.ProjectName;

        private static string FormatModifiedTime(long ticks)
        {
            try { return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm"); }
            catch { return "unknown time"; }
        }

        private void EnsurePointMarkerResources()
        {
            if (pointMarkerTexture == null) pointMarkerTexture = CreateCircleTexture(new Color(.04f, .04f, .04f, .96f));
            if (pointMarkerHoverTexture == null) pointMarkerHoverTexture = CreateCircleTexture(new Color(.48f, .24f, .24f, .98f));
            if (pointMarkerLabelStyle == null)
            {
                pointMarkerLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
                pointMarkerLabelStyle.normal.textColor = Color.white;
            }
        }

        private static Texture2D CreateCircleTexture(Color fill)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false) { name = "UltraCinematic Timeline Point" };
            Color transparent = new Color(0f, 0f, 0f, 0f);
            Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
            float outer = size * .49f, inner = outer - 3f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance > outer ? transparent : distance >= inner ? Color.white : fill);
            }
            texture.Apply(false, true);
            return texture;
        }

        private void DrawSegmentSettings(CinematicTimeline timeline)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("SEGMENT SETTINGS");
            if (timeline.SegmentCount > 0)
            {
                selectedSegment = Mathf.Clamp(selectedSegment, 0, timeline.SegmentCount - 1);
                GUILayout.BeginHorizontal(); GUILayout.Label("Segments", GUILayout.Width(70));
                for (int i = 0; i < timeline.SegmentCount; i++)
                    if (GUILayout.Toggle(selectedSegment == i, SegmentLabel(i), "Button", GUILayout.Width(42)) && selectedSegment != i) selectedSegment = i;
                GUILayout.EndHorizontal();
                GUILayout.Label("Segment " + SegmentLabel(selectedSegment) + ": Point " + (selectedSegment + 1) + " → Point " + (selectedSegment + 2));
                GUILayout.BeginHorizontal(); GUILayout.Label("Path", GUILayout.Width(70));
                foreach (PathType path in System.Enum.GetValues(typeof(PathType)))
                    if (GUILayout.Toggle(timeline.GetPath(selectedSegment) == path, path.ToString(), "Button") && timeline.GetPath(selectedSegment) != path) { timeline.SetPath(selectedSegment, path); controller.RefreshVisualization(); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(); GUILayout.Label("Easing", GUILayout.Width(70));
                foreach (EasingType easing in System.Enum.GetValues(typeof(EasingType)))
                    if (GUILayout.Toggle(timeline.GetEasing(selectedSegment) == easing, easing.ToString(), "Button") && timeline.GetEasing(selectedSegment) != easing) { timeline.SetEasing(selectedSegment, easing); controller.RefreshVisualization(); }
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("No segments yet.");
            GUILayout.EndVertical();
        }

        private void DrawPointSettings(CinematicTimeline timeline)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("CAMERA POINT SETTINGS");
            if (selectedPoint < 0 || selectedPoint >= timeline.Points.Count)
            {
                GUILayout.Label("Click a numbered Camera Point on the Timeline to edit its transform.");
                GUILayout.EndVertical();
                return;
            }

            EnsurePointInputs();
            GUILayout.Label("Point " + (selectedPoint + 1) + " — world position and camera rotation");
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Position");
            DrawPointField("X", 0, .02f);
            DrawPointField("Y", 1, .02f);
            DrawPointField("Z", 2, .02f);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("Rotation (degrees)");
            DrawPointField("Pitch X", 3, .25f);
            DrawPointField("Yaw Y", 4, .25f);
            DrawPointField("Roll Z", 5, .25f);
            DrawPointField("FOV", 6, .1f);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            string previewLabel = pointPreviewActive ? "RETURN" : "PREVIEW POINT";
            if (GUILayout.Button(previewLabel, GUILayout.Height(26)))
            {
                if (pointPreviewActive) EndPointPreview();
                else
                {
                    controller.EndTimelinePreview();
                    if (controller.BeginTimelinePreview())
                    {
                        pointPreviewActive = true;
                        controller.PreviewCameraPoint(timeline.Points[selectedPoint]);
                    }
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawCinematicSettings(CinematicTimeline timeline)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("CINEMATIC SETTINGS");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Flight time", GUILayout.Width(110));
            if (GUILayout.Button("-0.10", GUILayout.Width(58))) SetFlightTime(Mathf.Max(.1f, timeline.FlightDuration - .1f));
            flightTimeInput = GUILayout.TextField(flightTimeInput, GUILayout.Width(80));
            GUILayout.Label("seconds total", GUILayout.Width(88));
            if (GUILayout.Button("Apply", GUILayout.Width(58))) ApplyFlightTimeInput();
            if (GUILayout.Button("+0.10", GUILayout.Width(58))) SetFlightTime(timeline.FlightDuration + .1f);
            GUILayout.EndHorizontal();
            GUILayout.Label("Segment timing is calculated automatically from the measured path length.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Soft points", GUILayout.Width(110));
            bool softPointsEnabled = GUILayout.Toggle(timeline.SoftPointsEnabled, timeline.SoftPointsEnabled ? "ENABLED" : "DISABLED", "Button", GUILayout.Width(110));
            if (softPointsEnabled != timeline.SoftPointsEnabled)
            {
                timeline.SetSoftPointsEnabled(softPointsEnabled);
                controller.RefreshVisualization();
            }
            GUILayout.Label("Rounds internal points for every Path type.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Before point", GUILayout.Width(110));
            softIncomingInput = GUILayout.TextField(softIncomingInput, GUILayout.Width(70));
            GUILayout.Label("%", GUILayout.Width(18));
            GUILayout.Label("After point", GUILayout.Width(80));
            softOutgoingInput = GUILayout.TextField(softOutgoingInput, GUILayout.Width(70));
            GUILayout.Label("%", GUILayout.Width(18));
            if (GUILayout.Button("Apply", GUILayout.Width(58))) ApplySoftPointInputs();
            GUILayout.EndHorizontal();
            GUILayout.Label("Range: 1–45% on each side. First and last Camera Points always remain exact.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Playback world", GUILayout.Width(110));
            if (GUILayout.Toggle(timeline.PlaybackMode == CinematicPlaybackMode.LiveWorld, "LIVE WORLD", "Button") && timeline.PlaybackMode != CinematicPlaybackMode.LiveWorld)
                timeline.PlaybackMode = CinematicPlaybackMode.LiveWorld;
            if (GUILayout.Toggle(timeline.PlaybackMode == CinematicPlaybackMode.FrozenWorld, "FROZEN WORLD", "Button") && timeline.PlaybackMode != CinematicPlaybackMode.FrozenWorld)
                timeline.PlaybackMode = CinematicPlaybackMode.FrozenWorld;
            GUILayout.EndHorizontal();
            GUILayout.Label(timeline.PlaybackMode == CinematicPlaybackMode.FrozenWorld ? "The world is paused; the cinematic advances on unscaled time." : "The world continues running during the cinematic.");
            GUILayout.EndVertical();
        }

        private void SelectPoint(int index)
        {
            if (selectedPoint != index) EndPointPreview();
            selectedPoint = index;
            selectedSegment = Mathf.Clamp(index == controller.Timeline.Keyframes.Count - 1 ? index - 1 : index, 0, Mathf.Max(0, controller.Timeline.SegmentCount - 1));
            pointInputPoint = -1;
            SyncPointInputs();
        }

        private void DrawPointField(string label, int field, float sensitivity)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(54));
            GUI.SetNextControlName("point-field-" + field);
            pointInputs[field] = GUILayout.TextField(pointInputs[field] ?? "0.00", GUILayout.Width(82));
            if (GUILayout.Button("SET", GUILayout.Width(38))) ApplyPointInput(field);
            Rect scrubRect = GUILayoutUtility.GetRect(68, 22, GUILayout.Width(68));
            GUI.Box(scrubRect, "↔ DRAG");
            HandleScrub(scrubRect, field, sensitivity);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "point-field-" + field)
            {
                ApplyPointInput(field);
                Event.current.Use();
            }
            GUILayout.EndHorizontal();
        }

        private void HandleScrub(Rect rect, int field, float sensitivity)
        {
            Event e = Event.current;
            int control = GUIUtility.GetControlID(17000 + field, FocusType.Passive, rect);
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                scrubField = field;
                scrubControl = control;
                scrubStartX = e.mousePosition.x;
                scrubStartValue = GetPointValue(field);
                GUIUtility.hotControl = control;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && scrubField == field && GUIUtility.hotControl == scrubControl)
            {
                ApplyPointValue(field, scrubStartValue + (e.mousePosition.x - scrubStartX) * sensitivity);
                e.Use();
            }
            else if (e.rawType == EventType.MouseUp && scrubField == field)
            {
                scrubField = -1;
                GUIUtility.hotControl = 0;
            }
        }

        private float GetPointValue(int field)
        {
            CameraPoint point = controller.Timeline.Points[selectedPoint];
            if (field == 0) return point.Position.x;
            if (field == 1) return point.Position.y;
            if (field == 2) return point.Position.z;
            if (field == 6) return point.FieldOfView;
            Vector3 euler = SignedEuler(point.Rotation.eulerAngles);
            return field == 3 ? euler.x : field == 4 ? euler.y : euler.z;
        }

        private void ApplyPointInput(int field)
        {
            float value;
            string normalized = (pointInputs[field] ?? "").Replace(',', '.');
            if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) ApplyPointValue(field, value);
            else SyncPointInputs();
        }

        private void ApplyPointValue(int field, float value)
        {
            if (selectedPoint < 0 || selectedPoint >= controller.Timeline.Points.Count) return;
            CameraPoint point = controller.Timeline.Points[selectedPoint];
            float appliedValue = value;
            if (field < 3)
            {
                Vector3 position = point.Position;
                if (field == 0) position.x = value; else if (field == 1) position.y = value; else position.z = value;
                point.Position = position;
                controller.Timeline.RebuildAutomaticTiming();
            }
            else if (field < 6)
            {
                Vector3 euler = SignedEuler(point.Rotation.eulerAngles);
                if (field == 3) euler.x = value; else if (field == 4) euler.y = value; else euler.z = value;
                point.Rotation = Quaternion.Euler(euler);
            }
            else
            {
                appliedValue = Mathf.Clamp(value, 1f, 179f);
                point.FieldOfView = appliedValue;
            }
            pointInputs[field] = appliedValue.ToString("0.00", CultureInfo.InvariantCulture);
            controller.RefreshVisualization();
            if (pointPreviewActive) controller.PreviewCameraPoint(point);
        }

        private void UpdateTrackPreview(float mouseX, Rect track, float duration)
        {
            float time = Mathf.Clamp((mouseX - track.x) / track.width * duration, 0f, duration);
            controller.PreviewTimelineAt(time);
        }

        private void EndPointPreview()
        {
            if (!pointPreviewActive) return;
            pointPreviewActive = false;
            controller.EndTimelinePreview();
        }

        private void SyncFlightTimeInput()
        {
            if (controller == null) return;
            flightTimeInput = controller.Timeline.FlightDuration.ToString("0.00", CultureInfo.InvariantCulture);
        }
        private void ApplyFlightTimeInput()
        {
            float value;
            string normalized = (flightTimeInput ?? "").Replace(',', '.');
            if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) SetFlightTime(value);
            else SyncFlightTimeInput();
        }
        private void SetFlightTime(float value)
        {
            controller.Timeline.SetFlightDuration(value);
            SyncFlightTimeInput();
            controller.RefreshVisualization();
        }

        private void SyncSoftPointInputs()
        {
            if (controller == null) return;
            softIncomingInput = controller.Timeline.SoftPointIncomingPercent.ToString("0.00", CultureInfo.InvariantCulture);
            softOutgoingInput = controller.Timeline.SoftPointOutgoingPercent.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void ApplySoftPointInputs()
        {
            float incoming, outgoing;
            string normalizedIncoming = (softIncomingInput ?? "").Replace(',', '.');
            string normalizedOutgoing = (softOutgoingInput ?? "").Replace(',', '.');
            if (!float.TryParse(normalizedIncoming, NumberStyles.Float, CultureInfo.InvariantCulture, out incoming) ||
                !float.TryParse(normalizedOutgoing, NumberStyles.Float, CultureInfo.InvariantCulture, out outgoing))
            {
                SyncSoftPointInputs();
                return;
            }
            controller.Timeline.SetSoftPointWindows(incoming, outgoing);
            SyncSoftPointInputs();
            controller.RefreshVisualization();
        }

        private void EnsurePointInputs() { if (pointInputPoint != selectedPoint) SyncPointInputs(); }
        private void SyncPointInputs()
        {
            if (controller == null || selectedPoint < 0 || selectedPoint >= controller.Timeline.Points.Count) { pointInputPoint = -1; return; }
            pointInputPoint = selectedPoint;
            for (int i = 0; i < pointInputs.Length; i++) pointInputs[i] = GetPointValue(i).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static Vector3 SignedEuler(Vector3 euler) => new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
        private static float NormalizeAngle(float value) { while (value > 180f) value -= 360f; while (value < -180f) value += 360f; return value; }
        private static string SegmentLabel(int index) { index++; string result = ""; while (index > 0) { index--; result = (char)('A' + index % 26) + result; index /= 26; } return result; }
    }
}
