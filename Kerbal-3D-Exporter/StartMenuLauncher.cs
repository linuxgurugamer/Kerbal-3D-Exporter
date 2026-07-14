using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{
    /// <summary>
    /// Searches the Windows Start Menu for applications matching a search term
    /// and launches them with optional file arguments.
    /// Supports simple shortcuts (.lnk), direct executables, and application shortcuts.
    /// </summary>
    public class StartMenuLauncher
    {
        private static readonly string[] StartMenuPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs")
        };

        private static readonly string[] SearchExtensions = new[] { ".lnk", ".exe", ".url" };

        /// <summary>
        /// COM interface for reading shortcut properties
        /// </summary>
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPStr)] string pszFile);
        }

        [ComImport, Guid("0000010B-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig]
            int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [ComImport, Guid("0AEBF2B5-E630-4387-ABBA-E728EE30F27B")]
        private class ShellLink { }

        /// <summary>
        /// Alternative COM class for shortcuts (Windows 7+)
        /// </summary>
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDEE49FF00BC")]
        private class ShellLinkW { }

        /// <summary>
        /// Represents an application found in the Start Menu
        /// </summary>
        public class StartMenuEntry
        {
            public string DisplayName { get; set; }
            public string TargetPath { get; set; }
            public string Arguments { get; set; }
            public string WorkingDirectory { get; set; }
            public string IconPath { get; set; }
            public EntryType Type { get; set; }

            public override string ToString()
            {
                return $"{DisplayName} ({Type}) -> {TargetPath}";
            }
        }

        public enum EntryType
        {
            Shortcut,      // .lnk file
            Executable,    // .exe file
            UrlShortcut,   // .url file
            AppShortcut    // Windows Store/App package
        }

        /// <summary>
        /// Searches the Start Menu for applications matching the specified substring
        /// </summary>
        /// <param name="searchTerm">The substring to search for (case-insensitive)</param>
        /// <returns>List of matching StartMenuEntry objects</returns>
        public static List<StartMenuEntry> SearchStartMenu(string searchTerm)
        {
            var results = new List<StartMenuEntry>();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                Log.Warn("Search term cannot be empty");
                return results;
            }

            try
            {
                foreach (var startMenuPath in StartMenuPaths)
                {
                    if (!Directory.Exists(startMenuPath))
                        continue;

                    SearchDirectory(startMenuPath, searchTerm, results);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error searching Start Menu: {ex.Message}");
            }

            return results.OrderBy(e => e.DisplayName).ToList();
        }

        /// <summary>
        /// Recursively searches a directory for matching applications
        /// </summary>
        private static void SearchDirectory(string directoryPath, string searchTerm, List<StartMenuEntry> results)
        {
            try
            {
                var dirInfo = new DirectoryInfo(directoryPath);

                // Search for matching files
                foreach (var file in dirInfo.GetFiles())
                {
                    if (SearchExtensions.Contains(file.Extension.ToLower()) &&
                        file.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var entry = ParseStartMenuEntry(file.FullName);
                        if (entry != null)
                        {
                            results.Add(entry);
                        }
                    }
                }

                // Recurse into subdirectories
                foreach (var subDir in dirInfo.GetDirectories())
                {
                    SearchDirectory(subDir.FullName, searchTerm, results);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have access to
            }
            catch (Exception ex)
            {
                Log.Warn($"Error searching directory {directoryPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses a Start Menu entry and extracts its information
        /// </summary>
        private static StartMenuEntry ParseStartMenuEntry(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var extension = fileInfo.Extension.ToLower();

            try
            {
                switch (extension)
                {
                    case ".lnk":
                        return ParseShortcut(filePath);

                    case ".exe":
                        return new StartMenuEntry
                        {
                            DisplayName = fileInfo.Name.Replace(".exe", ""),
                            TargetPath = filePath,
                            Type = EntryType.Executable,
                            WorkingDirectory = fileInfo.DirectoryName
                        };

                    case ".url":
                        return ParseUrlShortcut(filePath);

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Error parsing {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses a Windows .lnk shortcut file using IShellLink COM interface
        /// </summary>
        private static StartMenuEntry ParseShortcut(string shortcutPath)
        {
#if false
            IShellLink shellLink = null;
            try
            {
                // Try the standard ShellLink class first
                shellLink = (IShellLink)new ShellLink();
                return ParseShortcutWithInterface(shellLink, shortcutPath);
            }
            catch (COMException ex) when (ex.ErrorCode == -2147221164) // CLASS_E_CLASSNOTAVAILABLE
            {
                Log.Warn($"ShellLink COM class not available, trying alternative: {shortcutPath}");

                // Try alternative COM class
                try
                {
                    shellLink = (IShellLink)new ShellLinkW();
                    return ParseShortcutWithInterface(shellLink, shortcutPath);
                }
                catch (Exception ex2)
                {
                    Log.Warn($"Alternative ShellLink also failed: {ex2.Message}");
                    return CreateShortcutEntryFromPath(shortcutPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error parsing shortcut {shortcutPath}: {ex.Message}");
                return CreateShortcutEntryFromPath(shortcutPath);
            }
            finally
            {
                if (shellLink != null)
                {
                    try
                    {
                        Marshal.ReleaseComObject(shellLink);
                    }
                    catch { }
                }
            }
#else
            return CreateShortcutEntryFromPath(shortcutPath);
#endif
        }

        /// <summary>
        /// Parse shortcut using a COM IShellLink interface
        /// </summary>
        private static StartMenuEntry ParseShortcutWithInterface(IShellLink shellLink, string shortcutPath)
        {
            IPersistFile persistFile = (IPersistFile)shellLink;
            persistFile.Load(shortcutPath, 0);

            // Get target path
            var pathBuffer = new StringBuilder(260);
            IntPtr fd;
            shellLink.GetPath(pathBuffer, pathBuffer.Capacity, out fd, 0);
            string targetPath = pathBuffer.ToString();

            // Get arguments
            var argsBuffer = new StringBuilder(260);
            shellLink.GetArguments(argsBuffer, argsBuffer.Capacity);
            string arguments = argsBuffer.ToString();

            // Get working directory
            var workDirBuffer = new StringBuilder(260);
            shellLink.GetWorkingDirectory(workDirBuffer, workDirBuffer.Capacity);
            string workingDirectory = workDirBuffer.ToString();

            // Get icon location
            var iconBuffer = new StringBuilder(260);
            int iconIndex;
            shellLink.GetIconLocation(iconBuffer, iconBuffer.Capacity, out iconIndex);
            string iconPath = iconBuffer.ToString();

            var entry = new StartMenuEntry
            {
                DisplayName = Path.GetFileNameWithoutExtension(shortcutPath),
                TargetPath = targetPath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                IconPath = iconPath,
                Type = EntryType.Shortcut
            };

            return entry;
        }

        /// <summary>
        /// Fallback: Create a minimal entry from the shortcut path when COM is unavailable
        /// This allows the shortcut to still be found, though with limited information
        /// </summary>
        private static StartMenuEntry CreateShortcutEntryFromPath(string shortcutPath)
        {
            Log.Info($"Creating fallback entry for shortcut: {shortcutPath}");

            return new StartMenuEntry
            {
                DisplayName = Path.GetFileNameWithoutExtension(shortcutPath),
                TargetPath = shortcutPath, // Can still be executed via Process.Start with the .lnk file
                Arguments = "",
                WorkingDirectory = Path.GetDirectoryName(shortcutPath),
                IconPath = "",
                Type = EntryType.Shortcut
            };
        }

        /// <summary>
        /// Parses a .url Internet shortcut file
        /// </summary>
        private static StartMenuEntry ParseUrlShortcut(string urlPath)
        {
            try
            {
                var entry = new StartMenuEntry
                {
                    DisplayName = Path.GetFileNameWithoutExtension(urlPath),
                    Type = EntryType.UrlShortcut,
                    WorkingDirectory = Path.GetDirectoryName(urlPath)
                };

                // Read the .url file to get the target URL
                var lines = File.ReadAllLines(urlPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.TargetPath = line.Substring(4);
                        break;
                    }
                }

                return entry;
            }
            catch (Exception ex)
            {
                Log.Warn($"Error parsing URL shortcut {urlPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Launches an application from a StartMenuEntry with optional file argument
        /// </summary>
        /// <param name="entry">The StartMenuEntry to launch</param>
        /// <param name="fileToOpen">Optional filepath to pass to the application</param>
        /// <returns>True if launch was successful, false otherwise</returns>
        public static bool LaunchApplication(StartMenuEntry entry, string fileToOpen = null)
        {
            if (entry == null)
            {
                Log.Error("StartMenuEntry cannot be null");
                return false;
            }

            try
            {
                string targetPath = entry.TargetPath;
                string arguments = entry.Arguments ?? "";

                if (string.IsNullOrEmpty(targetPath))
                {
                    Log.Error($"Target path is empty for {entry.DisplayName}");
                    return false;
                }

                // Add the file argument if provided
                if (!string.IsNullOrWhiteSpace(fileToOpen))
                {
                    if (!File.Exists(fileToOpen))
                    {
                        Log.Error($"File does not exist: {fileToOpen}");
                        return false;
                    }

                    arguments += (string.IsNullOrEmpty(arguments) ? "" : " ") + $"\"{fileToOpen}\"";
                }

                // Handle different entry types
                if (entry.Type == EntryType.UrlShortcut)
                {
                    System.Diagnostics.Process.Start(targetPath);
                }
                else
                {
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = targetPath,
                        Arguments = arguments,
                        WorkingDirectory = entry.WorkingDirectory,
                        UseShellExecute = true
                    };

                    System.Diagnostics.Process.Start(processInfo);
                }

                Log.Info($"Launched: {entry.DisplayName} with args: {arguments}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error launching {entry.DisplayName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Convenience method to search for an application in the start jmenu
        /// </summary>
        /// <param name="searchTerm">The substring to search for</param>
        /// <returns>True if an application was found, false otherwise</returns>
        public static bool Search(string searchTerm)
        {
            var matches = SearchStartMenu(searchTerm);

            if (matches.Count == 0)
            {
                Log.Warn($"No applications found matching: {searchTerm}");
                return false;
            }

            if (matches.Count > 1)
            {
                Log.Warn($"Multiple applications found matching '{searchTerm}':");
                foreach (var match in matches)
                {
                    Log.Info($"  - {match}");
                }
                Log.Info($"Launching first match: {matches[0].DisplayName}");
            }
            return true;
        }

        /// <summary>
        /// Convenience method to search and launch an application in one call
        /// </summary>
        /// <param name="searchTerm">The substring to search for</param>
        /// <param name="fileToOpen">Optional filepath to pass to the application</param>
        /// <returns>True if an application was found and launched, false otherwise</returns>
        public static bool SearchAndLaunch(string searchTerm, string fileToOpen = null)
        {
            var matches = SearchStartMenu(searchTerm);

            if (matches.Count == 0)
            {
                Log.Warn($"No applications found matching: {searchTerm}");
                return false;
            }

            if (matches.Count > 1)
            {
                Log.Warn($"Multiple applications found matching '{searchTerm}':");
                foreach (var match in matches)
                {
                    Log.Info($"  - {match}");
                }
                Log.Info($"Launching first match: {matches[0].DisplayName}");
            }

            return LaunchApplication(matches[0], fileToOpen);
        }
    }
}
