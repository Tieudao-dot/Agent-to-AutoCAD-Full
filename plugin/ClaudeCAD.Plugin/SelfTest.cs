using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using ClaudeCAD.Plugin.UI;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin
{
    /// <summary>
    /// CLAUDECHATSELFTEST: exercises, inside the real AutoCAD, the parts that cannot be tested on CI
    /// (off-screen picture, undo group, database events, snapshot, version copy, read-only tab,
    /// layer / name lists, assistant connection, AutoCAD's palette theme, status bar icon, compact mode,
    /// the right build for this AutoCAD) on a NEW temporary drawing — the user's drawings are not touched,
    /// and the last step checks exactly that. Writes a ✓/✗ report to the log folder and opens it.
    /// Every step is isolated: one that fails (or throws) never stops the ones after it.
    /// </summary>
    public static class SelfTest
    {
        private sealed class Step
        {
            public string Name;
            public bool Ok;
            public string Detail;
            public long Ms;
        }

        public static string Run()
        {
            var steps = new List<Step>();
            var dm = AcApp.DocumentManager;
            var original = dm.MdiActiveDocument;
            CleanOldRuns();
            var dir = Path.Combine(Path.GetTempPath(), "ClaudeCAD-selftest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(dir);
            var dwg = Path.Combine(dir, "selftest.dwg");
            Document doc = null;
            Document versionDoc = null;
            var handles = new List<string>();

            // what the user's drawing looks like before anything happens (compared again at the very end)
            DrawingState before = null;
            try { before = DrawingState.Of(original); } catch (Exception ex) { Logger.Warn("selftest: drawing state not read: " + ex.Message); }

            // a step whose verdict (✓ / ✗ + why) comes from a rule instead of an exception
            void Check(string name, Func<SelfTestRules.Verdict> check)
            {
                var sw = Stopwatch.StartNew();
                var st = new Step { Name = name };
                try
                {
                    var v = check();
                    st.Ok = v.Ok;
                    st.Detail = v.Detail;
                }
                catch (Exception ex)
                {
                    st.Ok = false;
                    st.Detail = ex.GetType().Name + ": " + ex.Message;
                    Logger.Error("selftest: " + name, ex);
                }
                st.Ms = sw.ElapsedMilliseconds;
                steps.Add(st);
            }

            // steps taken from the controller: a throw there must not lose the steps already done
            void Collect(string what, Func<List<(string name, bool ok, string detail)>> source)
            {
                try
                {
                    foreach (var (name, ok, detail) in source())
                        steps.Add(new Step { Name = name, Ok = ok, Detail = detail });
                }
                catch (Exception ex)
                {
                    Logger.Error("selftest: " + what, ex);
                    steps.Add(new Step { Name = what, Ok = false, Detail = ex.GetType().Name + ": " + ex.Message });
                }
            }

            void Do(string name, Func<string> action)
            {
                var sw = Stopwatch.StartNew();
                var st = new Step { Name = name };
                try
                {
                    st.Detail = action() ?? "";
                    st.Ok = true;
                }
                catch (Exception ex)
                {
                    st.Ok = false;
                    st.Detail = ex.GetType().Name + ": " + ex.Message;
                    Logger.Error("selftest: " + name, ex);
                }
                st.Ms = sw.ElapsedMilliseconds;
                steps.Add(st);
            }

            Do("Phiên bản AutoCAD / plugin", () => DrawingService.BuildInfo);

            Do("Tạo bản vẽ thử (không đụng bản vẽ của cậu)", () =>
            {
                try { doc = DocumentCollectionExtension.Add(dm, "acadiso.dwt"); }
                catch { doc = DocumentCollectionExtension.Add(dm, "acad.dwt"); }
                dm.MdiActiveDocument = doc;
                return Path.GetFileName(doc.Name);
            });
            if (doc == null)
            {
                // no test drawing: the checks that need none still run
                UiChecks(Check, Collect);
                return Report(steps, dir);
            }

            Do("Vẽ bằng API (khóa bản vẽ + transaction)", () =>
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var db = doc.Database;
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    var layer = new LayerTableRecord { Name = "CC-SELFTEST" };
                    lt.Add(layer);
                    tr.AddNewlyCreatedDBObject(layer, true);
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                    var ents = new Entity[]
                    {
                        new Line(new Point3d(0, 0, 0), new Point3d(4000, 0, 0)) { Layer = "CC-SELFTEST" },
                        new Circle(new Point3d(2000, 1500, 0), Vector3d.ZAxis, 500) { Layer = "CC-SELFTEST" },
                        new DBText { Position = new Point3d(0, 2500, 0), Height = 250, TextString = "ClaudeCAD self-test", Layer = "CC-SELFTEST" },
                    };
                    foreach (var e in ents)
                    {
                        ms.AppendEntity(e);
                        tr.AddNewlyCreatedDBObject(e, true);
                        handles.Add(e.Handle.ToString());
                    }
                    tr.Commit();
                }
                return $"3 đối tượng: {string.Join(", ", handles)}";
            });

            Do("Lưu bản vẽ thử", () =>
            {
                using (doc.LockDocument()) doc.Database.SaveAs(dwg, DwgVersion.Current);
                return dwg;
            });

            Do("Nhóm hoàn tác theo lượt (StartUndoMark / EndUndoMark)", () =>
            {
                var undo = new UndoScope();
                undo.EnsureStarted(doc);
                bool opened = undo.IsOpen;
                undo.End();
                if (!opened) throw new InvalidOperationException("không mở được nhóm hoàn tác (lượt chat sẽ cần nhiều lệnh U)");
                return "mở và đóng được";
            });

            Do("Sự kiện bản vẽ (ghi nhận sửa tay)", () =>
            {
                var key = DrawingHistory.KeyOf(doc) ?? ChatTranscript.KeyFor(dwg);
                DrawingHistory.Instance.Watch(doc);
                DrawingHistory.Instance.TakeManualChanges(doc, key, dwg, false, out _); // reset
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var id = Acad.IdFromHandle(doc.Database, handles[0]);
                    var line = (Line)tr.GetObject(id, OpenMode.ForWrite);
                    line.EndPoint = new Point3d(5000, 0, 0);
                    tr.Commit();
                }
                var report = DrawingHistory.Instance.TakeManualChanges(doc, key, dwg, true, out var shortText);
                if (report == null) throw new InvalidOperationException("sửa một đường thẳng nhưng không ghi nhận được");
                return shortText + " — " + report["text"];
            });

            Do("Biên bản (snapshot) sau lượt", () =>
            {
                var key = DrawingHistory.KeyOf(doc) ?? ChatTranscript.KeyFor(dwg);
                var sw = Stopwatch.StartNew();
                DrawingHistory.Instance.AfterTurn(doc, key, dwg);
                var file = DrawingHistory.LocalFile(key);
                if (!File.Exists(file)) throw new FileNotFoundException("không thấy file biên bản", file);
                var snap = DrawingSnapshot.FromBytes(File.ReadAllBytes(file));
                DrawingHistory.Instance.Reset(doc, key, dwg); // do not leave the test snapshot behind
                return $"{snap.Entries.Count} đối tượng, {new FileInfo(file).Length / 1024.0:0.0} KB trước khi xóa, {sw.ElapsedMilliseconds} ms";
            });

            Do("Chụp ảnh bản vẽ ẩn (capture_view)", () =>
            {
                var r = Capture.Render(doc, new Point3d(-500, -500, 0), new Point3d(5500, 3500, 0), 800, "window");
                if (r.Png == null || r.Png.Length < 500) throw new InvalidOperationException("ảnh rỗng");
                File.WriteAllBytes(Path.Combine(dir, "capture.png"), r.Png);
                return $"{r.Width}×{r.Height}, {r.Png.Length / 1024.0:0.0} KB (ảnh: dòng «Ảnh chụp thử» bên dưới, hoặc capture.png trong thư mục thử)";
            });

            Do("Danh sách layer / tên cho @", () =>
            {
                var rows = Interaction.LayerRows(doc);
                var names = Interaction.DrawingNames(doc);
                var test = rows.FirstOrDefault(r => r.Name == "CC-SELFTEST");
                if (test == null || test.Count != 3) throw new InvalidOperationException($"layer CC-SELFTEST đếm {(test == null ? "không thấy" : test.Count.ToString())}, cần 3");
                return $"{rows.Count} layer, {names.Count} tên";
            });

            // 3D phase 1: the same code paths as draw_box / draw_cylinder / solid_subtract / set_view_3d / capture_view
            string solidHandle = null;
            Check("Khối 3D: dựng hộp, trừ trụ, kiểm thể tích", () =>
            {
                double l = 1000, w = 800, h = 600, r = 200;
                double volume;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var db = doc.Database;
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                    var box = DrawingService.MakeBox(l, w, h, new double[] { 6000, 0, 0 }, null, 0);
                    box.SetDatabaseDefaults(db);
                    ms.AppendEntity(box);
                    tr.AddNewlyCreatedDBObject(box, true);
                    var cyl = DrawingService.MakeFrustum(new double[] { 6500, 400, 0 }, new Vector3d(0, 0, h), r, r);
                    cyl.SetDatabaseDefaults(db);
                    ms.AppendEntity(cyl);
                    tr.AddNewlyCreatedDBObject(cyl, true);
                    DrawingService.BooleanInto(box, cyl, BooleanOperationType.BoolSubtract);
                    if (!cyl.IsErased) cyl.Erase();
                    volume = box.MassProperties.Volume;
                    solidHandle = box.Handle.ToString();
                    tr.Commit();
                }
                return SelfTestRules.CheckVolume("hộp 1000×800×600 trừ trụ R200", volume,
                    SolidLogic.BoxVolume(l, w, h) - SolidLogic.FrustumVolume(r, r, h));
            });

            Do("Góc nhìn 3D (SW iso) + kiểu Conceptual + ảnh 3D", () =>
            {
                if (solidHandle == null) throw new InvalidOperationException("bước dựng khối 3D chưa chạy được");
                var iso = SolidLogic.Preset("sw_iso");
                var ext = Capture.ModelExtents(doc);
                DrawingService.ApplyView3d(doc, iso, ext?.MinPoint, ext?.MaxPoint);
                DrawingService.ApplyVisualStyle(doc, SolidLogic.VisualStyleName("conceptual"), "selftest");
                var r = Capture.Render(doc, null, null, 800, null, iso, SolidLogic.VisualStyleName("conceptual"));
                if (r.Png == null || r.Png.Length < 500) throw new InvalidOperationException("ảnh 3D rỗng");
                File.WriteAllBytes(Path.Combine(dir, "capture-3d.png"), r.Png);
                return $"{r.Width}×{r.Height}, {r.Png.Length / 1024.0:0.0} KB (capture-3d.png trong thư mục thử)";
            });

            // 3D phase 2: the same code paths as get_solid_info (edges), fillet_edges, slice_solid, check_interference
            Solid3d AddBox(Transaction tr, double l, double w, double h, double[] corner)
            {
                var db = doc.Database;
                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                var box = DrawingService.MakeBox(l, w, h, corner, null, 0);
                box.SetDatabaseDefaults(db);
                ms.AppendEntity(box);
                tr.AddNewlyCreatedDBObject(box, true);
                return box;
            }
            Check("Khối 3D đợt 2: bo 4 cạnh đứng R100, kiểm thể tích", () =>
            {
                double l = 1000, w = 800, h = 600, r = 100;
                double volume;
                int picked;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var box = AddBox(tr, l, w, h, new double[] { 9000, 0, 0 });
                    var t = DrawingService.ReadTopology(box);
                    var edges = Solid2Logic.FilterEdges(t.Edges, new[] { "parallel_z" }, t.Min, t.Max, "selftest");
                    picked = edges.Count;
                    if (picked != 4) throw new InvalidOperationException($"bộ lọc parallel_z chọn {picked} cạnh, cần 4 (hộp có {t.Edges.Count} cạnh)");
                    DrawingService.FilletSolid(box, edges.Select(i => t.EdgeIds[i]).ToList(), r, "selftest");
                    volume = box.MassProperties.Volume;
                    tr.Commit();
                }
                return SelfTestRules.CheckVolume($"hộp 1000×800×600 bo {picked} cạnh đứng R100", volume, Solid2Logic.FilletedBoxVolume(l, w, h, r, 4));
            });

            Check("Khối 3D đợt 2: cắt đôi bằng mặt phẳng, mỗi nửa ~½", () =>
            {
                double l = 1000, w = 800, h = 600;
                double v1, v2;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var box = AddBox(tr, l, w, h, new double[] { 12000, 0, 0 });
                    var plane = new PlaneDef { Point = new double[] { 12000 + l / 2, 0, 0 }, Normal = new double[] { 1, 0, 0 } };
                    using (var other = DrawingService.SliceSolid(box, plane, "both", "selftest"))
                    {
                        v1 = box.MassProperties.Volume;
                        v2 = other?.MassProperties.Volume ?? 0;
                    }
                    tr.Commit();
                }
                var half = SolidLogic.BoxVolume(l, w, h) / 2;
                var verdict = SelfTestRules.CheckVolume("nửa dương", v1, half);
                if (!SolidLogic.VolumeMatches(v2, half)) return new SelfTestRules.Verdict(false, verdict.Detail + $"; nửa âm {v2 / 1e9:0.######} m³, cần {half / 1e9:0.######} m³");
                return verdict;
            });

            Check("Khối 3D đợt 2: hai hộp chồng nhau, check_interference thấy va chạm", () =>
            {
                double overlap;
                bool hit;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var a = AddBox(tr, 1000, 1000, 1000, new double[] { 15000, 0, 0 });
                    var b = AddBox(tr, 1000, 1000, 1000, new double[] { 15500, 0, 0 });
                    hit = a.CheckInterference(b);
                    overlap = DrawingService.OverlapVolume(a, b) ?? double.NaN;
                    tr.Commit();
                }
                if (!hit) return new SelfTestRules.Verdict(false, "CheckInterference báo không va chạm");
                return SelfTestRules.CheckVolume("phần chồng 500×1000×1000", overlap, 500.0 * 1000 * 1000);
            });

            // 3D phase 3: the same code paths as build_3d_from_plan and section_view (handlers called directly)
            string planWalls = null;
            List<object> planWallLines = null, planDoorLines = null; // the plan of the room, read again by the đợt A step
            const double px = 20000; // a 4000 × 3000 room by its axes, wall 200, a 900 door gap in the bottom wall
            Check("3D đợt 3: dựng 3D phòng 4×3 m có cửa đi, kiểm thể tích tường", () =>
            {
                Dictionary<string, object> r;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var db = doc.Database;
                    db.Insunits = UnitsValue.Millimeters; // the test drawing only: the plan is drawn in mm
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                    string Add(Entity e) { e.SetDatabaseDefaults(db); ms.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true); return e.Handle.ToString(); }
                    Polyline Pl(params double[] xy)
                    {
                        var pl = new Polyline();
                        for (int i = 0; i < xy.Length; i += 2) pl.AddVertexAt(i / 2, new Point2d(px + xy[i], xy[i + 1]), 0, 0, 0);
                        return pl;
                    }
                    var walls = new List<object>
                    {
                        Add(Pl(2450, -100, 4100, -100, 4100, 3100, -100, 3100, -100, -100, 1550, -100)),
                        Add(Pl(2450, 100, 3900, 100, 3900, 2900, 100, 2900, 100, 100, 1550, 100)),
                        Add(new Line(new Point3d(px + 1550, -100, 0), new Point3d(px + 1550, 100, 0))),
                        Add(new Line(new Point3d(px + 2450, -100, 0), new Point3d(px + 2450, 100, 0))),
                    };
                    var door = new List<object>
                    {
                        Add(new Line(new Point3d(px + 1550, 100, 0), new Point3d(px + 1550, 1000, 0))),
                        Add(new Arc(new Point3d(px + 1550, 100, 0), 900, 0, Math.PI / 2)),
                    };
                    planWallLines = walls;
                    planDoorLines = door;
                    var ctx = new ToolContext(doc, tr, true, null);
                    var p = new Params("build_3d_from_plan", new Dictionary<string, object>
                    {
                        ["wall_handles"] = walls, ["door_handles"] = door, ["wall_height"] = 3000.0, ["door_height"] = 2100.0,
                        ["slab"] = "none", ["wall_layer_3d"] = "0", ["on_existing"] = "add",
                    });
                    r = (Dictionary<string, object>)DrawingService.Build3dFromPlan(ctx, p);
                    tr.Commit();
                }
                var ws = (Dictionary<string, object>)r["wall_solid"];
                planWalls = (string)ws["handle"];
                if (Convert.ToInt32(r["walls_count"]) != 4 || Convert.ToInt32(r["openings_count"]) != 1)
                    return new SelfTestRules.Verdict(false, $"nhận {r["walls_count"]} tường, {r["openings_count"]} cửa (cần 4 tường, 1 cửa)");
                // footprint 4200×3200 − 3800×2800 − door 200×900, × 3000, + lintel 200×900×900
                double expected = (4200.0 * 3200 - 3800.0 * 2800 - 200 * 900) * 3000 + 200.0 * 900 * 900;
                return SelfTestRules.CheckVolume("tường phòng 4×3 m, 1 cửa đi", Convert.ToDouble(ws["volume_m3"]) * 1e9, expected);
            });

            Check("3D đợt 3: cắt ngang tường ở cao 1 m, có đường bao vết cắt + hatch", () =>
            {
                if (planWalls == null) return new SelfTestRules.Verdict(false, "bước dựng 3D từ mặt bằng chưa chạy được");
                Dictionary<string, object> r;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var ctx = new ToolContext(doc, tr, true, null);
                    var p = new Params("section_view", new Dictionary<string, object>
                    {
                        ["handles"] = new List<object> { planWalls }, ["point"] = new List<object> { px, 0.0, 1000.0 }, ["plane"] = "xy",
                    });
                    r = (Dictionary<string, object>)DrawingService.SectionView(ctx, p);
                    tr.Commit();
                }
                int outline = ((System.Collections.ICollection)r["outline_handles"]).Count, hatch = ((System.Collections.ICollection)r["hatch_handles"]).Count;
                double area = Convert.ToDouble(r["cut_area_m2"]), want = (4200.0 * 3200 - 3800.0 * 2800 - 200 * 900) / 1e6;
                var detail = string.Format(CultureInfo.InvariantCulture, "{0} đường bao, {1} hatch, diện tích cắt {2:0.####} m² (cần {3:0.####} m²)", outline, hatch, area, want);
                return new SelfTestRules.Verdict(outline > 0 && hatch == 1 && Math.Abs(area - want) <= 1e-3 * want, detail);
            });

            // nâng cấp đợt A: the same code paths as build_3d_from_plan with storeys + columns (the storeys above are copies
            // of the first one moved up) and build_railing
            Check("Nâng cấp đợt A: dựng 2 tầng + sàn mái + 2 cột từ mặt bằng, kiểm cao độ và thể tích", () =>
            {
                if (planWallLines == null) return new SelfTestRules.Verdict(false, "bước dựng 3D từ mặt bằng chưa chạy được");
                Dictionary<string, object> r;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var db = doc.Database;
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                    string Add(Entity e) { e.SetDatabaseDefaults(db); ms.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true); return e.Handle.ToString(); }
                    // a 300 × 300 column and a round D300 column inside the room
                    var sq = new Polyline();
                    sq.AddVertexAt(0, new Point2d(px + 1000, 1000), 0, 0, 0);
                    sq.AddVertexAt(1, new Point2d(px + 1300, 1000), 0, 0, 0);
                    sq.AddVertexAt(2, new Point2d(px + 1300, 1300), 0, 0, 0);
                    sq.AddVertexAt(3, new Point2d(px + 1000, 1300), 0, 0, 0);
                    sq.Closed = true;
                    var cols = new List<object> { Add(sq), Add(new Circle(new Point3d(px + 3000, 2000, 0), Vector3d.ZAxis, 150)) };
                    var ctx = new ToolContext(doc, tr, true, null);
                    var p = new Params("build_3d_from_plan", new Dictionary<string, object>
                    {
                        ["wall_handles"] = planWallLines, ["door_handles"] = planDoorLines, ["column_handles"] = cols,
                        ["storeys"] = 2.0, ["storey_height"] = 3000.0, ["slab_thickness"] = 150.0, ["door_height"] = 2100.0, ["roof_slab"] = true,
                        ["slab"] = "bbox", ["base_z"] = 10000.0,
                        ["wall_layer_3d"] = "0", ["slab_layer_3d"] = "0", ["column_layer_3d"] = "0", ["on_existing"] = "add",
                    });
                    r = (Dictionary<string, object>)DrawingService.Build3dFromPlan(ctx, p);
                    tr.Commit();
                }
                var levels = (List<object>)r["levels"];
                if (levels.Count != 3) return new SelfTestRules.Verdict(false, $"{levels.Count} mức (cần 3: tầng 1, tầng 2, sàn mái)");
                var wall2 = (Dictionary<string, object>)((Dictionary<string, object>)levels[1])["wall_solid"];
                double z2 = Convert.ToDouble(((List<object>)wall2["min"])[2]), top2 = Convert.ToDouble(((List<object>)wall2["max"])[2]);
                // storey 2 starts 3000 above storey 1 (base 10000); its walls are storey height − slab = 2850 high
                if (Math.Abs(z2 - 13000) > 0.01 || Math.Abs(top2 - 15850) > 0.01)
                    return new SelfTestRules.Verdict(false, string.Format(CultureInfo.InvariantCulture, "tường tầng 2 từ z {0:0.###} tới {1:0.###} (cần 13000 tới 15850)", z2, top2));
                var total = (Dictionary<string, object>)r["total_volume_m3"];
                double wallOne = (4200.0 * 3200 - 3800.0 * 2800 - 200 * 900) * 2850 + 200.0 * 900 * 750; // lintel 2850 − 2100
                double colOne = (300.0 * 300 + Math.PI * 150 * 150) * 2850;
                double slabOne = 4200.0 * 3200 * 150;
                var vw = SelfTestRules.CheckVolume("tường 2 tầng", Convert.ToDouble(total["walls"]) * 1e9, 2 * wallOne);
                var vc = SelfTestRules.CheckVolume("2 cột × 2 tầng", Convert.ToDouble(total["columns"]) * 1e9, 2 * colOne);
                var vs = SelfTestRules.CheckVolume("2 sàn + sàn mái", Convert.ToDouble(total["slabs"]) * 1e9, 3 * slabOne);
                return new SelfTestRules.Verdict(vw.Ok && vc.Ok && vs.Ok, vw.Detail + "; " + vc.Detail + "; " + vs.Detail);
            });

            Check("Nâng cấp đợt A: lan can thẳng 3 m, kiểm số trụ và thể tích", () =>
            {
                Dictionary<string, object> r;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var ctx = new ToolContext(doc, tr, true, null);
                    var p = new Params("build_railing", new Dictionary<string, object>
                    {
                        ["path_points"] = new List<object> { new List<object> { px + 6000.0, 0.0 }, new List<object> { px + 9000.0, 0.0 } },
                        ["layer"] = "0",
                    });
                    r = (Dictionary<string, object>)DrawingService.BuildRailing(ctx, p);
                    tr.Commit();
                }
                if (Convert.ToInt32(r["posts"]) != 3) return new SelfTestRules.Verdict(false, $"{r["posts"]} trụ (cần 3: hai đầu và giữa)");
                // a straight railing: its pieces only touch, so the united solid's volume is the sum of the pieces
                var lay = RailingLogic.Build(new List<double[]> { new[] { 0.0, 0.0 }, new[] { 3000.0, 0.0 } }, false, new RailingLogic.Options());
                return SelfTestRules.CheckVolume("lan can 3 m cao 1100, song đứng", Convert.ToDouble(r["volume_m3"]) * 1e9, lay.Boxes.Sum(b => b.Volume));
            });

            string versionFile = Path.Combine(dir, "version-copy.dwg");
            Do("Bản sao phiên bản (Wblock + SaveAs)", () =>
            {
                using (doc.LockDocument())
                using (var copy = doc.Database.Wblock())
                    copy.SaveAs(versionFile, DwgVersion.Current);
                return $"{new FileInfo(versionFile).Length / 1024.0:0.0} KB";
            });

            Do("Mở phiên bản trong tab chỉ xem rồi đóng", () =>
            {
                versionDoc = DocumentCollectionExtension.Open(dm, versionFile, true);
                bool readOnly = versionDoc.IsReadOnly;
                DocumentExtension.CloseAndDiscard(versionDoc);
                versionDoc = null;
                dm.MdiActiveDocument = doc;
                return readOnly ? "mở chỉ xem, đóng được" : "mở được nhưng không ở chế độ chỉ xem";
            });

            Do("Phím tắt Ctrl+Shift+Space", () => PluginApp.HotkeyEnabled ? "đã bật (thử bấm sau khi test xong)" : "đang tắt trong ⚙");

            UiChecks(Check, Collect);

            // clean up: close the test drawing, back to the user's drawing
            try { if (versionDoc != null && !versionDoc.IsDisposed) DocumentExtension.CloseAndDiscard(versionDoc); } catch { }
            try { if (doc != null && !doc.IsDisposed) DocumentExtension.CloseAndDiscard(doc); } catch (Exception ex) { Logger.Warn("selftest: close failed: " + ex.Message); }
            try { if (original != null && !original.IsDisposed) dm.MdiActiveDocument = original; } catch { }

            if (before != null)
            {
                var testDoc = doc;
                Check("Bản vẽ của cậu không bị đụng tới", () =>
                {
                    if (testDoc != null && !testDoc.IsDisposed)
                        return new SelfTestRules.Verdict(false, "bản vẽ tạm chưa đóng được — đóng tab đó, chọn không lưu");
                    if (original == null || original.IsDisposed)
                        return new SelfTestRules.Verdict(false, "bản vẽ đang mở lúc bắt đầu đã bị đóng trong lúc kiểm tra");
                    var after = DrawingState.Of(original);
                    return SelfTestRules.CheckUntouched(before.Name, before.Handseed, after.Handseed, before.Count, after.Count, before.Dbmod, after.Dbmod);
                });
            }
            return Report(steps, dir);
        }

        /// <summary>
        /// The checks that need no drawing: the right build for this AutoCAD, AutoCAD's palette theme, the
        /// status bar icon, then what the controller knows (styles, compact mode, queue, skills, connection).
        /// </summary>
        private static void UiChecks(Action<string, Func<SelfTestRules.Verdict>> check,
            Action<string, Func<List<(string name, bool ok, string detail)>>> collect)
        {
            check("Bản plugin đúng với đời AutoCAD", () =>
            {
                var asm = typeof(SelfTest).Assembly;
                string framework = asm.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
                string path = "";
                try { path = asm.Location; } catch { }
                var acad = AcApp.Version;
                return SelfTestRules.CheckBuild(acad.Major, acad.Minor, framework, Environment.Version.Major, path);
            });

            check("Theme palette của AutoCAD (màu + font cho khung chat)", () =>
            {
                var info = AcadTheme.Read();
                bool variableDark = Theme.AutoCadIsDark();
                if (info == null)
                    return SelfTestRules.CheckTheme(false, null, variableDark, 0, AcadTheme.ColorCount, null, 0, 0, AcadTheme.IsSubscribed);
                double contrast = 0;
                if (info.Brushes.TryGetValue(Theme.Fg, out var fg) && info.Brushes.TryGetValue(Theme.PanelBg, out var bg)
                    && fg is System.Windows.Media.SolidColorBrush f && bg is System.Windows.Media.SolidColorBrush b)
                    contrast = SelfTestRules.Contrast(f.Color.R, f.Color.G, f.Color.B, b.Color.R, b.Color.G, b.Color.B);
                return SelfTestRules.CheckTheme(true, info.IsDark, variableDark, info.Brushes.Count, AcadTheme.ColorCount,
                    info.Font?.Source, info.FontSize, contrast, AcadTheme.IsSubscribed);
            });

            check("Icon ClaudeCAD trên thanh trạng thái", StatusTray.SelfCheck);

            collect("Kiểm tra khung chat", () => PluginApp.Controller.UiSelfChecks());
            collect("Kiểm tra kết nối", () => PluginApp.Controller.Diagnostics());
        }

        /// <summary>What must not change in the user's drawing while the test runs on another one.</summary>
        private sealed class DrawingState
        {
            public string Name;
            public string Handseed;
            public long Count = -1;
            public int Dbmod = -1;

            public static DrawingState Of(Document d)
            {
                if (d == null || d.IsDisposed) return null;
                var db = d.Database;
                var s = new DrawingState { Name = d.Name, Handseed = db.Handseed.ToString() };
                try
                {
                    // ids only, nothing is opened for write: model space and every layout
                    long n = 0;
                    using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                    {
                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        foreach (ObjectId id in bt)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                            if (!btr.IsLayout) continue;
                            foreach (ObjectId _ in btr) n++;
                        }
                        tr.Commit();
                    }
                    s.Count = n;
                }
                catch (Exception ex) { Logger.Warn("selftest: objects not counted: " + ex.Message); }
                try
                {
                    // DBMOD belongs to the active drawing: only readable while this one is in front
                    if (AcApp.DocumentManager.MdiActiveDocument == d) s.Dbmod = Convert.ToInt32(AcApp.GetSystemVariable("DBMOD"), CultureInfo.InvariantCulture);
                }
                catch { }
                return s;
            }
        }

        /// <summary>Test folders of earlier runs (kept when something failed) older than a week.</summary>
        private static void CleanOldRuns()
        {
            try
            {
                foreach (var d in Directory.GetDirectories(Path.GetTempPath(), "ClaudeCAD-selftest-*"))
                    if (DateTime.Now - Directory.GetLastWriteTime(d) > TimeSpan.FromDays(7))
                        try { Directory.Delete(d, true); } catch { }
            }
            catch { }
        }

        private static string Report(List<Step> steps, string dir)
        {
            var sb = new StringBuilder();
            sb.AppendLine(ProductInfo.DisplayName + " — tự kiểm tra " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            sb.AppendLine(new string('=', 60));
            foreach (var s in steps)
                sb.AppendLine($"{(s.Ok ? "✓" : "✗")} {s.Name}{(s.Ms > 0 ? $" ({s.Ms} ms)" : "")}: {s.Detail}");
            int bad = steps.Count(s => !s.Ok);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            sb.AppendLine(new string('=', 60));
            sb.AppendLine(bad == 0 ? "Tất cả đều đạt." : $"{bad} mục chưa đạt — gửi file này (và log CLAUDECHATLOG) cho người phát triển.");
            if (bad == 0)
            {
                // nothing to investigate: keep only the picture (next to the report), drop the test drawings
                try
                {
                    var png = Path.Combine(dir, "capture.png");
                    if (File.Exists(png))
                    {
                        Directory.CreateDirectory(Logger.LogDirectory);
                        var keep = Path.Combine(Logger.LogDirectory, "selftest-" + stamp + ".png");
                        File.Copy(png, keep, true);
                        sb.AppendLine("Ảnh chụp thử: " + keep);
                    }
                    Directory.Delete(dir, true);
                }
                catch (Exception ex) { Logger.Warn("selftest folder not removed: " + ex.Message); sb.AppendLine("Thư mục thử: " + dir); }
            }
            else sb.AppendLine("Thư mục thử (giữ lại để kiểm tra): " + dir);
            var file = Path.Combine(Logger.LogDirectory, "selftest-" + stamp + ".txt");
            try
            {
                Directory.CreateDirectory(Logger.LogDirectory);
                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
                Process.Start("notepad.exe", "\"" + file + "\"");
            }
            catch (Exception ex) { Logger.Warn("selftest report not written: " + ex.Message); }
            return $"{steps.Count - bad}/{steps.Count} mục đạt. Báo cáo: {file}";
        }
    }
}
