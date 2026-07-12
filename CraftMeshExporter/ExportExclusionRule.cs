using System;

namespace CraftMeshExporter
{

#if EXPORT_EXCLUSION_RULE_DEFINED
    internal sealed class ExportExclusionRule
    {
        public string PartName;
        public string Token;

        public bool MatchesPart(string partName)
        {
            if (string.IsNullOrEmpty(PartName) || PartName == "*")
                return true;

            return string.Equals(PartName, partName, StringComparison.OrdinalIgnoreCase);
        }
    }
#endif
}
