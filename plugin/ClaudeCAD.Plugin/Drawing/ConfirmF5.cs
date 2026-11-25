using System.Collections.Generic;
using System.Linq;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt F5: the Đồng ý card before overwriting a file / a saved view or giving a material to whole layers
    /// (Core/F5.cs, F5Confirm). A macro replay keeps the rules of before: no card can be answered there, so the old
    /// overwrite / replace parameters decide as they did.
    /// </summary>
    public static partial class DrawingService
    {
        /// <summary>Refuses the call (nothing changed) until the user approved these handles on a card; a macro replay passes.</summary>
        internal static void RequireCard(ToolContext ctx, Params p, string tool, string what, IEnumerable<string> handles)
        {
            if (ctx.Dispatcher?.MacroRunning == true) return;
            var missing = F5Confirm.Missing(handles, p.Get(SolidLogic.UserConfirmedParam));
            if (missing.Count > 0) throw new ToolException(ErrorCodes.InvalidParams, F5Confirm.CardMessage(tool, what, missing));
        }

        /// <summary>
        /// An existing output file: the card (its token) outside a macro; in a macro replay the old rule (oldFlag true =
        /// overwrite, else the old refusal). Nothing to ask when the file does not exist.
        /// </summary>
        internal static void RequireOverwriteCard(ToolContext ctx, Params p, string tool, string fullPath, bool exists, string oldRefusal, params string[] oldFlags)
        {
            if (!exists) return;
            if (ctx.Dispatcher?.MacroRunning == true)
            {
                if (!oldFlags.Any(f => p.Bool(f, false))) throw ToolException.Invalid(oldRefusal);
                return;
            }
            RequireCard(ctx, p, tool, $"overwrite the existing file '{fullPath}'", new[] { F5Confirm.FileToken(fullPath) });
        }
    }
}
