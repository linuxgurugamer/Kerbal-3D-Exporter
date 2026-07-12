using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    // ActiveVariantUtilities tries to figure out which meshes belong to non-selected
    // ModulePartVariants variants by reflecting into KSP's PartVariant/GameObjectVariant
    // internals. That's fragile: it has to guess exact field/property names and casing, and
    // getting it wrong (silently) means every variant's meshes get exported.
    //
    // This class sidesteps that guessing game entirely. KSP itself already applies the selected
    // variant -- enabling/disabling the relevant GameObjects and renderers -- as part of normal
    // part setup, before our exporter ever touches the scene. So instead of parsing KSP's variant
    // config, we just snapshot which mesh objects are ALREADY inactive/disabled at the very start
    // of the export, before ShroudUtilities (or anything else) mutates visibility. Any mesh that's
    // hidden at that point, on a part we know has a variants module, is treated as "belongs to a
    // non-selected variant" and is locked out of the export -- regardless of whether any later
    // pass in the pipeline flips its active state back on.
    //
    // Shroud/fairing-named objects get NO special exception here (see SnapshotOriginallyHiddenTransforms
    // for why): ShroudUtilities unconditionally hands off every mesh on a part with variants to this
    // snapshot, regardless of attach-node state, so this has to be the one source of truth for all of
    // them -- shroud-named or not -- or a shroud-named mesh belonging to a non-selected variant would
    // never actually get hidden.
    internal static class VariantSnapshotUtilities
    {
        public static HashSet<Part> BuildPartsWithVariantModules(List<Part> parts)
        {
            HashSet<Part> result = new HashSet<Part>();

            if (parts == null)
                return result;

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
                    if (moduleType != null && moduleType.Name == "ModulePartVariants")
                    {
                        result.Add(part);
                        break;
                    }
                }
            }

            return result;
        }

        // IMPORTANT: call this before any shroud/visibility mutation runs (i.e. before
        // ShroudUtilities.SetShroudVisibility), so the snapshot reflects KSP's own state rather
        // than anything this exporter has already changed.
        //
        // No shroud-name exception here, deliberately: ShroudUtilities unconditionally exempts
        // every part with a ModulePartVariants module from its shroud heuristics (see the comment
        // there), regardless of attach-node state. That means THIS snapshot is the only thing that
        // ever decides whether a variant-managed mesh -- shroud-named or not -- gets excluded, so it
        // has to capture all of them the same way, with no exceptions, or a shroud-named mesh
        // belonging to a non-selected variant would never get hidden.
        public static HashSet<Transform> SnapshotOriginallyHiddenTransforms(
            List<Part> parts,
            HashSet<Part> variantParts,
            Action<string> status)
        {
            HashSet<Transform> hidden = new HashSet<Transform>();

            if (parts == null || variantParts == null || variantParts.Count == 0)
                return hidden;

            int hiddenCount = 0;

            for (int i = 0; i < parts.Count; i++)
            {
                Part part = parts[i];
                if (part == null || !variantParts.Contains(part))
                    continue;

                MeshFilter[] filters = part.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter mf in filters)
                {
                    if (mf == null || mf.transform == null)
                        continue;

                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    bool isHidden = !mf.gameObject.activeInHierarchy || (mr != null && !mr.enabled);
                    if (isHidden && hidden.Add(mf.transform))
                        hiddenCount++;
                }

                SkinnedMeshRenderer[] skins = part.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (SkinnedMeshRenderer smr in skins)
                {
                    if (smr == null || smr.transform == null)
                        continue;

                    bool isHidden = !smr.gameObject.activeInHierarchy || !smr.enabled;
                    if (isHidden && hidden.Add(smr.transform))
                        hiddenCount++;
                }
            }

            if (status != null)
            {
                status("Variant snapshot: " + variantParts.Count + " part(s) with variant modules, " +
                    hiddenCount + " originally-hidden variant mesh transform(s) locked out of export.");
            }

            return hidden;
        }
    }
}
