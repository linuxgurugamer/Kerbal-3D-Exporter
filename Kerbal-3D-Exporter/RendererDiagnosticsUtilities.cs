using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    internal static class RendererDiagnosticsUtilities
    {
        private static readonly string[] SelectedVariantMemberNames =
        {
            "SelectedVariant", "selectedVariant", "currentVariant", "activeVariant",
            "selectedVariantName", "SelectedVariantName", "currentVariantName", "CurrentVariantName"
        };

        private static readonly string[] VariantNameMemberNames =
        {
            "Name", "name", "variantName", "VariantName", "displayName", "DisplayName"
        };

        public static List<RendererDiagnosticEntry> BuildEntries(List<Part> parts)
        {
            List<RendererDiagnosticEntry> entries = new List<RendererDiagnosticEntry>();
            if (parts == null)
                return entries;

            HashSet<Transform> inactiveVariantTransforms = ActiveVariantUtilities.BuildInactiveVariantTransformSet(parts, null);

            for (int i = 0; i < parts.Count; i++)
            {
                Part part = parts[i];
                if (part == null)
                    continue;

                string partName = GetPartName(part);
                string partTitle = part.partInfo != null ? part.partInfo.title : partName;
                string activeVariant = GetActiveVariant(part);
                string partGoName = part.gameObject != null ? part.gameObject.name : string.Empty;
                bool partActiveSelf = part.gameObject != null && part.gameObject.activeSelf;
                bool partActiveHierarchy = part.gameObject != null && part.gameObject.activeInHierarchy;

                Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null)
                        continue;

                    Mesh mesh = GetRendererMesh(renderer);
                    RendererDiagnosticEntry entry = new RendererDiagnosticEntry();
                    entry.Part = part;
                    entry.Renderer = renderer;
                    entry.Mesh = mesh;
                    entry.PartName = partName;
                    entry.PartTitle = partTitle;
                    entry.ActiveVariant = activeVariant;
                    entry.PartGameObjectName = partGoName;
                    entry.PartActiveSelf = partActiveSelf;
                    entry.PartActiveInHierarchy = partActiveHierarchy;
                    entry.Path = GetTransformPath(renderer.transform);
                    entry.RendererName = renderer.name;
                    entry.RendererType = renderer.GetType().Name;
                    entry.RendererEnabled = renderer.enabled;
                    entry.RendererActiveSelf = renderer.gameObject != null && renderer.gameObject.activeSelf;
                    entry.RendererActiveInHierarchy = renderer.gameObject != null && renderer.gameObject.activeInHierarchy;
                    entry.MeshName = mesh != null ? mesh.name : "<none>";
                    entry.Materials = GetMaterialList(renderer);
                    entry.HasMaterial = HasAnyRealMaterial(renderer);
                    entry.Key = BuildRendererKey(part, renderer);
                    entry.InactiveVariant = ActiveVariantUtilities.IsTransformInSetOrChildOfSet(renderer.transform, inactiveVariantTransforms);
                    entries.Add(entry);
                }

                // Colliders: physics-only meshes that typically have NO MeshRenderer at all (that's
                // literally why they're invisible in-game, not a naming convention). They're listed
                // here too, alongside renderers, so they're visible and individually toggleable in
                // the same table -- rather than silently always exporting with no way to control them.
                MeshFilter[] meshFilters = part.GetComponentsInChildren<MeshFilter>(true);
                for (int f = 0; f < meshFilters.Length; f++)
                {
                    MeshFilter mf = meshFilters[f];
                    if (mf == null || mf.sharedMesh == null || mf.gameObject == null)
                        continue;

                    // Already listed above as a normal renderer entry -- don't list it twice.
                    if (mf.GetComponent<MeshRenderer>() != null)
                        continue;

                    Collider[] colliders = mf.GetComponents<Collider>();
                    if (colliders == null || colliders.Length == 0)
                        continue;

                    RendererDiagnosticEntry entry = new RendererDiagnosticEntry();
                    entry.Part = part;
                    entry.Renderer = null;
                    entry.ColliderMeshFilter = mf;
                    entry.IsColliderOnly = true;
                    entry.Mesh = mf.sharedMesh;
                    entry.PartName = partName;
                    entry.PartTitle = partTitle;
                    entry.ActiveVariant = activeVariant;
                    entry.PartGameObjectName = partGoName;
                    entry.PartActiveSelf = partActiveSelf;
                    entry.PartActiveInHierarchy = partActiveHierarchy;
                    entry.Path = GetTransformPath(mf.transform);
                    entry.RendererName = mf.name;
                    entry.RendererType = GetColliderTypeNames(colliders);
                    entry.RendererEnabled = AnyColliderEnabled(colliders);
                    entry.RendererActiveSelf = mf.gameObject.activeSelf;
                    entry.RendererActiveInHierarchy = mf.gameObject.activeInHierarchy;
                    entry.MeshName = mf.sharedMesh.name;
                    entry.Materials = "<collider, no material>";
                    entry.HasMaterial = false;
                    // Default: colliders with no material are excluded from export unless the user
                    // explicitly re-enables one (their choice, once made, is still remembered across
                    // refreshes via rendererIncludeByKey -- this only sets the initial default).
                    entry.IncludeInExport = false;
                    entry.Key = BuildColliderKey(part, mf);
                    entry.InactiveVariant = ActiveVariantUtilities.IsTransformInSetOrChildOfSet(mf.transform, inactiveVariantTransforms);
                    entries.Add(entry);
                }
            }

            return entries;
        }



        public static string BuildRendererObjectKey(Renderer renderer)
        {
            if (renderer == null)
                return string.Empty;

            return NormalizeKey("renderer-object|" + renderer.GetInstanceID().ToString());
        }

        public static string BuildRendererTransformObjectKey(Renderer renderer)
        {
            if (renderer == null || renderer.transform == null)
                return string.Empty;

            return NormalizeKey("renderer-transform|" + renderer.transform.GetInstanceID().ToString());
        }

        public static string BuildRendererKey(Part part, Renderer renderer)
        {
            string partName = GetPartName(part);
            string path = renderer != null ? GetTransformPath(renderer.transform) : string.Empty;
            string rendererName = renderer != null ? renderer.name : string.Empty;
            string meshName = string.Empty;
            Mesh mesh = GetRendererMesh(renderer);
            if (mesh != null)
                meshName = mesh.name;

            return NormalizeKey(partName + "|" + path + "|" + rendererName + "|" + meshName);
        }

        public static string BuildRendererPathKey(Part part, Renderer renderer)
        {
            string partName = GetPartName(part);
            string path = renderer != null ? GetTransformPath(renderer.transform) : string.Empty;
            return NormalizeKey(partName + "|" + path);
        }

        public static string BuildRendererNameKey(Part part, Renderer renderer)
        {
            string partName = GetPartName(part);
            string path = renderer != null ? GetTransformPath(renderer.transform) : string.Empty;
            string rendererName = renderer != null ? renderer.name : string.Empty;
            return NormalizeKey(partName + "|" + path + "|" + rendererName);
        }

        public static string BuildRendererMeshKey(Part part, Renderer renderer)
        {
            string partName = GetPartName(part);
            string path = renderer != null ? GetTransformPath(renderer.transform) : string.Empty;
            Mesh mesh = GetRendererMesh(renderer);
            string meshName = mesh != null ? mesh.name : string.Empty;
            return NormalizeKey(partName + "|" + path + "|" + meshName);
        }

        public static void AddRendererKeys(HashSet<string> keys, Part part, Renderer renderer)
        {
            if (keys == null || renderer == null)
                return;

            AddKey(keys, BuildRendererObjectKey(renderer));
            AddKey(keys, BuildRendererTransformObjectKey(renderer));
            AddKey(keys, BuildRendererKey(part, renderer));
            AddKey(keys, BuildRendererPathKey(part, renderer));
            AddKey(keys, BuildRendererNameKey(part, renderer));
            AddKey(keys, BuildRendererMeshKey(part, renderer));
        }

        private static void AddKey(HashSet<string> keys, string key)
        {
            if (!string.IsNullOrEmpty(key))
                keys.Add(key);
        }

        private static string NormalizeKey(string key)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : key.ToLowerInvariant();
        }

        public static string BuildColliderKey(Part part, MeshFilter mf)
        {
            string partName = GetPartName(part);
            string path = mf != null ? GetTransformPath(mf.transform) : string.Empty;
            string meshName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : string.Empty;

            return NormalizeKey("collider|" + partName + "|" + path + "|" + meshName);
        }

        private static string GetColliderTypeNames(Collider[] colliders)
        {
            if (colliders == null || colliders.Length == 0)
                return "Collider";

            string result = string.Empty;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;

                if (result.Length > 0)
                    result += ",";
                result += colliders[i].GetType().Name;
            }

            return result.Length > 0 ? result : "Collider";
        }

        private static bool AnyColliderEnabled(Collider[] colliders)
        {
            if (colliders == null)
                return false;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                    return true;
            }

            return false;
        }

        private static Mesh GetRendererMesh(Renderer renderer)
        {
            if (renderer == null)
                return null;

            SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
            if (smr != null)
                return smr.sharedMesh;

            MeshFilter mf = renderer.GetComponent<MeshFilter>();
            if (mf != null)
                return mf.sharedMesh;

            return null;
        }

        private static string GetActiveVariant(Part part)
        {
            if (part == null || part.Modules == null)
                return "<none>";

            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule module = part.Modules[i];
                if (module == null || module.GetType() == null)
                    continue;

                if (module.GetType().Name != "ModulePartVariants")
                    continue;

                object selected = GetFirstMemberValue(module, SelectedVariantMemberNames);
                if (selected == null)
                    return "<unknown>";

                string selectedString = selected as string;
                if (!string.IsNullOrEmpty(selectedString))
                    return selectedString;

                string name = GetVariantName(selected);
                if (!string.IsNullOrEmpty(name))
                    return name;

                return selected.ToString();
            }

            return "<none>";
        }

        private static string GetVariantName(object variant)
        {
            object value = GetFirstMemberValue(variant, VariantNameMemberNames);
            return value != null ? value.ToString() : string.Empty;
        }

        private static object GetFirstMemberValue(object obj, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                object value = GetMemberValue(obj, names[i]);
                if (value != null)
                    return value;
            }
            return null;
        }

        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name))
                return null;

            Type type = obj.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            FieldInfo field = type.GetField(name, flags);
            if (field != null)
            {
                try { return field.GetValue(obj); }
                catch { return null; }
            }

            PropertyInfo prop = type.GetProperty(name, flags);
            if (prop != null && prop.GetIndexParameters().Length == 0)
            {
                try { return prop.GetValue(obj, null); }
                catch { return null; }
            }

            return null;
        }

        private static string GetMaterialList(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterials == null)
                return string.Empty;

            string result = string.Empty;
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (i > 0)
                    result += ",";
                result += mats[i] != null ? mats[i].name : "<null>";
            }
            return result;
        }

        private static bool HasAnyRealMaterial(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterials == null)
                return false;

            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null)
                    return true;
            }

            return false;
        }

        private static string GetPartName(Part part)
        {
            if (part == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(part.partName))
                return part.partName;
            return part.name ?? string.Empty;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            string path = transform.name;
            Transform t = transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }
    }
}
