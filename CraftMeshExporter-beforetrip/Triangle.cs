using System;
using UnityEngine;

namespace CraftMeshExporter
{
    internal struct Triangle
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 C;

        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            B = b;
            C = c;
        }

        public string GetSortedKey()
        {
            string[] pts =
            {
                PointKey(A),
                PointKey(B),
                PointKey(C)
            };

            Array.Sort(pts);
            return pts[0] + "|" + pts[1] + "|" + pts[2];
        }

        private static string PointKey(Vector3 v)
        {
            return
                Mathf.RoundToInt(v.x * 1000f) + "," +
                Mathf.RoundToInt(v.y * 1000f) + "," +
                Mathf.RoundToInt(v.z * 1000f);
        }
    }
}
