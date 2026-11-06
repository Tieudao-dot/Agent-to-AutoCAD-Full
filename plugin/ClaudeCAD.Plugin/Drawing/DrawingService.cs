using System.Collections.Generic;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// All drawing tool handlers (spec §7). Split into partial files per tool group;
    /// each group has a Register* method called from <see cref="RegisterAll"/>.
    /// </summary>
    public static partial class DrawingService
    {
        public static string PluginVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public static void RegisterAll(ToolDispatcher d, ITurnHost host)
        {
            RegisterSystem(d, host);
            // phase 1b
            RegisterDraw(d);
            RegisterLayers(d);
            RegisterBatchAndInfo(d);
            // phase 1c
            RegisterQuery(d);
            RegisterModify(d);
            // phase 1d
            RegisterAnnotation(d);
            RegisterBlocks(d);
            RegisterStyles(d);
            RegisterView(d, host);
            // phase 2
            RegisterLayouts(d);
            RegisterXrefs(d);
            RegisterTrimExtend(d);
            // v0.6: Claude looks at the drawing
            RegisterCapture(d);
        }

        private static void RegisterSystem(ToolDispatcher d, ITurnHost host)
        {
            // Phase 1a: two-way IPC check. Read-only, no document required.
            d.Register(new ToolDef("ping", false, (ctx, p) =>
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                return new Dictionary<string, object>
                {
                    ["pong"] = true,
                    ["echo"] = p.Get("message"),
                    ["plugin_version"] = PluginVersion,
                    ["acad_version"] = AcApp.Version.ToString(),
                    ["drawing"] = doc != null ? System.IO.Path.GetFileName(doc.Name) : null,
                };
            }, noDocument: true));
        }
    }
}
