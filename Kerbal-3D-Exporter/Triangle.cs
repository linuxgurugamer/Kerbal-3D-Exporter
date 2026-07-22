using System;
using UnityEngine;

namespace Kerbal_3D_Exporter
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

        // Vertex normals, carried from the source mesh.
        //
        // These are what makes round parts round. A KSP tank is a 24-sided prism, but an artist
        // marking it smooth stores normals pointing RADIALLY OUTWARD as if it were a true circle.
        // That is a record of the surface the model was meant to have, and until now the exporter
        // discarded it and wrote out the raw polygon. PnTessellator uses it to reconstruct the
        // intended curve. See PnTessellator for why extra vertices alone cannot do this.
        //
        // Zero when the source mesh has no normals, which PnTessellator treats as flat.
        public Vector3 Na;
        public Vector3 Nb;
        public Vector3 Nc;

        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            B = b;
            C = c;
            PartIndex = 0;
            Na = Vector3.zero;
            Nb = Vector3.zero;
            Nc = Vector3.zero;
        }

        public Triangle(Vector3 a, Vector3 b, Vector3 c, int partIndex)
        {
            A = a;
            B = b;
            C = c;
            PartIndex = partIndex;
            Na = Vector3.zero;
            Nb = Vector3.zero;
            Nc = Vector3.zero;
        }

        public Triangle(Vector3 a, Vector3 b, Vector3 c, int partIndex,
                        Vector3 na, Vector3 nb, Vector3 nc)
        {
            A = a;
            B = b;
            C = c;
            PartIndex = partIndex;
            Na = na;
            Nb = nb;
            Nc = nc;
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
