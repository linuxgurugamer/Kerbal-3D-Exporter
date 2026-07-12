using UnityEngine;

namespace CraftMeshExporter
{
    internal class RendererDiagnosticEntry
    {
        public Part Part;
        public Renderer Renderer;
        public Mesh Mesh;
        public string PartName;
        public string PartTitle;
        public string ActiveVariant;
        public string PartGameObjectName;
        public bool PartActiveSelf;
        public bool PartActiveInHierarchy;
        public string Path;
        public string RendererName;
        public string RendererType;
        public bool RendererEnabled;
        public bool RendererActiveSelf;
        public bool RendererActiveInHierarchy;
        public string MeshName;
        public string Materials;
        public string Key;
        public bool InactiveVariant;
        public bool IncludeInExport = true;

        // Colliders (physics-only meshes, typically with no MeshRenderer at all -- that's exactly
        // why they're invisible in-game) are listed in the same table as renderers so they can be
        // seen and toggled the same way. When this is set, Renderer is null and the fields above
        // are populated from the MeshFilter/GameObject/Collider components instead.
        public MeshFilter ColliderMeshFilter;
        public bool IsColliderOnly;

        // True if this entry has at least one real (non-null) material. Renderer entries with an
        // empty/all-null material list, and every collider-only entry (no Renderer means no
        // material is even possible), have this set to false.
        public bool HasMaterial;

        public string DisplayLine
        {
            get
            {
                return PartTitle + " [" + PartName + "]  |  Variant: " + ActiveVariant +
                    "  |  " + (IsColliderOnly ? "Collider: " : "Renderer: ") + RendererName +
                    "  |  Mesh: " + MeshName +
                    "  |  Active: " + (RendererActiveInHierarchy ? "yes" : "no");
            }
        }
    }
}
