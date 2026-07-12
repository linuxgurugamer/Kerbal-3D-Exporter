using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    internal static class ActiveVariantUtilities
    {
        private static readonly string[] VariantListMemberNames =
        {
            "variantList",
            "Variants",
            "variants",
            "partVariants",
            "availableVariants"
        };

        private static readonly string[] SelectedVariantMemberNames =
        {
            "SelectedVariant",
            "selectedVariant",
            "currentVariant",
            "activeVariant",
            "selectedVariantName",
            "SelectedVariantName",
            "currentVariantName",
            "CurrentVariantName"
        };

        private static readonly string[] VariantNameMemberNames =
        {
            "Name",
            "name",
            "variantName",
            "VariantName",
            "displayName",
            "DisplayName"
        };

        private static readonly string[] GameObjectSwitchListMemberNames =
        {
            "gameObjectVariants",
            "GameObjectSwitches",
            "gameObjectSwitches",
            "GameobjectSwitches",
            "gameobjectSwitches",
            "ObjectSwitches",
            "objectSwitches",
            "objectSwitch",
            "ObjectSwitch"
        };

        private static readonly string[] ObjectMemberNames =
        {
            "gameObjectName",
            "GameObjectName",
            "objectName",
            "ObjectName",
            "transformName",
            "TransformName",
            "name",
            "Name",
            "objectNames",
            "ObjectNames",
            "transformNames",
            "TransformNames",
            "objects",
            "Objects",
            "gameObjects",
            "GameObjects",
            "gameObject",
            "GameObject",
            "goName",
            "GOName",
            "transforms",
            "Transforms",
            "transform",
            "Transform"
        };

        private static readonly string[] EnabledMemberNames =
        {
            "isEnabled",
            "IsEnabled",
            "enabled",
            "Enabled",
            "active",
            "Active",
            "showObject",
            "ShowObject",
            "show",
            "Show",
            "visible",
            "Visible",
            "isVisible",
            "IsVisible",
            "visibleInVariant",
            "VisibleInVariant",
            "activeInVariant",
            "ActiveInVariant"
        };

        public static HashSet<Transform> BuildInactiveVariantTransformSet(List<Part> parts, Action<string> status)
        {
            HashSet<Transform> skip = new HashSet<Transform>();

            if (parts == null)
                return skip;

            int variantModuleCount = 0;
            int unresolvedModuleCount = 0;
            int skippedTransformCount = 0;

            for (int i = 0; i < parts.Count; i++)
            {
                Part part = parts[i];
                if (part == null || part.Modules == null)
                    continue;

                for (int m = 0; m < part.Modules.Count; m++)
                {
                    PartModule module = part.Modules[m];
                    if (module == null)
                        continue;

                    Type moduleType = module.GetType();
                    if (moduleType == null || moduleType.Name != "ModulePartVariants")
                        continue;

                    variantModuleCount++;
                    int before = skip.Count;
                    int added = AddInactiveVariantTransformsForModule(part, module, skip);
                    skippedTransformCount += added;

                    // If we found a variants module but resolved nothing to skip, the reflection
                    // lookups above likely don't match this KSP version's field names -- report it
                    // so a silent "exports every variant" failure is visible instead of invisible.
                    if (added == 0 && skip.Count == before)
                        unresolvedModuleCount++;
                }
            }

            if (status != null)
            {
                status("Part variant modules: " + variantModuleCount + ", inactive variant transforms skipped: " + skippedTransformCount);
                if (unresolvedModuleCount > 0)
                {
                    status("Warning: " + unresolvedModuleCount + " ModulePartVariants module(s) could not be read " +
                        "(unrecognized field names for this KSP version). Non-selected variant meshes on those parts " +
                        "will NOT be excluded from the export.");
                }
            }

            return skip;
        }

        private static int AddInactiveVariantTransformsForModule(Part part, PartModule module, HashSet<Transform> skip)
        {
            object selectedVariant = GetSelectedVariant(module);
            IList variants = GetFirstListMember(module, VariantListMemberNames);

            if (selectedVariant == null || variants == null)
                return 0;

            VariantObjects selected = CollectSwitchObjects(part, selectedVariant, true);
            VariantObjects all = new VariantObjects();

            for (int i = 0; i < variants.Count; i++)
            {
                object variant = variants[i];
                if (variant == null)
                    continue;

                VariantObjects objects = CollectSwitchObjects(part, variant, false);
                all.Add(objects);
            }

            int count = 0;

            foreach (Transform t in all.Transforms)
            {
                if (t == null)
                    continue;

                if (TransformIsInSetOrChildOfSet(t, selected.Transforms))
                    continue;

                count += AddTransformTree(t, skip);
            }

            foreach (string name in all.Names)
            {
                if (string.IsNullOrEmpty(name))
                    continue;

                if (selected.Names.Contains(name))
                    continue;

                count += AddPartTransformsByName(part, name, skip);
            }

            return count;
        }

        private static object GetSelectedVariant(PartModule module)
        {
            object selected = GetFirstMemberValue(module, SelectedVariantMemberNames);

            if (selected != null && !(selected is string))
                return selected;

            string selectedName = selected as string;
            IList variants = GetFirstListMember(module, VariantListMemberNames);
            if (variants == null || string.IsNullOrEmpty(selectedName))
                return selected;

            for (int i = 0; i < variants.Count; i++)
            {
                object variant = variants[i];
                string name = GetVariantName(variant);
                if (string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase))
                    return variant;
            }

            return selected;
        }

        private static string GetVariantName(object variant)
        {
            object value = GetFirstMemberValue(variant, VariantNameMemberNames);
            return value != null ? value.ToString() : string.Empty;
        }

        private static VariantObjects CollectSwitchObjects(Part part, object variant, bool selectedOnly)
        {
            VariantObjects result = new VariantObjects();
            IList switches = GetFirstListMember(variant, GameObjectSwitchListMemberNames);

            if (switches == null)
                return result;

            for (int i = 0; i < switches.Count; i++)
            {
                object sw = switches[i];
                if (sw == null)
                    continue;

                if (selectedOnly && !SwitchEnabled(sw))
                    continue;

                AddObjectsFromSwitch(part, sw, result);
            }

            return result;
        }

        private static bool SwitchEnabled(object sw)
        {
            object value = GetFirstMemberValue(sw, EnabledMemberNames);
            if (value == null)
                return true;

            if (value is bool)
                return (bool)value;

            bool parsed;
            if (bool.TryParse(value.ToString(), out parsed))
                return parsed;

            return true;
        }

        private static void AddObjectsFromSwitch(Part part, object sw, VariantObjects result)
        {
            for (int i = 0; i < ObjectMemberNames.Length; i++)
            {
                object value = GetMemberValue(sw, ObjectMemberNames[i]);
                AddObjectValue(part, value, result);
            }
        }

        private static void AddObjectValue(Part part, object value, VariantObjects result)
        {
            if (value == null || result == null)
                return;

            string s = value as string;
            if (s != null)
            {
                AddNameAndMatchingTransforms(part, result, s);

                // Some ModulePartVariants object-switch strings contain multiple names in one value
                // or use path-like names. Split common separators and match each token as well.
                string[] tokens = s.Split(new char[] { ',', ';', '|', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < tokens.Length; i++)
                    AddNameAndMatchingTransforms(part, result, tokens[i].Trim());

                return;
            }

            Transform t = value as Transform;
            if (t != null)
            {
                result.Transforms.Add(t);
                AddName(result.Names, t.name);
                return;
            }

            GameObject go = value as GameObject;
            if (go != null)
            {
                result.Transforms.Add(go.transform);
                AddName(result.Names, go.name);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                foreach (object item in enumerable)
                    AddObjectValue(part, item, result);

                return;
            }

            AddNameAndMatchingTransforms(part, result, value.ToString());
        }

        private static void AddNameAndMatchingTransforms(Part part, VariantObjects result, string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            AddName(result.Names, name);

            if (part == null)
                return;

            Transform[] transforms = part.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                    continue;

                if (TransformNameMatches(t, name))
                    result.Transforms.Add(t);
            }
        }

        private static bool TransformNameMatches(Transform transform, string name)
        {
            if (transform == null || string.IsNullOrEmpty(name))
                return false;

            string needle = name.Trim();
            if (needle.Length == 0)
                return false;

            if (string.Equals(transform.name, needle, StringComparison.OrdinalIgnoreCase))
                return true;

            // If the variant switch stores a path, compare against the leaf name too.
            int slash = Math.Max(needle.LastIndexOf('/'), needle.LastIndexOf('\\'));
            if (slash >= 0 && slash + 1 < needle.Length)
            {
                string leaf = needle.Substring(slash + 1);
                if (string.Equals(transform.name, leaf, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Last resort: match path-like tokens against the full transform path.
            // Require at least four characters so short generic names do not overmatch.
            if (needle.Length >= 4)
            {
                string path = GetTransformPath(transform);
                if (path.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
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

        private static void AddName(HashSet<string> names, string name)
        {
            if (names == null || string.IsNullOrEmpty(name))
                return;

            string cleaned = name.Trim();
            if (cleaned.Length > 0)
                names.Add(cleaned);
        }

        private static int AddPartTransformsByName(Part part, string transformName, HashSet<Transform> skip)
        {
            if (part == null || part.transform == null || string.IsNullOrEmpty(transformName))
                return 0;

            int count = 0;
            Transform[] transforms = part.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                    continue;

                if (TransformNameMatches(t, transformName))
                    count += AddTransformTree(t, skip);
            }

            return count;
        }

        private static int AddTransformTree(Transform root, HashSet<Transform> skip)
        {
            if (root == null || skip == null)
                return 0;

            int count = 0;

            if (!skip.Contains(root))
            {
                skip.Add(root);
                count++;
            }

            for (int i = 0; i < root.childCount; i++)
                count += AddTransformTree(root.GetChild(i), skip);

            return count;
        }

        public static bool IsTransformInSetOrChildOfSet(Transform transform, HashSet<Transform> set)
        {
            return TransformIsInSetOrChildOfSet(transform, set);
        }

        private static bool TransformIsInSetOrChildOfSet(Transform transform, HashSet<Transform> set)
        {
            if (transform == null || set == null || set.Count == 0)
                return false;

            Transform t = transform;
            while (t != null)
            {
                if (set.Contains(t))
                    return true;

                t = t.parent;
            }

            return false;
        }

        private static IList GetFirstListMember(object obj, string[] names)
        {
            object value = GetFirstMemberValue(obj, names);
            return value as IList;
        }

        private static object GetFirstMemberValue(object obj, string[] names)
        {
            if (obj == null || names == null)
                return null;

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
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(obj);

            PropertyInfo prop = type.GetProperty(name, flags);
            if (prop != null && prop.CanRead)
                return prop.GetValue(obj, null);

            return null;
        }

        private sealed class VariantObjects
        {
            public readonly HashSet<string> Names = new HashSet<string>();
            public readonly HashSet<Transform> Transforms = new HashSet<Transform>();

            public void Add(VariantObjects other)
            {
                if (other == null)
                    return;

                foreach (string name in other.Names)
                    Names.Add(name);

                foreach (Transform transform in other.Transforms)
                {
                    if (transform != null)
                        Transforms.Add(transform);
                }
            }
        }
    }
}
