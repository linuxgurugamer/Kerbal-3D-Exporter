# Kerbal 3D Exporter

A Kerbal Space Program 1 editor/flight plugin for exporting the currently loaded (or active) craft as a 3D model suitable for visualization and 3D printing.

## Features

- Export the current craft directly from the VAB, SPH, or Flight
- **Export to multiple formats:**
  - Binary STL
  - Wavefront OBJ
  - 3MF (3D Manufacturing Format)
  - STEP / .stp (ISO 10303 AP214, faceted B-rep, for CAD rather than printing)
  - Export any combination of formats in one run
- Adjustable export scale
- **Export orientation**, so a rocket that stands up in the VAB stands up in the slicer instead of lying on its side
- **Round part smoothing**, which rebuilds curved surfaces from the model's own vertex normals so tanks and nose cones stop looking polygonal
- Model coordinates are always relative to the craft's root part (placed at 0, 0, 0), regardless of where the craft is positioned in the world
- Launch clamps excluded from export by default (with toggle to include)
- Collapsible shroud list covering engines, procedural fairings, and parts that enclose other parts (structural tubes, engine plates, service bays), each with its own toggle
- Automatic detection of parts that geometrically contain other parts, reported by name in the diagnostics
- Bulk "All Shrouds On"/"All Shrouds Off" buttons
- Collapsible Renderer & Collider Diagnostics window listing every renderer and collider-only mesh
- Individual Export checkboxes for each mesh with live filtering and hover-to-highlight
- Colliders with no material excluded by default with one-click bulk toggle buttons
- Part variant support: only active/selected variant meshes exported
- Optional user-authored exclusion list (shroud-exclusions.txt) for shroud/fairing refinement
- Toolbar button (via ToolbarControl) for easy access
- Progress window showing each export stage with cancel support
- Instruction files generated alongside each model describing recommended post-processing
- Mesh and part diagnostics files for troubleshooting
- Coroutine-based state machine to keep the game responsive during export

## Installation

Install the mod into your KSP installation:

```
GameData/
  Kerbal-3D-Exporter/
    Plugins/
      Kerbal-3D-Exporter.dll
    Icons/
      toolbar-icon.png
      toolbar-icon-24.png
  000_ToolbarControl/
    Plugins/
      ToolbarControl.dll
      ClickThroughBlocker.dll
```

**Dependencies:**
- ToolbarControl
- ClickthroughBlocker

## Usage

1. Start Kerbal Space Program
2. Enter the VAB, SPH, or Flight
3. Load a craft (or use the currently active vessel in Flight)
4. Open the exporter window using either:
   - The toolbar button (top-right stock toolbar or Blizzy's Toolbar if installed), or
   - Right-click a command pod with the exporter module and select "Open Craft Exporter"
5. Configure the export options
6. Click Export

The exporter displays its progress while generating the model.

## Export Options

### Scale

Controls the size of the exported model.

- **Default:** 0.01
- **Minimum:** 0.001

The exporter converts KSP's internal meter units into millimeters before applying the user scale.

**Examples:**

| Scale | Result |
|-------|--------|
|  1.0  | Full-size millimeter export |
|  0.5  | Half-size |
|  2.0  | Double-size |
|  0.1  | 10% size |

### Model Origin

The exported model's x/y/z coordinates are always relative to the craft's root part, which is placed at exactly (0, 0, 0).

Vertices come out of Unity in raw world-space coordinates (an arbitrary position in the VAB/SPH, or a position that can drift in Flight). The exporter normalizes all coordinates relative to the root part to ensure consistency.

### Orientation

KSP is Y-up. Every slicer is Z-up. Without a rotation, a rocket that stands proudly in the VAB
arrives in the slicer lying on its side.

| Option | Effect |
|--------|--------|
| **Upright (Z up)** | *Default.* A vertical rocket exports vertical. |
| **Upright, turned 90 deg** | Same standing pose, a quarter turn about the vertical axis. Useful when a wide asymmetric craft fouls the bed one way but fits the other. |
| **Lay flat along X** | Craft lies along the bed. Use when it is taller than the printer's build height, which for a 30 cm rocket and a 25 cm printer is most of the time. |
| **As in game (Y up)** | The old behaviour, raw KSP axes. Kept for anyone whose downstream workflow already compensates. |

Every option is a true rotation (determinant +1), so triangle winding is preserved and nothing is
mirrored. This matters more than it sounds: the tempting shortcut of swapping the Y and Z axes has
determinant −1. It is a mirror, not a rotation, and it silently turns the model inside out, which
looks perfectly fine in a viewer and which a slicer will happily read as "print everything except
the rocket".

The rotation is applied once, during mesh collection, at the single point every vertex passes
through, so STL, OBJ, 3MF, and STEP always agree.

**This changed the default.** If you previously rotated the model by hand in your slicer, you no
longer need to.

### Round Part Smoothing

Makes round parts round. Off by default.

KSP fuel tanks are **24-sided prisms** — 15° per facet. That reads as visibly polygonal on a
print, especially a large one.

Adding vertices on its own does not fix this. Subdividing each facet gives you 48 points that all
still lie on the same 24 flat faces: identical silhouette, double the triangles. Extra vertices
cannot invent curvature that is not recorded anywhere.

The curvature *is* recorded, in the **vertex normals**. When an artist marks a cylinder smooth,
KSP stores normals pointing radially outward as though the surface were a true circle, even though
the positions form a polygon. That is what makes it look round in-game under lighting, and it is a
record of the surface the model was meant to have.

This option reconstructs that surface using curved PN triangles (the same technique GPUs use for
hardware tessellation): the three corner positions plus their three normals define a cubic Bézier
patch, and new vertices land on the intended curve rather than on the flat chord.

| Level      | Effect on curved surfaces | Max radial error on a 6.3 mm, 24-sided tank |
|------------|---------------------------|---------------------------------------------|
| Off        | export as modelled        | 0.054 mm                                    |
| 1 — Low    | 4× triangles              | 0.001 mm                                    |
| 2 — Medium | 9× triangles              | 0.001 mm                                    |
| 3 — High   | 16× triangles             | 0.001 mm                                    |

**Level 1 gets essentially all of the benefit.** Start there; go higher only if you can see a
reason to.

**Sharp edges are not affected, and there is no threshold to tune.** An artist marking an edge
sharp *splits* the normals there, so the two sides disagree, each patch is fitted to its own flat
normal set, and the crease stays perfectly sharp. A flat panel tessellates to itself exactly. Fins,
panels, and hard rims come through unchanged.

Only genuinely curved triangles are rebuilt — anything whose vertex normals sit within 3° of its
face normal is passed straight through. Since most of a rocket is flat panels, the triangle count
grows far less than the multipliers above suggest. The export log reports the split:

```
Smoothing curved surfaces: Low (4x on curved surfaces) (~180,000 triangles).
Curved triangles rebuilt: 38,400 of 64,820. Flat geometry left untouched.
```

**Whether you need this depends on print size.** At the default 0.01 scale the worst chord error
on a test craft was 0.05 mm, below what a 0.4 mm nozzle resolves. Print the same craft five times
larger and it becomes 0.27 mm, which is clearly visible.

Meshes with no normals come through flat. With no record of the intended surface there is nothing
to reconstruct, and inventing a curve would be guessing.

### Export Format

Choose any combination of:

- STL
- OBJ
- 3MF
- STEP (.stp)

Every selected format is written on the same export run.

#### About 3MF

3MF is the format most current slicers prefer. Unlike STL, it stores an indexed vertex list, so a shared vertex is written once instead of once per triangle, resulting in typically much smaller files.

3MF also records the unit (millimeters) inside the file, so the slicer doesn't have to guess the scale on import.

The .3mf written here is a plain OPC/ZIP package containing `[Content_Types].xml`, `_rels/.rels`, and `3D/3dmodel.model`. It opens directly in Cura, PrusaSlicer, SuperSlicer, OrcaSlicer, Bambu Studio, and similar slicers.

**Separate object + color per part:**

When enabled (default), the 3MF contains one `<object>` per KSP part, tied together by an assembly object, instead of a single merged mesh. A slicer with support for multi-part models can see and manipulate each part separately.

Each part also gets its own color from a generated palette.

**Important:** These colors are SYNTHETIC and decorative. A KSP part is textured, not flat-colored, so there is no single honest "color of this part" to read out of the mesh. The colors are for visual distinction only.

Turning this sub-option off gives the older behavior: one merged, uncolored mesh.

Apart from per-part colors, the 3MF export is geometry only, the same as STL and OBJ. It carries no textures.

#### About STEP (.stp)

**Read this before enabling STEP**, as it is not simply "a better STL."

STEP is a boundary-representation CAD format. A KSP craft is a triangle mesh. The only faithful way across that gap is a faceted B-rep: every single triangle becomes its own flat face.

That is valid STEP and it opens in Fusion 360, SolidWorks, FreeCAD, Onshape, and similar tools. However, it is NOT a "real" CAD model. There are no smooth NURBS surfaces and no analytic cylinders—only explicit triangles.

STEP files are also large. A faceted B-rep needs roughly 17 STEP entities per triangle, so a 100,000-triangle craft produces something on the order of 1.7 million lines and 100+ MB. Opening such files in a CAD tool can be slow.

The model is written as a surface body (an open shell), not a closed solid. This is deliberate: a KSP craft is many overlapping part meshes and is not watertight, and a STEP solid that claims to be watertight but isn't will cause CAD tools to fail.

**Most slicers cannot read STEP at all.** Use STL or 3MF for printing. Use STEP when you want the craft inside a CAD package to build a display stand or mount around it.

Because slicers generally cannot open STEP, the "open viewer after export" option never hands it a .stp file. It opens the STL, 3MF, or OBJ instead.

### STL Viewer / Slicer Executable

Optional path to the executable for an STL viewer or slicer, such as PrusaSlicer, Cura, MeshLab, or another viewer.

If "Open viewer after export completes" is enabled, the exporter opens the generated STL when STL export is selected. If STL was not exported, it opens the 3MF instead, and failing that, the OBJ.

If this field is blank and opening is enabled, the exporter attempts to open the model using the operating system's default file association.

### Open Viewer After Export Completes

When checked, the exporter launches the configured viewer/slicer after a successful export.

The viewer is not launched if the export is cancelled or fails.

### Show Engine Shrouds / Fairings by Default

Global master switch controlling whether shrouds, fairings, and enclosing shells are included in
the exported model.

When disabled, supported shroud meshes are hidden before mesh collection and restored after export.

Per-part toggles (in the Shroud List) can only further disable a shell when this is on; they cannot
re-enable one when this global switch is off.

### Exclude Launch Clamps

Checked by default. Excludes any part with a LaunchClamp module (stock launch clamps) from the export entirely, since they aren't part of the craft itself.

Uncheck to include launch clamps in the model.

### Exclude Collider-Only Meshes

Colliders (physics-only meshes with no MeshRenderer—that's why they're invisible in-game) can be listed and toggled individually in the Renderer & Collider Diagnostics window.

Colliders with no material default to excluded automatically. Use "Disable colliders without a material" / "Re-enable all colliders" in that window to apply or undo this in bulk, or toggle them individually.

## Shroud List

Formerly the Engine List. It now covers every part carrying geometry that can get in the way, not
just engines.

- Collapsed by default; click "Show Shroud List" to expand
- Lists, each with its own checkbox:
  - **engines**, and any part carrying a stock `ModuleJettison` shroud
  - **procedural fairings**, cargo bays, and service modules — labelled `(fairing)`
  - **parts that geometrically enclose other parts** — structural tubes, engine plates, interstage
    shells — labelled `(encloses N)`
- "Refresh Shroud List" rescans the current craft
- "All Shrouds On" / "All Shrouds Off" set every checkbox at once

The two kinds behave differently, because they are different things:

- An **engine** with a jettisonable shroud loses only the shroud mesh. The engine itself stays.
- An **enclosing part** *is* the shell in the way, so unticking it removes all of that part's own
  geometry, revealing whatever is mounted inside. Anything inside it is untouched.

Enclosure detection is geometric: a part is treated as enclosing another when the inner part is
contained across the shell's two lateral axes and overlaps along its long axis. Measuring laterally
rather than by volume is deliberate, because an engine bell hanging out the bottom of a structural
tube is still enclosed.

It is a heuristic, so every detection is reported by name (see below) and every detected part can
be re-ticked individually.

The list is rebuilt from the craft's actual parts at export time, carrying your existing choices
across by part reference. Adding or removing parts while the window is open therefore cannot leave
the export running against a stale list.

## Renderer & Collider Diagnostics

- Collapsed by default; click "Show Renderer Diagnostics" to expand
- Lists every Renderer (MeshRenderer/SkinnedMeshRenderer) and every collider-only mesh found on the craft, one row per object
- Each row has an Export checkbox; unchecking excludes that specific object from the model, independent of any other setting
- Hovering a renderer row highlights it in the 3D view (colliders have nothing visible to highlight)
- A filter box narrows the list by part name, mesh name, path, material, or type
- "Refresh Renderers" rescans the current craft
- "Disable colliders without a material" / "Re-enable all colliders" bulk-toggle every collider row that has no material (in practice, every collider row)
- Export automatically refreshes this list before building the model, so these settings take effect even if this window was never opened

## shroud-exclusions.txt (currently disabled)

Optional file at `GameData/Kerbal-3D-Exporter/shroud-exclusions.txt`.

Lets you specify additional PartName/Path/Material tokens to treat as shroud/fairing geometry. Applies only when shrouds/fairings are being hidden for that part or engine (i.e., it refines the shroud-hiding heuristics—it is not a general-purpose always-exclude list).

Use the generated `*_mesh_diagnostics.txt` file to find the exact tokens to add.

## Output Location

All exported files are written to:

```
GameData/Kerbal-3D-Exporter/Models/
```

**Example (from the VAB/SPH):**

```
SaturnV_printable.stl
SaturnV_printable.obj
SaturnV_printable.3mf
SaturnV_printable.stp
SaturnV_printable_mesh_diagnostics.txt
SaturnV_printable_part_diagnostics.txt   (includes the ENCLOSING PARTS section)
SaturnV_printable_STL_instructions.txt
SaturnV_printable_OBJ_instructions.txt
SaturnV_printable_3MF_instructions.txt
SaturnV_printable_STEP_instructions.txt
```

In Flight, exported files use a `_flight_current_printable` suffix instead of `_printable`.

## Export Process

The exporter performs the following steps:

1. Validate the current craft
2. Snapshot which meshes on parts with variants are already hidden
3. Prepare the output directory
4. Detect enclosing parts and refresh the shroud list
5. Apply the selected shroud visibility
6. Write part diagnostics
7. Collect meshes from all parts
8. Write mesh diagnostics
9. Remove invalid triangles
10. Remove duplicate triangles, then apply round part smoothing if enabled
11. Write STL (if selected)
12. Write STL instructions
13. Write OBJ (if selected)
14. Write OBJ instructions
15. Write 3MF (if selected)
16. Write 3MF instructions
17. Write STEP (if selected)
18. Write STEP instructions
19. Restore shroud visibility

Each stage is displayed in the exporter window.

## Recommended Workflow for 3D Printing

The exported model is intended as a starting point for creating printable models.

**Recommended workflow:**

1. Export the craft, with **Upright (Z up)** orientation and **Round part smoothing** at level 1
   if the model is large enough for faceting to show
2. Import the STL or OBJ into Blender
3. Inspect for missing or unwanted geometry
4. Merge meshes using Boolean Union
5. Remove internal geometry
6. Repair non-manifold edges
7. Fill holes
8. Thicken fragile parts if necessary
9. Export the repaired model as STL
10. Slice with your preferred slicer

## Limitations

The exporter works with meshes instantiated by Unity.

Some KSP-specific rendering features are not exported, including:

- Shaders
- Materials
- Textures (OBJ geometry only)
- Animations
- Particle systems
- Lights

Only the currently active/selected part variant's meshes are exported; other variants' meshes are excluded.

Some mods may generate geometry procedurally. Depending on implementation, the generated geometry may or may not appear in the export.

Because KSP crafts are assembled from many overlapping meshes, additional cleanup is typically required before producing a watertight mesh suitable for reliable 3D printing.

Round part smoothing reconstructs curvature from vertex normals; it cannot recover detail that was never modelled. A part built as a genuine low-poly faceted shape stays faceted, correctly, because its normals say it is flat.

Enclosure detection uses axis-aligned bounds, so it is a heuristic. A part may occasionally be flagged that you did not mean to hide; untick it in the Shroud List.

## Troubleshooting

### Export fails

Check the KSP log (`KSP.log`) for exception details.

### Missing parts

Verify that all required mods are installed and the craft loads correctly in the editor.

### A mesh is missing or unexpectedly included

1. Open the Renderer & Collider Diagnostics window
2. Use the filter box to find the mesh
3. Check whether its Export checkbox matches what you expect
4. Click "Refresh Renderers" to rescan the current live scene state
5. Check the `*_mesh_diagnostics.txt` file from your last export: every mesh is listed as either EXPORT or SKIP, with the specific reason it was skipped

### Shrouds still appear, or a shroud is missing

Some engines implement shrouds differently than the stock ModuleJettison system. Toggle the
specific renderer off in the Diagnostics window, or add a token to `shroud-exclusions.txt` if that
file is enabled in your build.

On engines that also have part variants, a shroud detected only by the name-heuristic fallback (not
a real ModuleJettison shroud) follows whatever the active variant already set, rather than being
independently controlled by the global switch.

**Enclosing parts are the exception to that.** Structural tubes, engine plates, and fairing bases
nearly all carry part variants, and that exemption is exactly why they could not previously be
hidden at all. They are governed by their own entry in the Shroud List instead, regardless of
variants. Meshes belonging to non-selected variants are still protected and stay hidden.

### A part is inside a fairing or a tube and I can't get rid of the shell

Untick that part in the Shroud List. It appears there labelled `(fairing)` or `(encloses N)`.

If it is not listed, the geometric detection did not consider it to contain anything — check the
`ENCLOSING PARTS` section of the part diagnostics file (below), which states plainly what was and
was not detected.

### Where enclosure detections are logged

Three places, in order of usefulness:

1. **`<craft>_printable_part_diagnostics.txt`**, under a section headed `ENCLOSING PARTS`. Written
   to disk next to the model, so it survives the session. The section is always present; when
   nothing is detected it says so explicitly, which is itself the answer if you expected a
   detection and did not get one.

   ```
   ENCLOSING PARTS
   ---------------
     ENCLOSURE | Structural Tube encloses LV-909 "Terrier"
     ENCLOSURE | AE-FF3 Fairing encloses Probodobodyne HECS
   ```

2. **`KSP.log`**, every line prefixed `[Kerbal3DExporter] ENCLOSURE |`. Attachable to a bug report:
   `grep ENCLOSURE KSP.log`.

3. **The exporter window**, but only the first five, then a pointer to the other two. The pane
   scrolls and is destroyed when the window closes, so it is the least useful place to read this
   after the fact.

### Parts near the bottom of the craft are missing entirely

This was a bug, fixed. The shroud name-heuristic used to walk *up* the transform hierarchy looking
for shroud tokens, and KSP parents child parts underneath their parent part. Stock fairing parts
are *named* `fairingSize1` / `fairingSize2` / `fairingSize3`, so every part attached below a
fairing had a shroud token in its ancestor chain, "looked like a shroud", and was dropped — while
parts above the fairing were unaffected.

The heuristic now stops at the part that owns the mesh and never reads the names of parts above it.

If you still see this, check the `*_mesh_diagnostics.txt` file: every mesh is listed as EXPORT or
SKIP with the specific reason.

### Large file sizes

Large crafts can contain millions of triangles. Consider reducing the model size or simplifying the mesh in Blender before slicing.

## Future Improvements

Possible enhancements include:

- Shared-vertex OBJ output
- Vertex welding
- Mesh decimation
- Automatic watertight mesh generation (prototyped offline: per-part generalized winding number
  for a free boolean union, exterior flood fill to delete internal geometry, and a signed distance
  field for sub-voxel accuracy — not yet ported into the mod)
- Automatic Boolean union
- glTF export with materials
- ZIP package generation

## License

GPLv3 (GNU General Public License version 3)

## Disclaimer

This project is provided as-is without warranty. Use at your own risk.

## Credits

**AI applications used in the development of this project:**
- ChatGPT4
- Claude.ai
- Github Copilot

Kerbal Space Program is © Squad / Private Division.
