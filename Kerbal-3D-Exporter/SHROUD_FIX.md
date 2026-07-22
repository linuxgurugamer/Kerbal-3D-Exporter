# Fix: engine shrouds not hidden on vessels with many parts and engines

## What was wrong

Three defects, all of which get worse as a vessel gets bigger. That is why the symptom showed
up specifically on craft with many parts and engines, and why it looked like an old problem
coming back rather than a clean failure.

### 1. ShroudUtilities crossed part boundaries (root cause)

KSP parents child parts **under their parent part's transform**. So
`part.GetComponentsInChildren<T>(true)` does not return "this part's things" — it returns this
part's things plus everything belonging to every part attached below it. For the vessel root,
that is the entire vessel.

`MeshCollector` has always known this and walks the hierarchy manually, stopping at any child
carrying its own `Part` component (`CollectOwnComponents`). `ShroudUtilities` did not. It used
`GetComponentsInChildren` in three places, so every ancestor part re-processed every descendant
part's shroud modules and shroud transforms.

Because the visibility write is unconditional (`go.SetActive(visible)` / `r.enabled = visible`
always run — the `savedObjects` / `savedRenderers` sets guard only the state *save*), whichever
part was processed **last** won. A fuel tank in the middle of a stack could re-show an engine
shroud several parts below it that the user had explicitly switched off.

Simulated on a `pod -> [tank -> engine -> decoupler] x 8` stack: the worst-affected shroud was
written by **24 different parts**.

### 2. ModuleJettison state was saved without dedupe, so it corrupted across exports

```csharp
savedStates.Add(new ShroudState(shroud, shroud.enabled));   // no dedupe
shroud.enabled = visible;
```

The GameObject and Renderer saves were guarded by hash sets. This one was not. Combined with
defect 1, a module at stack depth *d* was saved *d+1* times, and **every save after the first
captured the value the previous part's pass had just written**, not KSP's original.

`RestoreShroudVisibility` replays the list in order, so the last (already corrupted) save wins.

Measured in simulation, global shrouds off:

| stages | saves (old) | restored correctly | saves (fixed) | restored correctly |
|---|---|---|---|---|
| 1 | 6 | no | 1 | yes |
| 3 | 36 | no | 3 | yes |
| 8 | 216 | no | 8 | yes |
| 12 | 468 | no | 12 | yes |

Every `ModuleJettison` on the craft was left **disabled** after a single export. `ModuleJettison`
is what KSP uses to manage shroud visibility, so leaving it disabled means KSP stops managing the
shroud — and the *next* export then saves that wrong state as its "original". The damage
accumulates.

### 3. A mesh with no MeshRenderer bypassed the shroud checks entirely

This is the same defect class as the original `GetSkipReason` bug from earlier in this project.
Three checks were gated on `renderer != null`:

```csharp
if (hideEngineShroud && renderer != null && RendererLooksLikeShroud(renderer)) ...
if (bottomNodeHasNoAttachment && renderer != null && RendererLooksLikeShroud(renderer)) ...
if (!EXPORT_DISABLED_RENDERERS && renderer != null && (!renderer.enabled || ...)) ...
```

A `MeshFilter` with no sibling `MeshRenderer` — a collider mesh, an LOD mesh, a shroud whose
renderer lives elsewhere — failed all three guards and was exported regardless of the shroud
settings. The name heuristic never needed a `Renderer` in the first place: it walks the transform
chain, and the `Renderer` only adds material names on top.

### 4. The engine list could be stale

`StageRefreshEngineList` only rebuilt the list when it was null or empty, so a non-empty list was
trusted as-is. The window rebuilds its list only on open, after an export, or on an explicit
refresh — so a user who leaves the window open while they keep building (normal, and much more
likely on a big craft) could export against a list missing the engines they just added. Those
engines' shrouds silently fell back to the global default.

## What changed

**New: `PartHierarchyUtilities.cs`** — `GetOwnTransforms(part)` and `GetOwnComponents<T>(part)`,
the part-boundary-respecting replacements for `GetComponentsInChildren`. `ShroudUtilities` and
`MeshCollector` now share one definition of "this part's things", so they cannot drift apart
again.

**`ShroudUtilities.cs`**
- `ModuleJettison` enumeration and both transform searches are own-part only.
- Added `savedModules` hash set so each module's original state is saved exactly once.
- The `GetComponentsInChildren<Renderer>` inside `SaveAndSetObjectAndRenderers` is left alone and
  commented — it walks a shroud object's own sub-tree, which has no parts inside it. The problem
  was choosing *which* objects to call it on.

**`MeshCollector.cs`**
- New `LooksLikeShroud(transform, renderer)` works with or without a `Renderer`.
- The activity check now also rejects an inactive renderer-less mesh.

**`EngineUtilities.cs`** — new `ReconcileEngineOptions`, which rebuilds from the craft's actual
parts while carrying the user's existing per-engine choices across. Matching is by `Part`
reference, not name: a craft with six identical Swivels has six identically-named options, and
name matching would cross-wire them.

**`CraftPrintExporter.cs`** — always reconciles the engine list, reports when the window's list
was out of date, and logs a per-engine shroud summary.

## Verify it worked

The export status log now reports:

```
Shroud/fairing states changed: N
Shroud transforms marked for skipping: N
Engine shrouds -- hidden: N, shown: N
```

Check that "hidden" matches the number of engines you expect. If the window's list was stale you
will also see `Engine list was out of date (window had N); rebuilt from the current craft.`

## Behaviour change worth knowing

`ShroudUtilities` no longer reaches into child parts. That is the fix, but it is a real change:
if some part's shroud was only ever being hidden *by accident*, because an ancestor part happened
to reach it and happened to be processed last, it will now follow its own part's setting instead.
That is the correct behaviour, but if a specific craft looks different after this change, that is
the reason — and the per-engine log above will show what the exporter decided.
