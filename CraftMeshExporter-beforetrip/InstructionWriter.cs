using System.Globalization;
using System.IO;

namespace CraftMeshExporter
{
    internal static class InstructionWriter
    {
        public static void Write(string file, string format, string modelFile, float scale)
        {
            using (StreamWriter sw = new StreamWriter(file))
            {
                sw.WriteLine("KSP CraftMeshExporter - Next Steps");
                sw.WriteLine("==================================");
                sw.WriteLine();
                sw.WriteLine("Model file:");
                sw.WriteLine(modelFile);
                sw.WriteLine();
                sw.WriteLine("Format:");
                sw.WriteLine(format);
                sw.WriteLine();
                sw.WriteLine("Export scale:");
                sw.WriteLine(scale.ToString("R", CultureInfo.InvariantCulture));
                sw.WriteLine();
                sw.WriteLine("Units:");
                sw.WriteLine("The exported model is written in millimeters.");
                sw.WriteLine("KSP uses meters internally. The exporter converts meters to millimeters, then applies your export scale.");
                sw.WriteLine();
                sw.WriteLine("Recommended next steps:");
                sw.WriteLine("1. Open the model in Blender, MeshLab, Meshmixer, Netfabb, or similar software.");
                sw.WriteLine("2. Inspect for missing parts, inverted faces, duplicate shells, and thin details.");
                sw.WriteLine("3. Use boolean union or mesh repair to make a watertight mesh.");
                sw.WriteLine("4. Remove internal geometry where possible.");
                sw.WriteLine("5. Repair non-manifold edges, holes, and self-intersections.");
                sw.WriteLine("6. Thicken fragile details such as antennas, ladders, solar panels, fins, and struts.");
                sw.WriteLine("7. Confirm final model size in millimeters.");
                sw.WriteLine("8. Export the repaired model as STL.");
                sw.WriteLine("9. Open in your slicer and check supports, wall thickness, and print orientation.");
                sw.WriteLine();
                sw.WriteLine("Important:");
                sw.WriteLine("KSP craft are made from many overlapping game meshes. This export is only a starting point.");
                sw.WriteLine("For reliable printing, the model usually needs repair before slicing.");
            }

            UnityEngine.Debug.Log("[CraftMeshExporter] Wrote instructions: " + file);
        }
    }
}
