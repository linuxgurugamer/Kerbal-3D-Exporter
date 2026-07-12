using System.Collections.Generic;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    internal static class MeshCleanup
    {
        public static void RemoveBadTriangles(List<Triangle> triangles)
        {
            if (triangles == null)
                return;

            for (int i = triangles.Count - 1; i >= 0; i--)
            {
                Triangle t = triangles[i];

                if (!IsFinite(t.A) || !IsFinite(t.B) || !IsFinite(t.C))
                {
                    triangles.RemoveAt(i);
                    continue;
                }

                float area = Vector3.Cross(t.B - t.A, t.C - t.A).magnitude * 0.5f;
                if (area < 0.0001f)
                    triangles.RemoveAt(i);
            }
        }

        public static void RemoveDuplicateTriangles(List<Triangle> triangles)
        {
            if (triangles == null)
                return;

            HashSet<string> seen = new HashSet<string>();

            for (int i = triangles.Count - 1; i >= 0; i--)
            {
                string key = triangles[i].GetSortedKey();

                if (seen.Contains(key))
                    triangles.RemoveAt(i);
                else
                    seen.Add(key);
            }
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                     float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
