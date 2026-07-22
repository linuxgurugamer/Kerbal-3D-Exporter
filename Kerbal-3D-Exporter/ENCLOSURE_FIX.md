# Fix: shells not hidden when parts are inside other parts (fairings, structural tubes)

## Why the previous fix did not cover this

The earlier pass fixed real defects — part-boundary crossing, `ModuleJettison` state corruption
across exports, the `renderer != null` guards — but none of them reach this case. The whole
shroud system was **engine-only**, in five separate ways:

1. **The options list contained engines and nothing else.** `GetEngineOptions` filtered on
   `IsEnginePart`, so a fairing base or a structural tube could never appear in it.
2. **The per-part toggle was gated on `IsEnginePart` in *both* `ShroudUtilities` and
   `MeshCollector`.** Even if a non-engine part had been in the list, its toggle was ignored.
   This is the decisive one.
3. **`ModuleProceduralFairing` was not handled anywhere.** Procedural fairing panels are generated
   at runtime and carry no `ModuleJettison`, so nothing knew they existed.
4. **Parts with `ModulePartVariants` were exempted from the name heuristic.** Structural tubes,
   engine plates and fairing bases nearly all have variants. That exemption was a documented
   trade-off in `ShroudUtilities` — this is the case where it bites.
5. **`AttachNodeUtilities` only recognises a node literally called `bottom`.** Tubes and engine
   plates mount their contents on *interstage* nodes.

A sixth name token would not have fixed any of that. The property described — *"some parts of a
vessel are inside other parts"* — is geometric, so it is now tested geometrically.

## What changed

### New: `EnclosureUtilities.cs`

Finds parts that geometrically enclose other parts, from each part's **own** world bounds
(own-part matters here more than anywhere: measuring a tube together with its contents would make
every part trivially "enclose" its descendants).

Containment is measured **per axis**, not as one volume fraction, because enclosure is a *lateral*
property — a shell wraps around its contents, and an engine bell hanging out the bottom of a tube
is still enclosed. Thresholds: 80% on the two axes across the shell, 30% along its long axis, and
the shell must be at least 2× the contained volume.

That split matters. Tested against realistic part layouts:

| test | plain 80% volume | per-axis |
|---|---|---|
| tube containing an engine | ✓ | ✓ |
| fairing containing payload | ✓ | ✓ |
| engine plate with engines inside | ✓ | ✓ |
| **engine in tube, bell protruding** | **✗** | ✓ |
| engine deep in tube, bell well out | — | ✓ |
| stacked fuel tanks | ✓ reject | ✓ reject |
| radial engine surface-attached | ✓ reject | ✓ reject |
| flush antenna on a tank | ✓ reject | ✓ reject |
| landing leg on a tank | — | ✓ reject |
| decoupler / nose cone / similar-size | ✓ reject | ✓ reject |
| | **11/12** | **14/14** |

### The list is no longer engine-only

`GetShroudBearingOptions` now returns engines, `ModuleJettison` bearers, procedural fairings /
cargo bays / service modules, **and** geometric enclosers. Enclosers are labelled `(fairing)` or
`(encloses N)` in the window.

`EngineShroudOption` gained `IsEnclosure`, because the two cases must behave differently:

- an **engine** with a jettison shroud loses only the shroud mesh — the engine stays;
- an **enclosing part** *is* the thing in the way, so all of its own geometry goes.

### The `IsEnginePart` gate is gone from both places

This is the actual unlock. Without it, everything above would still have done nothing.

### A trap worth recording

The obvious implementation — mark the enclosure's transforms in `shroudTransformsToSkip` — is
**wrong**, and simulation caught it before it shipped. `TransformIsInSkipSet` matches by walking
**up** the parent chain, and KSP parents a part's contents *underneath* it. Marking a structural
tube therefore matched every mesh of every part mounted inside it: hiding the tube dropped the
engine, its bell, and the tank above it, leaving only the pod.

Enclosure exclusion is therefore done by **part identity** in `MeshCollector`, which cannot be
inherited by anything mounted inside. Verified: hiding the tube keeps the engine, bell and
everything inside; hiding a fairing keeps its payload.

The part's root GameObject is also never `SetActive(false)` — that would take its contents out of
the scene for the same reason. Renderers are disabled individually instead.

## How to use it

The window's list (now **Refresh Shroud List**) shows fairings and enclosing parts alongside
engines. Untick one to remove that shell from the export; leave it ticked to keep it.

## Where the detections are logged

Three places, because the obvious one is the worst one.

**1. The part diagnostics file — start here.** Written next to the exported model as
`<craft>_printable_part_diagnostics.txt`, under a section headed `ENCLOSING PARTS`:

```
ENCLOSING PARTS
---------------
These parts were detected as containing other parts. Each one appears in the
exporter window's shroud list; unticking it removes that shell from the export,
leaving whatever is inside it visible. Ticking it keeps the shell.

  ENCLOSURE | Structural Tube encloses LV-909 "Terrier"
  ENCLOSURE | AE-FF3 Fairing encloses Probodobodyne HECS
```

The section is always present; when nothing is detected it says so explicitly, which is itself
the answer if you expected a detection and did not get one.

**2. `KSP.log`** — every line, prefixed `[Kerbal3DExporter] ENCLOSURE |`. Survives the session and
can be attached to a bug report. `grep ENCLOSURE KSP.log`.

**3. The window's status pane** — but only the **first 5**, then `... and N more. Full list in the
part diagnostics file and KSP.log.`

That cap is deliberate. The pane scrolls, and closing the window destroys it (`Close()` calls
`Destroy(this)`), so it is the least useful place to put the one thing you would want to re-read
after a bad export. A large craft can also produce dozens of lines, which would push every other
status message out of view.

The window also reports the summary:

```
Parts enclosing other parts: N. Untick one in the shroud list to keep it in the export.
Shrouds/shells -- hidden: N, shown: N
Of those, N are enclosing parts (fairing/tube/bay) whose whole geometry is being removed.
```

## Caveats worth knowing

**The containment test is a heuristic.** Axis-aligned bounds are crude, and a part could be
flagged that you did not mean to hide. That is why every detection is logged by name and every
detected part appears in the list where it can be switched back on individually — visible and
overridable rather than silently reshaping the export.

**The variant exemption is deliberately bypassed for enclosures.** Tubes, plates and fairing bases
nearly all carry `ModulePartVariants`, and that exemption is precisely why they could never be
hidden. Non-selected variant meshes are still protected by
`VariantSnapshotUtilities.OriginallyHiddenVariantTransforms`, which keeps them hidden regardless.

**Untested in KSP.** The geometry and the hide/keep logic are verified in simulation against
realistic part layouts, but I cannot run the game. The `ENCLOSING PARTS` section of the part
diagnostics file is the first thing to check — it names exactly which part was judged to contain
which, and it is the one record that survives the session.

---

# Round 3: parts at the bottom not exported at all

Reported after the enclosure work: parts vanishing entirely when they have fairings, or when
they sit downstream of a part carrying `ModuleProceduralFairing`. Reproduced in simulation; a
pod → fairing → decoupler → engine stack exported **only the pod**.

Two mechanisms, both introduced by the enclosure work interacting with pre-existing code.

## Mechanism A: the token scan swept the part root, taking the downstream stack with it

Stock fairing parts are *named* with the token — `fairingSize2` — so the part's **root
transform** matched the shroud-name scan. `SaveSetAndMaybeSkipTransformTree` then swept the whole
Unity subtree into the skip set and called `SetActive(false)` on it. KSP parents every attached
child part under that root, and `TransformIsInSkipSet` matches by walking **up** the parents, so
every mesh of every downstream part inherited the skip. That is "parts downstream of
ModuleProceduralFairing aren't exported".

The bitter detail: `SaveAndSetObjectAndRenderers` contained a comment asserting that *"a shroud
GameObject does not have parts attached inside it"* as justification for
`GetComponentsInChildren`. That assumption is exactly what failed — the matched object *was* a
part root.

## Mechanism B: the fairing base itself vanished even when ticked

`ShroudUtilities` folded the stock bottom-node-empty rule into `visible`, but the export-side
`BuildHiddenEnclosureSet` is **toggle-only**. A fairing attached by its top node has an empty
bottom node (that is the normal way to attach one), so the scene side disabled all its renderers
while the export side did not identity-skip it → "disabled renderer" → its own meshes gone,
toggle ignored.

## Fixes (verified in simulation, both toggle states)

1. Bottom-node rule gated `&& !isEnclosure` — it is a stock convention about *engine shrouds*,
   not about fairings attached top-down.
2. Enclosure hiding is computed **toggle-only**, written to deliberately mirror
   `BuildHiddenEnclosureSet` — the scene side and export side are two halves of one decision, and
   any drift between them shows up as a part invisible in one and present in the other.
3. The token scan no longer runs on enclosure parts at all, and **never matches a transform that
   carries a `Part` component** — a part's internal name describes what the part *is*, not a
   shroud sub-mesh. Hiding a whole part is the enclosure toggle's job, explicitly.
4. Defense in depth: `AddTransformTreeToSkipSet` and the renderer walk now stop at part
   boundaries, and `SetActive(false)` is never applied to a Part-bearing GameObject. The callers
   were "supposed to" only hand these helpers shroud sub-objects; "supposed to" already failed
   once.

Result: with the fairing **ticked**, everything exports — base, panels, and the full downstream
stack. **Unticked**, only the fairing's own meshes are removed; the decoupler and engine below it
survive.

Behaviour note: procedural fairing *panels* (`FairingPanel` objects) are now governed solely by
the enclosure toggle, like the rest of the fairing part's geometry.

---

# Round 4: the actual root cause — the name heuristic crossed part boundaries

Rounds 2 and 3 fixed real defects but missed this one, and the decisive clue was
**"nothing is listed as enclosing them"**. That rules the enclosure detection out completely
and points at a path that has nothing to do with it.

## The mechanism

`MeshCollector.LooksLikeShroud` and `RendererLooksLikeShroud` both walked **up** the transform
parents looking for shroud tokens:

```csharp
Transform t = renderer.transform;
while (t != null) {
    if (NameHasShroudToken(t.name)) return true;
    t = t.parent;
}
```

KSP parents child parts **underneath** their parent part's transform. Stock fairing parts are
*named* `fairingSize1` / `fairingSize2` / `fairingSize3` — which contains the token `fairing`.

So every part attached below a fairing had a shroud token in its ancestor chain. Every one of
their meshes "looked like a shroud". All of them were skipped whenever `hideEngineShroud` was
true for that part — and that is true for **every part on the craft** when the global
*show shrouds* box is off, which is exactly how you would export to get rid of shrouds.

Simulated on `pod → fairingSize2 → decoupler → tank → engine`, shrouds off: the export contained
**only the pod**. Parts *above* the fairing were untouched — matching "the parts at the bottom".

This walk is long-standing, which is why it presented as "the old problem". Round 2 made it
strictly worse: removing the `renderer != null` guard meant renderer-less meshes started hitting
it too.

## The fix

`AncestorNameHasShroudToken` stops at the first transform carrying a `Part` component:

```csharp
if (t.GetComponent<Part>() != null)
    return false;      // names beyond here belong to other parts
```

The part's **own root name is also not tested**. A part's internal name describes what the part
*is*, not that some sub-mesh is a shroud; testing it would make every mesh of a fairing part
vanish regardless of its toggle. Whole-part hiding is the enclosure toggle's job.

Three further changes for the same reason:

- `RendererLooksLikeShroud` had an identical unbounded walk and would have kept the bug alive on
  its own.
- `TransformIsInSkipSet` also matches by walking up; now bounded at the part root, so a stray
  entry belonging to a part higher in the stack cannot claim every mesh below it.
- Enclosure parts are exempt from the name heuristic entirely. Their geometry is governed by the
  per-part toggle, and a fairing part is full of legitimately fairing-named meshes — the
  heuristic would strip the panels from an export the user asked to keep.

Verified across all three states — shrouds off, shrouds on with the fairing ticked, and shrouds on
with it unticked. The decoupler, tank and engine below the fairing survive in **every** case.
