using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    internal sealed class ExportContext
    {
        public float UserScale;
        public bool ExportStl;
        public bool ExportObj;
        public bool Export3mf;
        public bool ThreeMfPerPart;
        public bool ExportStp;
        public bool ShowShrouds;
        public bool ExcludeLaunchClamps;
        public string ViewerExePath;
        public string ViewerStartMenuSearch;
        public bool UseStartMenuSearchForViewer;
        public bool OpenViewerAfterExport;
        public List<EngineShroudOption> EngineShroudOptions = new List<EngineShroudOption>();
        public HashSet<string> DisabledRendererKeys = new HashSet<string>();
        public HashSet<Renderer> DisabledRenderers = new HashSet<Renderer>();
        public HashSet<Transform> DisabledRendererTransforms = new HashSet<Transform>();

        public string OutputDir;
        public string CraftName;
        public string StlFile;
        public string ObjFile;
        public string ThreeMfFile;
        public string StpFile;

        public List<Part> Parts;
        public string SceneDescription;
        public List<Triangle> Triangles;

        // partIndex (as stamped onto Triangle.PartIndex by MeshCollector) -> display name.
        // Only consumed by the per-part 3MF export.
        public Dictionary<int, string> PartNames = new Dictionary<int, string>();
        public List<ShroudState> SavedShroudStates = new List<ShroudState>();

        // Transforms that should never be exported when their engine shroud option is disabled.
        // This is needed because some shroud meshes are still reachable even after their GameObjects
        // or renderers are disabled, especially when mesh collection scans inactive children.
        public HashSet<Transform> ShroudTransformsToSkip = new HashSet<Transform>();

        // Transforms belonging to inactive/non-selected ModulePartVariants variants.
        // These must be skipped during mesh collection or every variant model can be exported.
        public HashSet<Transform> InactiveVariantTransformsToSkip = new HashSet<Transform>();

        // Snapshot-based (reflection-free) version of the above: meshes on a part known to have a
        // ModulePartVariants module that were ALREADY inactive/disabled before this exporter
        // touched anything. See VariantSnapshotUtilities for why this exists alongside
        // InactiveVariantTransformsToSkip.
        public HashSet<Transform> OriginallyHiddenVariantTransforms = new HashSet<Transform>();

        // Optional user-maintained exclusion rules and export diagnostics.
#if EXPORT_EXCLUSION_RULE_DEFINED
        public List<ExportExclusionRule> ExclusionRules = new List<ExportExclusionRule>();
#endif
        public List<string> MeshDiagnostics = new List<string>();
        public string MeshDiagnosticFile;
        public string PartDiagnosticFile;

        public ExportStage Stage;
        public Action<string> Status;
        public Action<float> Progress;
        public Func<bool> CancelRequested;
        public Action Complete;
    }
}
