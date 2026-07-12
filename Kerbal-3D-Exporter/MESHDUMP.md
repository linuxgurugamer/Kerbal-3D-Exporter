# Mesh dump (.k3dm)

A debug/diagnostic output. It is **not** an export format and produces no printable file.

## Where it is called from

`CraftPrintExporter.StageRemoveDuplicateTriangles`, at the tail, guarded by `ctx.DumpMesh`:

```
StageRemoveDuplicateTriangles
    MeshCleanup.RemoveDuplicateTriangles(ctx.Triangles)
    if (ctx.DumpMesh) WriteMeshDump(ctx)      <-- here
    ... fall through to the first selected export format
```

That location is deliberate. This is the exact triangle soup every writer downstream receives
(post RemoveBadTriangles, post RemoveDuplicateTriangles). Dumping any earlier would capture
geometry the writers never actually see, and the whole point of the dump is to reproduce their
input faithfully.

`WriteMeshDump` swallows exceptions the same way `WriteMeshDiagnostics` does. A diagnostic must
never take a real export down with it -- if the user asked for an STL, they get their STL.

## Why you would use it

1. **Bug reports.** Attach a .k3dm and the exact geometry that misbehaved can be reproduced
   without owning the craft or the mod list.

2. **Watertight mesh development.** The watertighting algorithm has too many unknowns to
   iterate on inside KSP, where one test cycle costs minutes. Dump once, then iterate against
   the dump in the offline harness in seconds.

## Dump-only runs are legal

The format validation accepts a dump with no export format selected. Somebody filing a bug
report should not be forced to also export an STL they do not want.

## Format

All little-endian, which is what `BinaryWriter` emits on every platform KSP runs on.

    magic       char[4]   "K3DM"
    version     int32     1
    scale       float32   the user scale the triangles were already multiplied by
    craftName   string    BinaryWriter length-prefixed UTF-8 (7-bit encoded length)
    partCount   int32
    parts       partCount x { int32 partIndex, string name }
    triCount    int32
    triangles   triCount x { float32 ax,ay,az, bx,by,bz, cx,cy,cz, int32 partIndex }

40 bytes per triangle. A 500k-triangle craft is about 20 MB.

Only parts that actually contributed geometry get a name entry. A part that was excluded
(launch clamp, hidden variant, disabled renderer) has a name in `ctx.PartNames` but no
triangles, and carrying it into the dump would just be noise.

`PartIndex` is the load-bearing field. It is what lets the offline tooling evaluate the winding
number **per part** rather than globally, which is what makes the boolean union free and stops
one inverted part from punching a hole through its neighbours.

## Reading it

`harness/synth.py` has `read_dump()`. Round-trip verified against bytes synthesised exactly the
way `BinaryWriter` lays them out, including the 7-bit length prefix on strings.

    python3 run.py --dump YourCraft.k3dm --voxel 0.8
