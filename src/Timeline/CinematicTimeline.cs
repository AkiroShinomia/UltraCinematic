using System;
using System.Collections.Generic;
using UltraCinematic.Data;
using UnityEngine;

namespace UltraCinematic.Timeline
{
    public sealed class CinematicTimeline
    {
        private const float DefaultFlightDuration = 5f;
        private const int ArcSamples = 64;
        public readonly List<CameraPoint> Points = new List<CameraPoint>();
        public readonly List<TimelineKeyframe> Keyframes = new List<TimelineKeyframe>();
        private readonly List<PathType> paths = new List<PathType>();
        private readonly List<EasingType> easings = new List<EasingType>();
        private readonly List<float[]> arcDistances = new List<float[]>();
        private readonly List<float> segmentLengths = new List<float>();
        private float flightDuration = DefaultFlightDuration;
        private bool softPointsEnabled = true;
        private float softPointIncoming = .1f;
        private float softPointOutgoing = .1f;
        public float CursorTime { get; set; }
        public CinematicPlaybackMode PlaybackMode { get; set; } = CinematicPlaybackMode.LiveWorld;
        public float FlightDuration => flightDuration;
        public bool SoftPointsEnabled => softPointsEnabled;
        public float SoftPointIncomingPercent => softPointIncoming * 100f;
        public float SoftPointOutgoingPercent => softPointOutgoing * 100f;
        public float Duration => SegmentCount == 0 ? 0f : flightDuration;
        public int SegmentCount => Math.Max(0, Keyframes.Count - 1);

        public void Add(CameraPoint point)
        {
            Points.Add(point); Keyframes.Add(new TimelineKeyframe { Point = point, Time = Keyframes.Count });
            if (Keyframes.Count > 1) { paths.Add(PathType.Linear); easings.Add(EasingType.Linear); }
            RebuildAutomaticTiming();
        }
        public bool RemoveLast()
        {
            if (Keyframes.Count == 0) return false;
            Points.RemoveAt(Points.Count - 1);
            Keyframes.RemoveAt(Keyframes.Count - 1);
            if (paths.Count > 0) paths.RemoveAt(paths.Count - 1);
            if (easings.Count > 0) easings.RemoveAt(easings.Count - 1);
            RebuildAutomaticTiming();
            return true;
        }
        public TimelineSegment GetSegment(int index) => new TimelineSegment { From = Keyframes[index], To = Keyframes[index + 1], PathType = paths[index], EasingType = easings[index] };
        public PathType GetPath(int index) => paths[index];
        public EasingType GetEasing(int index) => easings[index];
        public float GetSegmentDuration(int index)
        {
            if (index < 0 || index >= SegmentCount) return 0f;
            return Keyframes[index + 1].Time - Keyframes[index].Time;
        }
        public void SetPath(int index, PathType value)
        {
            if (index < 0 || index >= paths.Count || paths[index] == value) return;
            paths[index] = value;
            RebuildAutomaticTiming();
        }
        public void SetEasing(int index, EasingType value) { if (index >= 0 && index < easings.Count) easings[index] = value; }
        public void SetFlightDuration(float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration)) return;
            flightDuration = Mathf.Max(.1f, duration);
            RebuildAutomaticTiming();
        }

        public void SetSoftPointsEnabled(bool value)
        {
            if (softPointsEnabled == value) return;
            softPointsEnabled = value;
            RebuildAutomaticTiming();
        }

        public void SetSoftPointWindows(float incomingPercent, float outgoingPercent)
        {
            if (float.IsNaN(incomingPercent) || float.IsInfinity(incomingPercent) ||
                float.IsNaN(outgoingPercent) || float.IsInfinity(outgoingPercent)) return;
            softPointIncoming = Mathf.Clamp(incomingPercent, 1f, 45f) / 100f;
            softPointOutgoing = Mathf.Clamp(outgoingPercent, 1f, 45f) / 100f;
            RebuildAutomaticTiming();
        }

        public void RebuildAutomaticTiming()
        {
            arcDistances.Clear();
            segmentLengths.Clear();
            if (SegmentCount == 0)
            {
                if (Keyframes.Count == 1) Keyframes[0].Time = 0f;
                CursorTime = 0f;
                return;
            }

            for (int i = 0; i < Keyframes.Count; i++) Keyframes[i].Time = flightDuration * i / SegmentCount;
            for (int iteration = 0; iteration < 4; iteration++)
            {
                BuildArcTables();
                ApplyLengthWeightedTimes();
            }
            BuildArcTables();
            CursorTime = Mathf.Clamp(CursorTime, 0f, Duration);
        }

        internal float GetPathParameter(int segmentIndex, float distanceFraction)
        {
            distanceFraction = Mathf.Clamp01(distanceFraction);
            if (segmentIndex < 0 || segmentIndex >= arcDistances.Count || segmentIndex >= segmentLengths.Count) return distanceFraction;
            float[] table = arcDistances[segmentIndex];
            float length = segmentLengths[segmentIndex];
            if (table == null || table.Length < 2 || length <= .00001f) return distanceFraction;
            float target = distanceFraction * length;
            int upper = 1;
            while (upper < table.Length - 1 && table[upper] < target) upper++;
            float lowerDistance = table[upper - 1], upperDistance = table[upper];
            float fraction = upperDistance - lowerDistance <= .00001f ? 0f : (target - lowerDistance) / (upperDistance - lowerDistance);
            return ((upper - 1) + fraction) / (table.Length - 1f);
        }

        private void BuildArcTables()
        {
            arcDistances.Clear();
            segmentLengths.Clear();
            for (int segment = 0; segment < SegmentCount; segment++)
            {
                float[] distances = new float[ArcSamples + 1];
                Vector3 previous = TimelineEvaluator.EvaluatePositionRaw(this, segment, 0f);
                float length = 0f;
                for (int sample = 1; sample <= ArcSamples; sample++)
                {
                    Vector3 current = TimelineEvaluator.EvaluatePositionRaw(this, segment, sample / (float)ArcSamples);
                    length += Vector3.Distance(previous, current);
                    distances[sample] = length;
                    previous = current;
                }
                arcDistances.Add(distances);
                segmentLengths.Add(length);
            }
        }

        private void ApplyLengthWeightedTimes()
        {
            float totalWeight = 0f;
            for (int i = 0; i < segmentLengths.Count; i++) totalWeight += Mathf.Max(.001f, segmentLengths[i]);
            float elapsed = 0f;
            Keyframes[0].Time = 0f;
            for (int i = 0; i < SegmentCount; i++)
            {
                elapsed += flightDuration * Mathf.Max(.001f, segmentLengths[i]) / totalWeight;
                Keyframes[i + 1].Time = i == SegmentCount - 1 ? flightDuration : elapsed;
            }
        }
        public int FindSegment(float time)
        {
            if (SegmentCount == 0) return -1;
            for (int i = 0; i < SegmentCount; i++) if (time <= Keyframes[i + 1].Time) return i;
            return SegmentCount - 1;
        }
        public void Clear()
        {
            Points.Clear(); Keyframes.Clear(); paths.Clear(); easings.Clear(); arcDistances.Clear(); segmentLengths.Clear();
            CursorTime = 0f; flightDuration = DefaultFlightDuration; softPointsEnabled = true;
            softPointIncoming = .1f; softPointOutgoing = .1f; PlaybackMode = CinematicPlaybackMode.LiveWorld;
        }
    }

    public static class Easing
    {
        public static float Apply(EasingType type, float t)
        {
            switch (type) { case EasingType.EaseIn: return t * t; case EasingType.EaseOut: return 1f - (1f - t) * (1f - t); case EasingType.EaseInOut: return Mathf.SmoothStep(0f, 1f, t); default: return t; }
        }

        public static float ApplyContinuous(EasingType type, float t)
        {
            float envelope = t * t * (1f - t) * (1f - t);
            switch (type)
            {
                case EasingType.EaseIn: return Mathf.Clamp01(t - envelope);
                case EasingType.EaseOut: return Mathf.Clamp01(t + envelope);
                case EasingType.EaseInOut: return Mathf.Clamp01(t + envelope * (2f * t - 1f) * 2f);
                default: return t;
            }
        }
    }

    public static class TimelineEvaluator
    {
        private const float DerivativeStep = .005f;

        public static CameraState Evaluate(CinematicTimeline timeline, float time)
        {
            if (timeline.Keyframes.Count == 0) return new CameraState(Vector3.zero, Quaternion.identity, 60f);
            if (timeline.Keyframes.Count == 1) return State(timeline.Keyframes[0].Point);
            int index = timeline.FindSegment(Mathf.Clamp(time, 0f, timeline.Duration));
            TimelineSegment segment = timeline.GetSegment(index);
            float span = segment.To.Time - segment.From.Time;
            float normalizedTime = span <= 0f ? 0f : Mathf.Clamp01((time - segment.From.Time) / span);
            float distanceFraction = segment.PathType == PathType.Smooth ? Easing.ApplyContinuous(segment.EasingType, normalizedTime) : Easing.Apply(segment.EasingType, normalizedTime);
            float t = timeline.GetPathParameter(index, distanceFraction);
            CameraPoint a = segment.From.Point, b = segment.To.Point;
            Vector3 position = EvaluatePositionRaw(timeline, index, t);
            Quaternion rotation = segment.PathType == PathType.Smooth ? SmoothRotation(timeline, index, t) : Quaternion.Slerp(a.Rotation, b.Rotation, t);
            float fieldOfView = segment.PathType == PathType.Smooth ? SmoothFieldOfView(timeline, index, t, span) : Mathf.Lerp(a.FieldOfView, b.FieldOfView, t);
            return new CameraState(position, rotation, fieldOfView);
        }

        internal static Vector3 EvaluatePositionRaw(CinematicTimeline timeline, int index, float t)
        {
            int internalPoint = -1;
            float transition = 0f;
            float incomingWindow = timeline.SoftPointIncomingPercent / 100f;
            float outgoingWindow = timeline.SoftPointOutgoingPercent / 100f;
            float segmentSpan = timeline.Keyframes[index + 1].Time - timeline.Keyframes[index].Time;
            float routeTime = timeline.Keyframes[index].Time + Mathf.Clamp01(t) * segmentSpan;
            if (timeline.SoftPointsEnabled && t >= 1f - incomingWindow && index + 1 < timeline.SegmentCount)
            {
                internalPoint = index + 1;
            }
            else if (timeline.SoftPointsEnabled && t <= outgoingWindow && index > 0)
            {
                internalPoint = index;
            }

            if (internalPoint > 0 && internalPoint < timeline.Keyframes.Count - 1)
            {
                float incomingSpan = timeline.Keyframes[internalPoint].Time - timeline.Keyframes[internalPoint - 1].Time;
                float outgoingSpan = timeline.Keyframes[internalPoint + 1].Time - timeline.Keyframes[internalPoint].Time;
                float windowStart = timeline.Keyframes[internalPoint].Time - incomingSpan * incomingWindow;
                float windowEnd = timeline.Keyframes[internalPoint].Time + outgoingSpan * outgoingWindow;
                transition = (routeTime - windowStart) / Mathf.Max(.00001f, windowEnd - windowStart);
                return EvaluateSoftPointTransition(timeline, internalPoint, incomingWindow, outgoingWindow, Mathf.Clamp01(transition));
            }

            return EvaluatePositionBaseRaw(timeline, index, t);
        }

        private static Vector3 EvaluateSoftPointTransition(CinematicTimeline timeline, int pointIndex, float incomingWindow, float outgoingWindow, float s)
        {
            int incoming = pointIndex - 1, outgoing = pointIndex;
            float incomingT = 1f - incomingWindow, outgoingT = outgoingWindow;
            float incomingSpan = timeline.Keyframes[pointIndex].Time - timeline.Keyframes[pointIndex - 1].Time;
            float outgoingSpan = timeline.Keyframes[pointIndex + 1].Time - timeline.Keyframes[pointIndex].Time;
            float windowDuration = incomingWindow * incomingSpan + outgoingWindow * outgoingSpan;
            float incomingScale = windowDuration / Mathf.Max(.00001f, incomingSpan);
            float outgoingScale = windowDuration / Mathf.Max(.00001f, outgoingSpan);
            Vector3 p0 = EvaluatePositionBaseRaw(timeline, incoming, incomingT);
            Vector3 p1 = EvaluatePositionBaseRaw(timeline, outgoing, outgoingT);
            Vector3 v0 = FirstDerivative(timeline, incoming, incomingT) * incomingScale;
            Vector3 v1 = FirstDerivative(timeline, outgoing, outgoingT) * outgoingScale;
            Vector3 a0 = SecondDerivative(timeline, incoming, incomingT) * incomingScale * incomingScale;
            Vector3 a1 = SecondDerivative(timeline, outgoing, outgoingT) * outgoingScale * outgoingScale;

            Vector3 delta = p1 - p0;
            Vector3 c0 = p0;
            Vector3 c1 = v0;
            Vector3 c2 = a0 * .5f;
            Vector3 c3 = delta * 10f - v0 * 6f - v1 * 4f - a0 * 1.5f + a1 * .5f;
            Vector3 c4 = delta * -15f + v0 * 8f + v1 * 7f + a0 * 1.5f - a1;
            Vector3 c5 = delta * 6f - v0 * 3f - v1 * 3f - a0 * .5f + a1 * .5f;
            return c0 + s * (c1 + s * (c2 + s * (c3 + s * (c4 + s * c5))));
        }

        private static Vector3 FirstDerivative(CinematicTimeline timeline, int segment, float t)
        {
            float from = Mathf.Max(0f, t - DerivativeStep), to = Mathf.Min(1f, t + DerivativeStep);
            return (EvaluatePositionBaseRaw(timeline, segment, to) - EvaluatePositionBaseRaw(timeline, segment, from)) / Mathf.Max(.00001f, to - from);
        }

        private static Vector3 SecondDerivative(CinematicTimeline timeline, int segment, float t)
        {
            float step = Mathf.Min(DerivativeStep, Mathf.Min(t, 1f - t));
            if (step <= .00001f) return Vector3.zero;
            Vector3 before = EvaluatePositionBaseRaw(timeline, segment, t - step);
            Vector3 current = EvaluatePositionBaseRaw(timeline, segment, t);
            Vector3 after = EvaluatePositionBaseRaw(timeline, segment, t + step);
            return (after - current * 2f + before) / (step * step);
        }

        private static Vector3 EvaluatePositionBaseRaw(CinematicTimeline timeline, int index, float t)
        {
            TimelineSegment segment = timeline.GetSegment(index);
            CameraPoint a = segment.From.Point, b = segment.To.Point;
            Vector3 position;
            if (segment.PathType == PathType.Linear) position = Vector3.LerpUnclamped(a.Position, b.Position, t);
            else if (segment.PathType == PathType.Bezier)
            {
                float d = Vector3.Distance(a.Position, b.Position), u = 1f - t;
                Vector3 previous = index > 0 ? timeline.Keyframes[index - 1].Point.Position : a.Position;
                Vector3 next = index + 2 < timeline.Keyframes.Count ? timeline.Keyframes[index + 2].Point.Position : b.Position;
                Vector3 tangentA = Vector3.ClampMagnitude((b.Position - previous) * .5f, d * 1.5f);
                Vector3 tangentB = Vector3.ClampMagnitude((next - a.Position) * .5f, d * 1.5f);
                Vector3 p1 = a.Position + tangentA / 3f;
                Vector3 p2 = b.Position - tangentB / 3f;
                position = u*u*u*a.Position + 3f*u*u*t*p1 + 3f*u*t*t*p2 + t*t*t*b.Position;
            }
            else
            {
                float span = segment.To.Time - segment.From.Time;
                float t2 = t * t, t3 = t2 * t;
                Vector3 tangentA = PositionTangent(timeline, index) * span;
                Vector3 tangentB = PositionTangent(timeline, index + 1) * span;
                position = (2f*t3 - 3f*t2 + 1f) * a.Position + (t3 - 2f*t2 + t) * tangentA + (-2f*t3 + 3f*t2) * b.Position + (t3 - t2) * tangentB;
            }
            return position;
        }

        private static Vector3 PositionTangent(CinematicTimeline timeline, int index)
        {
            int last = timeline.Keyframes.Count - 1;
            if (index <= 0)
            {
                float dt = Mathf.Max(.0001f, timeline.Keyframes[1].Time - timeline.Keyframes[0].Time);
                return (timeline.Keyframes[1].Point.Position - timeline.Keyframes[0].Point.Position) / dt;
            }
            if (index >= last)
            {
                float dt = Mathf.Max(.0001f, timeline.Keyframes[last].Time - timeline.Keyframes[last - 1].Time);
                return (timeline.Keyframes[last].Point.Position - timeline.Keyframes[last - 1].Point.Position) / dt;
            }
            float totalTime = Mathf.Max(.0001f, timeline.Keyframes[index + 1].Time - timeline.Keyframes[index - 1].Time);
            return (timeline.Keyframes[index + 1].Point.Position - timeline.Keyframes[index - 1].Point.Position) / totalTime;
        }

        private static float SmoothFieldOfView(CinematicTimeline timeline, int index, float t, float span)
        {
            float t2 = t * t, t3 = t2 * t;
            float a = timeline.Keyframes[index].Point.FieldOfView, b = timeline.Keyframes[index + 1].Point.FieldOfView;
            float tangentA = ScalarTangent(timeline, index) * span, tangentB = ScalarTangent(timeline, index + 1) * span;
            return (2f*t3 - 3f*t2 + 1f) * a + (t3 - 2f*t2 + t) * tangentA + (-2f*t3 + 3f*t2) * b + (t3 - t2) * tangentB;
        }

        private static float ScalarTangent(CinematicTimeline timeline, int index)
        {
            int last = timeline.Keyframes.Count - 1;
            if (index <= 0)
            {
                float dt = Mathf.Max(.0001f, timeline.Keyframes[1].Time - timeline.Keyframes[0].Time);
                return (timeline.Keyframes[1].Point.FieldOfView - timeline.Keyframes[0].Point.FieldOfView) / dt;
            }
            if (index >= last)
            {
                float dt = Mathf.Max(.0001f, timeline.Keyframes[last].Time - timeline.Keyframes[last - 1].Time);
                return (timeline.Keyframes[last].Point.FieldOfView - timeline.Keyframes[last - 1].Point.FieldOfView) / dt;
            }
            float totalTime = Mathf.Max(.0001f, timeline.Keyframes[index + 1].Time - timeline.Keyframes[index - 1].Time);
            return (timeline.Keyframes[index + 1].Point.FieldOfView - timeline.Keyframes[index - 1].Point.FieldOfView) / totalTime;
        }

        private static Quaternion SmoothRotation(CinematicTimeline timeline, int index, float t)
        {
            Quaternion previous = index > 0 ? timeline.Keyframes[index - 1].Point.Rotation : timeline.Keyframes[index].Point.Rotation;
            Quaternion a = timeline.Keyframes[index].Point.Rotation;
            Quaternion b = timeline.Keyframes[index + 1].Point.Rotation;
            Quaternion next = index + 2 < timeline.Keyframes.Count ? timeline.Keyframes[index + 2].Point.Rotation : b;
            previous = SameHemisphere(previous, a); b = SameHemisphere(b, a); next = SameHemisphere(next, b);
            Quaternion controlA = RotationControl(previous, a, b);
            Quaternion controlB = RotationControl(a, b, next);
            Quaternion direct = Quaternion.Slerp(a, b, t);
            Quaternion controls = Quaternion.Slerp(controlA, controlB, t);
            return Quaternion.Slerp(direct, controls, 2f * t * (1f - t));
        }

        private static Quaternion RotationControl(Quaternion previous, Quaternion current, Quaternion next)
        {
            Quaternion inverse = Quaternion.Inverse(current);
            Vector3 before = QuaternionLog(inverse * previous);
            Vector3 after = QuaternionLog(inverse * next);
            return Normalize(current * QuaternionExp((before + after) * -.25f));
        }

        private static Vector3 QuaternionLog(Quaternion value)
        {
            value = Normalize(value);
            if (value.w < 0f) value = Negate(value);
            float angle = Mathf.Acos(Mathf.Clamp(value.w, -1f, 1f));
            float sine = Mathf.Sin(angle);
            if (Mathf.Abs(sine) < .00001f) return Vector3.zero;
            return new Vector3(value.x, value.y, value.z) * (angle / sine);
        }

        private static Quaternion QuaternionExp(Vector3 value)
        {
            float angle = value.magnitude;
            if (angle < .00001f) return Quaternion.identity;
            float scale = Mathf.Sin(angle) / angle;
            return Normalize(new Quaternion(value.x * scale, value.y * scale, value.z * scale, Mathf.Cos(angle)));
        }

        private static Quaternion SameHemisphere(Quaternion value, Quaternion reference) => Quaternion.Dot(value, reference) < 0f ? Negate(value) : value;
        private static Quaternion Negate(Quaternion value) => new Quaternion(-value.x, -value.y, -value.z, -value.w);
        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(value.x*value.x + value.y*value.y + value.z*value.z + value.w*value.w);
            if (magnitude < .00001f) return Quaternion.identity;
            float inverse = 1f / magnitude;
            return new Quaternion(value.x * inverse, value.y * inverse, value.z * inverse, value.w * inverse);
        }
        private static CameraState State(CameraPoint p) => new CameraState(p.Position, p.Rotation, p.FieldOfView);
    }
}
