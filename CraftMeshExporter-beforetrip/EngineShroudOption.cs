namespace CraftMeshExporter
{
    internal sealed class EngineShroudOption
    {
        public Part Part;
        public string DisplayName;
        public bool ShowShroud;

        public EngineShroudOption(Part part, string displayName, bool showShroud)
        {
            Part = part;
            DisplayName = displayName;
            ShowShroud = showShroud;
        }
    }
}
