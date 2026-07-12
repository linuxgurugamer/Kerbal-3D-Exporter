using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{
    /// <summary>
    /// Writes an ISO 10303-21 STEP file (.stp), schema AUTOMOTIVE_DESIGN (AP214).
    ///
    /// READ THIS BEFORE CHANGING ANYTHING HERE:
    ///
    /// STEP is a boundary-representation CAD format. What this mod has is a triangle soup. The
    /// only faithful way across that gap is a FACETED B-rep: every triangle becomes its own
    /// ADVANCED_FACE lying on its own PLANE, bounded by an EDGE_LOOP of three EDGE_CURVEs.
    /// That is real, valid STEP and it opens in Fusion 360, SolidWorks, FreeCAD, Onshape, etc.
    ///
    /// What it is NOT is a "proper" CAD model. There are no NURBS surfaces and no analytic
    /// cylinders. CAD software will show thousands of tiny flat facets, because that is
    /// genuinely all the information a mesh contains. Nothing can invent the smooth surfaces
    /// back; reconstructing them is a hard research problem, not a file-format conversion.
    ///
    /// The cost is size. Roughly 17 STEP entities are emitted per triangle, so a 100k-triangle
    /// craft lands somewhere around 1.7M lines and 100+ MB. That is inherent to faceted B-rep,
    /// not a defect in this writer. Decimate the mesh first (Blender, MeshLab) if the file is
    /// unmanageable.
    ///
    /// Two deliberate structural choices:
    ///
    ///   1. OPEN_SHELL + SHELL_BASED_SURFACE_MODEL, not CLOSED_SHELL + MANIFOLD_SOLID_BREP.
    ///      A KSP craft is many overlapping part meshes; it is emphatically not watertight (the
    ///      README says as much). Declaring a closed solid that isn't closed makes CAD kernels
    ///      reject the file outright, so this ships a surface model instead. It imports as a
    ///      surface body, which is the honest description of what it is.
    ///
    ///   2. Streamed straight to a StreamWriter rather than assembled in a StringBuilder. A
    ///      large craft would otherwise build a several-hundred-megabyte string in memory and
    ///      take KSP down with it.
    ///
    /// Entity references in the DATA section may point forward, so the shell and the product
    /// structure are written last, after the faces they refer to have been counted.
    /// </summary>
    internal static class StepWriter
    {
        // Vertices closer together than this (in exported millimeters) collapse to one entity.
        private const float WELD_EPSILON = 1e-4f;

        // Purely advisory. Past this, the file gets big enough that it is worth telling the user
        // why their CAD package is struggling, rather than letting them find out on import.
        private const int LARGE_MESH_TRIANGLE_WARNING = 50000;

        public static void Write(string file, List<Triangle> triangles, string craftName, float scale, Action<string> status)
        {
            if (triangles == null)
                triangles = new List<Triangle>();

            if (string.IsNullOrEmpty(craftName))
                craftName = "KSP_Craft";

            if (triangles.Count > LARGE_MESH_TRIANGLE_WARNING && status != null)
            {
                status("STEP: " + triangles.Count.ToString("N0", CultureInfo.InvariantCulture) +
                       " triangles. A faceted B-rep needs about 17 STEP entities per triangle, so this file " +
                       "will be very large and slow to open in CAD. Decimating the mesh first is recommended.");
            }

            using (StreamWriter sw = new StreamWriter(file, false, new UTF8Encoding(false)))
            {
                StepIdAllocator ids = new StepIdAllocator();

                WriteHeader(sw, craftName, scale);

                sw.WriteLine("DATA;");

                // Geometry first: the shell below needs the face ids, and STEP lets us reference
                // forward but not conjure ids we haven't allocated.
                List<int> faceIds = WriteFaces(sw, ids, triangles);

                WriteShellAndProduct(sw, ids, faceIds, craftName);

                sw.WriteLine("ENDSEC;");
                sw.WriteLine("END-ISO-10303-21;");
            }

            Log.Info("Wrote STEP: " + file + " (" + triangles.Count + " triangles)");
        }

        private static void WriteHeader(StreamWriter sw, string craftName, float scale)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            sw.WriteLine("ISO-10303-21;");
            sw.WriteLine("HEADER;");
            sw.WriteLine("FILE_DESCRIPTION((" + S("Faceted B-rep of a KSP craft. Units: millimeters. Export scale: " + F(scale)) + "),'2;1');");
            sw.WriteLine("FILE_NAME(" + S(craftName) + "," + S(timestamp) + ",(" + S("Kerbal-3D-Exporter") + "),(" + S("Kerbal Space Program") + "),");
            sw.WriteLine("  " + S("Kerbal-3D-Exporter") + "," + S("Kerbal Space Program") + "," + S("") + ");");
            sw.WriteLine("FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));");
            sw.WriteLine("ENDSEC;");
        }

        /// <summary>
        /// Emits one ADVANCED_FACE per triangle and returns their ids.
        ///
        /// Cartesian points, vertex points, and edge curves are shared between the triangles that
        /// use them. That sharing is not cosmetic: an unshared writer emits roughly twice the
        /// entities, and CAD kernels are far happier when the two faces meeting at an edge
        /// actually reference the same edge rather than two coincident ones.
        /// </summary>
        private static List<int> WriteFaces(StreamWriter sw, StepIdAllocator ids, List<Triangle> triangles)
        {
            // Quantized position -> id of the CARTESIAN_POINT for it.
            Dictionary<string, int> pointIds = new Dictionary<string, int>();
            // Same key -> id of the VERTEX_POINT wrapping that CARTESIAN_POINT.
            Dictionary<string, int> vertexIds = new Dictionary<string, int>();
            // Unordered vertex-id pair -> id of the EDGE_CURVE joining them.
            Dictionary<long, int> edgeIds = new Dictionary<long, int>();

            List<int> faceIds = new List<int>(triangles.Count);

            foreach (Triangle t in triangles)
            {
                string ka = Key(t.A);
                string kb = Key(t.B);
                string kc = Key(t.C);

                // Welding can pull a sliver triangle down onto a line or a point. Such a face has
                // no valid plane normal, so it cannot be represented and is dropped.
                if (ka == kb || kb == kc || ka == kc)
                    continue;

                Vector3 normal = Vector3.Cross(t.B - t.A, t.C - t.A);
                if (normal.sqrMagnitude <= float.Epsilon)
                    continue;

                normal = normal.normalized;

                int pa = GetPoint(sw, ids, pointIds, ka, t.A);
                int pb = GetPoint(sw, ids, pointIds, kb, t.B);
                int pc = GetPoint(sw, ids, pointIds, kc, t.C);

                int va = GetVertex(sw, ids, vertexIds, ka, pa);
                int vb = GetVertex(sw, ids, vertexIds, kb, pb);
                int vc = GetVertex(sw, ids, vertexIds, kc, pc);

                bool fwdAB, fwdBC, fwdCA;
                int eAB = GetEdge(sw, ids, edgeIds, pointIds, va, vb, pa, pb, t.A, t.B, out fwdAB);
                int eBC = GetEdge(sw, ids, edgeIds, pointIds, vb, vc, pb, pc, t.B, t.C, out fwdBC);
                int eCA = GetEdge(sw, ids, edgeIds, pointIds, vc, va, pc, pa, t.C, t.A, out fwdCA);

                int oAB = ids.Next();
                sw.WriteLine("#" + oAB + "=ORIENTED_EDGE('',*,*,#" + eAB + "," + B(fwdAB) + ");");
                int oBC = ids.Next();
                sw.WriteLine("#" + oBC + "=ORIENTED_EDGE('',*,*,#" + eBC + "," + B(fwdBC) + ");");
                int oCA = ids.Next();
                sw.WriteLine("#" + oCA + "=ORIENTED_EDGE('',*,*,#" + eCA + "," + B(fwdCA) + ");");

                int loop = ids.Next();
                sw.WriteLine("#" + loop + "=EDGE_LOOP('',(#" + oAB + ",#" + oBC + ",#" + oCA + "));");

                int bound = ids.Next();
                sw.WriteLine("#" + bound + "=FACE_OUTER_BOUND('',#" + loop + ",.T.);");

                // The plane the triangle lies on. Its axis placement is anchored at vertex A, with
                // the triangle normal as its Z axis and the A->B direction as its reference X.
                Vector3 refDir = (t.B - t.A).normalized;

                int nDir = ids.Next();
                sw.WriteLine("#" + nDir + "=DIRECTION('',(" + F(normal.x) + "," + F(normal.y) + "," + F(normal.z) + "));");
                int xDir = ids.Next();
                sw.WriteLine("#" + xDir + "=DIRECTION('',(" + F(refDir.x) + "," + F(refDir.y) + "," + F(refDir.z) + "));");

                int axis = ids.Next();
                sw.WriteLine("#" + axis + "=AXIS2_PLACEMENT_3D('',#" + pa + ",#" + nDir + ",#" + xDir + ");");

                int plane = ids.Next();
                sw.WriteLine("#" + plane + "=PLANE('',#" + axis + ");");

                int face = ids.Next();
                sw.WriteLine("#" + face + "=ADVANCED_FACE('',(#" + bound + "),#" + plane + ",.T.);");

                faceIds.Add(face);
            }

            return faceIds;
        }

        private static int GetPoint(StreamWriter sw, StepIdAllocator ids, Dictionary<string, int> map, string key, Vector3 v)
        {
            int id;
            if (map.TryGetValue(key, out id))
                return id;

            id = ids.Next();
            sw.WriteLine("#" + id + "=CARTESIAN_POINT('',(" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "));");
            map[key] = id;
            return id;
        }

        private static int GetVertex(StreamWriter sw, StepIdAllocator ids, Dictionary<string, int> map, string key, int pointId)
        {
            int id;
            if (map.TryGetValue(key, out id))
                return id;

            id = ids.Next();
            sw.WriteLine("#" + id + "=VERTEX_POINT('',#" + pointId + ");");
            map[key] = id;
            return id;
        }

        /// <summary>
        /// Returns the shared EDGE_CURVE between two vertices, creating it if this is the first
        /// triangle to use it.
        ///
        /// The edge is stored once, in canonical (lower vertex id first) order. A triangle that
        /// walks it the other way gets forward=false and the caller wraps it in an ORIENTED_EDGE
        /// with .F., which is exactly what that flag is for.
        /// </summary>
        private static int GetEdge(
            StreamWriter sw,
            StepIdAllocator ids,
            Dictionary<long, int> edgeMap,
            Dictionary<string, int> pointMap,
            int vFrom,
            int vTo,
            int pFrom,
            int pTo,
            Vector3 from,
            Vector3 to,
            out bool forward)
        {
            int lo = Math.Min(vFrom, vTo);
            int hi = Math.Max(vFrom, vTo);
            forward = (vFrom == lo);

            long key = ((long)lo << 32) | (uint)hi;

            int id;
            if (edgeMap.TryGetValue(key, out id))
                return id;

            // Build the curve in canonical order too, so the stored edge and its geometry agree.
            Vector3 cFrom = forward ? from : to;
            Vector3 cTo = forward ? to : from;
            int cpFrom = forward ? pFrom : pTo;

            Vector3 dir = (cTo - cFrom);
            float len = dir.magnitude;
            if (len <= float.Epsilon)
                len = 1f;
            dir /= len;

            int dirId = ids.Next();
            sw.WriteLine("#" + dirId + "=DIRECTION('',(" + F(dir.x) + "," + F(dir.y) + "," + F(dir.z) + "));");

            int vecId = ids.Next();
            sw.WriteLine("#" + vecId + "=VECTOR('',#" + dirId + "," + F(len) + ");");

            int lineId = ids.Next();
            sw.WriteLine("#" + lineId + "=LINE('',#" + cpFrom + ",#" + vecId + ");");

            // lo/hi are already VERTEX_POINT ids, and the curve above was built lo -> hi, so
            // same_sense is .T. here for every edge. Direction is carried by the ORIENTED_EDGE.
            id = ids.Next();
            sw.WriteLine("#" + id + "=EDGE_CURVE('',#" + lo + ",#" + hi + ",#" + lineId + ",.T.);");

            edgeMap[key] = id;
            return id;
        }

        private static void WriteShellAndProduct(StreamWriter sw, StepIdAllocator ids, List<int> faceIds, string craftName)
        {
            int shell = ids.Next();
            sw.Write("#" + shell + "=OPEN_SHELL('',(");
            WriteRefList(sw, faceIds);
            sw.WriteLine("));");

            int surfaceModel = ids.Next();
            sw.WriteLine("#" + surfaceModel + "=SHELL_BASED_SURFACE_MODEL('',(#" + shell + "));");

            // Global placement for the representation.
            int origin = ids.Next();
            sw.WriteLine("#" + origin + "=CARTESIAN_POINT('',(0.0,0.0,0.0));");
            int zAxis = ids.Next();
            sw.WriteLine("#" + zAxis + "=DIRECTION('',(0.0,0.0,1.0));");
            int xAxis = ids.Next();
            sw.WriteLine("#" + xAxis + "=DIRECTION('',(1.0,0.0,0.0));");
            int placement = ids.Next();
            sw.WriteLine("#" + placement + "=AXIS2_PLACEMENT_3D('',#" + origin + ",#" + zAxis + ",#" + xAxis + ");");

            // Units. Millimeters, matching every other exporter in this mod.
            int lengthUnit = ids.Next();
            sw.WriteLine("#" + lengthUnit + "=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.));");
            int angleUnit = ids.Next();
            sw.WriteLine("#" + angleUnit + "=(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.));");
            int solidUnit = ids.Next();
            sw.WriteLine("#" + solidUnit + "=(NAMED_UNIT(*)SI_UNIT($,.STERADIAN.)SOLID_ANGLE_UNIT());");
            int uncertainty = ids.Next();
            sw.WriteLine("#" + uncertainty + "=UNCERTAINTY_MEASURE_WITH_UNIT(LENGTH_MEASURE(1.0E-5),#" + lengthUnit +
                         ",'distance_accuracy_value','confusion accuracy');");

            int context = ids.Next();
            sw.WriteLine("#" + context + "=(GEOMETRIC_REPRESENTATION_CONTEXT(3)" +
                         "GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((#" + uncertainty + "))" +
                         "GLOBAL_UNIT_ASSIGNED_CONTEXT((#" + lengthUnit + ",#" + angleUnit + ",#" + solidUnit + "))" +
                         "REPRESENTATION_CONTEXT('',''));");

            int shapeRep = ids.Next();
            sw.WriteLine("#" + shapeRep + "=MANIFOLD_SURFACE_SHAPE_REPRESENTATION(" + S(craftName) +
                         ",(#" + placement + ",#" + surfaceModel + "),#" + context + ");");

            // Product structure. CAD packages want this; without it many will open the file but
            // show an unnamed, unplaced body, and some refuse it entirely.
            int appContext = ids.Next();
            sw.WriteLine("#" + appContext + "=APPLICATION_CONTEXT('automotive design');");
            int appProtocol = ids.Next();
            sw.WriteLine("#" + appProtocol + "=APPLICATION_PROTOCOL_DEFINITION('international standard'," +
                         "'automotive_design',2000,#" + appContext + ");");
            int productContext = ids.Next();
            sw.WriteLine("#" + productContext + "=PRODUCT_CONTEXT('',#" + appContext + ",'mechanical');");
            int product = ids.Next();
            sw.WriteLine("#" + product + "=PRODUCT(" + S(craftName) + "," + S(craftName) + ",'',(#" + productContext + "));");
            int formation = ids.Next();
            sw.WriteLine("#" + formation + "=PRODUCT_DEFINITION_FORMATION('','',#" + product + ");");
            int defContext = ids.Next();
            sw.WriteLine("#" + defContext + "=PRODUCT_DEFINITION_CONTEXT('part definition',#" + appContext + ",'design');");
            int definition = ids.Next();
            sw.WriteLine("#" + definition + "=PRODUCT_DEFINITION('design','',#" + formation + ",#" + defContext + ");");
            int defShape = ids.Next();
            sw.WriteLine("#" + defShape + "=PRODUCT_DEFINITION_SHAPE('','',#" + definition + ");");
            int shapeDefRep = ids.Next();
            sw.WriteLine("#" + shapeDefRep + "=SHAPE_DEFINITION_REPRESENTATION(#" + defShape + ",#" + shapeRep + ");");

            int prodRelated = ids.Next();
            sw.WriteLine("#" + prodRelated + "=PRODUCT_RELATED_PRODUCT_CATEGORY('part','',(#" + product + "));");
        }

        /// <summary>
        /// Writes a potentially enormous reference list, wrapped so no single physical line runs
        /// to hundreds of kilobytes. STEP treats newlines inside an entity as plain whitespace,
        /// but many text editors and a few parsers do not enjoy megabyte-long lines.
        /// </summary>
        private static void WriteRefList(StreamWriter sw, List<int> refs)
        {
            const int PER_LINE = 12;

            for (int i = 0; i < refs.Count; i++)
            {
                if (i > 0)
                    sw.Write(",");

                if (i > 0 && (i % PER_LINE) == 0)
                {
                    sw.WriteLine();
                    sw.Write("  ");
                }

                sw.Write("#");
                sw.Write(refs[i].ToString(CultureInfo.InvariantCulture));
            }
        }

        private static string Key(Vector3 v)
        {
            long x = (long)Math.Round(v.x / WELD_EPSILON);
            long y = (long)Math.Round(v.y / WELD_EPSILON);
            long z = (long)Math.Round(v.z / WELD_EPSILON);

            return x.ToString(CultureInfo.InvariantCulture) + "|" +
                   y.ToString(CultureInfo.InvariantCulture) + "|" +
                   z.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// STEP reals must carry a decimal point: "10" is not a valid REAL, "10.0" is. The
        /// "0.0#####" pattern guarantees at least one digit after the point for every value,
        /// including integers and zero.
        /// </summary>
        private static string F(float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f))
                f = 0f;

            return f.ToString("0.0#####", CultureInfo.InvariantCulture);
        }

        private static string B(bool b)
        {
            return b ? ".T." : ".F.";
        }

        /// <summary>
        /// STEP single-quoted strings escape an embedded quote by doubling it.
        /// </summary>
        private static string S(string s)
        {
            if (s == null)
                s = string.Empty;

            return "'" + s.Replace("'", "''") + "'";
        }

        private sealed class StepIdAllocator
        {
            private int next = 1;

            public int Next()
            {
                return next++;
            }
        }
    }
}
