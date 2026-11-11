namespace ClaudeCAD.Plugin.UI
{
    public static partial class AcadTheme
    {
        /// <summary>
        /// The palette follows AutoCAD's theme-changed event (true) or only the COLORTHEME variable (false).
        /// For CLAUDECHATSELFTEST.
        /// </summary>
        public static bool IsSubscribed => _handler != null;

        /// <summary>How many palette colors <see cref="Read"/> can take from AutoCAD at most.</summary>
        public static int ColorCount => Map.Length;
    }
}
