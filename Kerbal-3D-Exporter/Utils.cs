using System.IO;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{
    internal static class Utils
    {
        internal static string GetModDir
        {
            get
            {
                return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "Kerbal-3D-Exporter");
            }
        }
        internal static string GetDefaultOutputDirectory
        {
            get
            {
                return Path.Combine(GetModDir, "Models");
            }
        }

        internal static string GetIconPath
        {
            get
            {
                return Path.Combine("Kerbal-3D-Exporter", "PluginData", "Icons");
            }
        }
        internal static string GetSlicerConfigurationFilePath
        {
            get
            {
                return Path.Combine(GetModDir, "PluginData", "SlicerConfiguration.cfg");
            }
        }
    }
}
