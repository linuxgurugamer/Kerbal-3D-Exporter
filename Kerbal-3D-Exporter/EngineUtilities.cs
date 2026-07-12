using System.Collections.Generic;

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

            return GetEngineOptions(parts, defaultShowShroud);
        }

        public static List<EngineShroudOption> GetEngineOptions(List<Part> parts, bool defaultShowShroud)
        {
            List<EngineShroudOption> engines = new List<EngineShroudOption>();

            if (parts == null)
                return engines;

            foreach (Part p in parts)
            {
                if (p == null)
                    continue;

                if (!IsEnginePart(p))
                    continue;

                string title = p.partInfo != null ? p.partInfo.title : p.partName;
                engines.Add(new EngineShroudOption(p, title + "  [" + p.partName + "]", defaultShowShroud));
            }

            return engines;
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
