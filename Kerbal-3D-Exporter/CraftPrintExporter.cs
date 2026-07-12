using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;
using System.Diagnostics;

namespace Kerbal_3D_Exporter
{
    internal static class CraftPrintExporter
    {
        private const int TOTAL_PROGRESS_STEPS = 18;

        public static IEnumerator ExportCurrentCraft(
            float userScale,
            bool exportStl,
            bool exportObj,
            bool export3mf,
            bool threeMfPerPart,
            bool exportStp,
            bool dumpMesh,
            bool showShrouds,
            bool excludeLaunchClamps,
            List<EngineShroudOption> engineShroudOptions,
            HashSet<string> disabledRendererKeys,
            HashSet<Renderer> disabledRenderers,
            HashSet<Transform> disabledRendererTransforms,
            string outputDir,
            string viewerExePath,
            string viewerStartMenuSearch,
            bool useStartMenuSearchForViewer,
            bool openViewerAfterExport,
            Action<string> status,
            Action<float> progress,
            Func<bool> cancelRequested,
            Action complete)
        {
            ExportContext ctx = new ExportContext();
            ctx.UserScale = userScale;
            ctx.ExportStl = exportStl;
            ctx.ExportObj = exportObj;
            ctx.Export3mf = export3mf;
            ctx.ThreeMfPerPart = threeMfPerPart;
            ctx.ExportStp = exportStp;
            ctx.DumpMesh = dumpMesh;
            ctx.ShowShrouds = showShrouds;
            ctx.ExcludeLaunchClamps = excludeLaunchClamps;
            ctx.EngineShroudOptions = engineShroudOptions ?? new List<EngineShroudOption>();
            ctx.DisabledRendererKeys = disabledRendererKeys ?? new HashSet<string>();
            ctx.DisabledRenderers = disabledRenderers ?? new HashSet<Renderer>();
            ctx.DisabledRendererTransforms = disabledRendererTransforms ?? new HashSet<Transform>();
            ctx.OutputDir = outputDir;
            ctx.ViewerExePath = viewerExePath;
            ctx.ViewerStartMenuSearch = viewerStartMenuSearch;
            ctx.UseStartMenuSearchForViewer = useStartMenuSearchForViewer;
            ctx.OpenViewerAfterExport = openViewerAfterExport;
            ctx.Status = status;
            ctx.Progress = progress;
            ctx.CancelRequested = cancelRequested;
            ctx.Complete = complete;
            ctx.Stage = ExportStage.Validate;

            SetProgress(ctx, 0);

            while (ctx.Stage != ExportStage.Finished &&
                   ctx.Stage != ExportStage.Failed &&
                   ctx.Stage != ExportStage.Cancelled)
            {
                if (IsCancelRequested(ctx))
                {
                    Status(ctx, "Cancel requested. Restoring engine shroud/fairing visibility.");
                    SafeRestoreShrouds(ctx);
                    ctx.Stage = ExportStage.Cancelled;
                    break;
                }

                try
                {
                    RunStage(ctx);
                }
                catch (Exception ex)
                {
                    Log.Error("Export failed during stage " + ctx.Stage + ": " + ex);

                    Status(ctx, "Failed during stage: " + ctx.Stage + ". See KSP log.");
                    ScreenMessages.PostScreenMessage("Export failed, see log", 6f, ScreenMessageStyle.UPPER_CENTER);

                    SafeRestoreShrouds(ctx);
                    ctx.Stage = ExportStage.Failed;
                }

                yield return null;
            }

            if (ctx.Stage == ExportStage.Finished)
            {
                SetProgress(ctx, TOTAL_PROGRESS_STEPS);
                Status(ctx, "Export successful.");
                ScreenMessages.PostScreenMessage("Craft export complete", 5f, ScreenMessageStyle.UPPER_CENTER);
            }
            else if (ctx.Stage == ExportStage.Cancelled)
            {
                Status(ctx, "Export cancelled.");
                ScreenMessages.PostScreenMessage("Craft export cancelled", 5f, ScreenMessageStyle.UPPER_CENTER);
            }

            if (ctx.Complete != null)
                ctx.Complete();
        }

        private static void RunStage(ExportContext ctx)
        {
            switch (ctx.Stage)
            {
                case ExportStage.Validate:
                    StageValidate(ctx);
                    break;
                case ExportStage.PrepareOutput:
                    StagePrepareOutput(ctx);
                    break;
                case ExportStage.RefreshEngineList:
                    StageRefreshEngineList(ctx);
                    break;
                case ExportStage.SetShroudVisibility:
                    StageSetShroudVisibility(ctx);
                    break;
                case ExportStage.WritePartDiagnostics:
                    StageWritePartDiagnostics(ctx);
                    break;
                case ExportStage.CollectMeshes:
                    StageCollectMeshes(ctx);
                    break;
                case ExportStage.RemoveBadTriangles:
                    StageRemoveBadTriangles(ctx);
                    break;
                case ExportStage.RemoveDuplicateTriangles:
                    StageRemoveDuplicateTriangles(ctx);
                    break;
                case ExportStage.WriteStl:
                    StageWriteStl(ctx);
                    break;
                case ExportStage.WriteStlInstructions:
                    StageWriteStlInstructions(ctx);
                    break;
                case ExportStage.WriteObj:
                    StageWriteObj(ctx);
                    break;
                case ExportStage.WriteObjInstructions:
                    StageWriteObjInstructions(ctx);
                    break;
                case ExportStage.Write3mf:
                    StageWrite3mf(ctx);
                    break;
                case ExportStage.Write3mfInstructions:
                    StageWrite3mfInstructions(ctx);
                    break;
                case ExportStage.WriteStp:
                    StageWriteStp(ctx);
                    break;
                case ExportStage.WriteStpInstructions:
                    StageWriteStpInstructions(ctx);
                    break;
                case ExportStage.RestoreShroudVisibility:
                    StageRestoreShroudVisibility(ctx);
                    break;
                case ExportStage.OpenViewer:
                    StageOpenViewer(ctx);
                    break;
            }
        }

        private static void StageValidate(ExportContext ctx)
        {
            SetProgress(ctx, 0);
            Status(ctx, HighLogic.LoadedSceneIsFlight ? "Validating active flight vessel." : "Validating editor craft.");

            string sourceName;
            string sceneDescription;
            if (!ScenePartUtilities.TryGetCurrentParts(out ctx.Parts, out sourceName, out sceneDescription))
                throw new InvalidOperationException("No editor craft or active flight vessel loaded.");

            ctx.CraftName = SanitizeFileName(sourceName);
            if (string.IsNullOrEmpty(ctx.CraftName))
                ctx.CraftName = HighLogic.LoadedSceneIsFlight ? "ActiveVessel" : "UnnamedCraft";

            ctx.SceneDescription = sceneDescription;

            Status(ctx, sceneDescription + " validated. Parts: " + ctx.Parts.Count);

            // Must happen here, before any shroud/visibility mutation runs anywhere in the
            // pipeline, so the snapshot reflects KSP's own already-applied variant state.
            HashSet<Part> variantParts = VariantSnapshotUtilities.BuildPartsWithVariantModules(ctx.Parts);
            ctx.OriginallyHiddenVariantTransforms = VariantSnapshotUtilities.SnapshotOriginallyHiddenTransforms(
                ctx.Parts, variantParts, ctx.Status);

            ctx.Stage = ExportStage.PrepareOutput;
            SetProgress(ctx, 1);
        }

        private static void StagePrepareOutput(ExportContext ctx)
        {
            Status(ctx, "Preparing output folder.");

            if (string.IsNullOrEmpty(ctx.OutputDir))
                ctx.OutputDir = Utils.GetDefaultOutputDirectory;

            Directory.CreateDirectory(ctx.OutputDir);

            string suffix = HighLogic.LoadedSceneIsFlight ? "_flight_current_printable" : "_printable";

            ctx.StlFile = Path.Combine(ctx.OutputDir, ctx.CraftName + suffix + ".stl");
            ctx.ObjFile = Path.Combine(ctx.OutputDir, ctx.CraftName + suffix + ".obj");
            ctx.ThreeMfFile = Path.Combine(ctx.OutputDir, ctx.CraftName + suffix + ".3mf");
            ctx.StpFile = Path.Combine(ctx.OutputDir, ctx.CraftName + suffix + ".stp");
            ctx.MeshDumpFile = Path.Combine(ctx.OutputDir, ctx.CraftName + suffix + ".k3dm");

            Status(ctx, "Output folder ready: " + ctx.OutputDir);
#if EXPORT_EXCLUSION_RULE_DEFINED
            ctx.ExclusionRules = ExportExclusionUtilities.LoadRules(ctx.Status);
#endif
            ctx.MeshDiagnosticFile = Path.Combine(ctx.OutputDir, ctx.CraftName + suffix + "_mesh_diagnostics.txt");
            ctx.PartDiagnosticFile = Path.Combine(ctx.OutputDir, ctx.CraftName + suffix + "_part_diagnostics.txt");

            ctx.Stage = ExportStage.RefreshEngineList;
            SetProgress(ctx, 2);
        }

        private static void StageRefreshEngineList(ExportContext ctx)
        {
            Status(ctx, "Scanning engines.");
            if (ctx.EngineShroudOptions == null || ctx.EngineShroudOptions.Count == 0)
                ctx.EngineShroudOptions = EngineUtilities.GetEngineOptions(ctx.Parts, ctx.ShowShrouds);
            Status(ctx, "Engines found: " + ctx.EngineShroudOptions.Count);
            ctx.Stage = ExportStage.SetShroudVisibility;
            SetProgress(ctx, 3);
        }

        private static void StageSetShroudVisibility(ExportContext ctx)
        {
            Status(ctx, ctx.ShowShrouds ? "Showing engine shrouds/fairings." : "Hiding engine shrouds/fairings.");
            ShroudUtilities.SetShroudVisibility(
                ctx.Parts,
                ctx.ShowShrouds,
                ctx.EngineShroudOptions,
                ctx.SavedShroudStates,
                ctx.ShroudTransformsToSkip);
            Status(ctx, "Shroud/fairing states changed: " + ctx.SavedShroudStates.Count);

            ctx.InactiveVariantTransformsToSkip = ActiveVariantUtilities.BuildInactiveVariantTransformSet(ctx.Parts, ctx.Status);

            ctx.Stage = ExportStage.WritePartDiagnostics;
            SetProgress(ctx, 4);
        }


        private static void StageWritePartDiagnostics(ExportContext ctx)
        {
            Status(ctx, "Writing part/GameObject/variant diagnostics.");
            PartDiagnosticsWriter.Write(ctx.PartDiagnosticFile, ctx.Parts, ctx.SceneDescription);
            Status(ctx, "Part diagnostics written: " + ctx.PartDiagnosticFile);
            ctx.Stage = ExportStage.CollectMeshes;
            SetProgress(ctx, 5);
        }

        private static void StageCollectMeshes(ExportContext ctx)
        {
            Status(ctx, "Collecting meshes.");
            ctx.Triangles = MeshCollector.BuildTriangleList(
                ctx.Parts,
                ctx.UserScale,
                ctx.Status,
                ctx.ShowShrouds,
                ctx.ExcludeLaunchClamps,
                ctx.EngineShroudOptions,
                ctx.ShroudTransformsToSkip,
                ctx.InactiveVariantTransformsToSkip,
                ctx.OriginallyHiddenVariantTransforms,
#if EXPORT_EXCLUSION_RULE_DEFINED
                ctx.ExclusionRules,
#endif
                ctx.DisabledRendererKeys,
                ctx.DisabledRenderers,
                ctx.DisabledRendererTransforms,
                ctx.MeshDiagnostics,
                ctx.PartNames);
            Status(ctx, "Collected raw triangles: " + ctx.Triangles.Count);
            WriteMeshDiagnostics(ctx);
            ctx.Stage = ExportStage.RemoveBadTriangles;
            SetProgress(ctx, 6);
        }

        private static void StageRemoveBadTriangles(ExportContext ctx)
        {
            Status(ctx, "Removing invalid and degenerate triangles.");
            MeshCleanup.RemoveBadTriangles(ctx.Triangles);
            Status(ctx, "Remaining triangles: " + ctx.Triangles.Count);
            ctx.Stage = ExportStage.RemoveDuplicateTriangles;
            SetProgress(ctx, 7);
        }

        private static void StageRemoveDuplicateTriangles(ExportContext ctx)
        {
            Status(ctx, "Removing duplicate triangles.");
            MeshCleanup.RemoveDuplicateTriangles(ctx.Triangles);
            Status(ctx, "Remaining triangles: " + ctx.Triangles.Count);

            // Dumped HERE, at the tail of cleanup, because this is the exact triangle soup every
            // writer downstream receives. Dumping any earlier would capture geometry the writers
            // never actually see, and the whole point of the dump is to reproduce their input.
            if (ctx.DumpMesh)
                WriteMeshDump(ctx);

            if (ctx.ExportStl)
                ctx.Stage = ExportStage.WriteStl;
            else if (ctx.ExportObj)
                ctx.Stage = ExportStage.WriteObj;
            else if (ctx.Export3mf)
                ctx.Stage = ExportStage.Write3mf;
            else if (ctx.ExportStp)
                ctx.Stage = ExportStage.WriteStp;
            else
                ctx.Stage = ExportStage.RestoreShroudVisibility;

            SetProgress(ctx, 8);
        }

        private static void StageWriteStl(ExportContext ctx)
        {
            Status(ctx, "Writing binary STL.");
            BinaryStlWriter.Write(ctx.StlFile, ctx.Triangles);
            Status(ctx, "STL written: " + ctx.StlFile);
            ctx.Stage = ExportStage.WriteStlInstructions;
            SetProgress(ctx, 9);
        }

        private static void StageWriteStlInstructions(ExportContext ctx)
        {
            Status(ctx, "Writing STL instructions.");
            InstructionWriter.Write(
                Path.Combine(ctx.OutputDir, ctx.CraftName + "_printable_STL_instructions.txt"),
                "STL",
                ctx.StlFile,
                ctx.UserScale);
            Status(ctx, "STL instructions written.");

            if (ctx.ExportObj)
                ctx.Stage = ExportStage.WriteObj;
            else if (ctx.Export3mf)
                ctx.Stage = ExportStage.Write3mf;
            else if (ctx.ExportStp)
                ctx.Stage = ExportStage.WriteStp;
            else
                ctx.Stage = ExportStage.RestoreShroudVisibility;

            SetProgress(ctx, 10);
        }

        private static void StageWriteObj(ExportContext ctx)
        {
            Status(ctx, "Writing OBJ.");
            ObjWriter.Write(ctx.ObjFile, ctx.Triangles);
            Status(ctx, "OBJ written: " + ctx.ObjFile);
            ctx.Stage = ExportStage.WriteObjInstructions;
            SetProgress(ctx, 11);
        }

        private static void StageWriteObjInstructions(ExportContext ctx)
        {
            Status(ctx, "Writing OBJ instructions.");
            InstructionWriter.Write(
                Path.Combine(ctx.OutputDir, ctx.CraftName + "_printable_OBJ_instructions.txt"),
                "OBJ",
                ctx.ObjFile,
                ctx.UserScale);
            Status(ctx, "OBJ instructions written.");

            if (ctx.Export3mf)
                ctx.Stage = ExportStage.Write3mf;
            else if (ctx.ExportStp)
                ctx.Stage = ExportStage.WriteStp;
            else
                ctx.Stage = ExportStage.RestoreShroudVisibility;

            SetProgress(ctx, 12);
        }

        private static void StageWrite3mf(ExportContext ctx)
        {
            Status(ctx, ctx.ThreeMfPerPart
                ? "Writing 3MF (one object per part, with color palette)."
                : "Writing 3MF.");
            ThreeMfWriter.Write(ctx.ThreeMfFile, ctx.Triangles, ctx.CraftName, ctx.UserScale, ctx.ThreeMfPerPart, ctx.PartNames);
            Status(ctx, "3MF written: " + ctx.ThreeMfFile);
            ctx.Stage = ExportStage.Write3mfInstructions;
            SetProgress(ctx, 13);
        }

        private static void StageWrite3mfInstructions(ExportContext ctx)
        {
            Status(ctx, "Writing 3MF instructions.");
            InstructionWriter.Write(
                Path.Combine(ctx.OutputDir, ctx.CraftName + "_printable_3MF_instructions.txt"),
                "3MF",
                ctx.ThreeMfFile,
                ctx.UserScale);
            Status(ctx, "3MF instructions written.");

            if (ctx.ExportStp)
                ctx.Stage = ExportStage.WriteStp;
            else
                ctx.Stage = ExportStage.RestoreShroudVisibility;

            SetProgress(ctx, 14);
        }

        private static void StageWriteStp(ExportContext ctx)
        {
            Status(ctx, "Writing STEP. A faceted B-rep is large; this is the slow stage.");
            StepWriter.Write(ctx.StpFile, ctx.Triangles, ctx.CraftName, ctx.UserScale, ctx.Status);
            Status(ctx, "STEP written: " + ctx.StpFile);
            ctx.Stage = ExportStage.WriteStpInstructions;
            SetProgress(ctx, 15);
        }

        private static void StageWriteStpInstructions(ExportContext ctx)
        {
            Status(ctx, "Writing STEP instructions.");
            InstructionWriter.Write(
                Path.Combine(ctx.OutputDir, ctx.CraftName + "_printable_STEP_instructions.txt"),
                "STEP",
                ctx.StpFile,
                ctx.UserScale);
            Status(ctx, "STEP instructions written.");
            ctx.Stage = ExportStage.RestoreShroudVisibility;
            SetProgress(ctx, 16);
        }

        private static void StageRestoreShroudVisibility(ExportContext ctx)
        {
            Status(ctx, "Restoring engine shroud/fairing visibility.");
            ShroudUtilities.RestoreShroudVisibility(ctx.SavedShroudStates);
            Status(ctx, "Engine shroud/fairing visibility restored.");
            ctx.Stage = ExportStage.OpenViewer;
            SetProgress(ctx, 17);
        }

        private static void StageOpenViewer(ExportContext ctx)
        {
            if (!ctx.OpenViewerAfterExport)
            {
                ctx.Stage = ExportStage.Finished;
                SetProgress(ctx, 18);
                return;
            }

            // Preference order: STL first (every viewer and slicer understands it), then 3MF,
            // then OBJ. If the user only exported 3MF, that is what gets opened.
            //
            // STEP is deliberately not a candidate: slicers generally cannot read it, so
            // handing one a .stp would just pop an error dialog.
            string modelFile = null;
            if (ctx.ExportStl && !string.IsNullOrEmpty(ctx.StlFile) && File.Exists(ctx.StlFile))
                modelFile = ctx.StlFile;
            else if (ctx.Export3mf && !string.IsNullOrEmpty(ctx.ThreeMfFile) && File.Exists(ctx.ThreeMfFile))
                modelFile = ctx.ThreeMfFile;
            else if (ctx.ExportObj && !string.IsNullOrEmpty(ctx.ObjFile) && File.Exists(ctx.ObjFile))
                modelFile = ctx.ObjFile;

            if (string.IsNullOrEmpty(modelFile))
            {
                Status(ctx, "Viewer was not opened because no exported model file was found.");
                ctx.Stage = ExportStage.Finished;
                SetProgress(ctx, 18);
                return;
            }

            Log.Info("StageOpenViewer: Launching viewer for model file: " + modelFile);
            Log.Info($"StageOpenViewer, ctx.UseStartMenuSearchForViewer: {ctx.UseStartMenuSearchForViewer}");
            Log.Info($"StageOpenViewer, ctx.ViewerStartMenuSearch: {ctx.ViewerStartMenuSearch}");
            Log.Info($"StageOpenViewer, ctx.ViewerExePath: {ctx.ViewerExePath}");

            if (ctx.UseStartMenuSearchForViewer)
                StartMenuLauncher.SearchAndLaunch(ctx.ViewerStartMenuSearch, modelFile);
            else
                ViewerLauncher.Open(ctx.ViewerExePath, modelFile, ctx.Status);

            ctx.Stage = ExportStage.Finished;
            SetProgress(ctx, 18);
        }

        private static void WriteMeshDump(ExportContext ctx)
        {
            // Swallowed like WriteMeshDiagnostics: a diagnostic dump failing must never take a
            // real export down with it. The user asked for an STL; they get their STL.
            try
            {
                if (string.IsNullOrEmpty(ctx.MeshDumpFile))
                    return;

                MeshDumpWriter.Write(
                    ctx.MeshDumpFile,
                    ctx.Triangles,
                    ctx.CraftName,
                    ctx.UserScale,
                    ctx.PartNames);

                Status(ctx, "Mesh dump written: " + ctx.MeshDumpFile);
            }
            catch (Exception e)
            {
                Status(ctx, "Mesh dump failed (export continues): " + e.Message);
            }
        }

        private static void WriteMeshDiagnostics(ExportContext ctx)
        {
            try
            {
                if (ctx.MeshDiagnostics == null || ctx.MeshDiagnostics.Count == 0 || string.IsNullOrEmpty(ctx.MeshDiagnosticFile))
                    return;

                File.WriteAllLines(ctx.MeshDiagnosticFile, ctx.MeshDiagnostics.ToArray());
                Status(ctx, "Mesh diagnostics written: " + ctx.MeshDiagnosticFile);
            }
            catch (Exception ex)
            {
                Log.Error("Failed writing mesh diagnostics: " + ex);
                Status(ctx, "Failed writing mesh diagnostics. See KSP log.");
            }
        }

        private static void SafeRestoreShrouds(ExportContext ctx)
        {
            try
            {
                ShroudUtilities.RestoreShroudVisibility(ctx.SavedShroudStates);
            }
            catch (Exception restoreEx)
            {
                Log.Error("Failed restoring shrouds: " + restoreEx);
            }
        }

        private static bool IsCancelRequested(ExportContext ctx)
        {
            return ctx.CancelRequested != null && ctx.CancelRequested();
        }

        private static void Status(ExportContext ctx, string msg)
        {
            if (ctx.Status != null)
                ctx.Status(msg);
        }

        private static void SetProgress(ExportContext ctx, int completedSteps)
        {
            if (ctx.Progress != null)
                ctx.Progress(Mathf.Clamp01((float)completedSteps / (float)TOTAL_PROGRESS_STEPS));
        }

        private static string SanitizeFileName(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "UnnamedCraft";

            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            return s.Replace(" ", "_");
        }
    }
}
