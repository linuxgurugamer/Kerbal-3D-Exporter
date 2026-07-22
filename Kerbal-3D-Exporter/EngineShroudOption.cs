namespace Kerbal_3D_Exporter
{
    /// <summary>
    /// One row in the window's shroud list.
    ///
    /// Despite the name (kept so existing call sites and saved settings still line up) this is no
    /// longer engine-only. A structural tube, an engine plate or a procedural fairing base bears a
    /// shell that needs hiding just as much as an engine shroud does, and until now none of them
    /// could appear here at all.
    /// </summary>
    internal sealed class EngineShroudOption
    {
        public Part Part;
        public string DisplayName;
        public bool ShowShroud;

        /// <summary>
        /// True when this part IS the shell rather than merely carrying one -- a structural tube,
        /// a fairing, a service bay, an engine plate wrapped around its contents.
        ///
        /// The distinction changes what gets hidden. An engine with a ModuleJettison shroud only
        /// loses the shroud mesh; the engine itself must stay. An enclosing part IS the thing in
        /// the way, so hiding it means hiding all of its own geometry.
        /// </summary>
        public bool IsEnclosure;

        public EngineShroudOption(Part part, string displayName, bool showShroud)
            : this(part, displayName, showShroud, false)
        {
        }

        public EngineShroudOption(Part part, string displayName, bool showShroud, bool isEnclosure)
        {
            Part = part;
            DisplayName = displayName;
            ShowShroud = showShroud;
            IsEnclosure = isEnclosure;
        }
    }
}
