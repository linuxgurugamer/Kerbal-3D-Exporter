using System;

namespace Kerbal_3D_Exporter
{
    // Single shared source of truth for the "bottom attach node has nothing attached" check, used
    // by ShroudUtilities, MeshCollector, and VariantSnapshotUtilities. Previously each file had its
    // own private copy of this same logic; keeping one copy means all three always agree on it.
    internal static class AttachNodeUtilities
    {
        public static bool BottomAttachNodeIsEmpty(Part part)
        {
            if (part == null || part.attachNodes == null)
                return false;

            bool foundBottom = false;

            for (int i = 0; i < part.attachNodes.Count; i++)
            {
                AttachNode node = part.attachNodes[i];
                if (node == null || string.IsNullOrEmpty(node.id))
                    continue;

                if (!string.Equals(node.id, "bottom", StringComparison.OrdinalIgnoreCase))
                    continue;

                foundBottom = true;

                if (node.attachedPart != null)
                    return false;
            }

            return foundBottom;
        }
    }
}
