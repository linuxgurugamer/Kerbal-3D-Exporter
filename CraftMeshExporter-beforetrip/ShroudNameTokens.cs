namespace CraftMeshExporter
{
    // Single shared source of truth for "does this name look like a shroud/fairing" heuristics.
    // MeshCollector and ShroudUtilities must agree on this list, or a name that ShroudUtilities
    // treats as a shroud (and force-shows/force-hides) can slip past MeshCollector's own shroud
    // check, or vice versa. Keep this list conservative: every token here can force-toggle the
    // visibility of ANY matching transform in a part, so overly generic tokens (e.g. bare "mount")
    // will affect unrelated meshes.
    internal static class ShroudNameTokens
    {
        public static readonly string[] Tokens =
        {
            "shroud", "fairing", "fairng", "jettison", "interstage",
            "boattail", "boat_tail", "boat-tail", "enginefairing", "engine_fairing",
            "mountfairing", "mount_fairing", "fairingbase", "fairing_base",
            "fairingcollider", "fairing_collider", "engineplate", "engine_plate",
            "engine-plate", "adapterfairing", "adapter_fairing", "nodefairing",
            "node_fairing", "bottomfairing", "bottom_fairing"
        };

        public static bool NameHasShroudToken(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            string n = name.ToLowerInvariant();
            for (int i = 0; i < Tokens.Length; i++)
            {
                if (n.Contains(Tokens[i]))
                    return true;
            }

            return false;
        }
    }
}
