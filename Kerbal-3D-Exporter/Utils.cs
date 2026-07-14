using SpaceTuxUtility;
using System.IO;
using UnityEngine;

namespace Kerbal_3D_Exporter
{

    public static class Utils
    {
        public static GUIStyle solidWindowStyle;
        public static GUIStyle whiteFontStyle;
        public static GUIStyle labelRichTextStyle;
        public static GUIStyle labelRichTextFont11Style;

        public static GUIStyle labelRedBoldStyle, labelYellowBoldStyle;

        public static GUIStyle rightAlignedLabel;

        public static GUIStyle darkBoxStyle;
        public static GUIStyle darkBoxTextStyle;
        public static Texture2D darkBoxTexture;

        public static Texture2D windowBackgroundTexture;
        public static Texture2D paneBackgroundTexture;

        public static int ExportWinID, ConfigWinID;
        public static void InitStyles()
        {
            windowBackgroundTexture = MakeTexture(new Color(0.35f, 0.35f, 0.35f, 1f));
            paneBackgroundTexture = MakeTexture(new Color(0.22f, 0.22f, 0.22f, 1f));

            GUIStyle baseWindowStyle = HighLogic.Skin.window;


            solidWindowStyle = new GUIStyle(baseWindowStyle);
            solidWindowStyle.normal.background =
                solidWindowStyle.onNormal.background =
                solidWindowStyle.active.background =
                solidWindowStyle.focused.background =
                solidWindowStyle.onActive.background =
                solidWindowStyle.onFocused.background = windowBackgroundTexture;

            whiteFontStyle = new GUIStyle(GUI.skin.label);
            whiteFontStyle.normal.textColor =
                whiteFontStyle.hover.textColor =
                whiteFontStyle.active.textColor =
                whiteFontStyle.focused.textColor =
                whiteFontStyle.onNormal.textColor =
                whiteFontStyle.onHover.textColor =
                whiteFontStyle.onActive.textColor =
                whiteFontStyle.onFocused.textColor = Color.white;

            labelRedBoldStyle = new GUIStyle(GUI.skin.label);
            labelRedBoldStyle.normal.textColor =
                labelRedBoldStyle.hover.textColor =
                labelRedBoldStyle.active.textColor =
                labelRedBoldStyle.focused.textColor =
                labelRedBoldStyle.onNormal.textColor =
                labelRedBoldStyle.onHover.textColor =
                labelRedBoldStyle.onActive.textColor =
                labelRedBoldStyle.onFocused.textColor = Color.red;
            labelRedBoldStyle.fontStyle = FontStyle.Bold;


            labelYellowBoldStyle = new GUIStyle(GUI.skin.label);
            labelYellowBoldStyle.normal.textColor =
                labelYellowBoldStyle.hover.textColor =
                labelYellowBoldStyle.active.textColor =
                labelYellowBoldStyle.focused.textColor =
                labelYellowBoldStyle.onNormal.textColor =
                labelYellowBoldStyle.onHover.textColor =
                labelYellowBoldStyle.onActive.textColor =
                labelYellowBoldStyle.onFocused.textColor = Color.yellow;
            labelYellowBoldStyle.fontStyle = FontStyle.Bold;



            labelRichTextStyle = new GUIStyle(GUI.skin.label) { richText = true };
            labelRichTextFont11Style = new GUIStyle(labelRichTextStyle) { fontSize = 11 };

            darkBoxTexture = new Texture2D(1, 1);
            darkBoxTexture.SetPixel(0, 0, new Color32(30, 30, 30, 255));
            darkBoxTexture.Apply();

            darkBoxStyle = new GUIStyle(GUI.skin.box);
            darkBoxStyle.normal.background = darkBoxTexture;
            darkBoxStyle.padding = new RectOffset(10, 10, 10, 10);

            darkBoxTextStyle = new GUIStyle(GUI.skin.label);
            darkBoxTextStyle.normal.textColor = Color.white;

            rightAlignedLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight
            };

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

        public enum LengthUnit { Meters, Inches, Millimeters, Centimeters }

        public static readonly string[] LengthUnitAbbreviations = { "m", "in", "mm", "cm" }; // The order here must match the order in the enum above

        public static double ConvertMeters(double meters, LengthUnit unit)
        {
            switch (unit)
            {
                case LengthUnit.Inches:
                    return meters * 39.3700787401575;

                case LengthUnit.Millimeters:
                    return meters * 1000.0;

                case LengthUnit.Centimeters:
                    return meters * 100.0;

                case LengthUnit.Meters:
                default:
                    return meters;
            }
        }
    }
}
