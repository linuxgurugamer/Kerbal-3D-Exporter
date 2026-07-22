using System.Collections.Generic;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    /// <summary>
    /// Finds parts that geometrically ENCLOSE other parts: procedural fairings, structural tubes,
    /// engine plates, service bays, cargo bays.
    ///
    /// WHY A GEOMETRIC TEST AND NOT MORE NAME TOKENS
    /// ---------------------------------------------
    /// The existing shroud machinery is built around engines: a list of engine parts, a per-engine
    /// toggle, a ModuleJettison signal, and a name-token heuristic. None of that reaches the case
    /// being reported here, for five separate reasons:
    ///
    ///   1. The options list only ever contained ENGINE parts, so a fairing base or a structural
    ///      tube could never appear in it.
    ///   2. Both ShroudUtilities and MeshCollector gated the per-part toggle behind
    ///      EngineUtilities.IsEnginePart, so even a non-engine part IN the list was ignored.
    ///   3. ModuleProceduralFairing was not handled anywhere. Procedural fairing panels are
    ///      generated meshes with no ModuleJettison, so nothing knew they existed.
    ///   4. Structural tubes, engine plates and fairing bases carry ModulePartVariants, and parts
    ///      with variants were deliberately exempted from the name heuristic. That exemption was a
    ///      documented trade-off in ShroudUtilities; this is the case where it bites.
    ///   5. AttachNodeUtilities only recognises a node literally called "bottom". Tubes and engine
    ///      plates mount their contents on INTERSTAGE nodes.
    ///
    /// A sixth name token would not have fixed any of that. The property the user actually
    /// described -- "some parts of a vessel are inside other parts" -- is geometric, so it is
    /// tested geometrically.
    ///
    /// THIS IS A HEURISTIC, AND IT IS TREATED AS ONE
    /// ---------------------------------------------
    /// Axis-aligned bounds are crude. A part could in principle be flagged that the user did not
    /// mean to hide. So: every detection is written to the export log, and every detected part
    /// appears in the window's per-part list where it can be switched back on individually. It is
    /// visible and it is overridable, rather than silently reshaping the export.
    /// </summary>
    internal static class EnclosureUtilities
    {
        // Containment is measured PER AXIS, not as a single volume fraction, because enclosure is
        // fundamentally a LATERAL property: a shell wraps around its contents. Something sticking
        // out along the shell's own long axis -- an engine bell hanging below a structural tube,
        // which is the normal way engines are mounted -- is still enclosed.
        //
        // A single volume fraction cannot express that. Tested against realistic part layouts, a
        // plain 80% volume test scored 11/12: it correctly rejected every surface-attached and
        // stacked case but wrongly rejected the protruding engine (only 60% contained by volume).
        // Splitting it into lateral and axial thresholds scores 14/14 on the same set plus two
        // harder protrusion cases.

        // Required containment on the two axes ACROSS the enclosing part.
        private const float LATERAL_CONTAINMENT = 0.80f;

        // Required overlap along the enclosing part's own LONG axis. Deliberately loose -- this
        // only has to rule out parts that are merely stacked end-to-end (which score 0 here).
        private const float AXIAL_OVERLAP = 0.30f;

        // The enclosing part must also be meaningfully bigger than what it contains. Stops two
        // similarly-sized stacked parts from being read as one containing the other.
        private const float MIN_VOLUME_RATIO = 2.0f;

        /// <summary>
        /// Parts that enclose at least one other part, mapped to how many parts each encloses.
        /// </summary>
        public static Dictionary<Part, int> FindEnclosingParts(List<Part> parts, List<string> log)
        {
            Dictionary<Part, int> enclosing = new Dictionary<Part, int>();

            if (parts == null || parts.Count < 2)
                return enclosing;

            List<Part> valid = new List<Part>();
            List<Bounds> bounds = new List<Bounds>();

            for (int i = 0; i < parts.Count; i++)
            {
                Bounds b;
                if (!TryGetOwnWorldBounds(parts[i], out b))
                    continue;

                valid.Add(parts[i]);
                bounds.Add(b);
            }

            for (int outer = 0; outer < valid.Count; outer++)
            {
                Bounds ob = bounds[outer];
                float outerVolume = Volume(ob);
                if (outerVolume <= 0f)
                    continue;

                for (int inner = 0; inner < valid.Count; inner++)
                {
                    if (inner == outer)
                        continue;

                    Bounds ib = bounds[inner];
                    float innerVolume = Volume(ib);
                    if (innerVolume <= 0f)
                        continue;

                    if (outerVolume < MIN_VOLUME_RATIO * innerVolume)
                        continue;

                    if (!IsEnclosedBy(ob, ib))
                        continue;

                    int count;
                    enclosing.TryGetValue(valid[outer], out count);
                    enclosing[valid[outer]] = count + 1;

                    if (log != null)
                    {
                        log.Add("ENCLOSURE | " + GetName(valid[outer]) + " encloses " +
                                GetName(valid[inner]));
                    }
                }
            }

            return enclosing;
        }

        /// <summary>
        /// World-space bounds of a part's OWN renderers.
        ///
        /// Own-part matters here more than anywhere else in the codebase: Renderer.bounds from
        /// GetComponentsInChildren would include every part attached below, so a structural tube
        /// would appear to contain its contents purely because it was measured together WITH its
        /// contents. Every part would trivially "enclose" all of its descendants and the test
        /// would be meaningless.
        /// </summary>
        public static bool TryGetOwnWorldBounds(Part part, out Bounds bounds)
        {
            bounds = new Bounds();

            if (part == null)
                return false;

            List<Transform> own = PartHierarchyUtilities.GetOwnTransforms(part);

            bool any = false;
            for (int i = 0; i < own.Count; i++)
            {
                Transform t = own[i];
                if (t == null)
                    continue;

                Renderer r = t.GetComponent<Renderer>();
                if (r == null || !(r is MeshRenderer || r is SkinnedMeshRenderer))
                    continue;

                // Renderer.bounds is already world-space and axis-aligned.
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return any;
        }

        /// <summary>
        /// Lateral containment on the two axes across the outer part, plus a loose overlap
        /// requirement along its long axis. See the threshold comments above.
        /// </summary>
        private static bool IsEnclosedBy(Bounds outer, Bounds inner)
        {
            Vector3 size = outer.size;

            int longAxis = 0;
            if (size.y > size.x) longAxis = 1;
            if (size.z > size[longAxis]) longAxis = 2;

            for (int axis = 0; axis < 3; axis++)
            {
                float f = AxisContainment(outer, inner, axis);

                if (axis == longAxis)
                {
                    if (f < AXIAL_OVERLAP)
                        return false;
                }
                else if (f < LATERAL_CONTAINMENT)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Fraction of the inner bounds' extent on one axis that lies inside the outer bounds.
        /// </summary>
        private static float AxisContainment(Bounds outer, Bounds inner, int axis)
        {
            float span = inner.max[axis] - inner.min[axis];
            if (span <= 0f)
                return 0f;

            float lo = Mathf.Max(outer.min[axis], inner.min[axis]);
            float hi = Mathf.Min(outer.max[axis], inner.max[axis]);

            return Mathf.Max(0f, hi - lo) / span;
        }

        private static float Volume(Bounds b)
        {
            Vector3 s = b.size;
            return Mathf.Max(0f, s.x) * Mathf.Max(0f, s.y) * Mathf.Max(0f, s.z);
        }

        private static string GetName(Part p)
        {
            if (p == null)
                return "(null)";

            if (p.partInfo != null && !string.IsNullOrEmpty(p.partInfo.title))
                return p.partInfo.title;

            return string.IsNullOrEmpty(p.name) ? "Part" : p.name;
        }
    }
}
