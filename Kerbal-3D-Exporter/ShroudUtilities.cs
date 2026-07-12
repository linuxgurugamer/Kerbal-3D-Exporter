using System.Collections.Generic;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    internal static class ShroudUtilities
    {
        public static void SetShroudVisibility(
            List<Part> parts,
            bool defaultVisible,
            List<EngineShroudOption> engineOptions,
            List<ShroudState> savedStates,
            HashSet<Transform> shroudTransformsToSkip)
        {
            savedStates.Clear();

            if (shroudTransformsToSkip != null)
                shroudTransformsToSkip.Clear();

            if (parts == null)
                return;

            HashSet<GameObject> savedObjects = new HashSet<GameObject>();
            HashSet<Renderer> savedRenderers = new HashSet<Renderer>();
            Dictionary<Part, bool> engineVisibility = BuildEngineVisibilityMap(engineOptions);

            // Parts with a ModulePartVariants module manage their own mesh visibility (which
            // GameObjects are on/off) through the currently selected variant -- that's a completely
            // separate mechanism from the classic ModuleJettison/bottom-attach-node shroud
            // conventions below. A mesh the active variant enables (even one literally named
            // "Shroud") is not necessarily the kind of stock engine shroud those heuristics were
            // built for, so parts with variants are exempted from them (the ModuleJettison-confirmed
            // path further down still applies normally either way).
            HashSet<Part> variantParts = VariantSnapshotUtilities.BuildPartsWithVariantModules(parts);

            foreach (Part p in parts)
            {
                if (p == null)
                    continue;

                bool isEnginePart = EngineUtilities.IsEnginePart(p);
                bool partHasVariants = variantParts.Contains(p);

                bool visible = defaultVisible;

                if (isEnginePart && engineVisibility.ContainsKey(p))
                    visible = visible && engineVisibility[p];

                bool bottomNodeEmpty = AttachNodeUtilities.BottomAttachNodeIsEmpty(p);

                // KSP hides stock-style bottom-node engine shrouds when nothing is attached below.
                // That convention doesn't apply to variant-managed meshes -- the active variant is
                // already the source of truth for those, regardless of what's attached. (An earlier
                // attempt to split this by whether the bottom node was empty didn't work: that signal
                // doesn't reliably tell a real stand-alone shroud apart from a variant-managed mesh
                // that happens to share a name token, and empty-bottom-node is exactly the common case
                // for a bottom-stack engine, which is also exactly the kind of part likely to have
                // variants. So this is a full, unconditional exemption instead -- see the matching
                // unconditional snapshot in VariantSnapshotUtilities for the other half of this.)
                if (bottomNodeEmpty && !partHasVariants)
                    visible = false;

                ModuleJettison[] shrouds = p.GetComponentsInChildren<ModuleJettison>(true);

                foreach (ModuleJettison shroud in shrouds)
                {
                    if (shroud == null)
                        continue;

                    savedStates.Add(new ShroudState(shroud, shroud.enabled));
                    shroud.enabled = visible;

                    HideOrShowNamedShroudObject(
                        p,
                        shroud.jettisonName,
                        visible,
                        savedStates,
                        savedObjects,
                        savedRenderers,
                        shroudTransformsToSkip);
                }

                // Fallback for engines whose shrouds/fairings are not controlled by ModuleJettison,
                // or where the mesh object name does not exactly match jettisonName. This is a name
                // heuristic (see ShroudNameTokens) with no way to tell a real stand-alone shroud
                // object apart from a variant-managed mesh that just happens to share a token in its
                // name -- so it's skipped entirely for parts with variants, full stop. (Trade-off:
                // a shroud only detectable via this fallback, on a part that also has variants, can
                // no longer be forced hidden by the global toggle -- it just follows whatever the
                // active variant already set. Given the alternative was variant geometry flickering
                // on/off during every export, this is the safer default.)
                if (!partHasVariants)
                {
                    HideOrShowLikelyShroudObjects(
                        p,
                        visible,
                        savedStates,
                        savedObjects,
                        savedRenderers,
                        shroudTransformsToSkip);
                }
            }
        }

        public static void RestoreShroudVisibility(List<ShroudState> savedStates)
        {
            if (savedStates == null)
                return;

            foreach (ShroudState state in savedStates)
            {
                if (state.Module != null)
                    state.Module.enabled = state.ModuleEnabled;

                if (state.Renderer != null)
                    state.Renderer.enabled = state.RendererEnabled;

                if (state.GameObject != null)
                    state.GameObject.SetActive(state.GameObjectActive);
            }
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

        private static void HideOrShowNamedShroudObject(
            Part part,
            string objectName,
            bool visible,
            List<ShroudState> savedStates,
            HashSet<GameObject> savedObjects,
            HashSet<Renderer> savedRenderers,
            HashSet<Transform> shroudTransformsToSkip)
        {
            if (part == null || string.IsNullOrEmpty(objectName))
                return;

            Transform[] transforms = part.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in transforms)
            {
                if (t == null || t.gameObject == null)
                    continue;

                if (NameMatches(t.name, objectName))
                    SaveSetAndMaybeSkipTransformTree(t, visible, savedStates, savedObjects, savedRenderers, shroudTransformsToSkip);
            }
        }

        private static void HideOrShowLikelyShroudObjects(
            Part part,
            bool visible,
            List<ShroudState> savedStates,
            HashSet<GameObject> savedObjects,
            HashSet<Renderer> savedRenderers,
            HashSet<Transform> shroudTransformsToSkip)
        {
            Transform[] transforms = part.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in transforms)
            {
                if (t == null || t.gameObject == null)
                    continue;

                if (NameHasShroudToken(t.name))
                    SaveSetAndMaybeSkipTransformTree(t, visible, savedStates, savedObjects, savedRenderers, shroudTransformsToSkip);
            }
        }

        private static void SaveSetAndMaybeSkipTransformTree(
            Transform root,
            bool visible,
            List<ShroudState> savedStates,
            HashSet<GameObject> savedObjects,
            HashSet<Renderer> savedRenderers,
            HashSet<Transform> shroudTransformsToSkip)
        {
            if (root == null)
                return;

            if (!visible && shroudTransformsToSkip != null)
                AddTransformTreeToSkipSet(root, shroudTransformsToSkip);

            SaveAndSetObjectAndRenderers(root.gameObject, visible, savedStates, savedObjects, savedRenderers);
        }

        private static void AddTransformTreeToSkipSet(Transform root, HashSet<Transform> skipSet)
        {
            if (root == null || skipSet == null)
                return;

            skipSet.Add(root);

            for (int i = 0; i < root.childCount; i++)
                AddTransformTreeToSkipSet(root.GetChild(i), skipSet);
        }

        private static bool NameMatches(string actual, string wanted)
        {
            if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(wanted))
                return false;

            string a = actual.ToLowerInvariant();
            string w = wanted.ToLowerInvariant();

            return a == w || a.Contains(w) || w.Contains(a);
        }

        private static bool NameHasShroudToken(string name)
        {
            return Kerbal_3D_Exporter.ShroudNameTokens.NameHasShroudToken(name);
        }

        private static void SaveAndSetObjectAndRenderers(
            GameObject go,
            bool visible,
            List<ShroudState> savedStates,
            HashSet<GameObject> savedObjects,
            HashSet<Renderer> savedRenderers)
        {
            if (go == null)
                return;

            if (!savedObjects.Contains(go))
            {
                savedObjects.Add(go);
                savedStates.Add(new ShroudState(go, go.activeSelf));
            }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null)
                    continue;

                if (!savedRenderers.Contains(r))
                {
                    savedRenderers.Add(r);
                    savedStates.Add(new ShroudState(r, r.enabled));
                }

                r.enabled = visible;
            }

            go.SetActive(visible);
        }
    }
}
