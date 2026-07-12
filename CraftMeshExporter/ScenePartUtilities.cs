using System.Collections.Generic;

namespace CraftMeshExporter
{
    internal static class ScenePartUtilities
    {
        public static bool TryGetCurrentParts(out List<Part> parts, out string sourceName, out string sceneDescription)
        {
            parts = null;
            sourceName = "UnnamedCraft";
            sceneDescription = "Unknown";

            if (HighLogic.LoadedSceneIsEditor)
            {
                sceneDescription = "Editor craft";

                if (EditorLogic.fetch == null || EditorLogic.fetch.ship == null)
                    return false;

                parts = EditorLogic.fetch.ship.parts;
                sourceName = EditorLogic.fetch.ship.shipName;
                return parts != null && parts.Count > 0;
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                sceneDescription = "Flight active vessel current state";

                Vessel vessel = FlightGlobals.ActiveVessel;
                if (vessel == null)
                    return false;

                parts = vessel.parts;
                sourceName = vessel.vesselName;
                return parts != null && parts.Count > 0;
            }

            return false;
        }
    }
}
