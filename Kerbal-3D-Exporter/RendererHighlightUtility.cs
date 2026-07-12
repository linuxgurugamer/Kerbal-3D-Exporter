using UnityEngine;

namespace Kerbal_3D_Exporter
{
    internal static class RendererHighlightUtility
    {
        private static Renderer currentRenderer;
        private static Material[] originalSharedMaterials;
        private static Material[] highlightMaterials;

        public static void SetHoveredRenderer(Renderer renderer)
        {
            if (renderer == currentRenderer)
                return;

            ClearHighlight();

            if (renderer == null)
                return;

            currentRenderer = renderer;
            originalSharedMaterials = renderer.sharedMaterials;

            if (originalSharedMaterials == null || originalSharedMaterials.Length == 0)
                return;

            highlightMaterials = new Material[originalSharedMaterials.Length];
            for (int i = 0; i < originalSharedMaterials.Length; i++)
            {
                Material src = originalSharedMaterials[i];
                if (src == null)
                {
                    highlightMaterials[i] = null;
                    continue;
                }

                Material m = new Material(src);
                if (m.HasProperty("_Color"))
                    m.color = Color.yellow;
                if (m.HasProperty("_EmissiveColor"))
                    m.SetColor("_EmissiveColor", Color.yellow);
                if (m.HasProperty("_EmissionColor"))
                    m.SetColor("_EmissionColor", Color.yellow);
                highlightMaterials[i] = m;
            }

            renderer.sharedMaterials = highlightMaterials;
        }

        public static void ClearHighlight()
        {
            if (currentRenderer != null && originalSharedMaterials != null)
                currentRenderer.sharedMaterials = originalSharedMaterials;

            if (highlightMaterials != null)
            {
                for (int i = 0; i < highlightMaterials.Length; i++)
                {
                    if (highlightMaterials[i] != null)
                        Object.Destroy(highlightMaterials[i]);
                }
            }

            currentRenderer = null;
            originalSharedMaterials = null;
            highlightMaterials = null;
        }
    }
}
