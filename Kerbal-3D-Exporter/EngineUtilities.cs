using System.Collections.Generic;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    internal static class EngineUtilities
    {
        public static List<EngineShroudOption> GetEngineOptions(bool defaultShowShroud)
        {
            List<Part> parts;
            string sourceName;
            string sceneDescription;
            if (!ScenePartUtilities.TryGetCurrentParts(out parts, out sourceName, out sceneDescription))
                return new List<EngineShroudOption>();

            // Compute enclosures here too, so the window's list shows structural tubes and engine
            // plates immediately rather than only after the first export. This is an O(n^2) bounds
            // comparison over part count, and it runs on window open / refresh / export completion,
            // never per frame.
            Dictionary<Part, int> enclosing = EnclosureUtilities.FindEnclosingParts(parts, null);

            return GetShroudBearingOptions(parts, enclosing, defaultShowShroud);
        }

        public static List<EngineShroudOption> GetEngineOptions(List<Part> parts, bool defaultShowShroud)
        {
            return GetShroudBearingOptions(parts, null, defaultShowShroud);
        }

        /// <summary>
        /// Every part with geometry that can get in the way of the export: engines, parts carrying
        /// a jettisonable shroud, procedural fairings, and parts that geometrically enclose other
        /// parts.
        ///
        /// This list used to contain ENGINES ONLY, which is the root of the reported problem. A
        /// payload sealed inside a procedural fairing, or an engine mounted up inside a structural
        /// tube, is hidden by a shell belonging to a part that is not an engine -- so that shell
        /// could never be listed, and therefore never switched off.
        ///
        /// <paramref name="enclosingParts"/> comes from EnclosureUtilities and may be null, in
        /// which case only the module-based signals are used.
        /// </summary>
        public static List<EngineShroudOption> GetShroudBearingOptions(
            List<Part> parts,
            Dictionary<Part, int> enclosingParts,
            bool defaultShowShroud)
        {
            List<EngineShroudOption> options = new List<EngineShroudOption>();

            if (parts == null)
                return options;

            foreach (Part p in parts)
            {
                if (p == null)
                    continue;

                bool isEngine = IsEnginePart(p);
                bool hasJettison = PartHierarchyUtilities.GetOwnComponents<ModuleJettison>(p).Count > 0;
                bool hasFairing = HasProceduralFairing(p);

                int enclosedCount = 0;
                if (enclosingParts != null)
                    enclosingParts.TryGetValue(p, out enclosedCount);

                bool isEnclosure = hasFairing || enclosedCount > 0;

                if (!isEngine && !hasJettison && !isEnclosure)
                    continue;

                string title = p.partInfo != null ? p.partInfo.title : p.partName;
                string label = title + "  [" + p.partName + "]";

                if (isEnclosure)
                {
                    label += hasFairing
                        ? "  (fairing)"
                        : "  (encloses " + enclosedCount + ")";
                }

                options.Add(new EngineShroudOption(p, label, defaultShowShroud, isEnclosure));
            }

            return options;
        }

        /// <summary>
        /// Procedural fairings generate their panels at runtime and carry no ModuleJettison, so
        /// nothing in the exporter previously knew they existed. Matched by type name for the same
        /// reason VariantSnapshotUtilities does: it avoids a hard compile-time dependency on a
        /// module that not every KSP install ships.
        /// </summary>
        public static bool HasProceduralFairing(Part p)
        {
            if (p == null || p.Modules == null)
                return false;

            for (int i = 0; i < p.Modules.Count; i++)
            {
                PartModule m = p.Modules[i];
                if (m == null)
                    continue;

                string n = m.GetType().Name;
                if (n == "ModuleProceduralFairing" || n == "ModuleCargoBay" || n == "ModuleServiceModule")
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Rebuild the option list from the parts that are ACTUALLY on the craft right now,
        /// carrying the user's existing per-engine choices across.
        ///
        /// The window only rebuilds its list when it opens, when an export finishes, or when the
        /// refresh button is pressed. A user who leaves the window open while they keep building
        /// -- which is the normal way to work, and much more likely on a big craft -- can easily
        /// have added or removed engines since. The export then ran against a list that was
        /// missing those engines entirely, so their shrouds silently fell back to the global
        /// default no matter what the window appeared to show.
        ///
        /// Matching is by Part reference, not by name: a craft with six identical Swivels has six
        /// options with identical display names, and name matching would collapse or cross-wire
        /// them.
        /// </summary>
        public static List<EngineShroudOption> ReconcileEngineOptions(
            List<Part> parts,
            List<EngineShroudOption> existing,
            bool defaultShowShroud,
            Dictionary<Part, int> enclosingParts)
        {
            List<EngineShroudOption> current = GetShroudBearingOptions(parts, enclosingParts, defaultShowShroud);

            if (existing == null || existing.Count == 0)
                return current;

            Dictionary<Part, bool> previousChoice = new Dictionary<Part, bool>();
            for (int i = 0; i < existing.Count; i++)
            {
                EngineShroudOption option = existing[i];
                if (option == null || option.Part == null)
                    continue;

                previousChoice[option.Part] = option.ShowShroud;
            }

            for (int i = 0; i < current.Count; i++)
            {
                EngineShroudOption option = current[i];
                if (option == null || option.Part == null)
                    continue;

                bool choice;
                if (previousChoice.TryGetValue(option.Part, out choice))
                    option.ShowShroud = choice;
            }

            return current;
        }

        public static bool IsEnginePart(Part p)
        {
            if (p == null)
                return false;

            return p.FindModuleImplementing<ModuleEngines>() != null ||
                   p.FindModuleImplementing<ModuleEnginesFX>() != null;
        }
    }
}
