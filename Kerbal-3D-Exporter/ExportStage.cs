namespace Kerbal_3D_Exporter
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
        Write3mf,
        Write3mfInstructions,
        WriteStp,
        WriteStpInstructions,
        RestoreShroudVisibility,
        OpenViewer,
        Finished,
        Failed,
        Cancelled
    }
}
