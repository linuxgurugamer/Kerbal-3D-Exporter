#if false
namespace CraftMeshExporter
{
    public class ModuleCraftPrintExporter : PartModule
    {
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Open Craft Exporter")]
        public void OpenExporter()
        {
            CraftPrintExporterWindow.OpenWindow();
        }
    }
}
#endif