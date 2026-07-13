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
                    new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
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
                        new GUIStyle(GUI.skin.label) { richText = true });

                    if (isInstalled)
                    {
                        GUILayout.Label($"<color=lime>Status: INSTALLED</color>",
                            new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
                        GUILayout.Label($"Path: {selectedSlicer.Pathzzz}");
                    }
                    else
                    {
                        GUILayout.Label($"<color=red>Status: NOT INSTALLED</color>",
                            new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
                        GUILayout.Label("Click website link below to download.");
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label("STL Viewer / Slicer Executable");
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
                    GUILayout.FlexibleSpace();

                    GUILayout.Space(10);
                }
                else
                {
                    GUILayout.Label("<color=orange>Select a slicer from the list</color>",
                        new GUIStyle(GUI.skin.label) { richText = true });
                }

                    GUILayout.Label("Output Folder");
                    config.OutputDirectory = GUILayout.TextField(config.OutputDirectory);
                    using (new GUILayout.HorizontalScope())
                    {

                        GUI.enabled = Utils.GetDefaultOutputDirectory != config.OutputDirectory;
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Use Default Folder"))
                                config.OutputDirectory = Utils.GetDefaultOutputDirectory;
                            GUILayout.FlexibleSpace();
                        }
                        GUI.enabled = true;
                    }


                GUILayout.Space(10);

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

                    if (GUILayout.Button("Close", GUILayout.Width(100)))
                    {
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
                GUILayout.Label(statusColor, new GUIStyle(GUI.skin.label) { richText = true }, GUILayout.Width(20));

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
                    Pathzzz = "", // Empty path for uninstalled
                    StartMenuSearchName = "",
                    UseStartMenuSearch = false,
                    HomeUrl = slicerInfo.homeUrl,
                    IsFound = false
                };
            }

            config.SelectedSlicerName = slicerInfo.name;
            config.SelectedSlicerPath = installed?.Pathzzz ?? "";
            config.SelectedSlicerStartMenuSearch = installed.StartMenuSearchName;
            config.UseStartMenuSearchForSlicer = installed.UseStartMenuSearch;
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