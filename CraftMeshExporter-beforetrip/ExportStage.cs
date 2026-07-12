namespace CraftMeshExporter
{
    internal enum ExportStage
    {
        Validate,
        PrepareOutput,
        RefreshEngineList,
        SetShroudVisibility,
        WritePartDiagnostics,
        CollectMeshes,
        RemoveBadTriangles,
        RemoveDuplicateTriangles,
        WriteStl,
        WriteStlInstructions,
        WriteObj,
        WriteObjInstructions,
        RestoreShroudVisibility,
        OpenViewer,
        Finished,
        Failed,
        Cancelled
    }
}
