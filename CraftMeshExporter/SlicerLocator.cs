using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using static CraftMeshExporter.CraftMeshExporterToolbarRegistration;

namespace CraftMeshExporter
{
    public class SlicerHomeInfo
    {
        internal string name;
        internal string homeUrl;
        internal string startMenuSearchName;
        internal string[] LinuxPaths;
        internal string[] MacPaths;
        internal string[] WinPaths;
    }

    public class SlicerLocator
    {

        private static readonly SlicerHomeInfo[] SlicerDatabase = new[]
        {
            new SlicerHomeInfo
            {
                name = "Cura",
                homeUrl = "https://ultimaker.com/software/ultimaker-cura",
                startMenuSearchName = "Cura",
                WinPaths = new[] {
                    @"C:\Program Files\Ultimaker Cura\UltiMaker-Cura.exe",
                    @"C:\Program Files (x86)\Ultimaker Cura\UltiMaker-Cura.exe",
                    Environment.ExpandEnvironmentVariables(@"%PROGRAMFILES%\Ultimaker Cura\bin\UltiMaker-Cura.exe"),
                    Environment.ExpandEnvironmentVariables(@"%PROGRAMFILES(X86)%\Ultimaker Cura\bin\UltiMaker-Cura.exe"),
                    Environment.ExpandEnvironmentVariables(@"%APPDATA%\Ultimaker Cura")
                },
                MacPaths = new[] { "/Applications/Ultimaker Cura.app" },
                LinuxPaths = new[] { "/usr/bin/cura", "/snap/bin/cura", "~/.local/bin/cura" }
            },
            new SlicerHomeInfo
            {
                name = "PrusaSlicer",
                homeUrl = "https://www.prusa3d.com/en/prusaslicer/",
                startMenuSearchName = "PrusaSlicer",
                WinPaths = new[] {
                    @"C:\Program Files\Prusa3D\PrusaSlicer",
                    @"C:\Program Files (x86)\Prusa3D\PrusaSlicer",
                    Environment.ExpandEnvironmentVariables(@"%APPDATA%\PrusaSlicer")
                },
                MacPaths = new[] { "/Applications/PrusaSlicer.app" },
                LinuxPaths = new[] { "/usr/bin/prusa-slicer", "/snap/bin/prusa-slicer" }
            },
            new SlicerHomeInfo
            {
                name = "SuperSlicer",
                homeUrl = "https://github.com/supermerill/SuperSlicer",
                startMenuSearchName = "SuperSlicer",
                WinPaths = new[] {
                    @"C:\Program Files\SuperSlicer",
                    @"C:\Program Files (x86)\SuperSlicer",
                    Environment.ExpandEnvironmentVariables(@"%APPDATA%\SuperSlicer")
                },
                MacPaths = new[] { "/Applications/SuperSlicer.app" },
                LinuxPaths = new[] { "/usr/bin/super-slicer", "/snap/bin/superslicer" }
            },
            new SlicerHomeInfo
            {
                name = "OrcaSlicer",
                homeUrl = "https://github.com/SoftFever/OrcaSlicer",
                startMenuSearchName = "OrcaSlicer",
                WinPaths = new[] {
                    @"C:\Program Files\OrcaSlicer",
                    @"C:\Program Files (x86)\OrcaSlicer",
                    Environment.ExpandEnvironmentVariables(@"%APPDATA%\OrcaSlicer")
                },
                MacPaths = new[] { "/Applications/OrcaSlicer.app" },
                LinuxPaths = new[] { "/usr/bin/orca-slicer" }
            },
            new SlicerHomeInfo
            {
                name = "Creality Print",
                homeUrl = "https://www.creality.com/pages/download-software",
                startMenuSearchName = "Creality",
                WinPaths = new[] {
                    @"C:\Program Files\Creality",
                    @"C:\Program Files (x86)\Creality",
                    Environment.ExpandEnvironmentVariables(@"%APPDATA%\Creality")
                },
                MacPaths = new[] { "/Applications/Creality Print.app" },
                LinuxPaths = Array.Empty<string>()
            },
            new SlicerHomeInfo
            {
                name = "Bambu Studio",
                homeUrl = "https://bambulab.com/en/download/studio",
                startMenuSearchName = "Bambu",
                WinPaths = new[] {
                    @"C:\Program Files\Bambu Studio",
                    @"C:\Program Files (x86)\Bambu Studio",
                    Environment.ExpandEnvironmentVariables(@"%APPDATA%\BambuStudio")
                },
                MacPaths = new[] { "/Applications/Bambu Studio.app" },
                LinuxPaths = Array.Empty<string>()
            },
            new SlicerHomeInfo
            {
                name = "Papa's Best STL Viewer",
                homeUrl = "https://github.com/Jinja2/papas-best-stl-viewer",
                startMenuSearchName = "Papa",
                MacPaths = new[] { "/Applications/Papa's Best STL Viewer.app" },
                LinuxPaths = new[] { "/usr/bin/papas-best-stl-viewer", "/snap/bin/papas-best-stl-viewer" }
            }
        };

        public class FoundSlicerInfo
        {
            public string Name { get; set; }
            public string Pathzzz { get; set; }
            public string StartMenuSearchName { get; set; }
            public bool UseStartMenuSearch { get; set; } = false;
            public string HomeUrl { get; set; }
            public bool IsFound { get; set; }
            public bool startMenuFound { get; set; } = false;
        }

        /// <summary>
        /// Searches system for installed slicers and viewers
        /// IMPORTANT: Always returns .exe paths, never .lnk paths
        /// If a .lnk file is found, it's resolved to the actual executable
        /// </summary>
        public static List<FoundSlicerInfo> FindInstalledSlicers()
        {
            var foundSlicers = new List<FoundSlicerInfo>();

            foreach (var slicerInfo in SlicerDatabase)
            {
                if (StartMenuLauncher.Search(slicerInfo.startMenuSearchName))
                {
                    foundSlicers.Add(new FoundSlicerInfo
                    {
                        Name = slicerInfo.name,
                        Pathzzz = "", 
                        StartMenuSearchName = slicerInfo.startMenuSearchName,
                        UseStartMenuSearch = true,
                        HomeUrl = slicerInfo.homeUrl,
                        IsFound = true, 
                        startMenuFound = true
                    });
                    continue; // Found via Start Menu, skip path search
                }


                var pathsToSearch = GetPathsForPlatform(slicerInfo);

                foreach (var path in pathsToSearch)
                {
                    var expandedPath = Environment.ExpandEnvironmentVariables(path);

                    // Try the path as-is first
                    if (DirectoryExists(expandedPath) || FileExists(expandedPath))
                    {
                        string finalPath = expandedPath;

                        // If it's a .lnk shortcut, resolve it to the .exe
                        if (finalPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                        {
                            Log.Info($"Found shortcut: {finalPath}");
                            string resolved = ShortcutResolver.ResolveShortcut(finalPath);

                            if (!string.IsNullOrEmpty(resolved) && FileExists(resolved))
                            {
                                finalPath = resolved;
                                Log.Info($"✓ Resolved to: {finalPath}");
                            }
                            else
                            {
                                Log.Warn($"Could not resolve shortcut, skipping: {finalPath}");
                                continue;
                            }
                        }

                        foundSlicers.Add(new FoundSlicerInfo
                        {
                            Name = slicerInfo.name,
                            Pathzzz = finalPath,  // Always .exe, never .lnk
                            HomeUrl = slicerInfo.homeUrl,
                            //StartMenuSearchName = slicerInfo.startMenuSearchName,
                            UseStartMenuSearch = false,
                            IsFound = true
                        });
                        break; // Found this slicer, move to next
                    }
                }
            }

            return foundSlicers;
        }

        /// <summary>
        /// Returns the appropriate paths array based on current platform
        /// </summary>
        private static string[] GetPathsForPlatform(SlicerHomeInfo slicerInfo)
        {
            if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsPlayer)
                return slicerInfo.WinPaths;

            if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXPlayer)
                return slicerInfo.MacPaths;

            if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.LinuxPlayer)
                return slicerInfo.LinuxPaths;

            return slicerInfo.WinPaths; // Default to Windows
        }

        /// <summary>
        /// Presents found slicers/viewers in a UI and returns user's selection
        /// </summary>
        public static FoundSlicerInfo SelectSlicerFromUI(List<FoundSlicerInfo> slicers)
        {
            if (!slicers.Any())
            {
                Log.Info("No slicers or viewers found on system.");
                return null;
            }

            Log.Info("Found applications:");
            for (int i = 0; i < slicers.Count; i++)
            {
                Log.Info($"[{i}] {slicers[i].Name} - {slicers[i].Pathzzz}");
            }

            return slicers.FirstOrDefault();
        }

        /// <summary>
        /// Main entry point - find and select slicer/viewer
        /// </summary>
        public static FoundSlicerInfo LocateAndSelectSlicer()
        {
            var foundSlicers = FindInstalledSlicers();
            return SelectSlicerFromUI(foundSlicers);
        }

        private static bool DirectoryExists(string path) => Directory.Exists(path);
        private static bool FileExists(string path) => File.Exists(path);

        /// <summary>
        /// Returns all supported slicers from the database
        /// </summary>
        public static SlicerHomeInfo[] GetAllSupportedSlicers()
        {
            return SlicerDatabase;
        }

        /// <summary>
        /// Gets a specific slicer by name
        /// </summary>
        public static FoundSlicerInfo GetInstalledSlicerByName(string name)
        {
            var found = FindInstalledSlicers();
            return found.FirstOrDefault(s => s.Name == name);
        }
    }
}
