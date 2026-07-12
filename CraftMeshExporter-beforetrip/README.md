Craft Mesh Exporter

A Kerbal Space Program 1 editor/flight plugin for exporting the currently loaded (or active) craft as a 3D model suitable for visualization and 3D printing.

Features
    Export the current craft directly from the VAB, SPH, or Flight.
    Export to:
    Binary STL
    Wavefront OBJ
    Or both simultaneously.
    Adjustable export scale.
    Model coordinates are always relative to the craft's root part (placed at 0, 0, 0), regardless of where the craft happens to be sitting in the world.
    Launch clamps are excluded from export by default, with a toggle to include them.
    Collapsible engine list showing every engine in the current craft, with a per-engine toggle for including that engine's shroud/fairing, plus "All Shrouds On"/"All Shrouds Off" bulk buttons.
    Collapsible Renderer & Collider Diagnostics window listing every renderer AND every collider-only mesh on the craft, each with its own Export checkbox, a live filter box, and hover-to-highlight (for renderers).
    Colliders with no material default to excluded from export, with one-click buttons to bulk disable/re-enable them.
    Part variant support: only the currently active/selected variant's meshes are exported, without depending on parsing KSP's internal variant data structures.
    Optional user-authored exclusion list (shroud-exclusions.txt) for refining which shroud/fairing meshes get excluded.
    A toolbar button (via ToolbarControl, shown on either Blizzy's Toolbar or the stock KSP toolbar depending on what you have installed) to open the window directly, in addition to the part right-click action.
    Progress window showing each stage of the export, with cancel support.
    Creates instruction files alongside each exported model describing the recommended post-processing steps.
    Creates mesh and part diagnostics files alongside each exported model for troubleshooting what was included or excluded and why.
    Keeps the game responsive during export by using a coroutine-based state machine.

Installation
    Install the mod into your KSP installation:
    GameData/
        CraftMeshExporter/
            Plugins/
                CraftMeshExporter.dll
            Icons/
                toolbar-icon.png
                toolbar-icon-24.png
        000_ToolbarControl/
            Plugins/
                ToolbarControl.dll
                ClickThroughBlocker.dll
    ToolbarControl (https://github.com/linuxgurugamer/ToolbarControl) is required for the toolbar button and is not bundled with KSP; download it separately if you don't already have it from another mod. Without it, use the part right-click "Open Craft Exporter" action instead.
    (Optional) Install the included ModuleManager patch if you want the exporter automatically added to command pods.

Usage
    Start Kerbal Space Program.
    Enter the VAB, SPH, or Flight.
    Load a craft (or use the currently active vessel in Flight).
    Open the exporter window using either:
        The toolbar button (top-right stock toolbar, or Blizzy's Toolbar if installed), or
        Right-clicking a command pod with the exporter module and clicking Open Craft Exporter.
    Configure the export options.
    Click Export.

The exporter will display its progress while generating the model.

Export Options
    Scale

        Controls the size of the exported model.

        Default: 0.01
        Minimum: 0.001

        The exporter converts KSP's internal meter units into millimeters before applying the user scale.

        For example:

        Scale	Result
        1.0	Full-size millimeter export
        0.5	Half-size
        2.0	Double-size
        0.1	10% size

    Model Origin

        The exported model's x/y/z coordinates are always relative to the craft's root part, which is placed at exactly (0, 0, 0).

        Vertices come out of Unity in raw world-space coordinates, which is wherever the craft happens to be sitting (an arbitrary position in the VAB/SPH, or a position that can drift further in Flight). The root part's world position is subtracted from every vertex before scaling, so the model's origin doesn't depend on where the craft happens to be in the world.

    Export Format

        Choose one or both:

            STL
            OBJ

        If both are selected, both files will be generated.


    STL Viewer / Slicer Executable

        Optional path to the executable for an STL viewer or slicer, such as PrusaSlicer, Cura, MeshLab, or another viewer.

        If Open viewer after export completes is enabled, the exporter opens the generated STL when STL export is selected. If only OBJ is selected, it opens the OBJ instead.

        If this field is blank and opening is enabled, the exporter attempts to open the model using the operating system's default file association.

    Open Viewer After Export Completes

        When checked, the exporter launches the configured viewer/slicer after a successful export.

        The viewer is not launched if the export is cancelled or fails.

    Show Engine Shrouds / Fairings by Default

        Global master switch controlling whether engine shrouds/fairings are included in the exported model.

        When disabled, supported shroud meshes are hidden before mesh collection and restored after export.

        Per-engine toggles (in the Engine List) can only further disable a shroud when this is on; they cannot re-enable one when this global switch is off.

    Exclude Launch Clamps

        Checked by default. Excludes any part with a LaunchClamp module (stock launch clamps) from the export entirely, since they aren't part of the craft itself.

        Uncheck to include launch clamps in the model.

    Exclude Collider-Only Meshes

        Colliders (physics-only meshes with no MeshRenderer -- that's literally why they're invisible in-game, regardless of their name) can be listed and toggled individually in the Renderer & Collider Diagnostics window.

        Colliders with no material default to excluded automatically. Use "Disable colliders without a material" / "Re-enable all colliders" in that window to apply or undo this in bulk, or toggle individual rows.

Engine List
    Collapsed by default; click "Show Engine List" to expand it.
    Lists every engine found on the craft with a checkbox for whether to include that engine's shroud/fairing.
    "Refresh Engine List" rescans the current craft.
    "All Shrouds On" / "All Shrouds Off" set every engine's checkbox at once.

Renderer & Collider Diagnostics
    Collapsed by default; click "Show Renderer Diagnostics" to expand it.
    Lists every Renderer (MeshRenderer/SkinnedMeshRenderer) and every collider-only mesh (a MeshFilter with a Collider component but no MeshRenderer) found on the craft, one row per object.
    Each row has an Export checkbox; unchecking a row excludes that specific object from the model, independent of any other setting.
    Hovering a renderer row highlights it in the 3D view (colliders have nothing visible to highlight).
    A filter box narrows the list by part name, mesh name, path, material, or type.
    "Refresh Renderers" rescans the current craft.
    "Disable colliders without a material" / "Re-enable all colliders" bulk-toggle every collider row that has no material (in practice, every collider row).
    Export automatically refreshes this list before building the model, so these settings take effect even if this window was never opened.

shroud-exclusions.txt
    Optional file at GameData/CraftMeshExporter/shroud-exclusions.txt.
    Lets you specify additional PartName/Path/Material tokens to treat as shroud/fairing geometry.
    Applies only when shrouds/fairings are being hidden for that part or engine (i.e. it refines the shroud-hiding heuristics -- it is not a general-purpose always-exclude list).
    Use the generated *_mesh_diagnostics.txt file to find the exact tokens to add.

Output Location
    All exported files are written to:

        GameData/CraftMeshExporter/Models/

    Example (from the VAB/SPH):

        SaturnV_printable.stl
        SaturnV_printable.obj
        SaturnV_printable_mesh_diagnostics.txt
        SaturnV_printable_part_diagnostics.txt
        SaturnV_printable_STL_instructions.txt
        SaturnV_printable_OBJ_instructions.txt

    In Flight, exported files use a _flight_current_printable suffix instead of _printable.

Export Process
    The exporter performs the following steps:

        Validate the current craft.
        Snapshot which meshes on parts with variants are already hidden (before anything else touches the scene).
        Prepare the output directory.
        Refresh the engine list.
        Apply the selected shroud visibility.
        Write part diagnostics.
        Collect meshes from all parts.
        Write mesh diagnostics.
        Remove invalid triangles.
        Remove duplicate triangles.
        Write STL (if selected).
        Write STL instructions.
        Write OBJ (if selected).
        Write OBJ instructions.
        Restore shroud visibility.

    Each stage is displayed in the exporter window.

Recommended Workflow for 3D Printing
    The exported model is intended as a starting point for creating printable models.

    Recommended workflow:

        Export the craft.
        Import the STL or OBJ into Blender.
        Inspect for missing or unwanted geometry.
        Merge meshes using Boolean Union.
        Remove internal geometry.
        Repair non-manifold edges.
        Fill holes.
        Thicken fragile parts if necessary.
        Export the repaired model as STL.
        Slice with your preferred slicer.

Limitations
    The exporter works with meshes instantiated by Unity.

    Some KSP-specific rendering features are not exported, including:

        Shaders
        Materials
        Textures (OBJ geometry only)
        Animations
        Particle systems
        Lights

    Only the currently active/selected part variant's meshes are exported; other variants' meshes are excluded.

    Some mods may generate geometry procedurally. Depending on how those mods are implemented, the generated geometry may or may not appear in the export.

    Because KSP crafts are assembled from many overlapping meshes, additional cleanup is typically required before producing a watertight mesh suitable for reliable 3D printing.

Troubleshooting
    Export fails
        Check the KSP log (KSP.log) for exception details.

    Missing parts
        Verify that all required mods are installed and the craft loads correctly in the editor.

    A mesh is missing or unexpectedly included
        Open the Renderer & Collider Diagnostics window, use the filter box to find it, and check whether its Export checkbox matches what you expect. Refreshing rescans the current live scene state.
        Check the *_mesh_diagnostics.txt file from your last export: every mesh is listed as either EXPORT or SKIP, with the specific reason it was skipped.

    Shrouds still appear, or a shroud is missing
        Some engines implement shrouds differently than the stock ModuleJettison system. Try adding a token to shroud-exclusions.txt (see above), or toggle the specific renderer off in the Diagnostics window.
        On engines that also have part variants, a shroud only detected by the name-heuristic fallback (not a real ModuleJettison shroud) will follow whatever the active variant already set, rather than the global shroud toggle -- this is a deliberate trade-off to avoid variant geometry being exported incorrectly.

    Large file sizes
        Large crafts can contain millions of triangles. Consider reducing the model size or simplifying the mesh in Blender before slicing.

Future Improvements
    Planned enhancements include:

        Shared-vertex OBJ output.
        Vertex welding.
        Mesh decimation.
        Automatic watertight mesh generation.
        Automatic Boolean union.
        glTF export with materials.
        3MF export.
        ZIP package generation.
        Configurable export directory via the window (currently editable as a text field, defaulting to GameData/CraftMeshExporter/Models).

License
    This project is provided as-is without warranty. Use at your own risk.

Credits
    Created for the Kerbal Space Program modding community.

    Kerbal Space Program is (c) Squad / Private Division.
