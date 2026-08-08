using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal sealed class TerrainBoundaryStroke
    {
        private const float MinimumPointDistance = 0.025f;
        private readonly List<Vector3> points = new();

        public IReadOnlyList<Vector3> Points => points;
        public int PointCount => points.Count;

        public void AddPoint(Vector3 worldPosition)
        {
            if (points.Count > 0 && HorizontalDistance(points[^1], worldPosition) < MinimumPointDistance)
            {
                points[^1] = worldPosition;
                return;
            }

            points.Add(worldPosition);
        }

        public float EvaluateMask(Vector2 worldPosition, float radius, float falloff)
        {
            if (points.Count == 0)
            {
                return 0f;
            }

            float safeRadius = Mathf.Max(0.0001f, radius);
            float distance = points.Count == 1
                ? Vector2.Distance(worldPosition, ToHorizontal(points[0]))
                : DistanceToPolyline(worldPosition);
            float normalizedDistance = distance / safeRadius;
            if (normalizedDistance >= 1f)
            {
                return 0f;
            }

            float safeFalloff = Mathf.Clamp01(falloff);
            float innerRadius = 1f - safeFalloff;
            if (safeFalloff <= 0.0001f || normalizedDistance <= innerRadius)
            {
                return 1f;
            }

            float edge = Mathf.InverseLerp(innerRadius, 1f, normalizedDistance);
            float smoothEdge = edge * edge * (3f - 2f * edge);
            return 1f - smoothEdge;
        }

        public Bounds GetWorldBounds(float expansion)
        {
            if (points.Count == 0)
            {
                return default;
            }

            float minX = points[0].x;
            float maxX = points[0].x;
            float minZ = points[0].z;
            float maxZ = points[0].z;
            for (int i = 1; i < points.Count; i++)
            {
                minX = Mathf.Min(minX, points[i].x);
                maxX = Mathf.Max(maxX, points[i].x);
                minZ = Mathf.Min(minZ, points[i].z);
                maxZ = Mathf.Max(maxZ, points[i].z);
            }

            float safeExpansion = Mathf.Max(0f, expansion);
            Vector3 min = new(minX - safeExpansion, -50000f, minZ - safeExpansion);
            Vector3 max = new(maxX + safeExpansion, 50000f, maxZ + safeExpansion);
            Bounds bounds = new();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private float DistanceToPolyline(Vector2 point)
        {
            float closestSquared = float.PositiveInfinity;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 start = ToHorizontal(points[i - 1]);
                Vector2 end = ToHorizontal(points[i]);
                Vector2 segment = end - start;
                float segmentSquared = segment.sqrMagnitude;
                float t = segmentSquared > 0.000001f
                    ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentSquared)
                    : 0f;
                closestSquared = Mathf.Min(closestSquared, (point - (start + segment * t)).sqrMagnitude);
            }

            return Mathf.Sqrt(closestSquared);
        }

        private static Vector2 ToHorizontal(Vector3 value)
        {
            return new Vector2(value.x, value.z);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Vector2.Distance(ToHorizontal(first), ToHorizontal(second));
        }
    }
}
