using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

using static CraftMeshExporter.CraftMeshExporterToolbarRegistration;

namespace CraftMeshExporter
{
    /// <summary>
    /// Writes a 3MF (3D Manufacturing Format) file.
    ///
    /// A .3mf is an OPC package, i.e. a plain ZIP archive containing:
    ///
    ///     [Content_Types].xml
    ///     _rels/.rels
    ///     3D/3dmodel.model      (the mesh XML, unit="millimeter")
    ///
    /// The archive is produced by MinimalZipWriter (below) rather than by
    /// System.IO.Compression.ZipArchive, because that type lives in
    /// System.IO.Compression.dll, which is not part of the assembly set KSP's Mono runtime
    /// reliably exposes to plugins. MinimalZipWriter needs nothing beyond System.dll.
    ///
    /// Unlike STL, 3MF stores an indexed vertex list, so a shared vertex is written once
    /// instead of once per triangle. For a typical craft that makes the file considerably
    /// smaller than the equivalent STL, and it also carries metadata and an explicit unit.
    /// </summary>
    internal static class ThreeMfWriter
    {
        private const string MODEL_PATH = "3D/3dmodel.model";

        // Vertices closer together than this (in exported millimeters) collapse to one index.
        // 1e-4 mm is far below any printable resolution, so this only welds vertices that were
        // already meant to be the same point.
        private const float WELD_EPSILON = 1e-4f;

        public static void Write(
            string file,
            List<Triangle> triangles,
            string craftName,
            float scale,
            bool perPart,
            Dictionary<int, string> partNames)
        {
            if (triangles == null)
                triangles = new List<Triangle>();

            if (string.IsNullOrEmpty(craftName))
                craftName = "KSP Craft";

            string modelXml = perPart
                ? BuildPerPartModelXml(triangles, craftName, scale, partNames)
                : BuildModelXml(triangles, craftName, scale);

            using (MinimalZipWriter zip = new MinimalZipWriter(file))
            {
                // Per OPC, [Content_Types].xml must be the first entry in the archive.
                zip.AddTextEntry("[Content_Types].xml", ContentTypesXml());
                zip.AddTextEntry("_rels/.rels", RootRelsXml());
                zip.AddTextEntry(MODEL_PATH, modelXml);
            }

            Log.Info("[CraftMeshExporter] Wrote 3MF: " + file + " (" + triangles.Count + " triangles, perPart=" + perPart + ")");
        }

        /// <summary>
        /// Emits one &lt;object&gt; per KSP part, tied together by a single assembly object whose
        /// &lt;components&gt; reference them, plus a &lt;basematerials&gt; palette giving each part
        /// its own color.
        ///
        /// A slicer opening this sees each part as a separate, selectable body it can delete,
        /// move, or assign to its own extruder. That is a real workflow gain over the merged
        /// mesh, and it is something STL structurally cannot express at all.
        ///
        /// The colors are SYNTHETIC and decorative, not truthful. A KSP part is textured, not
        /// flat-colored, so there is no single honest "color of this part" to read out of it.
        /// These are generated purely so adjacent parts are visually distinguishable. Do not
        /// mistake them for the part's actual in-game appearance.
        ///
        /// Note: per the 3MF core spec an object carries EITHER a mesh OR components, never both,
        /// which is why the assembly object is separate from the part objects.
        /// </summary>
        private static string BuildPerPartModelXml(
            List<Triangle> triangles,
            string craftName,
            float scale,
            Dictionary<int, string> partNames)
        {
            // Group triangles by owning part, preserving first-seen order so the part numbering in
            // the output is stable and matches the order the collector walked the craft.
            Dictionary<int, List<Triangle>> byPart = new Dictionary<int, List<Triangle>>();
            List<int> partOrder = new List<int>();

            foreach (Triangle t in triangles)
            {
                List<Triangle> bucket;
                if (!byPart.TryGetValue(t.PartIndex, out bucket))
                {
                    bucket = new List<Triangle>();
                    byPart[t.PartIndex] = bucket;
                    partOrder.Add(t.PartIndex);
                }

                bucket.Add(t);
            }

            // Id space is shared across every resource in the model, materials included.
            const int MATERIALS_ID = 1;
            int nextObjectId = 2;

            StringBuilder sb = new StringBuilder(triangles.Count * 64 + 4096);

            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n");
            sb.Append("<model unit=\"millimeter\" xml:lang=\"en-US\" ");
            sb.Append("xmlns=\"http://schemas.microsoft.com/3dmanufacturing/core/2015/02\">\r\n");

            sb.Append("  <metadata name=\"Title\">").Append(EscapeXml(craftName)).Append("</metadata>\r\n");
            sb.Append("  <metadata name=\"Designer\">KSP CraftMeshExporter</metadata>\r\n");
            sb.Append("  <metadata name=\"Application\">Kerbal Space Program - CraftMeshExporter</metadata>\r\n");
            sb.Append("  <metadata name=\"Description\">Export scale ")
              .Append(F(scale))
              .Append("; units are millimeters; ")
              .Append(partOrder.Count.ToString(CultureInfo.InvariantCulture))
              .Append(" parts as separate objects. Part colors are synthetic and for visual separation only.</metadata>\r\n");

            sb.Append("  <resources>\r\n");

            // Palette. One <base> per part, in the same order as the objects below, so a part's
            // object can address its color with pindex = its position in this list.
            sb.Append("    <basematerials id=\"").Append(MATERIALS_ID).Append("\">\r\n");
            for (int i = 0; i < partOrder.Count; i++)
            {
                string name = LookupPartName(partNames, partOrder[i]);
                sb.Append("      <base name=\"").Append(EscapeXml(name))
                  .Append("\" displaycolor=\"").Append(PaletteColor(i, partOrder.Count))
                  .Append("\" />\r\n");
            }
            sb.Append("    </basematerials>\r\n");

            // One mesh object per part.
            List<int> componentIds = new List<int>(partOrder.Count);

            for (int i = 0; i < partOrder.Count; i++)
            {
                int objectId = nextObjectId++;
                componentIds.Add(objectId);

                string name = LookupPartName(partNames, partOrder[i]);

                sb.Append("    <object id=\"").Append(objectId)
                  .Append("\" type=\"model\" pid=\"").Append(MATERIALS_ID)
                  .Append("\" pindex=\"").Append(i)
                  .Append("\" name=\"").Append(EscapeXml(name)).Append("\">\r\n");

                AppendMesh(sb, byPart[partOrder[i]], "      ");

                sb.Append("    </object>\r\n");
            }

            // The assembly. Components only, no mesh of its own.
            int assemblyId = nextObjectId++;
            sb.Append("    <object id=\"").Append(assemblyId)
              .Append("\" type=\"model\" name=\"").Append(EscapeXml(craftName)).Append("\">\r\n");
            sb.Append("      <components>\r\n");
            foreach (int id in componentIds)
            {
                // Identity transform: the vertices are already in craft space, so each part object
                // is placed exactly where it was collected.
                sb.Append("        <component objectid=\"").Append(id)
                  .Append("\" transform=\"1 0 0 0 1 0 0 0 1 0 0 0\" />\r\n");
            }
            sb.Append("      </components>\r\n");
            sb.Append("    </object>\r\n");

            sb.Append("  </resources>\r\n");

            sb.Append("  <build>\r\n");
            sb.Append("    <item objectid=\"").Append(assemblyId).Append("\" />\r\n");
            sb.Append("  </build>\r\n");
            sb.Append("</model>\r\n");

            return sb.ToString();
        }

        private static string LookupPartName(Dictionary<int, string> partNames, int partIndex)
        {
            string name;
            if (partNames != null && partNames.TryGetValue(partIndex, out name) && !string.IsNullOrEmpty(name))
                return name;

            return partIndex > 0
                ? "Part " + partIndex.ToString(CultureInfo.InvariantCulture)
                : "Unattributed geometry";
        }

        /// <summary>
        /// Generates a visually distinguishable color per part.
        ///
        /// Hues are spread by the golden-angle increment rather than evenly divided, so that
        /// consecutively-numbered parts (which are usually physically adjacent on the craft) land
        /// far apart on the color wheel, and so that the palette stays well-separated no matter how
        /// many parts there turn out to be. Saturation and value alternate slightly to keep
        /// neighbors apart even when hues eventually start to crowd on very large craft.
        ///
        /// Deterministic in the part's ordinal, so re-exporting the same craft yields the same
        /// colors rather than reshuffling them.
        /// </summary>
        private static string PaletteColor(int ordinal, int total)
        {
            const float GOLDEN_ANGLE = 0.6180339887f;

            float h = (ordinal * GOLDEN_ANGLE) % 1f;
            float s = ((ordinal % 3) == 0) ? 0.55f : 0.75f;
            float v = ((ordinal % 2) == 0) ? 0.95f : 0.72f;

            Color c = Color.HSVToRGB(h, s, v);

            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);

            // 3MF displaycolor is sRGB hex, #RRGGBBAA.
            return "#" + r.ToString("X2", CultureInfo.InvariantCulture)
                       + g.ToString("X2", CultureInfo.InvariantCulture)
                       + b.ToString("X2", CultureInfo.InvariantCulture)
                       + "FF";
        }

        /// <summary>
        /// Writes a &lt;mesh&gt; for one triangle set. Vertex indices are local to the enclosing
        /// object, so each part object gets its own independent vertex list.
        /// </summary>
        private static void AppendMesh(StringBuilder sb, List<Triangle> triangles, string indent)
        {
            Dictionary<string, int> vertexIndices = new Dictionary<string, int>(triangles.Count * 3);
            List<Vector3> vertices = new List<Vector3>(triangles.Count * 3);
            List<int> faces = new List<int>(triangles.Count * 3);

            foreach (Triangle t in triangles)
            {
                int a = GetVertexIndex(t.A, vertexIndices, vertices);
                int b = GetVertexIndex(t.B, vertexIndices, vertices);
                int c = GetVertexIndex(t.C, vertexIndices, vertices);

                if (a == b || b == c || a == c)
                    continue;

                faces.Add(a);
                faces.Add(b);
                faces.Add(c);
            }

            sb.Append(indent).Append("<mesh>\r\n");

            sb.Append(indent).Append("  <vertices>\r\n");
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                sb.Append(indent).Append("    <vertex x=\"").Append(F(v.x))
                  .Append("\" y=\"").Append(F(v.y))
                  .Append("\" z=\"").Append(F(v.z))
                  .Append("\" />\r\n");
            }
            sb.Append(indent).Append("  </vertices>\r\n");

            sb.Append(indent).Append("  <triangles>\r\n");
            for (int i = 0; i + 2 < faces.Count; i += 3)
            {
                sb.Append(indent).Append("    <triangle v1=\"").Append(faces[i])
                  .Append("\" v2=\"").Append(faces[i + 1])
                  .Append("\" v3=\"").Append(faces[i + 2])
                  .Append("\" />\r\n");
            }
            sb.Append(indent).Append("  </triangles>\r\n");

            sb.Append(indent).Append("</mesh>\r\n");
        }

        private static string ContentTypesXml()
        {
            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\" />" +
                "<Default Extension=\"model\" ContentType=\"application/vnd.ms-package.3dmanufacturing-3dmodel+xml\" />" +
                "</Types>";
        }

        private static string RootRelsXml()
        {
            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rel0\" Target=\"/" + MODEL_PATH + "\" " +
                "Type=\"http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel\" />" +
                "</Relationships>";
        }

        private static string BuildModelXml(List<Triangle> triangles, string craftName, float scale)
        {
            Dictionary<string, int> vertexIndices = new Dictionary<string, int>(triangles.Count * 3);
            List<Vector3> vertices = new List<Vector3>(triangles.Count * 3);

            // v1/v2/v3 per triangle, flattened, already resolved to vertex indices.
            List<int> faces = new List<int>(triangles.Count * 3);

            foreach (Triangle t in triangles)
            {
                int a = GetVertexIndex(t.A, vertexIndices, vertices);
                int b = GetVertexIndex(t.B, vertexIndices, vertices);
                int c = GetVertexIndex(t.C, vertexIndices, vertices);

                // 3MF rejects a triangle that references the same vertex twice. Welding can
                // create those out of triangles that were merely very thin, so drop them here
                // rather than emitting a file a slicer will reject.
                if (a == b || b == c || a == c)
                    continue;

                faces.Add(a);
                faces.Add(b);
                faces.Add(c);
            }

            StringBuilder sb = new StringBuilder(vertices.Count * 48 + faces.Count * 16 + 1024);

            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n");
            sb.Append("<model unit=\"millimeter\" xml:lang=\"en-US\" ");
            sb.Append("xmlns=\"http://schemas.microsoft.com/3dmanufacturing/core/2015/02\">\r\n");

            sb.Append("  <metadata name=\"Title\">").Append(EscapeXml(craftName)).Append("</metadata>\r\n");
            sb.Append("  <metadata name=\"Designer\">KSP CraftMeshExporter</metadata>\r\n");
            sb.Append("  <metadata name=\"Application\">Kerbal Space Program - CraftMeshExporter</metadata>\r\n");
            sb.Append("  <metadata name=\"Description\">Export scale ")
              .Append(F(scale))
              .Append("; units are millimeters.</metadata>\r\n");

            sb.Append("  <resources>\r\n");
            sb.Append("    <object id=\"1\" type=\"model\" name=\"").Append(EscapeXml(craftName)).Append("\">\r\n");
            sb.Append("      <mesh>\r\n");

            sb.Append("        <vertices>\r\n");
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                sb.Append("          <vertex x=\"").Append(F(v.x))
                  .Append("\" y=\"").Append(F(v.y))
                  .Append("\" z=\"").Append(F(v.z))
                  .Append("\" />\r\n");
            }
            sb.Append("        </vertices>\r\n");

            sb.Append("        <triangles>\r\n");
            for (int i = 0; i + 2 < faces.Count; i += 3)
            {
                sb.Append("          <triangle v1=\"").Append(faces[i])
                  .Append("\" v2=\"").Append(faces[i + 1])
                  .Append("\" v3=\"").Append(faces[i + 2])
                  .Append("\" />\r\n");
            }
            sb.Append("        </triangles>\r\n");

            sb.Append("      </mesh>\r\n");
            sb.Append("    </object>\r\n");
            sb.Append("  </resources>\r\n");

            sb.Append("  <build>\r\n");
            sb.Append("    <item objectid=\"1\" />\r\n");
            sb.Append("  </build>\r\n");
            sb.Append("</model>\r\n");

            return sb.ToString();
        }

        private static int GetVertexIndex(Vector3 v, Dictionary<string, int> map, List<Vector3> vertices)
        {
            string key = VertexKey(v);

            int index;
            if (map.TryGetValue(key, out index))
                return index;

            index = vertices.Count;
            vertices.Add(v);
            map[key] = index;
            return index;
        }

        private static string VertexKey(Vector3 v)
        {
            // Quantize to WELD_EPSILON so vertices that are the same point land on one index.
            long x = (long)Math.Round(v.x / WELD_EPSILON);
            long y = (long)Math.Round(v.y / WELD_EPSILON);
            long z = (long)Math.Round(v.z / WELD_EPSILON);

            return x.ToString(CultureInfo.InvariantCulture) + "|" +
                   y.ToString(CultureInfo.InvariantCulture) + "|" +
                   z.ToString(CultureInfo.InvariantCulture);
        }

        private static string F(float f)
        {
            return f.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }

    /// <summary>
    /// A tiny, dependency-free ZIP writer.
    ///
    /// Deliberately implements only what 3MF needs: a few small text entries, stored
    /// uncompressed (method 0), with a standard local header / central directory / end-of-central
    /// -directory layout.
    ///
    /// Storing uncompressed is what lets this avoid System.IO.Compression.dll, which KSP's Mono
    /// runtime does not reliably hand to plugins. Every ZIP reader handles stored entries, so
    /// every slicer opens the result normally.
    /// </summary>
    internal sealed class MinimalZipWriter : IDisposable
    {
        private sealed class Entry
        {
            public string Name;
            public uint Crc;
            public int Size;
            public long LocalHeaderOffset;
        }

        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly List<Entry> entries = new List<Entry>();
        private bool closed;

        public MinimalZipWriter(string path)
        {
            stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            writer = new BinaryWriter(stream);
        }

        public void AddTextEntry(string entryName, string content)
        {
            // No BOM: some strict OPC readers reject a BOM in [Content_Types].xml.
            byte[] data = new UTF8Encoding(false).GetBytes(content);
            AddEntry(entryName, data);
        }

        public void AddEntry(string entryName, byte[] data)
        {
            if (closed)
                throw new InvalidOperationException("MinimalZipWriter is already closed.");

            byte[] nameBytes = Encoding.UTF8.GetBytes(entryName);

            Entry e = new Entry();
            e.Name = entryName;
            e.Crc = Crc32(data);
            e.Size = data.Length;
            e.LocalHeaderOffset = stream.Position;
            entries.Add(e);

            // Local file header
            writer.Write((uint)0x04034b50);          // signature
            writer.Write((ushort)20);                // version needed to extract
            writer.Write((ushort)0);                 // general purpose flags
            writer.Write((ushort)0);                 // compression method: 0 = stored
            writer.Write((ushort)0);                 // last mod time
            writer.Write((ushort)0);                 // last mod date
            writer.Write(e.Crc);                     // crc-32
            writer.Write((uint)e.Size);              // compressed size
            writer.Write((uint)e.Size);              // uncompressed size
            writer.Write((ushort)nameBytes.Length);  // file name length
            writer.Write((ushort)0);                 // extra field length
            writer.Write(nameBytes);
            writer.Write(data);
        }

        private void WriteCentralDirectory()
        {
            long centralStart = stream.Position;

            foreach (Entry e in entries)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(e.Name);

                writer.Write((uint)0x02014b50);          // signature
                writer.Write((ushort)20);                // version made by
                writer.Write((ushort)20);                // version needed to extract
                writer.Write((ushort)0);                 // general purpose flags
                writer.Write((ushort)0);                 // compression method: stored
                writer.Write((ushort)0);                 // last mod time
                writer.Write((ushort)0);                 // last mod date
                writer.Write(e.Crc);                     // crc-32
                writer.Write((uint)e.Size);              // compressed size
                writer.Write((uint)e.Size);              // uncompressed size
                writer.Write((ushort)nameBytes.Length);  // file name length
                writer.Write((ushort)0);                 // extra field length
                writer.Write((ushort)0);                 // file comment length
                writer.Write((ushort)0);                 // disk number start
                writer.Write((ushort)0);                 // internal file attributes
                writer.Write((uint)0);                   // external file attributes
                writer.Write((uint)e.LocalHeaderOffset); // relative offset of local header
                writer.Write(nameBytes);
            }

            long centralSize = stream.Position - centralStart;

            // End of central directory record
            writer.Write((uint)0x06054b50);       // signature
            writer.Write((ushort)0);              // number of this disk
            writer.Write((ushort)0);              // disk with start of central directory
            writer.Write((ushort)entries.Count);  // central dir entries on this disk
            writer.Write((ushort)entries.Count);  // total central dir entries
            writer.Write((uint)centralSize);      // size of central directory
            writer.Write((uint)centralStart);     // offset of central directory
            writer.Write((ushort)0);              // zip comment length
        }

        public void Dispose()
        {
            if (closed)
                return;

            closed = true;

            WriteCentralDirectory();

            writer.Flush();
            writer.Close();
            stream.Dispose();
        }

        private static uint[] crcTable;

        private static uint Crc32(byte[] data)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                    crcTable[i] = c;
                }
            }

            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
                crc = crcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);

            return crc ^ 0xFFFFFFFFu;
        }
    }
}
