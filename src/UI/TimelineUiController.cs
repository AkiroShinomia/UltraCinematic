using System;
using System.Collections.Generic;
using System.Globalization;
using UltraCinematic.Configuration;
using UltraCinematic.Core;
using UltraCinematic.Data;
using UltraCinematic.Persistence;
using UltraCinematic.Timeline;
using UnityEngine;

namespace UltraCinematic.UI
{
    internal sealed class TimelineUiController : MonoBehaviour
    {
        private enum DialogMode { None, Save, Load, Settings, ConfirmClear, ConfirmDelete, ConfirmOverwrite }

        private CinematicController controller;
        private UltraCinematicPreferences preferences;
        private Rect window = new Rect(30, 30, 900, 740);
        private TimelineSaveRepository saveRepository;
        private List<TimelineSaveEntry> savedProjects = new List<TimelineSaveEntry>();
        private TimelineSaveEntry pendingSave;
        private DialogMode dialogMode;
        private Vector2 saveListScroll;
        private string saveNameInput = "";
        private string dialogError = "";
        private string statusMessage = "";
        private string saveDirectoryInput = "";
        private string settingsMessage = "";
        private string settingsError = "";
        private Texture2D pointMarkerTexture;
        private Texture2D pointMarkerHoverTexture;
        private GUIStyle pointMarkerLabelStyle;
        private GUISkin darkSkin;
        private GUISkin darkSkinSource;
        private readonly List<Texture2D> themeTextures = new List<Texture2D>();
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

        public void Initialize(CinematicController value)
        {
            controller = value;
            preferences = UltraCinematicPlugin.Preferences;
            string directory = preferences == null ? UltraCinematicPreferences.DefaultTimelineDirectory : preferences.TimelineDirectory;
            saveRepository = new TimelineSaveRepository(directory);
            saveDirectoryInput = directory;
        }
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
            if (darkSkin != null) Destroy(darkSkin);
            for (int i = 0; i < themeTextures.Count; i++) if (themeTextures[i] != null) Destroy(themeTextures[i]);
            themeTextures.Clear();
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
            GUISkin previousSkin = GUI.skin;
            try
            {
                if (preferences != null && preferences.Style == InterfaceStyle.Dark) GUI.skin = GetDarkSkin(previousSkin);
                window = GUI.Window(GetInstanceID(), window, Draw, UiText.T("Cinematic Timeline", "Кинематографический таймлайн"));
            }
            finally { GUI.skin = previousSkin; }
        }

        private void Draw(int id)
        {
            CinematicTimeline timeline = controller.Timeline;
            if (dialogMode == DialogMode.None)
            {
                if (GUI.Button(new Rect(8f, 2f, 68f, 20f), UiText.T("SAVE", "СОХР."))) OpenSaveDialog();
                if (GUI.Button(new Rect(80f, 2f, 68f, 20f), UiText.T("LOAD", "ЗАГР."))) OpenLoadDialog();
                if (GUI.Button(new Rect(window.width - 246f, 2f, 68f, 20f), UiText.T("CLEAR", "ОЧИСТ."))) { dialogError = ""; dialogMode = DialogMode.ConfirmClear; }
                if (GUI.Button(new Rect(window.width - 174f, 2f, 132f, 20f), UiText.T("SETTINGS", "НАСТРОЙКИ"))) OpenSettingsDialog();
            }
            if (GUI.Button(new Rect(window.width - 38f, 2f, 30f, 20f), "X")) { controller.ToggleTimeline(); return; }
            if (dialogMode != DialogMode.None)
            {
                DrawDialog(timeline);
                GUI.DragWindow(new Rect(152f, 0f, window.width - 410f, 24f));
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
                GUI.Label(marker, (i + 1) + "\n" + timeline.Keyframes[i].Time.ToString("0.00") + UiText.T("s", "с"), pointMarkerLabelStyle);
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
                GUI.Label(new Rect((fromX + toX) * .5f - 34f, track.y + 15f, 68f, 22f), SegmentLabel(i) + ": " + timeline.GetSegmentDuration(i).ToString("0.00") + UiText.T("s", "с"));
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
                if (GUILayout.Button(UiText.T("START CINEMATIC", "ЗАПУСТИТЬ СИНЕМАТИК"), GUILayout.Height(30))) controller.StartPlayback();
            }
            else GUILayout.Label(UiText.T("Add at least two Camera Points to create and play a timeline.", "Добавьте минимум две точки камеры, чтобы создать и запустить таймлайн."));
            GUI.DragWindow(new Rect(152f, 0f, window.width - 410f, 24f));
        }

        private void DrawDialog(CinematicTimeline timeline)
        {
            GUILayout.Space(30f);
            if (dialogMode == DialogMode.Save) DrawSaveDialog(timeline);
            else if (dialogMode == DialogMode.Load) DrawLoadDialog(timeline);
            else if (dialogMode == DialogMode.Settings) DrawSettingsDialog();
            else if (dialogMode == DialogMode.ConfirmClear)
                DrawConfirmation(UiText.T("CLEAR CURRENT PROJECT", "ОЧИСТИТЬ ТЕКУЩИЙ ПРОЕКТ"), UiText.T("Delete every Camera Point and restore all Timeline settings to defaults?", "Удалить все точки камеры и восстановить стандартные настройки таймлайна?"), UiText.T("CLEAR EVERYTHING", "УДАЛИТЬ ВСЁ"), DialogMode.None, ClearCurrentProject);
            else if (dialogMode == DialogMode.ConfirmDelete)
                DrawConfirmation(UiText.T("DELETE SAVE", "УДАЛИТЬ СОХРАНЕНИЕ"), UiText.F("Permanently delete \"{0}\"?", "Навсегда удалить \"{0}\"?", SafePendingName()), UiText.T("DELETE", "УДАЛИТЬ"), DialogMode.Load, DeletePendingSave);
            else if (dialogMode == DialogMode.ConfirmOverwrite)
                DrawConfirmation(UiText.T("OVERWRITE SAVE", "ПЕРЕЗАПИСАТЬ СОХРАНЕНИЕ"), UiText.F("Replace \"{0}\" with the current Timeline?", "Заменить \"{0}\" текущим таймлайном?", SafePendingName()), UiText.T("OVERWRITE", "ПЕРЕЗАПИСАТЬ"), DialogMode.Load, () => OverwritePendingSave(timeline));
        }

        private void DrawSaveDialog(CinematicTimeline timeline)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label(UiText.T("SAVE TIMELINE PROJECT", "СОХРАНЕНИЕ ПРОЕКТА ТАЙМЛАЙНА"));
            GUILayout.Label(UiText.T("Level: ", "Уровень: ") + saveRepository.CurrentLevelName);
            GUILayout.Label(UiText.T("This project can only be loaded on this level.", "Этот проект можно загрузить только на данном уровне."));
            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(UiText.T("Project name", "Название проекта"), GUILayout.Width(130f));
            GUI.SetNextControlName("timeline-save-name");
            saveNameInput = GUILayout.TextField(saveNameInput ?? "", 64);
            GUILayout.EndHorizontal();
            DrawDialogError();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UiText.T("CANCEL", "ОТМЕНА"), GUILayout.Height(30f))) { dialogMode = DialogMode.None; dialogError = ""; }
            if (GUILayout.Button(UiText.T("SAVE", "СОХРАНИТЬ"), GUILayout.Height(30f)))
            {
                string error;
                if (saveRepository.Create(saveNameInput, timeline, out error))
                {
                    statusMessage = UiText.T("Saved project: ", "Проект сохранён: ") + saveNameInput.Trim();
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
            GUILayout.Label(UiText.T("LOAD TIMELINE PROJECT — ", "ЗАГРУЗКА ПРОЕКТА ТАЙМЛАЙНА — ") + saveRepository.CurrentLevelName);
            GUILayout.Label(UiText.T("Only projects saved on this exact level are shown.", "Показаны только проекты, сохранённые на этом уровне."));
            DrawDialogError();
            saveListScroll = GUILayout.BeginScrollView(saveListScroll, GUILayout.Height(window.height - 145f));
            if (savedProjects.Count == 0) GUILayout.Label(UiText.T("No saved Timeline projects on this level.", "На этом уровне пока нет сохранённых проектов таймлайна."));

            TimelineSaveEntry loadEntry = null, overwriteEntry = null, deleteEntry = null;
            for (int i = 0; i < savedProjects.Count; i++)
            {
                TimelineSaveEntry entry = savedProjects[i];
                GUILayout.BeginHorizontal("box");
                GUILayout.BeginVertical();
                GUILayout.Label(entry.Data.ProjectName);
                GUILayout.Label(entry.Data.Points.Length + UiText.T(" points  •  ", " точек  •  ") + entry.Data.FlightDuration.ToString("0.00") + UiText.T("s  •  ", "с  •  ") + FormatModifiedTime(entry.Data.ModifiedUtcTicks));
                if (!string.IsNullOrEmpty(entry.Warning)) GUILayout.Label(UiText.T("WARNING: ", "ПРЕДУПРЕЖДЕНИЕ: ") + entry.Warning);
                GUILayout.EndVertical();
                if (GUILayout.Button(UiText.T("LOAD", "ЗАГРУЗИТЬ"), GUILayout.Width(90f), GUILayout.Height(42f))) loadEntry = entry;
                if (GUILayout.Button(UiText.T("OVERWRITE", "ПЕРЕЗАПИСАТЬ"), GUILayout.Width(130f), GUILayout.Height(42f))) overwriteEntry = entry;
                if (GUILayout.Button(UiText.T("DELETE", "УДАЛИТЬ"), GUILayout.Width(90f), GUILayout.Height(42f))) deleteEntry = entry;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button(UiText.T("BACK", "НАЗАД"), GUILayout.Height(30f))) { dialogMode = DialogMode.None; dialogError = ""; }
            GUILayout.EndVertical();

            if (loadEntry != null) LoadProject(loadEntry, timeline);
            else if (overwriteEntry != null) { pendingSave = overwriteEntry; dialogError = ""; dialogMode = DialogMode.ConfirmOverwrite; }
            else if (deleteEntry != null) { pendingSave = deleteEntry; dialogError = ""; dialogMode = DialogMode.ConfirmDelete; }
        }

        private void DrawSettingsDialog()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label(UiText.T("ULTRACINEMATIC SETTINGS", "НАСТРОЙКИ ULTRACINEMATIC"));
            GUILayout.Space(10f);

            GUILayout.Label(UiText.T("Language", "Язык"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(preferences.Language == InterfaceLanguage.English, "ENGLISH", "Button", GUILayout.Height(32f)) && preferences.Language != InterfaceLanguage.English)
            {
                preferences.Language = InterfaceLanguage.English;
                settingsMessage = UiText.T("Language changed to English.", "Язык изменён на английский.");
                settingsError = "";
                controller.NotifyInterfaceSettingsChanged();
            }
            if (GUILayout.Toggle(preferences.Language == InterfaceLanguage.Russian, "РУССКИЙ", "Button", GUILayout.Height(32f)) && preferences.Language != InterfaceLanguage.Russian)
            {
                preferences.Language = InterfaceLanguage.Russian;
                settingsMessage = UiText.T("Language changed to Russian.", "Язык изменён на русский.");
                settingsError = "";
                controller.NotifyInterfaceSettingsChanged();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(14f);
            GUILayout.Label(UiText.T("UI style", "Стиль интерфейса"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(preferences.Style == InterfaceStyle.Classic, UiText.T("CLASSIC", "КЛАССИЧЕСКИЙ"), "Button", GUILayout.Height(32f)) && preferences.Style != InterfaceStyle.Classic)
            {
                preferences.Style = InterfaceStyle.Classic;
                settingsMessage = UiText.T("Classic UI style selected.", "Выбран классический стиль интерфейса.");
                settingsError = "";
            }
            if (GUILayout.Toggle(preferences.Style == InterfaceStyle.Dark, UiText.T("DARK", "ТЁМНЫЙ"), "Button", GUILayout.Height(32f)) && preferences.Style != InterfaceStyle.Dark)
            {
                preferences.Style = InterfaceStyle.Dark;
                settingsMessage = UiText.T("Dark UI style selected.", "Выбран тёмный стиль интерфейса.");
                settingsError = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(14f);
            GUILayout.Label(UiText.T("Timeline saves folder", "Папка сохранений таймлайна"));
            GUILayout.Label(UiText.T("Current folder:", "Текущая папка:"));
            GUILayout.TextArea(saveRepository.RootDirectory, GUILayout.Height(44f));
            GUILayout.Label(UiText.T("New absolute folder path:", "Новый полный путь к папке:"));
            saveDirectoryInput = GUILayout.TextField(saveDirectoryInput ?? "");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UiText.T("APPLY FOLDER", "ПРИМЕНИТЬ ПАПКУ"), GUILayout.Height(32f))) ApplySaveDirectory();
            if (GUILayout.Button(UiText.T("USE DEFAULT", "ВЕРНУТЬ СТАНДАРТНУЮ"), GUILayout.Height(32f))) ResetSaveDirectory();
            GUILayout.EndHorizontal();
            GUILayout.Label(UiText.T("Changing the folder does not move existing project files.", "При смене папки существующие файлы проектов не перемещаются."));

            if (!string.IsNullOrEmpty(settingsError)) GUILayout.Label(UiText.T("ERROR: ", "ОШИБКА: ") + settingsError);
            else if (!string.IsNullOrEmpty(settingsMessage)) GUILayout.Label(settingsMessage);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(UiText.T("BACK", "НАЗАД"), GUILayout.Height(34f)))
            {
                settingsMessage = "";
                settingsError = "";
                dialogMode = DialogMode.None;
            }
            GUILayout.EndVertical();
        }

        private void ApplySaveDirectory()
        {
            string error;
            if (!preferences.TrySetTimelineDirectory(saveDirectoryInput, out error))
            {
                settingsError = error;
                settingsMessage = "";
                return;
            }
            saveDirectoryInput = preferences.TimelineDirectory;
            saveRepository.SetRootDirectory(preferences.TimelineDirectory);
            settingsError = "";
            settingsMessage = UiText.T("Timeline save folder updated.", "Папка сохранений таймлайна обновлена.");
        }

        private void ResetSaveDirectory()
        {
            string error;
            if (!preferences.TryResetTimelineDirectory(out error))
            {
                settingsError = error;
                settingsMessage = "";
                return;
            }
            saveDirectoryInput = preferences.TimelineDirectory;
            saveRepository.SetRootDirectory(preferences.TimelineDirectory);
            settingsError = "";
            settingsMessage = UiText.T("Default Timeline save folder restored.", "Стандартная папка сохранений таймлайна восстановлена.");
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
            if (GUILayout.Button(UiText.T("CANCEL", "ОТМЕНА"), GUILayout.Height(34f))) { dialogMode = cancelMode; pendingSave = null; dialogError = ""; }
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

        private void OpenSettingsDialog()
        {
            EndPointPreview();
            saveDirectoryInput = preferences.TimelineDirectory;
            settingsMessage = "";
            settingsError = "";
            dialogMode = DialogMode.Settings;
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
            statusMessage = UiText.T("Loaded project: ", "Проект загружен: ") + entry.Data.ProjectName;
            dialogMode = DialogMode.None;
        }

        private void OverwritePendingSave(CinematicTimeline timeline)
        {
            string error;
            if (!saveRepository.Overwrite(pendingSave, timeline, out error)) { dialogError = error; return; }
            statusMessage = UiText.T("Overwritten project: ", "Проект перезаписан: ") + SafePendingName();
            pendingSave = null;
            RefreshSavedProjects();
            dialogMode = DialogMode.Load;
        }

        private void DeletePendingSave()
        {
            string deletedName = SafePendingName();
            string error;
            if (!saveRepository.Delete(pendingSave, out error)) { dialogError = error; return; }
            statusMessage = UiText.T("Deleted project: ", "Проект удалён: ") + deletedName;
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
            statusMessage = UiText.T("Current Timeline project cleared.", "Текущий проект таймлайна очищен.");
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
            if (!string.IsNullOrEmpty(dialogError)) GUILayout.Label(UiText.T("ERROR: ", "ОШИБКА: ") + dialogError);
        }

        private string SafePendingName() => pendingSave == null || pendingSave.Data == null ? UiText.T("unknown", "неизвестно") : pendingSave.Data.ProjectName;

        private static string FormatModifiedTime(long ticks)
        {
            try { return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm"); }
            catch { return UiText.T("unknown time", "неизвестное время"); }
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
            GUILayout.Label(UiText.T("SEGMENT SETTINGS", "НАСТРОЙКИ СЕГМЕНТА"));
            if (timeline.SegmentCount > 0)
            {
                selectedSegment = Mathf.Clamp(selectedSegment, 0, timeline.SegmentCount - 1);
                GUILayout.BeginHorizontal(); GUILayout.Label(UiText.T("Segments", "Сегменты"), GUILayout.Width(80));
                for (int i = 0; i < timeline.SegmentCount; i++)
                    if (GUILayout.Toggle(selectedSegment == i, SegmentLabel(i), "Button", GUILayout.Width(42)) && selectedSegment != i) selectedSegment = i;
                GUILayout.EndHorizontal();
                GUILayout.Label(UiText.F("Segment {0}: Point {1} → Point {2}", "Сегмент {0}: точка {1} → точка {2}", SegmentLabel(selectedSegment), selectedSegment + 1, selectedSegment + 2));
                GUILayout.BeginHorizontal(); GUILayout.Label(UiText.T("Path", "Траектория"), GUILayout.Width(90));
                foreach (PathType path in System.Enum.GetValues(typeof(PathType)))
                    if (GUILayout.Toggle(timeline.GetPath(selectedSegment) == path, PathLabel(path), "Button") && timeline.GetPath(selectedSegment) != path) { timeline.SetPath(selectedSegment, path); controller.RefreshVisualization(); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(); GUILayout.Label(UiText.T("Easing", "Сглаживание"), GUILayout.Width(90));
                foreach (EasingType easing in System.Enum.GetValues(typeof(EasingType)))
                    if (GUILayout.Toggle(timeline.GetEasing(selectedSegment) == easing, EasingLabel(easing), "Button") && timeline.GetEasing(selectedSegment) != easing) { timeline.SetEasing(selectedSegment, easing); controller.RefreshVisualization(); }
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label(UiText.T("No segments yet.", "Сегментов пока нет."));
            GUILayout.EndVertical();
        }

        private void DrawPointSettings(CinematicTimeline timeline)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label(UiText.T("CAMERA POINT SETTINGS", "НАСТРОЙКИ ТОЧКИ КАМЕРЫ"));
            if (selectedPoint < 0 || selectedPoint >= timeline.Points.Count)
            {
                GUILayout.Label(UiText.T("Click a numbered Camera Point on the Timeline to edit its transform.", "Нажмите на пронумерованную точку камеры на таймлайне, чтобы изменить её параметры."));
                GUILayout.EndVertical();
                return;
            }

            EnsurePointInputs();
            GUILayout.Label(UiText.F("Point {0} — world position and camera rotation", "Точка {0} — позиция в мире и поворот камеры", selectedPoint + 1));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label(UiText.T("Position", "Позиция"));
            DrawPointField("X", 0, .02f);
            DrawPointField("Y", 1, .02f);
            DrawPointField("Z", 2, .02f);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label(UiText.T("Rotation (degrees)", "Поворот (градусы)"));
            DrawPointField(UiText.T("Pitch X", "Наклон X"), 3, .25f);
            DrawPointField(UiText.T("Yaw Y", "Поворот Y"), 4, .25f);
            DrawPointField(UiText.T("Roll Z", "Крен Z"), 5, .25f);
            DrawPointField("FOV", 6, .1f);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            string previewLabel = pointPreviewActive ? UiText.T("RETURN", "ВЕРНУТЬСЯ") : UiText.T("PREVIEW POINT", "ПРЕДПРОСМОТР ТОЧКИ");
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
            GUILayout.Label(UiText.T("CINEMATIC SETTINGS", "НАСТРОЙКИ ПРОЛЁТА"));
            GUILayout.BeginHorizontal();
            GUILayout.Label(UiText.T("Flight time", "Время пролёта"), GUILayout.Width(120));
            if (GUILayout.Button("-0.10", GUILayout.Width(58))) SetFlightTime(Mathf.Max(.1f, timeline.FlightDuration - .1f));
            flightTimeInput = GUILayout.TextField(flightTimeInput, GUILayout.Width(80));
            GUILayout.Label(UiText.T("seconds total", "секунд всего"), GUILayout.Width(96));
            if (GUILayout.Button(UiText.T("Apply", "Применить"), GUILayout.Width(78))) ApplyFlightTimeInput();
            if (GUILayout.Button("+0.10", GUILayout.Width(58))) SetFlightTime(timeline.FlightDuration + .1f);
            GUILayout.EndHorizontal();
            GUILayout.Label(UiText.T("Segment timing is calculated automatically from the measured path length.", "Время сегментов рассчитывается автоматически по измеренной длине траектории."));
            GUILayout.BeginHorizontal();
            GUILayout.Label(UiText.T("Soft points", "Мягкие точки"), GUILayout.Width(120));
            bool softPointsEnabled = GUILayout.Toggle(timeline.SoftPointsEnabled, timeline.SoftPointsEnabled ? UiText.T("ENABLED", "ВКЛЮЧЕНО") : UiText.T("DISABLED", "ВЫКЛЮЧЕНО"), "Button", GUILayout.Width(120));
            if (softPointsEnabled != timeline.SoftPointsEnabled)
            {
                timeline.SetSoftPointsEnabled(softPointsEnabled);
                controller.RefreshVisualization();
            }
            GUILayout.Label(UiText.T("Rounds internal points for every Path type.", "Сглаживает внутренние точки для любого типа траектории."));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(UiText.T("Before point", "До точки"), GUILayout.Width(120));
            softIncomingInput = GUILayout.TextField(softIncomingInput, GUILayout.Width(70));
            GUILayout.Label("%", GUILayout.Width(18));
            GUILayout.Label(UiText.T("After point", "После точки"), GUILayout.Width(92));
            softOutgoingInput = GUILayout.TextField(softOutgoingInput, GUILayout.Width(70));
            GUILayout.Label("%", GUILayout.Width(18));
            if (GUILayout.Button(UiText.T("Apply", "Применить"), GUILayout.Width(78))) ApplySoftPointInputs();
            GUILayout.EndHorizontal();
            GUILayout.Label(UiText.T("Range: 1–45% on each side. First and last Camera Points always remain exact.", "Диапазон: 1–45% с каждой стороны. Первая и последняя точки всегда остаются точными."));
            GUILayout.BeginHorizontal();
            GUILayout.Label(UiText.T("Playback world", "Режим мира"), GUILayout.Width(120));
            if (GUILayout.Toggle(timeline.PlaybackMode == CinematicPlaybackMode.LiveWorld, UiText.T("LIVE WORLD", "ЖИВОЙ МИР"), "Button") && timeline.PlaybackMode != CinematicPlaybackMode.LiveWorld)
                timeline.PlaybackMode = CinematicPlaybackMode.LiveWorld;
            if (GUILayout.Toggle(timeline.PlaybackMode == CinematicPlaybackMode.FrozenWorld, UiText.T("FROZEN WORLD", "ЗАМОРОЖЕННЫЙ МИР"), "Button") && timeline.PlaybackMode != CinematicPlaybackMode.FrozenWorld)
                timeline.PlaybackMode = CinematicPlaybackMode.FrozenWorld;
            GUILayout.EndHorizontal();
            GUILayout.Label(timeline.PlaybackMode == CinematicPlaybackMode.FrozenWorld
                ? UiText.T("The world is paused; the cinematic advances on unscaled time.", "Мир остановлен; пролёт воспроизводится по независимому времени.")
                : UiText.T("The world continues running during the cinematic.", "Мир продолжает работать во время пролёта."));
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
            GUILayout.Label(label, GUILayout.Width(76));
            GUI.SetNextControlName("point-field-" + field);
            pointInputs[field] = GUILayout.TextField(pointInputs[field] ?? "0.00", GUILayout.Width(82));
            if (GUILayout.Button(UiText.T("SET", "ЗАД."), GUILayout.Width(44))) ApplyPointInput(field);
            Rect scrubRect = GUILayoutUtility.GetRect(68, 22, GUILayout.Width(68));
            GUI.Box(scrubRect, UiText.T("↔ DRAG", "↔ ТЯНУТЬ"));
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

        private static string PathLabel(PathType path)
        {
            if (path == PathType.Linear) return UiText.T("Linear", "Линейная");
            if (path == PathType.Bezier) return UiText.T("Bezier", "Безье");
            return UiText.T("Smooth", "Плавная");
        }

        private static string EasingLabel(EasingType easing)
        {
            if (easing == EasingType.Linear) return UiText.T("Linear", "Линейное");
            if (easing == EasingType.EaseIn) return UiText.T("Ease In", "Разгон");
            if (easing == EasingType.EaseOut) return UiText.T("Ease Out", "Торможение");
            return UiText.T("Ease In/Out", "Разгон и торможение");
        }

        private GUISkin GetDarkSkin(GUISkin source)
        {
            if (darkSkin != null && darkSkinSource == source) return darkSkin;
            if (darkSkin != null) Destroy(darkSkin);
            for (int i = 0; i < themeTextures.Count; i++) if (themeTextures[i] != null) Destroy(themeTextures[i]);
            themeTextures.Clear();

            darkSkinSource = source;
            darkSkin = Instantiate(source);
            darkSkin.name = "UltraCinematic Dark Skin";
            Color text = new Color(.94f, .96f, 1f, 1f);
            Color mutedText = new Color(.76f, .81f, .88f, 1f);
            ApplyBackground(darkSkin.window.normal, new Color(.055f, .065f, .085f, .98f), text);
            ApplyBackground(darkSkin.box.normal, new Color(.09f, .105f, .135f, .98f), text);
            ConfigureInteractiveStyle(darkSkin.button, text);
            ConfigureInteractiveStyle(darkSkin.toggle, text);
            ConfigureTextStyle(darkSkin.textField, text);
            ConfigureTextStyle(darkSkin.textArea, text);
            darkSkin.label.normal.textColor = mutedText;
            darkSkin.window.normal.textColor = text;
            darkSkin.box.normal.textColor = text;
            darkSkin.settings.selectionColor = new Color(.15f, .58f, .7f, 1f);
            return darkSkin;
        }

        private void ConfigureInteractiveStyle(GUIStyle style, Color text)
        {
            ApplyBackground(style.normal, new Color(.16f, .19f, .24f, 1f), text);
            ApplyBackground(style.hover, new Color(.24f, .3f, .39f, 1f), text);
            ApplyBackground(style.active, new Color(.1f, .5f, .6f, 1f), Color.white);
            ApplyBackground(style.focused, new Color(.2f, .25f, .32f, 1f), text);
            ApplyBackground(style.onNormal, new Color(.12f, .43f, .5f, 1f), Color.white);
            ApplyBackground(style.onHover, new Color(.16f, .53f, .62f, 1f), Color.white);
            ApplyBackground(style.onActive, new Color(.09f, .37f, .44f, 1f), Color.white);
            ApplyBackground(style.onFocused, new Color(.13f, .47f, .55f, 1f), Color.white);
        }

        private void ConfigureTextStyle(GUIStyle style, Color text)
        {
            ApplyBackground(style.normal, new Color(.035f, .043f, .058f, 1f), text);
            ApplyBackground(style.hover, new Color(.05f, .065f, .085f, 1f), text);
            ApplyBackground(style.active, new Color(.04f, .08f, .1f, 1f), text);
            ApplyBackground(style.focused, new Color(.04f, .08f, .1f, 1f), text);
        }

        private void ApplyBackground(GUIStyleState state, Color background, Color text)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.ARGB32, false) { name = "UltraCinematic Theme Pixel" };
            texture.SetPixel(0, 0, background);
            texture.Apply(false, true);
            themeTextures.Add(texture);
            state.background = texture;
            state.textColor = text;
        }

        private static string SegmentLabel(int index) { index++; string result = ""; while (index > 0) { index--; result = (char)('A' + index % 26) + result; index /= 26; } return result; }
    }
}
