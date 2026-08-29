using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using Newtonsoft.Json;
using UltraCinematic.Data;
using UltraCinematic.Timeline;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UltraCinematic.Persistence
{
    [Serializable]
    internal sealed class TimelineProjectData
    {
        public int SchemaVersion = 1;
        public string ProjectId;
        public string ProjectName;
        public string LevelId;
        public long ModifiedUtcTicks;
        public float FlightDuration;
        public int PlaybackMode;
        public bool SoftPointsEnabled;
        public float SoftPointIncomingPercent;
        public float SoftPointOutgoingPercent;
        public TimelinePointData[] Points;
        public TimelineSegmentData[] Segments;
    }

    [Serializable]
    internal sealed class TimelinePointData
    {
        public float PositionX, PositionY, PositionZ;
        public float RotationX, RotationY, RotationZ, RotationW;
        public float FieldOfView;
    }

    [Serializable]
    internal sealed class TimelineSegmentData
    {
        public int PathType;
        public int EasingType;
    }

    internal sealed class TimelineSaveEntry
    {
        internal TimelineProjectData Data;
        internal string FilePath;
        internal string Warning;
    }

    internal sealed class TimelineSaveRepository
    {
        private const int SchemaVersion = 1;
        private readonly string root = Path.Combine(Paths.ConfigPath, "UltraCinematic", "Timelines");

        internal string CurrentLevelName => SceneManager.GetActiveScene().name;

        internal List<TimelineSaveEntry> ListCurrentLevel(out string error)
        {
            error = "";
            List<TimelineSaveEntry> result = new List<TimelineSaveEntry>();
            try
            {
                string levelId = CurrentLevelId();
                string directory = CurrentLevelDirectory(levelId);
                if (!Directory.Exists(directory)) return result;
                foreach (string file in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    TimelineProjectData data;
                    string validationError;
                    string warning;
                    if (!TryRead(file, levelId, out data, out validationError, out warning)) continue;
                    result.Add(new TimelineSaveEntry { Data = data, FilePath = file, Warning = warning });
                }
                result.Sort((a, b) => b.Data.ModifiedUtcTicks.CompareTo(a.Data.ModifiedUtcTicks));
            }
            catch (Exception exception) { error = "Could not list saves: " + exception.Message; }
            return result;
        }

        internal bool Create(string projectName, CinematicTimeline timeline, out string error)
        {
            error = ValidateProjectName(projectName);
            if (error.Length > 0) return false;
            string normalizedName = projectName.Trim();
            string listError;
            List<TimelineSaveEntry> existing = ListCurrentLevel(out listError);
            if (listError.Length > 0) { error = listError; return false; }
            if (existing.Exists(entry => string.Equals(entry.Data.ProjectName, normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                error = "A save with this name already exists. Use OVERWRITE in LOAD.";
                return false;
            }

            TimelineProjectData data = Capture(timeline, normalizedName, Guid.NewGuid().ToString("N"));
            return Write(data, null, out error);
        }

        internal bool Overwrite(TimelineSaveEntry entry, CinematicTimeline timeline, out string error)
        {
            if (entry == null || entry.Data == null) { error = "Save entry is unavailable."; return false; }
            TimelineProjectData data = Capture(timeline, entry.Data.ProjectName, entry.Data.ProjectId);
            return Write(data, entry.FilePath, out error);
        }

        internal bool Delete(TimelineSaveEntry entry, out string error)
        {
            error = "";
            if (entry == null || string.IsNullOrEmpty(entry.FilePath)) { error = "Save entry is unavailable."; return false; }
            try
            {
                if (File.Exists(entry.FilePath)) File.Delete(entry.FilePath);
                return true;
            }
            catch (Exception exception) { error = "Could not delete save: " + exception.Message; return false; }
        }

        internal bool Apply(TimelineSaveEntry entry, CinematicTimeline timeline, out string error)
        {
            error = "";
            if (entry == null || entry.Data == null) { error = "Save entry is unavailable."; return false; }
            string validationError = Validate(entry.Data, CurrentLevelId());
            if (validationError.Length > 0) { error = validationError; return false; }

            TimelineProjectData data = entry.Data;
            timeline.Clear();
            for (int i = 0; i < data.Points.Length; i++)
            {
                TimelinePointData point = data.Points[i];
                timeline.Add(new CameraPoint
                {
                    Position = new Vector3(point.PositionX, point.PositionY, point.PositionZ),
                    Rotation = new Quaternion(point.RotationX, point.RotationY, point.RotationZ, point.RotationW),
                    FieldOfView = point.FieldOfView
                });
            }
            for (int i = 0; i < data.Segments.Length; i++)
            {
                timeline.SetPath(i, (PathType)data.Segments[i].PathType);
                timeline.SetEasing(i, (EasingType)data.Segments[i].EasingType);
            }
            timeline.SetFlightDuration(data.FlightDuration);
            timeline.SetSoftPointWindows(data.SoftPointIncomingPercent, data.SoftPointOutgoingPercent);
            timeline.SetSoftPointsEnabled(data.SoftPointsEnabled);
            timeline.PlaybackMode = (CinematicPlaybackMode)data.PlaybackMode;
            timeline.CursorTime = 0f;
            return true;
        }

        private TimelineProjectData Capture(CinematicTimeline timeline, string projectName, string projectId)
        {
            TimelineProjectData data = new TimelineProjectData
            {
                SchemaVersion = SchemaVersion,
                ProjectId = projectId,
                ProjectName = projectName,
                LevelId = CurrentLevelId(),
                ModifiedUtcTicks = DateTime.UtcNow.Ticks,
                FlightDuration = timeline.FlightDuration,
                PlaybackMode = (int)timeline.PlaybackMode,
                SoftPointsEnabled = timeline.SoftPointsEnabled,
                SoftPointIncomingPercent = timeline.SoftPointIncomingPercent,
                SoftPointOutgoingPercent = timeline.SoftPointOutgoingPercent,
                Points = new TimelinePointData[timeline.Points.Count],
                Segments = new TimelineSegmentData[timeline.SegmentCount]
            };
            for (int i = 0; i < timeline.Points.Count; i++)
            {
                CameraPoint point = timeline.Points[i];
                data.Points[i] = new TimelinePointData
                {
                    PositionX = point.Position.x, PositionY = point.Position.y, PositionZ = point.Position.z,
                    RotationX = point.Rotation.x, RotationY = point.Rotation.y, RotationZ = point.Rotation.z, RotationW = point.Rotation.w,
                    FieldOfView = point.FieldOfView
                };
            }
            for (int i = 0; i < timeline.SegmentCount; i++)
                data.Segments[i] = new TimelineSegmentData { PathType = (int)timeline.GetPath(i), EasingType = (int)timeline.GetEasing(i) };
            return data;
        }

        private bool Write(TimelineProjectData data, string existingPath, out string error)
        {
            error = "";
            string temporaryPath = null;
            try
            {
                string directory = CurrentLevelDirectory(data.LevelId);
                Directory.CreateDirectory(directory);
                string targetPath = existingPath ?? Path.Combine(directory, data.ProjectId + ".json");
                temporaryPath = targetPath + ".tmp";
                File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(data, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(targetPath))
                {
                    try { File.Replace(temporaryPath, targetPath, null); }
                    catch (PlatformNotSupportedException) { File.Copy(temporaryPath, targetPath, true); File.Delete(temporaryPath); }
                }
                else File.Move(temporaryPath, targetPath);
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not write save: " + exception.Message;
                try { if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                return false;
            }
        }

        private bool TryRead(string file, string expectedLevelId, out TimelineProjectData data, out string error, out string warning)
        {
            data = null;
            error = "";
            warning = "";
            try
            {
                data = JsonConvert.DeserializeObject<TimelineProjectData>(File.ReadAllText(file));
                if (data != null && data.Points == null && data.Segments == null)
                {
                    data.Points = new TimelinePointData[0];
                    data.Segments = new TimelineSegmentData[0];
                    warning = "Legacy 1.4.0 save: point data was not written and cannot be recovered.";
                }
                error = Validate(data, expectedLevelId);
                return error.Length == 0;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private static string Validate(TimelineProjectData data, string expectedLevelId)
        {
            if (data == null) return "Save data is empty.";
            if (data.SchemaVersion != SchemaVersion) return "Unsupported save version.";
            if (!string.Equals(data.LevelId, expectedLevelId, StringComparison.Ordinal)) return "This save belongs to another level.";
            if (!IsValidName(data.ProjectName) || string.IsNullOrEmpty(data.ProjectId)) return "Save identity is invalid.";
            if (!Finite(data.FlightDuration) || data.FlightDuration < .1f) return "Flight time is invalid.";
            if (!Enum.IsDefined(typeof(CinematicPlaybackMode), data.PlaybackMode)) return "Playback mode is invalid.";
            if (!Finite(data.SoftPointIncomingPercent) || data.SoftPointIncomingPercent < 1f || data.SoftPointIncomingPercent > 45f ||
                !Finite(data.SoftPointOutgoingPercent) || data.SoftPointOutgoingPercent < 1f || data.SoftPointOutgoingPercent > 45f) return "Soft Point settings are invalid.";
            if (data.Points == null || data.Points.Length > 10000) return "Camera Points are invalid.";
            if (data.Segments == null || data.Segments.Length != Math.Max(0, data.Points.Length - 1)) return "Segments are invalid.";
            for (int i = 0; i < data.Points.Length; i++)
            {
                TimelinePointData point = data.Points[i];
                if (point == null || !Finite(point.PositionX) || !Finite(point.PositionY) || !Finite(point.PositionZ) ||
                    !Finite(point.RotationX) || !Finite(point.RotationY) || !Finite(point.RotationZ) || !Finite(point.RotationW) ||
                    !Finite(point.FieldOfView) || point.FieldOfView < 1f || point.FieldOfView > 179f) return "Camera Point " + (i + 1) + " is invalid.";
            }
            for (int i = 0; i < data.Segments.Length; i++)
            {
                TimelineSegmentData segment = data.Segments[i];
                if (segment == null || !Enum.IsDefined(typeof(PathType), segment.PathType) || !Enum.IsDefined(typeof(EasingType), segment.EasingType))
                    return "Segment " + (i + 1) + " is invalid.";
            }
            return "";
        }

        private string CurrentLevelId()
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.name + "|" + scene.path;
        }

        private string CurrentLevelDirectory(string levelId)
        {
            string safeName = SanitizeFileName(CurrentLevelName);
            return Path.Combine(root, safeName + "_" + Hash(levelId).Substring(0, 16));
        }

        private static string ValidateProjectName(string value)
        {
            if (!IsValidName(value)) return "Name must contain 1–64 visible characters.";
            return "";
        }

        private static bool IsValidName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string trimmed = value.Trim();
            if (trimmed.Length > 64) return false;
            for (int i = 0; i < trimmed.Length; i++) if (char.IsControl(trimmed[i])) return false;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "UnknownLevel";
            StringBuilder builder = new StringBuilder(value.Length);
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < value.Length; i++) builder.Append(Array.IndexOf(invalid, value[i]) >= 0 ? '_' : value[i]);
            return builder.ToString();
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
