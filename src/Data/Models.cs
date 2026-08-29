using UnityEngine;

namespace UltraCinematic.Data
{
    public sealed class CameraPoint { public Vector3 Position; public Quaternion Rotation; public float FieldOfView; }
    public sealed class TimelineKeyframe { public CameraPoint Point; public float Time; }
    public enum PathType { Linear, Bezier, Smooth }
    public enum EasingType { Linear, EaseIn, EaseOut, EaseInOut }
    public enum CinematicPlaybackMode { LiveWorld, FrozenWorld }
    public sealed class TimelineSegment { public TimelineKeyframe From; public TimelineKeyframe To; public PathType PathType; public EasingType EasingType; }
    public readonly struct CameraState
    {
        public readonly Vector3 Position; public readonly Quaternion Rotation; public readonly float FieldOfView;
        public CameraState(Vector3 position, Quaternion rotation, float fov) { Position = position; Rotation = rotation; FieldOfView = fov; }
    }
}
