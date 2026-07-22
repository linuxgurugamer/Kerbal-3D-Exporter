using System;
using System.IO;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{
    public class SlicerConfiguration
    {
        private const string CONFIG_NODE = "SlicerConfiguration";

        private static readonly string ConfigFile = Utils.GetSlicerConfigurationFilePath;

        public string SelectedSlicerName { get; set; } = "";
        public string SelectedSlicerPath { get; set; } = "";

        public string SelectedSlicerStartMenuSearch { get; set; } = "";
        public bool UseStartMenuSearchForSlicer { get; set; }

        public bool DebugMode { get; set; }

        public string OutputDirectory { get; set; } = "";

        public Utils.LengthUnit Units { get; set; } = Utils.LengthUnit.Inches;

        public string ScaleText { get; set; } = "0.01";


        /// <summary>
        /// Save the configuration to PluginData.
        /// </summary>
        public void SaveConfiguration()
        {
            Log.Info("Saving slicer configuration");

            ConfigNode root = new ConfigNode();
            ConfigNode node = root.AddNode(CONFIG_NODE);

            node.AddValue(nameof(SelectedSlicerName), SelectedSlicerName);
            node.AddValue(nameof(SelectedSlicerPath), SelectedSlicerPath);
            node.AddValue(nameof(SelectedSlicerStartMenuSearch), SelectedSlicerStartMenuSearch);
            node.AddValue(nameof(UseStartMenuSearchForSlicer), UseStartMenuSearchForSlicer);
            node.AddValue(nameof(DebugMode), DebugMode);
            node.AddValue(nameof(OutputDirectory), OutputDirectory);

            node.AddValue(nameof(Units), (int)Units);
            node.AddValue(nameof(ScaleText), ScaleText);

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile));

            root.Save(ConfigFile);
        }

        /// <summary>
        /// Load the configuration from PluginData.
        /// </summary>
        public void LoadConfiguration()
        {
            Log.Info("Loading slicer configuration");

            if (!File.Exists(ConfigFile))
            {
                OutputDirectory = Utils.GetDefaultOutputDirectory;
                return;
            }

            ConfigNode root = ConfigNode.Load(ConfigFile);

            if (root == null)
            {
                OutputDirectory = Utils.GetDefaultOutputDirectory;
                return;
            }

            ConfigNode node = root.GetNode(CONFIG_NODE);

            if (node == null)
            {
                OutputDirectory = Utils.GetDefaultOutputDirectory;
                return;
            }

            SelectedSlicerName =
                node.GetValue(nameof(SelectedSlicerName)) ?? "";

            SelectedSlicerPath =
                node.GetValue(nameof(SelectedSlicerPath)) ?? "";

            SelectedSlicerStartMenuSearch =
                node.GetValue(nameof(SelectedSlicerStartMenuSearch)) ?? "";

            bool.TryParse(node.GetValue(nameof(UseStartMenuSearchForSlicer)), out bool useSearch);
            UseStartMenuSearchForSlicer = useSearch;

            bool.TryParse(node.GetValue(nameof(DebugMode)), out bool debugMode);
            DebugMode = debugMode;

            OutputDirectory =
                node.GetValue(nameof(OutputDirectory)) ?? "";

            if (string.IsNullOrEmpty(OutputDirectory))
                OutputDirectory = Utils.GetDefaultOutputDirectory;

            ScaleText = node.GetValue(nameof(ScaleText)) ?? "0.01";

            string value = node.GetValue(nameof(Units));
            try
            {
                Units = (Utils.LengthUnit)Enum.Parse(typeof(Utils.LengthUnit), value, true);
            }
            catch
            {
                Log.Warn(
                    "Invalid value '" + value +
                    "' for Units. Using " + Utils.LengthUnit.Inches);

                Units = Utils.LengthUnit.Inches;
            }
        }

        public bool IsConfigured()
        {
            return
                (!string.IsNullOrEmpty(SelectedSlicerName) &&
                 !string.IsNullOrEmpty(SelectedSlicerPath))
                ||
                (UseStartMenuSearchForSlicer &&
                 !string.IsNullOrEmpty(SelectedSlicerStartMenuSearch));
        }

        /// <summary>
        /// Reset to defaults and remove the config file.
        /// </summary>
        public void Clear()
        {
            SelectedSlicerName = "";
            SelectedSlicerPath = "";
            SelectedSlicerStartMenuSearch = "";
            UseStartMenuSearchForSlicer = false;
            DebugMode = false;

            OutputDirectory =
                Utils.GetDefaultOutputDirectory;
            ScaleText = "0.01";
            Units = Utils.LengthUnit.Inches;

            if (File.Exists(ConfigFile))
                File.Delete(ConfigFile);
        }
    }
}