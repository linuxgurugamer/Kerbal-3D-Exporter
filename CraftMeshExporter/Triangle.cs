using System;
using UnityEngine;

namespace CraftMeshExporter
{
    internal struct Triangle
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 C;

        // Which part this triangle came from (the partIndex counter in MeshCollector; 0 means
        // "unattributed"). This lives on the struct rather than in a parallel array on purpose:
        // MeshCleanup removes list elements, and a side array would silently desync from the
        // triangles it describes. Carried on the struct, the attribution survives cleanup for
        // free, which is what lets the 3MF writer emit one object per part.
        //
        // Note that RemoveDuplicateTriangles collapses coincident triangles from overlapping
        // parts down to whichever it sees first, so a shared face is attributed to one part
        // rather than duplicated. That is the behavior we want.
        public int PartIndex;

        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            B = b;
            C = c;
            PartIndex = 0;
        }

        public Triangle(Vector3 a, Vector3 b, Vector3 c, int partIndex)
        {
            A = a;
            B = b;
            C = c;
            PartIndex = partIndex;
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
