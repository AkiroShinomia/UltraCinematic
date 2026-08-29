using System.Collections.Generic;
using UltraCinematic.Core;
using UltraCinematic.Data;
using UltraCinematic.Timeline;
using UnityEngine;

namespace UltraCinematic.Visualization
{
    internal sealed class CinematicWorldVisualizer : MonoBehaviour
    {
        private CinematicController controller;
        private GameObject root;
        private Mesh coneMesh;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Transform> labels = new List<Transform>();
        public void Initialize(CinematicController value) { controller = value; }
        public void Show() { EnsureRoot(); Rebuild(); }
        public void Hide() { if (root != null) Destroy(root); root = null; objects.Clear(); labels.Clear(); }
        private void EnsureRoot() { if (root == null) root = new GameObject("UltraCinematic Visualization"); }
        public void Rebuild()
        {
            if (!controller.EditModeEnabled || controller.PlaybackActive) return; EnsureRoot();
            foreach (GameObject item in objects) if (item != null) Destroy(item); objects.Clear(); labels.Clear();
            for (int i = 0; i < controller.Timeline.Points.Count; i++) Marker(controller.Timeline.Points[i], i + 1);
            for (int i = 0; i < controller.Timeline.SegmentCount; i++) Line(controller.Timeline.GetSegment(i), i);
            if (controller.Timeline.Keyframes.Count > 0) Marker(TimelineEvaluator.Evaluate(controller.Timeline, controller.Timeline.CursorTime).Position, .1f, Color.cyan, null);
        }
        private void LateUpdate()
        {
            UnityEngine.Camera view = UnityEngine.Camera.main;
            if (view == null) return;
            foreach (Transform label in labels)
                if (label != null) label.rotation = Quaternion.LookRotation(label.position - view.transform.position, Vector3.up);
        }
        private void Marker(CameraPoint point, int number)
        {
            Marker(point.Position, .16f, Color.yellow, number.ToString());
            DirectionCone(point);
        }
        private void Marker(Vector3 position, float scale, Color color, string label)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.name = label == null ? "Timeline Cursor" : "Camera Point " + label; marker.transform.SetParent(root.transform); marker.transform.position = position; marker.transform.localScale = Vector3.one * scale;
            Collider collider = marker.GetComponent<Collider>(); if (collider != null) Destroy(collider); marker.GetComponent<Renderer>().material.color = color; objects.Add(marker);
            if (label != null) AddLabel("Point Label " + label, label, position + Vector3.up * .28f, color);
        }
        private void Line(TimelineSegment segment, int index)
        {
            GameObject go = new GameObject("Cinematic Segment " + SegmentLabel(index)); go.transform.SetParent(root.transform); LineRenderer line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.widthMultiplier = .035f; line.material = new Material(Shader.Find("Sprites/Default")); line.startColor = line.endColor = Color.cyan;
            int count = segment.PathType == PathType.Linear ? 2 : 32; line.positionCount = count;
            float start = segment.From.Time, duration = segment.To.Time - start;
            for (int i = 0; i < count; i++) line.SetPosition(i, TimelineEvaluator.Evaluate(controller.Timeline, start + duration * i / (count - 1f)).Position);
            objects.Add(go);
            Vector3 midpoint = TimelineEvaluator.Evaluate(controller.Timeline, start + duration * .5f).Position;
            AddLabel("Segment Label " + SegmentLabel(index), SegmentLabel(index), midpoint + Vector3.up * .22f, Color.cyan);
        }
        private void DirectionCone(CameraPoint point)
        {
            if (coneMesh == null) coneMesh = CreateConeMesh();
            GameObject cone = new GameObject("Camera Direction");
            cone.transform.SetParent(root.transform);
            cone.transform.SetPositionAndRotation(point.Position, point.Rotation);
            MeshFilter filter = cone.AddComponent<MeshFilter>(); filter.sharedMesh = coneMesh;
            MeshRenderer renderer = cone.AddComponent<MeshRenderer>(); renderer.material = new Material(Shader.Find("Sprites/Default")); renderer.material.color = new Color(1f, .75f, .1f, .85f);
            objects.Add(cone);
        }
        private void AddLabel(string name, string value, Vector3 position, Color color)
        {
            GameObject textObject = new GameObject(name); textObject.transform.SetParent(root.transform); textObject.transform.position = position;
            TextMesh text = textObject.AddComponent<TextMesh>(); text.text = value; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.fontSize = 64; text.characterSize = .03f; text.color = color;
            labels.Add(textObject.transform); objects.Add(textObject);
        }
        private static Mesh CreateConeMesh()
        {
            const int sides = 16; const float radius = .18f; const float baseDistance = .72f; const float tipDistance = .03f;
            Vector3[] vertices = new Vector3[sides + 2]; int[] triangles = new int[sides * 6];
            vertices[0] = new Vector3(0f, 0f, baseDistance); vertices[sides + 1] = new Vector3(0f, 0f, tipDistance);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, baseDistance);
                int next = (i + 1) % sides + 1; int offset = i * 6;
                triangles[offset] = 0; triangles[offset + 1] = next; triangles[offset + 2] = i + 1;
                triangles[offset + 3] = sides + 1; triangles[offset + 4] = i + 1; triangles[offset + 5] = next;
            }
            Mesh mesh = new Mesh { name = "UltraCinematic Direction Cone", vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static string SegmentLabel(int index) { index++; string result = ""; while (index > 0) { index--; result = (char)('A' + index % 26) + result; index /= 26; } return result; }
    }
}
