using System.Globalization;
using System.IO;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{
    internal static class InstructionWriter
    {
        public static void Write(string file, string format, string modelFile, float scale)
        {
            using (StreamWriter sw = new StreamWriter(file))
            {
                sw.WriteLine("Kerbal 3D Exporter - Next Steps");
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
                if (format == "STEP")
                {
                    sw.WriteLine("About STEP:");
                    sw.WriteLine("This STEP file is a FACETED B-rep. Every triangle of the mesh became its own flat face.");
                    sw.WriteLine("It is valid STEP and opens in Fusion 360, SolidWorks, FreeCAD, Onshape, and similar CAD tools,");
                    sw.WriteLine("but it is not a \"real\" CAD model: there are no smooth NURBS surfaces and no analytic cylinders.");
                    sw.WriteLine("A mesh simply does not contain that information, and no converter can invent it back.");
                    sw.WriteLine("Expect a large file and a slow import; roughly 17 STEP entities are needed per triangle.");
                    sw.WriteLine("If the file is unmanageable, decimate the mesh in Blender or MeshLab first, then re-export.");
                    sw.WriteLine("The model is written as a surface body (open shell), not a closed solid, because a KSP craft");
                    sw.WriteLine("is many overlapping part meshes and is not watertight. Most slicers cannot read STEP at all;");
                    sw.WriteLine("use the STL or 3MF export for printing, and this file for CAD work.");
                    sw.WriteLine();
                }

                if (format == "3MF")
                {
                    sw.WriteLine("About 3MF:");
                    sw.WriteLine("3MF stores an indexed vertex list, so it is usually much smaller than the equivalent STL,");
                    sw.WriteLine("and it records the unit (millimeters) inside the file, so a slicer does not have to guess the scale.");
                    sw.WriteLine("It opens directly in Cura, PrusaSlicer, SuperSlicer, OrcaSlicer, Bambu Studio, and Creality Print.");
                    sw.WriteLine();
                    sw.WriteLine("If \"separate object + color per part\" was enabled, each KSP part is its own object inside");
                    sw.WriteLine("the file, so the slicer shows them as individually selectable bodies you can delete, move, or");
                    sw.WriteLine("assign to their own extruder. STL cannot express this at all.");
                    sw.WriteLine();
                    sw.WriteLine("The per-part colors are SYNTHETIC. KSP parts are textured, not flat-colored, so there is no");
                    sw.WriteLine("single honest color to read from a part. The colors exist only so adjacent parts are easy to");
                    sw.WriteLine("tell apart on screen. They are not the part's real in-game appearance.");
                    sw.WriteLine();
                }

                sw.WriteLine("Important:");
                sw.WriteLine("KSP craft are made from many overlapping game meshes. This export is only a starting point.");
                sw.WriteLine("For reliable printing, the model usually needs repair before slicing.");
            }

            Log.Info("Wrote instructions: " + file);
        }
    }
}
