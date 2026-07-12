/// Disabled for now, can be reenabled if desired to use the PAW instead of a button in the editor or flight scene

#if false
namespace Kerbal-3D-Exporter
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