using ClickThroughFix;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;

namespace Kerbal_3D_Exporter
{
#if false
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class CraftPrintExporterWindowEditor : CraftPrintExporterWindow
    {
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CraftPrintExporterWindowFlight : CraftPrintExporterWindow
    {
    }
#endif

    public class CraftPrintExporterWindow : MonoBehaviour
    {
        private const float MIN_EXPORT_SCALE = 0.001f;

        private const int WINDOW_WIDTH = 760;
        private const int WINDOW_HEIGHT = 760;
        private const int HALF_WINDOW_WIDTH = WINDOW_WIDTH / 2;

        private Rect windowRect = new Rect(300, 80, WINDOW_WIDTH, WINDOW_HEIGHT);
        private bool showWindow;
        private bool exporting;
        private bool cancelRequested;

        private string scaleText = "0.01";
        private bool exportStl = true;
        private bool exportObj;
        private bool export3mf;
        private bool threeMfPerPart = true;
        private bool exportStp;
#if false
        private bool dumpMesh;
#endif
        private bool showShrouds = true;
        private bool excludeLaunchClamps = true;

        // KSP is Y-up, every slicer is Z-up. Defaulting to Upright means a rocket that stands
        // up in the VAB stands up in the slicer, which is what everybody expects and almost
        // never what they used to get.
        private ExportOrientation orientation = ExportOrientation.UprightZUp;
        private string viewerExePathText = string.Empty;
        private string viewerStartMenuSearchText = string.Empty;
        private bool useStartMenuSearchForViewer = false;
        private bool openViewerAfterExport = true;
        private float exportProgress;

        private Vector2 statusScroll;
        private Vector2 engineScroll;
        private Vector2 rendererScroll;
        private readonly List<string> statusLines = new List<string>();
        private List<EngineShroudOption> engineOptions = new List<EngineShroudOption>();
        private List<RendererDiagnosticEntry> rendererDiagnostics = new List<RendererDiagnosticEntry>();
        private bool showRendererDiagnostics;
        private bool showEngineList;
        private string rendererFilterText = string.Empty;
        private readonly Dictionary<string, bool> rendererIncludeByKey = new Dictionary<string, bool>();

        private SlicerConfiguration slicerConfig;
        private SlicerConfigurationWindow slicerConfigWindow;

        public void Start()
        {
            // Initialize slicer configuration
            slicerConfig = new SlicerConfiguration();
            slicerConfig.LoadConfiguration();

            // Use saved slicer path if available
            if (slicerConfig.IsConfigured())
            {
                viewerExePathText = slicerConfig.SelectedSlicerPath;
                viewerStartMenuSearchText = slicerConfig.SelectedSlicerStartMenuSearch;
                useStartMenuSearchForViewer = slicerConfig.UseStartMenuSearchForSlicer;
            }
        }

        public void OnDestroy()
        {
            RendererHighlightUtility.ClearHighlight();
        }

        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;
            GUI.enabled = SlicerConfigurationWindow.showWindow == false;
            if (showWindow)
                windowRect = ClickThruBlocker.GUILayoutWindow(Utils.ExportWinID, windowRect, DrawWindow, "Kerbal 3D Exporter", Utils.solidWindowStyle);
            GUI.enabled = true;
        }

        private void DrawWindow(int id)
        {
            using (new GUILayout.VerticalScope())
            {
                GUI.enabled = !exporting;

                GUILayout.Label("Export Scale");
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Scale:", GUILayout.Width(80));
                    scaleText = GUILayout.TextField(scaleText, GUILayout.Width(100));
                    GUILayout.Label("Minimum: " + MIN_EXPORT_SCALE.ToString("0.###", CultureInfo.InvariantCulture));
                }
                GUILayout.Space(8);

                //GUILayout.Label("STL Viewer / Slicer Executable");
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Configure STL Viewer / Slicer Executable", GUILayout.Width(500)))
                    {
                        OpenSlicerConfiguration();
                    }
                    GUILayout.FlexibleSpace();
                }

                // Display configured slicer info
                if (slicerConfig != null && slicerConfig.IsConfigured())
                {
                    GUILayout.Label($"<color=lime>Configured: {slicerConfig.SelectedSlicerName}</color>",
                        Utils.labelRichTextStyle);
                }

                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.VerticalScope())
                    {
                        exportStl = GUILayout.Toggle(exportStl, "Export STL");
                        exportObj = GUILayout.Toggle(exportObj, "Export OBJ");
                        exportStp = GUILayout.Toggle(exportStp, "Export STEP (.stp) - CAD, not printing. Large files.");
#if false
                        dumpMesh = GUILayout.Toggle(dumpMesh, "Dump mesh (.k3dm) - debug / bug reports, not printable");
#endif
                        export3mf = GUILayout.Toggle(export3mf, "Export 3MF");
                    }

                    using (new GUILayout.VerticalScope())
                    {
                        openViewerAfterExport = GUILayout.Toggle(openViewerAfterExport, "Open viewer after export completes");
                        bool newShowShrouds = GUILayout.Toggle(showShrouds, "Show engine shrouds / fairings by default");
                        if (newShowShrouds != showShrouds)
                        {
                            showShrouds = newShowShrouds;
                            SetAllEngineShroudToggles(showShrouds);
                        }

                        excludeLaunchClamps = GUILayout.Toggle(excludeLaunchClamps, "Exclude launch clamps");

                    }
                }

                if (export3mf)
                {
                    using (new GUILayout.HorizontalScope(GUILayout.Width(HALF_WINDOW_WIDTH / 2 - 40)))
                    {
                        GUILayout.Space(20);
                        threeMfPerPart = GUILayout.Toggle(threeMfPerPart, "Separate object + color per part", GUILayout.MaxWidth(HALF_WINDOW_WIDTH - 40));
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(40);
                        GUILayout.Label("(colors are decorative, not real part colors)");
                    }
                }

                GUILayout.Space(4);
                GUILayout.Label("Orientation in the exported file");
                DrawOrientationSelector();



                GUILayout.Space(10);

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(showEngineList ? "Hide Engine List" : "Show Engine List"))
                        showEngineList = !showEngineList;

                    if (GUILayout.Button(showRendererDiagnostics ? "Hide Renderer Diagnostics" : "Show Renderer Diagnostics"))
                    {
                        showRendererDiagnostics = !showRendererDiagnostics;
                        if (showRendererDiagnostics)
                            RefreshRendererDiagnostics(true);
                        else
                            RendererHighlightUtility.ClearHighlight();
                    }

                    if (GUILayout.Button("Refresh Renderers"))
                        RefreshRendererDiagnostics(true);
                }
                if (showEngineList)
                {
                    GUILayout.Label(HighLogic.LoadedSceneIsFlight ? "Engines in active vessel:" : "Engines in vessel:");
                    GUILayout.Label("Toggle each engine to include or exclude its shroud/fairing from the export.");

                    engineScroll = GUILayout.BeginScrollView(engineScroll, GUILayout.Height(145));
                    if (engineOptions.Count == 0)
                    {
                        GUILayout.Label("No engines found, or craft not scanned yet.");
                    }
                    else
                    {
                        for (int i = 0; i < engineOptions.Count; i++)
                        {
                            EngineShroudOption option = engineOptions[i];
                            if (option == null)
                                continue;

                            using (new GUILayout.HorizontalScope())
                            {
                                option.ShowShroud = GUILayout.Toggle(option.ShowShroud, "", GUILayout.Width(115));
                                GUILayout.Label(option.DisplayName);
                            }
                        }
                    }
                    GUILayout.EndScrollView();

                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Refresh Engine List"))
                            RefreshEngineList(true);

                        if (GUILayout.Button("All Shrouds On"))
                            SetAllEngineShroudToggles(true);

                        if (GUILayout.Button("All Shrouds Off"))
                            SetAllEngineShroudToggles(false);
                    }
                }

                GUILayout.Space(8);

                if (showRendererDiagnostics)
                    DrawRendererDiagnostics();

                GUILayout.Space(8);
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Export", GUILayout.Width(120)))
                        StartExport();
                    GUILayout.FlexibleSpace();
                }

                GUI.enabled = true;

                GUILayout.Space(10);

                DrawProgressBar();

                if (exporting)
                {
                    GUILayout.Label("Export in progress... input disabled until complete.");
                    if (GUILayout.Button("Cancel Export"))
                    {
                        cancelRequested = true;
                        AddStatus("Cancel requested. Waiting for current stage to finish.");
                    }
                }
                else
                {
                    GUILayout.Label("Status:");
                }

                statusScroll = GUILayout.BeginScrollView(statusScroll, GUILayout.Height(150));
                foreach (string line in statusLines)
                    GUILayout.Label(line);
                GUILayout.EndScrollView();
                GUILayout.FlexibleSpace();
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Close", GUILayout.Width(120)))
                        Close();
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(10);
            }
            if (!exporting)
                GUI.DragWindow();
        }

        private void DrawProgressBar()
        {
            GUILayout.Label("Progress: " + Mathf.RoundToInt(exportProgress * 100f) + "%");

            Rect r = GUILayoutUtility.GetRect(100f, 18f, GUILayout.ExpandWidth(true));
            GUI.Box(r, string.Empty);

            Rect fill = new Rect(r.x + 2f, r.y + 2f, Mathf.Max(0f, (r.width - 4f) * Mathf.Clamp01(exportProgress)), r.height - 4f);
            if (fill.width > 0f)
                GUI.Box(fill, string.Empty);
        }

        private void DrawRendererDiagnostics()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("Renderer Diagnostics");
                GUILayout.Label("Hover a renderer row to highlight it (colliders have nothing visible to highlight). Uncheck Export to exclude a row from the model.");

                GUILayout.BeginHorizontal();
                GUILayout.Label("Filter:", GUILayout.Width(50));
                rendererFilterText = GUILayout.TextField(rendererFilterText);
                GUILayout.EndHorizontal();

                GUILayout.Label("Renderers & colliders: " + rendererDiagnostics.Count);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Disable colliders without a material"))
                    SetIncludeForCollidersWithoutMaterial(false);
                if (GUILayout.Button("Re-enable all colliders"))
                    SetIncludeForCollidersWithoutMaterial(true);
                GUILayout.EndHorizontal();

                string filter = rendererFilterText != null ? rendererFilterText.Trim().ToLowerInvariant() : string.Empty;
                List<RendererDiagnosticEntry> visibleEntries = new List<RendererDiagnosticEntry>();

                for (int i = 0; i < rendererDiagnostics.Count; i++)
                {
                    RendererDiagnosticEntry entry = rendererDiagnostics[i];
                    if (entry == null)
                        continue;

                    string search = (entry.PartName + " " + entry.PartTitle + " " + entry.ActiveVariant + " " +
                        entry.RendererName + " " + entry.RendererType + " " + entry.Path + " " +
                        entry.MeshName + " " + entry.Materials + " " + entry.RendererActiveInHierarchy).ToLowerInvariant();

                    if (filter.Length > 0 && !search.Contains(filter))
                        continue;

                    ApplyRendererIncludeState(entry);
                    visibleEntries.Add(entry);
                }

                Renderer hoveredRenderer = null;
                RendererDiagnosticEntry hoveredEntry = null;

                const float rowHeight = 22f;
                const float headerHeight = 24f;
                const float listHeight = 220f;

                const float colExport = 60f;
                const float colPart = 210f;
                const float colVariant = 130f;
                const float colRenderer = 190f;
                const float colMesh = 210f;
                const float colType = 115f;
                const float colActive = 80f;
                const float colPath = 700f;
                const float colMaterials = 360f;

                float contentWidth = colExport + colPart + colVariant + colRenderer + colMesh +
                    colType + colActive + colPath + colMaterials + 20f;
                float contentHeight = Mathf.Max(1f, visibleEntries.Count * rowHeight);

                // Header viewport.  It uses the same horizontal offset as the scrollable body,
                // so columns stay aligned while the renderer table is scrolled sideways.
                Rect headerViewport = GUILayoutUtility.GetRect(1f, headerHeight, GUILayout.ExpandWidth(true));
                GUI.Box(headerViewport, string.Empty);
                GUI.BeginGroup(headerViewport);
                DrawRendererHeaderRow(new Rect(-rendererScroll.x, 0f, contentWidth, headerHeight),
                    colExport, colPart, colVariant, colRenderer, colMesh, colType, colActive, colPath, colMaterials);
                GUI.EndGroup();

                Rect viewportRect = GUILayoutUtility.GetRect(1f, listHeight, GUILayout.ExpandWidth(true));
                Rect contentRect = new Rect(0f, 0f, Mathf.Max(contentWidth, viewportRect.width - 20f), contentHeight);

                Event evt = Event.current;
                Vector2 mouse = evt != null ? evt.mousePosition : Vector2.zero;
                bool mouseInsideViewport = viewportRect.Contains(mouse);
                int hoveredIndex = -1;

                if (mouseInsideViewport)
                {
                    float contentY = mouse.y - viewportRect.y + rendererScroll.y;
                    if (contentY >= 0f && contentY < visibleEntries.Count * rowHeight)
                        hoveredIndex = Mathf.FloorToInt(contentY / rowHeight);
                }

                rendererScroll = GUI.BeginScrollView(viewportRect, rendererScroll, contentRect, true, true);

                for (int i = 0; i < visibleEntries.Count; i++)
                {
                    RendererDiagnosticEntry entry = visibleEntries[i];
                    Rect row = new Rect(0f, i * rowHeight, contentRect.width, rowHeight);

                    bool hover = i == hoveredIndex;
                    if (hover)
                    {
                        hoveredRenderer = entry.Renderer;
                        hoveredEntry = entry;
                        GUI.Box(row, string.Empty);
                    }

                    DrawRendererTableRow(row, entry,
                        colExport, colPart, colVariant, colRenderer, colMesh, colType, colActive, colPath, colMaterials);
                }

                GUI.EndScrollView();

                RendererHighlightUtility.SetHoveredRenderer(hoveredRenderer);

                GUILayout.Space(4);
                DrawHoveredRendererDetailsPane(hoveredEntry);

            }
        }

        private void DrawRendererHeaderRow(
            Rect row,
            float colExport,
            float colPart,
            float colVariant,
            float colRenderer,
            float colMesh,
            float colType,
            float colActive,
            float colPath,
            float colMaterials)
        {
            float x = row.x + 4f;
            DrawHeaderCell(new Rect(x, row.y + 3f, colExport, row.height - 6f), "Export");
            x += colExport;
            DrawHeaderCell(new Rect(x, row.y + 3f, colPart, row.height - 6f), "Part");
            x += colPart;
            DrawHeaderCell(new Rect(x, row.y + 3f, colVariant, row.height - 6f), "Variant");
            x += colVariant;
            DrawHeaderCell(new Rect(x, row.y + 3f, colRenderer, row.height - 6f), "Renderer");
            x += colRenderer;
            DrawHeaderCell(new Rect(x, row.y + 3f, colMesh, row.height - 6f), "Mesh");
            x += colMesh;
            DrawHeaderCell(new Rect(x, row.y + 3f, colType, row.height - 6f), "Type");
            x += colType;
            DrawHeaderCell(new Rect(x, row.y + 3f, colActive, row.height - 6f), "Active");
            x += colActive;
            DrawHeaderCell(new Rect(x, row.y + 3f, colPath, row.height - 6f), "Path");
            x += colPath;
            DrawHeaderCell(new Rect(x, row.y + 3f, colMaterials, row.height - 6f), "Materials");
        }

        private void DrawRendererTableRow(
            Rect row,
            RendererDiagnosticEntry entry,
            float colExport,
            float colPart,
            float colVariant,
            float colRenderer,
            float colMesh,
            float colType,
            float colActive,
            float colPath,
            float colMaterials)
        {
            float x = row.x + 4f;
            Rect toggleRect = new Rect(x + 16f, row.y + 2f, 20f, row.height - 4f);
            bool include = GUI.Toggle(toggleRect, entry.IncludeInExport, string.Empty);
            if (include != entry.IncludeInExport)
            {
                entry.IncludeInExport = include;
                if (!string.IsNullOrEmpty(entry.Key))
                    rendererIncludeByKey[entry.Key] = include;
            }

            x += colExport;
            DrawCell(new Rect(x, row.y + 2f, colPart - 4f, row.height - 4f), entry.PartTitle + " [" + entry.PartName + "]");
            x += colPart;
            DrawCell(new Rect(x, row.y + 2f, colVariant - 4f, row.height - 4f), entry.ActiveVariant);
            x += colVariant;
            DrawCell(new Rect(x, row.y + 2f, colRenderer - 4f, row.height - 4f), entry.RendererName);
            x += colRenderer;
            DrawCell(new Rect(x, row.y + 2f, colMesh - 4f, row.height - 4f), entry.MeshName);
            x += colMesh;
            DrawCell(new Rect(x, row.y + 2f, colType - 4f, row.height - 4f), entry.RendererType);
            x += colType;
            DrawCell(new Rect(x, row.y + 2f, colActive - 4f, row.height - 4f), entry.InactiveVariant ? "VariantOff" : (entry.RendererActiveInHierarchy ? "Yes" : "No"));
            x += colActive;
            DrawCell(new Rect(x, row.y + 2f, colPath - 4f, row.height - 4f), entry.Path);
            x += colPath;
            DrawCell(new Rect(x, row.y + 2f, colMaterials - 4f, row.height - 4f), entry.Materials);
        }

        private static void DrawHeaderCell(Rect rect, string text)
        {
            DrawClippedLabel(rect, text, GUI.skin.label);
        }

        private static void DrawCell(Rect rect, string text)
        {
            DrawClippedLabel(rect, text ?? string.Empty, GUI.skin.label);
        }

        private static void DrawClippedLabel(Rect rect, string text, GUIStyle sourceStyle)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            GUI.BeginGroup(rect);

            GUIStyle clippedStyle = new GUIStyle(sourceStyle);
            clippedStyle.clipping = TextClipping.Clip;
            clippedStyle.wordWrap = false;
            clippedStyle.alignment = TextAnchor.MiddleLeft;

            GUI.Label(new Rect(0f, 0f, rect.width, rect.height), text ?? string.Empty, clippedStyle);

            GUI.EndGroup();
        }

        private void DrawHoveredRendererDetailsPane(RendererDiagnosticEntry hoveredEntry)
        {
            const float detailsHeight = 118f;
            Rect outer = GUILayoutUtility.GetRect(1f, detailsHeight, GUILayout.ExpandWidth(true));

            GUI.Box(outer, string.Empty);

            // Clip everything to the details pane so it cannot be painted outside the pane.
            GUI.BeginGroup(outer);

            Rect inner = new Rect(6f, 4f, Mathf.Max(10f, outer.width - 12f), outer.height - 8f);
            float y = inner.y;
            float lineHeight = 18f;

            GUI.Label(new Rect(inner.x, y, inner.width, lineHeight), "Hovered renderer details:");
            y += lineHeight;

            if (hoveredEntry != null)
            {
                GUI.Label(new Rect(inner.x, y, inner.width, lineHeight),
                    "Part GO: " + hoveredEntry.PartGameObjectName +
                    " | ActiveSelf=" + hoveredEntry.PartActiveSelf +
                    " | ActiveInHierarchy=" + hoveredEntry.PartActiveInHierarchy);
                y += lineHeight;

                GUI.Label(new Rect(inner.x, y, inner.width, lineHeight),
                    "Path: " + hoveredEntry.Path);
                y += lineHeight;

                GUI.Label(new Rect(inner.x, y, inner.width, lineHeight),
                    "Renderer: " + hoveredEntry.RendererType +
                    " | Enabled=" + hoveredEntry.RendererEnabled +
                    " | ActiveSelf=" + hoveredEntry.RendererActiveSelf +
                    " | ActiveInHierarchy=" + hoveredEntry.RendererActiveInHierarchy);
                y += lineHeight;

                GUI.Label(new Rect(inner.x, y, inner.width, lineHeight),
                    "Mesh: " + hoveredEntry.MeshName);
                y += lineHeight;

                GUI.Label(new Rect(inner.x, y, inner.width, lineHeight),
                    "Materials: " + hoveredEntry.Materials);
            }
            else
            {
                GUI.Label(new Rect(inner.x, y, inner.width, lineHeight),
                    "Hover over a renderer row to show details.");
            }

            GUI.EndGroup();
        }

        private void StartExport()
        {
            float scale;

            if (!float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
            {
                AddStatus("Invalid scale value.");
                return;
            }

            if (scale < MIN_EXPORT_SCALE)
                scale = MIN_EXPORT_SCALE;

            scaleText = scale.ToString("0.###", CultureInfo.InvariantCulture);

            if (!exportStl && !exportObj && !export3mf && !exportStp
#if false
                && !dumpMesh
#endif
                )
            {
                AddStatus("Select at least one export format: STL, OBJ, 3MF, STEP, or a mesh dump.");
                return;
            }

            string outputDir = ResolveOutputDirectory(slicerConfig.OutputDirectory);
            if (string.IsNullOrEmpty(outputDir))
            {
                AddStatus("Output folder is empty or invalid.");
                return;
            }

            slicerConfig.OutputDirectory = outputDir;

            if (engineOptions.Count == 0)
                RefreshEngineList(false);

            // The disabled-renderer/collider sets built below come from rendererDiagnostics, which
            // is otherwise only populated when the user opens "Show Renderer Diagnostics" or clicks
            // "Refresh Renderers". Without this, an export run before ever opening that panel would
            // see an empty list and skip every diagnostics-based exclusion -- including the default
            // "colliders without a material are excluded" behavior -- even though nothing appeared
            // wrong to the user. Always refresh here so export reflects the same defaults regardless
            // of whether that panel has been opened yet.
            RefreshRendererDiagnostics(false);

            statusLines.Clear();
            exporting = true;
            cancelRequested = false;
            exportProgress = 0f;

            Log.Info($"StartExport, ctx.UseStartMenuSearchForViewer: {useStartMenuSearchForViewer}");
            Log.Info($"StartExport, ctx.ViewerStartMenuSearch: {viewerStartMenuSearchText}");
            Log.Info($"StartExport, ctx.ViewerExePath: {viewerExePathText}");


            StartCoroutine(CraftPrintExporter.ExportCurrentCraft(
                scale,
                exportStl,
                exportObj,
                export3mf,
                threeMfPerPart,
                exportStp,
#if false
                dumpMesh,
#endif
                showShrouds,
                excludeLaunchClamps,
                orientation,
                CloneEngineOptions(),
                BuildDisabledRendererKeySet(),
                BuildDisabledRendererObjectSet(),
                BuildDisabledRendererTransformSet(),
                outputDir,
                viewerExePathText,
                viewerStartMenuSearchText,
                useStartMenuSearchForViewer,
                openViewerAfterExport,
                AddStatus,
                SetProgress,
                IsCancelRequested,
                OnExportComplete));
        }

        private bool IsCancelRequested()
        {
            return cancelRequested;
        }

        private void SetProgress(float progress)
        {
            exportProgress = Mathf.Clamp01(progress);
        }


        private void ApplyRendererIncludeState(RendererDiagnosticEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Key))
                return;

            bool include;
            if (rendererIncludeByKey.TryGetValue(entry.Key, out include))
                entry.IncludeInExport = include;
            else
                rendererIncludeByKey[entry.Key] = entry.IncludeInExport;
        }

        // Bulk action: every collider entry with no material (in practice, every collider entry --
        // they have no Renderer, so no material is even possible) gets its Export checkbox set in
        // one click, instead of unchecking each one by hand.
        private void SetIncludeForCollidersWithoutMaterial(bool include)
        {
            for (int i = 0; i < rendererDiagnostics.Count; i++)
            {
                RendererDiagnosticEntry entry = rendererDiagnostics[i];
                if (entry == null || !entry.IsColliderOnly || entry.HasMaterial)
                    continue;

                entry.IncludeInExport = include;
                if (!string.IsNullOrEmpty(entry.Key))
                    rendererIncludeByKey[entry.Key] = include;
            }
        }


        private HashSet<Renderer> BuildDisabledRendererObjectSet()
        {
            HashSet<Renderer> disabled = new HashSet<Renderer>();

            for (int i = 0; i < rendererDiagnostics.Count; i++)
            {
                RendererDiagnosticEntry entry = rendererDiagnostics[i];
                if (entry == null || entry.Renderer == null)
                    continue;

                ApplyRendererIncludeState(entry);
                if (!entry.IncludeInExport)
                    disabled.Add(entry.Renderer);
            }

            return disabled;
        }

        private HashSet<Transform> BuildDisabledRendererTransformSet()
        {
            HashSet<Transform> disabled = new HashSet<Transform>();

            for (int i = 0; i < rendererDiagnostics.Count; i++)
            {
                RendererDiagnosticEntry entry = rendererDiagnostics[i];
                if (entry == null)
                    continue;

                Transform t = entry.Renderer != null ? entry.Renderer.transform
                    : (entry.ColliderMeshFilter != null ? entry.ColliderMeshFilter.transform : null);

                if (t == null)
                    continue;

                ApplyRendererIncludeState(entry);
                if (!entry.IncludeInExport)
                    disabled.Add(t);
            }

            return disabled;
        }

        private HashSet<string> BuildDisabledRendererKeySet()
        {
            HashSet<string> disabled = new HashSet<string>();

            for (int i = 0; i < rendererDiagnostics.Count; i++)
            {
                RendererDiagnosticEntry entry = rendererDiagnostics[i];
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                    continue;

                ApplyRendererIncludeState(entry);
                if (!entry.IncludeInExport)
                    RendererDiagnosticsUtilities.AddRendererKeys(disabled, entry.Part, entry.Renderer);
            }

            foreach (KeyValuePair<string, bool> kvp in rendererIncludeByKey)
            {
                if (!kvp.Value && !string.IsNullOrEmpty(kvp.Key))
                    disabled.Add(kvp.Key.ToLowerInvariant());
            }

            return disabled;
        }

        private void RefreshRendererDiagnostics(bool reportStatus)
        {
            RendererHighlightUtility.ClearHighlight();
            List<Part> parts;
            string sourceName;
            string sceneDescription;
            if (ScenePartUtilities.TryGetCurrentParts(out parts, out sourceName, out sceneDescription))
                rendererDiagnostics = RendererDiagnosticsUtilities.BuildEntries(parts);
            else
                rendererDiagnostics = new List<RendererDiagnosticEntry>();

            if (reportStatus)
                AddStatus("Renderer diagnostics refreshed. Renderers: " + rendererDiagnostics.Count);
        }

        private void RefreshEngineList(bool reportStatus)
        {
            engineOptions = EngineUtilities.GetEngineOptions(showShrouds);

            if (!reportStatus)
                return;

            if (engineOptions.Count == 0)
                AddStatus("No engines found.");
            else
                AddStatus("Engines found: " + engineOptions.Count);
        }

        private void SetAllEngineShroudToggles(bool enabled)
        {
            for (int i = 0; i < engineOptions.Count; i++)
            {
                if (engineOptions[i] != null)
                    engineOptions[i].ShowShroud = enabled;
            }
        }

        private void DrawOrientationSelector()
        {
            // Radio-style list rather than a dropdown: KSP's IMGUI has no real combo box, and
            // four options is few enough that showing them all costs nothing and saves a click.
            ExportOrientation[] all =
            {
                ExportOrientation.UprightZUp,
                ExportOrientation.UprightRotated90,
                ExportOrientation.LayFlatAlongX,
                ExportOrientation.AsInGame,
            };
            using (new GUILayout.HorizontalScope())
            {
                //GUILayout.Space(20);
                foreach (ExportOrientation o in all)
                {
                    GUILayout.Space(20);
                    bool selected = (orientation == o);
                    bool now = GUILayout.Toggle(selected, "");
                    GUILayout.Label(ExportOrientationUtilities.DisplayName(o), Utils.whiteFontStyle);

                    // Only act on a click that TURNS ONE ON. IMGUI toggles report their own state,
                    // so without this the user could untick the selected one and end up with no
                    // orientation chosen at all.
                    if (now && !selected)
                        orientation = o;
                }
            }

            GUILayout.Label("   " + ExportOrientationUtilities.Description(orientation));
        }

        private List<EngineShroudOption> CloneEngineOptions()
        {
            List<EngineShroudOption> copy = new List<EngineShroudOption>();

            for (int i = 0; i < engineOptions.Count; i++)
            {
                EngineShroudOption option = engineOptions[i];
                if (option == null)
                    continue;

                copy.Add(new EngineShroudOption(option.Part, option.DisplayName, option.ShowShroud));
            }

            return copy;
        }

        private void AddStatus(string text)
        {
            statusLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + text);
            statusScroll.y = 999999f;
        }

        private void OnExportComplete()
        {
            exporting = false;
            cancelRequested = false;
            RefreshEngineList(false);
            AddStatus("Process complete.");
        }

        private void OpenSlicerConfiguration()
        {
            if (slicerConfigWindow == null)
            {
                slicerConfigWindow = gameObject.AddComponent<SlicerConfigurationWindow>();
            }

            slicerConfigWindow.Open();
        }

        public void Update()
        {
            // Check if slicer configuration window closed and update path if changed
            if (slicerConfigWindow != null && !slicerConfigWindow.IsOpen())
            {
                var updatedConfig = slicerConfigWindow.GetConfiguration();
                if (updatedConfig.IsConfigured())
                {
                    viewerExePathText = updatedConfig.SelectedSlicerPath;
                    slicerConfig = updatedConfig;

                    viewerStartMenuSearchText = updatedConfig.SelectedSlicerStartMenuSearch;
                    useStartMenuSearchForViewer = updatedConfig.UseStartMenuSearchForSlicer;
                }
            }
        }

        public void Open()
        {
            slicerConfig = new SlicerConfiguration();
            slicerConfig.LoadConfiguration();

            showWindow = true;

            if (string.IsNullOrEmpty(slicerConfig.OutputDirectory))
                slicerConfig.OutputDirectory = Utils.GetDefaultOutputDirectory;

            RefreshEngineList(false);
            if (showRendererDiagnostics)
                RefreshRendererDiagnostics(false);
        }

        public void Close()
        {
            showWindow = false;
            Kerbal3DExporter_ToolbarButton.NotifyWindowClosed();
            Destroy(this);
        }

        public static void OpenWindow()
        {
            CraftPrintExporterWindow win = FindObjectOfType<CraftPrintExporterWindow>();
            if (win != null)
                win.Open();
        }

        public static void CloseWindow()
        {
            CraftPrintExporterWindow win = FindObjectOfType<CraftPrintExporterWindow>();
            if (win != null)
                win.Close();
        }

        private static string ResolveOutputDirectory(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string trimmed = text.Trim();
            if (trimmed.Length == 0)
                return string.Empty;

            if (Path.IsPathRooted(trimmed))
                return trimmed;

            return Path.Combine(KSPUtil.ApplicationRootPath, trimmed);
        }
    }
}
