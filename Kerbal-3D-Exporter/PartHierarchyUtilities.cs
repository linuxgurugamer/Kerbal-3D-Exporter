using System.Collections.Generic;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    /// <summary>
    /// Traversal that stops at part boundaries.
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// KSP parents child parts UNDER their parent part's transform. So
    /// part.GetComponentsInChildren&lt;T&gt;(true) does not return "this part's things" -- it
    /// returns this part's things PLUS everything belonging to every part attached below it.
    /// For a vessel root, that is the entire vessel.
    ///
    /// MeshCollector has always known this and walks the hierarchy manually, stopping at any
    /// child that carries its own Part component. ShroudUtilities did not: it used
    /// GetComponentsInChildren, so every ancestor part re-processed every descendant part's
    /// shroud modules and shroud transforms.
    ///
    /// Two things went wrong as a result, and both get worse the more parts and engines a
    /// vessel has, because both scale with how deep the part stack is:
    ///
    ///   1. CROSS-CONTAMINATION. Part P's shroud decision was applied to part Q's geometry.
    ///      Since the visibility write is unconditional, whichever part happened to be
    ///      processed last won. A fuel tank in the middle of the stack could re-show an engine
    ///      shroud three parts below it that the user had explicitly switched off.
    ///
    ///   2. STATE CORRUPTION ACROSS EXPORTS. A ModuleJettison at stack depth d was saved d+1
    ///      times, and each save after the first captured the value the PREVIOUS part's pass
    ///      had just written rather than the original. RestoreShroudVisibility replays the
    ///      list in order, so the last (corrupted) save won and the module was left in the
    ///      wrong state. The next export then started from that wrong baseline. Measured on a
    ///      simulated 8-stage stack: the deepest engine's module was saved 24 times and every
    ///      ModuleJettison in the vessel was left disabled after a single export.
    ///
    /// Both callers now share this walker, so they cannot drift apart again.
    /// </summary>
    internal static class PartHierarchyUtilities
    {
        /// <summary>
        /// Every transform belonging to <paramref name="part"/> itself, stopping at any child
        /// that begins a different part's hierarchy.
        ///
        /// This is the part-boundary-respecting replacement for
        /// part.GetComponentsInChildren&lt;Transform&gt;(true).
        /// </summary>
        public static List<Transform> GetOwnTransforms(Part part)
        {
            List<Transform> result = new List<Transform>();

            if (part == null || part.transform == null)
                return result;

            CollectOwnTransforms(part.transform, part, result);
            return result;
        }

        /// <summary>
        /// Components of type T that belong to <paramref name="part"/> itself, not to parts
        /// attached below it.
        /// </summary>
        public static List<T> GetOwnComponents<T>(Part part) where T : Component
        {
            List<T> result = new List<T>();

            if (part == null || part.transform == null)
                return result;

            List<Transform> transforms = GetOwnTransforms(part);
            for (int i = 0; i < transforms.Count; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                    continue;

                T[] found = t.GetComponents<T>();
                if (found == null)
                    continue;

                for (int j = 0; j < found.Length; j++)
                {
                    if (found[j] != null)
                        result.Add(found[j]);
                }
            }

            return result;
        }

        private static void CollectOwnTransforms(Transform current, Part ownerPart, List<Transform> result)
        {
            if (current == null)
                return;

            result.Add(current);

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

                CollectOwnTransforms(child, ownerPart, result);
            }
        }
    }
}
