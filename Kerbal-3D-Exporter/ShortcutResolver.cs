using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{
    /// <summary>
    /// Resolves Windows .lnk shortcut files to their actual executable paths
    /// Uses multiple strategies to find the target executable
    /// </summary>
    public class ShortcutResolver
    {
        /// <summary>
        /// Resolves a .lnk shortcut to its target executable
        /// Tries multiple methods in order of reliability
        /// </summary>
        public static string ResolveShortcut(string shortcutPath)
        {
            if (!shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                return shortcutPath;

            Log.Info($"Attempting to resolve: {shortcutPath}");

            // Method 1: Try to read the actual exe from common Ultimaker Cura location
            // (Since we know this is for Cura)
            string result = TryUltimakerCuraPath(shortcutPath);
            if (!string.IsNullOrEmpty(result))
                return result;

            // Method 2: Try enhanced binary parsing
            result = TryBinaryParsing(shortcutPath);
            if (!string.IsNullOrEmpty(result))
                return result;

            // Method 3: Try to launch shortcut directly (Windows will handle it)
            result = TryDirectShortcut(shortcutPath);
            if (!string.IsNullOrEmpty(result))
                return result;

            Log.Error($"✗ Failed to resolve shortcut: {shortcutPath}");
            return string.Empty;
        }

        /// <summary>
        /// Special handling for Ultimaker Cura shortcuts
        /// </summary>
        private static string TryUltimakerCuraPath(string shortcutPath)
        {
            try
            {
                // Cura is almost always in one of these locations
                string[] curaPaths = new[]
                {
                    @"C:\Program Files\Ultimaker Cura\bin\UltiMaker-Cura.exe",
                    @"C:\Program Files (x86)\Ultimaker Cura\bin\UltiMaker-Cura.exe",
                    @"C:\Program Files\Ultimaker Cura 5.13.0\bin\UltiMaker-Cura.exe",
                    @"C:\Program Files\Ultimaker Cura 5.13.0\UltiMaker-Cura.exe",
                    @"C:\Program Files (x86)\Ultimaker Cura 5.13.0\UltiMaker-Cura.exe",
                    @"C:\Program Files\Ultimaker Cura\UltiMaker-Cura.exe",
                    Environment.ExpandEnvironmentVariables(@"%PROGRAMFILES%\Ultimaker Cura\bin\UltiMaker-Cura.exe"),
                    Environment.ExpandEnvironmentVariables(@"%PROGRAMFILES(X86)%\Ultimaker Cura\bin\UltiMaker-Cura.exe"),
                };

                foreach (var path in curaPaths)
                {
                    if (File.Exists(path))
                    {
                        Log.Info($"✓ Found Cura at: {path}");
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Cura path search failed: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Enhanced binary parsing of .lnk files
        /// Looks for multiple patterns
        /// </summary>
        private static string TryBinaryParsing(string shortcutPath)
        {
            try
            {
                if (!File.Exists(shortcutPath))
                    return string.Empty;

                Log.Info("Trying binary parsing method...");
                byte[] data = File.ReadAllBytes(shortcutPath);

                // Look for .exe strings in the binary data
                List<string> candidates = ExtractPathsFromBinary(data);

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        Log.Info($"✓ Found via binary parsing: {candidate}");
                        return candidate;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Binary parsing failed: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Extract potential paths from binary .lnk data
        /// </summary>
        private static List<string> ExtractPathsFromBinary(byte[] data)
        {
            var candidates = new List<string>();

            try
            {
                // Look for common path prefixes in Unicode string
                string unicode = System.Text.Encoding.Unicode.GetString(data);

                // Search for common executable patterns
                string[] patterns = new[]
                {
                    ".exe",
                    ".EXE",
                };

                foreach (var pattern in patterns)
                {
                    int index = 0;
                    while ((index = unicode.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        // Try to extract the full path backwards
                        int start = index;
                        while (start > 0 &&
                               unicode[start - 1] != '\0' &&
                               unicode[start - 1] != ':' &&
                               unicode[start - 1] != '\n')
                        {
                            start--;
                        }

                        // Also try starting from drive letter
                        int driveStart = start;
                        if (driveStart > 0 && unicode[driveStart] != 'C' && unicode[driveStart] != 'D')
                        {
                            while (driveStart < index && unicode[driveStart] != 'C' &&
                                   (driveStart + 1 >= unicode.Length || unicode[driveStart + 1] != ':'))
                            {
                                driveStart++;
                            }
                        }

                        string candidate = unicode.Substring(start, index - start + 4);
                        candidate = candidate.Trim('\0', ' ', '\n', '\r');

                        if (candidate.Contains(":\\") && candidate.Length > 10)
                        {
                            candidates.Add(candidate);
                        }

                        index += 4;
                    }
                }

                // Also try to look for common program files paths
                if (unicode.Contains("Program Files"))
                {
                    int progIndex = unicode.IndexOf("Program Files");
                    if (progIndex > 0)
                    {
                        // Back up to find start
                        int start = progIndex;
                        while (start > 0 && unicode[start - 1] != '\0' && unicode[start - 1] != 'C' &&
                               unicode[start - 1] != 'D')
                        {
                            start--;
                        }

                        // Go forward to find end
                        int end = progIndex;
                        while (end < unicode.Length - 1 && unicode[end] != '\0')
                        {
                            end++;
                            if (unicode[end - 1] == 'e' && unicode[end - 2] == 'x' &&
                                unicode[end - 3] == 'e')
                            {
                                break;
                            }
                        }

                        if (end > progIndex)
                        {
                            string candidate = unicode.Substring(start, end - start);
                            candidate = candidate.Trim('\0', ' ', '\n', '\r');

                            if (candidate.Contains(":\\"))
                            {
                                candidates.Add(candidate);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Binary extraction error: {ex.Message}");
            }

            return candidates;
        }

        /// <summary>
        /// As a last resort, return the shortcut path itself
        /// Windows can launch .lnk files directly via cmd /c start
        /// </summary>
        private static string TryDirectShortcut(string shortcutPath)
        {
            try
            {
                if (File.Exists(shortcutPath))
                {
                    Log.Info("✓ Will launch shortcut directly via Windows");
                    // Return a special marker that means "launch this shortcut directly"
                    // The caller (ViewerLauncher) needs to handle this with:
                    // Process.Start("cmd.exe", $"/c start \"\" \"{path}\"");
                    return shortcutPath; // Caller will need to handle .lnk specially
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Direct shortcut failed: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets a launchable path - either .exe or .lnk
        /// </summary>
        public static string GetLaunchablePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // If it's a .lnk, try to resolve it
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                string resolved = ResolveShortcut(path);
                if (!string.IsNullOrEmpty(resolved))
                {
                    return resolved;
                }
            }

            // If it's an .exe, return as-is
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                return path;
            }

            Log.Error($"Path not launchable: {path}");
            return string.Empty;
        }

        /// <summary>
        /// Check if a path is a shortcut file
        /// </summary>
        public static bool IsShortcut(string path)
        {
            return path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a path is an executable file
        /// </summary>
        public static bool IsExecutable(string path)
        {
            return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path);
        }
    }
}
