using System.IO;
using UnityEngine;
using SpaceTuxUtility;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{

    internal static class Utils
    {
        public static GUIStyle solidWindowStyle;
        public static Texture2D windowBackgroundTexture;
        public static Texture2D paneBackgroundTexture;

        public static int ExportWinID, ConfigWinID;
        public static void InitStyles()
        {
            windowBackgroundTexture = MakeTexture(new Color(0.35f, 0.35f, 0.35f, 1f));
            paneBackgroundTexture = MakeTexture(new Color(0.22f, 0.22f, 0.22f, 1f));

            GUIStyle baseWindowStyle = HighLogic.Skin.window;

            solidWindowStyle = new GUIStyle(baseWindowStyle);
            solidWindowStyle.normal.background = windowBackgroundTexture;
            solidWindowStyle.onNormal.background = windowBackgroundTexture;
            solidWindowStyle.active.background = windowBackgroundTexture;
            solidWindowStyle.focused.background = windowBackgroundTexture;
            solidWindowStyle.onActive.background = windowBackgroundTexture;
            solidWindowStyle.onFocused.background = windowBackgroundTexture;

            ExportWinID = WindowHelper.NextWindowId("ExportWinID");
            ConfigWinID = WindowHelper.NextWindowId("ConfigWinID");
        }
        private static Texture2D MakeTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

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
