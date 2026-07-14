using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    internal static class MeshCollector
    {
        private const float METERS_TO_MM = 1000f;
        private const bool EXPORT_DISABLED_RENDERERS = false;

        public static List<Triangle> BuildTriangleList(
            List<Part> parts,
            float userScale,
            Action<string> status,
            bool defaultShowShrouds,
            bool excludeLaunchClamps,
            List<EngineShroudOption> engineOptions,
            HashSet<Transform> shroudTransformsToSkip,
            HashSet<Transform> inactiveVariantTransformsToSkip,
            HashSet<Transform> originallyHiddenVariantTransforms,
#if EXPORT_EXCLUSION_RULE_DEFINED
            List<ExportExclusionRule> exclusionRules,
#endif
            HashSet<string> disabledRendererKeys,
            HashSet<Renderer> disabledRenderers,
            HashSet<Transform> disabledRendererTransforms,
            List<string> diagnostics,
            Dictionary<int, string> partNames,
            ExportOrientation orientation)
        {
            List<Triangle> triangles = new List<Triangle>();
            float finalScale = METERS_TO_MM * userScale;
            Dictionary<Part, bool> engineVisibility = BuildEngineVisibilityMap(engineOptions);

            // See the matching comment in ShroudUtilities.SetShroudVisibility: parts with a
            // ModulePartVariants module are unconditionally exempted from the "bottom attach node
            // empty" shroud heuristic, regardless of attach-node state, so this file's own copy of
            // that check has to agree or it would independently re-exclude a variant-managed mesh
            // that ShroudUtilities correctly left alone.
            HashSet<Part> variantParts = VariantSnapshotUtilities.BuildPartsWithVariantModules(parts);

            // Vertices come out of Transform.TransformPoint in raw Unity WORLD space, which is
            // wherever the craft happens to sit (an arbitrary, often large, non-zero position in the
            // VAB/SPH, and something that can drift further in Flight after floating-origin shifts).
            // Subtracting the root part's world position here means the exported model's x/y/z are
            // always relative to the craft itself -- the root part ends up at (0, 0, 0) -- instead of
            // wherever the craft happened to be sitting in the world.
            Vector3 originOffset = GetRootPartPosition(parts);

            if (diagnostics != null)
            {
                diagnostics.Add("Kerbal 3D Exporter mesh diagnostics");
                diagnostics.Add("==================================");
                diagnostics.Add("Lines marked EXPORT are written to the model. Lines marked SKIP are not.");
                diagnostics.Add("Use PartName and Path/Material tokens here in GameData/Kerbal-3D-Exporter/shroud-exclusions.txt");
                diagnostics.Add("Model origin (world space, subtracted from every vertex): " + originOffset);
                diagnostics.Add("Export orientation: " + ExportOrientationUtilities.DisplayName(orientation)
                    + " -- " + ExportOrientationUtilities.Description(orientation));
                diagnostics.Add("");
            }

            if (parts == null)
                return triangles;

            int partIndex = 0;

            foreach (Part part in parts)
            {
                partIndex++;

                if (part == null)
                    continue;

                if (partNames != null && !partNames.ContainsKey(partIndex))
                    partNames[partIndex] = GetPartDisplayName(part);

                bool isLaunchClamp = LaunchClampUtilities.IsLaunchClampPart(part);

                if (excludeLaunchClamps && isLaunchClamp)
                {
                    AddDiagnostic(diagnostics, "SKIP", "launch clamp excluded by option", part, part.transform, null, null);
                    continue;
                }

                // Safety net: if this part's name suggests it might be a launch clamp but none of
                // the detection signals in LaunchClampUtilities caught it, log its actual module
                // list. If launch clamp exclusion is still missing parts, this line in
                // _mesh_diagnostics.txt shows the real module names to check against instead of
                // guessing at another one.
                if (excludeLaunchClamps && !isLaunchClamp && diagnostics != null &&
                    part.name != null && part.name.IndexOf("clamp", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    diagnostics.Add("NOTE | Part name suggests a launch clamp but was not detected as one | PartName=" +
                        GetPartName(part) + " | Modules=" + GetModuleTypeNames(part));
                }

                // Global ShowShrouds is the master switch.
                // Per-engine ShowShroud can only further disable a shroud; it must not re-enable
                // shrouds when the global checkbox is off.
                bool exportPartShrouds = defaultShowShrouds;

                if (EngineUtilities.IsEnginePart(part) && engineVisibility.ContainsKey(part))
                    exportPartShrouds = exportPartShrouds && engineVisibility[part];

                bool hideEngineShroud = !exportPartShrouds;
                bool bottomNodeHasNoAttachment = AttachNodeUtilities.BottomAttachNodeIsEmpty(part) && !variantParts.Contains(part);

                List<MeshFilter> filters = CollectOwnMeshFilters(part);
                foreach (MeshFilter mf in filters)
                {
                    if (mf == null || mf.sharedMesh == null)
                        continue;

                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    string reason = GetSkipReason(part, mf.transform, mr, hideEngineShroud, bottomNodeHasNoAttachment, shroudTransformsToSkip, inactiveVariantTransformsToSkip, originallyHiddenVariantTransforms,
#if EXPORT_EXCLUSION_RULE_DEFINED
                        exclusionRules, 
#endif
                        disabledRendererKeys, disabledRenderers, disabledRendererTransforms);
                    if (reason != null)
                    {
                        AddDiagnostic(diagnostics, "SKIP", reason, part, mf.transform, mf.sharedMesh, mr);
                        continue;
                    }

                    int before = triangles.Count;
                    AddMeshTriangles(triangles, mf.sharedMesh, mf.transform, finalScale, mr, hideEngineShroud, originOffset, partIndex, orientation);
                    AddDiagnostic(diagnostics, "EXPORT", "triangles=" + (triangles.Count - before), part, mf.transform, mf.sharedMesh, mr);
                }

                List<SkinnedMeshRenderer> skins = CollectOwnSkinnedMeshRenderers(part);
                foreach (SkinnedMeshRenderer smr in skins)
                {
                    if (smr == null)
                        continue;

                    string reason = GetSkipReason(part, smr.transform, smr, hideEngineShroud, bottomNodeHasNoAttachment, shroudTransformsToSkip, inactiveVariantTransformsToSkip, originallyHiddenVariantTransforms,
#if EXPORT_EXCLUSION_RULE_DEFINED
                        exclusionRules, 
#endif
                        disabledRendererKeys, disabledRenderers, disabledRendererTransforms);
                    if (reason != null)
                    {
                        AddDiagnostic(diagnostics, "SKIP", reason, part, smr.transform, smr.sharedMesh, smr);
                        continue;
                    }

                    Mesh baked = new Mesh();
                    smr.BakeMesh(baked);

                    int before = triangles.Count;
                    AddMeshTriangles(triangles, baked, smr.transform, finalScale, smr, hideEngineShroud, originOffset, partIndex, orientation);
                    AddDiagnostic(diagnostics, "EXPORT", "triangles=" + (triangles.Count - before), part, smr.transform, baked, smr);
                }

                if (partIndex % 10 == 0 && status != null)
                    status("Processed " + partIndex + " / " + parts.Count + " parts.");
            }

            return triangles;
        }

        private static string GetSkipReason(
            Part part,
            Transform transform,
            Renderer renderer,
            bool hideEngineShroud,
            bool bottomNodeHasNoAttachment,
            HashSet<Transform> shroudTransformsToSkip,
            HashSet<Transform> inactiveVariantTransformsToSkip,
            HashSet<Transform> originallyHiddenVariantTransforms,
#if EXPORT_EXCLUSION_RULE_DEFINED
            List<ExportExclusionRule> exclusionRules,
#endif
            HashSet<string> disabledRendererKeys,
            HashSet<Renderer> disabledRenderers,
            HashSet<Transform> disabledRendererTransforms)
        {
            if (RendererIsUserDisabled(part, transform, renderer, disabledRendererKeys, disabledRenderers, disabledRendererTransforms))
                return "disabled in renderer diagnostics window";

            if (TransformIsInSkipSet(transform, inactiveVariantTransformsToSkip))
                return "inactive part variant transform";

            // Reflection-free backstop for the check above: this mesh was already hidden by KSP
            // (before this exporter touched anything) on a part that has a variants module. See
            // VariantSnapshotUtilities.
            if (TransformIsInSkipSet(transform, originallyHiddenVariantTransforms))
                return "originally hidden mesh on a part-variant part; kept hidden";

            // Enforce shroud skipping before normal renderer activity checks.
            // Some KSP shroud/fairing renderers can exist and be active even when KSP would not draw
            // them, especially variant-based engine shrouds.
            if (hideEngineShroud && TransformIsInSkipSet(transform, shroudTransformsToSkip))
                return "marked shroud/fairing transform; shrouds disabled";

            if (hideEngineShroud && renderer != null && RendererLooksLikeShroud(renderer))
                return "renderer/material/path looks like shroud/fairing; shrouds disabled";

#if EXPORT_EXCLUSION_RULE_DEFINED
            // shroud-exclusions.txt is documented (see the sample file it generates) as applying
            // only when shrouds/fairings are being hidden for that part/engine -- it's a refinement
            // to the shroud-matching heuristics above, not a general-purpose always-exclude list.
            // Making it apply unconditionally means any rule written with that documented scope in
            // mind can suddenly match everywhere, all the time -- which is exactly what caused most
            // renderers to disappear from an export. Keep this gated.
            if (hideEngineShroud && renderer != null && MatchesUserExclusion(part, renderer, exclusionRules))
                return "matched shroud-exclusions.txt; shrouds disabled";
#endif

            // Stock-style engine shrouds are only visible in KSP when another part is attached to
            // the bottom attach node.  If the bottom node exists but is empty, skip likely shroud
            // renderers even when shrouds are enabled, because the in-game vessel would not show them.
            if (bottomNodeHasNoAttachment && renderer != null && RendererLooksLikeShroud(renderer))
                return "bottom attach node is empty; KSP would hide this shroud/fairing";

            if (bottomNodeHasNoAttachment && TransformIsInSkipSet(transform, shroudTransformsToSkip))
                return "bottom attach node is empty; marked shroud/fairing transform";

            if (!EXPORT_DISABLED_RENDERERS && renderer != null && (!renderer.enabled || !renderer.gameObject.activeInHierarchy))
                return "disabled renderer or inactive hierarchy";

            return null;
        }


        private static bool RendererIsUserDisabled(Part part, Transform transform, Renderer renderer, HashSet<string> disabledRendererKeys, HashSet<Renderer> disabledRenderers, HashSet<Transform> disabledRendererTransforms)
        {
            if (renderer != null && disabledRenderers != null && disabledRenderers.Contains(renderer))
                return true;

            // Transform-based matching works whether or not there's a Renderer -- this is what lets
            // collider-only meshes (no MeshRenderer at all) be individually disabled from the
            // Renderer Diagnostics window, the same way normal renderers can be.
            if (disabledRendererTransforms != null)
            {
                Transform t = transform;
                while (t != null)
                {
                    if (disabledRendererTransforms.Contains(t))
                        return true;
                    t = t.parent;
                }
            }

            // Everything below is key-string matching built from Renderer-specific data (name,
            // materials, etc.) -- there's nothing meaningful to build without a Renderer, and the
            // transform-based check above already covers the collider case.
            if (renderer == null)
                return false;

            if (disabledRendererKeys == null || disabledRendererKeys.Count == 0)
                return false;

            string key = RendererDiagnosticsUtilities.BuildRendererObjectKey(renderer);
            if (!string.IsNullOrEmpty(key) && disabledRendererKeys.Contains(key))
                return true;

            key = RendererDiagnosticsUtilities.BuildRendererTransformObjectKey(renderer);
            if (!string.IsNullOrEmpty(key) && disabledRendererKeys.Contains(key))
                return true;

            key = RendererDiagnosticsUtilities.BuildRendererKey(part, renderer);
            if (!string.IsNullOrEmpty(key) && disabledRendererKeys.Contains(key))
                return true;

            key = RendererDiagnosticsUtilities.BuildRendererPathKey(part, renderer);
            if (!string.IsNullOrEmpty(key) && disabledRendererKeys.Contains(key))
                return true;

            key = RendererDiagnosticsUtilities.BuildRendererNameKey(part, renderer);
            if (!string.IsNullOrEmpty(key) && disabledRendererKeys.Contains(key))
                return true;

            key = RendererDiagnosticsUtilities.BuildRendererMeshKey(part, renderer);
            return !string.IsNullOrEmpty(key) && disabledRendererKeys.Contains(key);
        }

#if EXPORT_EXCLUSION_RULE_DEFINED
        private static bool MatchesUserExclusion(Part part, Renderer renderer, List<ExportExclusionRule> rules)
        {
            if (renderer == null || rules == null || rules.Count == 0)
                return false;

            string partName = GetPartName(part);
            string haystack = BuildRendererSearchText(renderer);

            for (int i = 0; i < rules.Count; i++)
            {
                ExportExclusionRule rule = rules[i];
                if (rule == null || string.IsNullOrEmpty(rule.Token))
                    continue;

                if (!rule.MatchesPart(partName))
                    continue;

                if (haystack.Contains(rule.Token))
                    return true;
            }

            return false;
        }
#endif

        private static string BuildRendererSearchText(Renderer renderer)
        {
            string text = string.Empty;

            if (renderer == null)
                return text;

            text += " " + renderer.name;
            if (renderer.gameObject != null)
                text += " " + renderer.gameObject.name;

            text += " " + GetTransformPath(renderer.transform);

            Material[] mats = renderer.sharedMaterials;
            if (mats != null)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                        text += " " + mats[i].name;
                }
            }

            return text.ToLowerInvariant();
        }

        private static void AddDiagnostic(List<string> diagnostics, string action, string reason, Part part, Transform transform, Mesh mesh, Renderer renderer)
        {
            if (diagnostics == null)
                return;

            string mats = GetMaterialList(renderer);
            string meshName = mesh != null ? mesh.name : "<none>";
            string partTitle = part != null && part.partInfo != null ? part.partInfo.title : string.Empty;

            diagnostics.Add(action + " | Reason=" + reason +
                " | PartName=" + GetPartName(part) +
                " | PartTitle=" + partTitle +
                " | Path=" + GetTransformPath(transform) +
                " | Mesh=" + meshName +
                " | Materials=" + mats);
        }

        private static string GetMaterialList(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterials == null)
                return string.Empty;

            string s = string.Empty;
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (i > 0)
                    s += ",";
                s += mats[i] != null ? mats[i].name : "<null>";
            }
            return s;
        }

        // The root of a part tree is the one with no parent. This works the same way whether parts
        // came from an editor ShipConstruct or a Flight Vessel, without needing scene-specific API
        // (e.g. EditorLogic.RootPart / Vessel.rootPart) that would have to be checked separately for
        // each scene. Falls back to the first part if no parentless part is found (shouldn't happen
        // in practice, but the model should never fail to export just because of this).
        private static Vector3 GetRootPartPosition(List<Part> parts)
        {
            if (parts == null || parts.Count == 0)
                return Vector3.zero;

            for (int i = 0; i < parts.Count; i++)
            {
                Part p = parts[i];
                if (p != null && p.parent == null && p.transform != null)
                    return p.transform.position;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null && parts[i].transform != null)
                    return parts[i].transform.position;
            }

            return Vector3.zero;
        }

        private static string GetModuleTypeNames(Part part)
        {
            if (part == null || part.Modules == null)
                return string.Empty;

            string result = string.Empty;
            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule module = part.Modules[i];
                if (module == null)
                    continue;

                Type t = module.GetType();
                if (result.Length > 0)
                    result += ",";
                result += t != null ? t.Name : "<null>";
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

        private static bool TransformIsInSkipSet(Transform transform, HashSet<Transform> skipSet)
        {
            if (transform == null || skipSet == null || skipSet.Count == 0)
                return false;

            Transform t = transform;
            while (t != null)
            {
                if (skipSet.Contains(t))
                    return true;

                t = t.parent;
            }

            return false;
        }


        private static Dictionary<Part, bool> BuildEngineVisibilityMap(List<EngineShroudOption> engineOptions)
        {
            Dictionary<Part, bool> map = new Dictionary<Part, bool>();

            if (engineOptions == null)
                return map;

            for (int i = 0; i < engineOptions.Count; i++)
            {
                EngineShroudOption option = engineOptions[i];
                if (option == null || option.Part == null)
                    continue;

                map[option.Part] = option.ShowShroud;
            }

            return map;
        }

        // KSP parents each part's Unity transform under its attachment parent's transform, so a
        // plain GetComponentsInChildren(true) call on a part doesn't stay within that part -- it
        // also recurses into every part attached below it, since they're all nested underneath in
        // the same Unity scene graph. That silently duplicated meshes from child parts into their
        // parent's export (misattributed to the wrong Part in diagnostics), which is what made
        // launch clamp exclusion look broken: the clamp's own Part was correctly skipped, but a
        // second copy of its meshes was still being pulled in via whatever part is directly above
        // it in the attachment chain. These two methods walk the hierarchy manually instead, and
        // stop descending as soon as they hit a child transform that has its own Part component
        // (i.e. is the root of a different part), so each part's mesh collection only ever includes
        // that part's own geometry.
        private static List<MeshFilter> CollectOwnMeshFilters(Part part)
        {
            List<MeshFilter> result = new List<MeshFilter>();
            if (part == null || part.transform == null)
                return result;

            CollectOwnComponents(part.transform, part, result, CollectMeshFilterAt);
            return result;
        }

        private static List<SkinnedMeshRenderer> CollectOwnSkinnedMeshRenderers(Part part)
        {
            List<SkinnedMeshRenderer> result = new List<SkinnedMeshRenderer>();
            if (part == null || part.transform == null)
                return result;

            CollectOwnComponents(part.transform, part, result, CollectSkinnedMeshRendererAt);
            return result;
        }

        private static void CollectMeshFilterAt(Transform t, List<MeshFilter> result)
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null)
                result.Add(mf);
        }

        private static void CollectSkinnedMeshRendererAt(Transform t, List<SkinnedMeshRenderer> result)
        {
            SkinnedMeshRenderer smr = t.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
                result.Add(smr);
        }

        private static void CollectOwnComponents<T>(Transform current, Part ownerPart, List<T> result, Action<Transform, List<T>> collectAt)
        {
            collectAt(current, result);

            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (child == null)
                    continue;

                // A child carrying its own Part component marks the start of a different part's
                // hierarchy (an attached part, parented here by Unity) -- do not descend into it.
                Part childPart = child.GetComponent<Part>();
                if (childPart != null && childPart != ownerPart)
                    continue;

                CollectOwnComponents(child, ownerPart, result, collectAt);
            }
        }

        private static string GetPartDisplayName(Part part)
        {
            if (part == null)
                return "Part";

            // partInfo.title is the human-readable name shown in the editor ("FL-T400 Fuel Tank").
            // It is not always populated, so fall back to the internal part name.
            if (part.partInfo != null && !string.IsNullOrEmpty(part.partInfo.title))
                return part.partInfo.title;

            return string.IsNullOrEmpty(part.name) ? "Part" : part.name;
        }

        private static void AddMeshTriangles(
            List<Triangle> output,
            Mesh mesh,
            Transform transform,
            float scale,
            Renderer renderer,
            bool hideShroudSubmeshes,
            Vector3 originOffset,
            int partIndex,
            ExportOrientation orientation)
        {
            Vector3[] vertices = mesh.vertices;
            Material[] materials = renderer != null ? renderer.sharedMaterials : null;

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (hideShroudSubmeshes && SubmeshLooksLikeShroud(materials, sub))
                    continue;

                int[] tris = mesh.GetTriangles(sub);

                for (int i = 0; i < tris.Length; i += 3)
                {
                    // Orientation is applied HERE, at the one place every vertex passes through,
                    // so STL, OBJ, 3MF, STEP and the .k3dm dump are all guaranteed to agree. Doing
                    // it per-writer instead would mean four chances to get it wrong and four
                    // chances for them to disagree with each other.
                    //
                    // Every ExportOrientation is a proper rotation (determinant +1), so triangle
                    // winding survives untouched and no normals need flipping. An axis SWAP would
                    // have determinant -1 -- a mirror -- and would quietly turn the whole model
                    // inside out while still looking correct in any viewer.
                    Vector3 a = ExportOrientationUtilities.Apply(
                        (transform.TransformPoint(vertices[tris[i]]) - originOffset) * scale, orientation);
                    Vector3 b = ExportOrientationUtilities.Apply(
                        (transform.TransformPoint(vertices[tris[i + 1]]) - originOffset) * scale, orientation);
                    Vector3 c = ExportOrientationUtilities.Apply(
                        (transform.TransformPoint(vertices[tris[i + 2]]) - originOffset) * scale, orientation);

                    output.Add(new Triangle(a, b, c, partIndex));
                }
            }
        }

        private static bool RendererLooksLikeShroud(Renderer renderer)
        {
            if (renderer == null)
                return false;

            if (NameHasShroudToken(renderer.name))
                return true;

            if (renderer.gameObject != null && NameHasShroudToken(renderer.gameObject.name))
                return true;

            Transform t = renderer.transform;
            while (t != null)
            {
                if (NameHasShroudToken(t.name))
                    return true;
                t = t.parent;
            }

            Material[] mats = renderer.sharedMaterials;
            if (mats != null)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && NameHasShroudToken(mats[i].name))
                        return true;
                }
            }

            return false;
        }

        private static bool SubmeshLooksLikeShroud(Material[] materials, int submeshIndex)
        {
            if (materials == null || submeshIndex < 0 || submeshIndex >= materials.Length)
                return false;

            Material mat = materials[submeshIndex];
            return mat != null && NameHasShroudToken(mat.name);
        }

        private static bool NameHasShroudToken(string name)
        {
            return ShroudNameTokens.NameHasShroudToken(name);
        }
    }
}
