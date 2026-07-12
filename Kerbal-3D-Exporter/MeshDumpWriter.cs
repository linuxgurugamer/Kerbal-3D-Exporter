using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{
    /// <summary>
    /// Dumps the collected triangle soup, with part attribution intact, to a compact binary file
    /// for offline analysis.
    ///
    /// This exists to make the watertighting work possible at all. That algorithm has too many
    /// unknowns (Mono performance, the memory ceiling, how badly thin features like antennas and
    /// solar panels degrade under voxelization, whether marching-cubes stairstepping is tolerable
    /// on a fin) to iterate on inside KSP, where a single test cycle costs minutes. Dump once,
    /// then iterate against the dump in a standalone harness in seconds.
    ///
    /// It is also just useful on its own: attach a dump to a bug report and the exact geometry
    /// that misbehaved can be reproduced without owning the craft or the mod list.
    ///
    /// Format (all little-endian, which is what BinaryWriter emits on every platform KSP runs on):
    ///
    ///     magic       char[4]   "K3DM"
    ///     version     int32     1
    ///     scale       float32   the user scale the triangles were already multiplied by
    ///     craftName   string    BinaryWriter length-prefixed UTF-8
    ///     partCount   int32
    ///     parts       partCount x { int32 partIndex, string name }
    ///     triCount    int32
    ///     triangles   triCount x { float32 ax,ay,az, bx,by,bz, cx,cy,cz, int32 partIndex }
    ///
    /// 40 bytes per triangle. A 500k-triangle craft is about 20 MB, which is fine to write and
    /// fine to hand to somebody.
    /// </summary>
    internal static class MeshDumpWriter
    {
        private const int FORMAT_VERSION = 1;

        public static void Write(
            string file,
            List<Triangle> triangles,
            string craftName,
            float scale,
            Dictionary<int, string> partNames)
        {
            if (triangles == null)
                triangles = new List<Triangle>();

            if (string.IsNullOrEmpty(craftName))
                craftName = "craft";

            using (FileStream fs = new FileStream(file, FileMode.Create, FileAccess.Write))
            using (BinaryWriter w = new BinaryWriter(fs, new UTF8Encoding(false)))
            {
                w.Write((byte)'K');
                w.Write((byte)'3');
                w.Write((byte)'D');
                w.Write((byte)'M');
                w.Write(FORMAT_VERSION);
                w.Write(scale);
                w.Write(craftName);

                // Only emit names for parts that actually contributed geometry. A part that was
                // excluded (launch clamp, hidden variant, disabled renderer) has a name in the map
                // but no triangles, and carrying it into the dump would just be noise.
                HashSet<int> present = new HashSet<int>();
                foreach (Triangle t in triangles)
                    present.Add(t.PartIndex);

                List<KeyValuePair<int, string>> emitted = new List<KeyValuePair<int, string>>();
                if (partNames != null)
                {
                    foreach (KeyValuePair<int, string> kv in partNames)
                    {
                        if (present.Contains(kv.Key))
                            emitted.Add(kv);
                    }
                }

                w.Write(emitted.Count);
                foreach (KeyValuePair<int, string> kv in emitted)
                {
                    w.Write(kv.Key);
                    w.Write(kv.Value ?? string.Empty);
                }

                w.Write(triangles.Count);
                foreach (Triangle t in triangles)
                {
                    w.Write(t.A.x); w.Write(t.A.y); w.Write(t.A.z);
                    w.Write(t.B.x); w.Write(t.B.y); w.Write(t.B.z);
                    w.Write(t.C.x); w.Write(t.C.y); w.Write(t.C.z);
                    w.Write(t.PartIndex);
                }
            }

            Log.Info("[Kerbal3DExporter] Wrote mesh dump: " + file +
                     " (" + triangles.Count + " triangles)");
        }
    }
}
