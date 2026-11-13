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

        /// <summary>Which of the three builds is loaded (PackageContents.xml picks it by AutoCAD release).</summary>
        public static string PluginBuild =>
#if ACAD_R24
            "AutoCAD 2022–2024 (net48)";
#elif ACAD_R26
            "AutoCAD 2027 (net10.0)";
#else
            "AutoCAD 2025–2026 (net8.0)";
#endif

        /// <summary>The .NET runtime AutoCAD actually runs: 2025 / 2026 move from .NET 8 to .NET 10 with their updates.</summary>
        public static string RuntimeName =>
#if ACAD_R24
            ".NET Framework " + System.Environment.Version;
#else
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
#endif

        /// <summary>One line for the log, the self-test and the bug report: build, runtime, AutoCAD release.</summary>
        public static string BuildInfo
        {
            get
            {
                string acad = "?";
                try { acad = AcApp.Version + " · " + System.Convert.ToString(AcApp.GetSystemVariable("ACADVER")); } catch { }
                string edition = "?";
                try { edition = EditionLogic.Label(Sidecar.UpdateService.BundleEdition); } catch { }
                return $"{ProductInfo.DisplayName} {PluginVersion} · edition {edition} · bản {PluginBuild} · runtime {RuntimeName} · AutoCAD {acad}";
            }
        }

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
            // nâng cấp đợt B: stretch / break / lengthen / divide (2D), group / match_properties (every plan)
            RegisterEdit2D(d);
            RegisterUtil2D(d);
            // v0.6: Claude looks at the drawing
            RegisterCapture(d);
            // 3D phase 1 (ToolGroup.ThreeD)
            RegisterSolids(d);
            // 3D phase 2 (ToolGroup.ThreeD)
            RegisterSolids2(d);
            RegisterPlanTo3d(d);
            RegisterSections(d);
            // nâng cấp đợt D (ToolGroup.ThreeD): beams, PRESSPULL, STL, volume schedule
            RegisterSolids3(d);
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
                    ["plugin_build"] = PluginBuild,
                    ["runtime"] = RuntimeName,
                    ["acad_version"] = AcApp.Version.ToString(),
                    ["drawing"] = doc != null ? System.IO.Path.GetFileName(doc.Name) : null,
                };
            }, noDocument: true));
        }
    }
}
