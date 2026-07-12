using System.Collections.Generic;
using System.Globalization;
using System.IO;

using static CraftMeshExporter.CraftMeshExporterToolbarRegistration;


namespace CraftMeshExporter
{
    internal static class ObjWriter
    {
        public static void Write(string file, List<Triangle> triangles)
        {
            using (StreamWriter sw = new StreamWriter(file))
            {
                sw.WriteLine("# KSP printable OBJ export");
                sw.WriteLine("# Units: millimeters after export scale");

                int index = 1;

                foreach (Triangle t in triangles)
                {
                    sw.WriteLine("v " + F(t.A.x) + " " + F(t.A.y) + " " + F(t.A.z));
                    sw.WriteLine("v " + F(t.B.x) + " " + F(t.B.y) + " " + F(t.B.z));
                    sw.WriteLine("v " + F(t.C.x) + " " + F(t.C.y) + " " + F(t.C.z));
                    sw.WriteLine("f " + index + " " + (index + 1) + " " + (index + 2));
                    index += 3;
                }
            }

            Log.Info("[CraftMeshExporter] Wrote OBJ: " + file);
        }

        private static string F(float f)
        {
            return f.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
