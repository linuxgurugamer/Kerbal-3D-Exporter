using System;
using System.Collections.Generic;
using System.IO;

namespace CraftMeshExporter
{
#if EXPORT_EXCLUSION_RULE_DEFINED
    internal static class ExportExclusionUtilities
    {
        public static string GetExclusionFilePath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData/CraftMeshExporter/shroud-exclusions.txt");
        }

        public static List<ExportExclusionRule> LoadRules(Action<string> status)
        {
            List<ExportExclusionRule> rules = new List<ExportExclusionRule>();
            string file = GetExclusionFilePath();

            if (!File.Exists(file))
            {
                WriteSampleFile(file);
                if (status != null)
                    status("Created sample exclusion file: " + file);
                return rules;
            }

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line == null)
                    continue;

                line = line.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                string partName = "*";
                string token = line;

                int pipe = line.IndexOf('|');
                if (pipe >= 0)
                {
                    partName = line.Substring(0, pipe).Trim();
                    token = line.Substring(pipe + 1).Trim();
                }

                if (token.Length == 0)
                    continue;

                ExportExclusionRule rule = new ExportExclusionRule();
                rule.PartName = partName.Length == 0 ? "*" : partName;
                rule.Token = token.ToLowerInvariant();
                rules.Add(rule);
            }

            if (status != null)
                status("Loaded shroud exclusion rules: " + rules.Count);

            return rules;
        }

        private static void WriteSampleFile(string file)
        {
            string dir = Path.GetDirectoryName(file);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (StreamWriter sw = new StreamWriter(file))
            {
                sw.WriteLine("# CraftMeshExporter shroud/fairing exclusion rules");
                sw.WriteLine("# Used only when shrouds/fairings are disabled for the part/engine.");
                sw.WriteLine("# Format:");
                sw.WriteLine("#   partName|transform-or-material-token");
                sw.WriteLine("#   *|global-token");
                sw.WriteLine("# Examples:");
                sw.WriteLine("#   liquidEngine2|fairing");
                sw.WriteLine("#   *|shroud");
                sw.WriteLine("#");
                sw.WriteLine("# Use the generated *_mesh_diagnostics.txt file in the Models folder to find");
                sw.WriteLine("# the exact PartName, renderer path, mesh name, and material names to exclude.");
            }
        }
    }
#endif
}
