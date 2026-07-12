using System;
using System.Diagnostics;
using System.IO;

namespace CraftMeshExporter
{
    internal static class ViewerLauncher
    {
        public static void Open(string viewerExePath, string modelFile, Action<string> status)
        {
            try
            {
                if (string.IsNullOrEmpty(modelFile) || !File.Exists(modelFile))
                {
                    Status(status, "Viewer was not opened because the model file does not exist.");
                    return;
                }

                if (string.IsNullOrEmpty(viewerExePath))
                {
                    Status(status, "Viewer executable is empty; opening model with the OS default application.");
                    Process.Start(modelFile);
                    return;
                }

                viewerExePath = viewerExePath.Trim().Trim('"');

                if (!File.Exists(viewerExePath))
                {
                    Status(status, "Viewer executable not found: " + viewerExePath);
                    return;
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = viewerExePath;
                psi.Arguments = QuoteArgument(modelFile);
                psi.WorkingDirectory = Path.GetDirectoryName(viewerExePath);
                psi.UseShellExecute = false;

                Process.Start(psi);
                Status(status, "Opened viewer: " + viewerExePath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[CraftMeshExporter] Failed to open viewer: " + ex);
                Status(status, "Failed to open viewer. See KSP log.");
            }
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void Status(Action<string> status, string message)
        {
            if (status != null)
                status(message);
        }
    }
}
