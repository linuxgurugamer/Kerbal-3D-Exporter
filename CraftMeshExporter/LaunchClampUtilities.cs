using System;

namespace CraftMeshExporter
{
    // Uses several independent signals rather than relying on a single one, since a launch clamp
    // report showed the direct FindModuleImplementing<LaunchClamp>() check alone wasn't reliably
    // matching real stock launch clamp parts. Any of these being true is treated as a launch clamp:
    //
    //   1. part.Modules.Contains("LaunchClamp") -- KSP's own name-string-based module lookup. This
    //      matches however the part's cfg actually declared its MODULE { name = ... } entry, which
    //      is the same thing KSP itself uses internally, so it's the most direct signal available.
    //   2. Any module's exact type name (via reflection, no field-guessing involved) equals
    //      "LaunchClamp" -- catches cases where module identity might differ from the name string
    //      for some reason, without assuming a specific compiled type reference is the right one.
    //   3. The direct compiled-type check, kept as a third signal in case the above two somehow miss
    //      a case they would have caught.
    internal static class LaunchClampUtilities
    {
        public static bool IsLaunchClampPart(Part p)
        {
            if (p == null)
                return false;

            if (p.Modules != null && p.Modules.Contains("LaunchClamp"))
                return true;

            if (p.Modules != null)
            {
                for (int i = 0; i < p.Modules.Count; i++)
                {
                    PartModule module = p.Modules[i];
                    if (module == null)
                        continue;

                    Type moduleType = module.GetType();
                    if (moduleType != null && string.Equals(moduleType.Name, "LaunchClamp", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (p.FindModuleImplementing<LaunchClamp>() != null)
                return true;

            return false;
        }
    }
}
