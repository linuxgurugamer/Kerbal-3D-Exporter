using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    internal static class PartDiagnosticsWriter
    {
        private static readonly string[] VariantListMemberNames =
        {
            "Variants", "variants", "partVariants", "availableVariants"
        };

        private static readonly string[] SelectedVariantMemberNames =
        {
            "SelectedVariant", "selectedVariant", "currentVariant", "activeVariant",
            "selectedVariantName", "SelectedVariantName", "currentVariantName", "CurrentVariantName"
        };

        private static readonly string[] VariantNameMemberNames =
        {
            "Name", "name", "variantName", "VariantName", "displayName", "DisplayName"
        };

        public static void Write(string file, List<Part> parts, string sceneDescription)
        {
            if (string.IsNullOrEmpty(file))
                return;

            using (StreamWriter sw = new StreamWriter(file))
            {
                sw.WriteLine("Kerbal 3DExporter part diagnostics");
                sw.WriteLine("==================================");
                sw.WriteLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sw.WriteLine("Scene: " + Safe(sceneDescription));
                sw.WriteLine("Parts: " + (parts != null ? parts.Count.ToString() : "0"));
                sw.WriteLine();
                sw.WriteLine("This file shows each part's active GameObject state, selected part variant, and child GameObjects/renderers.");
                sw.WriteLine("Use Path, Mesh, Material, and Variant information here to identify shroud/fairing meshes that should be excluded.");
                sw.WriteLine();

                if (parts == null)
                    return;

                for (int i = 0; i < parts.Count; i++)
                {
                    Part part = parts[i];
                    if (part == null)
                        continue;

                    WritePart(sw, part, i);
                    sw.WriteLine();
                }
            }
        }

        private static void WritePart(StreamWriter sw, Part part, int index)
        {
            string partName = GetPartName(part);
            string partTitle = part.partInfo != null ? part.partInfo.title : string.Empty;
            GameObject partGo = part.gameObject;

            sw.WriteLine("PART " + index);
            sw.WriteLine("  PartName: " + Safe(partName));
            sw.WriteLine("  PartTitle: " + Safe(partTitle));
            sw.WriteLine("  GameObject: " + Safe(partGo != null ? partGo.name : "<null>"));
            sw.WriteLine("  GameObjectPath: " + Safe(partGo != null ? GetTransformPath(partGo.transform) : ""));
            sw.WriteLine("  ActiveSelf: " + Bool(partGo != null && partGo.activeSelf));
            sw.WriteLine("  ActiveInHierarchy: " + Bool(partGo != null && partGo.activeInHierarchy));
            sw.WriteLine("  IsEngine: " + Bool(EngineUtilities.IsEnginePart(part)));

            WriteVariantInfo(sw, part);
            WriteGameObjectTree(sw, part);
        }

        private static void WriteVariantInfo(StreamWriter sw, Part part)
        {
            sw.WriteLine("  PartVariants:");

            if (part.Modules == null)
            {
                sw.WriteLine("    <no modules>");
                return;
            }

            int count = 0;

            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule module = part.Modules[i];
                if (module == null || module.GetType() == null)
                    continue;

                if (module.GetType().Name != "ModulePartVariants")
                    continue;

                count++;

                string selectedName = GetSelectedVariantName(module);
                sw.WriteLine("    Module: " + module.GetType().FullName);
                sw.WriteLine("    ActiveVariant: " + Safe(selectedName));
                sw.WriteLine("    Variants: " + Safe(GetVariantNameList(module)));
            }

            if (count == 0)
                sw.WriteLine("    <none>");
        }

        private static void WriteGameObjectTree(StreamWriter sw, Part part)
        {
            sw.WriteLine("  GameObjects:");

            Transform[] transforms = part.GetComponentsInChildren<Transform>(true);
            if (transforms == null || transforms.Length == 0)
            {
                sw.WriteLine("    <none>");
                return;
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null || t.gameObject == null)
                    continue;

                GameObject go = t.gameObject;
                Renderer renderer = go.GetComponent<Renderer>();
                MeshFilter mf = go.GetComponent<MeshFilter>();
                SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();

                string rendererInfo = "Renderer=<none>";
                if (renderer != null)
                {
                    rendererInfo = "Renderer=" + renderer.GetType().Name +
                        ", RendererEnabled=" + Bool(renderer.enabled) +
                        ", RendererVisible=" + Bool(renderer.isVisible) +
                        ", Materials=" + Safe(GetMaterialList(renderer));
                }

                string meshInfo = "Mesh=<none>";
                if (mf != null && mf.sharedMesh != null)
                    meshInfo = "Mesh=" + Safe(mf.sharedMesh.name) + ", MeshType=MeshFilter";
                else if (smr != null && smr.sharedMesh != null)
                    meshInfo = "Mesh=" + Safe(smr.sharedMesh.name) + ", MeshType=SkinnedMeshRenderer";

                sw.WriteLine("    Path=" + Safe(GetTransformPath(t)) +
                    " | GameObject=" + Safe(go.name) +
                    " | ActiveSelf=" + Bool(go.activeSelf) +
                    " | ActiveInHierarchy=" + Bool(go.activeInHierarchy) +
                    " | " + rendererInfo +
                    " | " + meshInfo);
            }
        }

        private static string GetSelectedVariantName(PartModule module)
        {
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

        private static string GetVariantNameList(PartModule module)
        {
            IList variants = GetFirstListMember(module, VariantListMemberNames);
            if (variants == null)
                return "<unknown>";

            string result = string.Empty;
            for (int i = 0; i < variants.Count; i++)
            {
                object variant = variants[i];
                string name = GetVariantName(variant);
                if (string.IsNullOrEmpty(name))
                    name = variant != null ? variant.ToString() : "<null>";

                if (i > 0)
                    result += ", ";
                result += name;
            }

            return result;
        }

        private static string GetVariantName(object variant)
        {
            object value = GetFirstMemberValue(variant, VariantNameMemberNames);
            return value != null ? value.ToString() : string.Empty;
        }

        private static IList GetFirstListMember(object obj, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                object value = GetMemberValue(obj, names[i]);
                IList list = value as IList;
                if (list != null)
                    return list;
            }
            return null;
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

        private static string Safe(string s)
        {
            return s ?? string.Empty;
        }

        private static string Bool(bool b)
        {
            return b ? "true" : "false";
        }
    }
}
