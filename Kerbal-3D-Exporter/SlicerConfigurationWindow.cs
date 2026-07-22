using ClickThroughFix;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    public class SlicerConfigurationWindow : MonoBehaviour
    {
        private Rect windowRect = new Rect(Screen.width / 2 - 300, Screen.height / 2 - 250, 600, 550);
        public static bool showWindow;
        private SlicerConfiguration config;
        private List<SlicerLocator.FoundSlicerInfo> installedSlicers;
        private SlicerLocator.FoundSlicerInfo selectedSlicer;
        private Vector2 slicerScrollPosition;

        bool changed = false;

        public void OnEnable()
        {
            config = new SlicerConfiguration();
            config.LoadConfiguration();
            RefreshSlicers();
        }

        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;

            if (showWindow)
            {
                windowRect = ClickThruBlocker.GUILayoutWindow(Utils.ConfigWinID, windowRect, DrawWindow, "Kerbal 3D Exporter - Slicer Configuration", Utils.solidWindowStyle);
                GUI.BringWindowToFront(Utils.ConfigWinID);
            }
        }

        private void DrawWindow(int id)
        {
            using (new GUILayout.VerticalScope())
            {
                GUILayout.Label("Available Slicers & Viewers", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                GUILayout.Label("<color=orange>• Green = Installed | Gray = Not Found</color>",
                    Utils.labelRichTextFont11Style);
                GUILayout.Space(8);
                // Display all supported slicers
                var allSlicers = SlicerLocator.GetAllSupportedSlicers();
                if (allSlicers.Length == 0)
                {
                    GUILayout.Label("No slicers found in database.");
                }
                else
                {
                    slicerScrollPosition = GUILayout.BeginScrollView(slicerScrollPosition, GUILayout.Height(240));

                    foreach (var slicerInfo in allSlicers)
                    {
                        var isInstalled = installedSlicers.Any(s => s.Name == slicerInfo.name);
                        DrawSlicerButton(slicerInfo, isInstalled);
                    }

                    GUILayout.EndScrollView();
                }

                GUILayout.Space(8);

                // Display selected slicer info
                if (selectedSlicer != null)
                {
                    var isInstalled = installedSlicers.Any(s => s.Name == selectedSlicer.Name);

                    GUILayout.Box("");
                    GUILayout.Label($"<b>Selected:</b> {selectedSlicer.Name}",
                        Utils.labelRichTextStyle);

                    if (isInstalled)
                    {
                        GUILayout.Label($"<color=lime>Status: INSTALLED</color>",
                            Utils.labelRichTextFont11Style);
                    }
                    else
                    {
                        GUILayout.Label($"<color=red>Status: NOT INSTALLED</color>",
                            Utils.labelRedBoldStyle);
                        GUILayout.Label("Click website link below to download.");
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label("STL Viewer / Slicer Executable");
                    bool oUseStartMenuSearch = config.UseStartMenuSearchForSlicer;
                    string oStartMenuSearch = config.SelectedSlicerStartMenuSearch;
                    string oSelectedSlicerPath = config.SelectedSlicerPath;
                    using (new GUILayout.HorizontalScope())
                    {
                        using (new GUILayout.VerticalScope())
                        {
                            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                            {
                                config.UseStartMenuSearchForSlicer = GUILayout.Toggle(config.UseStartMenuSearchForSlicer, "Use Start Menu Search", GUILayout.Width(200));
                                using (new GUILayout.HorizontalScope())
                                {
                                    GUILayout.Label("Start Menu Search:", GUILayout.Width(200));
                                    config.SelectedSlicerStartMenuSearch = GUILayout.TextField(config.SelectedSlicerStartMenuSearch, GUILayout.Width(200));
                                }
                            }
                            using (new GUILayout.HorizontalScope())
                            {
                                GUILayout.Label("Executable Path:", GUILayout.Width(120));
                                config.SelectedSlicerPath = GUILayout.TextField(config.SelectedSlicerPath);
                            }
                        }
                    }
                    if (oUseStartMenuSearch != config.UseStartMenuSearchForSlicer ||
                        oStartMenuSearch != config.SelectedSlicerStartMenuSearch ||
                        oSelectedSlicerPath != config.SelectedSlicerPath)
                        changed = true;
                    GUILayout.FlexibleSpace();

                    GUILayout.Space(10);
                }
                else
                {
                    GUILayout.Label("<color=orange>Select a slicer from the list</color>",
                        Utils.labelRichTextStyle);
                }

                GUILayout.Label("Output Folder");
                config.OutputDirectory = GUILayout.TextField(config.OutputDirectory);
                using (new GUILayout.HorizontalScope())
                {
                    if (Utils.GetDefaultOutputDirectory != config.OutputDirectory)
                        changed = true;
                    GUI.enabled = Utils.GetDefaultOutputDirectory != config.OutputDirectory;

                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Use Default Folder"))
                        {
                            config.OutputDirectory = Utils.GetDefaultOutputDirectory;
                            changed = true;
                        }
                        GUILayout.FlexibleSpace();
                    }
                    GUI.enabled = true;
                }

                GUILayout.Space(10);
                config.DebugMode = GUILayout.Toggle(config.DebugMode, "Debug Mode");

                GUILayout.Space(10);
                if (changed)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        bool visible = (int)(Time.realtimeSinceStartup % 3) == 0;
                        if (visible)
                            GUILayout.Label("Current Configuration NOT SAVED", Utils.labelYellowBoldStyle);
                        else
                            GUILayout.Label(" ");
                        GUILayout.FlexibleSpace();
                    }
                }
                // Buttons
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh", GUILayout.Width(100)))
                    {
                        RefreshSlicers();
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Visit Website", GUILayout.Width(120)))
                    {
                        OpenUrl(selectedSlicer.HomeUrl);
                    }

                    GUILayout.FlexibleSpace();

                    // Only allow save if selected slicer is installed
                    var canSave = selectedSlicer != null &&
                        installedSlicers.Any(s => s.Name == selectedSlicer.Name);

                    GUI.enabled = canSave;
                    if (GUILayout.Button("Save", GUILayout.Width(100)))
                    {
                        SaveAndClose();
                    }
                    GUI.enabled = true;

                    if (GUILayout.Button(changed ? "Cancel" : "Close", GUILayout.Width(100)))
                    {
                        if (changed)
                            config.LoadConfiguration();
                        Close();
                    }
                }

                GUILayout.Space(5);
            }

            if (GUI.enabled)
                GUI.DragWindow();
        }

        private void DrawSlicerButton(SlicerHomeInfo slicerInfo, bool isInstalled)
        {
            var isSelected = selectedSlicer?.Name == slicerInfo.name;

            using (new GUILayout.HorizontalScope(GUI.skin.box))
            {
                // Status indicator
                var statusColor = isInstalled ? "<color=lime>●</color>" : "<color=gray>●</color>";
                GUILayout.Label(statusColor, Utils.labelRichTextStyle, GUILayout.Width(20));

                // Button to select slicer
                var displayText = isSelected ? $"{slicerInfo.name} ✓" : slicerInfo.name;
                var buttonStyle = isSelected ? new GUIStyle(GUI.skin.button)
                { normal = { background = GUI.skin.button.active.background } }
                    : new GUIStyle(GUI.skin.button);

                if (GUILayout.Button(displayText, buttonStyle))
                {
                    SelectSlicer(slicerInfo);
                }
            }
        }

        private void SelectSlicer(SlicerHomeInfo slicerInfo)
        {
            // Find if installed
            var installed = installedSlicers.FirstOrDefault(s => s.Name == slicerInfo.name);

            if (installed != null)
            {
                selectedSlicer = installed;
            }
            else
            {
                // Create a temporary FoundSlicerInfo for uninstalled slicers
                selectedSlicer = new SlicerLocator.FoundSlicerInfo
                {
                    Name = slicerInfo.name,
                    Path = "", // Empty path for uninstalled
                    StartMenuSearchName = "",
                    UseStartMenuSearch = false,
                    HomeUrl = slicerInfo.homeUrl,
                    IsFound = false
                };
            }

            config.SelectedSlicerName = slicerInfo.name;
            config.SelectedSlicerPath = installed?.Path ?? "";
            config.SelectedSlicerStartMenuSearch = installed?.StartMenuSearchName ?? "";
            config.UseStartMenuSearchForSlicer = installed?.UseStartMenuSearch ?? false;

            changed = true;
        }

        private void RefreshSlicers()
        {
            installedSlicers = SlicerLocator.FindInstalledSlicers();

            // Re-select the current slicer if it's still installed
            if (!string.IsNullOrEmpty(config.SelectedSlicerName))
            {
                var stillInstalled = installedSlicers.FirstOrDefault(s => s.Name == config.SelectedSlicerName);
                if (stillInstalled != null)
                {
                    selectedSlicer = stillInstalled;
                }
                else
                {
                    selectedSlicer = null;
                }
            }
        }

        private void SaveAndClose()
        {
            // Only allow saving if slicer is installed
            var isInstalled = installedSlicers.Any(s => s.Name == selectedSlicer.Name);

            if (!isInstalled)
            {
                ScreenMessages.PostScreenMessage("Can only save installed slicers!", 5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            if (selectedSlicer == null)
            {
                ScreenMessages.PostScreenMessage("Please select a slicer first!", 5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            config.SaveConfiguration();
            ScreenMessages.PostScreenMessage($"Configuration saved: {selectedSlicer.Name}", 5f, ScreenMessageStyle.UPPER_CENTER);
            Close();
        }

        public void Open()
        {
            RefreshSlicers();
            showWindow = true;
        }

        public static void Close()
        {
            showWindow = false;
            SlicerConfigurationWindow win = FindObjectOfType<SlicerConfigurationWindow>();
            if (win != null)
                Destroy(win);

        }

        public bool IsOpen()
        {
            return showWindow;
        }

        private void OpenUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                Application.OpenURL(url);
            }
        }

        public SlicerConfiguration GetConfiguration()
        {
            return config;
        }
    }
}