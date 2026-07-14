using UnityEngine;

namespace Kerbal_3D_Exporter
{
    /// <summary>
    /// How the craft is oriented in the exported file.
    ///
    /// THE PROBLEM THIS SOLVES
    /// -----------------------
    /// Unity -- and therefore KSP -- is Y-UP. A rocket standing in the VAB has its long axis along
    /// +Y. STL, OBJ, 3MF, STEP, and every slicer in existence are Z-UP.
    ///
    /// The exporter used to write Unity world coordinates straight out with no axis conversion, so
    /// a vertical rocket's long axis landed on the file's +Y, which the slicer reads as horizontal
    /// depth. Result: a rocket that stands proudly in the VAB arrives in Cura lying on its side.
    ///
    /// WHY A ROTATION AND NOT AN AXIS SWAP
    /// -----------------------------------
    /// The tempting fix is to swap Y and Z. Do not. A swap of two axes has DETERMINANT -1: it is a
    /// mirror, not a rotation, and it silently inverts the winding of every triangle in the model.
    /// The result looks perfectly fine in a viewer (which happily renders backfaces) and is
    /// completely inside-out to a slicer, which will cheerfully offer to print everything EXCEPT
    /// the rocket.
    ///
    /// Every transform below is a genuine rotation, determinant +1, so winding is preserved and no
    /// normals need fixing. That is not an accident, it is the whole reason they are written as
    /// rotations.
    /// </summary>
    internal enum ExportOrientation
    {
        /// <summary>
        /// Raw Unity/KSP axes, Y-up. What the exporter always did. A VAB rocket comes out lying on
        /// its side in the slicer. Kept for anyone whose downstream workflow already compensates.
        /// </summary>
        AsInGame = 0,

        /// <summary>
        /// Rotate +90 degrees about X. Unity +Y (up in the VAB) becomes +Z (up in the slicer).
        /// A vertical rocket arrives vertical. This is the sane default.
        /// </summary>
        UprightZUp = 1,

        /// <summary>
        /// Upright, then laid down along +X, so the craft's long axis runs across the bed.
        ///
        /// This is not a novelty. A 30 cm rocket does not fit in the ~25 cm build height of most
        /// printers, and printing it lying down (or sliced into sections) is the usual answer.
        /// </summary>
        LayFlatAlongX = 2,

        /// <summary>
        /// Rotate 90 degrees about the vertical axis from Upright. Same standing pose, turned a
        /// quarter turn -- handy when a wide asymmetric craft (wings, radiators, solar arrays)
        /// fouls the bed in one direction but fits in the other.
        /// </summary>
        UprightRotated90 = 3,
    }

    internal static class ExportOrientationUtilities
    {
        public static string DisplayName(ExportOrientation o)
        {
            switch (o)
            {
                case ExportOrientation.UprightZUp:
                    return "Upright (Z up)";
                case ExportOrientation.LayFlatAlongX:
                    return "Lay flat along X";
                case ExportOrientation.UprightRotated90:
                    return "Upright, turned 90 deg";
                default:
                    return "As in game (Y up)";
            }
        }

        public static string Description(ExportOrientation o)
        {
            switch (o)
            {
                case ExportOrientation.UprightZUp:
                    return "A rocket that stands up in the VAB stands up in the slicer.";
                case ExportOrientation.LayFlatAlongX:
                    return "Craft lies along the bed. Use when it is taller than the printer.";
                case ExportOrientation.UprightRotated90:
                    return "Standing, turned a quarter turn about the vertical axis.";
                default:
                    return "Raw KSP axes. A vertical rocket exports lying on its side.";
            }
        }

        /// <summary>
        /// Apply the orientation to a single vertex.
        ///
        /// Called per vertex on a mesh that can be half a million triangles, so this is deliberately
        /// a plain switch over hand-written component swaps rather than a Quaternion or Matrix4x4
        /// multiply. The rotations are all exact multiples of 90 degrees, so the matrices are pure
        /// +/-1 permutations -- doing them by hand is both faster and exactly precise, with no
        /// trigonometric rounding creeping into vertices that are about to be welded together by
        /// the mesh cleanup stages.
        /// </summary>
        public static Vector3 Apply(Vector3 v, ExportOrientation o)
        {
            switch (o)
            {
                case ExportOrientation.UprightZUp:
                    // Rotate +90 about X: (x, y, z) -> (x, -z, y). Unity +Y becomes file +Z.
                    return new Vector3(v.x, -v.z, v.y);

                case ExportOrientation.UprightRotated90:
                    // Upright, then +90 about the new vertical (Z): (x, y, z) -> (-y, x, z),
                    // composed with the upright rotation above.
                    return new Vector3(v.z, v.x, v.y);

                case ExportOrientation.LayFlatAlongX:
                    // Upright, then rotate +90 about Y so the craft's long axis lies along +X.
                    return new Vector3(v.y, -v.z, -v.x);

                case ExportOrientation.AsInGame:
                default:
                    return v;
            }
        }
    }
}
