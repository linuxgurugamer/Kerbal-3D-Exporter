using System.IO;
using KSP.UI.Screens;
using ToolbarControl_NS;
using UnityEngine;

using KSP_Log;
using static Kerbal_3D_Exporter.Kerbal3DExporter_ToolbarRegistration;


namespace Kerbal_3D_Exporter
{
    // ToolbarControl (linuxgurugamer, https://github.com/linuxgurugamer/ToolbarControl) is a shared
    // community library that lets a single button definition show up on EITHER Blizzy's Toolbar (if
    // installed) or KSP's stock ApplicationLauncher (otherwise), without this mod needing to detect
    // and branch between the two itself -- ToolbarControl owns that entirely internally.
    //
    // IMPORTANT: this file was written from the well-established, widely-copied ToolbarControl
    // integration pattern (RegisterMod once at startup, then AddToAllToolbars per scene), but has
    // NOT been compiled against the actual ToolbarControl.dll -- that library isn't available in
    // this environment. Before this will build:
    //   1. Download ToolbarControl.dll and ClickThroughBlocker.dll and place them at
    //      GameData/000_ToolbarControl/Plugins/ (the shared location every mod using ToolbarControl
    //      points at -- see the Reference entries in Kerbal-3D-Exporter.csproj).
    //   2. Double-check the AddToAllToolbars signature/parameter order and the button-state methods
    //      used in NotifyWindowClosed() below against whatever ToolbarControl version you're
    //      building against -- minor API differences between releases are possible, and this is the
    //      part with the least certainty behind it.

    // Registers this mod with ToolbarControl exactly once, before any scene loads. This is separate
    // from actually creating the button (see Kerbal3DExporter_ToolbarButton below), and every mod
    // using ToolbarControl needs to do this with its own unique MODID/MODNAME pair.

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class Kerbal3DExporter_ToolbarRegistration : MonoBehaviour
    {
        public static Log Log;

        public void Start()
        {
            Log = new Log("Kerbal-3D-Exporter");
            Log.SetLevel(Log.LEVEL.INFO);
            Log.Info("Logging initialized");

            ToolbarControl.RegisterMod(Kerbal3DExporter_ToolbarButton.MODID, Kerbal3DExporter_ToolbarButton.MODNAME);
        }
    }

    // Adds the actual button via ToolbarControl. EveryScene (rather than a single Startup value) is
    // used because the button needs to be re-registered whenever the relevant scenes are (re)loaded,
    // same as the previous stock-only implementation this replaces.
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class Kerbal3DExporter_ToolbarButton : MonoBehaviour
    {
        public const string MODID = "Kerbal3DExporter_Button";
        public const string MODNAME = "Kerbal 3D Exporter";

        // GameDatabase-style paths: relative to GameData, no file extension.
        private const string StockIcon = "toolbar-icon";
        private const string BlizzyIcon = "toolbar-icon-24";

        static private ToolbarControl toolbarControl = null;

        public void Start()
        {
            DontDestroyOnLoad(this);
            AddButton();
        }


        private void AddButton()
        {
            if (toolbarControl != null)
                return;

            toolbarControl = gameObject.AddComponent<ToolbarControl>();
            toolbarControl.AddToAllToolbars(
                OnTrue,
                OnFalse,
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.FLIGHT,
                MODID,
                "craftMeshExporterButton",
                Path.Combine(Utils.GetIconPath, StockIcon),
                Path.Combine(Utils.GetIconPath, BlizzyIcon),
                MODNAME);

        }

        private void OnTrue()
        {
            CraftPrintExporterWindow.OpenWindow();
        }

        private void OnFalse()
        {
            CraftPrintExporterWindow.CloseWindow();
        }

        // Called by CraftPrintExporterWindow.Close() (i.e. the in-window "Close" button) so the
        // toolbar icon's pressed/highlighted state doesn't get stuck "on" after the window is
        // closed some way other than clicking the toolbar icon itself.
        public static void NotifyWindowClosed()
        {
            SetButtonState(false);
        }

        private static void SetButtonState(bool active)
        {
            // ToolbarControl instances are per-scene (a fresh one is created by each addon
            // instance), so this looks up whichever one currently exists rather than caching a
            // single static reference.
            Kerbal3DExporter_ToolbarButton instance = FindObjectOfType<Kerbal3DExporter_ToolbarButton>();
            if (instance == null || toolbarControl == null)
                return;

            if (active)
                toolbarControl.SetTrue(false);
            else
                toolbarControl.SetFalse(false);
        }
    }
}
