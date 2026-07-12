using System;
using UnityEngine;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{
    public class SlicerConfiguration
    {
        private const string SELECTED_SLICER_KEY = "STLExporter_SelectedSlicer";
        private const string SELECTED_SLICER_PATH_KEY = "STLExporter_SelectedSlicerPath";

        private const string SELECTED_SLICER_START_MENU_SEARCH_KEY = "STLExporter_SelectedSlicerStartMenuSearch";
        private const string USE_START_MENU_SEARCH_FOR_SLICER_KEY = "STLExporter_UseStartMenuSearchForSlicer";

        private const string OUTPUT_DIRECTORY = "OutputDirectory";

        public string SelectedSlicerName { get; set; }
        public string SelectedSlicerPath { get; set; }
        public string SelectedSlicerStartMenuSearch { get; set; }   
        public bool UseStartMenuSearchForSlicer { get; set; }=false;

        public string OutputDirectory { get; set; }


        /// <summary>
        /// Saves the current configuration to PlayerPrefs
        /// </summary>
        public void SaveConfiguration()
        {
            Log.Info("SaveConfiguration: Saving slicer configuration to PlayerPrefs");

            PlayerPrefs.SetString(SELECTED_SLICER_KEY, SelectedSlicerName ?? "");
            PlayerPrefs.SetString(SELECTED_SLICER_PATH_KEY, SelectedSlicerPath ?? "");

            PlayerPrefs.SetString(SELECTED_SLICER_START_MENU_SEARCH_KEY, SelectedSlicerStartMenuSearch ?? "");
            PlayerPrefs.SetInt(USE_START_MENU_SEARCH_FOR_SLICER_KEY, UseStartMenuSearchForSlicer ? 1 : 0);

            PlayerPrefs.SetString(OUTPUT_DIRECTORY, OutputDirectory ?? "");

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads the configuration from PlayerPrefs
        /// </summary>
        public void LoadConfiguration()
        {
            Log.Info("LoadConfiguration: Loading slicer configuration from PlayerPrefs");

            SelectedSlicerName = PlayerPrefs.GetString(SELECTED_SLICER_KEY, "");
            SelectedSlicerPath = PlayerPrefs.GetString(SELECTED_SLICER_PATH_KEY, "");

            SelectedSlicerStartMenuSearch = PlayerPrefs.GetString(SELECTED_SLICER_START_MENU_SEARCH_KEY, "");
            UseStartMenuSearchForSlicer = PlayerPrefs.GetInt(USE_START_MENU_SEARCH_FOR_SLICER_KEY, 0) == 1;

            OutputDirectory = PlayerPrefs.GetString(OUTPUT_DIRECTORY, "");
            if (OutputDirectory == "")
            {
                OutputDirectory = CraftPrintExporterWindow.GetDefaultOutputDirectory();
            }
        }

        /// <summary>
        /// Checks if a slicer has been configured
        /// </summary>
        public bool IsConfigured()
        {
            return (!string.IsNullOrEmpty(SelectedSlicerName) && !string.IsNullOrEmpty(SelectedSlicerPath)) ||
                (UseStartMenuSearchForSlicer && !string.IsNullOrEmpty(SelectedSlicerStartMenuSearch));
        }

        /// <summary>
        /// Clears the current configuration
        /// </summary>
        public void Clear()
        {
            SelectedSlicerName = "";
            SelectedSlicerPath = "";
            SelectedSlicerStartMenuSearch = "";
            UseStartMenuSearchForSlicer = false;
            PlayerPrefs.DeleteKey(SELECTED_SLICER_KEY);
            PlayerPrefs.DeleteKey(SELECTED_SLICER_PATH_KEY);
            PlayerPrefs.DeleteKey(SELECTED_SLICER_START_MENU_SEARCH_KEY);
            PlayerPrefs.DeleteKey(USE_START_MENU_SEARCH_FOR_SLICER_KEY);
            PlayerPrefs.DeleteKey(OUTPUT_DIRECTORY);
            PlayerPrefs.Save();
        }

        ////////////////////////////

    }
}
