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
            HashSet<Transform> shroudTransformsToSkip,
            HashSet<Part> enclosureParts)
        {
            savedStates.Clear();

            if (shroudTransformsToSkip != null)
                shroudTransformsToSkip.Clear();

            if (parts == null)
                return;

            HashSet<GameObject> savedObjects = new HashSet<GameObject>();
            HashSet<Renderer> savedRenderers = new HashSet<Renderer>();

            // ModuleJettison needs the same dedupe the GameObject and Renderer saves already
            // had. Without it a module gets saved once per ancestor part that reaches it, and
            // every save after the first captures the value the previous part's pass just
            // wrote instead of KSP's original. RestoreShroudVisibility replays the list in
            // order, so the last (already corrupted) save wins and the module is left wrong --
            // which then becomes the "original" the NEXT export saves. That is why the symptom
            // shows up as an OLD problem coming back rather than a clean failure: the damage
            // accumulates across exports.
            HashSet<ModuleJettison> savedModules = new HashSet<ModuleJettison>();
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

                bool partHasVariants = variantParts.Contains(p);

                bool visible = defaultVisible;

                // NOTE: no `isEnginePart &&` guard here any more, and that removal is the core of
                // the fix. The per-part toggle used to be honoured ONLY for parts that are engines,
                // so a structural tube, engine plate or fairing base -- the parts that actually
                // enclose things -- could never be switched off even once they appeared in the list.
                if (engineVisibility.ContainsKey(p))
                    visible = visible && engineVisibility[p];

                bool isEnclosure = enclosureParts != null && enclosureParts.Contains(p);

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
                // The bottom-node rule is a stock convention about ENGINE shrouds: KSP hides a
                // bottom-node shroud when nothing is attached below. An enclosing part with an
                // empty bottom node is just... a fairing attached by its top node, which is the
                // normal way to attach one. Folding the rule into an enclosure's visibility made
                // fairing bases at the bottom of the stack vanish even when their toggle was ON,
                // because HideEnclosingPartGeometry ran off this value while MeshCollector's
                // part-identity skip ran off the toggle -- and only one of them fired.
                if (bottomNodeEmpty && !partHasVariants && !isEnclosure)
                    visible = false;

                // Own-part only. GetComponentsInChildren would return every ModuleJettison in
                // every part attached below this one as well -- for the vessel root, that is all
                // of them -- and this part's visibility decision would then be applied to all of
                // those other parts' shrouds. See PartHierarchyUtilities.
                List<ModuleJettison> shrouds = PartHierarchyUtilities.GetOwnComponents<ModuleJettison>(p);

                // An enclosing part IS the shell, so hiding it means hiding all of its own
                // geometry -- not just meshes that happen to carry a shroud-ish name. A structural
                // tube has no "Shroud" object anywhere in it; the tube is the shroud.
                //
                // Deliberately NOT gated on partHasVariants. Tubes, plates and fairing bases almost
                // all carry ModulePartVariants, and that exemption is exactly why they could never
                // be hidden. Variant safety is preserved by OriginallyHiddenVariantTransforms,
                // which keeps non-selected variant meshes hidden regardless of what happens here.
                // Toggle-only, and computed identically to CraftPrintExporter's
                // BuildHiddenEnclosureSet on purpose: these are the two halves of one decision
                // (scene visuals here, export exclusion there), and any drift between them shows
                // up as a part that is invisible in one and present in the other.
                bool enclosureHidden = isEnclosure &&
                    !(defaultVisible && (!engineVisibility.ContainsKey(p) || engineVisibility[p]));

                if (enclosureHidden)
                {
                    HideEnclosingPartGeometry(p, savedStates, savedObjects, savedRenderers);
                }

                foreach (ModuleJettison shroud in shrouds)
                {
                    if (shroud == null)
                        continue;

                    if (savedModules.Add(shroud))
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
                // Enclosure parts are excluded from the name-token fallback entirely. Their
                // visibility is governed by the toggle (whole part in or out); the token scan on
                // top of that is at best redundant and at worst catastrophic, because stock
                // fairing parts are NAMED with the token -- 'fairingSize2' -- so the scan matched
                // the part's ROOT transform and swept everything parented under it, including
                // every downstream part, into the skip set. That is the reported bug: parts at
                // the bottom of the stack disappearing whenever a fairing sat above them.
                if (!partHasVariants && !isEnclosure)
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

            // Own-part only: jettisonName names a transform inside THIS part's own model, so
            // searching descendant parts can only produce false matches on someone else's mesh.
            List<Transform> transforms = PartHierarchyUtilities.GetOwnTransforms(part);
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
            // Own-part only. This is a name heuristic, and running it across part boundaries
            // meant a tank could force-show or force-hide a shroud belonging to an engine
            // several parts below it, depending purely on which part the loop reached last.
            List<Transform> transforms = PartHierarchyUtilities.GetOwnTransforms(part);
            foreach (Transform t in transforms)
            {
                if (t == null || t.gameObject == null)
                    continue;

                // Never treat a transform that carries a Part component -- i.e. the part's own
                // root -- as a shroud object, no matter what its name says. A part's internal
                // name describes what the part IS ('fairingSize2'), not a shroud sub-mesh, and
                // KSP parents every attached child part under that root, so sweeping it takes
                // the whole downstream stack along. Hiding an entire part is the enclosure
                // toggle's job, where it is explicit and per-part.
                if (t.GetComponent<Part>() != null)
                    continue;

                if (NameHasShroudToken(t.name))
                    SaveSetAndMaybeSkipTransformTree(t, visible, savedStates, savedObjects, savedRenderers, shroudTransformsToSkip);
            }
        }

        /// <summary>
        /// Hide an enclosing part's OWN geometry, and nothing below it.
        ///
        /// This cannot go through SaveSetAndMaybeSkipTransformTree. That helper walks the whole
        /// Unity sub-tree, which for a part root descends straight into every part attached inside
        /// -- so hiding a structural tube would also hide the engines it contains, which is the
        /// exact opposite of the point. Same trap in SaveAndSetObjectAndRenderers, whose
        /// GetComponentsInChildren&lt;Renderer&gt; would disable the contents' renderers too.
        ///
        /// So the traversal is own-part, and the renderers are taken one transform at a time.
        /// </summary>
        private static void HideEnclosingPartGeometry(
            Part part,
            List<ShroudState> savedStates,
            HashSet<GameObject> savedObjects,
            HashSet<Renderer> savedRenderers)
        {
            // IMPORTANT: nothing here goes into shroudTransformsToSkip.
            //
            // MeshCollector.TransformIsInSkipSet walks UP the parent chain, and KSP parents a
            // part's contents UNDERNEATH it. So marking a structural tube's root transform would
            // match every mesh of every part mounted inside it, and the engine we were trying to
            // reveal would vanish along with the tube. Verified in simulation: marking the tube
            // subtree dropped the engine, its bell and the tank above it, leaving only the pod.
            //
            // Export exclusion for enclosing parts is therefore done by PART identity in
            // MeshCollector (see the enclosureParts check there), which cannot be inherited by
            // anything mounted inside. This method only handles the in-scene visuals.
            List<Transform> own = PartHierarchyUtilities.GetOwnTransforms(part);

            for (int i = 0; i < own.Count; i++)
            {
                Transform t = own[i];
                if (t == null || t.gameObject == null)
                    continue;

                GameObject go = t.gameObject;

                if (savedObjects.Add(go))
                    savedStates.Add(new ShroudState(go, go.activeSelf));

                // GetComponents, not GetComponentsInChildren: children are handled by this loop
                // when they belong to this part, and must NOT be touched when they do not.
                Renderer[] renderers = go.GetComponents<Renderer>();
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer rend = renderers[r];
                    if (rend == null)
                        continue;

                    if (savedRenderers.Add(rend))
                        savedStates.Add(new ShroudState(rend, rend.enabled));

                    rend.enabled = false;
                }
            }

            // The part's own root GameObject is deliberately NOT deactivated. SetActive(false) on a
            // part root would take every part attached inside it out of the scene as well, since
            // KSP parents them underneath. Disabling the renderers achieves the same visual result
            // without touching the contents, and the skip set above is what actually keeps the
            // geometry out of the export.
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
            {
                Transform child = root.GetChild(i);
                if (child == null)
                    continue;

                // Never cross into another part. TransformIsInSkipSet matches by walking UP the
                // parent chain, so one entry high in the hierarchy silently claims every mesh of
                // every part parented below it. The callers are supposed to only hand this shroud
                // sub-objects, but 'supposed to' already failed once (the fairing part root), so
                // the boundary is enforced here as well.
                if (child.GetComponent<Part>() != null)
                    continue;

                AddTransformTreeToSkipSet(child, skipSet);
            }
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

            // An earlier comment here asserted that "a shroud GameObject does not have parts
            // attached inside it" and used GetComponentsInChildren on that basis. That assumption
            // is exactly what failed: when the matched object IS a part root (stock fairing parts
            // are named 'fairingSize2', so the token scan matched them), the sub-tree contains
            // every downstream part, and this call disabled all of their renderers. The walk is
            // now part-boundary aware regardless of what the callers hand it.
            CollectOwnRenderers(go.transform, renderersScratch);
            for (int i = 0; i < renderersScratch.Count; i++)
            {
                Renderer r = renderersScratch[i];
                if (r == null)
                    continue;

                if (!savedRenderers.Contains(r))
                {
                    savedRenderers.Add(r);
                    savedStates.Add(new ShroudState(r, r.enabled));
                }

                r.enabled = visible;
            }
            renderersScratch.Clear();

            // SetActive(false) on a GameObject that carries a Part deactivates every part
            // parented beneath it in one call. Renderer disabling above already achieves the
            // visual result for the part itself, so the activation state of a part root is left
            // alone unconditionally.
            if (go.GetComponent<Part>() == null)
                go.SetActive(visible);
        }

        private static readonly List<Renderer> renderersScratch = new List<Renderer>();

        /// <summary>
        /// Renderers of this transform and its descendants, stopping at any child that carries a
        /// Part component. The boundary-aware replacement for GetComponentsInChildren.
        /// </summary>
        private static void CollectOwnRenderers(Transform t, List<Renderer> into)
        {
            if (t == null)
                return;

            Renderer[] own = t.GetComponents<Renderer>();
            for (int i = 0; i < own.Length; i++)
            {
                if (own[i] != null)
                    into.Add(own[i]);
            }

            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (child == null)
                    continue;

                if (child.GetComponent<Part>() != null)
                    continue;

                CollectOwnRenderers(child, into);
            }
        }
    }
}
